package com.zalchik.nimbus.engine

import com.zalchik.nimbus.data.HappKeys
import com.zalchik.nimbus.util.b64Bytes
import java.math.BigInteger
import java.net.URLDecoder
import java.nio.charset.CharacterCodingException
import java.nio.charset.CodingErrorAction
import java.nio.charset.StandardCharsets

/**
 * Happ's encrypted links: happ://crypt…/… and crypt://… — a subscription URL or the share links
 * inside, encrypted to a key the Happ app carries. Port of nimbus/happ.py.
 *
 *     happ://crypt/…  crypt2  crypt3  crypt4   one RSA PKCS#1 v1.5 block per key size
 *     happ://crypt5/…                          an RSA-wrapped ChaCha20-Poly1305 key, a nonce and
 *                                              the ciphertext, scrambled by two byte swaps
 *
 * The plaintext is the link the person holding it already has: their own subscription. It is
 * decrypted on the phone so a link from a provider's chat or an "add to Happ" button opens in
 * Nimbus like any other subscription link, instead of being a dead end.
 *
 * No third-party crypto library, as on desktop: RSA is one modular exponentiation and
 * ChaCha20-Poly1305 (RFC 8439) is short enough to keep here. Everything stays local.
 */
class HappError(message: String) : Exception(message)

object Happ {
    /** A link is a few kilobytes at most; the cap keeps a pasted wall of text from being chewed on. */
    const val MAX_PAYLOAD = 64 * 1024

    private val RSA_VERSION = mapOf(1 to "crypt", 2 to "crypt2", 3 to "crypt3", 4 to "crypt4")
    private val ACTIONS = mapOf("crypt" to 1, "crypt1" to 1, "crypt2" to 2, "crypt3" to 3, "crypt4" to 4, "crypt5" to 5)
    private val CRYPT_ACTION = Regex("(?i)^crypt\\d*$")

    // -- RSA -------------------------------------------------------------------------------

    private class RsaKey(val n: BigInteger, val d: BigInteger) {
        val size: Int get() = (n.bitLength() + 7) / 8

        /** One PKCS#1 v1.5 block: 00 02 <padding> 00 <message>. */
        fun decrypt(block: ByteArray): ByteArray {
            if (block.size != size) {
                throw HappError("a ${block.size}-byte block, the key takes $size")
            }
            val em = bigToBe(BigInteger(1, block).modPow(d, n), size)
            if (em.size < 2 || em[0] != 0.toByte() || em[1] != 2.toByte()) {
                throw HappError("the block doesn't belong to this key")
            }
            var end = -1
            for (i in 2 until em.size) {
                if (em[i] == 0.toByte()) {
                    end = i
                    break
                }
            }
            if (end < 10) throw HappError("bad padding")
            return em.copyOfRange(end + 1, em.size)
        }
    }

    /** Tag, contents and the offset just past one DER element. */
    private fun tlv(data: ByteArray, position: Int): Triple<Int, ByteArray, Int> {
        var pos = position
        if (pos + 2 > data.size) throw HappError("truncated key")
        val tag = data[pos].toInt() and 0xFF
        var length = data[pos + 1].toInt() and 0xFF
        pos += 2
        if (length and 0x80 != 0) {
            val n = length and 0x7F
            if (n < 1 || n > 4 || pos + n > data.size) throw HappError("bad key length")
            length = 0
            for (i in 0 until n) length = (length shl 8) or (data[pos + i].toInt() and 0xFF)
            pos += n
        }
        if (pos + length > data.size) throw HappError("truncated key")
        return Triple(tag, data.copyOfRange(pos, pos + length), pos + length)
    }

    /** RSAPrivateKey (PKCS#1) or PrivateKeyInfo wrapping one (PKCS#8). */
    private fun keyFromDer(der: ByteArray): RsaKey {
        val (tag, body, _) = tlv(der, 0)
        if (tag != 0x30) throw HappError("not a key")
        val (firstTag, _, afterFirst) = tlv(body, 0)
        if (firstTag != 0x02) throw HappError("not a key")
        var pos = afterFirst
        if (pos < body.size && (body[pos].toInt() and 0xFF) == 0x30) {
            // PKCS#8: version, algorithm, OCTET STRING holding the PKCS#1 key.
            pos = tlv(body, pos).third
            val (innerTag, inner, _) = tlv(body, pos)
            if (innerTag != 0x04) throw HappError("not a key")
            return keyFromDer(inner)
        }
        // PKCS#1: version, n, e, d, …
        val (_, modulus, afterModulus) = tlv(body, pos)
        pos = tlv(body, afterModulus).third
        val (_, privateExponent, _) = tlv(body, pos)
        return RsaKey(BigInteger(1, modulus), BigInteger(1, privateExponent))
    }

    private val parsed = HashMap<String, RsaKey>()

    private fun key(encoded: String): RsaKey = synchronized(parsed) {
        parsed.getOrPut(encoded) {
            val der = b64Bytes(encoded) ?: throw HappError("bad base64 key")
            keyFromDer(der)
        }
    }

    /**
     * (modulus, public exponent) of a table key. The tests use it to encrypt a link the way a
     * panel does — the padding is fixed, so the link is the same every run.
     */
    fun publicPart(encoded: String): Pair<BigInteger, BigInteger> {
        val der = b64Bytes(encoded) ?: throw HappError("bad base64 key")
        val (tag, body, _) = tlv(der, 0)
        if (tag != 0x30) throw HappError("not a key")
        val (firstTag, _, afterFirst) = tlv(body, 0)
        if (firstTag != 0x02) throw HappError("not a key")
        var pos = afterFirst
        if (pos < body.size && (body[pos].toInt() and 0xFF) == 0x30) {
            // PKCS#8: the PKCS#1 key sits in the OCTET STRING after the algorithm identifier.
            pos = tlv(body, pos).third
            val (innerTag, inner, _) = tlv(body, pos)
            if (innerTag != 0x04) throw HappError("not a key")
            return publicPartOfPkcs1(inner)
        }
        return publicPartOfPkcs1(der)
    }

    private fun publicPartOfPkcs1(der: ByteArray): Pair<BigInteger, BigInteger> {
        val (_, body, _) = tlv(der, 0)
        val (_, _, afterVersion) = tlv(body, 0)          // version
        val (_, modulus, afterModulus) = tlv(body, afterVersion)
        val (_, exponent, _) = tlv(body, afterModulus)   // public exponent
        return BigInteger(1, modulus) to BigInteger(1, exponent)
    }

    // -- ChaCha20-Poly1305 (RFC 8439) ---------------------------------------------------------

    private const val MASK = 0xFFFFFFFFL

    private fun rotl(value: Int, bits: Int): Int = (value shl bits) or (value ushr (32 - bits))

    private fun leToBig(bytes: ByteArray): BigInteger {
        val reversed = bytes.copyOf().reversedArray()
        return BigInteger(1, reversed)
    }

    private fun bigToLe(value: BigInteger, length: Int): ByteArray {
        val out = ByteArray(length)
        var current = value
        val ff = BigInteger.valueOf(255)
        for (i in 0 until length) {
            out[i] = (current.and(ff)).toByte()
            current = current.shiftRight(8)
        }
        return out
    }

    private fun bigToBe(value: BigInteger, length: Int): ByteArray {
        val out = ByteArray(length)
        var current = value
        val ff = BigInteger.valueOf(255)
        for (i in length - 1 downTo 0) {
            out[i] = (current.and(ff)).toByte()
            current = current.shiftRight(8)
        }
        return out
    }

    private fun chacha20Block(key: ByteArray, counter: Int, nonce: ByteArray): ByteArray {
        val state = IntArray(16)
        state[0] = 0x61707865
        state[1] = 0x3320646E
        state[2] = 0x79622D32
        state[3] = 0x6B206574
        for (i in 0 until 8) {
            state[4 + i] = leInt(key, i * 4)
        }
        state[12] = counter
        for (i in 0 until 3) {
            state[13 + i] = leInt(nonce, i * 4)
        }
        val x = state.copyOf()

        fun quarter(a: Int, b: Int, c: Int, d: Int) {
            x[a] = (x[a] + x[b]).toInt()
            x[d] = rotl(x[d] xor x[a], 16)
            x[c] = (x[c] + x[d]).toInt()
            x[b] = rotl(x[b] xor x[c], 12)
            x[a] = (x[a] + x[b]).toInt()
            x[d] = rotl(x[d] xor x[a], 8)
            x[c] = (x[c] + x[d]).toInt()
            x[b] = rotl(x[b] xor x[c], 7)
        }

        repeat(10) {
            quarter(0, 4, 8, 12); quarter(1, 5, 9, 13); quarter(2, 6, 10, 14); quarter(3, 7, 11, 15)
            quarter(0, 5, 10, 15); quarter(1, 6, 11, 12); quarter(2, 7, 8, 13); quarter(3, 4, 9, 14)
        }
        val out = ByteArray(64)
        for (i in 0 until 16) {
            val value = (x[i].toLong() and MASK) + (state[i].toLong() and MASK) and MASK
            out[i * 4] = (value and 0xFF).toByte()
            out[i * 4 + 1] = ((value shr 8) and 0xFF).toByte()
            out[i * 4 + 2] = ((value shr 16) and 0xFF).toByte()
            out[i * 4 + 3] = ((value shr 24) and 0xFF).toByte()
        }
        return out
    }

    private fun leInt(bytes: ByteArray, offset: Int): Int =
        (bytes[offset].toInt() and 0xFF) or
            ((bytes[offset + 1].toInt() and 0xFF) shl 8) or
            ((bytes[offset + 2].toInt() and 0xFF) shl 16) or
            ((bytes[offset + 3].toInt() and 0xFF) shl 24)

    private fun chacha20Xor(key: ByteArray, counter: Int, nonce: ByteArray, data: ByteArray): ByteArray {
        val out = ByteArray(data.size)
        var offset = 0
        while (offset < data.size) {
            val stream = chacha20Block(key, counter + offset / 64, nonce)
            val end = minOf(offset + 64, data.size)
            for (i in offset until end) out[i] = (data[i].toInt() xor stream[i - offset].toInt()).toByte()
            offset = end
        }
        return out
    }

    private fun poly1305(key: ByteArray, message: ByteArray): ByteArray {
        val r = leToBig(key.copyOfRange(0, 16)).and(BigInteger("0FFFFFFC0FFFFFFC0FFFFFFC0FFFFFFC0FFFFFFF", 16))
        val s = leToBig(key.copyOfRange(16, 32))
        val modulus = BigInteger.ONE.shiftLeft(130).subtract(BigInteger.valueOf(5))
        var acc = BigInteger.ZERO
        var offset = 0
        while (offset < message.size) {
            val end = minOf(offset + 16, message.size)
            val chunk = message.copyOfRange(offset, end) + 1.toByte()
            acc = acc.add(leToBig(chunk)).multiply(r).mod(modulus)
            offset = end
        }
        return bigToLe(acc.add(s), 16)
    }

    /** ChaCha20-Poly1305 open; the tag is the last 16 bytes of `sealed`. */
    fun aeadOpen(key: ByteArray, nonce: ByteArray, sealed: ByteArray, aad: ByteArray = ByteArray(0)): ByteArray {
        if (key.size != 32 || nonce.size != 12 || sealed.size < 16) {
            throw HappError("bad ChaCha20-Poly1305 input")
        }
        val body = sealed.copyOfRange(0, sealed.size - 16)
        val tag = sealed.copyOfRange(sealed.size - 16, sealed.size)
        val mac = poly1305(
            chacha20Block(key, 0, nonce).copyOfRange(0, 32),
            aad + ByteArray((16 - aad.size % 16) % 16) + body + ByteArray((16 - body.size % 16) % 16) +
                bigToLe(BigInteger.valueOf(aad.size.toLong()), 8) +
                bigToLe(BigInteger.valueOf(body.size.toLong()), 8),
        )
        if (!constantTimeEquals(mac, tag)) {
            throw HappError("the link is damaged or was encrypted to another key")
        }
        return chacha20Xor(key, 1, nonce, body)
    }

    private fun constantTimeEquals(left: ByteArray, right: ByteArray): Boolean {
        if (left.size != right.size) return false
        var diff = 0
        for (i in left.indices) diff = diff or (left[i].toInt() xor right[i].toInt())
        return diff == 0
    }

    // -- the formats --------------------------------------------------------------------------

    /** Happ's base64: url-safe or standard alphabet, padding optional, whitespace tolerated. */
    private fun payloadBytes(text: String): ByteArray {
        val clean = text.split(Regex("\\s")).joinToString("").replace('-', '+').replace('_', '/').replace("=", "")
        if (clean.isEmpty()) throw HappError("empty payload")
        if (clean.length > MAX_PAYLOAD) throw HappError("the payload is too long for a link")
        return b64Bytes(clean) ?: throw HappError("bad base64 payload")
    }

    /** AB -> BA over every pair; its own inverse. */
    fun swapPairs(data: ByteArray): ByteArray {
        val out = data.copyOf()
        var i = 0
        while (i < out.size - 1) {
            val tmp = out[i]
            out[i] = out[i + 1]
            out[i + 1] = tmp
            i += 2
        }
        return out
    }

    /** ABCD -> CDAB over every whole four-byte block; its own inverse. */
    fun swapBlockHalves(data: ByteArray): ByteArray {
        val out = data.copyOf()
        var i = 0
        while (i < out.size - 3) {
            val a = out[i]
            val b = out[i + 1]
            out[i] = out[i + 2]
            out[i + 1] = out[i + 3]
            out[i + 2] = a
            out[i + 3] = b
            i += 4
        }
        return out
    }

    private fun text(data: ByteArray): String {
        val decoder = StandardCharsets.UTF_8.newDecoder()
            .onMalformedInput(CodingErrorAction.REPORT)
            .onUnmappableCharacter(CodingErrorAction.REPORT)
        return try {
            decoder.decode(java.nio.ByteBuffer.wrap(data)).toString()
        } catch (_: CharacterCodingException) {
            throw HappError("the plaintext isn't text")
        }
    }

    /** crypt … crypt4: the whole payload is PKCS#1 v1.5 blocks of one key. */
    private fun rsa(payload: String, version: Int): String {
        val name = RSA_VERSION[version] ?: throw HappError("unknown version $version")
        val encoded = HappKeys.PKCS1[name] ?: throw HappError("no key for $name")
        val key = key(encoded)
        val data = payloadBytes(payload)
        if (data.isEmpty()) throw HappError("the payload isn't base64")
        if (data.size % key.size != 0) {
            throw HappError("the payload isn't a multiple of ${key.size} bytes")
        }
        val blocks = data.size / key.size
        if (blocks > 16) throw HappError("the payload has too many blocks")
        val out = java.io.ByteArrayOutputStream()
        for (i in 0 until blocks) {
            out.write(key.decrypt(data.copyOfRange(i * key.size, (i + 1) * key.size)))
        }
        return text(out.toByteArray())
    }

    /** nonce | salt? | <ciphertext length in digits> | mark | ciphertext | RSA-wrapped key. */
    private fun crypt5Body(body: ByteArray, key: RsaKey, salted: Boolean): String {
        if (body.size < 13) throw HappError("the payload is too short")
        val nonce = body.copyOfRange(0, 12)
        val salt: ByteArray
        val start: Int
        if (salted) {
            if (body.size < 22) throw HappError("the payload is too short")
            salt = body.copyOfRange(14, 22)
            start = 22
        } else {
            salt = ByteArray(0)
            start = 12
        }
        var end = start
        while (end < body.size && body[end].toInt() in 0x30..0x39) end++
        if (end == start || end - start > 9) throw HappError("the payload has no ciphertext length")
        val segment = String(body, start, end - start, StandardCharsets.ISO_8859_1).toInt()
        val packed = body.copyOfRange(end, body.size)
        if (segment < 1 || segment + 1 > packed.size) throw HappError("the payload is truncated")

        val wrappedKeyText = String(packed, segment + 1, packed.size - segment - 1, StandardCharsets.ISO_8859_1)
        val wrapped = swapPairs(key.decrypt(payloadBytes(wrappedKeyText)))
        val contentKey = payloadBytes(String(wrapped, StandardCharsets.ISO_8859_1)).toMutableList()
        if (contentKey.size != 32) throw HappError("a ${contentKey.size}-byte content key, 32 expected")
        if (salt.isNotEmpty()) {
            for (i in contentKey.indices) {
                contentKey[i] = (contentKey[i].toInt() xor salt[i % salt.size].toInt()).toByte()
            }
        }
        val sealedText = String(packed, 1, segment, StandardCharsets.ISO_8859_1)
        val sealed = payloadBytes(sealedText)
        val plain = swapPairs(aeadOpen(contentKey.toByteArray(), nonce, sealed))
        return text(payloadBytes(String(plain, StandardCharsets.ISO_8859_1)))
    }

    /** crypt5: the link names its key by an 8-character marker and swaps bytes twice. */
    private fun crypt5(payload: String): String {
        val shuffled = swapBlockHalves(payload.toByteArray(StandardCharsets.ISO_8859_1))
        if (shuffled.size < 9) throw HappError("the payload is too short")
        val marker = String(shuffled.copyOfRange(0, 4) + shuffled.copyOfRange(shuffled.size - 4, shuffled.size), StandardCharsets.ISO_8859_1)
        val encoded = HappKeys.CRYPT5[marker] ?: throw HappError("unknown crypt5 key '$marker'")
        val body = shuffled.copyOfRange(4, shuffled.size - 4)
        val key = key(encoded)
        // Older links have no salt; where the salt would be, a digit means exactly that.
        val salted = body.size > 12 && body[12].toInt() !in 0x30..0x39
        var last: HappError? = null
        for (attempt in listOf(salted, !salted)) {
            try {
                return crypt5Body(body, key, attempt)
            } catch (e: HappError) {
                last = e
            }
        }
        throw last ?: HappError("crypt5 key '$marker' doesn't open this link")
    }

    /** The plaintext of an encrypted payload. version 1–5 as named in the link, 0 = try them. */
    fun decrypt(payload: String, version: Int = 0): String {
        val text = payload.trim()
        if (text.isEmpty()) throw HappError("the link is empty")
        if (version == 5) return crypt5(text)
        if (RSA_VERSION.containsKey(version)) return rsa(text, version)
        var error: HappError = HappError("nothing to try")
        for (candidate in listOf(4, 3, 2, 1)) {
            try {
                return rsa(text, candidate)
            } catch (e: HappError) {
                error = e
            }
        }
        try {
            return crypt5(text)
        } catch (e: HappError) {
            error = e
        }
        throw error
    }

    // -- links ---------------------------------------------------------------------------------

    fun isCryptLink(text: String?): Boolean {
        val value = (text ?: "").trim()
        val index = value.indexOf("://")
        if (index < 0) return false
        val scheme = value.substring(0, index).lowercase()
        val rest = value.substring(index + 3)
        if (scheme == "happ") {
            val action = rest.trimStart('/').substringBefore('/').substringBefore('?')
            return CRYPT_ACTION.matches(action)
        }
        return scheme == "crypt"
    }

    /** A payload: no query or fragment (neither can appear in base64), percent-decoded once. */
    private fun payloadOf(value: String): String =
        decode(value.substringBefore('?').substringBefore('#').trim())

    private fun decode(value: String): String = try {
        URLDecoder.decode(value.replace("+", "%2B"), "UTF-8")
    } catch (_: Exception) {
        value
    }

    /** (version, payload) of a crypt link; version 0 when the link doesn't say. */
    fun parts(text: String): Pair<Int, String> {
        val index = text.trim().indexOf("://")
        if (index < 0) throw HappError("not a link")
        val scheme = text.trim().substring(0, index).lowercase()
        var rest = text.trim().substring(index + 3)
        if (rest.isEmpty()) throw HappError("not a link")
        rest = rest.trimStart('/')
        val slash = rest.indexOf('/')
        var action = if (slash < 0) rest else rest.substring(0, slash)
        var payload = if (slash < 0) "" else rest.substring(slash + 1)
        if ('?' in action) {
            val query = action.substringAfter('?')
            action = action.substringBefore('?')
            if (payload.isEmpty()) payload = query.substringAfter("url=", "")
        }
        val known = ACTIONS[action.lowercase()]
        if (known != null) return known to payloadOf(payload)
        if (scheme == "crypt") {
            var value = rest.substringAfter('?', "")
            if (value.isEmpty()) value = rest
            if (value.startsWith("url=")) value = value.substring(4)
            return 0 to payloadOf(value)
        }
        throw HappError("unsupported Happ action: $action")
    }

    /** What an encrypted link carries. A link inside a link is followed, as Happ does. */
    fun unwrap(text: String, depth: Int = 0): String {
        val (version, payload) = parts(text)
        val plain = decrypt(payload, version).trim()
        if (depth < 8 && isCryptLink(plain)) return unwrap(plain, depth + 1)
        return plain
    }
}
