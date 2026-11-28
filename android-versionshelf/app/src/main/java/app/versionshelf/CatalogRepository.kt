package app.versionshelf

import android.util.JsonReader
import android.util.JsonToken
import org.json.JSONObject
import java.io.BufferedInputStream
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.io.InputStreamReader
import java.net.HttpURLConnection
import java.net.URI
import java.net.URL
import java.util.zip.GZIPInputStream

/**
 * Reads only HTTPS catalogues. F-Droid's official v2 repository index is processed as a stream,
 * so the full (and increasingly large) index is never retained in the app's memory.
 */
class CatalogRepository {
    fun fetch(config: CatalogConfig, requestedPackages: Set<String>): CatalogSnapshot {
        require(requestedPackages.isNotEmpty()) { "No installed apps were supplied." }
        validateHttps(config.url)
        return when (config.type) {
            CatalogType.FDROID -> parseFdroidIndex(config.url, requestedPackages)
            CatalogType.CUSTOM -> parsePrivateCatalogue(JSONObject(fetchPrivateCatalogueUtf8(config.url)), requestedPackages)
        }
    }

    private fun parseFdroidIndex(indexUrl: String, requestedPackages: Set<String>): CatalogSnapshot {
        val repositoryBase = indexUrl.substringBeforeLast('/') + "/"
        val output = linkedMapOf<String, MutableList<VersionRecord>>()
        var packagesFound = false

        withIndexReader(indexUrl) { reader ->
            reader.beginObject()
            while (reader.hasNext()) {
                when (reader.nextName()) {
                    "packages" -> {
                        packagesFound = true
                        parseFdroidPackages(reader, requestedPackages, repositoryBase, output)
                    }
                    else -> reader.skipValue()
                }
            }
            reader.endObject()
        }

        if (!packagesFound) throw IOException("This is not an F-Droid v2 index: packages is missing.")
        return CatalogSnapshot(
            sourceLabel = "F-Droid",
            versionsByPackage = output.mapValues { (_, items) -> items.sortedByDescending { it.versionCode } },
        )
    }

    private fun parseFdroidPackages(
        reader: JsonReader,
        requestedPackages: Set<String>,
        repositoryBase: String,
        output: MutableMap<String, MutableList<VersionRecord>>,
    ) {
        reader.beginObject()
        while (reader.hasNext()) {
            val packageName = reader.nextName()
            if (packageName !in requestedPackages) {
                reader.skipValue()
                continue
            }
            parseFdroidPackage(reader, packageName, repositoryBase, output)
        }
        reader.endObject()
    }

    private fun parseFdroidPackage(
        reader: JsonReader,
        packageName: String,
        repositoryBase: String,
        output: MutableMap<String, MutableList<VersionRecord>>,
    ) {
        reader.beginObject()
        while (reader.hasNext()) {
            if (reader.nextName() == "versions") {
                parseFdroidVersions(reader, packageName, repositoryBase, output)
            } else {
                reader.skipValue()
            }
        }
        reader.endObject()
    }

    private fun parseFdroidVersions(
        reader: JsonReader,
        packageName: String,
        repositoryBase: String,
        output: MutableMap<String, MutableList<VersionRecord>>,
    ) {
        reader.beginObject()
        while (reader.hasNext()) {
            reader.nextName() // F-Droid's version hash is not required after integrity validation.
            val raw = parseFdroidVersion(reader) ?: continue
            if (!isSafeFdroidArtifact(raw.fileName, raw.checksum)) continue
            if (raw.signingHash != null && !isSha256(raw.signingHash)) continue
            output.getOrPut(packageName) { mutableListOf() }.add(
                VersionRecord(
                    packageName = packageName,
                    versionName = raw.versionName ?: raw.versionCode.toString(),
                    versionCode = raw.versionCode,
                    releasedAt = raw.added,
                    apkUrl = repositoryBase + raw.fileName.trimStart('/'),
                    sha256 = raw.checksum,
                    signingCertificateSha256 = raw.signingHash,
                    sourceLabel = "F-Droid",
                ),
            )
        }
        reader.endObject()
    }

    private fun parseFdroidVersion(reader: JsonReader): FdroidVersion? {
        var fileName: String? = null
        var checksum: String? = null
        var versionCode: Long? = null
        var versionName: String? = null
        var signingHash: String? = null
        var added: String? = null

        reader.beginObject()
        while (reader.hasNext()) {
            when (reader.nextName()) {
                "file" -> {
                    reader.beginObject()
                    while (reader.hasNext()) {
                        when (reader.nextName()) {
                            "name" -> fileName = reader.nextScalar()
                            "sha256" -> checksum = reader.nextScalar()
                            else -> reader.skipValue()
                        }
                    }
                    reader.endObject()
                }
                "manifest" -> {
                    reader.beginObject()
                    while (reader.hasNext()) {
                        when (reader.nextName()) {
                            "versionCode" -> versionCode = reader.nextScalar()?.toLongOrNull()
                            "versionName" -> versionName = reader.nextScalar()
                            else -> reader.skipValue()
                        }
                    }
                    reader.endObject()
                }
                "signer" -> {
                    reader.beginObject()
                    while (reader.hasNext()) {
                        if (reader.nextName() == "sha256") signingHash = reader.nextScalar() else reader.skipValue()
                    }
                    reader.endObject()
                }
                "added" -> added = reader.nextScalar()
                else -> reader.skipValue()
            }
        }
        reader.endObject()

        val name = fileName ?: return null
        val hash = checksum ?: return null
        val code = versionCode ?: return null
        return FdroidVersion(name, hash, code, versionName, signingHash, added)
    }

    private fun JsonReader.nextScalar(): String? = when (peek()) {
        JsonToken.STRING, JsonToken.NUMBER -> nextString()
        JsonToken.NULL -> {
            nextNull()
            null
        }
        else -> {
            skipValue()
            null
        }
    }

    private fun isSafeFdroidArtifact(fileName: String, checksum: String): Boolean {
        val trimmed = fileName.trimStart('/')
        return isSha256(checksum) && trimmed.isNotBlank() && !trimmed.contains("..") &&
            !trimmed.contains('?') && !trimmed.contains('#') && trimmed.endsWith(".apk", ignoreCase = true)
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

    private fun withIndexReader(url: String, action: (JsonReader) -> Unit) {
        val connection = openHttps(url)
        try {
            val status = connection.responseCode
            if (status !in 200..299) throw IOException("Catalogue returned HTTP $status.")
            val stream = BufferedInputStream(connection.inputStream)
            val decoded = if (connection.contentEncoding.equals("gzip", ignoreCase = true)) GZIPInputStream(stream) else stream
            JsonReader(InputStreamReader(decoded, Charsets.UTF_8)).use(action)
        } finally {
            connection.disconnect()
        }
    }

    private fun fetchPrivateCatalogueUtf8(url: String): String {
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
                    if (total > MAX_PRIVATE_CATALOGUE_BYTES) throw IOException("Private catalogue is larger than 8 MB.")
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

    private data class FdroidVersion(
        val fileName: String,
        val checksum: String,
        val versionCode: Long,
        val versionName: String?,
        val signingHash: String?,
        val added: String?,
    )

    private companion object {
        const val CONNECT_TIMEOUT_MS = 15_000
        const val READ_TIMEOUT_MS = 35_000
        const val MAX_PRIVATE_CATALOGUE_BYTES = 8 * 1024 * 1024
        const val MAX_REDIRECTS = 3
        val SHA_256 = Regex("^[a-fA-F0-9]{64}$")
    }
}
