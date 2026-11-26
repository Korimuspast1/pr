package com.zalchik.nimbus

import com.zalchik.nimbus.data.HappKeys
import com.zalchik.nimbus.engine.Deeplink
import com.zalchik.nimbus.engine.Happ
import com.zalchik.nimbus.engine.HappError
import com.zalchik.nimbus.util.hexToBytes
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import java.math.BigInteger

/**
 * Encrypted Happ links open to what they carry — the desktop's HappCryptTests, unchanged: the
 * same RFC 8439 vector, the same real crypt5 link, the same nesting of one link inside another.
 * crypt1–4 are RSA blocks of the published key table; crypt5 wraps a ChaCha20-Poly1305 key in
 * one and scrambles the bytes.
 */
class HappTests {
    companion object {
        private const val SUB = "https://panel.example.com/sub/AbCdEf123456"

        // A real link, as a panel hands it out: opens to https://example.com/sub.
        private const val CRYPT5 = "happ://crypt5/fzvdO4bMOfTWNaB3taWRhRaF64soexaE9dm3ZlLK0Rke9Rz3BG1f9gmj4tSDpjRSWWdX5G6oaaQK9Gs+fdJPWNXIF08BsaeifWtfTlCvC/nSWDv0ZrofgJXkQ8MlUk63CoJkt7RvXAfablYXb/cYWEZJQkDMyMaE5/1KegAbWVWVI60MPqDylUyYoLtOeOOX9amvELecOZ4kKz1QVqgE9uCBz3py+3Ghr1iVGKOhFwb98OFP+j0tGvDo/3d609DVq3RwBGXu1ogZ7PTc3/A5IlaA3Hff5IlVujozQ3ywmQBsTGd+l3AHJAX1oDbPkRSSwg7Y7hl3AKXKpZsEhMzPbJY8UxZ7GmVsxeROLopVx85ACqakzg+ZZwdZslfKgdRzUmL9Mv895HDOHE3tbh6qnDhE9Ew/Epx1iBCb2HjorOLDBluH8ztdL9mdUX+turjC4GLN0YR55P3H23A0W5zl0di5YfrI2nUBKxh30lUVG9NbqYKlwxgmhxAUZrQ6cnFGF2VuZ9VJLQ3sQ8rqXTtfau8ySbu770Hd6vVEun8aSJm4W4M0DagKbORL4A4M6Cuf1v/jj7EWhA9yhhcSuxkc6WUWMGYRraWBM9vSrSbjeT1U69a3n9T56M/TOJWf4z8fXrHRnCR1tfzwrjHiOJfZGiKvbAd4k6f+VADYpLdCq+ornEElv7V0sByfwPTgep+Q33Qkl67ArHpmbZDcCyWkGz0BoUzGJFe58YiS/oNFVdufbuDnFd1ArAVMztJJJbxlo4Is48+Iof=ff"

        private val RSA_VERSION = mapOf(1 to "crypt", 2 to "crypt2", 3 to "crypt3", 4 to "crypt4")

        /**
         * A link the way a panel makes one: PKCS#1 v1.5 blocks of the published key. The padding
         * is fixed, so the link is the same every run — which is what makes it a usable vector.
         */
        private fun link(text: String, version: Int): String {
            val (modulus, exponent) = Happ.publicPart(HappKeys.PKCS1.getValue(RSA_VERSION.getValue(version)))
            val size = (modulus.bitLength() + 7) / 8
            val body = text.toByteArray(Charsets.UTF_8)
            require(body.size <= size - 11) { "too long for the key" }
            val block = ByteArray(size)
            block[1] = 2
            for (i in 2 until size - body.size - 1) block[i] = 0xFF.toByte()
            block[size - body.size - 1] = 0
            body.copyInto(block, size - body.size)
            val cipher = BigInteger(1, block).modPow(exponent, modulus).toByteArray()
            val exact = when {
                cipher.size == size -> cipher
                cipher.size > size -> cipher.copyOfRange(cipher.size - size, cipher.size)
                else -> ByteArray(size - cipher.size) + cipher
            }
            val payload = com.zalchik.nimbus.util.b64Encode(exact, urlSafe = true, pad = false)
            val action = if (version == 1) "crypt" else "crypt$version"
            return "happ://$action/$payload"
        }

    }

    @Test
    fun aeadMatchesRfc8439() {
        val key = ByteArray(32) { (0x80 + it).toByte() }
        val nonce = "070000004041424344454647".hexToBytes()
        val aad = "50515253c0c1c2c3c4c5c6c7".hexToBytes()
        val sealed = (
            "d31a8d34648e60db7b86afbc53ef7ec2a4aded51296e08fea9e2b5a736ee62d6" +
                "3dbea45e8ca9671282fafb69da92728b1a71de0a9e060b2905d6a5b67ecd3b36" +
                "92ddbd7f2d778b8c9803aee328091b58fab324e4fad675945585808b4831d7bc" +
                "3ff4def08e4b7a9de576d26586cec64b6116" +
                "1ae10b594f09e26a7e902ecbd0600691"
            ).hexToBytes()

        val plain = Happ.aeadOpen(key, nonce, sealed, aad)
        assertEquals("Ladies an", String(plain.copyOfRange(0, 9), Charsets.UTF_8))

        // A changed byte must not decode: the Poly1305 tag is what makes a link trustworthy.
        val damaged = sealed.copyOf().also { it[it.size - 1] = (it[it.size - 1].toInt() xor 1).toByte() }
        assertThrows(HappError::class.java) { Happ.aeadOpen(key, nonce, damaged, aad) }
    }

    @Test
    fun rsaLinksOpen() {
        for (version in 1..4) {
            assertEquals("crypt$version", SUB, Happ.unwrap(link(SUB, version)))
            // The short form some providers hand out: the action word after crypt:// instead.
            assertEquals(
                "crypt$version short",
                SUB,
                Happ.unwrap(link(SUB, version).replace("happ://", "crypt://")),
            )
        }
        // A link inside a link, as Happ nests them.
        assertEquals(SUB, Happ.unwrap(link(link(SUB, 1), 4)))
    }

    @Test
    fun crypt5Opens() {
        assertEquals("https://example.com/sub", Happ.unwrap(CRYPT5))

        val damaged = CRYPT5.substring(0, 220) +
            (if (CRYPT5[220] == 'A') "B" else "A") +
            CRYPT5.substring(221)
        assertThrows(HappError::class.java) { Happ.unwrap(damaged) }
        assertThrows(HappError::class.java) { Happ.unwrap("happ://crypt5/AAAAAAAAAAAA") }
    }

    @Test
    fun linksThatWillNotOpen() {
        for (text in listOf("happ://crypt/abcdef", "happ://crypt4/not-base64!", "happ://crypt9/x", "happ://crypt5/")) {
            assertThrows(text, HappError::class.java) { Happ.unwrap(text) }
        }
    }

    @Test
    fun partsNameTheVersionAndPayload() {
        assertEquals(1 to "abc", Happ.parts("happ://crypt/abc"))
        assertEquals(4 to "abc", Happ.parts("happ://crypt4/abc"))
        // crypt:// without a version: try them all, which is what version 0 means.
        assertEquals(0 to "abc", Happ.parts("crypt://abc"))
        assertEquals(0 to "abc", Happ.parts("crypt://url=abc"))
        assertTrue(Happ.isCryptLink("happ://crypt2/xyz"))
        assertTrue(Happ.isCryptLink("CRYPT://xyz"))
        assertTrue(!Happ.isCryptLink("happ://add/xyz"))
        assertTrue(!Happ.isCryptLink("https://example.com"))
        assertThrows(HappError::class.java) { Happ.parts("happ://install/x") }
    }

    @Test
    fun deeplinkAddsWhatTheLinkCarries() {
        val encoded = Deeplink.parse(link(SUB, 4))
        assertEquals("subscription", encoded?.kind)
        assertEquals(SUB, encoded?.payload)
        assertEquals("subscription", Deeplink.parse(link(SUB, 4).replace("happ://", "crypt://"))?.kind)
        assertEquals(SUB, Deeplink.parse(link(link(SUB, 1), 4))?.payload)

        val vless = Fixtures.LINKS.getValue("vless-reality")
        val configs = Deeplink.parse(link(vless, 4))
        assertEquals("configs", configs?.kind)
        assertEquals(vless, configs?.payload)

        assertEquals("subscription", Deeplink.parse("happ://add/" + link(SUB, 4))?.kind)
        assertEquals("crypt", Deeplink.parse("happ://crypt/abcdef")?.kind)
        assertEquals("subscription", Deeplink.parse(CRYPT5)?.kind)
        assertTrue(Deeplink.isDeepLink(link(SUB, 4)))
        // A plain percent-encoded URL is not a deep link: it is what a deep link carries.
        assertNull(Deeplink.parse(java.net.URLEncoder.encode(SUB, "UTF-8")))
    }

    @Test
    fun keyTableIsComplete() {
        // Every RSA version has a key, and crypt5 keys parse as keys (not as garbage base64).
        assertEquals(setOf("crypt", "crypt2", "crypt3", "crypt4"), RSA_VERSION.values.toSet())
        for (name in RSA_VERSION.values) {
            val (modulus, exponent) = Happ.publicPart(HappKeys.PKCS1.getValue(name))
            assertEquals(BigInteger.valueOf(65537), exponent)
            assertTrue(name, modulus.bitLength() >= 1024)
        }
        assertTrue(HappKeys.CRYPT5.isNotEmpty())
        for ((marker, key) in HappKeys.CRYPT5) {
            assertEquals(marker, 8, marker.length)
            val (_, exponent) = Happ.publicPart(key)
            assertEquals(BigInteger.valueOf(65537), exponent)
        }
    }
}
