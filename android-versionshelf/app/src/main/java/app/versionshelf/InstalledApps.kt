package app.versionshelf

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build

object InstalledApps {
    @Suppress("DEPRECATION")
    fun scan(context: Context): List<InstalledApp> {
        val packageManager = context.packageManager
        val query = Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_LAUNCHER)
        val activities = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            packageManager.queryIntentActivities(query, PackageManager.ResolveInfoFlags.of(0))
        } else {
            packageManager.queryIntentActivities(query, 0)
        }

        return activities
            .mapNotNull { resolveInfo ->
                val applicationInfo = resolveInfo.activityInfo?.applicationInfo ?: return@mapNotNull null
                if ((applicationInfo.flags and android.content.pm.ApplicationInfo.FLAG_SYSTEM) != 0) {
                    return@mapNotNull null
                }
                val packageName = applicationInfo.packageName
                try {
                    val packageInfo = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                        packageManager.getPackageInfo(
                            packageName,
                            PackageManager.PackageInfoFlags.of(0),
                        )
                    } else {
                        packageManager.getPackageInfo(packageName, 0)
                    }
                    InstalledApp(
                        packageName = packageName,
                        label = packageManager.getApplicationLabel(applicationInfo).toString(),
                        versionName = packageInfo.versionName ?: "Unknown",
                        versionCode = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                            packageInfo.longVersionCode
                        } else {
                            packageInfo.versionCode.toLong()
                        },
                        icon = try {
                            packageManager.getApplicationIcon(applicationInfo)
                        } catch (_: PackageManager.NameNotFoundException) {
                            null
                        },
                    )
                } catch (_: PackageManager.NameNotFoundException) {
                    null
                }
            }
            .distinctBy { it.packageName }
            .sortedBy { it.label.lowercase() }
    }
}
