package com.zalchik.nimbus.engine

import com.zalchik.nimbus.BuildConfig
import com.zalchik.nimbus.data.Paths
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.util.arrOrEmpty
import com.zalchik.nimbus.util.bool
import com.zalchik.nimbus.util.objects
import com.zalchik.nimbus.util.parseObject
import com.zalchik.nimbus.util.str
import com.zalchik.nimbus.util.toHexString
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.security.MessageDigest
import java.text.SimpleDateFormat
import java.util.Locale
import java.util.TimeZone
import java.util.concurrent.TimeUnit

/**
 * Updates from GitHub Releases: find a newer release, download its APK, check it, and hand it to
 * the installer — the port of nimbus/updater.py, with the desktop's five install kinds (setup,
 * portable, AppImage, deb, rpm) collapsed into the one a phone has: ask the system to install an
 * APK it can read through a FileProvider.
 */
object Update {
    val REPO: String get() = BuildConfig.UPDATE_REPO.ifEmpty { "Korimuspast1/pr" }
    val API_URL: String get() = "https://api.github.com/repos/$REPO/releases/latest"
    val RELEASES_URL: String get() = "https://github.com/$REPO/releases/latest"
    const val CHECKSUMS = "SHA256SUMS.txt"
    const val CHECK_HOURS = 6

    open class UpdateError(message: String) : Exception(message)
    class Cancelled(message: String) : UpdateError(message)

    data class Asset(val name: String, val url: String, val size: Long)

    data class Release(
        val version: String,
        val tag: String,
        val published: Long = 0,
        val body: String = "",
        val page: String = RELEASES_URL,
        val assets: Map<String, Asset> = emptyMap(),
    )

    fun workDir(): File = File(Paths.cacheDir, "update").apply { mkdirs() }

    /** "v0.2.1" -> [0, 2, 1]; null for anything that isn't a plain dotted version. */
    fun parseVersion(text: String?): List<Int>? {
        val match = VERSION.matchEntire((text ?: "").trim()) ?: return null
        return match.groupValues[1].split('.').mapNotNull { it.toIntOrNull() }
    }

    fun isNewer(version: String, current: String = Paths.VERSION): Boolean {
        val new = parseVersion(version) ?: return false
        val cur = parseVersion(current) ?: return false
        val width = maxOf(new.size, cur.size)
        for (index in 0 until width) {
            val mine = new.getOrElse(index) { 0 }
            val theirs = cur.getOrElse(index) { 0 }
            if (mine != theirs) return mine > theirs
        }
        return false
    }

    private val VERSION = Regex("\\s*v?(\\d+(?:\\.\\d+){0,3})\\s*")

    private fun time(stamp: String): Long = try {
        val format = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US)
        format.timeZone = TimeZone.getTimeZone("UTC")
        (format.parse(stamp)?.time ?: 0) / 1000
    } catch (e: Exception) {
        0
    }

    /** The GitHub API's release object; null for drafts, prereleases and odd tags. */
    fun parseRelease(json: org.json.JSONObject?): Release? {
        if (json == null) return null
        if (json.bool("draft") || json.bool("prerelease")) return null
        val tag = json.str("tag_name")
        val parts = parseVersion(tag) ?: return null
        val assets = LinkedHashMap<String, Asset>()
        for (asset in json.arrOrEmpty("assets").objects()) {
            val name = asset.str("name")
            val url = asset.str("browser_download_url")
            if (name.isNotEmpty() && url.isNotEmpty()) {
                assets[name] = Asset(name, url, asset.optLong("size", 0))
            }
        }
        return Release(
            version = parts.joinToString("."),
            tag = tag,
            published = time(json.str("published_at")),
            body = json.str("body"),
            page = json.str("html_url").ifEmpty { RELEASES_URL },
            assets = assets,
        )
    }

    fun client(): OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .followRedirects(true)
        .build()

    private fun request(url: String) = Request.Builder()
        .url(url)
        .header("User-Agent", "Nimbus/${Paths.VERSION}")
        .header("Accept", "application/vnd.github+json")
        .build()

    /** The newest published release, or null if there's none. */
    fun latest(client: OkHttpClient = client()): Release? {
        client.newCall(request(API_URL)).execute().use { response ->
            if (response.code == 404) return null
            if (!response.isSuccessful) throw UpdateError("HTTP ${response.code}")
            return parseRelease(parseObject(response.body?.string() ?: ""))
        }
    }

    /**
     * The APK this phone should install: its own ABI's split when the release has one, the
     * universal build otherwise (which is what CI publishes for sideloading).
     */
    fun assetName(release: Release, abi: String): String? {
        val names = release.assets.keys
        val exact = "Nimbus-${release.version}-$abi.apk"
        if (names.contains(exact)) return exact
        val universal = names.firstOrNull { it.endsWith("-universal.apk") }
            ?: names.firstOrNull { it.endsWith(".apk") && !it.contains("-") }
        return universal
    }

    /** sha256sum output ("<hex>  <name>" or "<hex> *<name>") -> {name: hex}. */
    fun parseChecksums(text: String): Map<String, String> {
        val out = LinkedHashMap<String, String>()
        for (line in text.lines()) {
            val match = CHECKSUM.matchEntire(line) ?: continue
            out[match.groupValues[2]] = match.groupValues[1].lowercase()
        }
        return out
    }

    private val CHECKSUM = Regex("\\s*([0-9a-fA-F]{64})\\s+\\*?(.+?)\\s*")

    /** Raise if the file isn't what the release says (wantSize 0 = unknown). */
    fun verify(size: Long, digest: String, wantSize: Long, wantDigest: String = "") {
        if (wantSize > 0 && size != wantSize) {
            throw UpdateError(
                I18n.tr(
                    "файл скачался не полностью ({size} из {want_size} байт)",
                    values = mapOf("size" to size.toString(), "want_size" to wantSize.toString()),
                )
            )
        }
        if (wantDigest.isNotEmpty() && digest.lowercase() != wantDigest.lowercase()) {
            throw UpdateError(I18n.tr("контрольная сумма SHA-256 не совпадает: файл повреждён или подменён"))
        }
    }

    /** SHA-256 of `name` from the release's checksum file; "" if the release has none. */
    fun checksum(release: Release, name: String, client: OkHttpClient = client()): String {
        val asset = release.assets[CHECKSUMS] ?: return ""
        client.newCall(request(asset.url)).execute().use { response ->
            if (!response.isSuccessful) throw UpdateError("HTTP ${response.code}")
            val digest = parseChecksums(response.body?.string() ?: "")[name] ?: ""
            if (digest.isEmpty()) {
                throw UpdateError(I18n.tr("в {file} нет {name}", values = mapOf("file" to CHECKSUMS, "name" to name)))
            }
            return digest
        }
    }

    /** Download the release's APK into the cache dir and check its size and SHA-256. */
    fun download(
        release: Release,
        abi: String,
        client: OkHttpClient = client(),
        progress: ((done: Long, total: Long) -> Unit)? = null,
        cancelled: () -> Boolean = { false },
    ): File {
        val name = assetName(release, abi)
            ?: throw UpdateError(I18n.tr("в релизе {tag} нет APK", values = mapOf("tag" to release.tag)))
        val asset = release.assets[name]
            ?: throw UpdateError(
                I18n.tr("в релизе {tag} нет файла {name}", values = mapOf("tag" to release.tag, "name" to name))
            )
        val wantDigest = checksum(release, name, client)
        val dest = File(workDir(), name)
        val part = File(workDir(), "$name.part")
        val digest = MessageDigest.getInstance("SHA-256")
        var done = 0L
        var last = 0L
        try {
            client.newCall(request(asset.url)).execute().use { response ->
                if (!response.isSuccessful) throw UpdateError("HTTP ${response.code}")
                val total = if (asset.size > 0) asset.size else response.header("content-length")?.toLongOrNull() ?: 0L
                part.outputStream().use { out ->
                    val body = response.body ?: throw UpdateError("empty response")
                    body.byteStream().use { input ->
                        val buffer = ByteArray(1 shl 16)
                        while (true) {
                            if (cancelled()) throw Cancelled(I18n.tr("загрузка отменена"))
                            val read = input.read(buffer)
                            if (read < 0) break
                            out.write(buffer, 0, read)
                            digest.update(buffer, 0, read)
                            done += read
                            val now = System.nanoTime()
                            if (progress != null && (now - last > 100_000_000 || done == total)) {
                                last = now
                                progress(done, total)
                            }
                        }
                    }
                }
            }
            verify(done, digest.digest().toHexString(), asset.size, wantDigest)
            part.renameTo(dest)
        } finally {
            part.delete()
        }
        return dest
    }

    /** What the update sheet shows: the release's own notes, trimmed to the top of the list. */
    fun notes(release: Release): List<String> = release.body.lines()
        .map { it.trim() }
        .filter { it.isNotEmpty() && !it.startsWith("<!--") }
        .take(20)
}
