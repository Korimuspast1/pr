package com.zalchik.nimbus.data

import com.zalchik.nimbus.util.arr
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.bool
import com.zalchik.nimbus.util.int
import com.zalchik.nimbus.util.listOfStrings
import com.zalchik.nimbus.util.long
import com.zalchik.nimbus.util.obj
import com.zalchik.nimbus.util.objOrNull
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.parseObject
import com.zalchik.nimbus.util.str
import com.zalchik.nimbus.util.strOrNull
import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

/** A random id, as desktop's new_id(): the first 12 hex digits of a UUID. */
fun newId(): String = UUID.randomUUID().toString().replace("-", "").take(12)

/**
 * Subscriptions and servers, persisted as one profiles.json — the desktop's nimbus/store.py,
 * with the same keys, so the file can be copied from a computer and used as it is.
 */
data class Server(
    /** Stable across subscription updates: [Store.replaceServers] keeps the id of a same-named
     *  server, so the selection and a balancer's member list survive an update. */
    var id: String,
    var name: String,
    /** Owner: a subscription or a user group id; "" is "My servers". */
    var subId: String = "",
    /** Share link, for link-based servers. */
    var link: String = "",
    /** Full Xray config, for provider-supplied JSON servers (and for balancers). */
    var config: JSONObject? = null,
    /** ms; -1 = failed; null = not measured yet. */
    var latency: Int? = null,
) {
    val isFullConfig: Boolean get() = config != null

    /** What the list shows in the protocol badge. */
    val protocol: String
        get() {
            val cfg = config
            if (cfg != null) {
                val protos = cfg.arrOrEmpty("outbounds").objects()
                    .map { it.str("protocol") }
                    .filter { it !in NON_PROXY_PROTOCOLS }
                val routing = cfg.objOrNull("routing")
                if (routing != null && routing.arrOrEmpty("balancers").length() > 0) {
                    return "balancer · ${protos.size}"
                }
                return protos.firstOrNull()?.takeIf { it.isNotEmpty() } ?: "config"
            }
            val scheme = link.substringBefore("://", "").lowercase()
            return SCHEME_PROTOCOLS[scheme] ?: scheme
        }

    companion object {
        val NON_PROXY_PROTOCOLS = setOf("freedom", "blackhole", "dns", "loopback")

        /** What the abbreviations a link may use stand for, so one protocol has one name. */
        val SCHEME_PROTOCOLS = mapOf(
            "hy2" to "hysteria2",
            "wg" to "wireguard",
            "socks5" to "socks",
            "ss" to "shadowsocks",
        )

        fun fromJson(json: JSONObject): Server = Server(
            id = json.str("id").ifEmpty { newId() },
            name = json.str("name"),
            subId = json.str("sub_id"),
            link = json.str("link"),
            config = json.objOrNull("config"),
            latency = json.strOrNull("latency")?.let { text -> text.toIntOrNull() }
                ?: (json.opt("latency") as? Number)?.toInt(),
        )

        fun ofLink(id: String, name: String, subId: String, link: String) =
            Server(id = id, name = name, subId = subId, link = link)

        fun ofConfig(id: String, name: String, subId: String, config: JSONObject) =
            Server(id = id, name = name, subId = subId, config = config)
    }

    fun toJson(): JSONObject = obj(
        "id" to id,
        "name" to name,
        "sub_id" to subId,
        "link" to link,
        "config" to config,
        "latency" to latency,
    )
}

data class Subscription(
    val id: String,
    var url: String,
    var name: String = "",
    var customName: String = "",
    /** "" = use the global setting. */
    var userAgent: String = "",
    /** Epoch seconds, as on desktop. */
    var lastUpdated: Long = 0,
    var upload: Long = 0,
    var download: Long = 0,
    var total: Long = 0,
    var expire: Long = 0,
    /** From the profile-update-interval header; 0 = use the setting. */
    var updateHours: Int = 0,
    var supportUrl: String = "",
    var webPageUrl: String = "",
    var announce: String = "",
    var error: String = "",
    /** The panel only gives servers to a device that sends its HWID. */
    var sendHwid: Boolean = false,
) {
    val title: String
        get() {
            val host = if ("//" in url) url.split("/").getOrElse(2) { url } else url
            return customName.ifEmpty { name.ifEmpty { host } }
        }

    companion object {
        fun fromJson(json: JSONObject): Subscription = Subscription(
            id = json.str("id").ifEmpty { newId() },
            url = json.str("url"),
            name = json.str("name"),
            customName = json.str("custom_name"),
            userAgent = json.str("user_agent"),
            lastUpdated = json.long("last_updated"),
            upload = json.long("upload"),
            download = json.long("download"),
            total = json.long("total"),
            expire = json.long("expire"),
            updateHours = json.int("update_hours"),
            supportUrl = json.str("support_url"),
            webPageUrl = json.str("web_page_url"),
            announce = json.str("announce"),
            error = json.str("error"),
            sendHwid = json.bool("send_hwid"),
        )
    }

    fun toJson(): JSONObject = obj(
        "id" to id,
        "url" to url,
        "name" to name,
        "custom_name" to customName,
        "user_agent" to userAgent,
        "last_updated" to lastUpdated,
        "upload" to upload,
        "download" to download,
        "total" to total,
        "expire" to expire,
        "update_hours" to updateHours,
        "support_url" to supportUrl,
        "web_page_url" to webPageUrl,
        "announce" to announce,
        "error" to error,
        "send_hwid" to sendHwid,
    )
}

/** A folder of servers the user added by hand (links, JSON configs). */
data class Group(val id: String, var name: String) {
    companion object {
        fun fromJson(json: JSONObject): Group = Group(id = json.str("id").ifEmpty { newId() }, name = json.str("name"))
    }

    fun toJson(): JSONObject = obj("id" to id, "name" to name)
}

/**
 * Servers picked from any subscriptions and groups, kept on the fastest of them.
 *
 * Desktop builds one Xray config with a leastPing balancer and burstObservatory; the phone hands
 * the same members to sing-box as a urltest group, which does the same job inside the core
 * (see engine/Balancer.kt).
 */
data class Balancer(
    val id: String,
    var name: String,
    /** Servers picked one by one. */
    var serverIds: MutableList<String> = mutableListOf(),
    /** Whole subscriptions / groups ("" = "My servers"), their new servers too. */
    var subIds: MutableList<String> = mutableListOf(),
    /** Seconds between speed checks. */
    var interval: Int = 30,
    /** stable: keep a node until it fails or another is much faster | fastest: always the best. */
    var mode: String = MODE_STABLE,
    /** Best member at the last check; -1 = none answered. */
    var latency: Int? = null,
) {
    companion object {
        const val MODE_STABLE = "stable"
        const val MODE_FASTEST = "fastest"

        fun fromJson(json: JSONObject): Balancer = Balancer(
            id = json.str("id").ifEmpty { newId() },
            name = json.str("name"),
            serverIds = listOfStrings(json.opt("server_ids")).toMutableList(),
            subIds = listOfStrings(json.opt("sub_ids")).toMutableList(),
            interval = json.int("interval", 30),
            mode = if (json.str("mode") == MODE_FASTEST) MODE_FASTEST else MODE_STABLE,
            latency = (json.opt("latency") as? Number)?.toInt(),
        )
    }

    fun toJson(): JSONObject = obj(
        "id" to id,
        "name" to name,
        "server_ids" to arr(serverIds),
        "sub_ids" to arr(subIds),
        "interval" to interval,
        "mode" to mode,
        "latency" to latency,
    )

    val isStable: Boolean get() = mode == MODE_STABLE
}

data class Store(
    var subscriptions: MutableList<Subscription> = mutableListOf(),
    var servers: MutableList<Server> = mutableListOf(),
    var groups: MutableList<Group> = mutableListOf(),
    var balancers: MutableList<Balancer> = mutableListOf(),
) {
    companion object {
        fun load(): Store {
            val text = Paths.readOrNull(Paths.profilesFile) ?: return Store()
            val data = parseObject(text) ?: return Store()
            return fromJson(data)
        }

        fun fromJson(data: JSONObject): Store = Store(
            subscriptions = (data.opt("subscriptions") as? JSONArray)?.objects()
                ?.map { Subscription.fromJson(it) }?.toMutableList() ?: mutableListOf(),
            servers = (data.opt("servers") as? JSONArray)?.objects()
                ?.map { Server.fromJson(it) }?.toMutableList() ?: mutableListOf(),
            groups = (data.opt("groups") as? JSONArray)?.objects()
                ?.map { Group.fromJson(it) }?.toMutableList() ?: mutableListOf(),
            balancers = (data.opt("balancers") as? JSONArray)?.objects()
                ?.map { Balancer.fromJson(it) }?.toMutableList() ?: mutableListOf(),
        )
    }

    fun save() {
        Paths.writeAtomic(Paths.profilesFile, toJson().toString())
    }

    fun toJson(): JSONObject = obj(
        "subscriptions" to arr(subscriptions.map { it.toJson() }),
        "servers" to arr(servers.map { it.toJson() }),
        "groups" to arr(groups.map { it.toJson() }),
        "balancers" to arr(balancers.map { it.toJson() }),
    )

    fun server(serverId: String): Server? = servers.firstOrNull { it.id == serverId }
    fun subscription(subId: String): Subscription? = subscriptions.firstOrNull { it.id == subId }
    fun group(groupId: String): Group? = groups.firstOrNull { it.id == groupId }
    fun balancer(balancerId: String): Balancer? = balancers.firstOrNull { it.id == balancerId }

    /** The balancer's servers that still exist, in list order. */
    fun members(balancer: Balancer): List<Server> {
        val ids = balancer.serverIds.toSet()
        val subs = balancer.subIds.toSet()
        return servers.filter { it.id in ids || it.subId in subs }
    }

    fun removeGroup(groupId: String) {
        groups.removeAll { it.id == groupId }
        servers.removeAll { it.subId == groupId }
    }

    fun serversOf(subId: String): List<Server> = servers.filter { it.subId == subId }

    /** Swap a subscription's servers, keeping ids (and so the selection) for unchanged names. */
    fun replaceServers(subId: String, incoming: List<Server>) {
        val old = serversOf(subId).associateBy { it.name }
        for (server in incoming) {
            old[server.name]?.let { previous ->
                server.id = previous.id
                server.latency = previous.latency
            }
        }
        val first = servers.indexOfFirst { it.subId == subId }.let { if (it < 0) servers.size else it }
        val rest = servers.filterNot { it.subId == subId }
        servers = (rest.take(first) + incoming + rest.drop(first)).toMutableList()
    }

    fun removeSubscription(subId: String) {
        subscriptions.removeAll { it.id == subId }
        servers.removeAll { it.subId == subId }
    }

    fun touch(subscription: Subscription) {
        subscription.lastUpdated = System.currentTimeMillis() / 1000
    }

    /** Servers / balancers last connected to, newest first. */
    fun pushRecent(settings: Settings, id: String) {
        settings.recentServers.removeAll { it == id }
        settings.recentServers.add(0, id)
        while (settings.recentServers.size > 8) settings.recentServers.removeAt(settings.recentServers.size - 1)
    }
}
