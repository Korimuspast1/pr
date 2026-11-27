package com.zalchik.nimbus

import com.zalchik.nimbus.data.Balancer
import com.zalchik.nimbus.data.Group
import com.zalchik.nimbus.data.RoutePreset
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.data.Store
import com.zalchik.nimbus.data.Subscription
import com.zalchik.nimbus.util.b64Encode
import com.zalchik.nimbus.util.obj
import org.json.JSONArray
import org.json.JSONObject

/**
 * The links, configs and settings the tests share — the desktop's test fixtures, with the same
 * values, so a difference in behaviour is a difference in the port and not in the test data.
 */
object Fixtures {
    const val UUID = "b831381d-6324-4d53-ad4f-8cda48b30811"
    const val PBK = "Z84J2IelR9ch3k8VtlVhhs5ycBUlXA7wHBWcBLjF1QA"

    val VMESS: String = "vmess://" + b64Encode(
        JSONObject()
            .put("v", "2")
            .put("ps", "vm-ws")
            .put("add", "vm.example.com")
            .put("port", "443")
            .put("id", UUID)
            .put("aid", "0")
            .put("scy", "auto")
            .put("net", "ws")
            .put("type", "none")
            .put("host", "cdn.example.com")
            .put("path", "/ws")
            .put("tls", "tls")
            .put("sni", "cdn.example.com")
            .put("fp", "chrome")
            .toString()
    )

    val LINKS: Map<String, String> = linkedMapOf(
        "vless-reality" to "vless://$UUID@1.2.3.4:443?type=tcp&security=reality&sni=www.microsoft.com" +
            "&fp=firefox&pbk=$PBK&sid=6ba85179e30d4fc2&flow=xtls-rprx-vision#NL%20Reality",
        "vless-xhttp" to "vless://$UUID@x.example.com:443?type=xhttp&security=tls&sni=x.example.com" +
            "&path=%2Fxh&mode=packet-up&alpn=h2,http/1.1#xhttp",
        "vless-grpc" to "vless://$UUID@g.example.com:443?type=grpc&security=tls&serviceName=svc&mode=multi#grpc",
        "vmess" to VMESS,
        "trojan" to "trojan://p%40ss@t.example.com:443?sni=t.example.com&type=ws&path=%2Ft#trojan",
        "ss-sip002" to "ss://" + b64Encode("chacha20-ietf-poly1305:secret", urlSafe = true, pad = false) +
            "@5.6.7.8:8388#ss",
        "ss-2022-plain" to "ss://2022-blake3-aes-128-gcm:AAAAAAAAAAAAAAAAAAAAAA%3D%3D@5.6.7.8:8388#ss2022",
        // Legacy: standard (padded) base64 of method:password, where SIP002 uses the url-safe
        // alphabet without padding. The older form that hides the address inside the base64 too is
        // covered in LinksTests.
        "ss-legacy" to "ss://" + b64Encode("aes-256-gcm:pw") + "@5.6.7.8:8388#legacy",
        "hy2" to "hy2://authpass@h.example.com:443?sni=h.example.com&obfs=salamander&obfs-password=ob#hy2",
        "hy2-hop" to "hysteria2://authpass@h.example.com:443?sni=h.example.com&mport=20000-30000#hop",
        "socks" to "socks://" + b64Encode("user:pw") + "@10.0.0.1:1080#socks",
        "wireguard" to "wireguard://yAnz5TF%2BlXXJte14tji3zlMNq%2BhdP3K9Ug6v7ZMg9Gc%3D@162.159.192.1:2408" +
            "?publickey=bmXOC%2BF1FxEMF9dyiK2H5%2F1SUtzH0JuVo51h2wPfgyo%3D" +
            "&address=172.16.0.2%2F32,2606:4700::2%2F128&reserved=1,2,3&mtu=1280#warp",
    )

    /** A link sing-box has no transport for: xhttp went in after the last supported release. */
    val XHTTP: String get() = LINKS.getValue("vless-xhttp")

    /** A provider's full Xray config: two nodes, a balancer, and its own routing rules. */
    val PROVIDER: JSONObject = JSONObject(
        """
        {
          "remarks": "provider",
          "outbounds": [
            {"tag": "proxy", "protocol": "vless",
             "settings": {"vnext": [{"address": "a.example.com", "port": 443,
               "users": [{"id": "$UUID", "flow": "", "encryption": "none"}]}]},
             "streamSettings": {"network": "tcp", "security": "tls",
               "tlsSettings": {"serverName": "a.example.com", "fingerprint": "chrome"}}},
            {"tag": "proxy2", "protocol": "trojan",
             "settings": {"servers": [{"address": "b.example.com", "port": 443, "password": "pw"}]},
             "streamSettings": {"network": "tcp", "security": "tls",
               "tlsSettings": {"serverName": "b.example.com", "allowInsecure": true}}},
            {"tag": "direct", "protocol": "freedom", "settings": {}},
            {"tag": "block", "protocol": "blackhole", "settings": {}},
            {"tag": "dns-out", "protocol": "dns", "settings": {}}
          ],
          "routing": {
            "domainStrategy": "AsIs",
            "balancers": [{"tag": "balancer", "selector": ["proxy", "proxy2"]}],
            "rules": [
              {"type": "field", "outboundTag": "direct", "ip": ["geoip:private", "geoip:ru"]},
              {"type": "field", "outboundTag": "direct", "domain": ["geosite:private", "geosite:ru"]},
              {"type": "field", "outboundTag": "block", "domain": ["geosite:category-ads-all"]},
              {"type": "field", "balancerTag": "balancer", "network": "tcp,udp"}
            ]
          }
        }
        """.trimIndent()
    )

    fun server(id: String = "x", name: String = "server", link: String = LINKS.getValue("vless-reality")) =
        Server(id = id, name = name, link = link)

    fun configServer(id: String = "c", name: String = "provider", config: JSONObject = PROVIDER) =
        Server(id = id, name = name, config = config)

    fun settings(transform: (Settings) -> Unit = {}): Settings = Settings().also(transform)

    fun store(vararg servers: Server): Store = Store(
        servers = servers.toMutableList(),
        groups = mutableListOf(Group("g1", "Added")),
        subscriptions = mutableListOf(Subscription(id = "s1", url = "https://panel.example.com/sub/abc")),
        balancers = mutableListOf(),
    )

    fun balancer(id: String = "b1", name: String = "auto", members: List<String> = listOf("a", "b", "c")) =
        Balancer(id = id, name = name, serverIds = members.toMutableList())

    fun preset(
        id: String = "p1",
        name: String = "Work",
        mode: String = RoutePreset.MODE_BYPASS,
        provider: Boolean = true,
        packages: List<String> = emptyList(),
        sites: List<String> = emptyList(),
    ) = RoutePreset(
        id,
        name,
        mode,
        provider = provider,
        packages = packages.toMutableList(),
        sites = sites.toMutableList(),
    )

    /** Every outbound in a built config, by tag. */
    fun outbounds(config: JSONObject): Map<String, JSONObject> {
        val out = LinkedHashMap<String, JSONObject>()
        val array = config.opt("outbounds") as? JSONArray ?: JSONArray()
        for (i in 0 until array.length()) {
            val item = array.optJSONObject(i) ?: continue
            out[item.optString("tag", i.toString())] = item
        }
        return out
    }

    /** Every endpoint in a built config, by tag. */
    fun endpoints(config: JSONObject): List<JSONObject> =
        (config.opt("endpoints") as? JSONArray)?.let { array ->
            (0 until array.length()).mapNotNull { array.optJSONObject(it) }
        } ?: emptyList()

    fun rules(config: JSONObject): List<JSONObject> =
        (config.optJSONObject("route")?.opt("rules") as? JSONArray)?.let { array ->
            (0 until array.length()).mapNotNull { array.optJSONObject(it) }
        } ?: emptyList()

    fun dnsRules(config: JSONObject): List<JSONObject> =
        (config.optJSONObject("dns")?.opt("rules") as? JSONArray)?.let { array ->
            (0 until array.length()).mapNotNull { array.optJSONObject(it) }
        } ?: emptyList()

    fun ruleSets(config: JSONObject): List<JSONObject> =
        (config.optJSONObject("route")?.opt("rule_set") as? JSONArray)?.let { array ->
            (0 until array.length()).mapNotNull { array.optJSONObject(it) }
        } ?: emptyList()

    /** A minimal JSON object with the given pairs, for hand-written fixtures. */
    fun json(vararg pairs: Pair<String, Any?>): JSONObject = obj(*pairs)
}
