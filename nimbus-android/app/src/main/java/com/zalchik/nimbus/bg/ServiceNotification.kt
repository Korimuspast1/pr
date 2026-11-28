package com.zalchik.nimbus.bg

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.app.ServiceCompat
import com.zalchik.nimbus.R
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.MainActivity
import io.nekohasekai.libbox.Libbox

/**
 * The ongoing notification: what's connected, how fast it's going, and a way to disconnect
 * without opening the app. Desktop shows the same in its tray icon.
 *
 * Speed comes from the core's own counters over the command client, so it costs no extra
 * traffic; the screen-on/off receiver keeps those updates from waking the radio in a pocket.
 */
class ServiceNotification(private val service: Context) {
    companion object {
        const val CHANNEL_SERVICE = "service"
        const val CHANNEL_EVENTS = "events"
        const val ID_SERVICE = 1

        private val flags =
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) PendingIntent.FLAG_IMMUTABLE else 0

        fun createChannels(context: Context) {
            if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
            val manager = NotificationManagerCompat.from(context)
            manager.createNotificationChannel(
                NotificationChannel(
                    CHANNEL_SERVICE,
                    I18n.tr("Соединение"),
                    NotificationManager.IMPORTANCE_LOW,
                ).apply { setShowBadge(false) }
            )
            manager.createNotificationChannel(
                NotificationChannel(
                    CHANNEL_EVENTS,
                    I18n.tr("События"),
                    NotificationManager.IMPORTANCE_HIGH,
                )
            )
        }
    }

    private var shown = false

    private val builder: NotificationCompat.Builder
        get() = NotificationCompat.Builder(service, CHANNEL_SERVICE)
            .setShowWhen(false)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setSmallIcon(R.drawable.ic_cloud)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setColorized(true)
            .setContentIntent(
                PendingIntent.getActivity(
                    service,
                    0,
                    Intent(service, MainActivity::class.java)
                        .setFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT),
                    flags,
                )
            )
            .addAction(
                NotificationCompat.Action.Builder(
                    0,
                    I18n.tr("Отключиться"),
                    PendingIntent.getBroadcast(
                        service,
                        1,
                        Intent(ServiceController.ACTION_CLOSE).setPackage(service.packageName),
                        flags,
                    ),
                ).build()
            )

    /** Bring the service to the foreground; Android requires this within a few seconds of start. */
    fun show(name: String, text: String) {
        val notification = builder
            .setContentTitle(name.ifEmpty { I18n.tr("Nimbus") })
            .setContentText(text)
            .build()
        if (!shown) {
            ServiceCompat.startForeground(
                service as android.app.Service,
                ID_SERVICE,
                notification,
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                    android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_SYSTEM_EXEMPTED
                } else {
                    0
                },
            )
            shown = true
        } else {
            NotificationManagerCompat.from(service).notify(ID_SERVICE, notification)
        }
    }

    fun updateTraffic(uplink: Long, downlink: Long, name: String) {
        if (!shown) return
        val text = "${Libbox.formatBitrate(uplink * 8)} ↑  ${Libbox.formatBitrate(downlink * 8)} ↓"
        NotificationManagerCompat.from(service).notify(
            ID_SERVICE,
            builder.setContentTitle(name.ifEmpty { I18n.tr("Nimbus") }).setContentText(text).build(),
        )
    }

    fun close() {
        if (!shown) return
        shown = false
        ServiceCompat.stopForeground(service as android.app.Service, ServiceCompat.STOP_FOREGROUND_REMOVE)
    }

    /** A notification the core itself asks for (a subscription that expired, say). */
    fun event(notification: io.nekohasekai.libbox.Notification) {
        val channel = "event-${notification.typeID}"
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationManagerCompat.from(service).createNotificationChannel(
                NotificationChannel(channel, notification.typeName, NotificationManager.IMPORTANCE_HIGH)
            )
        }
        val builder = NotificationCompat.Builder(service, channel)
            .setShowWhen(true)
            .setAutoCancel(true)
            .setOnlyAlertOnce(true)
            .setSmallIcon(R.drawable.ic_cloud)
            .setCategory(NotificationCompat.CATEGORY_EVENT)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setContentTitle(notification.title)
            .setContentText(notification.body)
        if (notification.subtitle.isNotEmpty()) builder.setContentInfo(notification.subtitle)
        if (notification.openURL.isNotEmpty()) {
            builder.setContentIntent(
                PendingIntent.getActivity(
                    service,
                    notification.typeID,
                    Intent(service, MainActivity::class.java)
                        .setAction(Intent.ACTION_VIEW)
                        .setData(android.net.Uri.parse(notification.openURL))
                        .setFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT),
                    flags,
                )
            )
        }
        NotificationManagerCompat.from(service).notify(notification.identifier, notification.typeID, builder.build())
    }

    fun cancelEvent(identifier: String, typeID: Int) {
        NotificationManagerCompat.from(service).cancel(identifier, typeID)
    }
}
