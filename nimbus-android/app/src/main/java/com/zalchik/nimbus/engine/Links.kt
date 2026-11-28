package com.zalchik.nimbus.engine

import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.util.UrlParts
import com.zalchik.nimbus.util.b64Decode
import com.zalchik.nimbus.util.csvList
import com.zalchik.nimbus.util.hostOf
import com.zalchik.nimbus.util.hostPort
import com.zalchik.nimbus.util.int
import com.zalchik.nimbus.util.obj
import com.zalchik.nimbus.util.objNonEmpty
import com.zalchik.nimbus.util.parseObject
import com.zalchik.nimbus.util.parseQuery
import com.zalchik.nimbus.util.percentDecode
import com.zalchik.nimbus.util.splitUrl
import com.zalchik.nimbus.util.str
import org.json.JSONArray
import org.json.JSONObject

class LinkError(message: String) : Exception(message)

/**
 * What one share link turned into: a sing-box outbound (tag "proxy" unless the caller renames
 * it) and, for WireGuard, the endpoint that outbound dials through.
 *
 * [unsupported] says why the mobile core can't run the link at all (an XHTTP or a KCP transport):
 * the server is still listed and shared, and connecting reports the reason instead of failing
 * with a core error nobody can read.
 */
data class ParsedLink(
    val name: String,
    val outbound: JSONObject,
    val endpoint: JSONObject? = null,
    val protocol: String,
    val unsupported: String? = null,
    /** The remote address the link dials, for a direct latency probe. */
    val probe: String = "",
)

/**
 * Share links (vless://, vmess://, trojan://, ss://, hy2://, socks://, wireguard://) parsed into
 * sing-box outbounds — the phone's counterpart of nimbus/links.py, which builds Xray outbounds.
 *
 * The parameters are the same ones every client emits; what differs is the shape they land in:
 * sing-box names transports and TLS its own way, has neither XHTTP nor KCP, and WireGuard is an
 * endpoint rather than an outbound. allowInsecure is dropped here as it is on desktop.
 */
object Links {
    val SCHEMES = setOf(
        "vless", "vmess", "trojan", "ss", "hysteria2", "hy2",
        "socks", "socks5", "wireguard", "wg",
    )

    fun isLink(link: String): Boolean {
        val trimmed = link.trim()
        val index = trimmed.indexOf("://")
        if (index < 0) return false
        return trimmed.substring(0, index).lowercase() in SCHEMES
    }

    fun schemeOf(link: String): String = link.trim().substringBefore("://").lowercase()

    /** Display name and outbound(s) of a link. [tag] names the outbound ("proxy" by default). */
    fun parseLink(link: String, tag: String = "proxy"): ParsedLink {
        val text = link.trim()
        val scheme = schemeOf(text)
        val parsed = try {
            when (scheme) {
                "vless" -> vless(text, tag)
                "vmess" -> vmess(text, tag)
                "trojan" -> trojan(text, tag)
                "ss" -> shadowsocks(text, tag)
                "hysteria2", "hy2" -> hysteria2(text, tag)
                "socks", "socks5" -> socks(text, tag)
                "wireguard", "wg" -> wireguard(text, tag)
                else -> throw LinkError("unsupported scheme: $scheme")
            }
        } catch (e: LinkError) {
            throw e
        } catch (e: Exception) {
            throw LinkError("bad $scheme link: ${e.message ?: e.javaClass.simpleName}")
        }
        parsed.outbound.put("tag", tag)
        parsed.endpoint?.put("tag", "$tag-endpoint")
        return if (parsed.name.isEmpty()) parsed.copy(name = "${parsed.protocol} server") else parsed
    }

    // -- one parser per protocol ----------------------------------------------------------------

    private data class Split(
        val parts: UrlParts,
        val query: Map<String, String>,
        val host: String,
        val port: Int,
        val name: String,
    )

    private fun split(link: String): Split {
        val parts = splitUrl(link)
        val query = parseQuery(parts.query)
        if (parts.host.isEmpty() || parts.port <= 0) throw LinkError("missing host or port")
        return Split(parts, query, parts.host, parts.port, percentDecode(parts.fragment))
    }

    /** Transport and TLS from the common share-link query parameters. */
    private data class Stream(val transport: JSONObject?, val tls: JSONObject?, val unsupported: String?)

    private fun stream(query: Map<String, String>, defaultNetwork: String = "tcp"): Stream {
        var network = (query["type"] ?: query["net"] ?: defaultNetwork).lowercase()
        if (network == "raw") network = "tcp"
        if (network == "splithttp") network = "xhttp"
        val security = (query["security"] ?: "none").lowercase()
        val host = query["host"] ?: ""
        val path = percentDecode(query["path"] ?: "")

        var transport: JSONObject? = null
        var unsupported: String? = null
        when (network) {
            "tcp" -> {
                if (query["headerType"] == "http") {
                    transport = objNonEmpty(
                        "type" to "http",
                        "host" to if (host.isEmpty()) null else JSONArray(csvList(host)),
                        "path" to path.ifEmpty { "/" },
                        "method" to "GET",
                    )
                }
            }

            "ws" -> transport = objNonEmpty(
                "type" to "ws",
                "path" to path.ifEmpty { "/" },
                "headers" to if (host.isEmpty()) null else obj("Host" to JSONArray(csvList(host))),
                "max_early_data" to query["ed"]?.toIntOrNull()?.takeIf { it > 0 },
                "early_data_header_name" to query["eh"]?.takeIf { it.isNotEmpty() },
            )

            "httpupgrade" -> transport = objNonEmpty(
                "type" to "httpupgrade",
                "host" to host.takeIf { it.isNotEmpty() },
                "path" to path.ifEmpty { "/" },
            )

            "http", "h2" -> transport = objNonEmpty(
                "type" to "http",
                "host" to if (host.isEmpty()) null else JSONArray(csvList(host)),
                "path" to path.ifEmpty { "/" },
                "method" to "GET",
            )

            "grpc" -> transport = objNonEmpty(
                "type" to "grpc",
                "service_name" to percentDecode(query["serviceName"] ?: ""),
                "permit_without_stream" to true.takeIf { query["permitWithoutStream"] == "true" },
            )

            "quic" -> transport = obj("type" to "quic")

            "xhttp" -> unsupported =
                com.zalchik.nimbus.i18n.I18n.tr("XHTTP-серверы не поддерживаются мобильным ядром sing-box")

            "kcp", "mkcp" -> unsupported =
                com.zalchik.nimbus.i18n.I18n.tr("KCP не поддерживается мобильным ядром sing-box")
        }

        val sni = query["sni"] ?: query["peer"] ?: ""
        val fp = query["fp"] ?: ""
        var tls: JSONObject? = null
        if (security == "tls") {
            tls = JSONObject()
            tls.put("enabled", true)
            if (sni.isNotEmpty()) tls.put("server_name", sni)
            val alpn = csvList(percentDecode(query["alpn"] ?: ""))
            if (alpn.isNotEmpty()) tls.put("alpn", JSONArray(alpn))
            if (fp.isNotEmpty()) tls.put("utls", obj("enabled" to true, "fingerprint" to fp))
            val ech = query["ech"] ?: ""
            if (ech.isNotEmpty()) tls.put("ech", obj("enabled" to true, "config" to ech))
            // insecure=1 / allowInsecure=1 is deliberately dropped, as on desktop: Xray 26.2.6+
            // refuses a config that still carries it, and sing-box would silently trust any
            // certificate, which is worse. The link's pcs / vcn pins have no sing-box counterpart
            // (it pins the public key's hash), so they are dropped rather than half-honoured.
        } else if (security == "reality") {
            tls = obj(
                "enabled" to true,
                "server_name" to sni,
                "utls" to obj("enabled" to true, "fingerprint" to fp.ifEmpty { "chrome" }),
                "reality" to objNonEmpty(
                    "enabled" to true,
                    "public_key" to (query["pbk"] ?: ""),
                    "short_id" to (query["sid"] ?: ""),
                ),
            )
            // spx (spiderX) and pqv (mldsa65Verify) are Xray's; sing-box has neither.
        }
        return Stream(transport, tls, unsupported)
    }

    private fun withStream(outbound: JSONObject, stream: Stream): JSONObject {
        if (stream.transport != null) outbound.put("transport", stream.transport)
        if (stream.tls != null) outbound.put("tls", stream.tls)
        return outbound
    }

    private fun vless(link: String, tag: String): ParsedLink {
        val (parts, query, host, port, name) = split(link)
        val uuid = percentDecode(parts.username)
        if (uuid.isEmpty()) throw LinkError("missing uuid")
        val stream = stream(query)
        val outbound = withStream(
            objNonEmpty(
                "type" to "vless",
                "tag" to tag,
                "server" to host,
                "server_port" to port,
                "uuid" to uuid,
                "flow" to query["flow"]?.takeIf { it.isNotEmpty() },
                "packet_encoding" to query["packetEncoding"]?.takeIf { it.isNotEmpty() },
            ),
            stream,
        )
        return ParsedLink(name, outbound, protocol = "vless", unsupported = stream.unsupported, probe = hostPort(host, port))
    }

    private fun vmess(link: String, tag: String): ParsedLink {
        val body = link.substringAfter("://").substringBefore('#')
        val json = b64Decode(body)?.let { parseObject(it) }
        if (json == null) {
            // Some panels emit vmess in vless-style URL form.
            val (parts, query, host, port, name) = split(link)
            val uuid = percentDecode(parts.username)
            if (uuid.isEmpty()) throw LinkError("missing uuid")
            val stream = stream(query)
            val outbound = withStream(
                objNonEmpty(
                    "type" to "vmess",
                    "tag" to tag,
                    "server" to host,
                    "server_port" to port,
                    "uuid" to uuid,
                    "security" to (query["encryption"]?.takeIf { it.isNotEmpty() } ?: "auto"),
                ),
                stream,
            )
            return ParsedLink(name, outbound, protocol = "vmess", unsupported = stream.unsupported, probe = hostPort(host, port))
        }
        val query = linkedMapOf(
            "type" to json.str("net", "tcp"),
            "headerType" to json.str("type"),
            "host" to json.str("host"),
            "path" to json.str("path"),
            "security" to json.str("tls").ifEmpty { "none" },
            "sni" to json.str("sni"),
            "alpn" to json.str("alpn"),
            "fp" to json.str("fp"),
            "serviceName" to json.str("path"),
            "mode" to json.str("mode"),
            "ed" to json.str("ed"),
            "eh" to json.str("eh"),
            "permitWithoutStream" to json.str("permitWithoutStream"),
        )
        val stream = stream(query)
        val uuid = json.str("id")
        if (uuid.isEmpty()) throw LinkError("missing uuid")
        val alterId = json.int("aid")
        val server = json.str("add")
        val port = json.str("port").toIntOrNull() ?: json.int("port")
        val outbound = withStream(
            objNonEmpty(
                "type" to "vmess",
                "tag" to tag,
                "server" to server,
                "server_port" to port,
                "uuid" to uuid,
                "security" to json.str("scy").ifEmpty { "auto" },
                // aid is Xray's alterId; sing-box wants it only when a server still uses one.
                "alter_id" to if (alterId > 0) alterId else null,
                "packet_encoding" to json.str("packetEncoding").takeIf { it.isNotEmpty() },
            ),
            stream,
        )
        return ParsedLink(json.str("ps"), outbound, protocol = "vmess", unsupported = stream.unsupported, probe = hostPort(server, port))
    }

    private fun trojan(link: String, tag: String): ParsedLink {
        val (parts, query, host, port, name) = split(link)
        val password = percentDecode(parts.username)
        if (password.isEmpty()) throw LinkError("missing password")
        val withSecurity = query.toMutableMap()
        withSecurity.putIfAbsent("security", "tls")
        val stream = stream(withSecurity)
        val outbound = withStream(
            objNonEmpty(
                "type" to "trojan",
                "tag" to tag,
                "server" to host,
                "server_port" to port,
                "password" to password,
            ),
            stream,
        )
        // Xray's flow on trojan has no sing-box counterpart; the server works without it.
        return ParsedLink(name, outbound, protocol = "trojan", unsupported = stream.unsupported, probe = hostPort(host, port))
    }

    private fun shadowsocks(link: String, tag: String): ParsedLink {
        var rest = link.substringAfter("://")
        val hash = rest.indexOf('#')
        val name = if (hash >= 0) percentDecode(rest.substring(hash + 1)) else ""
        if (hash >= 0) rest = rest.substring(0, hash)
        val query = parseQuery(rest.substringAfter('?', ""))
        rest = rest.substringBefore('?').trimEnd('/')
        val userinfo: String
        val hostport: String
        if ('@' in rest) {
            val at = rest.lastIndexOf('@')
            var info = percentDecode(rest.substring(0, at))
            hostport = rest.substring(at + 1)
            if (':' !in info) info = b64Decode(info) ?: info
            userinfo = info
        } else {
            // legacy: base64(method:password@host:port)
            val decoded = b64Decode(rest) ?: throw LinkError("bad ss link")
            val at = decoded.lastIndexOf('@')
            if (at < 0) throw LinkError("bad ss link")
            userinfo = decoded.substring(0, at)
            hostport = decoded.substring(at + 1)
        }
        val method = userinfo.substringBefore(':')
        val password = userinfo.substringAfter(':', "")
        val host = hostport.substringBeforeLast(':').trim('[', ']')
        val port = hostport.substringAfterLast(':').toIntOrNull() ?: 0
        if (method.isEmpty() || host.isEmpty() || port <= 0) throw LinkError("missing method, host or port")
        val outbound = objNonEmpty(
            "type" to "shadowsocks",
            "tag" to tag,
            "server" to host,
            "server_port" to port,
            "method" to method,
            "password" to password,
            "plugin" to query["plugin"]?.takeIf { it.isNotEmpty() },
            "plugin_opts" to query["plugin-opts"]?.takeIf { it.isNotEmpty() },
        )
        if (query["type"] == "udp-over-tcp" || query["obfs"] == "udp-over-tcp") {
            outbound.put("udp_over_tcp", obj("enabled" to true))
        }
        return ParsedLink(name, outbound, protocol = "shadowsocks", probe = hostPort(host, port))
    }

    private fun hysteria2(link: String, tag: String): ParsedLink {
        val (parts, query, host, port, name) = split(link)
        var auth = percentDecode(parts.username)
        if (parts.password.isNotEmpty()) auth += ":" + percentDecode(parts.password)
        val outbound = JSONObject()
        outbound.put("type", "hysteria2")
        outbound.put("tag", tag)
        outbound.put("server", host)
        val hops = csvList(query["mport"] ?: "")
        val probe: String
        if (hops.isNotEmpty()) {
            // Port hopping: Xray's udphop finalmask is sing-box's server_ports + hop_interval.
            outbound.put("server_ports", JSONArray(hops))
            outbound.put("hop_interval", "30s")
            probe = hostPort(host, hops.first().substringBefore('-').toIntOrNull() ?: port)
        } else {
            outbound.put("server_port", port)
            probe = hostPort(host, port)
        }
        outbound.put("password", auth)
        if (query["obfs"] == "salamander" && !query["obfs-password"].isNullOrEmpty()) {
            outbound.put("obfs", obj("type" to "salamander", "password" to query["obfs-password"]))
        }
        query["upmbps"]?.toIntOrNull()?.let { if (it > 0) outbound.put("up_mbps", it) }
        query["downmbps"]?.toIntOrNull()?.let { if (it > 0) outbound.put("down_mbps", it) }
        outbound.put(
            "tls",
            obj(
                "enabled" to true,
                "server_name" to (query["sni"]?.takeIf { it.isNotEmpty() } ?: host),
                "alpn" to JSONArray(listOf("h3")),
            ),
        )
        return ParsedLink(name, outbound, protocol = "hysteria2", probe = probe)
    }

    private fun socks(link: String, tag: String): ParsedLink {
        val (parts, _, host, port, name) = split(link)
        var user = percentDecode(parts.username)
        var password = percentDecode(parts.password)
        if (user.isNotEmpty() && password.isEmpty()) {
            b64Decode(user)?.let { decoded ->
                if (':' in decoded) {
                    user = decoded.substringBefore(':')
                    password = decoded.substringAfter(':')
                }
            }
        }
        val outbound = objNonEmpty(
            "type" to "socks",
            "tag" to tag,
            "server" to host,
            "server_port" to port,
            "version" to "5",
            "username" to user.takeIf { it.isNotEmpty() },
            "password" to password.takeIf { it.isNotEmpty() },
        )
        return ParsedLink(name, outbound, protocol = "socks", probe = hostPort(host, port))
    }

    private fun wireguard(link: String, tag: String): ParsedLink {
        val (parts, query, host, port, name) = split(link)
        val peer = objNonEmpty(
            "address" to host,
            "port" to port,
            "public_key" to (query["publickey"] ?: query["publicKey"] ?: ""),
            "pre_shared_key" to query["presharedkey"]?.takeIf { it.isNotEmpty() },
            "allowed_ips" to JSONArray(
                csvList(percentDecode(query["allowedips"] ?: "")).ifEmpty { listOf("0.0.0.0/0", "::/0") },
            ),
            "persistent_keepalive_interval" to query["keepalive"]?.toIntOrNull(),
        )
        val reserved = csvList(query["reserved"] ?: "").mapNotNull { it.trim().toIntOrNull() }
        if (reserved.isNotEmpty()) peer.put("reserved", JSONArray(reserved))
        val endpoint = objNonEmpty(
            "type" to "wireguard",
            "tag" to "$tag-endpoint",
            "system" to false,
            "address" to JSONArray(
                csvList(percentDecode(query["address"] ?: "")).ifEmpty { listOf("172.16.0.2/32") },
            ),
            "private_key" to percentDecode(parts.username).ifEmpty { query["privatekey"] ?: "" },
            "mtu" to (query["mtu"]?.toIntOrNull() ?: 1420),
            "peers" to JSONArray(listOf(peer)),
            "workers" to query["workers"]?.toIntOrNull(),
        )
        // sing-box's WireGuard is an endpoint: the outbound dials through it.
        val outbound = obj("type" to "direct", "tag" to tag, "detour" to "$tag-endpoint")
        return ParsedLink(name, outbound, endpoint, protocol = "wireguard", probe = hostPort(host, port))
    }

    // -- helpers the rest of the app uses -------------------------------------------------------

    /**
     * Multiplexing, as desktop's _apply_mux: on for the protocols that have it, and never with a
     * Vision flow (xtls-rprx-vision multiplexes by itself and the two don't mix).
     */
    fun applyMultiplex(outbound: JSONObject, enabled: Boolean, concurrency: Int) {
        if (!enabled) return
        val type = outbound.str("type")
        if (type !in setOf("vless", "vmess", "trojan", "shadowsocks", "http", "socks")) return
        if (outbound.str("flow").isNotEmpty()) return
        outbound.put(
            "multiplex",
            objNonEmpty(
                "enabled" to true,
                "protocol" to "h2mux",
                "max_streams" to if (concurrency > 0) concurrency else null,
                "padding" to true,
            ),
        )
    }

    /** TLS fragmentation: the sing-box counterpart of Xray's sockopt fragment. */
    fun applyFragment(outbound: JSONObject, enabled: Boolean) {
        if (!enabled) return
        val tls = outbound.optJSONObject("tls") ?: return
        tls.put("fragment", true)
        tls.put("record_fragment", true)
    }

    private val DOMAIN = Regex(
        "(?=.{1,253}$)([a-z0-9_]([a-z0-9_-]*[a-z0-9])?\\.)+[a-z][a-z0-9-]*[a-z0-9]",
        RegexOption.IGNORE_CASE,
    )

    /**
     * Domain names of the proxy servers, as desktop's server_domains(): sing-box resolves them
     * with the direct DNS, because through the tunnel they would wait on a connection that needs
     * them first.
     */
    fun serverDomains(outbounds: List<JSONObject>, endpoints: List<JSONObject> = emptyList()): List<String> {
        val found = mutableListOf<String>()
        fun walk(node: Any?, key: String = "") {
            when (node) {
                is JSONObject -> for (name in node.keys()) walk(node.opt(name), name)
                is JSONArray -> for (i in 0 until node.length()) walk(node.opt(i), key)
                is String -> if (key in setOf("server", "address", "endpoint", "host")) {
                    val host = hostOf(node).lowercase()
                    if (DOMAIN.matches(host) && host !in found) found.add(host)
                }
            }
        }
        for (outbound in outbounds) walk(outbound)
        for (endpoint in endpoints) walk(endpoint)
        return found
    }

    /** The outbounds of a link-based server, for latency tests and group membership. */
    fun outboundsOf(server: Server): List<JSONObject> = try {
        listOf(parseLink(server.link).outbound)
    } catch (_: LinkError) {
        emptyList()
    }

    /** Why a server can't run on the mobile core, or null when it can. */
    fun unsupportedReason(server: Server): String? = try {
        if (server.isFullConfig) null else parseLink(server.link).unsupported
    } catch (e: LinkError) {
        e.message
    }

    /** The address a direct latency probe dials, or null when the server has none. */
    fun probeAddress(server: Server): String? = try {
        if (server.isFullConfig) null else parseLink(server.link).probe.takeIf { it.isNotEmpty() }
    } catch (_: LinkError) {
        null
    }
}
