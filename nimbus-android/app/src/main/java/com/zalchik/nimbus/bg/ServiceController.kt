package com.zalchik.nimbus.bg

import android.content.Context
import android.content.Intent
import android.net.VpnService
import androidx.core.content.ContextCompat
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

/** What a connection is doing right now, as the core reports it. */
data class ServiceState(
    val status: ServiceStatus = ServiceStatus.Stopped,
    /** The server or balancer the tunnel runs; "" when nothing is selected. */
    val selectedId: String = "",
    val selectedName: String = "",
    /** The last error the core or the build reported, or null while everything works. */
    val error: String? = null,
    /** Uplink/downlink bytes per second, and totals since the tunnel came up. */
    val uplink: Long = 0,
    val downlink: Long = 0,
    val uplinkTotal: Long = 0,
    val downlinkTotal: Long = 0,
    val trafficAvailable: Boolean = false,
    val connections: Int = 0,
    /** Node tag -> delay in ms, from the core's own urltest checks. */
    val delays: Map<String, Int> = emptyMap(),
    /** The node the core is on now, for a balancer. */
    val currentNode: String? = null,
    /** The provider's own groups (a full config can carry a selector), tag -> items. */
    val groups: List<GroupState> = emptyList(),
    val startedAt: Long = 0,
)

enum class ServiceStatus { Stopped, Starting, Started, Stopping }

data class GroupState(
    val tag: String,
    val type: String,
    val selectable: Boolean,
    val selected: String,
    val items: List<GroupItem>,
)

data class GroupItem(val tag: String, val type: String, val delay: Int, val testedAt: Long)

/**
 * The one place the service and the UI meet: the service writes what the core reports, the
 * screens read it. A phone has one tunnel and one process, so there's no binder here — the flows
 * are the interface, and the notification, the tile and the screens all observe the same state.
 */
object ServiceController {
    const val ACTION_CLOSE = "com.zalchik.nimbus.CLOSE"
    const val ACTION_RELOAD = "com.zalchik.nimbus.RELOAD"

    private val _state = MutableStateFlow(ServiceState())
    val state: StateFlow<ServiceState> = _state

    private val _logs = MutableStateFlow<List<LogLine>>(emptyList())
    val logs: StateFlow<List<LogLine>> = _logs

    /** What the user asked for; survives a reconnect and a reboot (see [BootReceiver]). */
    @Volatile
    var requestedByUser: Boolean = false

    val status: ServiceStatus get() = _state.value.status

    fun update(transform: (ServiceState) -> ServiceState) {
        _state.value = transform(_state.value)
    }

    fun setStatus(status: ServiceStatus) = update { it.copy(status = status) }

    fun setError(message: String?) = update { it.copy(error = message) }

    fun appendLogs(lines: List<LogLine>) {
        if (lines.isEmpty()) return
        val merged = (_logs.value + lines)
        _logs.value = if (merged.size > MAX_LOGS) merged.subList(merged.size - MAX_LOGS, merged.size) else merged
    }

    fun clearLogs() {
        _logs.value = emptyList()
    }

    private const val MAX_LOGS = 500

    /** Ask Android for the VPN permission; null means it's already granted. */
    fun prepare(context: Context): Intent? = VpnService.prepare(context)

    fun start(context: Context, selectedId: String) {
        requestedByUser = true
        val intent = Intent(context, NimbusVpnService::class.java)
            .setAction(NimbusVpnService.ACTION_START)
            .putExtra(NimbusVpnService.EXTRA_SELECTED, selectedId)
        ContextCompat.startForegroundService(context, intent)
    }

    fun stop(context: Context) {
        requestedByUser = false
        context.sendBroadcast(
            Intent(ACTION_CLOSE).setPackage(context.packageName)
        )
    }

    /** Rebuild the config from the current settings and hand it to the running core. */
    fun reload(context: Context) {
        context.sendBroadcast(
            Intent(ACTION_RELOAD).setPackage(context.packageName)
        )
    }
}

data class LogLine(val level: Int, val message: String, val time: Long = System.currentTimeMillis())
