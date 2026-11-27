package com.zalchik.nimbus.engine

import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.b64Decode
import com.zalchik.nimbus.util.b64Encode
import com.zalchik.nimbus.util.bool
import com.zalchik.nimbus.util.copy
import com.zalchik.nimbus.util.int
import com.zalchik.nimbus.util.listOfStrings
import com.zalchik.nimbus.util.obj
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.parseObject
import com.zalchik.nimbus.util.percentEncode
import com.zalchik.nimbus.util.str
import com.zalchik.nimbus.util.strOrNull
import com.zalchik.nimbus.util.stringify
import org.json.JSONArray
import org.json.JSONObject

/**
 * Sharing: outbounds back to share links (`vless://` …), and configs as standalone JSON — the
 * port of nimbus/share.py.
 *
 * The desktop's outbound -> link half speaks Xray, because that's what a panel's config holds and
 * what the desktop runs; it stays as it is. The phone also builds sing-box outbounds of its own,
 * so the same half reads those too. The JSON half differs from the desktop's outright: the phone
 * has no Xray core, so what it exports is the sing-box config it actually runs, which SFA, Happ
 * and any other sing-box client imports as is.
 */
object Share {
    private val NON_PROXY = setOf("freedom", "blackhole", "dns", "loopback")

    /** The same, as sing-box types: a built config's helpers and its groups are not nodes. */
    private val NON_PROXY_TYPES = setOf("direct", "block", "dns", "selector", "urltest")

    private fun q(params: Map<String, Any?>): String = params.entries
        .filter { (_, value) -> value != null && value != "" && value != false }
        .joinToString("&") { (key, value) -> "$key=${percentEncode(value.toString())}" }

    /** An IPv6 address goes in brackets; anything else stays as it is. */
    private fun host(address: String): String =
        if (":" in address && !address.startsWith("[")) "[$address]" else address

    /** (server, user) from either the flat 26.x form or vnext/servers lists. */
    private fun first(settings: JSONObject, key: String): Pair<JSONObject, JSONObject> {
        val list = settings.arrOrEmpty(key)
        if (list.length() > 0) {
            val server = list.optJSONObject(0) ?: JSONObject()
            val users = server.arrOrEmpty("users")
            val user = (users.optJSONObject(0) ?: JSONObject()).copy()
            for (name in server.keys()) if (!user.has(name)) user.put(name, server.opt(name))
            return server to user
        }
        return settings to settings
    }

    /** The transport and TLS parameters of a streamSettings block, as a share link spells them. */
    private fun streamParams(stream: JSONObject): LinkedHashMap<String, Any?> {
        val rawNetwork = stream.str("network", "tcp").lowercase()
        val network = when (rawNetwork) {
            "raw" -> "tcp"
            "splithttp" -> "xhttp"
            "h2" -> "http"
            else -> rawNetwork
        }
        val security = stream.str("security", "none").lowercase()
        val params = linkedMapOf<String, Any?>("type" to network, "security" to security)
        when (network) {
            "tcp" -> {
                val header = (stream.objOrNull("tcpSettings") ?: stream.objOrNull("rawSettings"))
                    ?.optJSONObject("header") ?: JSONObject()
                if (header.str("type") == "http") {
                    val request = header.optJSONObject("request") ?: JSONObject()
                    params["headerType"] = "http"
                    params["path"] = request.listOfStrings("path").joinToString(",")
                    params["host"] = (request.optJSONObject("headers") ?: JSONObject())
                        .listOfStrings("Host").joinToString(",")
                }
            }

            "ws", "httpupgrade" -> {
                val settings = stream.objOrNull("${network}Settings") ?: JSONObject()
                params["path"] = settings.str("path")
                params["host"] = settings.str("host")
                    .ifEmpty { (settings.optJSONObject("headers") ?: JSONObject()).str("Host") }
            }

            "grpc" -> {
                val settings = stream.objOrNull("grpcSettings") ?: JSONObject()
                params["serviceName"] = settings.str("serviceName")
                params["authority"] = settings.str("authority")
                if (settings.bool("multiMode") || settings.str("mode") == "multi") params["mode"] = "multi"
            }

            "xhttp" -> {
                val settings = stream.objOrNull("xhttpSettings") ?: stream.objOrNull("splithttpSettings")
                    ?: JSONObject()
                params["path"] = settings.str("path")
                params["host"] = settings.str("host")
                params["mode"] = settings.str("mode")
                settings.optJSONObject("extra")?.let { params["extra"] = stringify(it) }
            }

            "kcp" -> {
                val settings = stream.objOrNull("kcpSettings") ?: JSONObject()
                params["headerType"] = (settings.optJSONObject("header") ?: JSONObject()).str("type")
                params["seed"] = settings.str("seed")
            }
        }
        if (security == "tls") {
            val tls = stream.objOrNull("tlsSettings") ?: JSONObject()
            params["sni"] = tls.str("serverName")
            params["fp"] = tls.str("fingerprint")
            params["alpn"] = tls.listOfStrings("alpn").joinToString(",")
            params["pcs"] = tls.str("pinnedPeerCertSha256")
            params["vcn"] = tls.str("verifyPeerCertByName")
            params["ech"] = tls.str("echConfigList")
        } else if (security == "reality") {
            val reality = stream.objOrNull("realitySettings") ?: JSONObject()
            params["sni"] = reality.str("serverName")
            params["fp"] = reality.str("fingerprint")
            params["pbk"] = reality.str("publicKey").ifEmpty { reality.str("password") }
            params["sid"] = reality.str("shortId")
            params["spx"] = reality.str("spiderX")
            params["pqv"] = reality.str("mldsa65Verify")
        }
        return params
    }

    private fun JSONObject.objOrNull(key: String): JSONObject? = optJSONObject(key)

    /**
     * A share link for one outbound, or null for what links can't express. Both shapes arrive
     * here: Xray's, which is what a panel's config holds, and sing-box's, which is what Nimbus
     * builds and what a config imported from another sing-box client holds.
     */
    fun outboundLink(outbound: JSONObject, name: String): String? =
        if (outbound.has("protocol")) xrayLink(outbound, name) else singBoxLink(outbound, name)

    /** Share link for one Xray outbound, or null for protocols links can't express. */
    private fun xrayLink(outbound: JSONObject, name: String): String? {
        val protocol = outbound.str("protocol")
        val settings = outbound.optJSONObject("settings") ?: JSONObject()
        val stream = outbound.optJSONObject("streamSettings") ?: JSONObject()
        val fragment = "#" + percentEncode(name)
        when (protocol) {
            "vless" -> {
                val (server, user) = first(settings, "vnext")
                val params = linkedMapOf<String, Any?>(
                    "encryption" to user.str("encryption").ifEmpty { "none" },
                    "flow" to user.str("flow"),
                ).apply { putAll(streamParams(stream)) }
                return "vless://${percentEncode(user.str("id"))}@${host(server.str("address"))}" +
                    ":${server.int("port")}?${q(params)}$fragment"
            }

            "trojan" -> {
                val (server, user) = first(settings, "servers")
                val params = linkedMapOf<String, Any?>("flow" to user.str("flow"))
                    .apply { putAll(streamParams(stream)) }
                return "trojan://${percentEncode(user.str("password"))}@${host(server.str("address"))}" +
                    ":${server.int("port")}?${q(params)}$fragment"
            }

            "vmess" -> {
                val (server, user) = first(settings, "vnext")
                val params = streamParams(stream)
                val json = obj(
                    "v" to "2",
                    "ps" to name,
                    "add" to server.str("address"),
                    "port" to server.int("port").toString(),
                    "id" to user.str("id"),
                    "aid" to "0",
                    "scy" to user.str("security").ifEmpty { "auto" },
                    "net" to params["type"].toString(),
                    "type" to (params["headerType"]?.toString() ?: "none"),
                    "host" to (params["host"]?.toString() ?: ""),
                    "path" to if (params["type"] == "grpc") params["serviceName"]?.toString() ?: ""
                    else params["path"]?.toString() ?: "",
                    "tls" to if (params["security"] == "none") "" else params["security"].toString(),
                    "sni" to (params["sni"]?.toString() ?: ""),
                    "alpn" to (params["alpn"]?.toString() ?: ""),
                    "fp" to (params["fp"]?.toString() ?: ""),
                )
                params["mode"]?.let { json.put("mode", it) }
                return "vmess://" + b64Encode(stringify(json))
            }

            "shadowsocks" -> {
                val (server, user) = first(settings, "servers")
                val method = user.str("method")
                val password = user.str("password")
                // SIP002: AEAD-2022 keys go percent-encoded, not base64.
                val userinfo = if (method.startsWith("2022-")) {
                    "${percentEncode(method)}:${percentEncode(password)}"
                } else {
                    b64Encode("$method:$password", urlSafe = true, pad = false)
                }
                return "ss://$userinfo@${host(server.str("address"))}:${server.int("port")}$fragment"
            }

            "hysteria", "hysteria2" -> {
                val server = if (settings.has("address")) {
                    settings
                } else {
                    settings.arrOrEmpty("servers").optJSONObject(0) ?: JSONObject()
                }
                val hysteria = stream.objOrNull("hysteriaSettings") ?: JSONObject()
                val tls = stream.objOrNull("tlsSettings") ?: JSONObject()
                val params = linkedMapOf<String, Any?>(
                    "sni" to tls.str("serverName"),
                    "vcn" to tls.str("verifyPeerCertByName"),
                    "pinSHA256" to tls.str("pinnedPeerCertSha256"),
                )
                for (mask in (stream.objOrNull("finalmask") ?: JSONObject()).arrOrEmpty("udp").objects()) {
                    val maskSettings = mask.optJSONObject("settings") ?: JSONObject()
                    when (mask.str("type")) {
                        "salamander" -> {
                            params["obfs"] = "salamander"
                            params["obfs-password"] = maskSettings.str("password")
                        }

                        "udphop" -> params["mport"] = maskSettings.str("remotePorts")
                    }
                }
                val auth = percentEncode(hysteria.str("auth").ifEmpty { server.str("password") })
                return "hy2://$auth@${host(server.str("address"))}:${server.int("port")}?${q(params)}$fragment"
            }

            "socks" -> {
                val (server, user) = first(settings, "servers")
                val login = user.str("user")
                val credentials = if (login.isEmpty()) {
                    ""
                } else {
                    b64Encode("$login:${user.str("pass")}", urlSafe = true, pad = false) + "@"
                }
                return "socks://$credentials${host(server.str("address"))}:${server.int("port")}$fragment"
            }

            "wireguard" -> {
                val peer = settings.arrOrEmpty("peers").optJSONObject(0) ?: JSONObject()
                val endpoint = peer.str("endpoint")
                val port = endpoint.substringAfterLast(':', "")
                val peerHost = endpoint.substringBeforeLast(':', endpoint)
                val params = linkedMapOf<String, Any?>(
                    "publickey" to peer.str("publicKey"),
                    "presharedkey" to peer.str("preSharedKey"),
                    "address" to settings.listOfStrings("address").joinToString(","),
                    "mtu" to (settings.strOrNull("mtu") ?: ""),
                    "reserved" to settings.arrOrEmpty("reserved").let { array ->
                        (0 until array.length()).joinToString(",") { array.opt(it).toString() }
                    },
                    "allowedips" to peer.listOfStrings("allowedIPs").joinToString(","),
                )
                return "wireguard://${percentEncode(settings.str("secretKey"))}@$peerHost:$port?${q(params)}$fragment"
            }
        }
        return null
    }

    // -- sing-box outbounds ---------------------------------------------------------------------

    /**
     * The inverse of [Links.parseLink]: a sing-box outbound as the link that produces it. What a
     * link can't spell (an xhttp transport, Reality's spiderX) is left out rather than
     * half-honoured, exactly as the Xray half above does.
     */
    private fun singBoxLink(outbound: JSONObject, name: String): String? {
        val server = host(outbound.str("server"))
        val port = outbound.int("server_port")
        val params = singBoxStreamParams(outbound)
        val fragment = "#" + percentEncode(name)
        return when (outbound.str("type")) {
            "vless" -> {
                val extra = linkedMapOf<String, Any?>(
                    "flow" to outbound.str("flow"),
                    "packetEncoding" to outbound.str("packet_encoding"),
                ).apply { putAll(params) }
                "vless://${percentEncode(outbound.str("uuid"))}@$server:$port?${q(extra)}$fragment"
            }

            "trojan" ->
                "trojan://${percentEncode(outbound.str("password"))}@$server:$port?${q(params)}$fragment"

            "vmess" -> {
                val reality = outbound.objOrNull("tls")?.objOrNull("reality")
                if (reality != null) {
                    // The base64 JSON form has nowhere to put Reality's public key.
                    val extra = linkedMapOf<String, Any?>("encryption" to outbound.str("security"))
                        .apply { putAll(params) }
                    "vmess://${percentEncode(outbound.str("uuid"))}@$server:$port?${q(extra)}$fragment"
                } else {
                    val json = obj(
                        "v" to "2",
                        "ps" to name,
                        "add" to outbound.str("server"),
                        "port" to port.toString(),
                        "id" to outbound.str("uuid"),
                        "aid" to outbound.int("alter_id").toString(),
                        "scy" to outbound.str("security").ifEmpty { "auto" },
                        "net" to params["type"].toString(),
                        "type" to (params["headerType"]?.toString() ?: "none"),
                        "host" to (params["host"]?.toString() ?: ""),
                        // A vmess link carries gRPC's service name in its path.
                        "path" to if (params["type"] == "grpc") params["serviceName"]?.toString() ?: ""
                        else params["path"]?.toString() ?: "",
                        "tls" to if (params["security"] == "none") "" else params["security"].toString(),
                        "sni" to (params["sni"]?.toString() ?: ""),
                        "alpn" to (params["alpn"]?.toString() ?: ""),
                        "fp" to (params["fp"]?.toString() ?: ""),
                    )
                    "vmess://" + b64Encode(stringify(json))
                }
            }

            "shadowsocks" -> {
                val method = outbound.str("method")
                val password = outbound.str("password")
                if (method.isEmpty() || password.isEmpty()) return null
                // SIP002: AEAD-2022 keys go percent-encoded, not base64.
                val userinfo = if (method.startsWith("2022-")) {
                    "${percentEncode(method)}:${percentEncode(password)}"
                } else {
                    b64Encode("$method:$password", urlSafe = true, pad = false)
                }
                val extra = linkedMapOf<String, Any?>(
                    "plugin" to outbound.str("plugin"),
                    "plugin-opts" to outbound.str("plugin_opts"),
                )
                if (outbound.objOrNull("udp_over_tcp") != null) extra["type"] = "udp-over-tcp"
                val query = q(extra)
                "ss://$userinfo@$server:$port${if (query.isEmpty()) "" else "?$query"}$fragment"
            }

            "hysteria2" -> {
                val hops = outbound.listOfStrings("server_ports")
                // Port hopping carries no port of its own; the link still needs one to dial.
                val firstHop = hops.firstOrNull()?.substringBefore('-')?.toIntOrNull() ?: 0
                val dialPort = if (port > 0) port else firstHop
                if (dialPort <= 0) return null
                val extra = linkedMapOf<String, Any?>(
                    "sni" to (outbound.objOrNull("tls") ?: JSONObject()).str("server_name"),
                    "mport" to hops.joinToString(","),
                    "upmbps" to outbound.int("up_mbps").takeIf { it > 0 },
                    "downmbps" to outbound.int("down_mbps").takeIf { it > 0 },
                )
                val obfs = outbound.objOrNull("obfs")
                if (obfs != null && obfs.str("type") == "salamander") {
                    extra["obfs"] = "salamander"
                    extra["obfs-password"] = obfs.str("password")
                }
                "hy2://${percentEncode(outbound.str("password"))}@$server:$dialPort?${q(extra)}$fragment"
            }

            "socks" -> {
                val user = outbound.str("username")
                val password = outbound.str("password")
                val credentials = if (user.isEmpty() && password.isEmpty()) {
                    ""
                } else {
                    b64Encode("$user:$password", urlSafe = true, pad = false) + "@"
                }
                "socks://$credentials$server:$port$fragment"
            }

            else -> null
        }
    }

    /** A sing-box transport and TLS, as the share-link query parameters that spell them. */
    private fun singBoxStreamParams(outbound: JSONObject): LinkedHashMap<String, Any?> {
        val transport = outbound.objOrNull("transport") ?: JSONObject()
        val params = linkedMapOf<String, Any?>()
        when (transport.str("type")) {
            "ws" -> {
                params["type"] = "ws"
                params["path"] = transport.str("path")
                params["host"] = (transport.objOrNull("headers") ?: JSONObject())
                    .listOfStrings("Host").joinToString(",")
                params["ed"] = transport.int("max_early_data").takeIf { it > 0 }
                params["eh"] = transport.str("early_data_header_name")
            }

            "httpupgrade" -> {
                params["type"] = "httpupgrade"
                params["path"] = transport.str("path")
                params["host"] = transport.str("host")
            }

            "http" -> {
                // sing-box's http transport is what a link spells as TCP with an HTTP header; the
                // h2 form parses back to the same thing.
                params["type"] = "tcp"
                params["headerType"] = "http"
                params["path"] = transport.str("path")
                params["host"] = transport.listOfStrings("host").joinToString(",")
            }

            "grpc" -> {
                params["type"] = "grpc"
                params["serviceName"] = transport.str("service_name")
                if (transport.bool("permit_without_stream")) params["permitWithoutStream"] = "true"
            }

            "quic" -> params["type"] = "quic"

            else -> params["type"] = "tcp"
        }

        val tls = outbound.objOrNull("tls")
        if (tls != null && tls.bool("enabled")) {
            val reality = tls.objOrNull("reality")
            params["sni"] = tls.str("server_name")
            params["fp"] = (tls.objOrNull("utls") ?: JSONObject()).str("fingerprint")
            if (reality != null) {
                params["security"] = "reality"
                params["pbk"] = reality.str("public_key")
                params["sid"] = reality.str("short_id")
            } else {
                params["security"] = "tls"
                params["alpn"] = tls.listOfStrings("alpn").joinToString(",")
                params["ech"] = (tls.objOrNull("ech") ?: JSONObject()).str("config")
            }
        } else {
            params["security"] = "none"
        }
        return params
    }

    /** The proxy outbounds of a full config, the one tagged "proxy" first. */
    fun mainOutbounds(server: Server): List<JSONObject> {
        val outbounds = (server.config ?: JSONObject()).arrOrEmpty("outbounds").objects()
            .filter { it.str("protocol") !in NON_PROXY && it.str("type") !in NON_PROXY_TYPES }
        return outbounds.sortedBy { it.str("tag") != "proxy" }
    }

    /** A share link for the server; for a provider config it describes its main node only. */
    fun serverLink(server: Server): String? {
        if (!server.isFullConfig) {
            val link = server.link.substringBefore('#')
            if (link.startsWith("vmess://", ignoreCase = true)) {
                // A vmess link keeps its name inside the base64 JSON, so renaming means recoding.
                val json = b64Decode(link.removePrefix("vmess://").removePrefix("VMESS://"))
                    ?.let { parseObject(it) } ?: return server.link
                json.put("ps", server.name)
                return "vmess://" + b64Encode(stringify(json))
            }
            return "$link#${percentEncode(server.name)}"
        }
        for (outbound in mainOutbounds(server)) {
            outboundLink(outbound, server.name)?.let { return it }
        }
        return null
    }

    /** How many nodes a server holds: a provider config can carry several. */
    fun nodeCount(server: Server): Int = if (server.isFullConfig) mainOutbounds(server).size else 1

    /** A sing-box config another client can import as is. */
    fun serverConfig(server: Server, settings: Settings): JSONObject {
        val plain = settings.copy().apply {
            killSwitch = false
            routePreset = ""
            blockAds = false
            routingMode = "global"
        }
        val built = ConfigBuilder.build(Selected.OfServer(server), plain, ipv6 = false)
        val config = built.config.copy()
        config.remove("log")
        // The client that imports this makes its own tunnel; this phone's TUN would only be in
        // its way (and a kill switch on someone else's device is not ours to set).
        config.remove("inbounds")
        config.put("remarks", server.name)
        return config
    }

    data class Exported(val text: String, val skipped: Int)

    /** Newline-separated share links, and how many servers couldn't be expressed as one. */
    fun exportLinks(servers: List<Server>): Exported {
        val out = mutableListOf<String>()
        var skipped = 0
        for (server in servers) {
            val link = serverLink(server)
            if (link != null) out.add(link) else skipped++
        }
        return Exported(out.joinToString("\n"), skipped)
    }

    /** One config as is, several as a JSON array (v2rayN, Happ and Xray clients import either). */
    fun exportJson(servers: List<Server>, settings: Settings): String {
        val configs = servers.map { serverConfig(it, settings) }
        return when (configs.size) {
            0 -> "{}"
            1 -> stringify(configs[0], 2)
            else -> stringify(JSONArray(configs as List<Any>), 2)
        }
    }

    /** A subscription body of share links, for "share as subscription". */
    fun subscriptionBody(servers: List<Server>): String =
        b64Encode(exportLinks(servers).text)

    /** allowInsecure in a provider config: Xray 26.2.6+ rejects it, so nothing exports it. */
    fun dropAllowInsecure(config: JSONObject) {
        com.zalchik.nimbus.util.dropKey(config, "allowInsecure")
    }

    /** The Xray JSON of a server's own config, with what the desktop strips on export. */
    fun xrayJson(server: Server): JSONObject? {
        val config = server.config?.copy() ?: return null
        if (!config.has("remarks")) config.put("remarks", server.name)
        dropAllowInsecure(config)
        return config
    }
}
