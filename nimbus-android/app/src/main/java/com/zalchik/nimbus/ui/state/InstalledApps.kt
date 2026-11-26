package com.zalchik.nimbus.ui.state

import android.content.Context
import android.content.pm.ApplicationInfo
import android.content.pm.PackageManager
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/** An installed app the per-app filter can name. */
data class InstalledApp(
    val packageName: String,
    val label: String,
    val system: Boolean,
)

/**
 * The app list for per-app routing. Android gives no notification when it changes, so the screen
 * reads it once when it opens — enough for a list people edit rarely.
 */
object InstalledApps {
    private var cached: List<InstalledApp> = emptyList()

    suspend fun load(context: Context, includeSystem: Boolean = false): List<InstalledApp> =
        withContext(Dispatchers.IO) {
            if (cached.isNotEmpty()) return@withContext filter(cached, includeSystem)
            val manager = context.packageManager
            val packages = manager.getInstalledPackages(PackageManager.GET_META_DATA)
            val out = packages.mapNotNull { info ->
                val application = info.applicationInfo ?: return@mapNotNull null
                val label = manager.getApplicationLabel(application).toString()
                InstalledApp(
                    packageName = info.packageName,
                    label = label,
                    system = (application.flags and ApplicationInfo.FLAG_SYSTEM) != 0,
                )
            }.distinctBy { it.packageName }
            cached = out
            filter(out, includeSystem)
        }

    private fun filter(apps: List<InstalledApp>, includeSystem: Boolean): List<InstalledApp> =
        apps.filter { includeSystem || !it.system }.sortedBy { it.label.lowercase() }

    fun forget() {
        cached = emptyList()
    }
}
