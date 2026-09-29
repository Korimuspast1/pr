package app.ourcord.xposed.tweaks.plugins.internal

import android.app.AlertDialog
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.os.Build
import android.widget.Toast
import app.ourcord.plugins.API_VERSION
import app.ourcord.plugins.PluginManifest
import app.ourcord.reloadApp
import app.ourcord.xposed.OurCordConstants
import app.ourcord.xposed.api.registerNativeMethod
import app.ourcord.xposed.tweaks.OurCordUpdater
import app.ourcord.xposed.tweaks.plugins.InternalPluginFlags
import app.ourcord.xposed.tweaks.plugins.PluginStatesStore
import app.ourcord.xposed.versionCode
import app.ourcord.xposed.versionName
import java.io.File

private val manifest = PluginManifest(
    id = "ourcord.recovery",
    name = "Recovery",
    description = "Handles errors and provides troubleshooting options for OurCord.",
    author = "OurCord",
    icon = "ShieldIcon",
    version = API_VERSION,
)

internal val recoveryPlugin =
    internalPlugin(manifest, setOf(InternalPluginFlags.INTERNAL, InternalPluginFlags.ESSENTIAL)) {
        start {
            withAppActivity { act ->
                registerNativeMethod("ourcord.showRecoveryAlert") {
                    showRecoveryAlert(act)
                    null
                }

                registerNativeMethod("ourcord.alertError") {
                    val (error, version) = it
                    val errorString = "$error"

                    val clipboard = act.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                    val clip = ClipData.newPlainText("Stack Trace", errorString)

                    AlertDialog.Builder(act)
                        .setTitle("OurCord Error")
                        .setMessage(
                            """
                        OurCord: $version
                        Discord: ${act.versionName()} (${act.versionCode()})
                        Device: ${Build.MANUFACTURER} ${Build.MODEL}
                        
                        
                    """.trimIndent() + errorString
                        )
                        .setPositiveButton(android.R.string.ok) { dialog, _ -> dialog.dismiss() }
                        .setNeutralButton(android.R.string.copy) { dialog, _ ->
                            @Suppress("UsePropertyAccessSyntax")
                            clipboard.setPrimaryClip(clip)
                            Toast.makeText(act, "Copied stack trace", Toast.LENGTH_SHORT).show()
                            dialog.dismiss()
                        }
                        .setNegativeButton("Recovery") { dialog, _ ->
                            showRecoveryAlert(act)
                            dialog.dismiss()
                        }
                        .show()

                    null
                }
            }
        }
    }

/**
 * For the actual shake-gesture hook, you should be looking at [app.ourcord.xposed.tweaks.discordDevSupport].
 */
fun showRecoveryAlert(context: Context) {
    AlertDialog.Builder(context)
        .setTitle("OurCord Recovery Options")
        .setItems(
            arrayOf("Reload", "Enter Recovery Mode", "Delete Script", "Reset Loader Config"),
        ) { _, which ->
            when (which) {
                0 -> reloadApp()

                1 -> {
                    PluginStatesStore.requestDefaultsOnlyBoot(context.dataDir.absolutePath)
                    reloadApp()
                }

                2 -> {
                    val bundleFile = File(
                        context.dataDir,
                        "${OurCordConstants.CACHE_DIR}/${OurCordConstants.MAIN_SCRIPT_FILE}",
                    )
                    if (bundleFile.exists()) bundleFile.delete()
                    reloadApp()
                }

                3 -> {
                    OurCordUpdater.resetLoaderConfig()
                    reloadApp()
                }
            }
        }
        .show()
}