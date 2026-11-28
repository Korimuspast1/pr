package com.zalchik.nimbus

import android.app.Application
import android.app.NotificationManager
import android.content.ClipboardManager
import android.content.Context
import android.net.ConnectivityManager
import android.net.wifi.WifiManager
import android.os.Build
import android.os.PowerManager
import android.util.Log
import com.zalchik.nimbus.bg.ServiceNotification
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.data.Paths
import com.zalchik.nimbus.engine.Device
import com.zalchik.nimbus.i18n.I18n
import io.nekohasekai.libbox.Libbox
import io.nekohasekai.libbox.SetupOptions
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import java.util.Locale

/**
 * Process start: the folders Nimbus keeps its data in, the language, and the sing-box core's own
 * setup, which has to happen once before anything asks it for a service.
 *
 * The core writes its logs, rule sets and crash reports under the paths given here, so they end
 * up in the app's private storage — the phone's counterpart of desktop's ~/.config/Nimbus.
 */
class NimbusApplication : Application() {
    companion object {
        private const val TAG = "NimbusApplication"

        lateinit var application: NimbusApplication
            private set

        val connectivity: ConnectivityManager
            get() = application.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        val notificationManager: NotificationManager
            get() = application.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        val powerManager: PowerManager
            get() = application.getSystemService(Context.POWER_SERVICE) as PowerManager
        val wifiManager: WifiManager
            get() = application.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        val clipboard: ClipboardManager
            get() = application.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
    }

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    override fun onCreate() {
        super.onCreate()
        application = this

        Paths.init(this)
        ServiceNotification.createChannels(this)

        val settings = Settings.load()
        I18n.setLanguage(I18n.resolve(settings.language))
        runCatching { Libbox.setLocale(currentLocale().toLanguageTag()) }
            .onFailure { Log.w(TAG, "setLocale: ${it.message}") }

        // What the device headers say about this phone (nimbus/subscription.py's platform.node()).
        Device.os = "Android"
        Device.osVersion = Build.VERSION.RELEASE ?: ""
        Device.model = "${Build.MANUFACTURER} ${Build.MODEL}".trim()
        Device.name = Build.DEVICE ?: Device.model

        scope.launch {
            runCatching { Libbox.setup(setupOptions()) }
                .onFailure { Log.e(TAG, "libbox setup", it) }
            runCatching { Libbox.prepareCrashSignalHandlers() }
        }
    }

    private fun currentLocale(): Locale = when (I18n.lang) {
        "ru" -> Locale("ru")
        "en" -> Locale.ENGLISH
        else -> Locale.getDefault()
    }

    private fun setupOptions(): SetupOptions = SetupOptions().apply {
        basePath = Paths.dataDir.path
        workingPath = Paths.cacheDir.path
        tempPath = Paths.cacheDir.path
        fixAndroidStack = true
        // 0 is not "pick a port": the command server then listens on a unix socket at
        // basePath/command.sock and the command client dials the same path (libbox's
        // command_server.go / command_client.go both branch on the port being 0). That is what
        // we want in one process on a phone: nothing to collide with, no loopback involved.
        // basePath above is therefore load-bearing for the UI's connection to the core.
        commandServerListenPort = 0
        commandServerSecret = ""
        logMaxLines = 3000
        debug = BuildConfig.DEBUG
        crashReportSource = "NimbusApplication"
        appVersion = BuildConfig.VERSION_CODE.toString()
        appMarketingVersion = BuildConfig.VERSION_NAME
        oomKillerEnabled = false
        powerReportEnabled = false
        platformMetadata = metadata()
    }

    /** What the core's crash reports and logs say about the device they came from. */
    private fun metadata(): String = org.json.JSONObject().apply {
        put("os", "Android ${Build.VERSION.RELEASE}")
        put("sdk", Build.VERSION.SDK_INT)
        put("manufacturer", Build.MANUFACTURER)
        put("model", Build.MODEL)
    }.toString()
}
