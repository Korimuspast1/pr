package com.zalchik.nimbus.ui.state

import android.app.Application
import android.content.Context
import android.content.Intent
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.zalchik.nimbus.bg.LogLine
import com.zalchik.nimbus.bg.ServiceBridge
import com.zalchik.nimbus.bg.ServiceController
import com.zalchik.nimbus.bg.ServiceStatus
import com.zalchik.nimbus.data.Balancer
import com.zalchik.nimbus.data.Group
import com.zalchik.nimbus.data.Paths
import com.zalchik.nimbus.data.RoutePreset
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.data.Store
import com.zalchik.nimbus.data.newId
import com.zalchik.nimbus.engine.Balancers
import com.zalchik.nimbus.engine.ConfigBuilder
import com.zalchik.nimbus.engine.Deeplink
import com.zalchik.nimbus.engine.ExitIp
import com.zalchik.nimbus.engine.Links
import com.zalchik.nimbus.engine.Ping
import com.zalchik.nimbus.engine.Selected
import com.zalchik.nimbus.engine.Share
import com.zalchik.nimbus.engine.Subscription
import com.zalchik.nimbus.engine.Update
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.util.parseObject
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** A message the UI shows as a snackbar; `error` paints it red. */
data class Notice(val text: String, val error: Boolean = false, val id: Long = System.nanoTime())

/** What the exit-IP row shows. */
data class ExitState(val status: String = "", val ip: String = "", val country: String? = null)

/**
 * Everything the screens read and everything they can do — the phone's counterpart of the
 * desktop's controller.py.
 *
 * The tunnel itself lives in [ServiceController] and the service; this holds the data (servers,
 * subscriptions, balancers, settings), the work that touches the network (updates, latency, exit
 * IP, app updates), and the notices the UI shows. Screens observe it as Compose state, so a
 * finished download or a changed setting repaints the list without any signal plumbing.
 */
class AppState(application: Application) : AndroidViewModel(application) {
    private val context: Context get() = getApplication()

    var store by mutableStateOf(Store.load())
        private set
    var settings by mutableStateOf(Settings.load())
        private set

    /** "" = nothing happening, else what the busy row says. */
    var busy by mutableStateOf("")
        private set
    var exit by mutableStateOf(ExitState())
        private set
    var update by mutableStateOf<Update.Release?>(null)
        private set
    var updateError by mutableStateOf("")
        private set

    /** Latency of servers measured off the VPN, id -> ms (-1 unreachable). */
    val offlineDelays = MutableStateFlow<Map<String, Int>>(emptyMap())

    private val notices = MutableStateFlow<List<Notice>>(emptyList())
    val notice: StateFlow<Notice?> = notices.stateIn(viewModelScope, SharingStarted.Eagerly, null)

    val service: StateFlow<com.zalchik.nimbus.bg.ServiceState> = ServiceController.state
    val logs: StateFlow<List<LogLine>> = ServiceController.logs

    private var pingJob: Job? = null
    private var exitJob: Job? = null
    private var autoUpdateJob: Job? = null

    init {
        I18n.setLanguage(I18n.resolve(settings.language))
        if (settings.autoConnect && settings.selectedServer.isNotEmpty() &&
            ServiceController.status == ServiceStatus.Stopped
        ) {
            connect(settings.selectedServer)
        }
        if (settings.updateOnLaunch) updateSubscriptions(onlyDue = true)
        if (settings.updateCheck) checkUpdate(manual = false)
        startAutoUpdates()
    }

    // -- notices --------------------------------------------------------------------------------

    fun say(text: String, error: Boolean = false) {
        notices.value = listOf(Notice(text, error))
        viewModelScope.launch {
            kotlinx.coroutines.delay(4000)
            notices.value = emptyList()
        }
    }

    fun dismissNotice() {
        notices.value = emptyList()
    }

    // -- persistence ----------------------------------------------------------------------------

    private fun saveStore() {
        store.save()
        // Reassign so Compose sees a new value; the lists inside are mutable.
        store = store.copy()
    }

    fun saveSettings(transform: (Settings) -> Unit) {
        val current = settings
        transform(current)
        current.save()
        settings = current.copy()
        if (current.language != I18n.lang) I18n.setLanguage(I18n.resolve(current.language))
    }

    fun reload() {
        store = Store.load()
        settings = Settings.load()
    }

    // -- selection and the tunnel ----------------------------------------------------------------

    /** The server or balancer the tunnel runs (or would run) on. */
    fun selected(): Selected? {
        val id = settings.selectedServer
        if (id.isEmpty()) return null
        store.balancer(id)?.let { balancer ->
            val members = store.members(balancer)
            if (members.isNotEmpty()) return Selected.OfBalancer(balancer, members)
        }
        store.server(id)?.let { return Selected.OfServer(it) }
        return null
    }

    fun select(id: String, connect: Boolean = false) {
        saveSettings { it.selectedServer = id }
        store.pushRecent(settings, id)
        saveStore()
        if (connect) connect(id) else if (ServiceController.status == ServiceStatus.Started) reloadService()
    }

    /** Android's "allow Nimbus to set up a VPN?" prompt, when it hasn't been answered yet. */
    var permissionIntent by mutableStateOf<Intent?>(null)
        private set
    private var pendingId = ""

    fun connect(id: String = settings.selectedServer) {
        if (id.isEmpty()) {
            say(I18n.tr("Сервер не выбран"), error = true)
            return
        }
        saveSettings { it.selectedServer = id }
        val permission = ServiceController.prepare(context)
        if (permission != null) {
            // First time on this device: ask, and start once the answer arrives.
            pendingId = id
            permissionIntent = permission
            return
        }
        startTunnel(id)
    }

    /** The activity finished Android's VPN prompt: start the tunnel if it was allowed. */
    fun onPermissionResult(granted: Boolean) {
        val id = pendingId
        permissionIntent = null
        pendingId = ""
        if (!granted) {
            if (id.isNotEmpty()) say(I18n.tr("Без разрешения VPN подключиться нельзя"), error = true)
            return
        }
        if (id.isNotEmpty()) startTunnel(id)
    }

    private fun startTunnel(id: String) {
        ServiceController.start(context, id)
        exit = ExitState("checking")
        scheduleExitCheck()
    }

    fun disconnect() {
        ServiceController.stop(context)
        exit = ExitState()
    }

    fun toggle() {
        when (ServiceController.status) {
            ServiceStatus.Started -> disconnect()
            ServiceStatus.Stopped -> connect()
            else -> Unit
        }
    }

    /** Rebuild the config from the current settings and hand it to the running core. */
    fun reloadService() {
        if (ServiceController.status == ServiceStatus.Stopped) return
        ServiceController.reload(context)
        scheduleExitCheck()
    }

    // -- exit IP ----------------------------------------------------------------------------------

    private fun scheduleExitCheck(delayMs: Long = 1500) {
        exitJob?.cancel()
        exitJob = viewModelScope.launch {
            kotlinx.coroutines.delay(delayMs)
            checkExitIp()
        }
    }

    fun checkExitIp(manual: Boolean = false) {
        if (ServiceController.status != ServiceStatus.Started) {
            exit = ExitState()
            return
        }
        exit = exit.copy(status = "checking")
        exitJob?.cancel()
        exitJob = viewModelScope.launch(Dispatchers.IO) {
            val state = try {
                val answer = ExitIp.lookup()
                ExitState("ok", answer.ip, answer.country)
            } catch (e: Exception) {
                if (manual) say(e.message ?: I18n.tr("Не удалось узнать адрес"), error = true)
                ExitState("fail")
            }
            withContext(Dispatchers.Main) { exit = state }
        }
    }

    // -- importing --------------------------------------------------------------------------------

    /**
     * Add whatever `text` holds: a deep link (including Happ's encrypted ones), a subscription
     * URL, share links, or an Xray JSON config — the desktop's import_text and deeplink handling
     * in one entry point, because on a phone they all arrive as text.
     */
    fun import(text: String, groupId: String = "", onDone: ((Int) -> Unit)? = null) {
        val payload = text.trim()
        if (payload.isEmpty()) {
            say(I18n.tr("Пусто"), error = true)
            return
        }
        viewModelScope.launch(Dispatchers.IO) {
            busy = I18n.tr("Импорт…")
            val added = try {
                importNow(payload, groupId)
            } catch (e: Exception) {
                withContext(Dispatchers.Main) { say(e.message ?: I18n.tr("Не удалось импортировать"), error = true) }
                0
            } finally {
                withContext(Dispatchers.Main) { busy = "" }
            }
            withContext(Dispatchers.Main) { onDone?.invoke(added) }
        }
    }

    private suspend fun importNow(payload: String, groupId: String): Int {
        val deep = Deeplink.parse(payload)
        when (deep?.kind) {
            "subscription" -> {
                val count = addSubscription(deep.payload, deep.name)
                return count
            }

            "configs" -> return importConfigs(deep.payload, groupId)
            "crypt" -> {
                withContext(Dispatchers.Main) { say(deep.payload, error = true) }
                return 0
            }

            "invalid" -> {
                withContext(Dispatchers.Main) { say(I18n.tr("Это не ссылка и не конфиг"), error = true) }
                return 0
            }
        }
        // Not a deep link: a URL, a block of links, or a config.
        if (payload.startsWith("http://", true) || payload.startsWith("https://", true)) {
            if (payload.lineSequence().count() == 1 && !Links.isLink(payload)) {
                return addSubscription(payload, "")
            }
        }
        return importConfigs(payload, groupId)
    }

    /** Share links or Xray JSON, into [groupId] ("": the plain list). */
    private fun importConfigs(text: String, groupId: String): Int {
        val parsed = Subscription.parseBody(text, groupId)
        if (parsed.servers.isEmpty()) {
            val message = parsed.errors.joinToString("; ").ifEmpty { I18n.tr("Серверы не найдены") }
            viewModelScope.launch(Dispatchers.Main) { say(message, error = true) }
            return 0
        }
        for (server in parsed.servers) {
            if (groupId.isNotEmpty() && store.group(groupId) == null) {
                store.groups.add(Group(groupId, I18n.tr("Добавленные")))
            }
            server.subId = groupId
            store.servers.add(server)
        }
        saveStore()
        viewModelScope.launch(Dispatchers.Main) {
            say(I18n.plural(parsed.servers.size, "Добавлен {n} сервер", "Добавлено {n} сервера", "Добавлено {n} серверов")
                .replace("{n}", parsed.servers.size.toString()))
        }
        return parsed.servers.size
    }

    // -- subscriptions ----------------------------------------------------------------------------

    /** Add a subscription by URL and fetch it; returns how many servers it brought. */
    fun addSubscription(url: String, name: String = ""): Int {
        val existing = store.subscriptions.firstOrNull { it.url == url }
        if (existing != null) {
            updateSubscription(existing.id)
            return store.serversOf(existing.id).size
        }
        val subscription = com.zalchik.nimbus.data.Subscription(
            id = newId(),
            url = url,
            name = name,
        )
        store.subscriptions.add(subscription)
        saveStore()
        return fetchSubscription(subscription)
    }

    /** Download a subscription and replace its servers; returns how many it has now. */
    fun fetchSubscription(subscription: com.zalchik.nimbus.data.Subscription): Int {
        var count = 0
        viewModelScope.launch(Dispatchers.IO) {
            withContext(Dispatchers.Main) { busy = I18n.tr("Обновление подписки…") }
            try {
                val servers = Subscription.fetch(
                    subscription,
                    settings.subUserAgent,
                    settings.hwidMode,
                    settings.hwidCustom,
                    settings.hwidDevice,
                )
                store.replaceServers(subscription.id, servers)
                store.touch(subscription)
                subscription.error = ""
                saveStore()
                count = servers.size
                withContext(Dispatchers.Main) {
                    busy = ""
                    say(
                        I18n.tr("Подписка обновлена: {n}", values = mapOf("n" to servers.size.toString()))
                    )
                }
            } catch (e: Exception) {
                subscription.error = e.message ?: ""
                saveStore()
                withContext(Dispatchers.Main) {
                    busy = ""
                    say(e.message ?: I18n.tr("Не удалось обновить подписку"), error = true)
                }
            }
        }
        return count
    }

    fun updateSubscription(subId: String) {
        val subscription = store.subscription(subId) ?: return
        fetchSubscription(subscription)
    }

    /** Update everything, or only what's due (`profile-update-interval` / the setting). */
    fun updateSubscriptions(onlyDue: Boolean = false, subIds: List<String>? = null) {
        val due = store.subscriptions.filter { subscription ->
            if (subIds != null && subscription.id !in subIds) return@filter false
            if (!onlyDue) return@filter true
            if (!settings.subAutoUpdate) return@filter false
            val hours = if (subscription.updateHours > 0) subscription.updateHours else settings.subUpdateHours
            if (hours <= 0) return@filter false
            val age = System.currentTimeMillis() / 1000 - subscription.lastUpdated
            age >= hours * 3600
        }
        if (due.isEmpty()) return
        viewModelScope.launch(Dispatchers.IO) {
            for (subscription in due) {
                try {
                    val servers = withContext(Dispatchers.IO) {
                        Subscription.fetch(
                            subscription,
                            settings.subUserAgent,
                            settings.hwidMode,
                            settings.hwidCustom,
                            settings.hwidDevice,
                        )
                    }
                    store.replaceServers(subscription.id, servers)
                    store.touch(subscription)
                    subscription.error = ""
                } catch (e: Exception) {
                    subscription.error = e.message ?: ""
                }
            }
            saveStore()
            withContext(Dispatchers.Main) {
                val failed = due.count { it.error.isNotEmpty() }
                if (failed == 0) {
                    say(I18n.tr("Подписки обновлены"))
                } else {
                    say(I18n.plural(failed, "{n} подписка не обновилась", "{n} подписки не обновились", "{n} подписок не обновились")
                        .replace("{n}", failed.toString()), error = true)
                }
            }
        }
    }

    /** Re-check the subscriptions that are due, every so often while the app is open. */
    private fun startAutoUpdates() {
        autoUpdateJob?.cancel()
        autoUpdateJob = viewModelScope.launch {
            while (true) {
                kotlinx.coroutines.delay(15 * 60 * 1000L)
                if (settings.subAutoUpdate) updateSubscriptions(onlyDue = true)
            }
        }
    }

    fun removeSubscription(subId: String) {
        val was = settings.selectedServer
        store.removeSubscription(subId)
        saveStore()
        if (store.servers.none { it.id == was }) saveSettings { it.selectedServer = "" }
    }

    fun renameSubscription(subId: String, name: String) {
        store.subscription(subId)?.customName = name
        saveStore()
    }

    fun setSubscriptionAgent(subId: String, userAgent: String) {
        store.subscription(subId)?.userAgent = userAgent
        saveStore()
    }

    fun setSubscriptionHwid(subId: String, send: Boolean) {
        store.subscription(subId)?.sendHwid = send
        saveStore()
    }

    // -- servers, groups, balancers ---------------------------------------------------------------

    fun removeServer(serverId: String) {
        val was = settings.selectedServer
        store.servers.removeAll { it.id == serverId }
        store.balancers.forEach { it.serverIds.remove(serverId) }
        saveStore()
        if (was == serverId) saveSettings { it.selectedServer = "" }
    }

    fun renameServer(serverId: String, name: String) {
        store.server(serverId)?.name = name
        saveStore()
    }

    fun addGroup(name: String): Group {
        val group = Group(newId(), name)
        store.groups.add(group)
        saveStore()
        return group
    }

    fun renameGroup(groupId: String, name: String) {
        store.group(groupId)?.name = name
        saveStore()
    }

    fun removeGroup(groupId: String) {
        val was = settings.selectedServer
        store.removeGroup(groupId)
        saveStore()
        if (store.servers.none { it.id == was }) saveSettings { it.selectedServer = "" }
    }

    fun moveServer(serverId: String, groupId: String) {
        store.server(serverId)?.subId = groupId
        saveStore()
    }

    fun addBalancer(name: String, serverIds: List<String>, subIds: List<String>, mode: String, interval: Int): Balancer {
        val balancer = Balancer(
            id = newId(),
            name = name,
            serverIds = serverIds.toMutableList(),
            subIds = subIds.toMutableList(),
            interval = interval,
            mode = mode,
        )
        store.balancers.add(balancer)
        saveStore()
        return balancer
    }

    fun updateBalancer(balancer: Balancer) {
        val index = store.balancers.indexOfFirst { it.id == balancer.id }
        if (index >= 0) store.balancers[index] = balancer else store.balancers.add(balancer)
        saveStore()
    }

    fun removeBalancer(balancerId: String) {
        val was = settings.selectedServer
        store.balancers.removeAll { it.id == balancerId }
        saveStore()
        if (was == balancerId) saveSettings { it.selectedServer = "" }
    }

    // -- latency ----------------------------------------------------------------------------------

    /** Measure servers directly (off the VPN); results land in [offlineDelays] as they come in. */
    fun ping(servers: List<Server>? = null) {
        pingJob?.cancel()
        val targets = servers ?: store.servers
        if (targets.isEmpty()) return
        pingJob = viewModelScope.launch(Dispatchers.IO) {
            withContext(Dispatchers.Main) { busy = I18n.tr("Проверка задержки…") }
            val timeout = (settings.pingTimeout.coerceIn(1, 30)) * 1000
            val results = Ping.pingServers(targets, timeout) { id, ms ->
                offlineDelays.value = offlineDelays.value + (id to ms)
                store.server(id)?.latency = ms
            }
            offlineDelays.value = results
            for ((id, ms) in results) store.server(id)?.latency = ms
            saveStore()
            withContext(Dispatchers.Main) { busy = "" }
        }
    }

    /** The delay to show for a server: the core's own number while connected, else the probe's. */
    fun delayOf(server: Server): Int? {
        val live = liveDelays[server.id]
        if (live != null) return live
        val offline = offlineDelays.value[server.id]
        if (offline != null) return offline
        return server.latency
    }

    /** Live delays for balancer members, mapped from the core's node tags back to servers. */
    private val liveDelays: Map<String, Int>
        get() {
            val state = ServiceController.state.value
            if (state.delays.isEmpty()) return emptyMap()
            val out = LinkedHashMap<String, Int>()
            for (balancer in store.balancers) {
                val members = store.members(balancer)
                for ((index, member) in members.withIndex()) {
                    val best = state.delays.entries
                        .filter { it.key.startsWith("${Balancers.PREFIX}$index-") && it.value > 0 }
                        .minOfOrNull { it.value }
                    if (best != null) out[member.id] = best
                }
            }
            // A provider's own group: its items are the config's node tags.
            return out
        }

    /** Ask the core to measure its balancer group again now. */
    fun retestBalancer() {
        val group = ServiceController.state.value.groups.firstOrNull { it.tag == Balancers.TAG } ?: return
        testGroup(group.tag)
    }

    /**
     * Measure any group the core reports, instead of waiting for its interval. It works for a
     * urltest group and for a selector one alike: sing-box tests the members of whatever group it
     * is given, and stores the delays the node list reads.
     */
    fun testGroup(groupTag: String) {
        ServiceBridge.urlTest(groupTag)
        say(I18n.tr("Задержка проверяется…"))
    }

    /**
     * Switch a group's node. Only a provider's own `selector` group takes one: sing-box answers a
     * node choice for a selector and refuses a urltest group, which keeps picking the fastest node
     * itself — that is what a Nimbus balancer is, so the node list says so instead of offering it.
     */
    fun selectNode(groupTag: String, nodeTag: String) {
        ServiceBridge.selectOutbound(groupTag, nodeTag)
    }

    // -- sharing ----------------------------------------------------------------------------------

    fun shareLink(server: Server): String? = Share.serverLink(server)

    fun shareJson(server: Server): String = Share.exportJson(listOf(server), settings)

    fun shareSubscriptionLink(subscription: com.zalchik.nimbus.data.Subscription): String = subscription.url

    /** The text to put on the clipboard or in a share sheet for a whole subscription. */
    fun exportLinks(servers: List<Server>): Share.Exported = Share.exportLinks(servers)

    // -- presets and per-app ----------------------------------------------------------------------

    fun addPreset(preset: RoutePreset) {
        saveSettings { it.routePresets.add(preset) }
        reloadService()
    }

    fun updatePreset(preset: RoutePreset) {
        saveSettings { current ->
            val index = current.routePresets.indexOfFirst { it.id == preset.id }
            if (index >= 0) current.routePresets[index] = preset else current.routePresets.add(preset)
        }
        reloadService()
    }

    fun removePreset(presetId: String) {
        saveSettings {
            it.routePresets.removeAll { preset -> preset.id == presetId }
            if (it.routePreset == presetId) it.routePreset = ""
        }
        reloadService()
    }

    fun applyPreset(presetId: String) {
        saveSettings { it.routePreset = presetId }
        reloadService()
    }

    fun setRoutingMode(mode: String) {
        saveSettings { it.routingMode = mode }
        reloadService()
    }

    fun setProviderRouting(enabled: Boolean) {
        saveSettings { it.useProviderRouting = enabled }
        reloadService()
    }

    // -- app updates ------------------------------------------------------------------------------

    fun checkUpdate(manual: Boolean) {
        viewModelScope.launch(Dispatchers.IO) {
            if (manual) withContext(Dispatchers.Main) { busy = I18n.tr("Проверка обновлений…") }
            try {
                val release = Update.latest()
                val newer = release?.takeIf { Update.isNewer(it.version) && it.version != settings.updateSkip }
                withContext(Dispatchers.Main) {
                    update = newer
                    updateError = ""
                    if (manual) {
                        say(
                            if (newer != null) I18n.tr("Есть новая версия: {version}", values = mapOf("version" to newer.version))
                            else I18n.tr("Обновлений нет")
                        )
                    }
                }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    updateError = e.message ?: ""
                    if (manual) say(e.message ?: I18n.tr("Не удалось проверить обновления"), error = true)
                }
            } finally {
                withContext(Dispatchers.Main) { if (manual) busy = "" }
            }
        }
    }

    fun skipUpdate(version: String) {
        saveSettings { it.updateSkip = version }
        update = null
    }

    /** Download the APK and hand it to the installer; [onProgress] gets (done, total) bytes. */
    fun downloadUpdate(release: Update.Release, onProgress: (Long, Long) -> Unit, onDone: (java.io.File) -> Unit) {
        viewModelScope.launch(Dispatchers.IO) {
            withContext(Dispatchers.Main) { busy = I18n.tr("Скачивание обновления…") }
            try {
                val abi = android.os.Build.SUPPORTED_ABIS.firstOrNull() ?: "universal"
                val file = Update.download(release, abi, progress = { done, total ->
                    viewModelScope.launch(Dispatchers.Main) { onProgress(done, total) }
                })
                withContext(Dispatchers.Main) {
                    busy = ""
                    onDone(file)
                }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    busy = ""
                    say(e.message ?: I18n.tr("Не удалось скачать обновление"), error = true)
                }
            }
        }
    }

    // -- diagnostics ------------------------------------------------------------------------------

    /** The config that would run right now, for "send to support" and the diagnostics screen. */
    fun currentConfig(): String {
        val selected = selected() ?: return ""
        val built = ConfigBuilder.build(selected, settings, ipv6 = false)
        return ConfigBuilder.dump(built.config)
    }

    fun unsupportedOf(server: Server): String? = Links.unsupportedReason(server)

    fun clearLogs() {
        ServiceController.clearLogs()
    }

    /** What the about screen shows: app, core and data-folder versions. */
    fun versions(): Map<String, String> = linkedMapOf(
        I18n.tr("Версия") to Paths.VERSION,
        "sing-box" to com.zalchik.nimbus.BuildConfig.SINGBOX_VERSION,
        I18n.tr("Папка данных") to Paths.dataDir.path,
    )

    /** A server by id, or a balancer's members — what the home list asks for. */
    fun serverOrBalancer(id: String): Any? = store.server(id) ?: store.balancer(id)

    /** Whether the selected item can run on a phone at all (XHTTP/KCP links can't). */
    fun selectionUnsupported(): String? {
        val selected = selected() ?: return null
        return when (selected) {
            is Selected.OfServer -> Links.unsupportedReason(selected.server)
            is Selected.OfBalancer -> {
                val reasons = selected.members.mapNotNull { Links.unsupportedReason(it) }
                if (reasons.size == selected.members.size && reasons.isNotEmpty()) reasons.first() else null
            }
        }
    }

    /** Parse a config file the user opened from another app. */
    fun importFileText(text: String, groupId: String = "") = import(text, groupId)

    /** The subscription a server belongs to, for the list's section headers. */
    fun ownerOf(server: Server): String {
        store.subscription(server.subId)?.let { return it.title }
        store.group(server.subId)?.let { return it.name }
        return ""
    }

    /** "nimbus://import/<url>" for a subscription, to share it with another device. */
    fun deeplinkFor(url: String): String = "nimbus://import/$url"

    /** A config's own JSON, for the "look at what runs" screen. */
    fun configOf(server: Server): String =
        server.config?.toString(2) ?: parseObject("{}")?.toString() ?: ""
}
