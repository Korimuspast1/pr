package com.zalchik.nimbus.bg

import android.content.Context
import android.content.Intent
import android.net.ConnectivityManager
import android.net.ProxyInfo
import android.net.VpnService
import android.os.Build
import android.os.IBinder
import android.os.ParcelFileDescriptor
import android.util.Log
import com.zalchik.nimbus.i18n.I18n
import io.nekohasekai.libbox.Libbox
import io.nekohasekai.libbox.Notification
import io.nekohasekai.libbox.TunOptions
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withContext

/**
 * The VPN Android sees. It owns the tunnel's file descriptor and the platform half of libbox;
 * [BoxService] owns the core's life cycle and the state the UI reads.
 *
 * `openTun` is the whole of Android's VPN API: the core describes the tunnel it wants (addresses,
 * MTU, routes, which apps are in it) and this translates it into a [VpnService.Builder]. What
 * comes back is a file descriptor the core reads and writes packets on — no root, no iptables,
 * which is why the desktop's TUN and system-proxy modes collapse into one here.
 */
class NimbusVpnService : VpnService() {
    companion object {
        private const val TAG = "NimbusVpnService"
        const val ACTION_START = "com.zalchik.nimbus.START"
        const val EXTRA_SELECTED = "selected"
    }

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private lateinit var platformInterface: PlatformInterfaceImpl
    private lateinit var boxService: BoxService
    private var commandClient: ServiceCommandClient? = null

    var fileDescriptor: ParcelFileDescriptor? = null
        private set

    /** Set by [BoxService] before the core starts; the tunnel is built to match it. */
    var killSwitch: Boolean = false

    val connectivity: ConnectivityManager
        get() = getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager

    override fun onCreate() {
        super.onCreate()
        platformInterface = PlatformInterfaceImpl(this, connectivity)
        boxService = BoxService(this, platformInterface)
        ServiceNotification.createChannels(this)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int =
        boxService.onStartCommand(intent)

    override fun onBind(intent: Intent): IBinder? = super.onBind(intent)

    override fun onDestroy() {
        boxService.onDestroy()
        scope.cancel()
        super.onDestroy()
    }

    override fun onRevoke() {
        runBlocking {
            withContext(Dispatchers.Main) { boxService.onRevoke() }
        }
    }

    // -- the tunnel ------------------------------------------------------------------------------

    /** Build the tunnel the core asked for and hand it the file descriptor. */
    fun openTun(options: TunOptions): Int {
        if (prepare(this) != null) error("android: missing vpn permission")

        val builder = Builder()
            .setSession(I18n.tr("Nimbus"))
            .setMtu(options.mtu)

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            builder.setMetered(false)
        }
        // The kill switch on Android is the system's own: Always-on VPN plus "block connections
        // without VPN". setBlocking is what this app can do from here, and it is exactly that.
        builder.setBlocking(killSwitch)

        var address = options.inet4Address
        while (address.hasNext()) {
            val prefix = address.next()
            builder.addAddress(prefix.address(), prefix.prefix())
        }
        address = options.inet6Address
        while (address.hasNext()) {
            val prefix = address.next()
            builder.addAddress(prefix.address(), prefix.prefix())
        }

        if (options.autoRoute) {
            if (options.dnsMode.unwrap != Libbox.DNSModeDisabled) {
                val dnsServers = options.dnsServerAddress
                while (dnsServers.hasNext()) {
                    builder.addDnsServer(dnsServers.next())
                }
            }

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                val routeAddress = options.inet4RouteAddress
                if (routeAddress.hasNext()) {
                    while (routeAddress.hasNext()) builder.addRoute(routeAddress.next().toIpPrefix())
                } else {
                    builder.addRoute("0.0.0.0", 0)
                }
                val routeAddress6 = options.inet6RouteAddress
                if (routeAddress6.hasNext()) {
                    while (routeAddress6.hasNext()) builder.addRoute(routeAddress6.next().toIpPrefix())
                }
                val excludeAddress = options.inet4RouteExcludeAddress
                while (excludeAddress.hasNext()) builder.excludeRoute(excludeAddress.next().toIpPrefix())
                val excludeAddress6 = options.inet6RouteExcludeAddress
                while (excludeAddress6.hasNext()) builder.excludeRoute(excludeAddress6.next().toIpPrefix())
            } else {
                val routeRange = options.inet4RouteRange
                while (routeRange.hasNext()) {
                    val prefix = routeRange.next()
                    builder.addRoute(prefix.address(), prefix.prefix())
                }
                val routeRange6 = options.inet6RouteRange
                while (routeRange6.hasNext()) {
                    val prefix = routeRange6.next()
                    builder.addRoute(prefix.address(), prefix.prefix())
                }
            }

            val includePackage = options.includePackage
            while (includePackage.hasNext()) {
                val packageName = includePackage.next()
                runCatching { builder.addAllowedApplication(packageName) }
                    .onFailure { Log.e(TAG, "addAllowedApplication $packageName", it) }
            }
            val excludePackage = options.excludePackage
            while (excludePackage.hasNext()) {
                val packageName = excludePackage.next()
                runCatching { builder.addDisallowedApplication(packageName) }
                    .onFailure { Log.e(TAG, "addDisallowedApplication $packageName", it) }
            }
        }

        // A system proxy makes no sense next to a tunnel, but the core offers one and some
        // providers' configs ask for it; Android can set it from API 29.
        if (options.isHTTPProxyEnabled && Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            builder.setHttpProxy(
                ProxyInfo.buildDirectProxy(
                    options.httpProxyServer,
                    options.httpProxyServerPort,
                    options.httpProxyBypassDomain.toList(),
                )
            )
        }

        val descriptor = builder.establish() ?: error("android: the application is not prepared or is revoked")
        fileDescriptor = descriptor
        return descriptor.fd
    }

    fun closeTun() {
        val descriptor = fileDescriptor ?: return
        fileDescriptor = null
        runCatching { descriptor.close() }
    }

    /** Let the core keep its own sockets out of the tunnel. */
    fun protectSocket(fd: Int): Boolean = protect(fd)

    // -- the core's reports ----------------------------------------------------------------------

    fun startCommandClient(notification: ServiceNotification, name: () -> String) {
        if (commandClient != null) return
        commandClient = ServiceCommandClient(notification, name).also { created ->
            ServiceBridge.client = created
            created.connect()
        }
    }

    fun stopCommandClient() {
        ServiceBridge.client = null
        commandClient?.disconnect()
        commandClient = null
    }

    fun sendCoreNotification(notification: Notification) {
        scope.launch(Dispatchers.Main) { ServiceNotification(this@NimbusVpnService).event(notification) }
    }

    fun cancelCoreNotification(identifier: String, typeID: Int) {
        scope.launch(Dispatchers.Main) {
            ServiceNotification(this@NimbusVpnService).cancelEvent(identifier, typeID)
        }
    }
}
