package app.versionshelf

import android.graphics.drawable.Drawable

data class InstalledApp(
    val packageName: String,
    val label: String,
    val versionName: String,
    val versionCode: Long,
    val icon: Drawable?,
)

data class VersionRecord(
    val packageName: String,
    val versionName: String,
    val versionCode: Long,
    val releasedAt: String?,
    val apkUrl: String,
    val sha256: String,
    val signingCertificateSha256: String?,
    val sourceLabel: String,
)

data class CatalogSnapshot(
    val sourceLabel: String,
    val versionsByPackage: Map<String, List<VersionRecord>>,
)

enum class CatalogType { FDROID, CUSTOM }

data class CatalogConfig(val type: CatalogType, val url: String) {
    val sourceLabel: String
        get() = if (type == CatalogType.FDROID) "F-Droid" else "Private catalogue"

    companion object {
        const val DEFAULT_FDROID_URL = "https://f-droid.org/repo/index-v2.json"
    }
}
