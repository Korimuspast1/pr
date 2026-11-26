package com.zalchik.nimbus

import com.zalchik.nimbus.data.RoutePreset
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.engine.Balancers
import com.zalchik.nimbus.engine.ConfigBuilder
import com.zalchik.nimbus.engine.Xray
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.objOrNull
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.str
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The configs Nimbus hands sing-box. The desktop validates with `sing-box check`; a unit test on
 * a phone has no binary, so these assert the fields the core reads — the ones that are easy to get
 * subtly wrong (a legacy geoip rule it ignores, a `find_process` it needs, a WireGuard outbound it
 * removed) and cheap to check.
 */
class ConfigBuilderTests {
    private fun built(
        settings: com.zalchik.nimbus.data.Settings = Fixtures.settings(),
        server: Server = Fixtures.server(),
        ipv6: Boolean = false,
    ) = ConfigBuilder.buildServer(server, settings, ipv6)

    // -- the tunnel ------------------------------------------------------------------------------

    @Test
    fun tunInboundIsWhatAndroidNeeds() {
        val inbound = built().config.arrOrEmpty("inbounds").objects().single()
        assertEquals("tun", inbound.str("type"))
        assertEquals(ConfigBuilder.TUN_TAG, inbound.str("tag"))
        assertEquals(ConfigBuilder.TUN_NAME, inbound.str("interface_name"))
        assertEquals(ConfigBuilder.TUN_ADDRESS4, inbound.arrOrEmpty("address").getString(0))
        assertEquals(1500, inbound.opt("mtu"))
        assertEquals(true, inbound.opt("auto_route"))
        // No ipv6 on the device: no ipv6 address in the tunnel.
        assertEquals(1, inbound.arrOrEmpty("address").length())
        val exclude = inbound.arrOrEmpty("route_exclude_address").strings()
        assertTrue(exclude.contains("127.0.0.0/8"))
        assertTrue(exclude.any { it.startsWith("192.168.") })
    }

    @Test
    fun ipv6AddsTheSecondAddress() {
        val inbound = built(ipv6 = true).config.arrOrEmpty("inbounds").objects().single()
        val addresses = inbound.arrOrEmpty("address").strings()
        assertTrue(addresses.contains(ConfigBuilder.TUN_ADDRESS4))
        assertTrue(addresses.contains(ConfigBuilder.TUN_ADDRESS6))
    }

    @Test
    fun killSwitchAndMtuReachTheTunnel() {
        val settings = Fixtures.settings { it.killSwitch = true; it.tunMtu = 9000; it.bypassPrivate = false }
        val inbound = built(settings).config.arrOrEmpty("inbounds").objects().single()
        assertEquals(true, inbound.opt("strict_route"))
        assertEquals(9000, inbound.opt("mtu"))
        val exclude = inbound.arrOrEmpty("route_exclude_address").strings()
        assertFalse(exclude.any { it.startsWith("192.168.") })
    }

    // -- dns --------------------------------------------------------------------------------------

    @Test
    fun dnsServersHaveATypeAndAFinalServer() {
        val config = built().config
        val dns = config.objOrNull("dns")!!
        val servers = dns.arrOrEmpty("servers").objects()
        assertTrue(servers.isNotEmpty())
        for (server in servers) {
            assertTrue(server.toString(), server.str("type").isNotEmpty())
            assertTrue(server.str("tag").isNotEmpty())
        }
        assertEquals(ConfigBuilder.DNS_REMOTE, dns.str("final"))
        val tags = servers.map { it.str("tag") }
        assertTrue(tags.contains(ConfigBuilder.DNS_REMOTE))
        assertTrue(tags.contains(ConfigBuilder.DNS_DIRECT))
    }

    @Test
    fun dohAndPlainAddressesBecomeTheRightTransport() {
        val doh = ConfigBuilder.sbDns("https://1.1.1.1/dns-query", "dns-remote", "proxy", false)
        assertEquals("https", doh.str("type"))
        assertEquals("1.1.1.1", doh.str("server"))
        assertEquals("/dns-query", doh.str("path"))

        val plain = ConfigBuilder.sbDns("8.8.8.8", "dns-direct", null, false)
        assertEquals("udp", plain.str("type"))
        assertEquals("8.8.8.8", plain.str("server"))
        assertEquals(53, plain.opt("server_port"))

        // UDP through a tunnel is unreliable, so a resolver reached through the proxy becomes DoH
        // when it is a known one and TCP otherwise.
        val throughProxy = ConfigBuilder.sbDns("1.1.1.1", "dns-remote", "proxy", true)
        assertEquals("https", throughProxy.str("type"))
        assertEquals("/dns-query", throughProxy.str("path"))
        val unknown = ConfigBuilder.sbDns("5.6.7.8", "dns-remote", "proxy", true)
        assertEquals("tcp", unknown.str("type"))

        val local = ConfigBuilder.sbDns("localhost", "dns-local", null, false)
        assertEquals("local", local.str("type"))

        // A named server needs a resolver of its own, or sing-box asks the tunnel to resolve the
        // tunnel's DNS server.
        val named = ConfigBuilder.sbDns("tls://dns.adguard.com", "dns-direct", null, false)
        assertEquals("tls", named.str("type"))
        assertEquals(ConfigBuilder.DNS_LOCAL, named.str("domain_resolver"))
    }

    @Test
    fun fakeDnsHandsOutPlaceholders() {
        val settings = Fixtures.settings { it.fakeDns = true }
        val dns = built(settings).config.objOrNull("dns")!!
        val fake = dns.arrOrEmpty("servers").objects().firstOrNull { it.str("type") == "fakeip" }
        assertNotNull(fake)
        assertEquals(ConfigBuilder.FAKE_RANGE4, fake!!.objOrNull("fakeip")?.str("inet4_range"))
        // A one-second TTL: apps cached fake addresses for ten minutes and they led nowhere after
        // the tunnel went down.
        val rule = dns.arrOrEmpty("rules").objects().firstOrNull { it.str("server") == ConfigBuilder.DNS_FAKE }
        assertNotNull(rule)
        assertEquals(1, rule!!.opt("rewrite_ttl"))

        val off = built(Fixtures.settings { it.fakeDns = false }).config.objOrNull("dns")!!
        assertNull(off.arrOrEmpty("servers").objects().firstOrNull { it.str("type") == "fakeip" })
    }

    // -- routing ----------------------------------------------------------------------------------

    @Test
    fun globalModeRoutesEverythingToTheProxy() {
        val config = built(Fixtures.settings { it.routingMode = "global" }).config
        val route = config.objOrNull("route")!!
        assertEquals(ConfigBuilder.DIRECT, route.str("final"))
        assertEquals(true, route.opt("auto_detect_interface"))
        val rules = route.arrOrEmpty("rules").objects()
        val last = rules.last()
        assertEquals(ConfigBuilder.PROXY, last.str("outbound"))
        assertEquals("route", last.str("action"))
        // Sniffing first, so domain rules see the domain a connection really asks for.
        assertEquals("sniff", rules.first().str("action"))
        assertTrue(rules.any { it.str("action") == "hijack-dns" })
    }

    @Test
    fun smartModeSendsRussianSitesDirect() {
        val settings = Fixtures.settings { it.routingMode = "bypass_ru" }
        val config = built(settings).config
        val rules = config.objOrNull("route")!!.arrOrEmpty("rules").objects()
        assertTrue(rules.any { it.arrOrEmpty("domain_suffix").strings().contains("ru") })
        // The rule sets the mobile core downloads instead of Xray's .dat files.
        val sets = config.objOrNull("route")!!.arrOrEmpty("rule_set").objects()
        val tags = sets.map { it.str("tag") }
        assertTrue(tags.toString(), tags.any { it.startsWith("geosite-") })
        for (set in sets) {
            assertEquals("remote", set.str("type"))
            assertEquals("binary", set.str("format"))
            assertTrue(set.str("url").endsWith(".srs"))
            assertEquals(ConfigBuilder.DIRECT, set.str("download_detour"))
        }
        // The direct resolver's own address must not go through the tunnel it feeds.
        assertTrue(rules.any { it.arrOrEmpty("ip_cidr").strings().contains("8.8.8.8/32") })
    }

    @Test
    fun noLegacyGeoRulesSurvive() {
        // sing-box keeps the geoip/geosite keys in its schema and ignores them; a config that
        // still emits them looks routed and isn't.
        val settings = Fixtures.settings {
            it.routingMode = "bypass_ru"
            it.blockAds = true
            it.bypassPrivate = true
        }
        val text = built(settings).config.toString()
        assertFalse(text.contains("\"geoip\""))
        assertFalse(text.contains("\"geosite\""))
    }

    @Test
    fun adsAreRejectedAndPrivateAddressesBypassed() {
        val rules = built(Fixtures.settings { it.blockAds = true; it.bypassPrivate = true })
            .config.objOrNull("route")!!.arrOrEmpty("rules").objects()
        assertTrue(rules.any { it.str("action") == "reject" })
        assertTrue(rules.any { it.opt("ip_is_private") == true && it.str("outbound") == ConfigBuilder.DIRECT })
    }

    @Test
    fun presetsRouteTheirSitesAndApps() {
        val preset = Fixtures.preset(
            sites = listOf("example.com", "full:exact.example", "keyword:ads", "1.2.3.0/24", "geosite:ru"),
            packages = listOf("com.example.app"),
        )
        val settings = Fixtures.settings {
            it.routePresets.add(preset)
            it.routePreset = preset.id
        }
        val config = built(settings).config
        val rules = config.objOrNull("route")!!.arrOrEmpty("rules").objects()

        val packages = rules.firstOrNull { it.arrOrEmpty("package_name").length() > 0 }
        assertNotNull(packages)
        assertEquals(ConfigBuilder.DIRECT, packages!!.str("outbound"))
        assertEquals(listOf("com.example.app"), packages.arrOrEmpty("package_name").strings())
        // Package rules only match when the core asks the platform who owns a connection.
        assertEquals(true, config.objOrNull("route")!!.opt("find_process"))

        assertTrue(rules.any { it.arrOrEmpty("domain_suffix").strings().contains("example.com") })
        assertTrue(rules.any { it.arrOrEmpty("domain").strings().contains("exact.example") })
        assertTrue(rules.any { it.arrOrEmpty("domain_keyword").strings().contains("ads") })
        assertTrue(rules.any { it.arrOrEmpty("ip_cidr").strings().contains("1.2.3.0/24") })
        assertTrue(config.objOrNull("route")!!.arrOrEmpty("rule_set").objects()
            .any { it.str("tag") == "geosite-category-ru" })
    }

    @Test
    fun findProcessIsLeftOutWhenNothingNeedsIt() {
        assertNull(built().config.objOrNull("route")!!.opt("find_process"))
        val perApp = built(Fixtures.settings { it.perAppEnabled = true }).config
        assertEquals(true, perApp.objOrNull("route")!!.opt("find_process"))
    }

    @Test
    fun vpnOnlyPresetFlipsTheDirection() {
        val preset = Fixtures.preset(mode = RoutePreset.MODE_VPN, sites = listOf("work.example"))
        val settings = Fixtures.settings {
            it.routePresets.add(preset)
            it.routePreset = preset.id
        }
        val rules = built(settings).config.objOrNull("route")!!.arrOrEmpty("rules").objects()
        val claimed = rules.firstOrNull { it.arrOrEmpty("domain_suffix").strings().contains("work.example") }
        assertEquals(ConfigBuilder.PROXY, claimed?.str("outbound"))
        // Everything the preset didn't claim goes past the VPN, as the desktop's tail rule does.
        assertTrue(
            rules.any {
                it.str("outbound") == ConfigBuilder.DIRECT &&
                    it.arrOrEmpty("network").strings() == listOf("tcp", "udp")
            }
        )
    }

    @Test
    fun siteEntriesAreReadTheWayDesktopReadsThem() {
        assertEquals("domain_suffix" to "example.com", ConfigBuilder.siteEntry("example.com"))
        assertEquals("domain_suffix" to "example.com", ConfigBuilder.siteEntry("*.example.com"))
        assertEquals("domain_suffix" to "example.com", ConfigBuilder.siteEntry("https://example.com/page"))
        assertEquals("domain" to "exact.example", ConfigBuilder.siteEntry("full:exact.example"))
        assertEquals("domain_keyword" to "ads", ConfigBuilder.siteEntry("keyword:ads"))
        assertEquals("domain_regex" to "^a.*z$", ConfigBuilder.siteEntry("regexp:^a.*z$"))
        assertEquals("ip" to "1.2.3.4/32", ConfigBuilder.siteEntry("1.2.3.4"))
        assertEquals("ip" to "10.0.0.0/8", ConfigBuilder.siteEntry("10.0.0.0/8"))
        assertEquals("ip" to "private", ConfigBuilder.siteEntry("geoip:private"))
        assertNull(ConfigBuilder.siteEntry("# a comment"))
        assertNull(ConfigBuilder.siteEntry(""))
        val ruleSets = Xray.RuleSets()
        assertEquals("rule_set" to "geosite-youtube", ConfigBuilder.siteEntry("geosite:youtube", ruleSets))
        assertEquals("rule_set" to "geoip-ru", ConfigBuilder.siteEntry("geoip:ru", ruleSets))
        // Xray's geosite:ru is published as category-ru: a rule set that 404s stops the core.
        assertEquals("rule_set" to "geosite-category-ru", ConfigBuilder.siteEntry("geosite:ru", ruleSets))
        assertEquals(
            listOf("geosite-youtube", "geoip-ru", "geosite-category-ru"),
            ruleSets.tags,
        )
        for (set in ruleSets.toJsonArray().objects()) {
            assertTrue(set.str("url"), set.str("url").endsWith(".srs"))
        }
    }

    @Test
    fun domainStrategyReachesDnsAndRoute() {
        val config = built(Fixtures.settings { it.domainStrategy = "IPOnDemand" }).config
        assertEquals("prefer_ipv4", config.objOrNull("dns")!!.str("strategy"))
        assertTrue(config.objOrNull("route")!!.str("default_domain_resolver").isNotEmpty())

        val asIs = built(Fixtures.settings { it.domainStrategy = "AsIs" }).config
        assertFalse(asIs.objOrNull("dns")!!.has("strategy"))
    }

    // -- servers ----------------------------------------------------------------------------------

    @Test
    fun linkServerBecomesOneProxyOutbound() {
        val result = built()
        val outbounds = Fixtures.outbounds(result.config)
        assertEquals(listOf(ConfigBuilder.PROXY, ConfigBuilder.DIRECT, ConfigBuilder.BLOCK), outbounds.keys.toList())
        assertEquals("vless", outbounds.getValue(ConfigBuilder.PROXY).str("type"))
        assertEquals(listOf(ConfigBuilder.PROXY), result.proxyTags)
        assertNull(result.groupTag)
        assertTrue(result.unsupported.isEmpty())
    }

    @Test
    fun muxAndFragmentSettingsReachTheOutbound() {
        val settings = Fixtures.settings { it.mux = true; it.muxConcurrency = 4; it.fragment = true }
        val outbound = Fixtures.outbounds(built(settings).config).getValue(ConfigBuilder.PROXY)
        assertEquals(4, outbound.objOrNull("multiplex")?.opt("concurrency"))
        assertNotNull(outbound.objOrNull("fragment"))
    }

    @Test
    fun wireguardServerKeepsItsEndpoint() {
        val result = built(server = Fixtures.server(link = Fixtures.LINKS.getValue("wireguard")))
        val endpoints = Fixtures.endpoints(result.config)
        assertEquals(1, endpoints.size)
        assertEquals("wireguard", endpoints[0].str("type"))
        assertEquals("direct", Fixtures.outbounds(result.config).getValue(ConfigBuilder.PROXY).str("type"))
        assertEquals("${ConfigBuilder.PROXY}-endpoint", Fixtures.outbounds(result.config)
            .getValue(ConfigBuilder.PROXY).str("detour"))
    }

    @Test
    fun xhttpServerIsReportedBeforeItRuns() {
        val result = built(server = Fixtures.server(link = Fixtures.XHTTP))
        assertEquals(1, result.unsupported.size)
        assertTrue(result.unsupported[0].lowercase().contains("xhttp"))
    }

    @Test
    fun providerConfigIsTranslatedAndCleaned() {
        val result = built(server = Fixtures.configServer())
        val config = result.config
        // allowInsecure has no sing-box counterpart and Xray itself refuses it since 26.2.6.
        assertFalse(config.toString().contains("allowInsecure"))
        val outbounds = Fixtures.outbounds(config)
        assertTrue(outbounds.containsKey("proxy"))
        assertTrue(outbounds.containsKey("proxy2"))
        assertEquals("vless", outbounds.getValue("proxy").str("type"))
        assertEquals("trojan", outbounds.getValue("proxy2").str("type"))
        // The provider's own balancer becomes a urltest group the node switcher can drive.
        val groups = ConfigBuilder.groupsOf(config)
        assertTrue(groups.toString(), groups.isNotEmpty())
        assertNotNull(result.groupTag)
        // Its helper outbounds (freedom/blackhole/dns) are Nimbus' direct and block instead.
        assertFalse(outbounds.values.any { it.str("type") == "freedom" })
        assertTrue(outbounds.containsKey(ConfigBuilder.DIRECT))
        assertTrue(outbounds.containsKey(ConfigBuilder.BLOCK))
        // Its geoip/geosite rules became downloadable rule sets.
        val sets = config.objOrNull("route")!!.arrOrEmpty("rule_set").objects().map { it.str("tag") }
        assertTrue(sets.toString(), sets.any { it.startsWith("geoip-") || it.startsWith("geosite-") })
        assertFalse(config.toString().contains("\"geoip\":"))
    }

    @Test
    fun providerRoutingCanBeTurnedOff() {
        val on = built(Fixtures.settings { it.useProviderRouting = true }, Fixtures.configServer())
        assertTrue(ConfigBuilder.keepProviderRouting(Fixtures.configServer(), Fixtures.settings()))
        assertFalse(
            ConfigBuilder.keepProviderRouting(
                Fixtures.configServer(),
                Fixtures.settings { it.useProviderRouting = false },
            )
        )
        val off = built(Fixtures.settings { it.useProviderRouting = false }, Fixtures.configServer())
        // Without the provider's rules, Nimbus' own final rule sends everything to the proxy.
        val rules = off.config.objOrNull("route")!!.arrOrEmpty("rules").objects()
        assertEquals(ConfigBuilder.PROXY, rules.last().str("outbound"))
        assertTrue(on.config.objOrNull("route")!!.arrOrEmpty("rules").length() > 0)
    }

    @Test
    fun aVpnPresetReplacesProviderRouting() {
        val preset = Fixtures.preset(mode = RoutePreset.MODE_VPN, sites = listOf("work.example"))
        val settings = Fixtures.settings {
            it.routePresets.add(preset)
            it.routePreset = preset.id
        }
        assertFalse(ConfigBuilder.keepProviderRouting(Fixtures.configServer(), settings))
        // A bypass preset that keeps the provider's rules sits on top of them.
        val bypass = Fixtures.preset(mode = RoutePreset.MODE_BYPASS, sites = listOf("bank.example"))
        val keep = Fixtures.settings {
            it.routePresets.add(bypass)
            it.routePreset = bypass.id
        }
        assertTrue(ConfigBuilder.keepProviderRouting(Fixtures.configServer(), keep))
        val without = Fixtures.preset(mode = RoutePreset.MODE_BYPASS, provider = false)
        assertFalse(
            ConfigBuilder.keepProviderRouting(
                Fixtures.configServer(),
                Fixtures.settings {
                    it.routePresets.add(without)
                    it.routePreset = without.id
                },
            )
        )
    }

    // -- balancers ----------------------------------------------------------------------------------

    @Test
    fun balancerBecomesAUrltestGroup() {
        val members = listOf(
            Fixtures.server("a", "NL", Fixtures.LINKS.getValue("vless-reality")),
            Fixtures.server("b", "DE", Fixtures.LINKS.getValue("trojan")),
            Fixtures.server("c", "FI", Fixtures.LINKS.getValue("hy2")),
        )
        val balancer = Fixtures.balancer(members = members.map { it.id })
        val result = ConfigBuilder.buildBalancer(balancer, members, Fixtures.settings(), ipv6 = false)
        val config = result.config
        assertEquals(Balancers.TAG, result.groupTag)
        val group = Fixtures.outbounds(config).getValue(Balancers.TAG)
        assertEquals("urltest", group.str("type"))
        val nodes = group.arrOrEmpty("outbounds").strings()
        assertEquals(3, nodes.size)
        for (node in nodes) assertTrue(node, node.startsWith(Balancers.PREFIX))
        assertEquals("30s", group.str("interval"))
        // Stable keeps a node while another is within the gain; fastest moves immediately.
        assertEquals(Balancers.MIN_GAIN_MS, group.opt("tolerance"))
        val fastest = ConfigBuilder.buildBalancer(
            balancer.copy(mode = com.zalchik.nimbus.data.Balancer.MODE_FASTEST),
            members,
            Fixtures.settings(),
            ipv6 = false,
        )
        assertEquals(0, Fixtures.outbounds(fastest.config).getValue(Balancers.TAG).opt("tolerance"))
        assertTrue(result.proxyTags.contains(Balancers.TAG))
    }

    @Test
    fun balancerOfOneSubscriptionKeepsProviderRules() {
        val members = listOf(
            Server("p1", "one", subId = "s1", config = Fixtures.PROVIDER),
            Server("p2", "two", subId = "s1", config = Fixtures.PROVIDER),
        )
        val balancer = Fixtures.balancer(members = members.map { it.id })
        val kept = ConfigBuilder.buildBalancer(balancer, members, Fixtures.settings(), ipv6 = false)
        assertTrue(ConfigBuilder.keepProviderRoutingFor(members, Fixtures.settings()))
        assertNotNull(kept.config.objOrNull("route"))

        // Two different subscriptions: no single provider's rules can describe the set.
        val mixed = listOf(members[0], members[1].copy(id = "p3", subId = "s2"))
        assertFalse(ConfigBuilder.keepProviderRoutingFor(mixed, Fixtures.settings()))
        assertFalse(
            ConfigBuilder.keepProviderRoutingFor(
                members,
                Fixtures.settings { it.useProviderRouting = false },
            )
        )
    }

    @Test
    fun aDeadMemberDoesNotTakeTheBalancerDown() {
        val members = listOf(
            Fixtures.server("a", "good", Fixtures.LINKS.getValue("trojan")),
            Server("b", "broken", link = "vless://not-a-link"),
        )
        val result = ConfigBuilder.buildBalancer(
            Fixtures.balancer(members = listOf("a", "b")),
            members,
            Fixtures.settings(),
            ipv6 = false,
        )
        assertEquals(1, Fixtures.outbounds(result.config).getValue(Balancers.TAG).arrOrEmpty("outbounds").length())
        assertEquals(1, result.unsupported.size)
        assertTrue(result.unsupported[0].startsWith("broken"))
    }

    // -- helpers the UI uses -----------------------------------------------------------------------

    @Test
    fun helperViewsOfABuiltConfig() {
        val result = built(server = Fixtures.configServer())
        val tags = ConfigBuilder.tagsOf(result.config)
        assertTrue(tags.contains(ConfigBuilder.PROXY))
        assertTrue(tags.contains(ConfigBuilder.DIRECT))
        val addresses = ConfigBuilder.remoteAddresses(result.config)
        assertTrue(addresses.toString(), addresses.any { it.contains("a.example.com") })
        val probes = ConfigBuilder.probeAddresses(result.config)
        assertTrue(probes.isNotEmpty())
        assertTrue(ConfigBuilder.dump(result.config).contains("\"inbounds\""))
    }

    @Test
    fun dumpIsValidJsonAndKeepsTheOrder() {
        val text = ConfigBuilder.dump(built().config)
        assertNotNull(JSONObject(text))
        assertTrue(text.indexOf("\"log\"") < text.indexOf("\"outbounds\""))
    }
}
