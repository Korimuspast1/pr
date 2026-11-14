package app.versionshelf

import android.content.Context
import android.content.Intent
import android.content.pm.PackageInfo
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import java.io.File
import java.io.FileOutputStream
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URI
import java.net.URL
import java.security.MessageDigest

object DownloadInstaller {
    sealed interface Result {
        data class Ready(val apk: File) : Result
        data class Error(val message: String) : Result
    }

    fun downloadAndVerify(context: Context, app: InstalledApp, version: VersionRecord): Result {
        return try {
            validateHttps(version.apkUrl)
            val fileName = "${safePart(app.packageName)}-${version.versionCode}.apk"
            val directory = ApkFileProvider.downloadDirectory(context)
            val partial = File(directory, "$fileName.part")
            val completed = File(directory, fileName)
            partial.delete()
            completed.delete()
            streamToFile(version.apkUrl, partial)

            val actualChecksum = sha256(partial)
            if (!actualChecksum.equals(version.sha256, ignoreCase = true)) {
                partial.delete()
                return Result.Error("Checksum mismatch. The APK was removed.")
            }
            verifyArchive(context, partial, app, version)
            if (!partial.renameTo(completed)) throw IOException("Could not finalise the verified APK.")
            Result.Ready(completed)
        } catch (error: Exception) {
            Result.Error(error.message ?: "The APK could not be verified.")
        }
    }

    fun beginSystemInstall(context: Context, apk: File) {
        val packageManager = context.packageManager
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && !packageManager.canRequestPackageInstalls()) {
            context.startActivity(
                Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES)
                    .setData(Uri.parse("package:${context.packageName}"))
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
            )
            return
        }
        val apkUri = ApkFileProvider.uriFor(context, apk)
        context.startActivity(
            Intent(Intent.ACTION_VIEW)
                .setDataAndType(apkUri, "application/vnd.android.package-archive")
                .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
        )
    }

    @Suppress("DEPRECATION")
    private fun verifyArchive(context: Context, apk: File, app: InstalledApp, version: VersionRecord) {
        val packageManager = context.packageManager
        val flags = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            PackageManager.GET_SIGNING_CERTIFICATES
        } else {
            PackageManager.GET_SIGNATURES
        }
        val archiveInfo = packageManager.getPackageArchiveInfo(apk.absolutePath, flags)
            ?: throw IOException("Android cannot read this APK.")
        if (archiveInfo.packageName != app.packageName) {
            throw IOException("The downloaded APK is for ${archiveInfo.packageName}, not ${app.packageName}.")
        }
        val archiveVersionCode = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            archiveInfo.longVersionCode
        } else {
            archiveInfo.versionCode.toLong()
        }
        if (archiveVersionCode != version.versionCode) {
            throw IOException("APK version code does not match the selected catalogue entry.")
        }
        val archiveCerts = certificateDigests(archiveInfo)
        if (archiveCerts.isEmpty()) throw IOException("The APK has no readable signing certificate.")
        val catalogueCert = version.signingCertificateSha256?.lowercase()
        if (catalogueCert != null && catalogueCert !in archiveCerts) {
            throw IOException("Signing certificate does not match the catalogue.")
        }

        val installedInfo = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            packageManager.getPackageInfo(app.packageName, PackageManager.PackageInfoFlags.of(flags.toLong()))
        } else {
            packageManager.getPackageInfo(app.packageName, flags)
        }
        val installedCerts = certificateDigests(installedInfo)
        if (installedCerts.isNotEmpty() && archiveCerts.intersect(installedCerts).isEmpty()) {
            throw IOException("This APK is signed differently from the installed app, so it cannot safely update it.")
        }
    }

    @Suppress("DEPRECATION")
    private fun certificateDigests(packageInfo: PackageInfo): Set<String> {
        val signatures = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            packageInfo.signingInfo?.apkContentsSigners?.asList().orEmpty()
        } else {
            packageInfo.signatures?.asList().orEmpty()
        }
        return signatures.map { signature ->
            MessageDigest.getInstance("SHA-256")
                .digest(signature.toByteArray())
                .joinToString("") { "%02x".format(it) }
        }.toSet()
    }

    private fun streamToFile(url: String, destination: File) {
        val connection = openHttps(url)
        try {
            if (connection.responseCode !in 200..299) throw IOException("APK returned HTTP ${connection.responseCode}.")
            connection.inputStream.use { input ->
                FileOutputStream(destination).use { output -> input.copyTo(output, DEFAULT_BUFFER_SIZE) }
            }
        } finally {
            connection.disconnect()
        }
    }

    private fun sha256(file: File): String {
        val digest = MessageDigest.getInstance("SHA-256")
        file.inputStream().use { input ->
            val buffer = ByteArray(16 * 1024)
            while (true) {
                val count = input.read(buffer)
                if (count < 0) break
                digest.update(buffer, 0, count)
            }
        }
        return digest.digest().joinToString("") { "%02x".format(it) }
    }

    private fun openHttps(startUrl: String): HttpURLConnection {
        var next = startUrl
        repeat(MAX_REDIRECTS + 1) {
            validateHttps(next)
            val connection = (URL(next).openConnection() as HttpURLConnection).apply {
                connectTimeout = 15_000
                readTimeout = 120_000
                instanceFollowRedirects = false
                setRequestProperty("User-Agent", "VersionShelf/1.0")
            }
            if (connection.responseCode !in 300..399) return connection
            val location = connection.getHeaderField("Location") ?: throw IOException("APK redirect has no Location header.")
            next = URI(next).resolve(location).toString()
            connection.disconnect()
        }
        throw IOException("Too many APK redirects.")
    }

    private fun validateHttps(value: String) {
        val uri = try {
            URI(value)
        } catch (_: Exception) {
            throw IOException("Invalid APK URL.")
        }
        if (uri.scheme != "https" || uri.host.isNullOrBlank() || uri.userInfo != null) {
            throw IOException("Only valid HTTPS APK URLs are accepted.")
        }
    }

    private fun safePart(value: String): String = value.replace(Regex("[^A-Za-z0-9._-]"), "_")

    private const val MAX_REDIRECTS = 3
}
