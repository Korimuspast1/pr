package com.glass.launcher

import android.content.ActivityNotFoundException
import android.content.ClipData
import android.content.ClipboardManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.net.Uri
import android.os.Build
import android.provider.Settings
import android.widget.Toast
import androidx.core.content.FileProvider
import androidx.core.content.pm.ShortcutInfoCompat
import androidx.core.content.pm.ShortcutManagerCompat
import androidx.core.graphics.drawable.IconCompat
import androidx.core.graphics.drawable.toBitmap
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.util.concurrent.TimeUnit

sealed class LaunchResult {
    data object Ok : LaunchResult()
    data class Error(val message: String, val canRoot: Boolean = false) : LaunchResult()
}

object Launcher {
    const val ACTION_SHORTCUT = "com.glass.launcher.LAUNCH_SHORTCUT"
    const val EXTRA_JSON = "shortcut_json"

    val COMMON_ACTIONS = listOf(
        Intent.ACTION_MAIN, Intent.ACTION_VIEW, Intent.ACTION_SEND, Intent.ACTION_SEND_MULTIPLE,
        Intent.ACTION_EDIT, Intent.ACTION_PICK, Intent.ACTION_DIAL, Intent.ACTION_CALL,
        Intent.ACTION_SENDTO, Intent.ACTION_SEARCH, Intent.ACTION_WEB_SEARCH, Intent.ACTION_GET_CONTENT,
        Intent.ACTION_CREATE_SHORTCUT, Intent.ACTION_APPLICATION_PREFERENCES,
        Settings.ACTION_SETTINGS, Settings.ACTION_WIFI_SETTINGS, Settings.ACTION_BLUETOOTH_SETTINGS,
        Settings.ACTION_DISPLAY_SETTINGS, Settings.ACTION_APPLICATION_DEVELOPMENT_SETTINGS,
        Settings.ACTION_ACCESSIBILITY_SETTINGS, Settings.ACTION_LOCATION_SOURCE_SETTINGS,
    )

    val COMMON_CATEGORIES = listOf(
        Intent.CATEGORY_DEFAULT, Intent.CATEGORY_LAUNCHER, Intent.CATEGORY_BROWSABLE,
        Intent.CATEGORY_HOME, Intent.CATEGORY_INFO, Intent.CATEGORY_PREFERENCE,
    )

    /** name -> flag, shown as toggles in the editor. */
    val FLAGS: List<Pair<String, Int>> = listOf(
        "CLEAR_TOP" to Intent.FLAG_ACTIVITY_CLEAR_TOP,
        "CLEAR_TASK" to Intent.FLAG_ACTIVITY_CLEAR_TASK,
        "SINGLE_TOP" to Intent.FLAG_ACTIVITY_SINGLE_TOP,
        "NO_HISTORY" to Intent.FLAG_ACTIVITY_NO_HISTORY,
        "MULTIPLE_TASK" to Intent.FLAG_ACTIVITY_MULTIPLE_TASK,
        "NO_ANIMATION" to Intent.FLAG_ACTIVITY_NO_ANIMATION,
        "EXCLUDE_FROM_RECENTS" to Intent.FLAG_ACTIVITY_EXCLUDE_FROM_RECENTS,
        "REORDER_TO_FRONT" to Intent.FLAG_ACTIVITY_REORDER_TO_FRONT,
        "NEW_DOCUMENT" to Intent.FLAG_ACTIVITY_NEW_DOCUMENT,
        "LAUNCH_ADJACENT" to Intent.FLAG_ACTIVITY_LAUNCH_ADJACENT,
        "GRANT_READ_URI" to Intent.FLAG_GRANT_READ_URI_PERMISSION,
    )

    fun buildIntent(d: ShortcutData): Intent {
        val i = Intent()
        if (d.action.isNotBlank()) i.action = d.action.trim()
        if (d.pkg.isNotBlank() && d.cls.isNotBlank()) {
            val cls = if (d.cls.startsWith(".")) d.pkg + d.cls else d.cls
            i.component = ComponentName(d.pkg.trim(), cls.trim())
        } else if (d.pkg.isNotBlank()) {
            i.setPackage(d.pkg.trim())
        }
        when {
            d.data.isNotBlank() && d.mime.isNotBlank() -> i.setDataAndType(Uri.parse(d.data.trim()), d.mime.trim())
            d.data.isNotBlank() -> i.data = Uri.parse(d.data.trim())
            d.mime.isNotBlank() -> i.type = d.mime.trim()
        }
        d.categories.filter { it.isNotBlank() }.forEach { i.addCategory(it.trim()) }
        if (i.action == null && i.component == null && i.`package` == null) i.action = Intent.ACTION_MAIN
        i.addFlags(d.flags or Intent.FLAG_ACTIVITY_NEW_TASK)
        for (e in d.extras) {
            if (e.key.isBlank()) continue
            when (e.type) {
                "int" -> e.value.trim().toIntOrNull()?.let { i.putExtra(e.key, it) }
                "long" -> e.value.trim().toLongOrNull()?.let { i.putExtra(e.key, it) }
                "bool" -> i.putExtra(e.key, e.value.trim().equals("true", true) || e.value.trim() == "1")
                "float" -> e.value.trim().toFloatOrNull()?.let { i.putExtra(e.key, it) }
                "double" -> e.value.trim().toDoubleOrNull()?.let { i.putExtra(e.key, it) }
                "uri" -> i.putExtra(e.key, Uri.parse(e.value.trim()))
                else -> i.putExtra(e.key, e.value)
            }
        }
        return i
    }

    private fun q(s: String) = "'" + s.replace("'", "'\\''") + "'"

    /** `am start ...` command line equivalent of the shortcut. */
    fun amCommand(d: ShortcutData): String {
        val sb = StringBuilder("am start")
        if (d.action.isNotBlank()) sb.append(" -a ").append(q(d.action.trim()))
        if (d.data.isNotBlank()) sb.append(" -d ").append(q(d.data.trim()))
        if (d.mime.isNotBlank()) sb.append(" -t ").append(q(d.mime.trim()))
        d.categories.filter { it.isNotBlank() }.forEach { sb.append(" -c ").append(q(it.trim())) }
        if (d.flags != 0) sb.append(" -f ").append(d.flags or Intent.FLAG_ACTIVITY_NEW_TASK)
        for (e in d.extras) {
            if (e.key.isBlank()) continue
            val opt = when (e.type) {
                "int" -> "--ei"; "long" -> "--el"; "bool" -> "--ez"; "float" -> "--ef"
                "double" -> "--ed"; "uri" -> "--eu"; else -> "--es"
            }
            sb.append(' ').append(opt).append(' ').append(q(e.key)).append(' ').append(q(e.value))
        }
        if (d.pkg.isNotBlank() && d.cls.isNotBlank()) {
            val cls = if (d.cls.startsWith(".")) d.pkg + d.cls else d.cls
            sb.append(" -n ").append(q("${d.pkg.trim()}/${cls.trim()}"))
        } else if (d.pkg.isNotBlank()) {
            sb.append(' ').append(q(d.pkg.trim()))
        }
        return sb.toString()
    }

    fun adbCommand(d: ShortcutData) = "adb shell " + amCommand(d)

    fun intentUri(d: ShortcutData): String = buildIntent(d).toUri(Intent.URI_INTENT_SCHEME)

    fun launch(ctx: Context, d: ShortcutData, record: Boolean = true): LaunchResult {
        if (d.root) {
            val ok = launchRoot(d)
            if (ok == null) {
                if (record) Store.addRecent(d)
                return LaunchResult.Ok
            }
            return LaunchResult.Error(ok)
        }
        return try {
            ctx.startActivity(buildIntent(d))
            if (record) Store.addRecent(d)
            LaunchResult.Ok
        } catch (e: SecurityException) {
            if (Store.rootFallback && launchRoot(d) == null) {
                if (record) Store.addRecent(d)
                LaunchResult.Ok
            } else {
                LaunchResult.Error(tr("Permission denied: activity is not exported or protected.", "Доступ запрещён: активити не экспортирована или защищена."), canRoot = true)
            }
        } catch (e: ActivityNotFoundException) {
            LaunchResult.Error(tr("Activity not found", "Активити не найдена"))
        } catch (e: Throwable) {
            LaunchResult.Error(e.javaClass.simpleName + ": " + (e.message ?: ""))
        }
    }

    /** Launch via `su`. Returns null on success, or an error message. */
    fun launchRoot(d: ShortcutData): String? = runSu(amCommand(d))

    fun runSu(cmd: String): String? = try {
        val p = ProcessBuilder("su", "-c", cmd).redirectErrorStream(true).start()
        val finished = if (Build.VERSION.SDK_INT >= 26) p.waitFor(8, TimeUnit.SECONDS) else { p.waitFor(); true }
        val out = p.inputStream.bufferedReader().readText()
        when {
            !finished -> { p.destroy(); tr("Root request timed out", "Истекло время ожидания root") }
            p.exitValue() != 0 -> out.ifBlank { tr("Root denied", "Root запрещён") }
            out.contains("Error", ignoreCase = false) -> out.trim()
            else -> null
        }
    } catch (e: Throwable) {
        tr("Root is not available", "Root недоступен")
    }

    suspend fun checkRoot(): Boolean = withContext(Dispatchers.IO) {
        try {
            val p = ProcessBuilder("su", "-c", "id").redirectErrorStream(true).start()
            val out = p.inputStream.bufferedReader().readText()
            p.waitFor() == 0 && out.contains("uid=0")
        } catch (e: Throwable) {
            false
        }
    }

    fun launchAndReport(ctx: Context, d: ShortcutData): LaunchResult {
        val r = launch(ctx, d)
        if (r is LaunchResult.Error) toast(ctx, r.message)
        return r
    }

    // --------------------------------------------------------------- shortcuts

    fun shortcutIntent(ctx: Context, d: ShortcutData): Intent =
        Intent(ctx, ShortcutActivity::class.java)
            .setAction(ACTION_SHORTCUT)
            .putExtra(EXTRA_JSON, d.toJson().toString())
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK)

    private fun iconFor(ctx: Context, d: ShortcutData): Bitmap? {
        if (d.icon.isNotBlank()) Pkg.iconFromResName(ctx, d.icon)?.let { return it }
        if (d.pkg.isBlank()) return null
        return try { Pkg.appIconDrawable(ctx, d.pkg, d.cls)?.toBitmap(192, 192, Bitmap.Config.ARGB_8888) } catch (e: Throwable) { null }
    }

    private fun shortcutInfo(ctx: Context, d: ShortcutData, rank: Int = 0): ShortcutInfoCompat {
        val bmp = iconFor(ctx, d)
        val icon = if (bmp != null) IconCompat.createWithBitmap(bmp) else IconCompat.createWithResource(ctx, R.mipmap.ic_launcher)
        val label = d.displayName.ifBlank { "Shortcut" }
        return ShortcutInfoCompat.Builder(ctx, "s_" + d.key().hashCode().toUInt().toString(16))
            .setShortLabel(label.take(24))
            .setLongLabel(label.take(48))
            .setIcon(icon)
            .setRank(rank)
            .setIntent(shortcutIntent(ctx, d))
            .build()
    }

    fun pinShortcut(ctx: Context, d: ShortcutData): Boolean = try {
        if (ShortcutManagerCompat.isRequestPinShortcutSupported(ctx)) {
            ShortcutManagerCompat.requestPinShortcut(ctx, shortcutInfo(ctx, d), null)
        } else false
    } catch (e: Throwable) {
        false
    }

    fun updateDynamicShortcuts(ctx: Context) {
        try {
            if (!Store.dynamicShortcuts) {
                ShortcutManagerCompat.removeAllDynamicShortcuts(ctx); return
            }
            val max = ShortcutManagerCompat.getMaxShortcutCountPerActivity(ctx).coerceAtMost(4)
            val list = Store.favorites.take(max).mapIndexed { i, d -> shortcutInfo(ctx, d, i) }
            ShortcutManagerCompat.setDynamicShortcuts(ctx, list)
        } catch (e: Throwable) {
            // ignore – launcher may not support dynamic shortcuts
        }
    }

    // ------------------------------------------------------------------ misc

    fun shareText(ctx: Context, d: ShortcutData) {
        val text = buildString {
            appendLine(d.displayName)
            if (d.pkg.isNotBlank()) appendLine("Package: ${d.pkg}")
            if (d.cls.isNotBlank()) appendLine("Class: ${d.cls}")
            appendLine()
            appendLine(adbCommand(d))
            appendLine()
            append(intentUri(d))
        }
        share(ctx, text)
    }

    fun share(ctx: Context, text: String) {
        val i = Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_TEXT, text)
        ctx.startActivity(Intent.createChooser(i, null).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }

    fun copy(ctx: Context, text: String, label: String = "text") {
        val cm = ctx.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        cm.setPrimaryClip(ClipData.newPlainText(label, text))
        if (Build.VERSION.SDK_INT < 33) toast(ctx, tr("Copied", "Скопировано"))
    }

    fun toast(ctx: Context, text: String) = Toast.makeText(ctx, text, Toast.LENGTH_SHORT).show()

    fun openAppInfo(ctx: Context, pkg: String) = safeStart(ctx,
        Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:$pkg")))

    fun openStore(ctx: Context, pkg: String) {
        if (!safeStart(ctx, Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=$pkg")), silent = true))
            safeStart(ctx, Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=$pkg")))
    }

    fun uninstall(ctx: Context, pkg: String) = safeStart(ctx, Intent(Intent.ACTION_DELETE, Uri.parse("package:$pkg")))

    fun launchApp(ctx: Context, pkg: String): Boolean {
        val i = ctx.packageManager.getLaunchIntentForPackage(pkg)
        if (i == null) {
            toast(ctx, tr("No launcher activity", "Нет активити запуска")); return false
        }
        return safeStart(ctx, i)
    }

    fun safeStart(ctx: Context, i: Intent, silent: Boolean = false): Boolean = try {
        ctx.startActivity(i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)); true
    } catch (e: Throwable) {
        if (!silent) toast(ctx, e.message ?: e.javaClass.simpleName); false
    }

    suspend fun shareApk(ctx: Context, pkg: String, label: String) {
        val uri = withContext(Dispatchers.IO) {
            try {
                val src = File(ctx.packageManager.getApplicationInfo(pkg, 0).sourceDir)
                val dir = File(ctx.cacheDir, "apks").apply { mkdirs(); listFiles()?.forEach { it.delete() } }
                val safe = label.replace(Regex("[^A-Za-z0-9._-]+"), "_")
                val dst = File(dir, "${safe}_$pkg.apk")
                src.inputStream().use { input -> dst.outputStream().use { input.copyTo(it) } }
                FileProvider.getUriForFile(ctx, ctx.packageName + ".files", dst)
            } catch (e: Throwable) {
                null
            }
        }
        if (uri == null) {
            toast(ctx, tr("Could not extract APK", "Не удалось извлечь APK")); return
        }
        val i = Intent(Intent.ACTION_SEND).setType("application/vnd.android.package-archive")
            .putExtra(Intent.EXTRA_STREAM, uri).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        ctx.startActivity(Intent.createChooser(i, null).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }
}
