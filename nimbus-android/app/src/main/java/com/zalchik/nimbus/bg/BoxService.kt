package com.zalchik.nimbus.bg

import android.app.Service
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.net.ConnectivityManager
import android.os.Build
import android.os.PowerManager
import android.util.Log
import androidx.core.content.ContextCompat
import com.zalchik.nimbus.data.Paths
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.data.Store
import com.zalchik.nimbus.engine.ConfigBuilder
import com.zalchik.nimbus.engine.Selected
import com.zalchik.nimbus.i18n.I18n
import io.nekohasekai.libbox.CommandServer
import io.nekohasekai.libbox.CommandServerHandler
import io.nekohasekai.libbox.OverrideOptions
import io.nekohasekai.libbox.SystemProxyStatus
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withContext

/**
 * The tunnel's life cycle: build the config for what the user selected, hand it to the sing-box
 * core, keep the notification and the state the UI reads in step with it, and tear it all down.
 *
 * Desktop's controller.py does this around two child processes (sing-box in front, Xray behind a
 * SOCKS bridge); a phone has one core in-process, so what's left is the ordering that matters:
 * the command server comes up before the service, a reload replaces the config without dropping
 * the tunnel, and any failure is reported through the same state the screens watch — a VPN that
 * dies silently is worse than one that says why.
 */
class BoxService(
    private val service: NimbusVpnService,
    private val platformInterface: PlatformInterfaceImpl,
) : CommandServerHandler {
    companion object {
        private const val TAG = "BoxService"
    }

    private val scope: CoroutineScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val notification = ServiceNotification(service)
    private var commandServer: CommandServer? = null
    private var receiverRegistered = false
    private var job: Job? = null

    /** The config that's running, so a reload can tell whether anything changed. */
    private var runningConfig = ""
    private var runningName = ""

    private val receiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context, intent: Intent) {
            when (intent.action) {
                ServiceController.ACTION_CLOSE -> stop()
                ServiceController.ACTION_RELOAD -> reload()
                Intent.ACTION_SCREEN_ON -> commandServer?.wakeNow()
                Intent.ACTION_SCREEN_OFF -> commandServer?.pause()
                PowerManager.ACTION_DEVICE_IDLE_MODE_CHANGED -> updateIdleMode()
            }
        }
    }

    // -- lifecycle ------------------------------------------------------------------------------

    fun onStartCommand(intent: Intent?): Int {
        registerReceiver()
        val selected = intent?.getStringExtra(NimbusVpnService.EXTRA_SELECTED)
            ?.takeIf { it.isNotEmpty() }
        if (ServiceController.status == ServiceStatus.Started && selected == null) {
            // Android restarts a foreground service after a process death: the tunnel is either
            // still up or the core is gone; a reload settles it either way.
            reload()
            return Service.START_STICKY
        }
        ServiceController.setStatus(ServiceStatus.Starting)
        job?.cancel()
        job = scope.launch { startTunnel(selected) }
        return Service.START_STICKY
    }

    private fun registerReceiver() {
        if (receiverRegistered) return
        val filter = IntentFilter().apply {
            addAction(ServiceController.ACTION_CLOSE)
            addAction(ServiceController.ACTION_RELOAD)
            addAction(Intent.ACTION_SCREEN_ON)
            addAction(Intent.ACTION_SCREEN_OFF)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                addAction(PowerManager.ACTION_DEVICE_IDLE_MODE_CHANGED)
            }
        }
        ContextCompat.registerReceiver(service, receiver, filter, ContextCompat.RECEIVER_NOT_EXPORTED)
        receiverRegistered = true
    }

    /** Build the config for the current selection and start (or replace) the tunnel with it. */
    private fun startTunnel(selectedId: String?) {
        val settings = Settings.load()
        val store = Store.load()
        val id = selectedId ?: settings.selectedServer
        val selected = selectionOf(store, settings, id)
        if (selected == null) {
            fail(I18n.tr("Сервер не выбран"))
            return
        }
        ServiceController.update { it.copy(selectedId = selected.id, selectedName = selected.name) }
        notification.show(selected.name, I18n.tr("Подключение…"))
        // The tunnel is built to match this, so it has to be set before the core asks for it.
        service.killSwitch = settings.killSwitch

        val built = try {
            ConfigBuilder.build(selected, settings, hasIpv6())
        } catch (e: Exception) {
            Log.e(TAG, "build config", e)
            fail(e.message ?: I18n.tr("Не удалось собрать конфигурацию"))
            return
        }
        if (built.unsupported.isNotEmpty()) {
            Log.w(TAG, "unsupported: ${built.unsupported}")
        }
        val content = ConfigBuilder.dump(built.config)
        writeDump(content)

        try {
            val server = commandServer ?: CommandServer(this, platformInterface).also { created ->
                created.start()
                commandServer = created
            }
            DefaultNetworkMonitor.start(service.connectivity)
            server.startOrReloadService(content, overrideOptions(settings))
            runningConfig = content
            runningName = selected.name
        } catch (e: Exception) {
            Log.e(TAG, "start service", e)
            fail(describe(e))
            return
        }

        ServiceController.update { state ->
            state.copy(
                status = ServiceStatus.Started,
                error = null,
                selectedId = selected.id,
                selectedName = selected.name,
                startedAt = System.currentTimeMillis(),
            )
        }
        notification.show(selected.name, I18n.tr("Подключено"))
        service.startCommandClient(notification) { runningName }
    }

    /** Rebuild from the current settings and hand the new config to the running core. */
    fun reload() {
        job?.cancel()
        job = scope.launch {
            if (commandServer == null) {
                startTunnel(null)
                return@launch
            }
            val settings = Settings.load()
            val store = Store.load()
            val selected = selectionOf(store, settings, settings.selectedServer)
            if (selected == null) {
                fail(I18n.tr("Сервер не выбран"))
                return@launch
            }
            val built = try {
                ConfigBuilder.build(selected, settings, hasIpv6())
            } catch (e: Exception) {
                fail(e.message ?: I18n.tr("Не удалось собрать конфигурацию"))
                return@launch
            }
            val content = ConfigBuilder.dump(built.config)
            service.killSwitch = settings.killSwitch
            if (content == runningConfig) return@launch   // nothing changed: leave the tunnel alone
            writeDump(content)
            try {
                commandServer!!.startOrReloadService(content, overrideOptions(settings))
                runningConfig = content
                runningName = selected.name
                ServiceController.update {
                    it.copy(selectedId = selected.id, selectedName = selected.name, error = null)
                }
                notification.show(selected.name, I18n.tr("Подключено"))
            } catch (e: Exception) {
                fail(describe(e))
            }
        }
    }

    fun stop() {
        ServiceController.setStatus(ServiceStatus.Stopping)
        scope.launch {
            service.stopCommandClient()
            runCatching { commandServer?.closeService() }
                .onFailure { commandServer?.setError("android: close service: ${it.message}") }
            runCatching { commandServer?.close() }
            commandServer = null
            runningConfig = ""
            service.closeTun()
            DefaultNetworkMonitor.stop()
            withContext(Dispatchers.Main) {
                notification.close()
                ServiceController.update {
                    it.copy(
                        status = ServiceStatus.Stopped,
                        uplink = 0,
                        downlink = 0,
                        uplinkTotal = 0,
                        downlinkTotal = 0,
                        trafficAvailable = false,
                        connections = 0,
                        delays = emptyMap(),
                        currentNode = null,
                        groups = emptyList(),
                    )
                }
                service.stopSelf()
            }
        }
    }

    fun onDestroy() {
        if (receiverRegistered) {
            runCatching { service.unregisterReceiver(receiver) }
            receiverRegistered = false
        }
        service.stopCommandClient()
        runCatching { commandServer?.closeService() }
        runCatching { commandServer?.close() }
        commandServer = null
        service.closeTun()
        DefaultNetworkMonitor.stop()
        notification.close()
        scope.cancel()
    }

    fun onRevoke() {
        // Android took the VPN away (the user turned it off in system settings).
        ServiceController.requestedByUser = false
        stop()
    }

    // -- what the core asks for ------------------------------------------------------------------

    override fun serviceStop() {
        ServiceController.setStatus(ServiceStatus.Stopping)
        service.closeTun()
        runCatching { commandServer?.closeService() }
    }

    override fun serviceReload() {
        runBlocking { reload() }
    }

    override fun getSystemProxyStatus(): SystemProxyStatus? = null

    override fun setSystemProxyEnabled(enabled: Boolean) {
        // A phone has no system proxy to set: everything goes through the tunnel.
    }

    override fun connectSSHAgent(): Int = -1

    override fun triggerNativeCrash() {
        Thread {
            Thread.sleep(200)
            throw RuntimeException("debug native crash")
        }.start()
    }

    override fun writeDebugMessage(message: String?) {
        Log.d(TAG, message ?: "")
    }

    // -- helpers --------------------------------------------------------------------------------

    /** What to run: the balancer if that's what's selected, otherwise the server. */
    private fun selectionOf(store: Store, settings: Settings, id: String): Selected? {
        if (id.isEmpty()) return null
        store.balancer(id)?.let { balancer ->
            val members = store.members(balancer)
            if (members.isEmpty()) return null
            return Selected.OfBalancer(balancer, members)
        }
        store.server(id)?.let { return Selected.OfServer(it) }
        return null
    }

    /** Per-app routing: Android applies it to the tunnel itself, not to routing rules. */
    private fun overrideOptions(settings: Settings): OverrideOptions = OverrideOptions().apply {
        autoRedirect = false
        if (settings.perAppEnabled && settings.perAppPackages.isNotEmpty()) {
            val own = service.packageName
            if (settings.perAppMode == com.zalchik.nimbus.data.RoutePreset.MODE_VPN) {
                includePackage = StringArray((settings.perAppPackages + own).iterator())
            } else {
                excludePackage = StringArray((settings.perAppPackages - own).iterator())
            }
        }
    }

    /** IPv6 only when the network the phone is on actually has it. */
    private fun hasIpv6(): Boolean = try {
        val manager = service.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        val network = manager.activeNetwork ?: return false
        manager.getLinkProperties(network)?.let { link ->
            link.linkAddresses.any { address ->
                address.address is java.net.Inet6Address && !address.address.isLinkLocalAddress
            }
        } ?: false
    } catch (e: Exception) {
        false
    }

    private fun updateIdleMode() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M) return
        val server = commandServer ?: return
        val powerManager = service.getSystemService(Context.POWER_SERVICE) as? PowerManager ?: return
        if (powerManager.isDeviceIdleMode) server.pause() else server.wake()
    }

    private fun fail(message: String) {
        Log.e(TAG, message)
        ServiceController.update { it.copy(status = ServiceStatus.Stopped, error = message) }
        notification.show(runningName, message)
        scope.launch {
            withContext(Dispatchers.Main) {
                // Keep the notification up for a moment so the reason is readable, then stop.
                kotlinx.coroutines.delay(6000)
                notification.close()
                service.stopSelf()
                ServiceController.setStatus(ServiceStatus.Stopped)
            }
        }
    }

    /** libbox wraps Go errors; the useful text is in the decoded message when there is one. */
    private fun describe(error: Exception): String {
        val message = error.message ?: return error.javaClass.simpleName
        return message
    }

    private fun writeDump(content: String) {
        try {
            Paths.writeAtomic(Paths.configDump, content)
        } catch (e: Exception) {
            Log.w(TAG, "config dump", e)
        }
    }
}
