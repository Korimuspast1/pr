package com.zalchik.nimbus

import com.zalchik.nimbus.engine.Links
import com.zalchik.nimbus.engine.LinkError
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.objOrNull
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.str
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Share links become sing-box outbounds. The desktop's LinkTests assert `xray run -test` accepts
 * the result; a phone has no core binary in a unit test, so these assert the sing-box fields the
 * core reads instead — which is the same claim, spelled out.
 */
class LinksTests {
    @Test
    fun everyLinkParses() {
        for ((label, link) in Fixtures.LINKS) {
            val parsed = Links.parseLink(link, "proxy")
            assertTrue(label, parsed.outbound.str("tag").isNotEmpty())
            assertTrue(label, parsed.outbound.str("type").isNotEmpty())
            // xhttp is the one Nimbus can't run: it must say so instead of emitting a bad config.
            if (label == "vless-xhttp") {
                assertNotNull(parsed.unsupported)
            }
        }
    }

    @Test
    fun namesAndProtocols() {
        assertEquals("NL Reality", Links.parseLink(Fixtures.LINKS.getValue("vless-reality")).name)
        assertEquals("vm-ws", Links.parseLink(Fixtures.VMESS).name)
        assertEquals("trojan", Links.parseLink(Fixtures.LINKS.getValue("trojan")).name)
        assertEquals("ss", Links.parseLink(Fixtures.LINKS.getValue("ss-sip002")).name)
        assertEquals("warp", Links.parseLink(Fixtures.LINKS.getValue("wireguard")).name)
        assertEquals("vless", Links.parseLink(Fixtures.LINKS.getValue("vless-reality")).protocol)
        assertEquals("hysteria2", Links.parseLink(Fixtures.LINKS.getValue("hy2")).protocol)
        assertEquals("wireguard", Links.parseLink(Fixtures.LINKS.getValue("wireguard")).protocol)
    }

    @Test
    fun realityFields() {
        val outbound = Links.parseLink(Fixtures.LINKS.getValue("vless-reality")).outbound
        assertEquals("vless", outbound.str("type"))
        assertEquals("1.2.3.4", outbound.str("server"))
        assertEquals(443, outbound.opt("server_port"))
        assertEquals(Fixtures.UUID, outbound.str("uuid"))
        assertEquals("xtls-rprx-vision", outbound.str("flow"))
        val tls = outbound.objOrNull("tls")
        assertNotNull(tls)
        assertEquals(true, tls!!.opt("enabled"))
        assertEquals("www.microsoft.com", tls.str("server_name"))
        assertEquals("firefox", tls.objOrNull("utls")?.str("fingerprint"))
        val reality = tls.objOrNull("reality")
        assertNotNull(reality)
        assertEquals(Fixtures.PBK, reality!!.str("public_key"))
        assertEquals("6ba85179e30d4fc2", reality.str("short_id"))
    }

    @Test
    fun insecureIsDroppedNotTranslated() {
        // Plenty of free configs ship insecure=1. sing-box's TLS has no such field, and mapping it
        // to insecure:true would be a lie: the flag is dropped and the rest still connects.
        val link = "trojan://happybirthdaydaniel@de-bots4.h1cloud.net:25267?security=tls" +
            "&sni=t.me/FuckTheWall&alpn=http/1.1&insecure=1&fp=firefox&type=tcp#Sponsor%20@FuckTheWall"
        val outbound = Links.parseLink(link).outbound
        val tls = outbound.objOrNull("tls")!!
        assertNull(tls.opt("insecure"))
        assertNull(tls.opt("allow_insecure"))
        assertEquals("t.me/FuckTheWall", tls.str("server_name"))
        assertEquals("firefox", tls.objOrNull("utls")?.str("fingerprint"))
        assertEquals(listOf("http/1.1"), tls.arrOrEmpty("alpn").objects().map { it.toString() }.let {
            (0 until tls.arrOrEmpty("alpn").length()).map { i -> tls.arrOrEmpty("alpn").getString(i) }
        })
        assertEquals("happybirthdaydaniel", outbound.str("password"))
        assertEquals("Sponsor @FuckTheWall", Links.parseLink(link).name)
    }

    @Test
    fun hy2InsecureIsDroppedToo() {
        val outbound = Links.parseLink("hy2://pass@h.example.com:443?sni=h.example.com&insecure=1").outbound
        assertEquals("hysteria2", outbound.str("type"))
        assertNull(outbound.objOrNull("tls")?.opt("insecure"))
    }

    @Test
    fun shadowsocksVariants() {
        for (key in listOf("ss-sip002", "ss-2022-plain", "ss-legacy")) {
            val outbound = Links.parseLink(Fixtures.LINKS.getValue(key)).outbound
            assertEquals(key, "shadowsocks", outbound.str("type"))
            assertTrue(key, outbound.str("method").isNotEmpty())
            assertTrue(key, outbound.str("password").isNotEmpty())
            assertEquals(key, "5.6.7.8", outbound.str("server"))
            assertEquals(key, 8388, outbound.opt("server_port"))
        }
        assertEquals(
            "chacha20-ietf-poly1305",
            Links.parseLink(Fixtures.LINKS.getValue("ss-sip002")).outbound.str("method"),
        )
        assertEquals(
            "aes-256-gcm",
            Links.parseLink(Fixtures.LINKS.getValue("ss-legacy")).outbound.str("method"),
        )
    }

    @Test
    fun hysteria2ObfsAndHop() {
        val obfs = Links.parseLink(Fixtures.LINKS.getValue("hy2")).outbound
        assertEquals("salamander", obfs.objOrNull("obfs")?.str("type"))
        assertEquals("ob", obfs.objOrNull("obfs")?.str("password"))

        val hop = Links.parseLink(Fixtures.LINKS.getValue("hy2-hop")).outbound
        val ports = hop.opt("server_ports")
        assertNotNull(ports)
    }

    @Test
    fun socksCarriesCredentials() {
        val outbound = Links.parseLink(Fixtures.LINKS.getValue("socks")).outbound
        assertEquals("socks", outbound.str("type"))
        assertEquals("10.0.0.1", outbound.str("server"))
        assertEquals("user", outbound.str("username"))
        assertEquals("pw", outbound.str("password"))
    }

    @Test
    fun transports() {
        val ws = Links.parseLink(Fixtures.VMESS).outbound.objOrNull("transport")!!
        assertEquals("ws", ws.str("type"))
        assertEquals("/ws", ws.str("path"))
        val host = ws.objOrNull("headers")?.arrOrEmpty("Host")
        assertEquals(1, host?.length())
        assertEquals("cdn.example.com", host?.getString(0))

        val grpc = Links.parseLink(Fixtures.LINKS.getValue("vless-grpc")).outbound.objOrNull("transport")!!
        assertEquals("grpc", grpc.str("type"))
        assertEquals("svc", grpc.str("service_name"))
    }

    @Test
    fun xhttpIsReportedUnsupported() {
        // sing-box has http, ws, quic, grpc and httpupgrade — no xhttp. Nimbus says so rather than
        // quietly building a config that can't dial.
        val reason = Links.parseLink(Fixtures.XHTTP).unsupported
        assertNotNull(reason)
        assertTrue(reason!!.lowercase().contains("xhttp"))
        // The reason travels with the server, so the list can warn before anyone taps connect.
        assertEquals(reason, Links.unsupportedReason(Fixtures.server(link = Fixtures.XHTTP)))
        // A provider's own config is Xray's business, not a share link: nothing to report.
        assertNull(Links.unsupportedReason(Fixtures.configServer()))

        val kcp = Links.parseLink(
            "vmess://" + com.zalchik.nimbus.util.b64Encode(
                """{"v":"2","ps":"k","add":"k.example.com","port":"443","id":"${Fixtures.UUID}",""" +
                    """"aid":"0","scy":"auto","net":"kcp","tls":""}"""
            ),
        ).unsupported
        assertNotNull(kcp)
        assertTrue(kcp!!.lowercase().contains("kcp"))
    }

    @Test
    fun wireguardBecomesAnEndpoint() {
        // sing-box removed the wireguard outbound; the same key material is an endpoint now, and
        // the outbound that uses it is a direct detour to that endpoint.
        val parsed = Links.parseLink(Fixtures.LINKS.getValue("wireguard"))
        val endpoint = parsed.endpoint
        assertNotNull(endpoint)
        assertEquals("wireguard", endpoint!!.str("type"))
        assertTrue(endpoint.str("private_key").isNotEmpty())
        assertTrue(endpoint.arrOrEmpty("address").length() >= 2)
        assertEquals(1280, endpoint.opt("mtu"))
        val peers = endpoint.arrOrEmpty("peers").objects()
        assertEquals(1, peers.size)
        assertEquals("162.159.192.1", peers[0].str("address"))
        assertEquals(2408, peers[0].opt("port"))
        assertTrue(peers[0].str("public_key").isNotEmpty())
        assertEquals("direct", parsed.outbound.str("type"))
        assertEquals("proxy-endpoint", parsed.outbound.str("detour"))
    }

    @Test
    fun multiplexAndFragmentAreAppliedOnRequest() {
        val outbound = Links.parseLink(Fixtures.LINKS.getValue("trojan"), "proxy").outbound
        Links.applyMultiplex(outbound, true, 8)
        assertEquals(8, outbound.objOrNull("multiplex")?.opt("concurrency"))
        Links.applyFragment(outbound, true)
        assertNotNull(outbound.objOrNull("fragment"))

        // Reality's flow already fills a connection; mux on top of it is refused by servers.
        val reality = Links.parseLink(Fixtures.LINKS.getValue("vless-reality")).outbound
        Links.applyMultiplex(reality, true, 8)
        assertNull(reality.objOrNull("multiplex"))
    }

    @Test
    fun badLinksAreRejected() {
        for (link in listOf(
            "vless://",
            "vless://@1.2.3.4:443",
            "unknown://1.2.3.4:443",
            "vmess://not-base64",
            "trojan://pw@host",
            "ss://x@1.2.3.4:0",
        )) {
            assertThrows(link, LinkError::class.java) { Links.parseLink(link) }
        }
        assertTrue(!Links.isLink("https://example.com"))
        assertTrue(Links.isLink(Fixtures.LINKS.getValue("trojan")))
        assertEquals("vless", Links.schemeOf("VLESS://x@1.2.3.4:443"))
    }

    @Test
    fun domainsForSniffingComeOutOfTheLink() {
        val outbound = Links.parseLink(Fixtures.VMESS).outbound
        val domains = Links.serverDomains(listOf(outbound))
        assertTrue(domains.toString(), domains.any { it.contains("vm.example.com") || it.contains("cdn.example.com") })
        val probe = Links.probeAddress(Fixtures.server(link = Fixtures.LINKS.getValue("vless-reality")))
        assertNotNull(probe)
        assertEquals("1.2.3.4:443", probe)
    }
}
