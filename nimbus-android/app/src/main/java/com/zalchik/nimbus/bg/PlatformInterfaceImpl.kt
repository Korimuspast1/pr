package com.zalchik.nimbus.bg

import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.os.Build
import android.os.Process
import android.system.OsConstants
import android.util.Log
import androidx.annotation.RequiresApi
import io.nekohasekai.libbox.BridgeOptions
import io.nekohasekai.libbox.BridgeSession
import io.nekohasekai.libbox.ConnectionOwner
import io.nekohasekai.libbox.InterfaceUpdateListener
import io.nekohasekai.libbox.Libbox
import io.nekohasekai.libbox.LocalDNSTransport
import io.nekohasekai.libbox.NeighborUpdateListener
import io.nekohasekai.libbox.NetworkInterfaceIterator
import io.nekohasekai.libbox.Notification
import io.nekohasekai.libbox.PlatformInterface
import io.nekohasekai.libbox.PlatformUser
import io.nekohasekai.libbox.ShellSession
import io.nekohasekai.libbox.StringIterator
import io.nekohasekai.libbox.TunOptions
import io.nekohasekai.libbox.WIFIState
import java.net.InetSocketAddress
import java.net.NetworkInterface
import io.nekohasekai.libbox.NetworkInterface as LibboxNetworkInterface

/**
 * What the sing-box core asks the phone for: the tunnel's file descriptor, the interface it must
 * leave through, whose packet this is, and how to resolve a name locally.
 *
 * This is the whole surface libbox generates for Android, so most of it is stubs — a phone has no
 * root bridge, no shell session, no Tailscale hostname, no neighbour table. What matters is
 * [openTun], [autoDetectInterfaceControl], [getInterfaces], [findConnectionOwner] and
 * [localDNSTransport]; the rest exist so the core's calls don't crash it.
 */
class PlatformInterfaceImpl(
    private val service: NimbusVpnService,
    private val connectivity: ConnectivityManager,
) : PlatformInterface {
    companion object {
        private const val TAG = "PlatformInterface"
    }

    // -- the tunnel ---------------------------------------------------------------------------

    override fun usePlatformAutoDetectInterfaceControl(): Boolean = true

    /** Keep the core's own sockets out of the tunnel: without this, it routes to itself. */
    override fun autoDetectInterfaceControl(fd: Int) {
        service.protect(fd)
    }

    override fun openTun(options: TunOptions): Int = service.openTun(options)

    override fun registerMyInterface(name: String?) {
        // The core names its own interface; Android's VpnService decides the name, so there is
        // nothing to register.
    }

    // -- networks ------------------------------------------------------------------------------

    override fun startDefaultInterfaceMonitor(listener: InterfaceUpdateListener) {
        DefaultNetworkMonitor.setListener(listener)
    }

    override fun closeDefaultInterfaceMonitor(listener: InterfaceUpdateListener) {
        DefaultNetworkMonitor.setListener(null)
    }

    override fun getInterfaces(): NetworkInterfaceIterator {
        val networks = connectivity.allNetworks
        val systemInterfaces = NetworkInterface.getNetworkInterfaces().toList()
        val interfaces = mutableListOf<LibboxNetworkInterface>()
        for (network in networks) {
            val linkProperties = connectivity.getLinkProperties(network) ?: continue
            val capabilities = connectivity.getNetworkCapabilities(network) ?: continue
            val name = linkProperties.interfaceName ?: continue
            val systemInterface = systemInterfaces.find { it.name == name } ?: continue

            val item = LibboxNetworkInterface()
            item.name = name
            item.index = systemInterface.index
            item.dnsServer = StringArray(linkProperties.dnsServers.mapNotNull { it.hostAddress }.iterator())
            item.gateway = StringArray(
                linkProperties.routes
                    .filter { it.destination != null && it.destination.prefixLength == 0 }
                    .mapNotNull { it.gateway }
                    .filterNot { it.isAnyLocalAddress }
                    .mapNotNull { it.hostAddress }
                    .iterator()
            )
            item.type = when {
                capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) -> Libbox.InterfaceTypeWIFI
                capabilities.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) -> Libbox.InterfaceTypeCellular
                capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) -> Libbox.InterfaceTypeEthernet
                else -> Libbox.InterfaceTypeOther
            }
            runCatching { item.mtu = systemInterface.mtu }
                .onFailure { Log.e(TAG, "mtu of $name", it) }
            item.addresses = StringArray(
                systemInterface.interfaceAddresses.mapNotNull { address ->
                    val host = address.address?.hostAddress ?: return@mapNotNull null
                    "${host.trim('[', ']')}/${address.networkPrefixLength}"
                }.iterator()
            )
            var flags = 0
            if (capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)) {
                flags = OsConstants.IFF_UP or OsConstants.IFF_RUNNING
            }
            if (systemInterface.isLoopback) flags = flags or OsConstants.IFF_LOOPBACK
            if (systemInterface.isPointToPoint) flags = flags or OsConstants.IFF_POINTOPOINT
            if (systemInterface.supportsMulticast()) flags = flags or OsConstants.IFF_MULTICAST
            item.flags = flags
            item.metered = !capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_METERED)
            interfaces.add(item)
        }
        return InterfaceArray(interfaces.iterator())
    }

    override fun includeAllNetworks(): Boolean = false

    override fun underNetworkExtension(): Boolean = false

    @RequiresApi(Build.VERSION_CODES.Q)
    override fun findConnectionOwner(
        ipProtocol: Int,
        sourceAddress: String,
        sourcePort: Int,
        destinationAddress: String,
        destinationPort: Int,
    ): ConnectionOwner {
        // Per-app routing needs to know whose packet this is; Android answers from API 29 up.
        val uid = connectivity.getConnectionOwnerUid(
            ipProtocol,
            InetSocketAddress(sourceAddress, sourcePort),
            InetSocketAddress(destinationAddress, destinationPort),
        )
        if (uid == Process.INVALID_UID) error("android: connection owner not found")
        val packages = service.packageManager.getPackagesForUid(uid)
        val owner = ConnectionOwner()
        owner.userId = uid
        owner.userName = packages?.firstOrNull() ?: ""
        owner.setAndroidPackageNames(StringArray((packages?.toList() ?: emptyList<String>()).iterator()))
        return owner
    }

    override fun clearDNSCache() {
        // Android keeps no resolver cache of its own that an app may drop.
    }

    override fun readWIFIState(): WIFIState? = null

    override fun localDNSTransport(): LocalDNSTransport = LocalResolver

    // -- notifications -------------------------------------------------------------------------

    override fun sendNotification(notification: Notification) = service.sendCoreNotification(notification)

    override fun cancelNotification(identifier: String, typeID: Int) =
        service.cancelCoreNotification(identifier, typeID)

    // -- features a phone doesn't have ---------------------------------------------------------

    override fun startNeighborMonitor(listener: NeighborUpdateListener) {
        // find_neighbor is off in every config Nimbus builds.
    }

    override fun closeNeighborMonitor(listener: NeighborUpdateListener) {
    }

    override fun usePlatformBridge(): Boolean = false

    override fun createBridge(options: BridgeOptions): BridgeSession =
        throw UnsupportedOperationException("android: no root bridge")

    override fun usePlatformShell(): Boolean = false

    override fun checkPlatformShell() {
        throw UnsupportedOperationException("android: no shell")
    }

    override fun openShellSession(
        user: PlatformUser?,
        command: String?,
        environ: StringIterator?,
        term: String?,
        rows: Int,
        cols: Int,
    ): ShellSession = throw UnsupportedOperationException("android: no shell")

    override fun lookupUser(username: String?): PlatformUser =
        throw UnsupportedOperationException("android: no users")

    override fun lookupSFTPServer(): String = ""

    override fun readSystemSSHHostKey(): String = ""

    override fun tailscaleHostname(): String = ""
}
