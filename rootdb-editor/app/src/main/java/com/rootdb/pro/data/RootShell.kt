package com.rootdb.pro.data

import android.content.Context
import android.os.Process
import com.topjohnwu.superuser.Shell
import java.io.File

/** Результат выполнения root-команды */
data class ExecResult(val code: Int, val out: String, val err: String) {
    val ok: Boolean get() = code == 0
}

/**
 * Обёртка над libsu: все root-операции с файлами чужих приложений.
 * ВАЖНО: методы блокирующие — вызывать только с Dispatchers.IO.
 * Shell инициализируется лениво (при первом root-вызове), чтобы не
 * закэшировать non-root shell до выдачи root в Magisk/KernelSU.
 */
object RootShell {

    private var appContext: Context? = null

    fun init(context: Context) {
        appContext = context.applicationContext
    }

    private fun cacheDir(): File {
        val ctx = appContext ?: throw IllegalStateException("RootShell не инициализирован")
        return File(ctx.cacheDir, "rootwork").apply { mkdirs() }
    }

    /** Есть ли root (Magisk / KernelSU / SuperSU) */
    fun isRooted(): Boolean = try {
        Shell.getShell()
        Shell.isAppGrantedRoot() == true
    } catch (_: Throwable) {
        false
    }

    /** Выполнить команду от root */
    fun exec(cmd: String): ExecResult = try {
        val r = Shell.cmd(cmd).exec()
        ExecResult(r.code, r.out.joinToString("\n"), r.err.joinToString("\n"))
    } catch (t: Throwable) {
        ExecResult(-1, "", t.message ?: "ошибка root")
    }

    /** Экранирование пути для shell (одинарные кавычки) */
    fun q(path: String): String = "'" + path.replace("'", "'\\''") + "'"

    /** stat файла: размер, время, права, владелец. Запасной вариант — ls -ln. */
    fun stat(path: String): FileStat? {
        val r = exec("stat -c '%s|%Y|%a|%u|%g|%F' -- ${q(path)}")
        if (r.ok) {
            val p = r.out.trim().split("|")
            if (p.size >= 6) {
                return FileStat(
                    size = p[0].toLongOrNull() ?: 0,
                    mtimeSec = p[1].toLongOrNull() ?: 0,
                    mode = p[2].toIntOrNull() ?: 0,
                    uid = p[3].toIntOrNull() ?: 0,
                    gid = p[4].toIntOrNull() ?: 0,
                    type = p[5]
                )
            }
        }
        // Запасной вариант: ls -ln (числовые uid/gid, права текстом)
        val r2 = exec("ls -ln -- ${q(path)}")
        if (r2.ok) {
            val parts = r2.out.trim().split(Regex("\\s+"))
            if (parts.size >= 5) {
                val perms = parts[0]
                return FileStat(
                    size = parts[4].toLongOrNull() ?: 0,
                    mtimeSec = 0,
                    mode = permsToOctal(perms),
                    uid = parts[2].toIntOrNull() ?: 0,
                    gid = parts[3].toIntOrNull() ?: 0,
                    type = if (perms.startsWith("d")) "directory" else "regular file"
                )
            }
        }
        return null
    }

    /** "-rwxrwx---" -> 0o770 (упрощённо, без suid/sgid/sticky) */
    private fun permsToOctal(perms: String): Int {
        if (perms.length < 10) return 0
        var mode = 0
        val marks = charArrayOf('r', 'w', 'x')
        for (i in 0..2) {
            var bits = 0
            for (j in 0..2) {
                val c = perms[1 + i * 3 + j]
                if (c == marks[j] || c == 's' || c == 'S' || c == 't' || c == 'T') {
                    bits = bits or (1 shl (2 - j))
                }
            }
            mode = mode or (bits shl (6 - i * 3))
        }
        return mode
    }

    /** Список имён в каталоге (по одному на строку) */
    fun listNames(dir: String): List<String> {
        val r = exec("ls -1A -- ${q(dir)}")
        if (!r.ok) return emptyList()
        return r.out.split("\n").map { it.trimEnd('\r') }.filter { it.isNotEmpty() }
    }

    /**
     * Подробный список каталога. Сначала пробуем батч-stat,
     * при неудаче — пофайловые stat/ls (надёжность важнее скорости).
     */
    fun listDir(dir: String): List<DirEntry> {
        val names = listNames(dir)
        if (names.isEmpty()) return emptyList()
        val out = mutableListOf<DirEntry>()
        var batchFailed = false
        names.chunked(100).forEach { batch ->
            if (batchFailed) return@forEach
            val paths = batch.map { "$dir/$it" }
            val cmd = "stat -c '%n|%s|%Y|%F' -- " + paths.joinToString(" ") { q(it) }
            val r = exec(cmd)
            if (!r.ok) {
                batchFailed = true
                return@forEach
            }
            r.out.lines().forEach { line ->
                val p = line.split("|")
                if (p.size < 4) return@forEach
                val full = p[0]
                out += DirEntry(
                    name = full.substringAfterLast("/"),
                    path = full,
                    isDir = p[3].contains("directory", ignoreCase = true),
                    size = p[1].toLongOrNull() ?: 0,
                    modifiedSec = p[2].toLongOrNull() ?: 0
                )
            }
        }
        if (batchFailed || out.size != names.size) {
            out.clear()
            names.forEach { name ->
                val path = "$dir/$name"
                val st = stat(path)
                out += DirEntry(
                    name = name,
                    path = path,
                    isDir = st?.type?.contains("directory", ignoreCase = true) == true,
                    size = st?.size ?: 0,
                    modifiedSec = st?.mtimeSec ?: 0
                )
            }
        }
        return out.sortedWith(compareBy<DirEntry> { !it.isDir }.thenBy { it.name.lowercase() })
    }

    /** Рекурсивный поиск файлов через find (для сканирования files/ и cache/) */
    fun findFiles(dir: String, maxDepth: Int = 3, maxFiles: Int = 300): List<String> {
        val r = exec("find ${q(dir)} -maxdepth $maxDepth -type f 2>/dev/null")
        if (!r.ok) return emptyList()
        return r.out.split("\n").map { it.trimEnd('\r') }.filter { it.isNotEmpty() }.take(maxFiles)
    }

    /** Первые N байт файла (для hex-просмотра) — через dd во временный файл */
    fun headBytes(path: String, n: Int): ByteArray? {
        val tmp = File(cacheDir(), "head_${System.nanoTime()}.bin")
        val r = exec("dd if=${q(path)} of=${q(tmp.absolutePath)} bs=1 count=$n 2>/dev/null")
        if (!r.ok || !tmp.exists()) return null
        return try {
            tmp.readBytes().also { tmp.delete() }
        } catch (_: Exception) {
            tmp.delete()
            null
        }
    }

    /** Прочитать текстовый файл (первые maxBytes) через root */
    fun readText(path: String, maxBytes: Int = 200_000): String? {
        val tmp = File(cacheDir(), "read_${System.nanoTime()}.txt")
        val r = exec("dd if=${q(path)} of=${q(tmp.absolutePath)} bs=1024 count=${(maxBytes + 1023) / 1024} 2>/dev/null")
        if (!r.ok || !tmp.exists()) return null
        return try {
            val bytes = tmp.readBytes()
            tmp.delete()
            String(bytes, Charsets.UTF_8)
        } catch (_: Exception) {
            tmp.delete()
            null
        }
    }

    /** Скопировать файл приложения в нашу директорию и сделать доступным для чтения/записи */
    fun copyToAppDir(src: String, dst: File): Boolean {
        dst.parentFile?.mkdirs()
        val myUid = Process.myUid()
        val r = exec("cp -- ${q(src)} ${q(dst.absolutePath)} && chown $myUid:$myUid ${q(dst.absolutePath)} && chmod 660 ${q(dst.absolutePath)}")
        return r.ok && dst.exists()
    }

    /** Вернуть файл обратно в приложение с исходными правами и владельцем */
    fun copyBack(src: File, dstPath: String, mode: Int, uid: Int, gid: Int): Boolean {
        val r = exec(
            "cp -- ${q(src.absolutePath)} ${q(dstPath)} && " +
                "chown $uid:$gid ${q(dstPath)} && chmod $mode ${q(dstPath)}"
        )
        return r.ok
    }

    /** Записать текст в файл приложения через root (для SharedPreferences) */
    fun writeTextFile(path: String, content: String, mode: Int, uid: Int, gid: Int): Boolean {
        val tmp = File(cacheDir(), "write_${System.nanoTime()}.txt")
        tmp.writeText(content, Charsets.UTF_8)
        val ok = copyBack(tmp, path, mode, uid, gid)
        tmp.delete()
        return ok
    }

    /** Удалить файл через root */
    fun remove(path: String): Boolean = exec("rm -f -- ${q(path)}").ok

    /** Принудительно остановить приложение (чтобы оно не перезаписало правки) */
    fun forceStop(pkg: String): Boolean = exec("am force-stop -- $pkg").ok

    /** Запущено ли приложение */
    fun isRunning(pkg: String): Boolean {
        val r = exec("pidof -- $pkg")
        return r.ok && r.out.trim().isNotEmpty()
    }

    /** Размер каталога данных приложения (du) */
    fun dataSize(dataDir: String): Long {
        val r = exec("du -sb -- ${q(dataDir)}")
        if (!r.ok) return 0
        return r.out.trim().split(Regex("\\s+")).firstOrNull()?.toLongOrNull() ?: 0
    }

    /** Кто root-менеджер: Magisk / KernelSU / SuperSU / APatch */
    fun rootManager(): String {
        val checks = listOf(
            "magisk" to "/data/adb/magisk",
            "KernelSU" to "/data/adb/ksu",
            "APatch" to "/data/adb/ap",
            "SuperSU" to "/data/adb/supersu"
        )
        for ((name, path) in checks) {
            if (exec("test -e $path").ok) return name
        }
        return if (isRooted()) "Root" else "Нет root"
    }
}
