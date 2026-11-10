package com.rootdb.pro.data

import android.content.Context
import android.content.pm.ApplicationInfo
import android.content.pm.PackageManager

/** Сканер приложений и их локальных баз данных (через root) */
object AppScanner {

    private val SQLITE_HEADER = "SQLite format 3".toByteArray(Charsets.US_ASCII)

    /** Все установленные приложения */
    fun listApps(context: Context): List<AppInfo> {
        val pm = context.packageManager
        val apps = pm.getInstalledApplications(PackageManager.GET_META_DATA)
        return apps.map { ai ->
            val label = try {
                pm.getApplicationLabel(ai).toString()
            } catch (_: Exception) {
                ai.packageName
            }
            AppInfo(
                pkg = ai.packageName,
                label = label,
                icon = try {
                    pm.getApplicationIcon(ai)
                } catch (_: Exception) {
                    null
                },
                isSystem = (ai.flags and ApplicationInfo.FLAG_SYSTEM) != 0,
                versionName = try {
                    pm.getPackageInfo(ai.packageName, 0).versionName
                } catch (_: Exception) {
                    null
                },
                dataDir = ai.dataDir ?: "/data/data/${ai.packageName}"
            )
        }.sortedBy { it.label.lowercase() }
    }

    /** Заголовок файла — SQLite? (быстрая проверка первых 15 байт) */
    fun isSqliteFile(path: String): Boolean {
        val r = RootShell.exec("head -c 15 -- ${RootShell.q(path)} 2>/dev/null")
        return r.ok && r.out.startsWith("SQLite format 3")
    }

    /** Похоже на зашифрованную базу (SQLCipher): не SQLite-заголовок, но файл непустой */
    fun looksEncrypted(path: String, size: Long): Boolean {
        if (size < 512) return false
        return !isSqliteFile(path)
    }

    /**
     * Полное сканирование баз приложения:
     * databases/ (все файлы), files/ и cache/ (рекурсивно до 2 уровней).
     */
    fun scanDatabases(pkg: String): List<DbFileInfo> {
        val base = "/data/data/$pkg"
        val found = mutableListOf<DbFileInfo>()

        // 1) databases/ — главный каталог
        RootShell.listDir("$base/databases").forEach { e ->
            if (e.isDir) return@forEach
            found += classify(e.path, e.size, e.modifiedSec)
        }

        // 2) files/ и cache/ — ищем SQLite глубже
        listOf("files", "cache").forEach { sub ->
            val root = "$base/$sub"
            var visited = 0
            fun walk(dir: String, depth: Int) {
                if (depth > 2 || visited > 400) return
                RootShell.listDir(dir).forEach { e ->
                    visited++
                    if (e.isDir) {
                        walk(e.path, depth + 1)
                    } else if (isDbName(e.name)) {
                        found += classify(e.path, e.size, e.modifiedSec)
                    }
                }
            }
            walk(root, 0)
        }

        // 3) shared_prefs не базы, но показываем отдельно в UI; здесь только БД
        return found.distinctBy { it.path }.sortedByDescending { it.size }
    }

    private fun isDbName(name: String): Boolean {
        val n = name.lowercase()
        return n.endsWith(".db") || n.endsWith(".sqlite") || n.endsWith(".sqlite3") ||
            n.endsWith(".db3") || n.endsWith(".sdb") || n.endsWith(".realm")
    }

    private fun classify(path: String, size: Long, modifiedSec: Long): DbFileInfo {
        val name = path.substringAfterLast("/")
        val lower = name.lowercase()
        val isWal = lower.endsWith("-wal")
        val isShm = lower.endsWith("-shm")
        val isJournal = lower.endsWith("-journal")
        val sqlite = isSqliteFile(path)
        val encrypted = !sqlite && !isWal && !isShm && !isJournal && size >= 512
        return DbFileInfo(
            name = name,
            path = path,
            size = size,
            modifiedSec = modifiedSec,
            isSqlite = sqlite,
            isEncrypted = encrypted,
            isWal = isWal,
            isShm = isShm,
            isJournal = isJournal
        )
    }

    /** Список SharedPreferences-файлов приложения */
    fun listPrefs(pkg: String): List<DirEntry> =
        RootShell.listDir("/data/data/$pkg/shared_prefs").filter { !it.isDir }

    /** Размер данных приложения */
    fun dataSize(pkg: String): Long = RootShell.dataSize("/data/data/$pkg")
}
