package com.zalchik.nimbus.util

/**
 * Base64 as Nimbus reads it: url-safe or standard alphabet, padding optional, whitespace
 * tolerated — desktop's b64decode(). Implemented here rather than with android.util.Base64 so
 * the engine (links, Happ, sharing) runs unchanged in JVM unit tests.
 */

private const val STANDARD = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
private const val URL_SAFE = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"

private val WHITESPACE = Regex("\\s")

/** The alphabet-normalised, padded form of `text`, or null if it holds characters base64 can't. */
fun b64Clean(text: String): String? {
    val clean = WHITESPACE.replace(text, "").replace('-', '+').replace('_', '/').replace("=", "")
    if (clean.isEmpty()) return ""
    for (ch in clean) {
        if (STANDARD.indexOf(ch) < 0) return null
    }
    return clean + "=".repeat((4 - clean.length % 4) % 4)
}

/** Decoded bytes, or null when the text isn't base64 (a bad character, a stray length). */
fun b64Bytes(text: String): ByteArray? {
    val clean = b64Clean(text) ?: return null
    if (clean.isEmpty()) return ByteArray(0)
    val body = clean.trimEnd('=')
    val out = ByteArray(body.length * 3 / 4)
    var position = 0
    var buffer = 0
    var bits = 0
    for (ch in body) {
        buffer = (buffer shl 6) or STANDARD.indexOf(ch)
        bits += 6
        if (bits >= 8) {
            bits -= 8
            out[position++] = ((buffer shr bits) and 0xFF).toByte()
        }
    }
    return if (position == out.size) out else out.copyOf(position)
}

fun b64Decode(text: String): String? = b64Bytes(text)?.toString(Charsets.UTF_8)

/** Decoded text, or the original when it isn't base64 (a link that only looks like one). */
fun b64DecodeOrSelf(text: String): String = b64Decode(text) ?: text

fun b64Encode(data: ByteArray, urlSafe: Boolean = false, pad: Boolean = true): String {
    val alphabet = if (urlSafe) URL_SAFE else STANDARD
    val out = StringBuilder((data.size + 2) / 3 * 4)
    var index = 0
    while (index < data.size) {
        val b0 = data[index].toInt() and 0xFF
        val b1 = if (index + 1 < data.size) data[index + 1].toInt() and 0xFF else 0
        val b2 = if (index + 2 < data.size) data[index + 2].toInt() and 0xFF else 0
        out.append(alphabet[b0 shr 2])
        out.append(alphabet[((b0 and 0x03) shl 4) or (b1 shr 4)])
        if (index + 1 < data.size) out.append(alphabet[((b1 and 0x0F) shl 2) or (b2 shr 6)]) else if (pad) out.append('=')
        if (index + 2 < data.size) out.append(alphabet[b2 and 0x3F]) else if (pad) out.append('=')
        index += 3
    }
    return out.toString()
}

fun b64Encode(text: String, urlSafe: Boolean = false, pad: Boolean = true): String =
    b64Encode(text.toByteArray(Charsets.UTF_8), urlSafe, pad)

/** Lower-case hex, as Python's bytes.hex(). */
fun ByteArray.toHexString(): String = joinToString("") { "%02x".format(it) }

fun String.hexToBytes(): ByteArray {
    val clean = trim().lowercase()
    require(clean.length % 2 == 0) { "odd hex length" }
    return ByteArray(clean.length / 2) { i ->
        clean.substring(i * 2, i * 2 + 2).toInt(16).toByte()
    }
}
