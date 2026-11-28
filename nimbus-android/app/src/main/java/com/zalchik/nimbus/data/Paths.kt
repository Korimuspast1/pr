package com.zalchik.nimbus.data

import android.content.Context
import com.zalchik.nimbus.BuildConfig
import java.io.File

/**
 * Where Nimbus keeps its data — the phone's counterpart of desktop's nimbus/paths.py.
 *
 * The layout and the file names are the same as on the computer (settings.json, profiles.json,
 * logs/, run/), so the files can be copied between the two: "Import data from a computer" reads
 * a profiles.json straight out of ~/.config/Nimbus.
 */
object Paths {
    const val APP_NAME = "Nimbus"
    const val VERSION: String = BuildConfig.VERSION_NAME

    /** App-private files dir; a JVM test that never calls [init] gets a temporary one. */
    @Volatile
    var dataDir: File = File(System.getProperty("java.io.tmpdir"), "nimbus-test-data")
        private set

    @Volatile
    var cacheDir: File = dataDir
        private set

    val settingsFile: File get() = File(dataDir, "settings.json")
    val profilesFile: File get() = File(dataDir, "profiles.json")
    val deviceIdFile: File get() = File(dataDir, "device_id")
    val logDir: File get() = File(dataDir, "logs")
    val runDir: File get() = File(dataDir, "run")
    val mainLog: File get() = File(logDir, "nimbus.log")
    val configDump: File get() = File(runDir, "config.json")

    fun init(context: Context) {
        dataDir = context.filesDir
        cacheDir = context.cacheDir
        for (dir in listOf(dataDir, logDir, runDir, cacheDir)) dir.mkdirs()
    }

    /** For tests and for the "import data" screen: point the store somewhere else. */
    fun useDataDir(dir: File) {
        dir.mkdirs()
        dataDir = dir
        logDir.mkdirs()
        runDir.mkdirs()
    }

    /** Write through a temporary file and rename, as desktop does: no half-written settings. */
    fun writeAtomic(file: File, text: String) {
        file.parentFile?.mkdirs()
        val tmp = File(file.parentFile, file.name + ".tmp")
        tmp.writeText(text, Charsets.UTF_8)
        if (file.exists()) file.delete()
        if (!tmp.renameTo(file)) {
            file.writeText(text, Charsets.UTF_8)
            tmp.delete()
        }
    }

    fun readOrNull(file: File): String? = try {
        if (file.isFile) file.readText(Charsets.UTF_8) else null
    } catch (_: Exception) {
        null
    }
}
