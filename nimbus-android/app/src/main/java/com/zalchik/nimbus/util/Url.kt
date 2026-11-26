package com.zalchik.nimbus.util

/**
 * The tolerant URL handling share links need — Python's urlsplit/parse_qs/quote/unquote, which
 * java.net.URI can't stand in for: real-world links carry unencoded spaces, pipes and brackets
 * in passwords and names, and URI throws on those.
 */

data class UrlParts(
    val scheme: String,
    val username: String,
    val password: String,
    val host: String,
    val port: Int,
    val path: String,
    val query: String,
    val fragment: String,
) {
    val hasPort: Boolean get() = port > 0
    val netloc: String get() = if (hasPort) "$host:$port" else host
}

/** scheme://user:pass@host:port/path?query#fragment, with any part allowed to be missing. */
fun splitUrl(link: String): UrlParts {
    var rest = link.trim()
    var scheme = ""
    val schemeEnd = rest.indexOf("://")
    if (schemeEnd >= 0) {
        scheme = rest.substring(0, schemeEnd).lowercase()
        rest = rest.substring(schemeEnd + 3)
    }
    var fragment = ""
    val hash = rest.indexOf('#')
    if (hash >= 0) {
        fragment = rest.substring(hash + 1)
        rest = rest.substring(0, hash)
    }
    var query = ""
    val question = rest.indexOf('?')
    if (question >= 0) {
        query = rest.substring(question + 1)
        rest = rest.substring(0, question)
    }
    var path = ""
    val slash = rest.indexOf('/')
    if (slash >= 0) {
        path = rest.substring(slash)
        rest = rest.substring(0, slash)
    }
    // An IPv6 host arrives bracketed ([2001:db8::1]:443); user info, if any, precedes the last @.
    var authority = rest
    var userinfo = ""
    val at = authority.lastIndexOf('@')
    if (at >= 0) {
        userinfo = authority.substring(0, at)
        authority = authority.substring(at + 1)
    }
    var host = authority
    var port = 0
    if (host.startsWith("[")) {
        val close = host.indexOf(']')
        if (close >= 0) {
            val tail = host.substring(close + 1)
            host = host.substring(1, close).lowercase()
            if (tail.startsWith(":")) port = tail.substring(1).toIntOrNull() ?: 0
        }
    } else {
        val colon = host.lastIndexOf(':')
        if (colon >= 0) {
            val tail = host.substring(colon + 1)
            val parsed = tail.toIntOrNull()
            if (parsed != null && tail.isNotEmpty()) {
                port = parsed
                host = host.substring(0, colon)
            }
        }
        host = host.lowercase()
    }
    var username = ""
    var password = ""
    if (userinfo.isNotEmpty()) {
        val colon = userinfo.indexOf(':')
        if (colon >= 0) {
            username = userinfo.substring(0, colon)
            password = userinfo.substring(colon + 1)
        } else {
            username = userinfo
        }
    }
    return UrlParts(scheme, username, password, host, port, path, query, fragment)
}

/** Query parameters, percent-decoded, first value per key (Python's parse_qs with [0]). */
fun parseQuery(query: String): Map<String, String> {
    val out = LinkedHashMap<String, String>()
    if (query.isEmpty()) return out
    for (part in query.split('&')) {
        if (part.isEmpty()) continue
        val equals = part.indexOf('=')
        val key: String
        val value: String
        if (equals >= 0) {
            key = part.substring(0, equals)
            value = part.substring(equals + 1)
        } else {
            key = part
            value = ""
        }
        val name = percentDecode(key.replace('+', ' '))
        if (!out.containsKey(name)) out[name] = percentDecode(value.replace('+', ' '))
    }
    return out
}

private val HEX = "0123456789ABCDEF"

/** Python's unquote(): %XX becomes the byte it names; the bytes are then read as UTF-8. */
fun percentDecode(text: String): String {
    if ('%' !in text) return text
    val out = java.io.ByteArrayOutputStream(text.length)
    var i = 0
    while (i < text.length) {
        val ch = text[i]
        if (ch == '%' && i + 2 < text.length) {
            val value = text.substring(i + 1, i + 3).toIntOrNull(16)
            if (value != null) {
                out.write(value)
                i += 3
                continue
            }
        }
        out.write(ch.toString().toByteArray(Charsets.UTF_8))
        i++
    }
    return out.toString("UTF-8")
}

/** Python's quote(value, safe=""): everything but the unreserved characters is percent-encoded. */
fun percentEncode(text: String, safe: String = ""): String {
    val out = StringBuilder()
    for (byte in text.toByteArray(Charsets.UTF_8)) {
        val value = byte.toInt() and 0xFF
        val ch = value.toChar()
        val unreserved = value in 0x30..0x39 || value in 0x41..0x5A || value in 0x61..0x7A ||
            ch == '_' || ch == '.' || ch == '-' || ch == '~'
        if (unreserved || safe.indexOf(ch) >= 0) {
            out.append(ch)
        } else {
            out.append('%').append(HEX[value shr 4]).append(HEX[value and 0x0F])
        }
    }
    return out.toString()
}

/** "a,b, c" -> ["a", "b", "c"], as desktop's _csv(). */
fun csvList(value: String?): List<String> =
    (value ?: "").split(',').map { it.trim() }.filter { it.isNotEmpty() }

/** A host:port pair for a config, bracketing an IPv6 address. */
fun hostPort(host: String, port: Int): String =
    if (':' in host && !host.startsWith("[")) "[$host]:$port" else "$host:$port"

/** The host of a value that may be a URL, a host:port pair or a bare name. */
fun hostOf(value: String): String {
    var text = value.trim()
    if ("://" in text) return splitUrl(text).host
    // A bracketed IPv6 host:port: the brackets say where the address ends, colons can't.
    if (text.startsWith("[")) {
        val close = text.indexOf(']')
        if (close > 0) return text.substring(1, close)
    }
    if (text.count { it == ':' } == 1) text = text.substringBefore(':')
    return text.trim('[', ']').trimEnd('.')
}
