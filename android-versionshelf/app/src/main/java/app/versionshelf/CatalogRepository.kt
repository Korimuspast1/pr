package app.versionshelf

import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedInputStream
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URI
import java.net.URL
import java.util.zip.GZIPInputStream

/**
 * Reads only HTTPS catalogues. F-Droid's official v2 repository index is supported out of the box;
 * a deliberately small signed-by-hash private catalogue format is supported for apps an operator owns.
 */
class CatalogRepository {
    fun fetch(config: CatalogConfig, requestedPackages: Set<String>): CatalogSnapshot {
        require(requestedPackages.isNotEmpty()) { "No installed apps were supplied." }
        validateHttps(config.url)
        val json = JSONObject(fetchUtf8(config.url))
        return when (config.type) {
            CatalogType.FDROID -> parseFdroid(json, config.url, requestedPackages)
            CatalogType.CUSTOM -> parsePrivateCatalogue(json, requestedPackages)
        }
    }

    private fun parseFdroid(
        root: JSONObject,
        indexUrl: String,
        requestedPackages: Set<String>,
    ): CatalogSnapshot {
        val packages = root.optJSONObject("packages")
            ?: throw IOException("This is not an F-Droid v2 index: packages is missing.")
        val repositoryBase = indexUrl.substringBeforeLast('/') + "/"
        val output = linkedMapOf<String, MutableList<VersionRecord>>()

        for (packageName in requestedPackages) {
            val packageEntry = packages.optJSONObject(packageName) ?: continue
            val versions = packageEntry.optJSONObject("versions") ?: continue
            val keys = versions.keys()
            while (keys.hasNext()) {
                val version = versions.optJSONObject(keys.next()) ?: continue
                val manifest = version.optJSONObject("manifest") ?: continue
                val file = version.optJSONObject("file") ?: continue
                val fileName = file.optString("name").trimStart('/')
                val checksum = file.optString("sha256")
                val versionCode = manifest.optLong("versionCode", -1L)
                if (fileName.isBlank() || checksum.isBlank() || versionCode < 0L) continue

                val signingHash = version.optJSONObject("signer")?.optString("sha256")
                    ?.takeIf { it.isNotBlank() }
                if (!isSha256(checksum) || (signingHash != null && !isSha256(signingHash)) ||
                    fileName.contains("..") || fileName.contains('?') || fileName.contains('#') || !fileName.endsWith(".apk")) {
                    continue
                }
                output.getOrPut(packageName) { mutableListOf() }.add(
                    VersionRecord(
                        packageName = packageName,
                        versionName = manifest.optString("versionName", versionCode.toString()),
                        versionCode = versionCode,
                        releasedAt = version.optString("added").takeIf { it.isNotBlank() },
                        apkUrl = repositoryBase + fileName,
                        sha256 = checksum,
                        signingCertificateSha256 = signingHash,
                        sourceLabel = "F-Droid",
                    ),
                )
            }
        }
        return CatalogSnapshot(
            sourceLabel = "F-Droid",
            versionsByPackage = output.mapValues { (_, items) -> items.sortedByDescending { it.versionCode } },
        )
    }

    /**
     * Private catalogue schema:
     * {"schema":1,"apps":[{"packageId":"org.example.app","versions":[{
     * "versionName":"2.1","versionCode":201,"apkUrl":"https://…/app.apk",
     * "sha256":"64 hex chars","signingCertificateSha256":"64 hex chars","releasedAt":"2026-01-14"}]}]}
     */
    private fun parsePrivateCatalogue(
        root: JSONObject,
        requestedPackages: Set<String>,
    ): CatalogSnapshot {
        if (root.optInt("schema", 0) != 1) {
            throw IOException("Private catalogue schema must be 1.")
        }
        val apps = root.optJSONArray("apps") ?: throw IOException("Private catalogue has no apps array.")
        val output = linkedMapOf<String, MutableList<VersionRecord>>()
        for (i in 0 until apps.length()) {
            val app = apps.optJSONObject(i) ?: continue
            val packageName = app.optString("packageId")
            if (packageName !in requestedPackages) continue
            val versions = app.optJSONArray("versions") ?: continue
            for (j in 0 until versions.length()) {
                val version = versions.optJSONObject(j) ?: continue
                val versionCode = version.optLong("versionCode", -1L)
                val apkUrl = version.optString("apkUrl")
                val checksum = version.optString("sha256")
                val cert = version.optString("signingCertificateSha256").takeIf { it.isNotBlank() }
                if (versionCode < 0L || apkUrl.isBlank() || !isSha256(checksum)) continue
                validateHttps(apkUrl)
                if (cert != null && !isSha256(cert)) continue
                output.getOrPut(packageName) { mutableListOf() }.add(
                    VersionRecord(
                        packageName = packageName,
                        versionName = version.optString("versionName", versionCode.toString()),
                        versionCode = versionCode,
                        releasedAt = version.optString("releasedAt").takeIf { it.isNotBlank() },
                        apkUrl = apkUrl,
                        sha256 = checksum,
                        signingCertificateSha256 = cert,
                        sourceLabel = "Private catalogue",
                    ),
                )
            }
        }
        return CatalogSnapshot(
            sourceLabel = "Private catalogue",
            versionsByPackage = output.mapValues { (_, items) -> items.sortedByDescending { it.versionCode } },
        )
    }

    private fun fetchUtf8(url: String): String {
        val connection = openHttps(url)
        try {
            val status = connection.responseCode
            if (status !in 200..299) throw IOException("Catalogue returned HTTP $status.")
            val stream = BufferedInputStream(connection.inputStream)
            val decoded = if (connection.contentEncoding.equals("gzip", ignoreCase = true)) GZIPInputStream(stream) else stream
            decoded.use { input ->
                val bytes = ByteArrayOutputStream()
                val buffer = ByteArray(16 * 1024)
                var total = 0
                while (true) {
                    val count = input.read(buffer)
                    if (count < 0) break
                    total += count
                    if (total > MAX_INDEX_BYTES) throw IOException("Catalogue is larger than 45 MB.")
                    bytes.write(buffer, 0, count)
                }
                return bytes.toString(Charsets.UTF_8.name())
            }
        } finally {
            connection.disconnect()
        }
    }

    private fun openHttps(startUrl: String): HttpURLConnection {
        var next = startUrl
        repeat(MAX_REDIRECTS + 1) {
            validateHttps(next)
            val connection = (URL(next).openConnection() as HttpURLConnection).apply {
                connectTimeout = CONNECT_TIMEOUT_MS
                readTimeout = READ_TIMEOUT_MS
                instanceFollowRedirects = false
                setRequestProperty("Accept-Encoding", "gzip")
                setRequestProperty("User-Agent", "VersionShelf/1.0")
            }
            val code = connection.responseCode
            if (code !in 300..399) return connection
            val location = connection.getHeaderField("Location")
                ?: throw IOException("Redirect response has no Location header.")
            next = URI(next).resolve(location).toString()
            connection.disconnect()
        }
        throw IOException("Too many catalogue redirects.")
    }

    private fun validateHttps(value: String) {
        val uri = try {
            URI(value)
        } catch (_: Exception) {
            throw IOException("Invalid catalogue URL.")
        }
        if (uri.scheme != "https" || uri.host.isNullOrBlank() || uri.userInfo != null) {
            throw IOException("Only valid HTTPS URLs are accepted.")
        }
    }

    private fun isSha256(value: String): Boolean = SHA_256.matches(value)

    private companion object {
        const val CONNECT_TIMEOUT_MS = 15_000
        const val READ_TIMEOUT_MS = 35_000
        const val MAX_INDEX_BYTES = 45 * 1024 * 1024
        const val MAX_REDIRECTS = 3
        val SHA_256 = Regex("^[a-fA-F0-9]{64}$")
    }
}
