package com.zalchik.nimbus.data

import com.zalchik.nimbus.util.arr
import com.zalchik.nimbus.util.bool
import com.zalchik.nimbus.util.int
import com.zalchik.nimbus.util.listOfStrings
import com.zalchik.nimbus.util.obj
import com.zalchik.nimbus.util.parseObject
import com.zalchik.nimbus.util.str
import org.json.JSONArray
import org.json.JSONObject

/**
 * A routing preset: which apps and which sites go past the VPN ("bypass") or, the other way
 * round, are the only ones that go through it ("vpn").
 *
 * Desktop presets name programs by path (chrome.exe); a phone names them by package, so the
 * `apps` list of an imported desktop preset is dropped and its sites are kept.
 */
data class RoutePreset(
    val id: String,
    var name: String = "",
    var mode: String = MODE_BYPASS,
    /** Keep the subscription's own rules under this preset (a "bypass" one can sit on top). */
    var provider: Boolean = true,
    var packages: MutableList<String> = mutableListOf(),
    var sites: MutableList<String> = mutableListOf(),
) {
    companion object {
        const val MODE_BYPASS = "bypass"
        const val MODE_VPN = "vpn"

        fun fromJson(json: JSONObject): RoutePreset = RoutePreset(
            id = json.str("id"),
            name = json.str("name"),
            mode = if (json.str("mode") == MODE_VPN) MODE_VPN else MODE_BYPASS,
            provider = json.bool("provider", true),
            packages = json.listOfStrings("packages").toMutableList(),
            sites = json.listOfStrings("sites").toMutableList(),
        )
    }

    fun toJson(): JSONObject = obj(
        "id" to id,
        "name" to name,
        "mode" to mode,
        "provider" to provider,
        "packages" to arr(packages),
        "sites" to arr(sites),
    )

    val isVpnOnly: Boolean get() = mode == MODE_VPN
}

/**
 * User settings, persisted as settings.json — the same keys and the same defaults as the
 * desktop's nimbus/settings.py, minus what a phone has no use for (a local SOCKS/HTTP proxy,
 * environment variables, the tray) and plus the Android ones (per-app routing, boot start).
 *
 * A settings.json copied from a computer loads as it is: unknown keys are ignored, as desktop
 * ignores the mobile ones.
 */
data class Settings(
    // -- Connection ---------------------------------------------------------------------
    /** On a phone everything is a VPN; the key stays so the file matches the desktop's. */
    var tunnelMode: String = "tun",
    /** Always-on VPN + "block networks without VPN" in Android's own settings; see the UI. */
    var killSwitch: Boolean = false,
    var tunMtu: Int = 1500,
    /** Per-app routing: the phone's version of a preset's program list. */
    var perAppEnabled: Boolean = false,
    var perAppMode: String = RoutePreset.MODE_VPN,
    var perAppPackages: MutableList<String> = mutableListOf(),

    // -- Routing ------------------------------------------------------------------------
    var routingMode: String = "global",           // global | bypass_ru
    var useProviderRouting: Boolean = true,
    var blockAds: Boolean = true,
    var bypassPrivate: Boolean = true,
    var domainStrategy: String = "AsIs",          // AsIs | IPIfNonMatch | IPOnDemand
    var routePresets: MutableList<RoutePreset> = mutableListOf(),
    var routePreset: String = "",

    // -- DNS ----------------------------------------------------------------------------
    var remoteDns: String = "https://1.1.1.1/dns-query",
    var directDns: String = "8.8.8.8",
    var fakeDns: Boolean = true,

    // -- Advanced -----------------------------------------------------------------------
    var sniffing: Boolean = true,
    var mux: Boolean = false,
    var muxConcurrency: Int = 8,
    var fragment: Boolean = false,
    var fragmentPackets: String = "tlshello",
    var fragmentLength: String = "100-200",
    var fragmentInterval: String = "10-20",
    var logLevel: String = "warning",             // none | error | warning | info | debug

    // -- Subscriptions ------------------------------------------------------------------
    /** Panels send full Xray JSON configs to this user agent. */
    var subUserAgent: String = "Happ/3.0.0",
    var subAutoUpdate: Boolean = true,
    var subUpdateHours: Int = 2,
    var updateOnLaunch: Boolean = true,
    var hwidMode: String = "auto",                // auto | always | never
    var hwidCustom: String = "",
    var hwidDevice: String = "",

    // -- Ping ---------------------------------------------------------------------------
    var pingUrl: String = "https://www.gstatic.com/generate_204",
    var pingTimeout: Int = 5,

    // -- App ----------------------------------------------------------------------------
    var autoConnect: Boolean = false,
    var autostart: Boolean = false,               // connect after a reboot
    var balancerNotify: Boolean = true,
    var updateCheck: Boolean = true,
    var updateSkip: String = "",
    var theme: String = "dark",                   // dark | light | auto
    var language: String = "auto",                // auto | ru | en
    var selectedServer: String = "",
    var recentServers: MutableList<String> = mutableListOf(),
    var sortMode: String = "source",              // source | ping | name
    var groupSort: MutableMap<String, String> = mutableMapOf(),
    var settingsVersion: Int = SETTINGS_VERSION,
) {
    companion object {
        const val SETTINGS_VERSION = 4

        fun load(): Settings {
            val text = Paths.readOrNull(Paths.settingsFile) ?: return Settings()
            val data = parseObject(text) ?: return Settings()
            return fromJson(data)
        }

        /**
         * Settings from a JSON object, with the desktop's migrations applied and unknown keys
         * ignored — so a file from another version, or from the computer, still loads.
         */
        fun fromJson(data: JSONObject): Settings {
            val json = JSONObject(data.toString())
            val version = json.int("settings_version", 1)
            if (version < 3) {
                // 0.1.1 forced everything through the VPN and 0.1.2 recommended turning the
                // provider's routing off. It isn't better: Russian sites went abroad and back.
                json.put("use_provider_routing", true)
            }
            if (version < 4) {
                // FakeDNS became the default in 4: an earlier "off" was only the old default.
                json.put("fake_dns", true)
            }
            val settings = Settings(
                tunnelMode = json.str("tunnel_mode", "tun").ifEmpty { "tun" },
                killSwitch = json.bool("kill_switch"),
                tunMtu = json.int("tun_mtu", 1500),
                perAppEnabled = json.bool("per_app_enabled"),
                perAppMode = json.str("per_app_mode", RoutePreset.MODE_VPN),
                perAppPackages = json.list("per_app_packages"),
                routingMode = json.str("routing_mode", "global"),
                useProviderRouting = json.bool("use_provider_routing", true),
                blockAds = json.bool("block_ads", true),
                bypassPrivate = json.bool("bypass_private", true),
                domainStrategy = json.str("domain_strategy", "AsIs"),
                routePresets = json.presets("route_presets"),
                routePreset = json.str("route_preset"),
                remoteDns = json.str("remote_dns", "https://1.1.1.1/dns-query"),
                directDns = json.str("direct_dns", "8.8.8.8"),
                fakeDns = json.bool("fake_dns", true),
                sniffing = json.bool("sniffing", true),
                mux = json.bool("mux"),
                muxConcurrency = json.int("mux_concurrency", 8),
                fragment = json.bool("fragment"),
                fragmentPackets = json.str("fragment_packets", "tlshello"),
                fragmentLength = json.str("fragment_length", "100-200"),
                fragmentInterval = json.str("fragment_interval", "10-20"),
                logLevel = json.str("log_level", "warning"),
                subUserAgent = json.str("sub_user_agent", "Happ/3.0.0"),
                subAutoUpdate = json.bool("sub_auto_update", true),
                subUpdateHours = json.int("sub_update_hours", 2),
                updateOnLaunch = json.bool("update_on_launch", true),
                hwidMode = json.str("hwid_mode", "auto"),
                hwidCustom = json.str("hwid_custom"),
                hwidDevice = json.str("hwid_device"),
                pingUrl = json.str("ping_url", "https://www.gstatic.com/generate_204"),
                pingTimeout = json.int("ping_timeout", 5),
                autoConnect = json.bool("auto_connect"),
                autostart = json.bool("autostart"),
                balancerNotify = json.bool("balancer_notify", true),
                updateCheck = json.bool("update_check", true),
                updateSkip = json.str("update_skip"),
                theme = json.str("theme", "dark"),
                language = json.str("language", "auto"),
                selectedServer = json.str("selected_server"),
                recentServers = json.list("recent_servers"),
                sortMode = json.str("sort_mode", "source"),
                groupSort = json.stringMap("group_sort"),
                settingsVersion = SETTINGS_VERSION,
            )
            return settings
        }

        private fun JSONObject.list(key: String): MutableList<String> =
            listOfStrings(opt(key)).toMutableList()

        private fun JSONObject.presets(key: String): MutableList<RoutePreset> {
            val out = mutableListOf<RoutePreset>()
            val array = opt(key) as? JSONArray ?: return out
            for (i in 0 until array.length()) {
                val item = array.optJSONObject(i) ?: continue
                val preset = RoutePreset.fromJson(item)
                if (preset.id.isNotEmpty()) out.add(preset)
            }
            return out
        }

        private fun JSONObject.stringMap(key: String): MutableMap<String, String> {
            val out = mutableMapOf<String, String>()
            val json = opt(key) as? JSONObject ?: return out
            for (name in json.keys()) out[name] = json.opt(name)?.toString() ?: ""
            return out
        }
    }

    fun save() {
        Paths.writeAtomic(Paths.settingsFile, toJson().toString(2))
    }

    fun toJson(): JSONObject = obj(
        "tunnel_mode" to tunnelMode,
        "kill_switch" to killSwitch,
        "tun_mtu" to tunMtu,
        "per_app_enabled" to perAppEnabled,
        "per_app_mode" to perAppMode,
        "per_app_packages" to arr(perAppPackages),
        "routing_mode" to routingMode,
        "use_provider_routing" to useProviderRouting,
        "block_ads" to blockAds,
        "bypass_private" to bypassPrivate,
        "domain_strategy" to domainStrategy,
        "route_presets" to arr(routePresets.map { it.toJson() }),
        "route_preset" to routePreset,
        "remote_dns" to remoteDns,
        "direct_dns" to directDns,
        "fake_dns" to fakeDns,
        "sniffing" to sniffing,
        "mux" to mux,
        "mux_concurrency" to muxConcurrency,
        "fragment" to fragment,
        "fragment_packets" to fragmentPackets,
        "fragment_length" to fragmentLength,
        "fragment_interval" to fragmentInterval,
        "log_level" to logLevel,
        "sub_user_agent" to subUserAgent,
        "sub_auto_update" to subAutoUpdate,
        "sub_update_hours" to subUpdateHours,
        "update_on_launch" to updateOnLaunch,
        "hwid_mode" to hwidMode,
        "hwid_custom" to hwidCustom,
        "hwid_device" to hwidDevice,
        "ping_url" to pingUrl,
        "ping_timeout" to pingTimeout,
        "auto_connect" to autoConnect,
        "autostart" to autostart,
        "balancer_notify" to balancerNotify,
        "update_check" to updateCheck,
        "update_skip" to updateSkip,
        "theme" to theme,
        "language" to language,
        "selected_server" to selectedServer,
        "recent_servers" to arr(recentServers),
        "sort_mode" to sortMode,
        "group_sort" to JSONObject(groupSort as Map<*, *>),
        "settings_version" to settingsVersion,
    )

    val activePreset: RoutePreset?
        get() = if (routePreset.isEmpty()) null else routePresets.firstOrNull { it.id == routePreset }
}
