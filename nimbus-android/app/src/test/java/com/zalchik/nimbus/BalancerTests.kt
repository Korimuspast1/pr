package com.zalchik.nimbus

import com.zalchik.nimbus.data.Balancer
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.engine.Balancers
import com.zalchik.nimbus.engine.Xray
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.str
import com.zalchik.nimbus.util.strings
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A balancer on a phone is a sing-box urltest group plus Nimbus' own reading of its delays: the
 * node it is on now, whether switching is worth telling the user about, and which member a node
 * tag belongs to. The desktop's BalancerTests, against the phone's shapes.
 */
class BalancerTests {
    private val members = listOf(
        Fixtures.server("a", "🇳🇱 NL", Fixtures.LINKS.getValue("vless-reality")),
        Fixtures.server("b", "🇩🇪 DE", Fixtures.LINKS.getValue("trojan")),
        Fixtures.server("c", "🇫🇮 FI", Fixtures.LINKS.getValue("hy2")),
    )

    private fun delays(vararg pairs: Pair<Int, Int>): Map<String, Int> =
        pairs.associate { (index, ms) -> "${Balancers.PREFIX}$index-node" to ms }

    @Test
    fun nodesAreTaggedApartFromEverythingElse() {
        val converted = Balancers.convert(members, Fixtures.balancer(), Fixtures.settings())
        assertEquals(3, converted.nodeTags.size)
        for ((index, tag) in converted.nodeTags.withIndex()) {
            assertTrue(tag, tag.startsWith("${Balancers.PREFIX}$index-"))
        }
        assertEquals(Balancers.TAG, converted.groupTag)
        // The group is first, so the tunnel's outbound list reads group, nodes, direct, block.
        assertEquals("urltest", converted.outbounds[0].str("type"))
        assertTrue(converted.outbounds.none { it.str("tag") == Balancers.TAG && it !== converted.outbounds[0] })
    }

    @Test
    fun aliveNodesAreTheOnesThatAnswered() {
        val all = delays(0 to 120, 1 to -1, 2 to 80) + ("direct" to 5)
        val alive = Balancers.alive(all)
        assertEquals(setOf("${Balancers.PREFIX}0-node", "${Balancers.PREFIX}2-node"), alive.keys)
        assertEquals("${Balancers.PREFIX}2-node", Balancers.fastest(all))
        assertEquals(members[2], Balancers.currentNode(all, members))
        assertNull(Balancers.fastest(mapOf("direct" to 5)))
        assertNull(Balancers.fastest(delays(0 to -1, 1 to -1)))
    }

    @Test
    fun stableModeKeepsANodeUnlessAnotherIsMuchFaster() {
        val current = "${Balancers.PREFIX}0-node"
        // 20 ms better is within the gain: stay, so connections aren't dropped for nothing.
        assertEquals(current, Balancers.pickStable(delays(0 to 100, 1 to 80), current))
        // 60 ms better is beyond it: switch.
        assertEquals(
            "${Balancers.PREFIX}1-node",
            Balancers.pickStable(delays(0 to 100, 1 to 40), current),
        )
        // A third of the current delay counts too, on slow links where 50 ms means nothing.
        assertEquals(
            "${Balancers.PREFIX}1-node",
            Balancers.pickStable(delays(0 to 1000, 1 to 600), current),
        )
        // The current node died: go to whatever answers.
        assertEquals(
            "${Balancers.PREFIX}1-node",
            Balancers.pickStable(delays(0 to -1, 1 to 400), current),
        )
        // Nothing answers at all: staying is the only choice there is.
        assertEquals(current, Balancers.pickStable(delays(0 to -1, 1 to -1), current))
        assertNull(Balancers.pickStable(emptyMap(), null))
    }

    @Test
    fun toleranceAndIntervalFollowTheSettings() {
        assertEquals(Balancers.MIN_GAIN_MS, Balancers.tolerance(Balancer.MODE_STABLE))
        assertEquals(0, Balancers.tolerance(Balancer.MODE_FASTEST))
        assertEquals("30s", Balancers.interval(30))
        // sing-box's own floor: an interval below it means a check every ten seconds anyway.
        assertEquals("10s", Balancers.interval(1))
        assertEquals("120s", Balancers.interval(120))

        val group = Balancers.convert(members, Fixtures.balancer().copy(interval = 5), Fixtures.settings())
            .outbounds[0]
        assertEquals("10s", group.str("interval"))
        assertEquals(Fixtures.settings().pingUrl, group.str("url"))
        assertEquals(false, group.opt("interrupt_exist_connections"))
    }

    @Test
    fun tagsMapBackToMembers() {
        assertEquals(members[1], Balancers.memberOf("${Balancers.PREFIX}1-node", members))
        assertEquals(members[0], Balancers.memberOf("${Balancers.PREFIX}0-x-y", members))
        assertNull(Balancers.memberOf("${Balancers.PREFIX}9-node", members))
        assertNull(Balancers.memberOf("proxy", members))
        assertNull(Balancers.memberOf(null, members))

        val items = listOf("${Balancers.PREFIX}2-node", "${Balancers.PREFIX}0-node", "${Balancers.PREFIX}0-node")
        assertEquals(listOf(members[2], members[0]), Balancers.membersOf(items, members))
        assertTrue(Balancers.isOwnGroup(items))
        // A provider's own selector lists its own tags, which Nimbus must not claim as its nodes.
        assertFalse(Balancers.isOwnGroup(listOf("proxy", "proxy2")))
        assertEquals(80, Balancers.bestDelay(listOf("${Balancers.PREFIX}0-node" to 120, "${Balancers.PREFIX}1-node" to 80)))
        assertNull(Balancers.bestDelay(listOf("${Balancers.PREFIX}0-node" to -1)))
    }

    @Test
    fun namesLoseTheirFlagsForNotifications() {
        assertEquals("NL", Balancers.plain("🇳🇱 NL"))
        assertEquals("Авто", Balancers.plain("🇷🇺 Авто"))
        assertEquals("plain", Balancers.plain("plain"))
    }

    @Test
    fun oneSubscriptionKeepsTheProvidersRules() {
        val same = listOf(
            Server("p1", "one", subId = "s1", config = Fixtures.PROVIDER),
            Server("p2", "two", subId = "s1", config = Fixtures.PROVIDER),
        )
        assertNotNull(Balancers.providerSource(same))

        val different = listOf(same[0], same[1].copy(id = "p3", subId = "s2"))
        assertNull(Balancers.providerSource(different))

        // A subscription of plain links has no provider rules to keep.
        assertNull(Balancers.providerSource(members))

        val converted = Balancers.convert(same, Fixtures.balancer(), Fixtures.settings())
        assertTrue(converted.providerRules.isNotEmpty())
        // The provider's rules point at tags that still exist after the rename.
        val tags = converted.outbounds.map { it.str("tag") }.toSet()
        for (rule in converted.providerRules) {
            val outbound = rule.str("outbound")
            if (outbound.isNotEmpty()) assertTrue("$outbound in $tags", outbound in tags)
        }
    }

    @Test
    fun providerRulesCanBeTurnedOff() {
        val same = listOf(
            Server("p1", "one", subId = "s1", config = Fixtures.PROVIDER),
            Server("p2", "two", subId = "s1", config = Fixtures.PROVIDER),
        )
        val off = Balancers.convert(
            same,
            Fixtures.balancer(),
            Fixtures.settings { it.useProviderRouting = false },
        )
        assertTrue(off.providerRules.isEmpty())
    }

    @Test
    fun providerRoutingIsDetected() {
        assertEquals(true, Xray.providerRouting(Fixtures.PROVIDER))
        assertNull(Xray.providerRouting(null))
        // A config with no freedom outbound has nothing to bypass to.
        val noDirect = JSONObject(
            """{"outbounds":[{"tag":"proxy","protocol":"vmess","settings":{}}],"routing":{"rules":[]}}"""
        )
        assertNull(Xray.providerRouting(noDirect))
    }

    @Test
    fun xrayConfigBecomesSingBoxOutboundsAndRules() {
        val converted = Xray.convert(Fixtures.PROVIDER)
        val types = converted.outbounds.map { it.str("type") }
        assertTrue(types.toString(), types.contains("vless"))
        assertTrue(types.toString(), types.contains("trojan"))
        // Helpers become Nimbus' own direct and block, not Xray's freedom and blackhole.
        assertTrue(converted.outbounds.any { it.str("type") == "direct" })
        assertTrue(converted.outbounds.any { it.str("type") == "block" })
        assertTrue(converted.rules.isNotEmpty())
        assertFalse(converted.toString().contains("allowInsecure"))

        // The provider's balancer becomes a group the node switcher can drive.
        assertTrue(converted.groups.isNotEmpty())
        val group = converted.groups.first()
        assertTrue(group.str("type") == "urltest" || group.str("type") == "selector")
        val nodes = group.arrOrEmpty("outbounds").strings()
        assertEquals(2, nodes.size)

        // geoip/geosite became downloadable rule sets.
        val tags = converted.ruleSets.tags
        assertTrue(tags.toString(), tags.any { it.startsWith("geoip-") })
        assertTrue(tags.toString(), tags.any { it.startsWith("geosite-") })
        for (set in converted.ruleSets.toJsonArray().objects()) {
            assertEquals("remote", set.str("type"))
            assertTrue(set.str("url").endsWith(".srs"))
        }
    }

    @Test
    fun wireguardInAProviderConfigBecomesAnEndpoint() {
        val config = JSONObject(
            """
            {"outbounds":[
              {"tag":"wg","protocol":"wireguard",
               "settings":{"address":["172.16.0.2/32"],"secretKey":"s","peers":[{"publicKey":"p","allowedIPs":["0.0.0.0/0"]}]}}
            ]}
            """.trimIndent()
        )
        val converted = Xray.convert(config)
        assertEquals(1, converted.endpoints.size)
        assertEquals("wireguard", converted.endpoints[0].str("type"))
        // The outbound that carries traffic is a direct detour to the endpoint.
        assertEquals("direct", converted.outbounds.first { it.str("tag") == "wg" }.str("type"))
        assertEquals("wg-endpoint", converted.outbounds.first { it.str("tag") == "wg" }.str("detour"))
    }

    @Test
    fun unsupportedPartsAreReportedNotSilentlyDropped() {
        val config = JSONObject(
            """
            {"outbounds":[
              {"tag":"x","protocol":"vless","settings":{"vnext":[{"address":"x.example.com","port":443,
                "users":[{"id":"${Fixtures.UUID}"}]}]},
               "streamSettings":{"network":"xhttp","security":"tls"}}
            ]}
            """.trimIndent()
        )
        val converted = Xray.convert(config)
        assertTrue(converted.unsupported.toString(), converted.unsupported.isNotEmpty())

        // A protocol sing-box has no counterpart for is reported too.
        val odd = JSONObject("""{"outbounds":[{"tag":"o","protocol":"shadowsocksr","settings":{}}]}""")
        assertTrue(Xray.convert(odd).unsupported.isNotEmpty())
    }

    @Test
    fun groupsAndTagsCanBeReadOutOfABuiltConfig() {
        val converted = Balancers.convert(members, Fixtures.balancer(), Fixtures.settings())
        val config = JSONObject()
            .put("outbounds", JSONArray(converted.outbounds as List<Any>))
        val groups = com.zalchik.nimbus.engine.ConfigBuilder.groupsOf(config)
        assertTrue(groups.contains(Balancers.TAG))
        val tags = com.zalchik.nimbus.engine.ConfigBuilder.tagsOf(config)
        assertTrue(tags.containsAll(converted.nodeTags))
    }
}
