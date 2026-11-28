package com.zalchik.nimbus.engine

import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.objOrNull
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.str
import org.json.JSONObject
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLSocket

/**
 * Latency: how long a server takes to answer.
 *
 * Desktop measures through a temporary Xray with an HTTP inbound per outbound, so the number
 * includes a real handshake with the protocol. A phone has no spare core to spend on that, and
 * the numbers it shows come from two places instead:
 *
 *  - **while connected**, the running sing-box measures its own urltest group and libbox reports
 *    each node's delay over the command client ([Balancers] turns those tags back into servers);
 *  - **while disconnected**, [probe] dials the server directly: a TCP connect, plus a TLS
 *    handshake for TLS/Reality nodes, which is the part of the delay that actually differs
 *    between providers.
 *
 * A probe is not the same number as a real request — it misses the protocol handshake — but it
 * ranks servers the same way, which is what the list and the balancer editor need.
 */
object Ping {
    /** Probes at once: each one is a TLS handshake, and dozens at a time peg a phone's CPU. */
    const val WORKERS = 10

    const val UNREACHABLE = -1

    private val TLS_PROTOCOLS = setOf("tls", "reality", "xtls")

    private val context: SSLContext by lazy {
        SSLContext.getInstance("TLS").apply { init(null, null, null) }
    }

    /**
     * Milliseconds to reach [address] ("host:port"), or [UNREACHABLE]. With TLS the handshake is
     * measured too; a certificate the phone doesn't trust still counts as a reply, since what
     * matters here is how far away the server is, not whether it's who it says it is.
     */
    fun probe(address: String, timeoutMs: Int, tls: Boolean = false, serverName: String = ""): Int {
        if (address.isEmpty()) return UNREACHABLE
        val host = address.substringBeforeLast(':').trim('[', ']')
        val port = address.substringAfterLast(':', "").toIntOrNull() ?: return UNREACHABLE
        if (host.isEmpty() || port <= 0) return UNREACHABLE
        return try {
            val start = System.nanoTime()
            if (tls) {
                val socket = context.socketFactory.createSocket() as SSLSocket
                socket.use {
                    it.soTimeout = timeoutMs
                    it.connect(InetSocketAddress(host, port), timeoutMs)
                    // No SNI and no verification: some servers answer a probe handshake only.
                    it.enabledProtocols = arrayOf("TLSv1.2", "TLSv1.3")
                    if (serverName.isNotEmpty()) {
                        val params = it.sslParameters
                        params.serverNames = listOf(javax.net.ssl.SNIHostName(serverName))
                        params.endpointIdentificationAlgorithm = null
                        it.sslParameters = params
                    }
                    it.startHandshake()
                }
            } else {
                Socket().use {
                    it.soTimeout = timeoutMs
                    it.connect(InetSocketAddress(host, port), timeoutMs)
                }
            }
            ((System.nanoTime() - start) / 1_000_000).toInt()
        } catch (e: Exception) {
            UNREACHABLE
        }
    }

    /** What to dial for a server, and whether the protocol expects TLS on that socket. */
    data class Target(val address: String, val tls: Boolean, val serverName: String)

    fun targetOf(server: Server): Target? {
        val address = ConfigBuilder.probeAddresses(server).firstOrNull() ?: return null
        return Target(address, tlsOf(server), serverNameOf(server))
    }

    private fun tlsOf(server: Server): Boolean {
        val outbound = firstOutbound(server) ?: return false
        val stream = outbound.objOrNull("streamSettings") ?: JSONObject()
        if (stream.str("security").lowercase() in TLS_PROTOCOLS) return true
        // sing-box-shaped outbounds (a balancer member kept from a link) name it in tls.enabled.
        return outbound.objOrNull("tls")?.let { it.opt("enabled") == true } ?: false
    }

    private fun serverNameOf(server: Server): String {
        val outbound = firstOutbound(server) ?: return ""
        val stream = outbound.objOrNull("streamSettings") ?: JSONObject()
        for (key in listOf("tlsSettings", "realitySettings")) {
            val name = stream.objOrNull(key)?.str("serverName") ?: ""
            if (name.isNotEmpty()) return name
        }
        return outbound.objOrNull("tls")?.str("server_name") ?: ""
    }

    private fun firstOutbound(server: Server): JSONObject? = when {
        server.isFullConfig -> server.config?.arrOrEmpty("outbounds")?.objects()
            ?.firstOrNull { it.str("protocol") !in setOf("freedom", "blackhole", "dns", "loopback") }

        else -> try {
            Links.parseLink(server.link).outbound
        } catch (e: LinkError) {
            null
        }
    }

    /** A latency, once: two tries, since a handshake can stall on a busy radio. */
    fun measure(server: Server, timeoutMs: Int): Int {
        val target = targetOf(server) ?: return UNREACHABLE
        repeat(2) {
            val ms = probe(target.address, timeoutMs, target.tls, target.serverName)
            if (ms >= 0) return ms
        }
        return UNREACHABLE
    }

    /**
     * server id -> latency in ms (-1 = unreachable), measured off the VPN.
     * [onResult] is called from worker threads as results come in, so the list fills in.
     */
    fun pingServers(
        servers: List<Server>,
        timeoutMs: Int,
        onResult: ((String, Int) -> Unit)? = null,
    ): Map<String, Int> {
        val result = servers.associate { it.id to UNREACHABLE }.toMutableMap()
        if (servers.isEmpty()) return result
        val pool = Executors.newFixedThreadPool(minOf(WORKERS, servers.size))
        try {
            val futures = servers.map { server ->
                pool.submit<Int> {
                    val ms = measure(server, timeoutMs)
                    synchronized(result) { result[server.id] = ms }
                    onResult?.invoke(server.id, ms)
                    ms
                }
            }
            for (future in futures) {
                try {
                    future.get(timeoutMs.toLong() * 4 + 2000, TimeUnit.MILLISECONDS)
                } catch (e: Exception) {
                    // A probe that never returns leaves -1 behind, which is what it means.
                }
            }
        } finally {
            pool.shutdownNow()
        }
        return result
    }

    /** The best of a balancer's nodes, for the row in the server list. */
    fun bestOf(delays: List<Int?>): Int? = delays.filterNotNull().filter { it >= 0 }.minOrNull()

    /** "123 ms" / "—" as the list shows it. */
    fun format(ms: Int?): String = when {
        ms == null -> "…"
        ms < 0 -> "—"
        else -> "$ms ms"
    }
}
