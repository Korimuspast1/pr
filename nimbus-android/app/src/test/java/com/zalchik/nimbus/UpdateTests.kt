package com.zalchik.nimbus

import com.zalchik.nimbus.engine.Update
import com.zalchik.nimbus.util.obj
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The app updates itself from its GitHub releases: pick the right APK for the device's ABI, check
 * its SHA-256 against the release's own checksum file, and only then hand it to the installer.
 * The desktop's UpdaterTests, with the phone's asset names.
 */
class UpdateTests {
    private val release = JSONObject(
        """
        {
          "tag_name": "v0.3.0",
          "draft": false,
          "prerelease": false,
          "body": "## Что нового\n- всё",
          "published_at": "2026-10-16T09:17:44Z",
          "html_url": "https://github.com/Korimuspast1/pr/releases/tag/v0.3.0",
          "assets": [
            {"name": "Nimbus-0.3.0-arm64-v8a.apk", "size": 5,
             "browser_download_url": "https://example.com/Nimbus-0.3.0-arm64-v8a.apk"},
            {"name": "Nimbus-0.3.0-universal.apk", "size": 7,
             "browser_download_url": "https://example.com/Nimbus-0.3.0-universal.apk"},
            {"name": "SHA256SUMS.txt", "size": 90,
             "browser_download_url": "https://example.com/SHA256SUMS.txt"}
          ]
        }
        """.trimIndent()
    )

    @Test
    fun versions() {
        assertEquals(listOf(0, 2, 10), Update.parseVersion("v0.2.10"))
        assertEquals(listOf(0, 3, 0), Update.parseVersion("0.3.0"))
        assertNull(Update.parseVersion("v0.3.0-beta"))
        assertNull(Update.parseVersion("latest"))
        assertNull(Update.parseVersion(null))

        assertTrue(Update.isNewer("v0.2.10", "0.2.9"))
        assertTrue(Update.isNewer("0.3", "0.2.9"))
        assertTrue(Update.isNewer("0.2.0.1", "0.2"))
        assertFalse(Update.isNewer("v0.2.0", "0.2.0"))
        assertFalse(Update.isNewer("0.2", "0.2.0"))
        assertFalse(Update.isNewer("0.1.9", "0.2.0"))
        assertFalse(Update.isNewer("nightly", "0.2.0"))
        assertFalse(Update.isNewer("0.3.0", "not a version"))
    }

    @Test
    fun releaseParsing() {
        val parsed = Update.parseRelease(release)
        assertNotNull(parsed)
        assertEquals("0.3.0", parsed!!.version)
        assertEquals("v0.3.0", parsed.tag)
        assertTrue(parsed.published > 0)
        assertEquals(3, parsed.assets.size)
        assertEquals(5, parsed.assets.getValue("Nimbus-0.3.0-arm64-v8a.apk").size)
        assertTrue(parsed.page.contains("releases"))
        assertTrue(Update.notes(parsed).any { it.contains("нового") })

        // Drafts and prereleases are not something to install on a phone unattended.
        assertNull(Update.parseRelease(JSONObject(release.toString()).put("draft", true)))
        assertNull(Update.parseRelease(JSONObject(release.toString()).put("prerelease", true)))
        assertNull(Update.parseRelease(JSONObject(release.toString()).put("tag_name", "nightly")))
        assertNull(Update.parseRelease(null))
        assertNull(Update.parseRelease(JSONObject()))
    }

    @Test
    fun theRightApkForTheDevice() {
        val parsed = Update.parseRelease(release)!!
        assertEquals("Nimbus-0.3.0-arm64-v8a.apk", Update.assetName(parsed, "arm64-v8a"))
        // An ABI the release didn't build for falls back to the universal APK.
        assertEquals("Nimbus-0.3.0-universal.apk", Update.assetName(parsed, "x86"))
        assertEquals("Nimbus-0.3.0-universal.apk", Update.assetName(parsed, "universal"))

        val noApk = Update.parseRelease(
            JSONObject(release.toString()).put("assets", JSONArray().put(
                obj("name" to "SHA256SUMS.txt", "browser_download_url" to "https://example.com/s", "size" to 1)
            ))
        )!!
        assertNull(Update.assetName(noApk, "arm64-v8a"))
    }

    @Test
    fun checksums() {
        val hash = "a".repeat(64)
        val text = "$hash  Nimbus-0.3.0-arm64-v8a.apk\r\n" +
            "${"B".repeat(64)} *Nimbus-0.3.0-universal.apk\n" +
            "garbage line\n"
        val parsed = Update.parseChecksums(text)
        assertEquals(2, parsed.size)
        assertEquals(hash, parsed["Nimbus-0.3.0-arm64-v8a.apk"])
        assertEquals("b".repeat(64), parsed["Nimbus-0.3.0-universal.apk"])
        assertTrue(Update.parseChecksums("").isEmpty())
    }

    @Test
    fun aDownloadIsVerifiedBeforeItIsInstalled() {
        val hash = "a".repeat(64)
        Update.verify(5, hash, 5, hash.uppercase())
        Update.verify(5, hash, 0, "")        // nothing known: nothing to check

        assertThrows(Update.UpdateError::class.java) { Update.verify(4, hash, 5, hash) }
        assertThrows(Update.UpdateError::class.java) { Update.verify(5, "c".repeat(64), 5, hash) }
    }

    @Test
    fun theRepositoryAndUrlsAreTheOnesBuiltIn() {
        assertTrue(Update.REPO.isNotEmpty())
        assertTrue(Update.API_URL.contains(Update.REPO))
        assertEquals("SHA256SUMS.txt", Update.CHECKSUMS)
        assertTrue(Update.CHECK_HOURS > 0)
    }
}
