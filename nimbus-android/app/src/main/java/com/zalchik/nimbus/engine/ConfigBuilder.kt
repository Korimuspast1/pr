package com.zalchik.nimbus.engine

import com.zalchik.nimbus.data.Balancer
import com.zalchik.nimbus.data.RoutePreset
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.bool
import com.zalchik.nimbus.util.int
import com.zalchik.nimbus.util.hostOf
import com.zalchik.nimbus.util.obj
import com.zalchik.nimbus.util.objNonEmpty
import com.zalchik.nimbus.util.objOrNull
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.splitUrl
import com.zalchik.nimbus.util.str
import com.zalchik.nimbus.util.strings
import com.zalchik.nimbus.util.stringify
import org.json.JSONArray
import org.json.JSONObject

/** A server or a balancer the user picked — what a config is built for. */
sealed class Selected {
    abstract val id: String
    abstract val name: String

    data class OfServer(val server: Server) : Selected() {
        override val id: String get() = server.id
        override val name: String get() = server.name
    }

    data class OfBalancer(val balancer: Balancer, val members: List<Server>) : Selected() {
        override val id: String get() = balancer.id
        override val name: String get() = balancer.name
    }
}

/**
 * The config that actually runs: one sing-box config with a TUN inbound, Nimbus' DNS and routing,
 * and the selected server (or a balancer's members as a urltest group) behind it.
 *
 * Desktop builds two configs — sing-box in front, Xray behind a local SOCKS bridge — because a
 * provider's Xray config has to run in Xray. A phone carries one core, so everything is sing-box:
 * provider configs are translated (see [Xray]) and the bridge disappears.
 */
object ConfigBuilder {
    const val TUN_TAG = "tun-in"
    const val TUN_NAME = "Nimbus"
    const val TUN_GATEWAY = "172.19.0.1"
    const val TUN_ADDRESS4 = "$TUN_GATEWAY/24"
    const val TUN_ADDRESS6 = "fdfe:dcba:9876::1/96"

    const val DIRECT = "direct"
    const val BLOCK = "block"
    const val PROXY = "proxy"

    const val DNS_REMOTE = "dns-remote"
    const val DNS_DIRECT = "dns-direct"
    const val DNS_LOCAL = "dns-local"
    const val DNS_FAKE = "dns-fake"

    /** FakeIP ranges, as desktop's: not fc00::/18, which the private-range bypass would leak. */
    const val FAKE_RANGE4 = "198.18.0.0/15"
    const val FAKE_RANGE6 = "2001:db8::/32"

    /** What "Local networks direct" keeps out of the tunnel entirely. */
    val PRIVATE_RANGES = listOf(
        "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "169.254.0.0/16", "224.0.0.0/4",
        "fc00::/7", "fe80::/10", "ff00::/8",
    )

    /** Public resolvers that also answer DoH on their IP: plain UDP to them becomes DoH. */
    val DOH_IPS = setOf(
        "1.1.1.1", "1.0.0.1", "1.1.1.2", "1.0.0.2", "1.1.1.3", "1.0.0.3", "8.8.8.8", "8.8.4.4",
        "9.9.9.9", "149.112.112.112", "94.140.14.14", "94.140.15.15",
    )

    private val SB_LOG = mapOf("error" to "error", "warning" to "warn", "info" to "info", "debug" to "debug")

    data class Built(
        val config: JSONObject,
        /** Outbound tags whose traffic counts as "through the VPN". */
        val proxyTags: List<String>,
        /** The group the user can switch nodes in (a balancer, or a provider's own). */
        val groupTag: String? = null,
        /** Why parts of the server can't run on a phone, shown before connecting. */
        val unsupported: List<String> = emptyList(),
    ) {
        val json: String get() = stringify(config, 2)
    }

    // -- entry points ---------------------------------------------------------------------------

    fun build(selected: Selected, settings: Settings, ipv6: Boolean): Built = when (selected) {
        is Selected.OfServer -> buildServer(selected.server, settings, ipv6)
        is Selected.OfBalancer -> buildBalancer(selected.balancer, selected.members, settings, ipv6)
    }

    /** One server: a share link, or a provider's full Xray config translated to sing-box. */
    fun buildServer(server: Server, settings: Settings, ipv6: Boolean): Built {
        val config = server.config
        return if (config != null) {
            buildFullConfig(server, config, settings, ipv6)
        } else {
            buildLink(server, settings, ipv6)
        }
    }

    private fun buildLink(server: Server, settings: Settings, ipv6: Boolean): Built {
        val parsed = Links.parseLink(server.link, PROXY)
        Links.applyMultiplex(parsed.outbound, settings.mux, settings.muxConcurrency)
        Links.applyFragment(parsed.outbound, settings.fragment && parsed.outbound.str("type") !in setOf("hysteria", "hysteria2", "wireguard"))
        val ruleSets = Xray.RuleSets()
        val outbounds = mutableListOf(parsed.outbound)
        val endpoints = mutableListOf<JSONObject>()
        parsed.endpoint?.let { endpoints.add(it) }
        outbounds.add(obj("type" to "direct", "tag" to DIRECT))
        outbounds.add(obj("type" to "block", "tag" to BLOCK))

        val rules = mutableListOf<JSONObject>()
        rules += sniffRules(settings)
        rules += presetRules(settings.activePreset, ruleSets, PROXY)
        rules += routingRules(settings, ruleSets, PROXY)
        rules += obj("network" to JSONArray(listOf("tcp", "udp")), "action" to "route", "outbound" to PROXY)

        val domains = Links.serverDomains(listOf(parsed.outbound), endpoints)
        val config = assemble(settings, ipv6, outbounds, endpoints, rules, ruleSets, domains, ru = ruDirect(server, settings))
        return Built(
            config,
            listOf(PROXY),
            unsupported = listOfNotNull(parsed.unsupported),
        )
    }

    /** A provider's config, translated. Its own rules stay unless the user turned them off. */
    private fun buildFullConfig(server: Server, source: JSONObject, settings: Settings, ipv6: Boolean): Built {
        val converted = Xray.convert(source, "3m")
        val ruleSets = converted.ruleSets
        val keep = keepProviderRouting(server, settings)

        val outbounds = converted.outbounds.toMutableList()
        if (outbounds.none { it.str("tag") == DIRECT }) outbounds.add(obj("type" to "direct", "tag" to DIRECT))
        if (outbounds.none { it.str("tag") == BLOCK }) outbounds.add(obj("type" to "block", "tag" to BLOCK))
        val mainTag = outbounds.firstOrNull { it.str("type") !in setOf("direct", "block") }?.str("tag") ?: PROXY
        val groupTag = converted.groups.firstOrNull()?.str("tag")

        // Settings the provider's config can't carry: multiplexing and TLS fragmentation are
        // Nimbus' own, and are applied to the nodes the provider gave (as on desktop, where they
        // are applied to link servers only and a full config runs as shipped).
        for (outbound in outbounds) {
            Links.applyMultiplex(outbound, settings.mux, settings.muxConcurrency)
            Links.applyFragment(outbound, settings.fragment)
        }

        val proxyTag = groupTag ?: mainTag
        val rules = mutableListOf<JSONObject>()
        rules += sniffRules(settings)
        if (keep) {
            // The provider's rules first (its RU-direct list, its split routing), then the LAN
            // bypass, which a provider's config never has and a phone always needs.
            rules += presetRules(settings.activePreset, ruleSets, proxyTag)
            rules += converted.rules
            if (settings.bypassPrivate) {
                rules.add(0, obj("ip_is_private" to true, "action" to "route", "outbound" to DIRECT))
            }
        } else {
            rules += presetRules(settings.activePreset, ruleSets, proxyTag)
            rules += routingRules(settings, ruleSets, proxyTag)
            rules += obj("network" to JSONArray(listOf("tcp", "udp")), "action" to "route", "outbound" to proxyTag)
        }

        val domains = Links.serverDomains(outbounds, converted.endpoints)
        val ru = ruDirect(server, settings)
        val config = assemble(
            settings, ipv6, outbounds, converted.endpoints, rules, ruleSets, domains, ru,
            converted.directDomains, converted.directRuleSets, converted.domainStrategy,
        )
        val proxyTags = outbounds.map { it.str("tag") }.filter { it.isNotEmpty() && it != DIRECT && it != BLOCK }
        return Built(config, proxyTags, groupTag, converted.unsupported.distinct())
    }

    /**
     * A balancer: its members as one urltest group, so the core itself keeps to the node that
     * answers fastest. Desktop does the same with an Xray leastPing balancer + burstObservatory;
     * the member tags are named the same way (nimbus-auto-<member>-<tag>), so the notification
     * "the balancer moved to another server" reads the same and maps back to a server.
     */
    fun buildBalancer(balancer: Balancer, members: List<Server>, settings: Settings, ipv6: Boolean): Built {
        val converted = Balancers.convert(members, balancer, settings)
        val ruleSets = converted.ruleSets
        val outbounds = converted.outbounds.toMutableList()
        outbounds.add(obj("type" to "direct", "tag" to DIRECT))
        outbounds.add(obj("type" to "block", "tag" to BLOCK))
        val proxyTag = converted.groupTag ?: PROXY

        val rules = mutableListOf<JSONObject>()
        rules += sniffRules(settings)
        rules += presetRules(settings.activePreset, ruleSets, proxyTag)
        if (converted.providerRules.isNotEmpty() && keepProviderRoutingFor(members, settings)) {
            rules += converted.providerRules
            if (settings.bypassPrivate) {
                rules.add(0, obj("ip_is_private" to true, "action" to "route", "outbound" to DIRECT))
            }
        } else {
            rules += routingRules(settings, ruleSets, proxyTag)
        }
        rules += obj("network" to JSONArray(listOf("tcp", "udp")), "action" to "route", "outbound" to proxyTag)

        val domains = Links.serverDomains(outbounds, converted.endpoints)
        val config = assemble(
            settings, ipv6, outbounds, converted.endpoints, rules, ruleSets, domains,
            ru = converted.ruDirect,
            directDomains = converted.directDomains,
            directRuleSets = converted.directRuleSets,
            domainStrategy = converted.domainStrategy,
        )
        val proxyTags = outbounds.map { it.str("tag") }.filter { it.isNotEmpty() && it != DIRECT && it != BLOCK }
        return Built(config, proxyTags, converted.groupTag, converted.unsupported.distinct())
    }

    // -- the pieces ------------------------------------------------------------------------------

    /**
     * Whether the provider's own rules stay. As on desktop: they do by default, and a "vpn"
     * preset replaces them while a "bypass" one can sit on top.
     */
    fun keepProviderRouting(server: Server, settings: Settings): Boolean {
        val preset = settings.activePreset
        var keep = settings.useProviderRouting
        if (preset != null) {
            keep = !preset.isVpnOnly && preset.provider && Xray.providerRouting(server.config) != null
        }
        return keep
    }

    /** Provider rules apply to a balancer only when every member comes from one subscription. */
    fun keepProviderRoutingFor(members: List<Server>, settings: Settings): Boolean {
        val preset = settings.activePreset
        if (preset != null && (preset.isVpnOnly || !preset.provider)) return false
        if (!settings.useProviderRouting) return false
        return Balancers.providerSource(members) != null
    }

    /**
     * Whether Russian sites go past the VPN, so their names are better resolved directly —
     * desktop's ru_direct().
     */
    fun ruDirect(server: Server, settings: Settings): Boolean {
        if (settings.routingMode == "bypass_ru" && !(server.isFullConfig && settings.useProviderRouting)) return true
        val provider = Xray.providerRouting(server.config)
        return provider == true && settings.useProviderRouting
    }

    private fun sniffRules(settings: Settings): List<JSONObject> {
        val rules = mutableListOf<JSONObject>()
        if (settings.sniffing) {
            rules.add(
                objNonEmpty(
                    "action" to "sniff",
                    "sniffer" to JSONArray(listOf("tls", "http", "quic", "dns", "stun")),
                    "timeout" to "1s",
                ),
            )
        }
        // Port-53 traffic is the tunnel's own DNS: answer it here instead of proxying it.
        rules.add(obj("protocol" to "dns", "action" to "hijack-dns"))
        return rules
    }

    /**
     * Nimbus' own rules: local networks direct, ads blocked, Russian sites direct. The phone's
     * version of routing_rules(), with sing-box's rule sets where Xray had geoip/geosite (the
     * mobile core has no .dat files: it downloads the compiled sets, once, over the direct
     * outbound).
     */
    fun routingRules(settings: Settings, ruleSets: Xray.RuleSets, proxyTag: String): List<JSONObject> {
        val rules = mutableListOf<JSONObject>()
        if (settings.bypassPrivate) {
            rules.add(obj("ip_is_private" to true, "action" to "route", "outbound" to DIRECT))
        }
        if (settings.blockAds) {
            ruleSets.geosite("category-ads-all")?.let { tag ->
                rules.add(obj("rule_set" to JSONArray(listOf(tag)), "action" to "reject"))
            }
        }
        if (settings.routingMode == "bypass_ru") {
            ruleSets.geosite("category-ru")?.let { tag ->
                rules.add(obj("rule_set" to JSONArray(listOf(tag)), "action" to "route", "outbound" to DIRECT))
            }
            rules.add(
                obj(
                    "domain_suffix" to JSONArray(listOf("ru", "su", "xn--p1ai")),
                    "action" to "route",
                    "outbound" to DIRECT,
                ),
            )
            ruleSets.geoip("ru")?.let { tag ->
                rules.add(obj("rule_set" to JSONArray(listOf(tag)), "action" to "route", "outbound" to DIRECT))
            }
            // The direct resolver's own address must not go through the tunnel.
            val directIp = dnsIp(settings.directDns)
            if (directIp != null) {
                rules.add(
                    obj(
                        "ip_cidr" to JSONArray(listOf(if ('/' in directIp) directIp else "$directIp/32")),
                        "action" to "route",
                        "outbound" to DIRECT,
                    ),
                )
            }
        }
        return rules
    }

    /**
     * A preset's rules. Sites are matched as on desktop (a bare domain means it and its
     * subdomains, geoip:/geosite: become rule sets, an IP or a CIDR stays one); apps become
     * package names, which is how a phone names programs.
     */
    fun presetRules(preset: RoutePreset?, ruleSets: Xray.RuleSets, proxyTag: String): List<JSONObject> {
        if (preset == null) return emptyList()
        val target = if (preset.isVpnOnly) {
            obj("action" to "route", "outbound" to proxyTag)
        } else {
            obj("action" to "route", "outbound" to DIRECT)
        }
        val rules = mutableListOf<JSONObject>()
        val domains = mutableListOf<String>()
        val suffixes = mutableListOf<String>()
        val keywords = mutableListOf<String>()
        val regexes = mutableListOf<String>()
        val ips = mutableListOf<String>()
        val sets = mutableListOf<String>()
        var privateIps = false
        for (line in preset.sites) {
            val entry = siteEntry(line, ruleSets) ?: continue
            when (entry.first) {
                "domain" -> domains.add(entry.second)
                "domain_keyword" -> keywords.add(entry.second)
                "domain_regex" -> regexes.add(entry.second)
                "domain_suffix" -> suffixes.add(entry.second)
                "ip" -> ips.add(entry.second)
                "ip_is_private" -> privateIps = true
                "rule_set" -> sets.add(entry.second)
            }
        }
        fun rule(match: JSONObject): JSONObject {
            for (name in target.keys()) match.put(name, target.opt(name))
            return match
        }
        if (preset.packages.isNotEmpty()) {
            rules.add(rule(obj("package_name" to JSONArray(preset.packages))))
        }
        if (domains.isNotEmpty()) rules.add(rule(obj("domain" to JSONArray(domains))))
        if (suffixes.isNotEmpty()) rules.add(rule(obj("domain_suffix" to JSONArray(suffixes))))
        if (keywords.isNotEmpty()) rules.add(rule(obj("domain_keyword" to JSONArray(keywords))))
        if (regexes.isNotEmpty()) rules.add(rule(obj("domain_regex" to JSONArray(regexes))))
        if (sets.isNotEmpty()) rules.add(rule(obj("rule_set" to JSONArray(sets.distinct()))))
        if (privateIps) rules.add(rule(obj("ip_is_private" to true)))
        if (ips.isNotEmpty()) rules.add(rule(obj("ip_cidr" to JSONArray(ips))))
        if (preset.isVpnOnly) {
            // Everything the preset didn't claim goes past the VPN, as desktop's tail rule does.
            rules.add(
                obj(
                    "network" to JSONArray(listOf("tcp", "udp")),
                    "action" to "route",
                    "outbound" to DIRECT,
                ),
            )
        }
        return rules
    }

    private val IPV4 = Regex("\\d{1,3}(\\.\\d{1,3}){3}(/\\d{1,2})?")
    private val IPV6 = Regex("[0-9a-fA-F]*:[0-9a-fA-F:.]*(/\\d{1,3})?")

    /**
     * ("domain" | "domain_suffix" | "domain_keyword" | "domain_regex" | "ip" | "rule_set",
     * matcher) for one line of a preset's site list: example.com, https://example.com/page,
     * *.example.com, 1.2.3.0/24, geosite:youtube, geoip:ru …
     */
    fun siteEntry(entry: String, ruleSets: Xray.RuleSets? = null): Pair<String, String>? {
        var value = entry.trim()
        val low = value.lowercase()
        if (value.isEmpty() || value.startsWith("#")) return null
        if (low.startsWith("geoip:")) {
            val code = value.substring(6)
            // Xray's geoip:private is sing-box's ip_is_private: a matcher of its own, not a CIDR.
            // (There is no geoip-private rule set to point at either.)
            if (code.equals("private", true)) return "ip_is_private" to "true"
            val tag = ruleSets?.geoip(code) ?: return null
            return "rule_set" to tag
        }
        if (low.startsWith("geosite:")) {
            val tag = ruleSets?.geosite(value.substring(8)) ?: return null
            return "rule_set" to tag
        }
        if (low.startsWith("full:")) return "domain" to value.substring(5)
        if (low.startsWith("keyword:")) return "domain_keyword" to value.substring(8)
        if (low.startsWith("regexp:")) return "domain_regex" to value.substring(7)
        if (low.startsWith("domain:")) return "domain_suffix" to value.substring(7)
        if ("://" in value) value = splitUrl(value).host
        if (IPV4.matches(value) || IPV6.matches(value.trim('[', ']'))) {
            return "ip" to (if ('/' in value) value else "$value/32")
        }
        value = value.split("/")[0].split(":")[0].removePrefix("*.").trimEnd('.').lowercase()
        if ('.' in value && ' ' !in value) return "domain_suffix" to value
        return null
    }

    /** IP of a DNS server address given literally (8.8.8.8, https://1.1.1.1/dns-query). */
    fun dnsIp(address: String): String? {
        val host = if ("://" in address) splitUrl(address).host else address.split(":")[0]
        if (host.isEmpty()) return null
        return if (isIpLiteral(host)) host else null
    }

    fun isIpLiteral(value: String): Boolean {
        val text = value.trim('[', ']')
        if (text.isEmpty()) return false
        if (':' in text) return text.matches(Regex("[0-9a-fA-F:.]+"))
        val parts = text.split('.')
        return parts.size == 4 && parts.all { (it.toIntOrNull() ?: -1) in 0..255 }
    }

    /**
     * A sing-box DNS server from Nimbus' address formats (1.1.1.1, https://1.1.1.1/dns-query,
     * tcp://8.8.8.8, tls://…, quic+local://dns.adguard.com, localhost) — desktop's _sb_dns().
     */
    fun sbDns(address: String, tag: String, detour: String?, throughProxy: Boolean): JSONObject {
        val value = address.trim()
        if (value.lowercase() in setOf("localhost", "local")) return obj("type" to "local", "tag" to tag)
        var scheme = "udp"
        var rest = value
        if ("://" in value) {
            scheme = value.substringBefore("://").lowercase().replace("+local", "")
            rest = value.substringAfter("://")
        }
        val parts = splitUrl("x://$rest")
        val host = parts.host
        var path = parts.path
        var port = parts.port
        // UDP through a proxy is unreliable (Throne: "overwrite remote dns to TCP based"): known
        // resolvers become DoH, the rest TCP.
        if (throughProxy && (scheme == "udp" || scheme == "quic")) {
            scheme = if (host in DOH_IPS) "https" else "tcp"
            if (scheme == "https") {
                path = "/dns-query"
                port = 0
            }
        }
        val kind = when (scheme) {
            "https" -> "https"
            "h3" -> "h3"
            "tls" -> "tls"
            "quic" -> "quic"
            "tcp" -> "tcp"
            else -> "udp"
        }
        val out = obj("type" to kind, "tag" to tag)
        if (host.isNotEmpty()) out.put("server", host)
        if (port > 0) out.put("server_port", port)
        if (kind == "https" || kind == "h3") out.put("path", path.ifEmpty { "/dns-query" })
        if (host.isNotEmpty() && !isIpLiteral(host)) out.put("domain_resolver", DNS_LOCAL)
        if (!detour.isNullOrEmpty()) out.put("detour", detour)
        return out
    }

    fun tunInbound(settings: Settings, ipv6: Boolean): JSONObject {
        val exclude = mutableListOf("127.0.0.0/8", "255.255.255.255/32")
        if (settings.bypassPrivate) {
            exclude += PRIVATE_RANGES.filter { ipv6 || ':' !in it }
        }
        return objNonEmpty(
            "type" to "tun",
            "tag" to TUN_TAG,
            "interface_name" to TUN_NAME,
            "address" to JSONArray(listOf(TUN_ADDRESS4) + if (ipv6) listOf(TUN_ADDRESS6) else emptyList()),
            "mtu" to settings.tunMtu,
            "auto_route" to true,
            // A kill switch on Android is the system's Always-on VPN + "block connections without
            // VPN"; strict_route is what the desktop needs and the phone ignores.
            "strict_route" to settings.killSwitch,
            "route_exclude_address" to JSONArray(exclude),
        )
    }

    private fun assemble(
        settings: Settings,
        ipv6: Boolean,
        outbounds: List<JSONObject>,
        endpoints: List<JSONObject>,
        rules: List<JSONObject>,
        ruleSets: Xray.RuleSets,
        serverDomains: List<String>,
        ru: Boolean,
        directDomains: List<String> = emptyList(),
        directRuleSets: List<String> = emptyList(),
        domainStrategy: String = settings.domainStrategy,
    ): JSONObject {
        val proxyDetour = outbounds.firstOrNull { it.str("tag") == PROXY }?.str("tag")
            ?: outbounds.firstOrNull { it.str("type") !in setOf("direct", "block", "urltest", "selector") }?.str("tag")
        val servers = JSONArray()
        servers.put(sbDns(settings.remoteDns, DNS_REMOTE, proxyDetour, true))
        servers.put(sbDns(settings.directDns, DNS_DIRECT, null, false))
        servers.put(obj("type" to "local", "tag" to DNS_LOCAL))

        val dnsRules = JSONArray()
        if (serverDomains.isNotEmpty()) {
            dnsRules.put(obj("domain" to JSONArray(serverDomains), "action" to "route", "server" to DNS_DIRECT))
        }
        if (ru) {
            dnsRules.put(
                obj(
                    "domain_suffix" to JSONArray(listOf("ru", "su", "xn--p1ai")),
                    "action" to "route",
                    "server" to DNS_DIRECT,
                ),
            )
        }
        if (directDomains.isNotEmpty()) {
            dnsRules.put(obj("domain_suffix" to JSONArray(directDomains.distinct()), "action" to "route", "server" to DNS_DIRECT))
        }
        if (directRuleSets.isNotEmpty()) {
            dnsRules.put(obj("rule_set" to JSONArray(directRuleSets.distinct()), "action" to "route", "server" to DNS_DIRECT))
        }
        if (settings.fakeDns) {
            servers.put(
                obj(
                    "type" to "fakeip",
                    "tag" to DNS_FAKE,
                    "inet4_range" to FAKE_RANGE4,
                    "inet6_range" to FAKE_RANGE6,
                ),
            )
            // TTL 1 s, not sing-box's 600: apps cached fake addresses for ten minutes or more, and
            // they lead nowhere once the tunnel is down.
            dnsRules.put(
                obj(
                    "query_type" to JSONArray(listOf("A", "AAAA")),
                    "action" to "route",
                    "server" to DNS_FAKE,
                    "rewrite_ttl" to 1,
                ),
            )
        }

        val log = if (settings.logLevel == "none") {
            obj("disabled" to true)
        } else {
            obj("level" to (SB_LOG[settings.logLevel] ?: "warn"))
        }

        // sing-box asks the platform who owns a connection only when a rule needs it, so say so
        // outright whenever package names take part in routing — otherwise the rules silently match
        // nothing (route/router.go: needFindProcess).
        val findProcess = settings.perAppEnabled ||
            settings.routePresets.any { it.packages.isNotEmpty() }

        val route = objNonEmpty(
            "rules" to JSONArray(rules),
            "rule_set" to ruleSets.toJsonArray().takeIf { it.length() > 0 },
            "final" to DIRECT,
            "find_process" to true.takeIf { findProcess },
            "auto_detect_interface" to true,
            "default_domain_resolver" to resolverFor(domainStrategy),
        )

        val config = JSONObject()
        config.put("log", log)
        config.put(
            "dns",
            objNonEmpty(
                "servers" to servers,
                "rules" to dnsRules.takeIf { it.length() > 0 },
                "final" to DNS_REMOTE,
                "reverse_mapping" to true,
                "strategy" to strategyFor(domainStrategy),
            ),
        )
        config.put("inbounds", JSONArray(listOf(tunInbound(settings, ipv6))))
        config.put("outbounds", JSONArray(outbounds))
        if (endpoints.isNotEmpty()) config.put("endpoints", JSONArray(endpoints))
        config.put("route", route)
        return config
    }

    /** Xray's domainStrategy as the sing-box resolver it comes closest to. */
    private fun resolverFor(domainStrategy: String): Any = when (domainStrategy) {
        "IPIfNonMatch", "IPOnDemand" -> obj("server" to DNS_DIRECT, "strategy" to "prefer_ipv4")
        else -> DNS_DIRECT
    }

    private fun strategyFor(domainStrategy: String): String? = when (domainStrategy) {
        "IPIfNonMatch", "IPOnDemand" -> "prefer_ipv4"
        else -> null
    }

    /** A config the core itself accepts, or the reason it doesn't. */
    fun check(config: JSONObject): String? = try {
        io.nekohasekai.libbox.Libbox.checkConfig(stringify(config))
        null
    } catch (e: Exception) {
        e.message ?: e.toString()
    }

    /** Outbound tags a config uses, for diagnostics and the connection sheet. */
    fun tagsOf(config: JSONObject): List<String> =
        config.arrOrEmpty("outbounds").objects().map { it.str("tag") }.filter { it.isNotEmpty() }

    /** The urltest/selector groups a config carries: what the node switcher lists. */
    fun groupsOf(config: JSONObject): List<String> =
        config.arrOrEmpty("outbounds").objects()
            .filter { it.str("type") == "urltest" || it.str("type") == "selector" }
            .map { it.str("tag") }

    /** Every address a config dials, for the exit-IP and latency screens. */
    fun remoteAddresses(config: JSONObject): List<String> {
        val out = mutableListOf<String>()
        for (outbound in config.arrOrEmpty("outbounds").objects()) {
            val server = outbound.str("server")
            val port = outbound.int("server_port")
            if (server.isNotEmpty()) out.add(if (port > 0) "$server:$port" else server)
        }
        for (endpoint in config.arrOrEmpty("endpoints").objects()) {
            for (peer in endpoint.arrOrEmpty("peers").objects()) {
                val address = peer.str("address")
                if (address.isNotEmpty()) out.add("$address:${peer.int("port")}")
            }
        }
        return out.distinct()
    }

    /** The addresses a direct latency probe dials for a server. */
    fun probeAddresses(server: Server): List<String> {
        Links.probeAddress(server)?.let { return listOf(it) }
        val config = server.config ?: return emptyList()
        return Xray.convert(config).outbounds.mapNotNull { outbound ->
            val host = outbound.str("server")
            val port = outbound.int("server_port")
            if (host.isEmpty() || port <= 0) null else "$host:$port"
        }.distinct()
    }

    /** The host a server dials, for the country flag next to its name. */
    fun hostOfServer(server: Server): String {
        Links.probeAddress(server)?.let { return hostOf(it) }
        val config = server.config ?: return ""
        for (outbound in config.arrOrEmpty("outbounds").objects()) {
            if (outbound.str("protocol") in Server.NON_PROXY_PROTOCOLS) continue
            val settings = outbound.objOrNull("settings") ?: continue
            val list = settings.arrOrEmpty("vnext").objects() + settings.arrOrEmpty("servers").objects()
            val address = list.firstOrNull { it.str("address").isNotEmpty() }?.str("address")
                ?: settings.str("address")
            if (address.isNotEmpty()) return hostOf(address)
            val peers = settings.arrOrEmpty("peers").objects()
            if (peers.isNotEmpty()) return hostOf(peers[0].str("endpoint"))
        }
        return ""
    }

    /** The member index a balancer node tag names — desktop's member_of(). */
    fun memberIndex(tag: String): Int? {
        if (!tag.startsWith(Balancers.PREFIX)) return null
        return tag.removePrefix(Balancers.PREFIX).substringBefore('-').toIntOrNull()
    }

    /** Whether a config fakes DNS answers, sniffs, and blocks ads: the connection sheet's rows. */
    fun fakeDnsOf(config: JSONObject): Boolean =
        (config.objOrNull("dns")?.arrOrEmpty("servers")?.objects() ?: emptyList()).any { it.str("type") == "fakeip" }

    fun sniffingOf(config: JSONObject): Boolean =
        (config.objOrNull("route")?.arrOrEmpty("rules")?.objects() ?: emptyList()).any { it.str("action") == "sniff" }

    fun blockAdsOf(config: JSONObject): Boolean =
        (config.objOrNull("route")?.arrOrEmpty("rules")?.objects() ?: emptyList()).any { it.str("action") == "reject" }

    fun bypassPrivateOf(config: JSONObject): Boolean =
        (config.objOrNull("route")?.arrOrEmpty("rules")?.objects() ?: emptyList()).any { it.bool("ip_is_private") }

    /** Whether a config routes Russian sites past the VPN. */
    fun routesRuDirect(config: JSONObject): Boolean =
        (config.objOrNull("route")?.arrOrEmpty("rules")?.objects() ?: emptyList()).any { rule ->
            if (rule.str("outbound") != DIRECT) return@any false
            val suffixes = rule.arrOrEmpty("domain_suffix").strings()
            val sets = rule.arrOrEmpty("rule_set").strings()
            suffixes.any { it == "ru" } || sets.any { it == "geoip-ru" || it == "geosite-category-ru" }
        }

    /** A short human description of the routing a config applies. */
    fun routingSummary(config: JSONObject, settings: Settings): String {
        val preset = settings.activePreset
        return when {
            preset != null && preset.isVpnOnly -> I18n.tr("Через VPN идут только выбранные приложения")
            preset != null -> preset.name
            routesRuDirect(config) -> I18n.tr("Российские сайты напрямую")
            else -> I18n.tr("Весь трафик через VPN")
        }
    }

    /** allowInsecure in a provider config: Xray 26.2.6+ rejects the key, so it never survives. */
    fun dropAllowInsecure(config: JSONObject) {
        com.zalchik.nimbus.util.dropKey(config, "allowInsecure")
    }

    /** A config as text, for run/config.json and the "send to support" sheet. */
    fun dump(config: JSONObject): String = stringify(config, 2)
}
