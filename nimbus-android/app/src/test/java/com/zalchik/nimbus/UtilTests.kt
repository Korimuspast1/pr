package com.zalchik.nimbus

import com.zalchik.nimbus.util.arr
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.b64Bytes
import com.zalchik.nimbus.util.b64Clean
import com.zalchik.nimbus.util.b64Decode
import com.zalchik.nimbus.util.b64DecodeOrSelf
import com.zalchik.nimbus.util.b64Encode
import com.zalchik.nimbus.util.bool
import com.zalchik.nimbus.util.copy
import com.zalchik.nimbus.util.csvList
import com.zalchik.nimbus.util.dropKey
import com.zalchik.nimbus.util.hexToBytes
import com.zalchik.nimbus.util.hostOf
import com.zalchik.nimbus.util.hostPort
import com.zalchik.nimbus.util.int
import com.zalchik.nimbus.util.isEmptyJson
import com.zalchik.nimbus.util.listOfStrings
import com.zalchik.nimbus.util.long
import com.zalchik.nimbus.util.obj
import com.zalchik.nimbus.util.objNonEmpty
import com.zalchik.nimbus.util.objOrNull
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.parseObject
import com.zalchik.nimbus.util.parseQuery
import com.zalchik.nimbus.util.percentDecode
import com.zalchik.nimbus.util.percentEncode
import com.zalchik.nimbus.util.putIf
import com.zalchik.nimbus.util.putIfNonEmpty
import com.zalchik.nimbus.util.splitUrl
import com.zalchik.nimbus.util.str
import com.zalchik.nimbus.util.strOrNull
import com.zalchik.nimbus.util.strings
import com.zalchik.nimbus.util.stringify
import com.zalchik.nimbus.util.toHexString
import com.zalchik.nimbus.util.walkStrings
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The three helpers the whole port is built on. They stand in for Python's json, base64 and
 * urllib on a phone where `android.util.Base64` throws in a JVM test and `java.net.URI` refuses
 * the links real providers hand out — so their tolerances are the behaviour under test.
 */
class UtilTests {
    // -- json -----------------------------------------------------------------------------------

    @Test
    fun objectsDropWhatWasNeverSet() {
        val built = obj("a" to 1, "b" to null, "c" to "x")
        assertEquals(setOf("a", "c"), built.keys().asSequence().toSet())

        val nonEmpty = objNonEmpty(
            "text" to "",
            "list" to JSONArray(),
            "empty" to JSONObject(),
            "off" to false,
            "on" to true,
            "zero" to 0,
            "kept" to "value",
        )
        // An absent option stays absent, which is what sing-box's omitempty means for it.
        assertEquals(setOf("on", "zero", "kept"), nonEmpty.keys().asSequence().toSet())
        assertTrue(isEmptyJson(null))
        assertTrue(isEmptyJson(""))
        assertFalse(isEmptyJson("x"))
        assertFalse(isEmptyJson(0))
    }

    @Test
    fun arraysSkipNulls() {
        assertEquals(listOf("a", "b"), arr("a", null, "b").strings())
        assertEquals(listOf("a"), arr(listOf("a", null)).strings())
        assertEquals(0, arr(emptyList<String>()).length())
    }

    @Test
    fun accessorsNeverThrow() {
        val json = parseObject("""{"s":"text","n":"42","i":7,"l":12345678901,"b":"true","o":{"x":1}}""")!!
        assertEquals("text", json.str("s"))
        assertEquals("", json.str("missing"))
        assertEquals("fallback", json.str("missing", "fallback"))
        assertEquals(42, json.int("n"))
        assertEquals(7, json.int("i"))
        assertEquals(3, json.int("missing", 3))
        assertEquals(12345678901L, json.long("l"))
        assertTrue(json.bool("b"))
        assertFalse(json.bool("missing"))
        assertEquals(1, json.objOrNull("o")?.int("x"))
        assertNull(json.objOrNull("missing"))
        assertEquals(0, json.arrOrEmpty("missing").length())
        assertNull(json.strOrNull("missing"))

        // A number where a string was expected, and the other way round.
        val odd = parseObject("""{"port":"443","name":42}""")!!
        assertEquals(443, odd.int("port"))
        assertEquals("42", odd.str("name"))
    }

    @Test
    fun listsComeFromEitherShape() {
        val json = parseObject("""{"a":["x","y"],"b":"z","c":null}""")!!
        assertEquals(listOf("x", "y"), listOfStrings(json.opt("a")))
        // A panel that sends one value instead of a list still loads.
        assertEquals(listOf("z"), listOfStrings(json.opt("b")))
        assertEquals(emptyList<String>(), listOfStrings(json.opt("c")))
        assertEquals(emptyList<String>(), listOfStrings(json.opt("missing")))
    }

    @Test
    fun aDeepCopyIsIndependent() {
        val original = parseObject("""{"a":{"b":[1,2]},"c":"x"}""")!!
        val copy = original.copy()
        assertEquals(original.toString(), copy.toString())
        copy.objOrNull("a")!!.arrOrEmpty("b").put(3)
        copy.put("c", "changed")
        // Editing the copy must not reach the stored server config it was built from.
        assertEquals(2, original.objOrNull("a")!!.arrOrEmpty("b").length())
        assertEquals("x", original.str("c"))

        val array = JSONArray("""[{"a":1},[2]]""")
        val arrayCopy = array.copy()
        arrayCopy.getJSONObject(0).put("a", 9)
        assertEquals(1, array.getJSONObject(0).int("a"))
    }

    @Test
    fun putIfKeepsWhatTheProviderSent() {
        val json = JSONObject()
        json.putIf("set", "x")
        json.putIf("skipped", null)
        assertEquals(setOf("set"), json.keys().asSequence().toSet())

        json.putIfNonEmpty("empty", "")
        json.putIfNonEmpty("kept", "y")
        assertEquals(setOf("set", "kept"), json.keys().asSequence().toSet())
    }

    @Test
    fun dropKeyRemovesEverywhereInTheTree() {
        val json = parseObject(
            """{"allowInsecure":true,"tls":{"allowInsecure":true,"serverName":"x"},
              |"list":[{"allowInsecure":false},{"keep":1}]}""".trimMargin()
        )!!
        dropKey(json, "allowInsecure")
        assertFalse(json.toString().contains("allowInsecure"))
        assertEquals("x", json.objOrNull("tls")!!.str("serverName"))
        assertEquals(1, json.arrOrEmpty("list").objects()[1].int("keep"))
    }

    @Test
    fun walkStringsVisitsEveryStringWithItsKey() {
        val json = parseObject("""{"a":"one","b":{"c":"two"},"d":["three",4]}""")!!
        val found = mutableListOf<Pair<String, String>>()
        walkStrings(json) { key, value -> found.add(key to value) }
        assertTrue(found.contains("a" to "one"))
        assertTrue(found.contains("c" to "two"))
        assertTrue(found.contains("d" to "three"))
        assertEquals(3, found.size)
    }

    @Test
    fun stringifyLeavesNonAsciiAlone() {
        val json = obj("name" to "🇳🇱 Авто")
        assertTrue(stringify(json).contains("Авто"))
        assertFalse(stringify(json).contains("\\u"))
        assertTrue(stringify(json, 2).contains("\n"))
        assertEquals("null", stringify(null))
        assertEquals("\"x\"", stringify("x"))
        // Broken JSON is null rather than an exception: a pasted config is user input.
        assertNull(parseObject("{"))
        assertNull(parseObject("not json"))
    }

    // -- base64 ---------------------------------------------------------------------------------

    @Test
    fun base64RoundTrips() {
        val text = "vless://abc?x=1#🇳🇱"
        assertEquals(text, b64Decode(b64Encode(text)))
        val bytes = byteArrayOf(0, 1, -1, 127, -128)
        assertTrue(bytes.contentEquals(b64Bytes(b64Encode(bytes))!!))
    }

    @Test
    fun base64ToleratesWhatProvidersSend() {
        val padded = b64Encode("hello")
        // Missing padding, url-safe alphabet, whitespace and a stray BOM all arrive in the wild.
        assertEquals("hello", b64Decode(padded.trimEnd('=')))
        assertEquals("hello", b64Decode(padded.replace('+', '-').replace('/', '_')))
        assertEquals("hello", b64Decode("  $padded\n"))
        assertEquals("hello", b64DecodeOrSelf(b64Encode("hello")))
        // Not base64: the text itself, so a plain list of links stays one.
        assertEquals("vless://x", b64DecodeOrSelf("vless://x"))
        assertNull(b64Decode("!!!!"))
        assertNull(b64Clean("!!"))
        assertNotNull(b64Clean(padded))
    }

    @Test
    fun hexRoundTrips() {
        assertEquals("00ff10", byteArrayOf(0, -1, 16).toHexString())
        assertTrue(byteArrayOf(0, -1, 16).contentEquals("00ff10".hexToBytes()))
        assertTrue(byteArrayOf(0xAB.toByte()).contentEquals("ab".hexToBytes()))
        assertEquals(0, "".hexToBytes().size)
    }

    // -- urls -----------------------------------------------------------------------------------

    @Test
    fun shareLinksSplitIntoParts() {
        val parts = splitUrl("vless://${Fixtures.UUID}@1.2.3.4:443?type=tcp&sni=x#NL%20Reality")
        assertEquals("vless", parts.scheme)
        assertEquals(Fixtures.UUID, parts.username)
        assertEquals("1.2.3.4", parts.host)
        assertEquals(443, parts.port)
        assertEquals("type=tcp&sni=x", parts.query)
        assertEquals("NL%20Reality", parts.fragment)
        assertEquals("1.2.3.4:443", parts.netloc)

        // A password arrives percent-encoded, because a raw @ or : would end it early. User info
        // ends at the last @, so the host is read correctly even with an encoded one in it.
        val odd = splitUrl("trojan://p%40ss%3Aword@t.example.com:443?sni=t#name")
        assertEquals("t.example.com", odd.host)
        assertEquals(443, odd.port)
        assertEquals("p%40ss%3Aword", odd.username)
        assertEquals("", odd.password)
        assertEquals("sni=t", odd.query)
        assertEquals("name", odd.fragment)

        // The first unencoded colon is what splits a user from a password, as in RFC 3986.
        val withUser = splitUrl("socks://user:p%40ss@10.0.0.1:1080")
        assertEquals("user", withUser.username)
        assertEquals("p%40ss", withUser.password)
        assertEquals("10.0.0.1", withUser.host)

        // No port, no query, no fragment, and an IPv6 host.
        assertEquals(0, splitUrl("https://example.com/path").port)
        assertEquals("example.com", splitUrl("https://example.com/path").host)
        assertEquals("/path", splitUrl("https://example.com/path").path)
        assertEquals("2001:db8::1", splitUrl("vless://x@[2001:db8::1]:443").host)
        assertEquals(443, splitUrl("vless://x@[2001:db8::1]:443").port)
    }

    @Test
    fun queriesKeepTheFirstValueAndDecodeIt() {
        val query = parseQuery("a=1&b=&a=2&c=%2Fx%20y&d")
        assertEquals("1", query["a"])
        assertEquals("", query["b"])
        assertEquals("/x y", query["c"])
        assertEquals("", query["d"])
        assertTrue(parseQuery("").isEmpty())
        // A "+" in a query is a space, as Python's parse_qs reads it.
        assertEquals("a b", parseQuery("x=a+b")["x"])
    }

    @Test
    fun percentCodingMatchesPython() {
        assertEquals("NL Reality", percentDecode("NL%20Reality"))
        assertEquals("🇳🇱 Авто", percentDecode("%F0%9F%87%B3%F0%9F%87%B1%20%D0%90%D0%B2%D1%82%D0%BE"))
        assertEquals(
            "%F0%9F%87%B3%F0%9F%87%B1%20%D0%90%D0%B2%D1%82%D0%BE",
            percentEncode("🇳🇱 Авто"),
        )
        assertEquals("a_b-c.d~e", percentEncode("a_b-c.d~e"))
        assertEquals("a/b", percentEncode("a/b", safe = "/"))
        // A stray percent is left alone rather than throwing, as unquote does.
        assertEquals("100%", percentDecode("100%"))
        assertEquals("zz", percentDecode("zz"))
    }

    @Test
    fun csvHostAndPortHelpers() {
        assertEquals(listOf("a", "b", "c"), csvList("a,b, c"))
        assertEquals(listOf("h2", "http/1.1"), csvList("h2,http/1.1"))
        assertTrue(csvList("").isEmpty())
        assertTrue(csvList(null).isEmpty())

        assertEquals("1.2.3.4:443", hostPort("1.2.3.4", 443))
        assertEquals("[2001:db8::1]:443", hostPort("2001:db8::1", 443))
        assertEquals("[2001:db8::1]:443", hostPort("[2001:db8::1]", 443))

        assertEquals("example.com", hostOf("https://example.com/path"))
        assertEquals("example.com", hostOf("example.com:443"))
        assertEquals("2001:db8::1", hostOf("[2001:db8::1]:443"))
        assertEquals("example.com", hostOf("example.com."))
    }
}
