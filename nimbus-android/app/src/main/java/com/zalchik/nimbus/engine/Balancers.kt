package com.zalchik.nimbus.engine

import com.zalchik.nimbus.data.Balancer
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.listOfStrings
import com.zalchik.nimbus.util.obj
import com.zalchik.nimbus.util.objNonEmpty
import com.zalchik.nimbus.util.objOrNull
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.str
import org.json.JSONArray
import org.json.JSONObject
import kotlin.math.max

/**
 * User balancers: the servers picked from any subscriptions and groups become one config with a
 * urltest group, so the core keeps to the fastest of them and moves new connections without a
 * reconnect.
 *
 * Desktop builds an Xray config with a leastPing balancer plus burstObservatory, and reads the
 * observatory back over Xray's API; the phone hands the same members to sing-box, which measures
 * and switches inside the core and reports the delays over libbox's command client. The member
 * tags keep the desktop's shape (nimbus-auto-<member index>-<their own tag>), so a delay maps
 * back to a server exactly as it does there.
 *
 * Provider rules ("Smart") are kept only when every member comes from the same subscription;
 * otherwise nobody's rules apply and the traffic goes as with "All traffic".
 */
object Balancers {
    const val TAG = "nimbus-auto"
    const val PREFIX = "nimbus-auto-"

    /**
     * Stable mode moves to another node only when it is faster by both of these: small gains
     * aren't worth a new exit IP (sites log you out, ask for captchas), and delays jitter by tens
     * of ms. sing-box's urltest tolerance is an absolute number of milliseconds, so the phone
     * keeps the 50 ms half in the core and the ratio half here, for the "best now" row and the
     * switch notification.
     */
    const val MIN_GAIN_MS = 50
    const val MIN_GAIN_RATIO = 0.3

    private val NON_PROXY = setOf("direct", "block")

    data class Converted(
        val outbounds: MutableList<JSONObject> = mutableListOf(),
        val endpoints: MutableList<JSONObject> = mutableListOf(),
        /** Node tags in member order: what the urltest group tests. */
        val nodeTags: MutableList<String> = mutableListOf(),
        var groupTag: String? = null,
        val providerRules: List<JSONObject> = emptyList(),
        val ruleSets: Xray.RuleSets = Xray.RuleSets(),
        val directDomains: List<String> = emptyList(),
        val directRuleSets: List<String> = emptyList(),
        val domainStrategy: String = "AsIs",
        val ruDirect: Boolean = false,
        val unsupported: MutableList<String> = mutableListOf(),
    )

    private class Member(
        val outbounds: List<JSONObject>,
        val endpoints: List<JSONObject>,
        val nodeTags: List<String>,
        /** The member's own outbound tags -> the tags they got in the balancer. */
        val mapping: Map<String, String>,
    )

    fun convert(members: List<Server>, balancer: Balancer, settings: Settings): Converted {
        val converted = Converted()
        val source = providerSource(members)
        var sourceMapping: Map<String, String> = emptyMap()
        var sourceConfig: Xray.Converted? = null

        for ((index, server) in members.withIndex()) {
            val member = try {
                member(index, server, settings, converted.ruleSets, converted.unsupported)
            } catch (e: LinkError) {
                // A link that no longer parses shouldn't take the whole balancer down.
                converted.unsupported.add("${server.name}: ${e.message ?: "link"}")
                continue
            }
            converted.outbounds += member.outbounds
            converted.endpoints += member.endpoints
            converted.nodeTags += member.nodeTags
            if (server === source) {
                sourceMapping = member.mapping
                sourceConfig = server.config?.let { Xray.convert(it) }
            }
        }

        val group = if (converted.nodeTags.isEmpty()) {
            null
        } else {
            objNonEmpty(
                "type" to "urltest",
                "tag" to TAG,
                "outbounds" to JSONArray(converted.nodeTags),
                "url" to settings.pingUrl,
                "interval" to "${max(10, balancer.interval)}s",
                // Stable keeps a node while it is within the tolerance of the best one; fastest
                // moves as soon as another answers quicker.
                "tolerance" to if (balancer.isStable) MIN_GAIN_MS else 0,
                "idle_timeout" to "5m",
                "interrupt_exist_connections" to false,
            )
        }
        if (group != null) {
            converted.outbounds.add(0, group)
            converted.groupTag = TAG
        }

        val keepProvider = source != null && settings.useProviderRouting &&
            (settings.activePreset == null || (!settings.activePreset!!.isVpnOnly && settings.activePreset!!.provider))
        if (keepProvider && source != null) {
            converted.providerRules = providerRules(source.config, sourceMapping, converted)
            converted.directDomains = sourceConfig?.directDomains ?: emptyList()
            converted.directRuleSets = sourceConfig?.directRuleSets ?: emptyList()
            converted.domainStrategy = sourceConfig?.domainStrategy ?: settings.domainStrategy
        } else {
            converted.domainStrategy = settings.domainStrategy
        }

        val providerRu = if (source != null) Xray.providerRouting(source.config) else null
        converted.ruDirect = when {
            settings.routingMode == "bypass_ru" && !(source != null && settings.useProviderRouting) -> true
            providerRu == true && settings.useProviderRouting -> true
            else -> false
        }
        return converted
    }

    /** One member's outbounds under tags of its own, and its old tag -> new tag. */
    private fun member(
        index: Int,
        server: Server,
        settings: Settings,
        ruleSets: Xray.RuleSets,
        unsupported: MutableList<String>,
    ): Member {
        val config = server.config
        if (config == null) {
            val tag = "$PREFIX$index-proxy"
            val parsed = Links.parseLink(server.link, tag)
            if (parsed.unsupported != null) unsupported.add("${server.name}: ${parsed.unsupported}")
            Links.applyMultiplex(parsed.outbound, settings.mux, settings.muxConcurrency)
            Links.applyFragment(
                parsed.outbound,
                settings.fragment && parsed.outbound.str("type") !in setOf("hysteria", "hysteria2"),
            )
            return Member(
                outbounds = listOf(parsed.outbound),
                endpoints = listOfNotNull(parsed.endpoint),
                nodeTags = listOf(tag),
                mapping = mapOf("proxy" to tag),
            )
        }

        val converted = Xray.convert(config)
        unsupported.addAll(converted.unsupported.map { "${server.name}: $it" })

        // Tags other outbounds dial through: chain hops, not nodes of their own.
        val refs = converted.outbounds.mapNotNull { it.str("detour").takeIf { tag -> tag.isNotEmpty() } }.toSet()
        val mapping = LinkedHashMap<String, String>()
        val kept = mutableListOf<JSONObject>()
        for ((j, outbound) in converted.outbounds.withIndex()) {
            val tag = outbound.str("tag").ifEmpty { "out$j" }
            val type = outbound.str("type")
            if (type in NON_PROXY && tag !in refs) continue   // the balancer's own direct / block take their place
            val node = type !in NON_PROXY && tag !in refs
            val newTag = if (node) "$PREFIX$index-$tag" else "n$index-$tag"
            mapping[tag] = newTag
            outbound.put("tag", newTag)
            kept.add(outbound)
        }
        val endpointMapping = LinkedHashMap<String, String>()
        val endpoints = mutableListOf<JSONObject>()
        for (endpoint in converted.endpoints) {
            val tag = endpoint.str("tag")
            val base = tag.removeSuffix("-endpoint")
            val newBase = mapping[base]
            val newTag = if (newBase != null) "$newBase-endpoint" else "n$index-$tag"
            endpointMapping[tag] = newTag
            endpoint.put("tag", newTag)
            endpoints.add(endpoint)
        }
        for (outbound in kept) {
            val detour = outbound.str("detour")
            if (detour.isEmpty()) continue
            val remapped = mapping[detour] ?: endpointMapping[detour]
            if (remapped != null) outbound.put("detour", remapped)
        }
        if (settings.mux) {
            for (outbound in kept) Links.applyMultiplex(outbound, true, settings.muxConcurrency)
        }
        if (settings.fragment) {
            for (outbound in kept) Links.applyFragment(outbound, true)
        }
        val nodeTags = kept.map { it.str("tag") }.filter { it.startsWith(PREFIX) }
        return Member(kept, endpoints, nodeTags, mapping)
    }

    /** The source config's rules, pointed at the balancer group and its direct / block. */
    private fun providerRules(source: JSONObject?, mapping: Map<String, String>, converted: Converted): List<JSONObject> {
        if (source == null) return emptyList()
        val outbounds = source.arrOrEmpty("outbounds").objects()
        val protocols = outbounds.associate { it.str("tag") to it.str("protocol") }
        val ownBalancers = source.objOrNull("routing")?.arrOrEmpty("balancers")?.objects()
            ?.map { it.str("tag") }?.toSet() ?: emptySet()
        val rules = mutableListOf<JSONObject>()
        for (rule in source.objOrNull("routing")?.arrOrEmpty("rules")?.objects() ?: emptyList()) {
            val match = Xray.matchersOf(rule, converted) ?: continue
            val out = rule.str("outboundTag")
            val balancerTag = rule.str("balancerTag")
            val protocol = protocols[out]
            val action: JSONObject? = when {
                balancerTag.isNotEmpty() && balancerTag in ownBalancers ->
                    obj("action" to "route", "outbound" to TAG)

                out.isNotEmpty() && protocol !in listOf("freedom", "blackhole", "dns", "loopback") &&
                    protocols.containsKey(out) -> obj("action" to "route", "outbound" to TAG)

                protocol == "freedom" -> obj("action" to "route", "outbound" to "direct")
                protocol == "blackhole" -> obj("action" to "reject")
                mapping.containsKey(out) -> obj("action" to "route", "outbound" to mapping[out])
                else -> null
            }
            if (action == null) continue
            for (name in action.keys()) match.put(name, action.opt(name))
            rules.add(match)
        }
        return rules
    }

    /**
     * The member whose provider rules the balancer keeps: only when all members come from one
     * subscription, and one of them actually has rules worth keeping.
     */
    fun providerSource(members: List<Server>): Server? {
        val subs = members.map { it.subId }.toSet()
        if (subs.size != 1) return null
        return members.firstOrNull { Xray.providerRouting(it.config) != null }
    }

    /** Node tag -> delay in ms, for the nodes that answered the last check. */
    fun alive(delays: Map<String, Int>): Map<String, Int> =
        delays.filterKeys { it.startsWith(PREFIX) }.filterValues { it > 0 }

    /** The node urltest picks: the alive one with the lowest delay. */
    fun fastest(delays: Map<String, Int>): String? {
        val nodes = alive(delays)
        return nodes.minByOrNull { it.value }?.key
    }

    /**
     * Stay on the current node while it answers, unless another one is much faster — desktop's
     * pick_stable(), used for the "best now" row and to decide whether a switch is worth telling
     * the user about.
     */
    fun pickStable(delays: Map<String, Int>, current: String?): String? {
        val nodes = alive(delays)
        if (nodes.isEmpty()) return current   // nothing answers: no better choice than staying
        val best = nodes.minByOrNull { it.value }?.key ?: return current
        val currentDelay = current?.let { nodes[it] }
        if (currentDelay == null) return best
        val gain = currentDelay - (nodes[best] ?: return current)
        return if (gain >= max(MIN_GAIN_MS.toDouble(), currentDelay * MIN_GAIN_RATIO).toInt()) best else current
    }

    /** The server a balanced node tag belongs to. */
    fun memberOf(tag: String?, members: List<Server>): Server? {
        if (tag == null || !tag.startsWith(PREFIX)) return null
        val index = tag.removePrefix(PREFIX).substringBefore('-').toIntOrNull() ?: return null
        return members.getOrNull(index)
    }

    /** The member the core is on now. */
    fun currentNode(delays: Map<String, Int>, members: List<Server>): Server? =
        memberOf(fastest(delays), members)

    private val FLAG = Regex("[\uD83C\uDDE6-\uD83C\uDDFF]")

    /** A server name without its flag emoji, for notifications that show them as two letters. */
    fun plain(name: String): String = FLAG.replace(name, "").trim()

    /** The delays of a group's members, from what libbox reports. */
    fun delaysOf(items: List<Pair<String, Int>>): Map<String, Int> = items.toMap()

    /** Members of a balancer that a group's items describe, in the group's order. */
    fun membersOf(items: List<String>, members: List<Server>): List<Server> =
        items.mapNotNull { memberOf(it, members) }.distinct()

    /** Whether a group's items are all balancer nodes (a provider's own group has other tags). */
    fun isOwnGroup(items: List<String>): Boolean = items.all { it.startsWith(PREFIX) }

    /** The best delay among a group's items, for the balancer row in the list. */
    fun bestDelay(items: List<Pair<String, Int>>): Int? =
        alive(items.toMap()).values.minOrNull()

    /** The tolerance a mode implies, for the balancer editor's summary. */
    fun tolerance(mode: String): Int = if (mode == Balancer.MODE_STABLE) MIN_GAIN_MS else 0

    /** The interval sing-box will actually use. */
    fun interval(seconds: Int): String = "${max(10, seconds)}s"

    /** The rules a config's group needs, as tags. */
    fun ruleSetsOf(config: JSONObject): List<String> =
        listOfStrings(config.objOrNull("route")?.opt("rule_set"))
}
