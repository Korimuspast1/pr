package com.zalchik.nimbus.bg

import android.util.Log
import com.zalchik.nimbus.engine.Balancers
import io.nekohasekai.libbox.CommandClient
import io.nekohasekai.libbox.CommandClientHandler
import io.nekohasekai.libbox.CommandClientOptions
import io.nekohasekai.libbox.ConnectionEvents
import io.nekohasekai.libbox.Libbox
import io.nekohasekai.libbox.LogIterator
import io.nekohasekai.libbox.OutboundGroupItemIterator
import io.nekohasekai.libbox.OutboundGroupIterator
import io.nekohasekai.libbox.StatusMessage
import io.nekohasekai.libbox.StringIterator

/**
 * What the running core reports: log lines, traffic counters, and the state of every outbound
 * group — which for a Nimbus balancer means each member's last measured delay and the node the
 * core is on now.
 *
 * This is how the phone knows a balancer's latencies without probing them itself: sing-box runs
 * its urltest checks in the background and libbox hands the numbers over, so the list shows real
 * delays for as long as the tunnel is up. Provider groups (a selector inside a full config) come
 * through the same channel, and [selectOutbound] is what the node switcher calls.
 *
 * The epoch guard is here because the callbacks arrive from Go threads while connect/disconnect
 * run on another: a late callback from a dropped client must not overwrite the new one's state.
 */
class ServiceCommandClient(
    private val notification: ServiceNotification,
    private val name: () -> String,
) : CommandClientHandler {
    companion object {
        private const val TAG = "ServiceCommandClient"

        /** Nanoseconds; the core multiplies it by the interval it reports status at. */
        private const val STATUS_INTERVAL = 1_000_000_000L
    }

    private val lock = Any()
    private var epoch = 0
    private var client: CommandClient? = null

    fun connect() {
        val current: CommandClient?
        val generation: Int
        synchronized(lock) {
            generation = ++epoch
            current = client
            client = null
        }
        if (current != null) {
            runCatching { current.disconnect() }
            ServiceController.update { it.copy(trafficAvailable = false) }
        }
        Thread {
            val options = CommandClientOptions().apply {
                statusInterval = STATUS_INTERVAL
                addCommand(Libbox.CommandLog)
                addCommand(Libbox.CommandStatus)
                addCommand(Libbox.CommandGroup)
                addCommand(Libbox.CommandOutbounds)
            }
            val created = try {
                CommandClient(this, options)
            } catch (e: Exception) {
                Log.e(TAG, "new command client", e)
                return@Thread
            }
            try {
                created.connect()
            } catch (e: Exception) {
                Log.e(TAG, "connect", e)
                return@Thread
            }
            synchronized(lock) {
                if (generation == epoch) client = created else runCatching { created.disconnect() }
            }
        }.start()
    }

    fun disconnect() {
        val current: CommandClient?
        synchronized(lock) {
            epoch++
            current = client
            client = null
        }
        if (current != null) {
            Thread { runCatching { current.disconnect() } }.start()
        }
    }

    fun selectOutbound(groupTag: String, outboundTag: String) {
        val current = synchronized(lock) { client } ?: return
        try {
            current.selectOutbound(groupTag, outboundTag)
        } catch (e: Exception) {
            Log.e(TAG, "selectOutbound $groupTag/$outboundTag", e)
        }
    }

    /** Measure a group's nodes again now instead of waiting for the interval. */
    fun urlTest(groupTag: String) {
        val current = synchronized(lock) { client } ?: return
        try {
            current.urlTest(groupTag)
        } catch (e: Exception) {
            Log.e(TAG, "urlTest $groupTag", e)
        }
    }

    /** Drop what's on screen and ask the core to drop what it buffers. */
    fun resetLogs() {
        ServiceController.clearLogs()
        val current = synchronized(lock) { client } ?: return
        runCatching { current.clearLogs() }
    }

    fun closeConnections() {
        val current = synchronized(lock) { client } ?: return
        runCatching { current.closeConnections() }
    }

    // -- what the core sends ---------------------------------------------------------------------

    override fun connected() {
        Log.d(TAG, "command client connected")
    }

    override fun disconnected(message: String?) {
        Log.d(TAG, "command client disconnected: $message")
        ServiceController.update { it.copy(trafficAvailable = false) }
    }

    override fun clearLogs() {
        ServiceController.clearLogs()
    }

    override fun writeLogs(messageList: LogIterator) {
        val lines = messageList.toList().map { entry ->
            LogLine(entry.level, entry.message)
        }
        ServiceController.appendLogs(lines)
    }

    override fun writeStatus(message: StatusMessage) {
        ServiceController.update {
            it.copy(
                uplink = message.uplink,
                downlink = message.downlink,
                uplinkTotal = message.uplinkTotal,
                downlinkTotal = message.downlinkTotal,
                trafficAvailable = message.trafficAvailable,
                connections = message.connectionsIn + message.connectionsOut,
            )
        }
        notification.updateTraffic(message.uplink, message.downlink, name())
    }

    override fun writeGroups(message: OutboundGroupIterator) {
        val groups = mutableListOf<GroupState>()
        val delays = LinkedHashMap<String, Int>()
        var current: String? = null
        while (message.hasNext()) {
            val group = message.next()
            val items = mutableListOf<GroupItem>()
            val iterator = group.items
            while (iterator.hasNext()) {
                val item = iterator.next()
                items.add(GroupItem(item.tag, item.type, item.urlTestDelay, item.urlTestTime))
                if (item.urlTestDelay > 0) delays[item.tag] = item.urlTestDelay
            }
            groups.add(GroupState(group.tag, group.type, group.selectable, group.selected, items))
            if (group.tag == Balancers.TAG) {
                current = group.selected
                // A urltest group reports the node it settled on; the delays say how each did.
                val fastest = Balancers.fastest(delays.filterKeys { it.startsWith(Balancers.PREFIX) })
                if (current.isNullOrEmpty()) current = fastest
            }
        }
        ServiceController.update { it.copy(groups = groups, delays = delays, currentNode = current) }
    }

    override fun writeOutbounds(message: OutboundGroupItemIterator) {
        // The flat outbound list; the groups above already carry what the UI shows.
    }

    override fun setDefaultLogLevel(level: Int) {
    }

    override fun initializeClashMode(modeList: StringIterator?, currentMode: String?) {
        // Nimbus has no clash modes: routing is expressed in rules, not in a mode switch.
    }

    override fun updateClashMode(newMode: String?) {
    }

    override fun writeConnectionEvents(events: ConnectionEvents?) {
    }
}
