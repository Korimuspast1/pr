package com.zalchik.nimbus.engine

import com.zalchik.nimbus.data.Paths
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.data.Subscription
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.b64Decode
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.str
import com.zalchik.nimbus.util.toHexString
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.IOException
import java.security.MessageDigest
import java.util.UUID
import java.util.concurrent.TimeUnit

/** What the device headers say about this phone; set once from `Build.*` by the application. */
object Device {
    var os: String = "Android"
    var osVersion: String = ""
    var model: String = ""
    var name: String = ""
}

/**
 * Fetching and decoding subscriptions — the port of nimbus/subscription.py.
 *
 * The phone talks to the panel directly (no core to route through): while the VPN is up its own
 * traffic already goes through the tunnel, so a subscription updates over the connection exactly
 * as it does on desktop with `proxy` set.
 */
object Subscription {
    private val DUMMY_HOSTS = setOf("", "0.0.0.0", "::", "localhost")
    private const val TIMEOUT = 20L

    /** The machine id: a random one kept in the data folder (a phone has no MachineGuid). */
    fun machineId(file: File = Paths.deviceIdFile): String {
        val existing = try {
            file.readText().trim()
        } catch (e: Exception) {
            ""
        }
        if (existing.isNotEmpty()) return existing
        val value = UUID.randomUUID().toString().replace("-", "")
        try {
            file.parentFile?.mkdirs()
            file.writeText(value)
        } catch (e: Exception) {
            // No place to keep it: every run then looks like a new device, which panels accept.
        }
        return value
    }

    /** Stable per device, but not a raw identifier: panels only need to tell devices apart. */
    fun autoHwid(machineId: String = machineId()): String =
        MessageDigest.getInstance("SHA-256")
            .digest("nimbus:$machineId".toByteArray())
            .toHexString()
            .take(32)

    fun randomHwid(): String = UUID.randomUUID().toString().replace("-", "")

    /** HTTP headers take ASCII only; a phone named «Мой телефон» goes percent-encoded. */
    fun headerValue(value: String): String =
        if (value.all { it.code < 128 }) value else percentEncodeHeader(value)

    private fun percentEncodeHeader(value: String): String {
        val safe = " -_.()"
        val out = StringBuilder()
        for (byte in value.toByteArray()) {
            val c = byte.toInt().toChar()
            if (c in 'A'..'Z' || c in 'a'..'z' || c in '0'..'9' || c in safe) out.append(c)
            else out.append('%').append("%02X".format(byte))
        }
        return out.toString()
    }

    /** Device headers Remnawave and other panels read (the same ones Happ and v2rayN send). */
    fun hwidHeaders(custom: String = "", device: String = ""): Map<String, String> {
        val values = linkedMapOf(
            "x-hwid" to (custom.trim().ifEmpty { autoHwid() }),
            "x-device-os" to Device.os,
            "x-ver-os" to Device.osVersion,
            "x-device-model" to device.trim().ifEmpty { Device.model.ifEmpty { Device.name } },
        )
        return values.mapValues { headerValue(it.value) }
    }

    /** The address an outbound dials, from either the Xray or the sing-box shape. */
    private fun addressOf(outbound: org.json.JSONObject): String {
        outbound.str("server").takeIf { it.isNotEmpty() }?.let { return it }
        val settings = outbound.optJSONObject("settings") ?: return ""
        for (key in listOf("vnext", "servers")) {
            val list = settings.optJSONArray(key) ?: continue
            for (i in 0 until list.length()) {
                val value = list.optJSONObject(i)?.optString("address") ?: ""
                if (value.isNotEmpty()) return value
            }
        }
        return settings.optString("address", "")
    }

    /**
     * True if no server points anywhere real: the stub "update the app / device not allowed"
     * entries panels hand out instead of the list.
     */
    fun placeholdersOnly(servers: List<Server>): Boolean {
        for (server in servers) {
            val outbounds = try {
                if (server.isFullConfig) {
                    server.config!!.arrOrEmpty("outbounds").objects().filter {
                        it.str("protocol") !in setOf("freedom", "blackhole", "dns", "loopback")
                    }.map { addressOf(it) }
                } else {
                    listOf(addressOf(Links.parseLink(server.link).outbound))
                }
            } catch (e: Exception) {
                continue
            }
            for (address in outbounds) {
                val host = address.trim('[', ']').lowercase()
                if (host !in DUMMY_HOSTS && !host.startsWith("127.")) return false
            }
        }
        return true
    }

    /** Panels send some headers as "base64:<data>". */
    fun headerText(value: String): String {
        if (!value.startsWith("base64:")) return value
        return b64Decode(value.removePrefix("base64:")) ?: value
    }

    data class Parsed(val servers: MutableList<Server>, val errors: MutableList<String>)

    /** Decode a subscription body or pasted text. */
    fun parseBody(text: String, subId: String = ""): Parsed {
        val body = text.trim().removePrefix("\uFEFF")
        val servers = mutableListOf<Server>()
        val errors = mutableListOf<String>()

        if (body.startsWith("[") || body.startsWith("{")) {
            val data = try {
                com.zalchik.nimbus.util.parseJson(body)
            } catch (e: Exception) {
                null
            }
            if (data != null) {
                val items = when (data) {
                    is org.json.JSONArray -> (0 until data.length()).mapNotNull { data.optJSONObject(it) }
                    is org.json.JSONObject -> listOf(data)
                    else -> emptyList()
                }
                for ((index, config) in items.withIndex()) {
                    val name = config.str("remarks")
                    if (config.has("outbounds")) {
                        servers.add(
                            com.zalchik.nimbus.data.Server.ofConfig(
                                com.zalchik.nimbus.data.newId(),
                                name.ifEmpty { "Config ${index + 1}" },
                                subId,
                                config,
                            )
                        )
                    } else {
                        errors.add("item ${index + 1}: not an Xray config")
                    }
                }
                return Parsed(servers, errors)
            }
        }

        var lines = body.lines()
        if (lines.none { Links.isLink(it) }) {
            val flat = body.replace("\n", "").replace("\r", "")
            b64Decode(flat)?.let { lines = it.lines() }
        }
        for (raw in lines) {
            val line = raw.trim()
            if (!Links.isLink(line)) continue
            try {
                val parsed = Links.parseLink(line)
                servers.add(
                    com.zalchik.nimbus.data.Server.ofLink(
                        com.zalchik.nimbus.data.newId(),
                        parsed.name.ifEmpty { line.substringBefore("://") },
                        subId,
                        line,
                    )
                )
            } catch (e: LinkError) {
                errors.add(e.message ?: "bad link")
            }
        }
        if (servers.isEmpty() && errors.isEmpty()) errors.add("no servers found")
        return Parsed(servers, errors)
    }

    /** An HTTP failure the panel caused, not the network: worth showing instead of retrying. */
    class FetchError(message: String) : IOException(message)

    fun client(): OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(TIMEOUT, TimeUnit.SECONDS)
        .writeTimeout(TIMEOUT, TimeUnit.SECONDS)
        .followRedirects(true)
        .build()

    /**
     * Download a subscription, update its metadata in place and return its servers.
     *
     * `hwidMode` "auto": the device headers go out only if the panel gives no real servers
     * without them, which is then remembered; "always" / "never" as they say.
     */
    fun fetch(
        sub: Subscription,
        defaultUserAgent: String,
        hwidMode: String = "auto",
        customHwid: String = "",
        deviceName: String = "",
        client: OkHttpClient = client(),
    ): List<Server> {
        val userAgent = sub.userAgent.ifEmpty { defaultUserAgent }.ifEmpty { "Nimbus/${Paths.VERSION}" }
        val headers = hwidHeaders(customHwid, deviceName)
        if (hwidMode == "never") return fetchOnce(sub, userAgent, emptyMap(), client)
        if (hwidMode == "always" || sub.sendHwid) return fetchOnce(sub, userAgent, headers, client)

        var first = emptyList<Server>()
        var error: Exception? = null
        try {
            first = fetchOnce(sub, userAgent, emptyMap(), client)
            if (!placeholdersOnly(first)) return first
        } catch (e: IOException) {
            if (e !is FetchError) throw e      // no connection at all: an HWID won't help
            error = e
        } catch (e: Exception) {
            error = e
        }
        val second = try {
            fetchOnce(sub, userAgent, headers, client)
        } catch (e: Exception) {
            // The panel's stub entries say what's wrong better than an HTTP error.
            if (first.isNotEmpty()) return first
            throw error ?: e
        }
        if (first.isNotEmpty() && placeholdersOnly(second)) return first
        sub.sendHwid = true
        return second
    }

    private fun fetchOnce(
        sub: Subscription,
        userAgent: String,
        extra: Map<String, String>,
        client: OkHttpClient,
    ): List<Server> {
        val request = Request.Builder()
            .url(sub.url)
            .header("User-Agent", userAgent)
            .apply { extra.forEach { (key, value) -> header(key, value) } }
            .build()
        client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) {
                throw FetchError("HTTP ${response.code}")
            }
            val headers = response.headers
            val title = headers["profile-title"]
            if (title != null) {
                sub.name = headerText(title)
            } else {
                val disposition = headers["content-disposition"]
                if (disposition != null) {
                    FILE_NAME.find(disposition)?.groupValues?.get(1)?.let { sub.name = it }
                }
            }
            val info = USERINFO.findAll(headers["subscription-userinfo"] ?: "")
                .associate { it.groupValues[1] to it.groupValues[2].toLongOrNull() ?: 0L }
            sub.upload = info["upload"] ?: 0
            sub.download = info["download"] ?: 0
            sub.total = info["total"] ?: 0
            sub.expire = info["expire"] ?: 0
            sub.updateHours = headers["profile-update-interval"]?.toIntOrNull() ?: 0
            sub.supportUrl = headers["support-url"] ?: ""
            sub.webPageUrl = headers["profile-web-page-url"] ?: ""
            sub.announce = headerText(headers["announce"] ?: "")

            val parsed = parseBody(response.body?.string() ?: "", sub.id)
            if (parsed.servers.isEmpty()) {
                throw FetchError(parsed.errors.joinToString("; ").ifEmpty { "empty subscription" })
            }
            sub.error = ""
            return parsed.servers
        }
    }

    private val FILE_NAME = Regex("filename\\*?=(?:UTF-8'')?\"?([^\";]+)")
    private val USERINFO = Regex("(\\w+)=(\\d+)")
}
