package com.rootdb.pro.data

import android.graphics.drawable.Drawable

/** Информация об установленном приложении */
data class AppInfo(
    val pkg: String,
    val label: String,
    val icon: Drawable?,
    val isSystem: Boolean,
    val versionName: String?,
    val dataDir: String
)

/** Файл базы данных, найденный при сканировании */
data class DbFileInfo(
    val name: String,
    val path: String,
    val size: Long,
    val modifiedSec: Long,
    val isSqlite: Boolean,
    val isEncrypted: Boolean,
    val isWal: Boolean = false,
    val isShm: Boolean = false,
    val isJournal: Boolean = false
)

/** Результат stat(1) по файлу */
data class FileStat(
    val size: Long,
    val mtimeSec: Long,
    val mode: Int,
    val uid: Int,
    val gid: Int,
    val type: String
)

/** Запись каталога */
data class DirEntry(
    val name: String,
    val path: String,
    val isDir: Boolean,
    val size: Long,
    val modifiedSec: Long
)

/** Таблица SQLite */
data class TableInfo(
    val name: String,
    val rowCount: Long,
    val sql: String?
)

/** Колонка таблицы */
data class ColumnInfo(
    val name: String,
    val type: String,
    val notNull: Boolean,
    val defaultValue: String?,
    val isPk: Boolean
)

/** Строка таблицы как упорядоченная карта колонка -> значение */
typealias RowMap = LinkedHashMap<String, Any?>

/** Результат выполнения SQL */
sealed interface SqlOutcome {
    data class Rows(val columns: List<String>, val rows: List<RowMap>) : SqlOutcome
    data class Affected(val count: Int) : SqlOutcome
    data class Error(val message: String) : SqlOutcome
}

/** Найденное значение при глобальном поиске по базам */
data class SearchHit(
    val dbName: String,
    val dbPath: String,
    val table: String,
    val column: String,
    val valuePreview: String,
    val rowId: Long?
)

/** Режим работы Guardian для одной базы */
enum class WatchMode { RESTORE, KILL, NOTIFY }

/** Отслеживаемая база в Guardian */
data class WatchItem(
    val pkg: String,
    val dbPath: String,
    val backupPath: String,
    val mode: WatchMode,
    val label: String
)

/** Событие Guardian (лог) */
data class GuardianEvent(
    val timeMs: Long,
    val text: String
)

/** Расшифрованное значение (декодеры) */
data class DecodedValue(val kind: String, val text: String)

/** Результат расшифровки SQLCipher-базы */
data class DecryptResult(val ok: Boolean, val message: String, val outFile: String? = null)

/** Форматирование размеров */
fun formatSize(bytes: Long): String {
    if (bytes < 1024) return "$bytes Б"
    val kb = bytes / 1024.0
    if (kb < 1024) return "%.1f КБ".format(kb)
    val mb = kb / 1024.0
    if (mb < 1024) return "%.1f МБ".format(mb)
    val gb = mb / 1024.0
    return "%.2f ГБ".format(gb)
}

fun formatDateTime(sec: Long): String {
    if (sec <= 0) return "—"
    val d = java.util.Date(sec * 1000)
    return java.text.SimpleDateFormat("dd.MM.yyyy HH:mm", java.util.Locale.getDefault()).format(d)
}
