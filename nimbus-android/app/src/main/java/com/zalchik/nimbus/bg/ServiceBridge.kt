package com.zalchik.nimbus.bg

/**
 * How the rest of the app reaches the running core: the service owns the command client, and
 * everything else asks through here. A phone has one tunnel in one process, so a plain reference
 * is enough — no binder, no broadcast, and calls made while nothing is running are simply
 * dropped (the UI shows the tunnel as stopped anyway).
 */
object ServiceBridge {
    @Volatile
    var client: ServiceCommandClient? = null
        internal set

    /** Switch a group's node: a Nimbus balancer, or a provider's own selector. */
    fun selectOutbound(groupTag: String, outboundTag: String) {
        client?.selectOutbound(groupTag, outboundTag)
    }

    /** Measure a group's nodes again now, instead of waiting for the core's interval. */
    fun urlTest(groupTag: String) {
        client?.urlTest(groupTag)
    }

    fun clearLogs() {
        client?.resetLogs()
    }

    /** Drop every connection, so a new one goes through the node picked since (exit IP screen). */
    fun closeConnections() {
        client?.closeConnections()
    }
}
