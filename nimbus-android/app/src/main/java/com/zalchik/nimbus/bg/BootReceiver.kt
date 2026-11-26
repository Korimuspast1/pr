package com.zalchik.nimbus.bg

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.util.Log
import com.zalchik.nimbus.data.Settings
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

/**
 * Reconnect after a reboot or an app update, when the user asked for that ("start with the
 * system" in settings) — the phone's version of desktop's autostart.
 *
 * Android only lets this work if the VPN permission was granted before the reboot and the user
 * hasn't turned the app's battery optimisation into something that kills it; when it can't, the
 * tile and the app still connect on the first tap.
 */
class BootReceiver : BroadcastReceiver() {
    companion object {
        private const val TAG = "BootReceiver"
    }

    override fun onReceive(context: Context, intent: Intent) {
        when (intent.action) {
            Intent.ACTION_BOOT_COMPLETED,
            Intent.ACTION_MY_PACKAGE_REPLACED,
            Intent.ACTION_LOCKED_BOOT_COMPLETED -> Unit

            else -> return
        }
        val pending = goAsync()
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        scope.launch {
            try {
                val settings = Settings.load()
                if (!settings.autostart || settings.selectedServer.isEmpty()) return@launch
                // Null means Android remembers the permission; anything else is an intent only an
                // activity can launch, and a receiver has no business putting one on screen.
                if (ServiceController.prepare(context) != null) {
                    Log.i(TAG, "autostart skipped: the VPN permission needs the app to ask again")
                    return@launch
                }
                ServiceController.requestedByUser = true
                ServiceController.start(context, settings.selectedServer)
            } catch (e: Exception) {
                Log.e(TAG, "autostart", e)
            } finally {
                pending.finish()
            }
        }
    }
}
