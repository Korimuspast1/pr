package com.glass.launcher

import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.content.pm.ActivityInfo
import android.content.pm.ApplicationInfo
import android.content.pm.ComponentInfo
import android.content.pm.PackageInfo
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.drawable.Drawable
import android.os.Build
import android.util.LruCache
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.core.content.pm.PackageInfoCompat
import androidx.core.content.res.ResourcesCompat
import androidx.core.graphics.drawable.toBitmap
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.security.MessageDigest

data class AppEntry(
    val pkg: String,
    val label: String,
    val versionName: String,
    val versionCode: Long,
    val activityCount: Int,
    val isSystem: Boolean,
    val enabled: Boolean,
    val firstInstall: Long,
    val lastUpdate: Long,
    val targetSdk: Int,
)

data class ActEntry(
    val pkg: String,
    val cls: String,
    val label: String,
    val appLabel: String,
    val exported: Boolean,
    val enabled: Boolean,
    val permission: String?,
    val isLauncher: Boolean = false,
) {
    val shortClass: String get() = if (cls.startsWith(pkg)) cls.removePrefix(pkg) else cls
}

data class CompEntry(val name: String, val exported: Boolean, val enabled: Boolean, val permission: String?, val extra: String? = null) {
    fun shortName(pkg: String) = if (name.startsWith(pkg)) name.removePrefix(pkg) else name
}

data class PermEntry(val name: String, val granted: Boolean)

data class PkgDetails(
    val app: AppEntry,
    val minSdk: Int,
    val uid: Int,
    val apkPath: String,
    val apkSize: Long,
    val dataDir: String,
    val installer: String?,
    val sha256: String?,
    val activities: List<ActEntry>,
    val services: List<CompEntry>,
    val receivers: List<CompEntry>,
    val providers: List<CompEntry>,
    val permissions: List<PermEntry>,
    val launchIntent: Boolean,
    val splitCount: Int,
)

@Suppress("DEPRECATION")
object Pkg {
    var apps by mutableStateOf<List<AppEntry>?>(null)
    var loading by mutableStateOf(false)
    var progress by mutableFloatStateOf(0f)
    var allActivities by mutableStateOf<List<ActEntry>?>(null)
    var activitiesLoading by mutableStateOf(false)

    private val iconCache = LruCache<String, ImageBitmap>(400)

    private fun info(pm: PackageManager, pkg: String, flags: Int): PackageInfo? = try {
        pm.getPackageInfo(pkg, flags)
    } catch (e: Throwable) {
        null
    }

    private fun installedPackages(pm: PackageManager): List<PackageInfo> = try {
        pm.getInstalledPackages(0)
    } catch (e: Throwable) {
        pm.getInstalledApplications(0).mapNotNull { info(pm, it.packageName, 0) }
    }

    suspend fun load(ctx: Context, force: Boolean = false) {
        if (loading) return
        if (apps != null && !force) return
        loading = true
        progress = 0f
        try {
            val result = withContext(Dispatchers.IO) {
                val pm = ctx.packageManager
                val pkgs = installedPackages(pm)
                val out = ArrayList<AppEntry>(pkgs.size)
                pkgs.forEachIndexed { i, p ->
                    val ai = p.applicationInfo
                    val acts = info(pm, p.packageName, PackageManager.GET_ACTIVITIES or PackageManager.MATCH_DISABLED_COMPONENTS)?.activities
                    out += AppEntry(
                        pkg = p.packageName,
                        label = ai?.loadLabel(pm)?.toString() ?: p.packageName,
                        versionName = p.versionName ?: "",
                        versionCode = PackageInfoCompat.getLongVersionCode(p),
                        activityCount = acts?.size ?: 0,
                        isSystem = ai != null && (ai.flags and ApplicationInfo.FLAG_SYSTEM) != 0,
                        enabled = ai?.enabled ?: true,
                        firstInstall = p.firstInstallTime,
                        lastUpdate = p.lastUpdateTime,
                        targetSdk = ai?.targetSdkVersion ?: 0,
                    )
                    if (i % 8 == 0) progress = i.toFloat() / pkgs.size
                }
                out
            }
            apps = result
            allActivities = null
        } finally {
            loading = false
            progress = 1f
        }
    }

    private fun isExported(ci: ComponentInfo) = ci.exported

    fun activitiesOf(ctx: Context, pkg: String): List<ActEntry> {
        val pm = ctx.packageManager
        val p = info(pm, pkg, PackageManager.GET_ACTIVITIES or PackageManager.MATCH_DISABLED_COMPONENTS) ?: return emptyList()
        val appLabel = p.applicationInfo?.loadLabel(pm)?.toString() ?: pkg
        val launcherCls = pm.getLaunchIntentForPackage(pkg)?.component?.className
        return (p.activities ?: emptyArray()).map { a ->
            ActEntry(
                pkg = pkg,
                cls = a.name,
                label = safeLabel(pm, a) ?: a.name.substringAfterLast('.'),
                appLabel = appLabel,
                exported = isExported(a),
                enabled = a.enabled,
                permission = a.permission,
                isLauncher = a.name == launcherCls || a.targetActivity != null && a.targetActivity == launcherCls,
            )
        }.sortedWith(compareByDescending<ActEntry> { it.isLauncher }.thenByDescending { it.exported }.thenBy { it.label.lowercase() })
    }

    private fun safeLabel(pm: PackageManager, a: ActivityInfo): String? = try {
        a.loadLabel(pm)?.toString()
    } catch (e: Throwable) {
        null
    }

    suspend fun loadAllActivities(ctx: Context) {
        if (allActivities != null || activitiesLoading) return
        activitiesLoading = true
        try {
            val list = withContext(Dispatchers.IO) {
                val pkgs = apps?.map { it.pkg } ?: installedPackages(ctx.packageManager).map { it.packageName }
                pkgs.flatMap { activitiesOf(ctx, it) }
            }
            allActivities = list
        } finally {
            activitiesLoading = false
        }
    }

    /** Resolve activities that handle a given intent action (e.g. android.intent.action.SEND). */
    fun queryAction(ctx: Context, action: String): List<ActEntry> {
        val pm = ctx.packageManager
        val intents = buildList {
            add(Intent(action))
            add(Intent(action).addCategory(Intent.CATEGORY_DEFAULT))
            add(Intent(action).setType("*/*"))
            add(Intent(action).setType("text/plain"))
        }
        val flags = if (Build.VERSION.SDK_INT >= 23) PackageManager.MATCH_ALL else 0
        val seen = HashSet<String>()
        val out = ArrayList<ActEntry>()
        for (i in intents) {
            val res = try { pm.queryIntentActivities(i, flags) } catch (e: Throwable) { emptyList() }
            for (r in res) {
                val a = r.activityInfo ?: continue
                val key = a.packageName + "/" + a.name
                if (!seen.add(key)) continue
                out += ActEntry(
                    pkg = a.packageName, cls = a.name,
                    label = r.loadLabel(pm)?.toString() ?: a.name,
                    appLabel = a.applicationInfo?.loadLabel(pm)?.toString() ?: a.packageName,
                    exported = a.exported, enabled = a.enabled, permission = a.permission,
                )
            }
        }
        return out
    }

    private fun comps(arr: Array<out ComponentInfo>?, extra: (ComponentInfo) -> String? = { null }): List<CompEntry> =
        (arr ?: emptyArray()).map {
            val perm = when (it) {
                is android.content.pm.ServiceInfo -> it.permission
                is android.content.pm.ActivityInfo -> it.permission
                is android.content.pm.ProviderInfo -> it.readPermission
                else -> null
            }
            CompEntry(it.name, it.exported, it.enabled, perm, extra(it))
        }.sortedBy { it.name }

    @SuppressLint("PackageManagerGetSignatures")
    fun details(ctx: Context, pkg: String): PkgDetails? {
        val pm = ctx.packageManager
        val base = info(pm, pkg, PackageManager.GET_PERMISSIONS) ?: return null
        val ai = base.applicationInfo ?: return null
        val dis = PackageManager.MATCH_DISABLED_COMPONENTS
        val services = comps(info(pm, pkg, PackageManager.GET_SERVICES or dis)?.services)
        val receivers = comps(info(pm, pkg, PackageManager.GET_RECEIVERS or dis)?.receivers)
        val providers = comps(info(pm, pkg, PackageManager.GET_PROVIDERS or dis)?.providers) {
            (it as? android.content.pm.ProviderInfo)?.authority
        }
        val acts = activitiesOf(ctx, pkg)
        val req = base.requestedPermissions ?: emptyArray()
        val reqFlags = base.requestedPermissionsFlags
        val perms = req.mapIndexed { i, n ->
            val granted = reqFlags != null && i < reqFlags.size &&
                (reqFlags[i] and PackageInfo.REQUESTED_PERMISSION_GRANTED) != 0
            PermEntry(n, granted)
        }.sortedWith(compareByDescending<PermEntry> { it.granted }.thenBy { it.name })

        val sha = try {
            val sigBytes: ByteArray? = if (Build.VERSION.SDK_INT >= 28) {
                val si = info(pm, pkg, PackageManager.GET_SIGNING_CERTIFICATES)?.signingInfo
                val arr = if (si == null) null else if (si.hasMultipleSigners()) si.apkContentsSigners else si.signingCertificateHistory
                arr?.firstOrNull()?.toByteArray()
            } else {
                info(pm, pkg, PackageManager.GET_SIGNATURES)?.signatures?.firstOrNull()?.toByteArray()
            }
            sigBytes?.let { b ->
                MessageDigest.getInstance("SHA-256").digest(b).joinToString(":") { "%02X".format(it) }
            }
        } catch (e: Throwable) {
            null
        }

        val installer = try {
            if (Build.VERSION.SDK_INT >= 30) pm.getInstallSourceInfo(pkg).installingPackageName
            else pm.getInstallerPackageName(pkg)
        } catch (e: Throwable) {
            null
        }

        val app = AppEntry(
            pkg = pkg,
            label = ai.loadLabel(pm).toString(),
            versionName = base.versionName ?: "",
            versionCode = PackageInfoCompat.getLongVersionCode(base),
            activityCount = acts.size,
            isSystem = (ai.flags and ApplicationInfo.FLAG_SYSTEM) != 0,
            enabled = ai.enabled,
            firstInstall = base.firstInstallTime,
            lastUpdate = base.lastUpdateTime,
            targetSdk = ai.targetSdkVersion,
        )
        val apk = ai.sourceDir ?: ""
        return PkgDetails(
            app = app,
            minSdk = if (Build.VERSION.SDK_INT >= 24) ai.minSdkVersion else 0,
            uid = ai.uid,
            apkPath = apk,
            apkSize = try { File(apk).length() + (ai.splitSourceDirs?.sumOf { File(it).length() } ?: 0L) } catch (e: Throwable) { 0L },
            dataDir = ai.dataDir ?: "",
            installer = installer,
            sha256 = sha,
            activities = acts,
            services = services,
            receivers = receivers,
            providers = providers,
            permissions = perms,
            launchIntent = pm.getLaunchIntentForPackage(pkg) != null,
            splitCount = ai.splitSourceDirs?.size ?: 0,
        )
    }

    // ---------------------------------------------------------------- icons

    private fun Drawable.toImage(size: Int = 144): ImageBitmap =
        toBitmap(size, size, Bitmap.Config.ARGB_8888).asImageBitmap()

    fun appIconDrawable(ctx: Context, pkg: String, cls: String?): Drawable? {
        val pm = ctx.packageManager
        return try {
            if (!cls.isNullOrBlank()) pm.getActivityIcon(android.content.ComponentName(pkg, cls))
            else pm.getApplicationIcon(pkg)
        } catch (e: Throwable) {
            try { pm.getApplicationIcon(pkg) } catch (e2: Throwable) { null }
        }
    }

    fun loadIcon(ctx: Context, pkg: String, cls: String?): ImageBitmap? {
        val key = if (cls.isNullOrBlank()) pkg else "$pkg/$cls"
        iconCache.get(key)?.let { return it }
        val img = try { appIconDrawable(ctx, pkg, cls)?.toImage() } catch (e: Throwable) { null } ?: return null
        iconCache.put(key, img)
        return img
    }

    fun cachedIcon(pkg: String, cls: String?): ImageBitmap? =
        iconCache.get(if (cls.isNullOrBlank()) pkg else "$pkg/$cls")

    /** e.g. "com.foo:mipmap/ic_launcher" for the given activity icon. */
    fun iconResName(ctx: Context, pkg: String, cls: String): String {
        val pm = ctx.packageManager
        return try {
            val ai = pm.getActivityInfo(android.content.ComponentName(pkg, cls), PackageManager.MATCH_DISABLED_COMPONENTS)
            val res = ai.iconResource.takeIf { it != 0 } ?: ai.applicationInfo.icon
            if (res == 0) "" else pm.getResourcesForApplication(pkg).getResourceName(res)
        } catch (e: Throwable) {
            ""
        }
    }

    fun iconFromResName(ctx: Context, name: String): Bitmap? {
        if (!name.contains(':')) return null
        val pkg = name.substringBefore(':')
        return try {
            val res = ctx.packageManager.getResourcesForApplication(pkg)
            val id = res.getIdentifier(name, null, null)
            if (id == 0) null else ResourcesCompat.getDrawable(res, id, null)?.toBitmap(192, 192, Bitmap.Config.ARGB_8888)
        } catch (e: Throwable) {
            null
        }
    }
}

@Composable
fun rememberIcon(pkg: String, cls: String? = null): ImageBitmap? {
    val ctx = LocalContext.current
    val state by produceState(Pkg.cachedIcon(pkg, cls), pkg, cls) {
        if (value == null) value = withContext(Dispatchers.IO) { Pkg.loadIcon(ctx, pkg, cls) }
    }
    return state
}

@Composable
fun rememberResIcon(name: String): ImageBitmap? {
    val ctx = LocalContext.current
    val state by produceState<ImageBitmap?>(null, name) {
        value = withContext(Dispatchers.IO) { Pkg.iconFromResName(ctx, name)?.asImageBitmap() }
    }
    return state
}
