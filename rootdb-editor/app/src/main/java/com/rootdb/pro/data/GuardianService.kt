package com.rootdb.pro.data

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** Состояние Guardian для UI */
object GuardianState {
    private val _running = MutableStateFlow(false)
    val running: StateFlow<Boolean> = _running.asStateFlow()
    private val _events = MutableStateFlow<List<GuardianEvent>>(emptyList())
    val events: StateFlow<List<GuardianEvent>> = _events.asStateFlow()
    private val _status = MutableStateFlow("Остановлен")
    val status: StateFlow<String> = _status.asStateFlow()

    fun setRunning(v: Boolean) {
        _running.value = v
    }

    fun setStatus(s: String) {
        _status.value = s
    }

    fun pushEvent(e: GuardianEvent) {
        _events.value = (listOf(e) + _events.value).take(60)
    }

    fun loadInitial(events: List<GuardianEvent>) {
        _events.value = events
    }
}

/**
 * Guardian — фоновая служба: следит за изменёнными базами и не даёт
 * приложению откатить правки (восстановление из бэкапа / force-stop / уведомление).
 */
class GuardianService : Service() {

    private val scope = CoroutineScope(Dispatchers.Default + Job())
    private var lastStat = mutableMapOf<String, Pair<Long, Long>>() // path -> (mtime, size)

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        createChannel()
        GuardianState.setRunning(true)
        GuardianState.loadInitial(GuardianStore(this).loadEvents())
        log("Guardian запущен")
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val notif = buildNotification("Наблюдаю за базами…")
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(1, notif, ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC)
        } else {
            startForeground(1, notif)
        }
        scope.launch { watchLoop() }
        return START_STICKY
    }

    private suspend fun watchLoop() {
        val store = GuardianStore(this)
        val settings = SettingsStore(this)
        while (true) {
            val items = store.loadItems()
            GuardianState.setStatus(if (items.isEmpty()) "Нет отслеживаемых баз" else "Отслеживаю: ${items.size}")
            items.forEach { item ->
                try {
                    checkItem(item)
                } catch (e: Exception) {
                    log("Ошибка ${item.label}: ${e.message}")
                }
            }
            delay(settings.guardianIntervalSec.coerceAtLeast(1) * 1000L)
        }
    }

    private suspend fun checkItem(item: WatchItem) {
        val st = withContext(Dispatchers.IO) { RootShell.stat(item.dbPath) } ?: return
        val prev = lastStat[item.dbPath]
        val cur = st.mtimeSec to st.size
        if (prev == null) {
            lastStat[item.dbPath] = cur
            return
        }
        if (prev == cur) return
        // База изменилась
        lastStat[item.dbPath] = cur
        val appName = item.pkg.substringAfterLast(".")
        when (item.mode) {
            WatchMode.RESTORE -> {
                val backup = java.io.File(item.backupPath)
                if (backup.exists()) {
                    val ok = withContext(Dispatchers.IO) {
                        RootShell.copyBack(backup, item.dbPath, st.mode, st.uid, st.gid)
                    }
                    withContext(Dispatchers.IO) { RootShell.forceStop(item.pkg) }
                    // Обновляем снимок после восстановления
                    withContext(Dispatchers.IO) { RootShell.stat(item.dbPath) }?.let {
                        lastStat[item.dbPath] = it.mtimeSec to it.size
                    }
                    log(if (ok) "↩ $appName: правки восстановлены из бэкапа" else "⚠ $appName: не удалось восстановить")
                } else {
                    log("⚠ $appName: бэкап не найден")
                }
            }
            WatchMode.KILL -> {
                withContext(Dispatchers.IO) { RootShell.forceStop(item.pkg) }
                log("⛔ $appName: приложение остановлено (база изменилась)")
            }
            WatchMode.NOTIFY -> {
                log("🔔 $appName: база изменилась (${formatSize(st.size)})")
            }
        }
        updateNotification("Последнее событие: ${GuardianState.events.value.firstOrNull()?.text ?: "—"}")
    }

    private fun log(text: String) {
        GuardianState.pushEvent(GuardianEvent(System.currentTimeMillis(), text))
        GuardianStore(this).addEvent(text)
    }

    private fun createChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val ch = NotificationChannel(
                CHANNEL_ID, getString(R.string.guardian_channel_name), NotificationManager.IMPORTANCE_LOW
            ).apply { description = getString(R.string.guardian_channel_desc) }
            getSystemService(NotificationManager::class.java)?.createNotificationChannel(ch)
        }
    }

    private fun buildNotification(text: String): Notification =
        NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("RootDB Guardian")
            .setContentText(text)
            .setSmallIcon(android.R.drawable.ic_dialog_info)
            .setOngoing(true)
            .build()

    private fun updateNotification(text: String) {
        val nm = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        nm.notify(1, buildNotification(text))
    }

    override fun onDestroy() {
        GuardianState.setRunning(false)
        GuardianState.setStatus("Остановлен")
        super.onDestroy()
    }

    companion object {
        const val CHANNEL_ID = "guardian"
        fun start(context: Context) {
            val i = Intent(context, GuardianService::class.java)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) context.startForegroundService(i)
            else context.startService(i)
        }
        fun stop(context: Context) {
            context.stopService(Intent(context, GuardianService::class.java))
        }
    }
}
