package com.zalchik.nimbus.bg

import android.app.PendingIntent
import android.content.Intent
import android.os.Build
import android.service.quicksettings.Tile
import android.service.quicksettings.TileService
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.MainActivity
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch

/**
 * The Quick Settings tile: connect and disconnect without opening the app, which is what the
 * desktop's tray icon is for.
 *
 * Android won't let a tile ask for the VPN permission itself, so the first use opens the app;
 * after that the tile toggles the tunnel and says what it's connected to.
 */
class NimbusTileService : TileService() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private var collector: Job? = null

    override fun onStartListening() {
        super.onStartListening()
        render(ServiceController.state.value)
        collector = scope.launch { ServiceController.state.collect { render(it) } }
    }

    override fun onStopListening() {
        collector?.cancel()
        collector = null
        super.onStopListening()
    }

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }

    private fun render(state: ServiceState) {
        val tile = qsTile ?: return
        tile.state = when (state.status) {
            ServiceStatus.Started -> Tile.STATE_ACTIVE
            ServiceStatus.Stopped -> Tile.STATE_INACTIVE
            else -> Tile.STATE_UNAVAILABLE
        }
        tile.label = if (state.status == ServiceStatus.Started && state.selectedName.isNotEmpty()) {
            state.selectedName
        } else {
            I18n.tr("Nimbus")
        }
        tile.subtitle = when (state.status) {
            ServiceStatus.Started -> I18n.tr("Подключено")
            ServiceStatus.Starting, ServiceStatus.Stopping -> I18n.tr("…")
            ServiceStatus.Stopped -> state.error ?: I18n.tr("Отключено")
        }
        runCatching { tile.updateTile() }
    }

    override fun onClick() {
        when (ServiceController.state.value.status) {
            ServiceStatus.Started -> ServiceController.stop(this)
            ServiceStatus.Stopped -> {
                val settings = Settings.load()
                // Only an activity can ask for the VPN permission, and there's nothing to connect
                // to until a server is chosen — either way the app has to open first.
                if (settings.selectedServer.isEmpty() || ServiceController.prepare(this) != null) {
                    openApp()
                } else {
                    ServiceController.start(this, settings.selectedServer)
                }
            }

            else -> Unit
        }
    }

    @Suppress("DEPRECATION")
    private fun openApp() {
        val intent = Intent(this, MainActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_REORDER_TO_FRONT)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            startActivityAndCollapse(
                PendingIntent.getActivity(this, 0, intent, PendingIntent.FLAG_IMMUTABLE)
            )
        } else {
            startActivityAndCollapse(intent)
        }
    }
}
