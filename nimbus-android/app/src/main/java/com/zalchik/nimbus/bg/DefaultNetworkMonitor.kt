package com.zalchik.nimbus.bg

import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.util.Log
import io.nekohasekai.libbox.InterfaceUpdateListener
import java.net.NetworkInterface

/**
 * Which network the phone is on, and telling the core when that changes — Wi-Fi to mobile and
 * back, which is the everyday case a desktop never meets.
 *
 * The core needs it for two things: `auto_detect_interface` in the config (its own sockets must
 * leave through the real network, not the tunnel), and the DNS it hands to [LocalResolver]. A
 * missed update shows up as a dead tunnel that only a reconnect fixes, so the callback is
 * registered for as long as the service runs and the interface is looked up again if the first
 * try comes back empty (Android publishes the network a moment before its link properties).
 *
 * `registerDefaultNetworkCallback` needs API 24, which is this app's minimum, so there is no
 * per-version registration dance here.
 */
object DefaultNetworkMonitor {
    private const val TAG = "DefaultNetworkMonitor"

    @Volatile
    var defaultNetwork: Network? = null
        private set

    @Volatile
    private var listener: InterfaceUpdateListener? = null

    private var registered = false
    private var connectivity: ConnectivityManager? = null

    private val callback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) {
            defaultNetwork = network
            checkDefaultInterfaceUpdate(network)
        }

        override fun onCapabilitiesChanged(network: Network, capabilities: NetworkCapabilities) {
            if (defaultNetwork == network) checkDefaultInterfaceUpdate(network)
        }

        override fun onLost(network: Network) {
            if (defaultNetwork != network) return
            defaultNetwork = null
            checkDefaultInterfaceUpdate(null)
        }
    }

    fun start(connectivityManager: ConnectivityManager) {
        connectivity = connectivityManager
        if (registered) return
        try {
            connectivityManager.registerDefaultNetworkCallback(callback)
            registered = true
        } catch (e: Exception) {
            Log.e(TAG, "registerDefaultNetworkCallback", e)
        }
        defaultNetwork = try {
            connectivityManager.activeNetwork
        } catch (e: Exception) {
            null
        }
        checkDefaultInterfaceUpdate(defaultNetwork)
    }

    fun stop() {
        if (!registered) return
        try {
            connectivity?.unregisterNetworkCallback(callback)
        } catch (e: Exception) {
            Log.e(TAG, "unregisterNetworkCallback", e)
        }
        registered = false
    }

    fun setListener(listener: InterfaceUpdateListener?) {
        this.listener = listener
        checkDefaultInterfaceUpdate(defaultNetwork)
    }

    private fun checkDefaultInterfaceUpdate(network: Network?) {
        val listener = listener ?: return
        if (network == null) {
            listener.updateDefaultInterface("", -1, false, false)
            return
        }
        val manager = connectivity ?: return
        // Android reports the network before its link properties are ready; retry briefly rather
        // than hand the core an empty interface it would then keep using.
        for (attempt in 0 until 10) {
            val linkProperties = try {
                manager.getLinkProperties(network)
            } catch (e: Exception) {
                null
            }
            if (linkProperties == null) {
                sleep()
                continue
            }
            val index = try {
                NetworkInterface.getByName(linkProperties.interfaceName)?.index ?: -1
            } catch (e: Exception) {
                -1
            }
            if (index < 0) {
                sleep()
                continue
            }
            val capabilities = manager.getNetworkCapabilities(network)
            val expensive = capabilities?.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_METERED) == false
            val constrained = capabilities?.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_SUSPENDED) == false
            listener.updateDefaultInterface(linkProperties.interfaceName, index, expensive, constrained)
            return
        }
        listener.updateDefaultInterface("", -1, false, false)
    }

    private fun sleep() {
        try {
            Thread.sleep(100)
        } catch (e: InterruptedException) {
            Thread.currentThread().interrupt()
        }
    }
}
