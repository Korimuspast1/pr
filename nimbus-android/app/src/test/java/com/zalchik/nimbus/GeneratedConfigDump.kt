package com.zalchik.nimbus

import com.zalchik.nimbus.data.Balancer
import com.zalchik.nimbus.data.RoutePreset
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.engine.ConfigBuilder
import com.zalchik.nimbus.engine.Selected
import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Writes every config the builder can produce to `nimbus.configDumpDir` (set by
 * app/build.gradle.kts, collected as a CI artifact). Nothing here asserts anything about the
 * files: it exists so a reviewer can read the exact JSON a phone would hand sing-box, and so a
 * schema change shows up as a diff in the artifact instead of as a guess.
 */
class GeneratedConfigDump {
    private val variants: List<Pair<String, Settings>> = listOf(
        "default" to Settings(),
        "tun-killswitch" to Settings().apply { tunnelMode = "tun"; killSwitch = true },
        "bypass-ru-fakedns" to Settings().apply { routingMode = "bypass_ru"; fakeDns = true },
        "mux-fragment" to Settings().apply { mux = true; fragment = true },
        "no-provider-rules" to Settings().apply { useProviderRouting = false },
        "per-app" to Settings().apply {
            perAppEnabled = true
            perAppMode = RoutePreset.MODE_VPN
            perAppPackages = mutableListOf("com.example.app")
        },
        "preset-bypass" to Settings().apply {
            routePresets = mutableListOf(
                RoutePreset(
                    "p1",
                    "Work",
                    RoutePreset.MODE_BYPASS,
                    sites = mutableListOf("example.com", "geosite:ru", "1.2.3.0/24"),
                    packages = mutableListOf("com.example.app"),
                )
            )
            routePreset = "p1"
        },
        "preset-vpn" to Settings().apply {
            routePresets = mutableListOf(
                RoutePreset("p2", "Only work", RoutePreset.MODE_VPN, sites = mutableListOf("work.example"))
            )
            routePreset = "p2"
        },
        "ipondemand-noads" to Settings().apply {
            domainStrategy = "IPOnDemand"
            blockAds = false
            bypassPrivate = false
            logLevel = "debug"
        },
    )

    private val servers: List<Pair<String, Server>> = listOf(
        "vless-reality" to Fixtures.server(link = Fixtures.LINKS.getValue("vless-reality")),
        "vmess-ws" to Fixtures.server(link = Fixtures.VMESS),
        "trojan-ws" to Fixtures.server(link = Fixtures.LINKS.getValue("trojan")),
        "ss-2022" to Fixtures.server(link = Fixtures.LINKS.getValue("ss-2022-plain")),
        "hy2" to Fixtures.server(link = Fixtures.LINKS.getValue("hy2")),
        "socks" to Fixtures.server(link = Fixtures.LINKS.getValue("socks")),
        "wireguard" to Fixtures.server(link = Fixtures.LINKS.getValue("wireguard")),
        "xhttp" to Fixtures.server(link = Fixtures.XHTTP),
        "provider-config" to Fixtures.configServer(),
    )

    @Test
    fun dumpEveryCombination() {
        val dir = File(System.getProperty("nimbus.configDumpDir") ?: return)
        dir.mkdirs()
        var count = 0

        for ((serverName, server) in servers) {
            for ((variantName, settings) in variants) {
                for (ipv6 in listOf(false, true)) {
                    val built = ConfigBuilder.build(Selected.OfServer(server), settings, ipv6)
                    val name = "$serverName.$variantName.${if (ipv6) "ipv6" else "ipv4"}.json"
                    File(dir, name).writeText(header(built) + ConfigBuilder.dump(built.config))
                    count++
                }
            }
        }

        // Balancers: one of plain links, one of a single subscription's provider configs.
        val linkMembers = listOf(
            Fixtures.server("a", "NL", Fixtures.LINKS.getValue("vless-reality")),
            Fixtures.server("b", "DE", Fixtures.LINKS.getValue("trojan")),
            Fixtures.server("c", "FI", Fixtures.LINKS.getValue("hy2")),
        )
        val providerMembers = listOf(
            Server("p1", "one", "s1", config = Fixtures.PROVIDER),
            Server("p2", "two", "s1", config = Fixtures.PROVIDER),
        )
        for ((name, members) in listOf("links" to linkMembers, "providers" to providerMembers)) {
            for (mode in listOf(Balancer.MODE_STABLE, Balancer.MODE_FASTEST)) {
                val balancer = Balancer("b1", "auto", members.map { it.id }.toMutableList(), mode = mode)
                for ((variantName, settings) in variants) {
                    val built = ConfigBuilder.build(
                        Selected.OfBalancer(balancer, members),
                        settings,
                        ipv6 = false,
                    )
                    File(dir, "balancer-$name.$mode.$variantName.json")
                        .writeText(header(built) + ConfigBuilder.dump(built.config))
                    count++
                }
            }
        }

        assertTrue("nothing was dumped", count > 100)
        assertTrue(File(dir, "vless-reality.default.ipv4.json").length() > 100)
    }

    /** What the file is, and whether the builder flagged anything it can't run. */
    private fun header(built: ConfigBuilder.Built): String = buildString {
        append("// proxy tags: ").append(built.proxyTags.joinToString(", ")).append('\n')
        append("// group: ").append(built.groupTag ?: "—").append('\n')
        if (built.unsupported.isNotEmpty()) {
            append("// unsupported: ").append(built.unsupported.joinToString("; ")).append('\n')
        }
        append('\n')
    }
}
