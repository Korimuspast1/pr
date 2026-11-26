package com.zalchik.nimbus.engine

import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.util.arr
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.bool
import com.zalchik.nimbus.util.hostOf
import com.zalchik.nimbus.util.int
import com.zalchik.nimbus.util.listOfStrings
import com.zalchik.nimbus.util.obj
import com.zalchik.nimbus.util.objNonEmpty
import com.zalchik.nimbus.util.objOrNull
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.str
import org.json.JSONArray
import org.json.JSONObject

/**
 * Provider configs into sing-box.
 *
 * On the computer a subscription that answers the Happ user agent sends whole Xray configs, and
 * Nimbus runs them in Xray-core as they are. A phone carries one core (sing-box), so those
 * configs are translated instead: outbounds protocol by protocol, the provider's routing rules
 * into sing-box rules, its leastPing balancers into urltest groups, geoip/geosite lists into
 * downloadable rule sets.
 *
 * What has no sing-box counterpart is reported in [Converted.unsupported] rather than silently
 * dropped, and what does is kept: tags stay the same, so the provider's rules keep pointing at
 * the outbounds they meant.
 */
object Xray {

    /** Xray protocols that carry traffic; everything else is a helper. */
    private val NON_PROXY = setOf("freedom", "blackhole", "dns", "loopback")

    /** Rule sets a config asks for: tag -> download URL. Deduplicated by the builder. */
    class RuleSets {
        private val urls = LinkedHashMap<String, String>()

        fun geoip(code: String): String? {
            val name = code.lowercase()
            if (name.isEmpty()) return null
            val tag = "geoip-$name"
            urls[tag] = "https://raw.githubusercontent.com/SagerNet/sing-geoip/rule-set/$tag.srs"
            return tag
        }

        /**
         * Lists Xray's geosite.dat spells differently than SagerNet publishes them. Panels ship
         * Loyalsoldier's or v2fly's dat, where the Russian list is `geosite:ru`; sing-geosite has
         * no `geosite-ru.srs` and calls the same list `category-ru`. A rule set that can't be
         * downloaded stops the core from starting, so the name has to be right.
         */
        private val GEOSITE_ALIASES = mapOf("ru" to "category-ru")

        fun geosite(category: String): String? {
            val name = category.lowercase()
            if (name.isEmpty()) return null
            val tag = "geosite-${GEOSITE_ALIASES[name] ?: name}"
            urls[tag] = "https://raw.githubusercontent.com/SagerNet/sing-geosite/rule-set/$tag.srs"
            return tag
        }

        val tags: List<String> get() = urls.keys.toList()
        val entries: Map<String, String> get() = urls

        fun toJsonArray(): JSONArray {
            val out = JSONArray()
            for ((tag, url) in urls) {
                out.put(
                    obj(
                        "type" to "remote",
                        "tag" to tag,
                        "format" to "binary",
                        "url" to url,
                        "download_detour" to "direct",
                    ),
                )
            }
            return out
        }
    }

    data class Converted(
        val outbounds: MutableList<JSONObject> = mutableListOf(),
        val endpoints: MutableList<JSONObject> = mutableListOf(),
        val rules: MutableList<JSONObject> = mutableListOf(),
        val groups: MutableList<JSONObject> = mutableListOf(),
        val ruleSets: RuleSets = RuleSets(),
        var domainStrategy: String = "AsIs",
        /** Domains the provider's own DNS sends to the direct resolver. */
        val directDomains: MutableList<String> = mutableListOf(),
        val directRuleSets: MutableList<String> = mutableListOf(),
        val unsupported: MutableList<String> = mutableListOf(),
    )

    // -- outbounds ------------------------------------------------------------------------------

    /**
     * One Xray outbound as a sing-box one (plus an endpoint for WireGuard), keeping its tag.
     * Returns null for helpers sing-box has no use for (its dns and loopback outbounds).
     */
    fun outbound(source: JSONObject, converted: Converted): JSONObject? {
        val protocol = source.str("protocol")
        val tag = source.str("tag").ifEmpty { "proxy" }
        val settings = source.objOrNull("settings") ?: JSONObject()
        val stream = source.objOrNull("streamSettings") ?: JSONObject()
        return when (protocol) {
            "freedom" -> freedom(tag, settings)
            "blackhole" -> obj("type" to "block", "tag" to tag)
            "dns", "loopback" -> null
            "vless" -> vless(tag, settings, stream, converted)
            "vmess" -> vmess(tag, settings, stream, converted)
            "trojan" -> trojan(tag, settings, stream, converted)
            "shadowsocks" -> shadowsocks(tag, settings, stream, converted)
            "socks" -> socks(tag, settings, stream, converted)
            "http" -> http(tag, settings, stream, converted)
            "wireguard" -> wireguard(tag, settings, converted)
            "hysteria" -> hysteria(tag, settings, stream, converted)
            "hysteria2" -> hysteria2(tag, settings, stream, converted)
            else -> {
                converted.unsupported.add(
                    I18n.tr("Протокол {proto} не поддерживается на телефоне", values = mapOf("proto" to protocol)),
                )
                null
            }
        }
    }

    /** (server, user) from either the flat 26.x form or the vnext/servers lists. */
    private fun first(settings: JSONObject, key: String): Pair<JSONObject, JSONObject> {
        val list = settings.optJSONArray(key)
        if (list != null && list.length() > 0) {
            val server = list.optJSONObject(0) ?: JSONObject()
            val users = server.optJSONArray("users")
            val user = if (users != null && users.length() > 0) users.optJSONObject(0) ?: JSONObject() else JSONObject()
            val merged = JSONObject()
            for (name in server.keys()) merged.put(name, server.opt(name))
            for (name in user.keys()) merged.put(name, user.opt(name))
            return server to merged
        }
        return settings to settings
    }

    private fun freedom(tag: String, settings: JSONObject): JSONObject {
        val outbound = obj("type" to "direct", "tag" to tag)
        val redirect = settings.str("redirect")
        if (redirect.isNotEmpty()) {
            val host = hostOf(redirect)
            val port = redirect.substringAfterLast(':').toIntOrNull() ?: 0
            if (host.isNotEmpty()) outbound.put("override_address", host)
            if (port > 0) outbound.put("override_port", port)
        }
        // Xray's fragment helper (sockopt.fragment) has no outbound of its own in sing-box:
        // fragmentation is a TLS option there, applied by ConfigBuilder to every TLS outbound.
        return outbound
    }

    private fun vless(tag: String, settings: JSONObject, stream: JSONObject, converted: Converted): JSONObject {
        val (server, user) = first(settings, "vnext")
        val outbound = objNonEmpty(
            "type" to "vless",
            "tag" to tag,
            "server" to server.str("address"),
            "server_port" to server.int("port"),
            "uuid" to user.str("id").ifEmpty { user.str("uuid") },
            "flow" to user.str("flow").takeIf { it.isNotEmpty() },
            "packet_encoding" to user.str("packetEncoding").takeIf { it.isNotEmpty() },
        )
        applyStream(outbound, stream, converted)
        applyMultiplex(outbound, user.objOrNull("mux") ?: stream.objOrNull("mux"))
        applyDialer(outbound, stream)
        return outbound
    }

    private fun vmess(tag: String, settings: JSONObject, stream: JSONObject, converted: Converted): JSONObject {
        val (server, user) = first(settings, "vnext")
        val outbound = objNonEmpty(
            "type" to "vmess",
            "tag" to tag,
            "server" to server.str("address"),
            "server_port" to server.int("port"),
            "uuid" to user.str("id").ifEmpty { user.str("uuid") },
            "security" to user.str("security").ifEmpty { "auto" },
            "alter_id" to user.int("alterId").takeIf { it > 0 },
            "packet_encoding" to user.str("packetEncoding").takeIf { it.isNotEmpty() },
        )
        applyStream(outbound, stream, converted)
        applyMultiplex(outbound, user.objOrNull("mux") ?: stream.objOrNull("mux"))
        applyDialer(outbound, stream)
        return outbound
    }

    private fun trojan(tag: String, settings: JSONObject, stream: JSONObject, converted: Converted): JSONObject {
        val (server, user) = first(settings, "servers")
        val outbound = objNonEmpty(
            "type" to "trojan",
            "tag" to tag,
            "server" to server.str("address"),
            "server_port" to server.int("port"),
            "password" to user.str("password"),
        )
        applyStream(outbound, stream, converted)
        applyMultiplex(outbound, user.objOrNull("mux") ?: stream.objOrNull("mux"))
        applyDialer(outbound, stream)
        return outbound
    }

    private fun shadowsocks(tag: String, settings: JSONObject, stream: JSONObject, converted: Converted): JSONObject {
        val (server, user) = first(settings, "servers")
        val outbound = objNonEmpty(
            "type" to "shadowsocks",
            "tag" to tag,
            "server" to server.str("address"),
            "server_port" to server.int("port"),
            "method" to user.str("method"),
            "password" to user.str("password"),
            "plugin" to user.str("plugin").takeIf { it.isNotEmpty() },
            "plugin_opts" to user.str("pluginOpts").takeIf { it.isNotEmpty() },
        )
        if (user.bool("uot") || user.str("packetEncoding") == "packetaddr") {
            outbound.put("udp_over_tcp", obj("enabled" to true))
        }
        applyStream(outbound, stream, converted)
        applyMultiplex(outbound, user.objOrNull("mux") ?: stream.objOrNull("mux"))
        applyDialer(outbound, stream)
        return outbound
    }

    private fun socks(tag: String, settings: JSONObject, stream: JSONObject, converted: Converted): JSONObject {
        val (server, user) = first(settings, "servers")
        val outbound = objNonEmpty(
            "type" to "socks",
            "tag" to tag,
            "server" to server.str("address"),
            "server_port" to server.int("port"),
            "version" to settings.str("version", "5").removePrefix("socks"),
            "username" to user.str("user").takeIf { it.isNotEmpty() },
            "password" to user.str("pass").takeIf { it.isNotEmpty() },
        )
        applyStream(outbound, stream, converted)
        applyDialer(outbound, stream)
        return outbound
    }

    private fun http(tag: String, settings: JSONObject, stream: JSONObject, converted: Converted): JSONObject {
        val (server, user) = first(settings, "servers")
        val outbound = objNonEmpty(
            "type" to "http",
            "tag" to tag,
            "server" to server.str("address"),
            "server_port" to server.int("port"),
            "username" to user.str("user").takeIf { it.isNotEmpty() },
            "password" to user.str("pass").takeIf { it.isNotEmpty() },
            "path" to settings.str("path").takeIf { it.isNotEmpty() },
        )
        applyStream(outbound, stream, converted)
        applyDialer(outbound, stream)
        return outbound
    }

    private fun wireguard(tag: String, settings: JSONObject, converted: Converted): JSONObject {
        val endpointTag = "$tag-endpoint"
        val peers = JSONArray()
        for (peer in settings.arrOrEmpty("peers").objects()) {
            val endpoint = peer.str("endpoint")
            val address = endpoint.substringBeforeLast(':').trim('[', ']')
            val port = endpoint.substringAfterLast(':').toIntOrNull() ?: 0
            val convertedPeer = objNonEmpty(
                "address" to address.takeIf { it.isNotEmpty() },
                "port" to port.takeIf { it > 0 },
                "public_key" to peer.str("publicKey"),
                "pre_shared_key" to peer.str("preSharedKey").takeIf { it.isNotEmpty() },
                "allowed_ips" to arr(listOfStrings(peer.opt("allowedIPs")).ifEmpty { listOf("0.0.0.0/0", "::/0") }),
                "persistent_keepalive_interval" to peer.int("keepAlive").takeIf { it > 0 },
            )
            val reserved = peer.optJSONArray("reserved")
            if (reserved != null) convertedPeer.put("reserved", reserved)
            peers.put(convertedPeer)
        }
        if (peers.length() == 0) {
            // The flat single-peer form some panels emit.
            val endpoint = settings.str("endpoint")
            peers.put(
                objNonEmpty(
                    "address" to endpoint.substringBeforeLast(':').trim('[', ']'),
                    "port" to endpoint.substringAfterLast(':').toIntOrNull(),
                    "public_key" to settings.str("publicKey"),
                    "pre_shared_key" to settings.str("preSharedKey").takeIf { it.isNotEmpty() },
                    "allowed_ips" to JSONArray(listOf("0.0.0.0/0", "::/0")),
                ),
            )
        }
        converted.endpoints.add(
            objNonEmpty(
                "type" to "wireguard",
                "tag" to endpointTag,
                "system" to false,
                "address" to arr(listOfStrings(settings.opt("address")).ifEmpty { listOf("172.16.0.2/32") }),
                "private_key" to settings.str("secretKey").ifEmpty { settings.str("privateKey") },
                "mtu" to settings.int("mtu").takeIf { it > 0 },
                "peers" to peers,
                "workers" to settings.int("workers").takeIf { it > 0 },
            ),
        )
        return obj("type" to "direct", "tag" to tag, "detour" to endpointTag)
    }

    private fun hysteria(tag: String, settings: JSONObject, stream: JSONObject, converted: Converted): JSONObject {
        val version = settings.int("version", stream.objOrNull("hysteriaSettings")?.int("version") ?: 1)
        val hysteriaSettings = stream.objOrNull("hysteriaSettings") ?: JSONObject()
        if (version >= 2) return hysteria2(tag, settings, stream, converted)
        val outbound = objNonEmpty(
            "type" to "hysteria",
            "tag" to tag,
            "server" to settings.str("address"),
            "server_port" to settings.int("port"),
            "up_mbps" to settings.int("upMbps").takeIf { it > 0 },
            "down_mbps" to settings.int("downMbps").takeIf { it > 0 },
            "obfs" to hysteriaSettings.str("obfs").takeIf { it.isNotEmpty() },
            "auth_str" to hysteriaSettings.str("auth").takeIf { it.isNotEmpty() },
        )
        applyTls(outbound, stream, converted)
        applyDialer(outbound, stream)
        return outbound
    }

    private fun hysteria2(tag: String, settings: JSONObject, stream: JSONObject, converted: Converted): JSONObject {
        val (server, user) = if (settings.has("address")) settings to settings else first(settings, "servers")
        val hysteriaSettings = stream.objOrNull("hysteriaSettings") ?: JSONObject()
        val outbound = JSONObject()
        outbound.put("type", "hysteria2")
        outbound.put("tag", tag)
        outbound.put("server", server.str("address"))
        outbound.put("server_port", server.int("port"))
        val password = hysteriaSettings.str("auth").ifEmpty { user.str("password") }
        if (password.isNotEmpty()) outbound.put("password", password)
        server.int("upMbps").takeIf { it > 0 }?.let { outbound.put("up_mbps", it) }
        server.int("downMbps").takeIf { it > 0 }?.let { outbound.put("down_mbps", it) }
        // Xray's finalmask: salamander obfuscation and UDP port hopping.
        val masks = stream.objOrNull("finalmask")?.optJSONArray("udp")?.objects() ?: emptyList()
        for (mask in masks) {
            val maskSettings = mask.objOrNull("settings") ?: JSONObject()
            when (mask.str("type")) {
                "salamander" -> outbound.put(
                    "obfs",
                    obj("type" to "salamander", "password" to maskSettings.str("password")),
                )

                "udphop" -> {
                    val ports = maskSettings.str("remotePorts").split(',').map { it.trim() }.filter { it.isNotEmpty() }
                    if (ports.isNotEmpty()) {
                        outbound.put("server_ports", JSONArray(ports))
                        outbound.remove("server_port")
                        outbound.put("hop_interval", (maskSettings.str("interval", "30") + "s"))
                    }
                }
            }
        }
        applyTls(outbound, stream, converted)
        applyDialer(outbound, stream)
        return outbound
    }

    // -- stream settings ------------------------------------------------------------------------

    private fun applyStream(outbound: JSONObject, stream: JSONObject, converted: Converted) {
        val network = stream.str("network", "tcp").lowercase()
        when (network) {
            "tcp", "raw", "" -> {
                val header = stream.objOrNull("tcpSettings")?.objOrNull("header")
                    ?: stream.objOrNull("rawSettings")?.objOrNull("header")
                if (header != null && header.str("type") == "http") {
                    val request = header.objOrNull("request") ?: JSONObject()
                    val hosts = request.objOrNull("headers")?.let { listOfStrings(it.opt("Host")) } ?: emptyList()
                    val paths = listOfStrings(request.opt("path"))
                    outbound.put(
                        "transport",
                        objNonEmpty(
                            "type" to "http",
                            "host" to if (hosts.isEmpty()) null else JSONArray(hosts),
                            "path" to (paths.firstOrNull() ?: "/"),
                            "method" to "GET",
                            "headers" to extraHeaders(request),
                        ),
                    )
                }
            }

            "ws" -> {
                val settings = stream.objOrNull("wsSettings") ?: JSONObject()
                outbound.put(
                    "transport",
                    objNonEmpty(
                        "type" to "ws",
                        "path" to settings.str("path", "/").ifEmpty { "/" },
                        "headers" to objNonEmpty("Host" to settings.str("host").takeIf { it.isNotEmpty() }),
                        "max_early_data" to settings.int("maxEarlyData").takeIf { it > 0 },
                        "early_data_header_name" to settings.str("earlyDataHeaderName").takeIf { it.isNotEmpty() },
                    ),
                )
            }

            "httpupgrade" -> {
                val settings = stream.objOrNull("httpupgradeSettings") ?: JSONObject()
                outbound.put(
                    "transport",
                    objNonEmpty(
                        "type" to "httpupgrade",
                        "host" to settings.str("host").takeIf { it.isNotEmpty() },
                        "path" to settings.str("path", "/").ifEmpty { "/" },
                    ),
                )
            }

            "http", "h2" -> {
                val settings = stream.objOrNull("httpSettings") ?: JSONObject()
                outbound.put(
                    "transport",
                    objNonEmpty(
                        "type" to "http",
                        "host" to JSONArray(listOfStrings(settings.opt("host"))),
                        "path" to settings.str("path", "/").ifEmpty { "/" },
                        "method" to settings.str("method", "GET").ifEmpty { "GET" },
                    ),
                )
            }

            "grpc" -> {
                val settings = stream.objOrNull("grpcSettings") ?: JSONObject()
                if (settings.bool("multiMode") || settings.str("mode") == "multi") {
                    // sing-box has no gRPC multi mode; the service name is what matters.
                    converted.unsupported.add(I18n.tr("gRPC multiMode не поддерживается мобильным ядром"))
                }
                outbound.put(
                    "transport",
                    objNonEmpty(
                        "type" to "grpc",
                        "service_name" to settings.str("serviceName").ifEmpty { settings.str("authority") },
                        "permit_without_stream" to settings.bool("permitWithoutStream"),
                    ),
                )
            }

            "quic" -> outbound.put("transport", obj("type" to "quic"))

            "xhttp", "splithttp" -> converted.unsupported.add(
                I18n.tr("XHTTP-серверы не поддерживаются мобильным ядром sing-box"),
            )

            "kcp", "mkcp" -> converted.unsupported.add(
                I18n.tr("KCP не поддерживается мобильным ядром sing-box"),
            )
        }
        applyTls(outbound, stream, converted)
    }

    private fun extraHeaders(request: JSONObject): JSONObject? {
        val headers = request.objOrNull("headers") ?: return null
        val out = JSONObject()
        for (name in headers.keys()) {
            if (name.equals("Host", ignoreCase = true)) continue
            out.put(name, JSONArray(listOfStrings(headers.opt(name))))
        }
        return if (out.length() == 0) null else out
    }

    private fun applyTls(outbound: JSONObject, stream: JSONObject, converted: Converted) {
        val security = stream.str("security", "none").lowercase()
        if (security == "tls") {
            val settings = stream.objOrNull("tlsSettings") ?: JSONObject()
            val tls = JSONObject()
            tls.put("enabled", true)
            if (settings.str("serverName").isNotEmpty()) tls.put("server_name", settings.str("serverName"))
            val alpn = listOfStrings(settings.opt("alpn"))
            if (alpn.isNotEmpty()) tls.put("alpn", JSONArray(alpn))
            if (settings.str("fingerprint").isNotEmpty()) {
                tls.put("utls", obj("enabled" to true, "fingerprint" to settings.str("fingerprint")))
            }
            if (settings.str("minVersion").isNotEmpty()) tls.put("min_version", settings.str("minVersion"))
            if (settings.str("maxVersion").isNotEmpty()) tls.put("max_version", settings.str("maxVersion"))
            val cipherSuites = listOfStrings(settings.opt("cipherSuites"))
            if (cipherSuites.isNotEmpty()) tls.put("cipher_suites", JSONArray(cipherSuites))
            val ech = listOfStrings(settings.opt("echConfigList")) + settings.str("echConfigList")
            val echList = ech.filter { it.isNotEmpty() }.distinct()
            if (echList.isNotEmpty()) tls.put("ech", obj("enabled" to true, "config" to JSONArray(echList)))
            // allowInsecure is dropped as on desktop; sing-box's own `insecure` stays off. A
            // pinned certificate becomes sing-box's public-key pin only when the provider gave a
            // public key hash (certificatePublicSha256 is a different pin and is reported).
            val pin = settings.str("pinnedPeerCertPublicKeySha256")
            if (pin.isNotEmpty()) tls.put("certificate_public_key_sha256", JSONArray(listOf(pin)))
            if (settings.str("pinnedPeerCertSha256").isNotEmpty() || settings.str("verifyPeerCertCN").isNotEmpty()) {
                converted.unsupported.add(I18n.tr("Закрепление сертификата провайдером не поддерживается мобильным ядром"))
            }
            if (settings.bool("allowInsecure")) {
                converted.unsupported.add(I18n.tr("allowInsecure игнорируется: соединение проверяет сертификат"))
            }
            outbound.put("tls", tls)
        } else if (security == "reality") {
            val settings = stream.objOrNull("realitySettings") ?: JSONObject()
            outbound.put(
                "tls",
                obj(
                    "enabled" to true,
                    "server_name" to settings.str("serverName"),
                    "utls" to obj(
                        "enabled" to true,
                        "fingerprint" to settings.str("fingerprint").ifEmpty { "chrome" },
                    ),
                    "reality" to objNonEmpty(
                        "enabled" to true,
                        "public_key" to settings.str("publicKey"),
                        "short_id" to settings.str("shortId"),
                    ),
                ),
            )
        }
    }

    private fun applyMultiplex(outbound: JSONObject, mux: JSONObject?) {
        if (mux == null || !mux.bool("enabled")) return
        if (outbound.str("flow").isNotEmpty()) return
        outbound.put(
            "multiplex",
            objNonEmpty(
                "enabled" to true,
                "protocol" to when (mux.str("protocol").lowercase()) {
                    "smux" -> "smux"
                    "yamux" -> "yamux"
                    else -> "h2mux"
                },
                "max_connections" to mux.int("concurrency").takeIf { it > 0 },
                "max_streams" to mux.int("maxStreams").takeIf { it > 0 },
                "padding" to mux.bool("padding"),
            ),
        )
    }

    /**
     * Xray's dialerProxy / proxySettings — "dial through that other outbound" — is sing-box's
     * detour. A fragment helper turns into a plain direct outbound: sing-box fragments at the
     * TLS layer instead (see [Links.applyFragment]).
     */
    private fun applyDialer(outbound: JSONObject, stream: JSONObject) {
        val sockopt = stream.objOrNull("sockopt") ?: JSONObject()
        val dialer = sockopt.str("dialerProxy")
        val chained = dialer.ifEmpty { outbound.objOrNull("proxySettings")?.str("tag") ?: "" }
        if (chained.isNotEmpty()) outbound.put("detour", chained)
        if (sockopt.bool("tcpFastOpen")) outbound.put("tcp_fast_open", true)
        // sockopt.interface (binding to a named adapter) is the desktop TUN trick: on Android the
        // core protects its own sockets through the platform interface, so it is simply unused.
    }

    // -- routing --------------------------------------------------------------------------------

    /**
     * The provider's rules, as sing-box rules. A rule that can't be expressed (one bound to an
     * inbound tag that no longer exists, an unknown matcher) is skipped rather than loosened:
     * desktop's balancer does the same with the tags it can't remap.
     */
    fun rules(source: JSONObject, converted: Converted): List<JSONObject> {
        val outbounds = source.arrOrEmpty("outbounds").objects()
        val protocols = outbounds.associate { it.str("tag") to it.str("protocol") }
        val balancerTags = source.objOrNull("routing")?.arrOrEmpty("balancers")?.objects()
            ?.map { it.str("tag") }?.toSet() ?: emptySet()
        val out = mutableListOf<JSONObject>()
        val sourceRules = source.objOrNull("routing")?.arrOrEmpty("rules")?.objects() ?: emptyList()
        for (rule in sourceRules) {
            if (rule.has("inboundTag") || rule.has("inbound_tag")) {
                // Inbound tags of the provider's own inbounds (its socks/http listeners) don't
                // exist on the phone: everything arrives through the one TUN inbound.
                continue
            }
            val match = matchersOf(rule, converted) ?: continue
            val target = when {
                rule.str("balancerTag").isNotEmpty() ->
                    obj("action" to "route", "outbound" to groupTag(rule.str("balancerTag")))

                rule.str("outboundTag").isNotEmpty() -> {
                    val tag = rule.str("outboundTag")
                    when (protocols[tag]) {
                        "freedom" -> obj("action" to "route", "outbound" to tag)
                        "blackhole" -> obj("action" to "reject")
                        "dns", "loopback" -> continue
                        null -> if (tag in balancerTags) {
                            obj("action" to "route", "outbound" to groupTag(tag))
                        } else {
                            continue
                        }
                        else -> obj("action" to "route", "outbound" to tag)
                    }
                }

                else -> continue
            }
            for (name in target.keys()) match.put(name, target.opt(name))
            out.add(match)
        }
        return out
    }

    /** The tag of the sing-box urltest group that stands in for an Xray balancer. */
    fun groupTag(balancerTag: String): String =
        if (balancerTag.startsWith("nimbus-")) balancerTag else "nimbus-group-$balancerTag"

    /** Rule matchers, or null when the rule can't be expressed. Public: a balancer remaps the
     *  provider rules of the member whose rules it keeps. */
    fun matchersOf(rule: JSONObject, converted: Converted): JSONObject? {
        val match = JSONObject()
        val domains = mutableListOf<String>()
        val domainSuffix = mutableListOf<String>()
        val domainKeyword = mutableListOf<String>()
        val domainRegex = mutableListOf<String>()
        val ruleSets = mutableListOf<String>()
        val ipCidr = mutableListOf<String>()
        var ipIsPrivate = false

        for (entry in listOfStrings(rule.opt("domain"))) {
            val value = entry.trim()
            if (value.isEmpty()) continue
            when {
                value.startsWith("geosite:") -> converted.ruleSets.geosite(value.substring(8))?.let { ruleSets.add(it) }
                value.startsWith("domain:") -> domainSuffix.add(value.substring(7))
                value.startsWith("full:") -> domains.add(value.substring(5))
                value.startsWith("keyword:") -> domainKeyword.add(value.substring(8))
                value.startsWith("regexp:") -> domainRegex.add(value.substring(7))
                value.startsWith("attr:") -> return null   // attribute matchers have no counterpart
                else -> domainSuffix.add(value)           // Xray: a domain and its subdomains
            }
        }
        for (entry in listOfStrings(rule.opt("ip"))) {
            val value = entry.trim()
            if (value.isEmpty()) continue
            when {
                value.lowercase().startsWith("geoip:private") -> ipIsPrivate = true
                value.lowercase().startsWith("geoip:") -> converted.ruleSets.geoip(value.substring(6))?.let { ruleSets.add(it) }
                else -> ipCidr.add(if ('/' in value) value else "$value/32")
            }
        }
        for (entry in listOfStrings(rule.opt("source"))) {
            if (entry.contains('/')) ipCidr.add(entry) else ipCidr.add("$entry/32")
        }

        if (domains.isNotEmpty()) match.put("domain", JSONArray(domains))
        if (domainSuffix.isNotEmpty()) match.put("domain_suffix", JSONArray(domainSuffix))
        if (domainKeyword.isNotEmpty()) match.put("domain_keyword", JSONArray(domainKeyword))
        if (domainRegex.isNotEmpty()) match.put("domain_regex", JSONArray(domainRegex))
        if (ruleSets.isNotEmpty()) match.put("rule_set", JSONArray(ruleSets.distinct()))
        if (ipCidr.isNotEmpty()) match.put("ip_cidr", JSONArray(ipCidr))
        if (ipIsPrivate) match.put("ip_is_private", true)

        val network = listOfStrings(rule.opt("network"))
        if (network.isNotEmpty()) match.put("network", JSONArray(network))
        val protocol = listOfStrings(rule.opt("protocol"))
        if (protocol.isNotEmpty()) match.put("protocol", JSONArray(protocol))
        val ports = mutableListOf<Int>()
        val ranges = mutableListOf<String>()
        for (entry in listOfStrings(rule.opt("port"))) {
            val range = entry.trim()
            if ('-' in range) ranges.add(range.replace('-', ':')) else range.toIntOrNull()?.let { ports.add(it) }
        }
        if (ports.isNotEmpty()) match.put("port", JSONArray(ports))
        if (ranges.isNotEmpty()) match.put("port_range", JSONArray(ranges))
        if (rule.has("attrs") || rule.has("domainMatcher")) return null
        return if (match.length() == 0) null else match
    }

    /**
     * The provider's balancers as sing-box urltest groups: same job (keep to the node that
     * answers fastest), done by the core instead of by observatory + a routing API.
     */
    fun groups(source: JSONObject, interval: String, converted: Converted): List<JSONObject> {
        val outbounds = source.arrOrEmpty("outbounds").objects()
        val balancers = source.objOrNull("routing")?.arrOrEmpty("balancers")?.objects() ?: return emptyList()
        val out = mutableListOf<JSONObject>()
        for (balancer in balancers) {
            val tag = balancer.str("tag")
            if (tag.isEmpty()) continue
            val selectors = listOfStrings(balancer.opt("selector"))
            val members = outbounds.filter { item ->
                val itemTag = item.str("tag")
                itemTag.isNotEmpty() && item.str("protocol") !in NON_PROXY &&
                    (selectors.isEmpty() || selectors.any { itemTag.startsWith(it) })
            }.map { it.str("tag") }
            if (members.isEmpty()) continue
            val strategy = balancer.objOrNull("strategy")?.str("type") ?: "leastPing"
            out.add(
                objNonEmpty(
                    "type" to "urltest",
                    "tag" to groupTag(tag),
                    "outbounds" to JSONArray(members),
                    // leastPing keeps the fastest node and only moves when the gain is worth it —
                    // sing-box's tolerance is exactly that. random has no counterpart: the group
                    // still picks the fastest, which is what a phone wants on a metered network.
                    "tolerance" to if (strategy == "random") 0 else 50,
                    "interval" to interval,
                    "idle_timeout" to "5m",
                    "interrupt_exist_connections" to false,
                ),
            )
        }
        return out
    }

    /**
     * What the provider's DNS asks for: the domains its "direct" resolver answers, so they go to
     * the phone's direct DNS instead of through the tunnel.
     */
    fun dnsDomains(source: JSONObject, converted: Converted) {
        val servers = source.objOrNull("dns")?.arrOrEmpty("servers")?.objects() ?: return
        for (server in servers) {
            val domains = listOfStrings(server.opt("domains"))
            if (domains.isEmpty()) continue
            val suffix = mutableListOf<String>()
            val sets = mutableListOf<String>()
            for (entry in domains) {
                when {
                    entry.startsWith("geosite:") -> converted.ruleSets.geosite(entry.substring(8))?.let { sets.add(it) }
                    entry.startsWith("full:") -> suffix.add(entry.substring(5))
                    entry.startsWith("domain:") -> suffix.add(entry.substring(7))
                    entry.startsWith("keyword:") -> suffix.add(entry.substring(8))
                    else -> suffix.add(entry)
                }
            }
            converted.directDomains.addAll(suffix)
            converted.directRuleSets.addAll(sets)
        }
    }

    /** Whether a config's outbounds can all run on the phone. */
    fun convert(config: JSONObject, interval: String = "3m"): Converted {
        val converted = Converted()
        for (item in config.arrOrEmpty("outbounds").objects()) {
            val result = outbound(item, converted) ?: continue
            // allowInsecure survives in provider configs and older saved ones; strip it as the
            // desktop does before anything reads it.
            com.zalchik.nimbus.util.dropKey(result, "allowInsecure")
            converted.outbounds.add(result)
        }
        dnsDomains(config, converted)
        converted.rules.addAll(rules(config, converted))
        converted.groups.addAll(groups(config, interval, converted))
        converted.domainStrategy = config.objOrNull("routing")?.str("domainStrategy").orEmpty().ifEmpty { "AsIs" }
        return converted
    }

    /**
     * What the provider's own rules keep out of the VPN, as desktop's provider_routing(): null
     * when they don't (a share link, or rules that only keep the local network direct), and
     * {"ru": true} when Russian sites are among them.
     */
    fun providerRouting(config: JSONObject?): Boolean? {
        if (config == null) return null
        val outbounds = config.arrOrEmpty("outbounds").objects()
        val direct = outbounds.filter { it.str("protocol") == "freedom" && it.str("tag").isNotEmpty() }
            .map { it.str("tag") }.toSet()
        if (direct.isEmpty()) return null
        var found = false
        var ru = false
        for (rule in config.objOrNull("routing")?.arrOrEmpty("rules")?.objects() ?: emptyList()) {
            if (rule.str("outboundTag") !in direct) continue
            val entries = listOfStrings(rule.opt("domain")) + listOfStrings(rule.opt("ip"))
            val public = entries.filter { !isLocalEntry(it) }
            if (public.isNotEmpty()) {
                found = true
                if (public.any { entry -> RU_MARKERS.any { entry.lowercase().contains(it) } }) ru = true
            }
        }
        return if (found) ru else null
    }

    private val LOCAL_PREFIXES = listOf(
        "geoip:private", "10.", "127.", "169.254.", "172.16.", "192.168.", "fc00:", "fe80:", "::1",
        "localhost", "domain:local", "geosite:private",
    )
    private val RU_MARKERS = listOf(
        "category-ru", "geoip:ru", "geosite:ru", "domain:ru", "domain:su", "xn--p1ai", "yandex", "vk.com",
    )

    private fun isLocalEntry(entry: String): Boolean {
        val value = entry.lowercase()
        return LOCAL_PREFIXES.any { value.startsWith(it) }
    }

    /** Every outbound of a full-config server, for latency tests (detour chains included). */
    fun proxyOutbounds(server: com.zalchik.nimbus.data.Server): List<JSONObject> {
        val config = server.config ?: return emptyList()
        return convert(config).outbounds
    }
}
