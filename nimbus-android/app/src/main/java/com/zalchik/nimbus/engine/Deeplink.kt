package com.zalchik.nimbus.engine

import com.zalchik.nimbus.util.percentDecode
import com.zalchik.nimbus.util.parseObject

/**
 * Deep links that add a subscription or configs: `nimbus://import/<url>`, and the formats other
 * clients' "add to app" buttons use (`happ://add/<url>`, `v2rayn://install-sub?url=…`,
 * `clash://install-config?url=…`, `sing-box://import-remote-profile?url=…`).
 *
 * Happ's encrypted links (`happ://crypt…/…`, and the `crypt://crypt4/…` form some providers
 * shorten them to) carry the subscription URL or the share links inside, encrypted to a key the
 * Happ app ships; they're decrypted here, so such a link adds what it holds like any other.
 */
object Deeplink {
    const val SCHEME = "nimbus"

    /** scheme -> path prefixes with the URL after them. */
    private val PATH_FORMS = mapOf(
        "nimbus" to listOf("import/", "add/"),
        "happ" to listOf("add/"),
    )

    /** scheme -> actions that carry the URL as `?url=…`. */
    private val QUERY_FORMS = mapOf(
        "nimbus" to listOf("import"),
        "v2rayn" to listOf("install-sub", "install-config"),
        "v2rayng" to listOf("install-sub", "install-config"),
        "clash" to listOf("install-config"),
        "clashmeta" to listOf("install-config"),
        "sing-box" to listOf("import-remote-profile"),
    )

    val SCHEMES: Set<String> = (PATH_FORMS.keys + QUERY_FORMS.keys + "crypt").toSortedSet()

    /** "subscription" (payload: its URL) | "configs" (share links) | "crypt" (payload: why it
     * won't open) | "invalid". */
    data class DeepLink(val kind: String, val payload: String = "", val name: String = "")

    fun isDeepLink(text: String?): Boolean {
        val parts = (text ?: "").trim().split("://", limit = 2)
        return parts.size == 2 && parts[0].lowercase() in SCHEMES
    }

    /** What the link carries, percent-decoded once if it came encoded. */
    private fun payload(value: String): String {
        val trimmed = value.trim()
        if ("://" in trimmed) return trimmed
        val decoded = percentDecode(trimmed)
        return if ("://" in decoded) decoded else trimmed
    }

    private fun isJson(value: String): Boolean {
        val head = value.trimStart().take(1)
        if (head != "[" && head != "{") return false
        return try {
            parseObject(value) != null || com.zalchik.nimbus.util.parseJson(value) != null
        } catch (e: Exception) {
            false
        }
    }

    private fun classify(value: String, name: String = "", depth: Int = 0): DeepLink {
        val payload = payload(value)
        if (depth < 4 && isDeepLink(payload)) {
            parse(payload, depth + 1)?.let { inner ->
                return DeepLink(inner.kind, inner.payload, name.ifEmpty { inner.name })
            }
        }
        if (payload.startsWith("http://", true) || payload.startsWith("https://", true)) {
            val parts = com.zalchik.nimbus.util.splitUrl(payload)
            if (parts.host.isNotEmpty() && payload.none { it.isWhitespace() }) {
                return DeepLink("subscription", payload, name)
            }
        }
        if (isJson(payload) || payload.lines().any { Links.isLink(it.trim()) }) {
            return DeepLink("configs", payload, name)
        }
        return DeepLink("invalid", payload, name)
    }

    /** A Happ encrypted link: whatever it carries is what gets added. */
    private fun encrypted(text: String): DeepLink = try {
        val plain = Happ.unwrap(text).trim()
        // Nothing inside is still a crypt link's failure, not an invalid one: the message is what
        // the import sheet shows, and "invalid" would say the link itself makes no sense.
        if (plain.isEmpty()) DeepLink("crypt", "the link is empty") else classify(plain)
    } catch (e: HappError) {
        DeepLink("crypt", e.message ?: "")
    }

    /** The deep link in `text`, or null if it isn't one. */
    fun parse(text: String?, depth: Int = 0): DeepLink? {
        val trimmed = (text ?: "").trim()
        if (!isDeepLink(trimmed)) return null
        val scheme = trimmed.substringBefore("://").lowercase()
        val rest = trimmed.substringAfter("://")
        if ((scheme == "happ" || scheme == "crypt") && Happ.isCryptLink(trimmed)) {
            return encrypted(trimmed)
        }
        for (prefix in PATH_FORMS[scheme].orEmpty()) {
            if (rest.startsWith(prefix, ignoreCase = true)) {
                return classify(rest.substring(prefix.length), depth = depth + 1)
            }
        }
        val action = rest.substringBefore('?').trim('/').lowercase()
        if (action in QUERY_FORMS[scheme].orEmpty()) {
            val queryAndFragment = rest.substringAfter('?', "")
            val query = queryAndFragment.substringBefore('#')
            val fragment = queryAndFragment.substringAfter('#', "")
            val params = parseQueryKeepBlanks(query)
            val url = params["url"] ?: ""
            val name = params["name"]?.takeIf { it.isNotEmpty() } ?: percentDecode(fragment)
            return if (url.isNotEmpty()) classify(url, name, depth + 1) else DeepLink("invalid")
        }
        return DeepLink("invalid")
    }

    /** `a=1&b=&a=2` -> first value of each key, blank values kept (Python's parse_qs with
     * keep_blank_values, which the desktop uses). */
    private fun parseQueryKeepBlanks(query: String): Map<String, String> {
        val out = LinkedHashMap<String, String>()
        if (query.isEmpty()) return out
        for (pair in query.split('&')) {
            if (pair.isEmpty()) continue
            val key = percentDecode(pair.substringBefore('='))
            val value = percentDecode(pair.substringAfter('=', ""))
            if (!out.containsKey(key)) out[key] = value
        }
        return out
    }
}
