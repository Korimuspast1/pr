package com.zalchik.nimbus

import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.engine.Links
import com.zalchik.nimbus.engine.Qr
import com.zalchik.nimbus.engine.Share
import com.zalchik.nimbus.engine.Subscription
import com.zalchik.nimbus.util.b64Encode
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.objOrNull
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.str
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * What a subscription body turns into, and what Nimbus turns back into a link, a config or a QR
 * code — the desktop's SubscriptionTests and ShareTests, with the same fixtures.
 */
class SubscriptionTests {
    @Test
    fun bodyFormats() {
        val plain = Fixtures.LINKS.values.joinToString("\n")
        assertEquals(Fixtures.LINKS.size, Subscription.parseBody(plain).servers.size)

        val encoded = b64Encode(plain)
        assertEquals(Fixtures.LINKS.size, Subscription.parseBody(encoded).servers.size)

        // A panel that answers the Happ user agent sends whole configs instead of links.
        val configs = JSONArray()
            .put(JSONObject().put("remarks", "A").put("outbounds", JSONArray().put(JSONObject().put("protocol", "freedom"))))
            .put(JSONObject().put("remarks", "B").put("outbounds", JSONArray().put(JSONObject().put("protocol", "freedom"))))
        val parsed = Subscription.parseBody(configs.toString())
        assertEquals(listOf("A", "B"), parsed.servers.map { it.name })
        assertTrue(parsed.servers.all { it.isFullConfig })
        assertTrue(parsed.errors.isEmpty())
    }

    @Test
    fun namesAndOwnership() {
        val parsed = Subscription.parseBody(Fixtures.LINKS.getValue("trojan"), "s1")
        assertEquals("trojan", parsed.servers.single().name)
        assertEquals("s1", parsed.servers.single().subId)

        // A link with no name still becomes a server, named after its protocol.
        val anonymous = Subscription.parseBody("trojan://pw@1.2.3.4:443")
        assertEquals(1, anonymous.servers.size)
        assertTrue(anonymous.servers[0].name.isNotEmpty())
    }

    @Test
    fun badBodiesSayWhatWentWrong() {
        val nothing = Subscription.parseBody("just some text")
        assertTrue(nothing.servers.isEmpty())
        assertTrue(nothing.errors.isNotEmpty())

        val broken = Subscription.parseBody("vless://\n" + Fixtures.LINKS.getValue("trojan"))
        assertEquals(1, broken.servers.size)
        assertEquals(1, broken.errors.size)

        val notAConfig = Subscription.parseBody("""[{"remarks":"x"}]""")
        assertTrue(notAConfig.servers.isEmpty())
        assertTrue(notAConfig.errors.any { "not an Xray config" in it })
    }

    @Test
    fun base64HeadersAreDecoded() {
        assertEquals("hello", Subscription.headerText("base64:" + b64Encode("hello")))
        assertEquals("plain", Subscription.headerText("plain"))
        // Not valid base64: the value is kept as it came, rather than dropped.
        assertEquals("base64:!!!", Subscription.headerText("base64:!!!"))
    }

    @Test
    fun headersStayAscii() {
        assertEquals("Pixel 8", Subscription.headerValue("Pixel 8"))
        val encoded = Subscription.headerValue("Мой телефон")
        assertTrue(encoded, encoded.all { it.code < 128 })
        assertTrue(encoded.contains("%"))
    }

    @Test
    fun hwidIsStablePerDevice() {
        val first = Subscription.autoHwid("device-1")
        assertEquals(first, Subscription.autoHwid("device-1"))
        assertFalse(first == Subscription.autoHwid("device-2"))
        assertEquals(32, first.length)
        assertTrue(first.all { it in '0'..'9' || it in 'a'..'f' })

        val headers = Subscription.hwidHeaders(custom = "abc", device = "Pixel")
        assertEquals("abc", headers["x-hwid"])
        assertEquals("Pixel", headers["x-device-model"])
        assertTrue(headers.containsKey("x-device-os"))

        val auto = Subscription.hwidHeaders()
        assertEquals(32, auto.getValue("x-hwid").length)
    }

    @Test
    fun machineIdIsKept() {
        val file = java.io.File(System.getProperty("java.io.tmpdir"), "nimbus-test-device-id")
        file.delete()
        val first = Subscription.machineId(file)
        assertEquals(first, Subscription.machineId(file))
        assertEquals(32, first.length)
        file.delete()
    }

    @Test
    fun placeholderServersAreSpotted() {
        // Panels answer a device they don't recognise with one stub server pointing nowhere.
        val stub = Server.ofLink(
            "x",
            "Обновите приложение",
            "",
            "trojan://pw@0.0.0.0:443",
        )
        assertTrue(Subscription.placeholdersOnly(listOf(stub)))
        val localhost = Server.ofLink("y", "stub", "", "vless://${Fixtures.UUID}@127.0.0.1:443")
        assertTrue(Subscription.placeholdersOnly(listOf(localhost)))
        assertFalse(Subscription.placeholdersOnly(listOf(Fixtures.server())))
        assertFalse(Subscription.placeholdersOnly(emptyList()).not())
    }
}

/** Links out again: what "share", "export" and the QR code send to another device. */
class ShareTests {
    @Test
    fun linksRoundtripThroughOutbounds() {
        for ((label, link) in Fixtures.LINKS) {
            val parsed = Links.parseLink(link)
            val outbound = parsed.outbound
            if (parsed.endpoint != null) continue    // WireGuard has no share-link form
            val again = Share.outboundLink(outbound, parsed.name)
            assertNotNull(label, again)
            val second = Links.parseLink(again!!)
            assertEquals(label, parsed.name, second.name)
            assertEquals(label, outbound.str("type"), second.outbound.str("type"))
            assertEquals(label, outbound.str("server"), second.outbound.str("server"))
            assertEquals(label, outbound.opt("server_port"), second.outbound.opt("server_port"))
            assertEquals(
                label,
                outbound.objOrNull("tls")?.toString(),
                second.outbound.objOrNull("tls")?.toString(),
            )
            assertEquals(
                label,
                outbound.objOrNull("transport")?.toString(),
                second.outbound.objOrNull("transport")?.toString(),
            )
        }
    }

    @Test
    fun serverLinkCarriesTheName() {
        val server = Fixtures.server(name = "NL Reality")
        val link = Share.serverLink(server)
        assertNotNull(link)
        assertTrue(link!!.endsWith("#NL%20Reality"))

        val renamed = server.copy(name = "🇳🇱 Авто")
        val shared = Share.serverLink(renamed)!!
        assertEquals("🇳🇱 Авто", Links.parseLink(shared).name)

        // A vmess link keeps its name inside the base64 JSON, so renaming means recoding.
        val vmess = Fixtures.server(name = "renamed", link = Fixtures.VMESS)
        val recoded = Share.serverLink(vmess)!!
        assertEquals("renamed", Links.parseLink(recoded).name)
    }

    @Test
    fun providerConfigSharesItsMainNode() {
        val server = Server(id = "p", name = "🇳🇱 Авто", config = Fixtures.PROVIDER)
        val link = Share.serverLink(server)
        assertNotNull(link)
        assertTrue(link!!.startsWith("vless://"))
        assertEquals(2, Share.nodeCount(server))
        assertEquals(1, Share.nodeCount(Fixtures.server()))
    }

    @Test
    fun exportedJsonIsACleanSingBoxConfig() {
        val server = Fixtures.server(name = "mine", link = Fixtures.LINKS.getValue("trojan"))
        val config = Share.serverConfig(server, Fixtures.settings { it.killSwitch = true })
        assertEquals("mine", config.str("remarks"))
        // What another client imports must not carry this phone's tunnel or a kill switch.
        assertFalse(config.toString().contains("\"strict_route\":true"))
        assertFalse(config.has("log"))
        val inbounds = config.arrOrEmpty("inbounds")
        assertEquals(0, inbounds.length())

        val many = Share.exportJson(listOf(server, Fixtures.configServer(name = "provider")), Fixtures.settings())
        val array = JSONArray(many)
        assertEquals(2, array.length())
        assertEquals("mine", array.getJSONObject(0).str("remarks"))

        val one = Share.exportJson(listOf(server), Fixtures.settings())
        assertEquals("mine", JSONObject(one).str("remarks"))
        assertEquals("{}", Share.exportJson(emptyList(), Fixtures.settings()))
    }

    @Test
    fun allowInsecureNeverLeavesTheApp() {
        val config = JSONObject(Fixtures.PROVIDER.toString())
        assertTrue(config.toString().contains("allowInsecure"))
        Share.dropAllowInsecure(config)
        assertFalse(config.toString().contains("allowInsecure"))
        // Everything else about the provider's TLS stays.
        val trojan = config.arrOrEmpty("outbounds").objects()
            .first { it.str("protocol") == "trojan" }
        assertEquals("b.example.com", trojan.objOrNull("streamSettings")?.objOrNull("tlsSettings")?.str("serverName"))
    }

    @Test
    fun exportLinksCountsWhatItSkips() {
        val exported = Share.exportLinks(
            listOf(Fixtures.server(), Server(id = "p", name = "provider", config = Fixtures.PROVIDER))
        )
        assertEquals(2, exported.text.lines().size)
        assertEquals(0, exported.skipped)

        val body = Share.subscriptionBody(listOf(Fixtures.server()))
        assertTrue(Subscription.parseBody(body).servers.size == 1)
    }

    @Test
    fun qrCodesDecode() {
        for (text in listOf(
            Fixtures.LINKS.getValue("vless-reality"),
            "https://sub.example.com/abc",
            "nimbus://import/https://panel.example.com/sub/AbCdEf123456",
        )) {
            val modules = Qr.encode(text)
            assertTrue(modules.size > 20)
            assertEquals(modules.size, modules[0].size)
            // The quiet zone is what a scanner needs to find the code at all.
            assertFalse(modules[0].any { it })

            val width = modules[0].size
            val scale = 6
            val size = width * scale
            val pixels = Qr.pixels(text, size, 2)
            assertEquals(size * size, pixels.size)
            assertEquals(text, Qr.decode(pixels, size, size))
        }
    }

    @Test
    fun tooMuchTextHasNoCode() {
        assertThrows(Qr.QrError::class.java) { Qr.encode("") }
        assertThrows(Qr.QrError::class.java) { Qr.encode("x".repeat(20000)) }
        assertNull(Qr.decode(IntArray(100) { 0xFFFFFFFF.toInt() }, 10, 10))
    }
}
