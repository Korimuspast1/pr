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
 * Sharing: Xray outbounds back to share links (`vless://` …), and configs as standalone JSON —
 * the port of nimbus/share.py.
 *
 * Share links describe Xray-shaped nodes, and that's what a panel's full config holds, so the
 * outbound -> link half is the desktop's code as it stands. The JSON half differs: the phone has
 * no Xray core, so what it exports is the sing-box config it actually runs, which SFA, Happ and
 * any other sing-box client imports as is.
 */
object Share {
    private val NON_PROXY = setOf("freedom", "blackhole", "dns", "loopback")

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

    /** Share link for one Xray outbound, or null for protocols links can't express. */
    fun outboundLink(outbound: JSONObject, name: String): String? {
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

    /** The proxy outbounds of a full config, the one tagged "proxy" first. */
    fun mainOutbounds(server: Server): List<JSONObject> {
        val outbounds = (server.config ?: JSONObject()).arrOrEmpty("outbounds").objects()
            .filter { it.str("protocol") !in NON_PROXY }
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
