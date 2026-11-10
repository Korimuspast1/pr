package com.rootdb.pro.data

import android.content.ContentValues
import android.content.Context
import android.database.Cursor
import java.io.File

class DbException(message: String) : Exception(message)

/** Единый интерфейс над обычной и SQLCipher-базой */
interface DbHandle {
    fun rawQuery(sql: String, args: Array<String>? = null): Cursor
    fun execSQL(sql: String, bindArgs: Array<Any?>? = null)
    fun insert(table: String, values: ContentValues): Long
    fun close()
    fun isOpen(): Boolean
}

class PlainDbHandle(val db: android.database.sqlite.SQLiteDatabase) : DbHandle {
    override fun rawQuery(sql: String, args: Array<String>?): Cursor = db.rawQuery(sql, args)
    override fun execSQL(sql: String, bindArgs: Array<Any?>?) {
        if (bindArgs == null) db.execSQL(sql) else db.execSQL(sql, bindArgs)
    }
    override fun insert(table: String, values: ContentValues): Long = db.insert(table, null, values)
    override fun close() = db.close()
    override fun isOpen(): Boolean = db.isOpen
}

class CipherDbHandle(val db: net.sqlcipher.database.SQLiteDatabase) : DbHandle {
    override fun rawQuery(sql: String, args: Array<String>?): Cursor = db.rawQuery(sql, args)
    override fun execSQL(sql: String, bindArgs: Array<Any?>?) {
        if (bindArgs == null) db.execSQL(sql) else db.execSQL(sql, bindArgs)
    }
    override fun insert(table: String, values: ContentValues): Long = db.insert(table, null, values)
    override fun close() = db.close()
    override fun isOpen(): Boolean = db.isOpen
}

/** Открытая для редактирования копия базы */
data class OpenedDb(
    val workFile: File,
    val origPath: String,
    val origStat: FileStat?,
    val db: DbHandle,
    val encrypted: Boolean
)

/** Строка с rowid (если есть) */
data class RowData(val rowId: Long?, val values: RowMap)

/**
 * Работа с базами: копируем базу через root в кэш, редактируем локально,
 * затем возвращаем обратно с исходными правами и владельцем.
 */
object DbManager {

    @Volatile
    private var libsLoaded = false

    private fun ensureLibs(context: Context) {
        if (!libsLoaded) {
            net.sqlcipher.database.SQLiteDatabase.loadLibs(context)
            libsLoaded = true
        }
    }

    fun workDir(context: Context, pkg: String, dbName: String, tag: String = "main"): File {
        val dir = File(context.cacheDir, "dbs/$pkg/$dbName/$tag")
        dir.mkdirs()
        return dir
    }

    fun backupsDir(context: Context): File {
        val dir = File(context.filesDir, "backups")
        dir.mkdirs()
        return dir
    }

    /** Открыть базу для редактирования (копия в кэше). password — для SQLCipher. */
    fun openForEdit(context: Context, pkg: String, dbPath: String, password: String? = null, tag: String = "main"): OpenedDb {
        val dbName = dbPath.substringAfterLast("/")
        val dir = workDir(context, pkg, dbName, tag)
        val work = File(dir, dbName)
        // Чистим старую копию
        work.delete()
        File(dir, "$dbName-wal").delete()
        File(dir, "$dbName-shm").delete()

        val stat = RootShell.stat(dbPath)
        if (!RootShell.copyToAppDir(dbPath, work)) {
            throw DbException("Не удалось скопировать базу через root. Проверьте root-доступ.")
        }
        // WAL/SHM — для консистентности копии
        listOf("-wal", "-shm").forEach { suf ->
            val src = File(dbPath + suf)
            val dst = File(dir, dbName + suf)
            if (src.exists()) RootShell.copyToAppDir(src.absolutePath, dst)
        }

        val head = work.readBytes().take(16).toByteArray()
        val isSqlite = head.size >= 16 && String(head.copyOfRange(0, 15), Charsets.US_ASCII) == "SQLite format 3"
        return try {
            if (isSqlite) {
                val db = android.database.sqlite.SQLiteDatabase.openDatabase(
                    work.absolutePath, null, android.database.sqlite.SQLiteDatabase.OPEN_READWRITE, null
                )
                OpenedDb(work, dbPath, stat, PlainDbHandle(db), encrypted = false)
            } else {
                if (password.isNullOrEmpty()) {
                    throw DbException("База зашифрована (SQLCipher). Нужен ключ/пароль — откройте экран расшифровки.")
                }
                val db = openCipher(context, work.absolutePath, password)
                OpenedDb(work, dbPath, stat, CipherDbHandle(db), encrypted = true)
            }
        } catch (e: DbException) {
            throw e
        } catch (e: Exception) {
            throw DbException("Не удалось открыть базу: ${e.message}")
        }
    }

    /** Открыть SQLCipher-базу с проверкой ключа (пароль или hex-ключ) */
    private fun openCipher(context: Context, path: String, password: String): net.sqlcipher.database.SQLiteDatabase {
        ensureLibs(context)
        // Попытка 1: пароль
        try {
            val db = net.sqlcipher.database.SQLiteDatabase.openDatabase(
                path, password, null, net.sqlcipher.database.SQLiteDatabase.OPEN_READWRITE
            )
            verifyCipherKey(db)
            return db
        } catch (_: Exception) {
        }
        // Попытка 2: hex-ключ (64 hex-символа)
        val hex = password.trim()
        if (hex.matches(Regex("^[0-9a-fA-F]{64}$"))) {
            val db = net.sqlcipher.database.SQLiteDatabase.openDatabase(
                path, "", null, net.sqlcipher.database.SQLiteDatabase.OPEN_READWRITE
            )
            db.execSQL("PRAGMA key = \"x'$hex'\"")
            verifyCipherKey(db)
            return db
        }
        throw DbException("Неверный ключ/пароль или база не SQLCipher")
    }

    private fun verifyCipherKey(db: net.sqlcipher.database.SQLiteDatabase) {
        db.rawQuery("SELECT count(*) FROM sqlite_master", null).use { it.moveToFirst() }
    }

    /** Сохранить правки обратно в приложение */
    fun saveBack(opened: OpenedDb) {
        checkpoint(opened.db)
        val st = opened.origStat
        val ok = if (st != null) {
            RootShell.copyBack(opened.workFile, opened.origPath, st.mode, st.uid, st.gid)
        } else {
            RootShell.copyBack(opened.workFile, opened.origPath, 660, 0, 0)
        }
        // WAL/SHM исходника больше не нужны — база уже с checkpoint'ом
        RootShell.remove(opened.origPath + "-wal")
        RootShell.remove(opened.origPath + "-shm")
        if (!ok) throw DbException("Не удалось записать базу обратно через root")
    }

    fun checkpoint(db: DbHandle) {
        try {
            db.rawQuery("PRAGMA wal_checkpoint(TRUNCATE)", null).use { it.moveToFirst() }
        } catch (_: Exception) {
        }
    }

    // ---------- Схема ----------

    fun listTables(db: DbHandle): List<TableInfo> {
        val out = mutableListOf<TableInfo>()
        db.rawQuery(
            "SELECT name, sql FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name", null
        ).use { c ->
            while (c.moveToNext()) {
                val name = c.getString(0)
                val sql = if (c.isNull(1)) null else c.getString(1)
                val count = try {
                    db.rawQuery("SELECT COUNT(*) FROM \"$name\"", null).use { cc ->
                        cc.moveToFirst()
                        cc.getLong(0)
                    }
                } catch (_: Exception) {
                    -1
                }
                out += TableInfo(name, count, sql)
            }
        }
        return out
    }

    fun tableColumns(db: DbHandle, table: String): List<ColumnInfo> {
        val out = mutableListOf<ColumnInfo>()
        db.rawQuery("PRAGMA table_info(\"$table\")", null).use { c ->
            while (c.moveToNext()) {
                out += ColumnInfo(
                    name = c.getString(1),
                    type = if (c.isNull(2)) "" else c.getString(2),
                    notNull = c.getInt(3) == 1,
                    defaultValue = if (c.isNull(4)) null else c.getString(4),
                    isPk = c.getInt(5) > 0
                )
            }
        }
        return out
    }

    // ---------- Чтение строк ----------

    fun fetchPage(db: DbHandle, table: String, offset: Int, limit: Int, where: String? = null, args: Array<String>? = null): List<RowData> {
        val whereClause = if (where.isNullOrBlank()) "" else " WHERE $where"
        // Пробуем с rowid
        val withRowId = try {
            db.rawQuery("SELECT rowid AS __rowid, * FROM \"$table\"$whereClause LIMIT $limit OFFSET $offset", args)
        } catch (_: Exception) {
            null
        }
        if (withRowId != null) {
            val rows = mutableListOf<RowData>()
            withRowId.use { c ->
                val cols = c.columnNames.toList()
                while (c.moveToNext()) {
                    val map: RowMap = LinkedHashMap()
                    cols.forEachIndexed { i, col ->
                        if (col != "__rowid") map[col] = cursorValue(c, i)
                    }
                    rows += RowData(c.getLong(0), map)
                }
            }
            return rows
        }
        // Без rowid (WITHOUT ROWID таблицы)
        val rows = mutableListOf<RowData>()
        db.rawQuery("SELECT * FROM \"$table\"$whereClause LIMIT $limit OFFSET $offset", args).use { c ->
            val cols = c.columnNames.toList()
            while (c.moveToNext()) {
                val map: RowMap = LinkedHashMap()
                cols.forEachIndexed { i, col -> map[col] = cursorValue(c, i) }
                rows += RowData(null, map)
            }
        }
        return rows
    }

    fun countRows(db: DbHandle, table: String, where: String? = null, args: Array<String>? = null): Long {
        val whereClause = if (where.isNullOrBlank()) "" else " WHERE $where"
        return try {
            db.rawQuery("SELECT COUNT(*) FROM \"$table\"$whereClause", args).use { c ->
                c.moveToFirst()
                c.getLong(0)
            }
        } catch (_: Exception) {
            -1
        }
    }

    private fun cursorValue(c: Cursor, i: Int): Any? = when (c.getType(i)) {
        Cursor.FIELD_TYPE_INTEGER -> c.getLong(i)
        Cursor.FIELD_TYPE_FLOAT -> c.getDouble(i)
        Cursor.FIELD_TYPE_STRING -> c.getString(i)
        Cursor.FIELD_TYPE_BLOB -> c.getBlob(i)
        else -> null
    }

    // ---------- Изменения ----------

    fun updateCell(db: DbHandle, table: String, column: String, row: RowData, newValue: Any?) {
        val (where, args) = rowWhere(db, table, row)
        val bind = mutableListOf<Any?>()
        bind += newValue
        bind += args
        db.execSQL("UPDATE \"$table\" SET \"$column\" = ? WHERE $where", bind.toTypedArray())
    }

    fun deleteRow(db: DbHandle, table: String, row: RowData) {
        val (where, args) = rowWhere(db, table, row)
        db.execSQL("DELETE FROM \"$table\" WHERE $where", args.toTypedArray())
    }

    fun insertRow(db: DbHandle, table: String, values: Map<String, Any?>) {
        val cols = values.keys.filter { it.isNotBlank() }
        if (cols.isEmpty()) throw DbException("Нет значений для вставки")
        val placeholders = cols.joinToString(",") { "?" }
        val colList = cols.joinToString(",") { "\"$it\"" }
        db.execSQL("INSERT INTO \"$table\" ($colList) VALUES ($placeholders)", cols.map { values[it] }.toTypedArray())
    }

    /** Условие для идентификации строки: rowid или первичный ключ */
    private fun rowWhere(db: DbHandle, table: String, row: RowData): Pair<String, List<Any?>> {
        if (row.rowId != null) return "rowid = ?" to listOf(row.rowId)
        val pkCols = tableColumns(db, table).filter { it.isPk }
        if (pkCols.isEmpty()) throw DbException("У таблицы нет rowid и первичного ключа — строка не идентифицируется")
        val where = pkCols.joinToString(" AND ") { "\"${it.name}\" IS ?" }
        return where to pkCols.map { row.values[it.name] }
    }

    // ---------- SQL-консоль ----------

    fun execSql(db: DbHandle, sql: String): SqlOutcome {
        val trimmed = sql.trim().trimEnd(';')
        if (trimmed.isEmpty()) return SqlOutcome.Error("Пустой запрос")
        val upper = trimmed.uppercase()
        val isQuery = upper.startsWith("SELECT") || upper.startsWith("WITH") ||
            upper.startsWith("PRAGMA") || upper.startsWith("EXPLAIN")
        return try {
            if (isQuery) {
                db.rawQuery(trimmed, null).use { c ->
                    val cols = c.columnNames.toList()
                    val rows = mutableListOf<RowMap>()
                    while (c.moveToNext() && rows.size < 500) {
                        val map: RowMap = LinkedHashMap()
                        cols.forEachIndexed { i, col -> map[col] = cursorValue(c, i) }
                        rows += map
                    }
                    SqlOutcome.Rows(cols, rows)
                }
            } else {
                db.execSQL(trimmed)
                val changes = try {
                    db.rawQuery("SELECT changes()", null).use { c ->
                        c.moveToFirst()
                        c.getInt(0)
                    }
                } catch (_: Exception) {
                    -1
                }
                SqlOutcome.Affected(changes)
            }
        } catch (e: Exception) {
            SqlOutcome.Error(e.message ?: "ошибка SQL")
        }
    }

    // ---------- Экспорт ----------

    fun exportCsv(db: DbHandle, table: String): String {
        val cols = tableColumns(db, table).map { it.name }
        val sb = StringBuilder()
        sb.append(cols.joinToString(",") { "\"${it.replace("\"", "\"\"")}\"" }).append("\n")
        var offset = 0
        while (true) {
            val page = fetchPage(db, table, offset, 500)
            if (page.isEmpty()) break
            page.forEach { row ->
                sb.append(cols.joinToString(",") { col ->
                    val v = row.values[col]
                    when (v) {
                        null -> ""
                        is ByteArray -> "\"${v.joinToString("") { "%02x".format(it) }}\""
                        else -> "\"${v.toString().replace("\"", "\"\"")}\""
                    }
                }).append("\n")
            }
            offset += page.size
        }
        return sb.toString()
    }

    fun exportJson(db: DbHandle, table: String): String {
        val arr = org.json.JSONArray()
        var offset = 0
        while (true) {
            val page = fetchPage(db, table, offset, 500)
            if (page.isEmpty()) break
            page.forEach { row ->
                val obj = org.json.JSONObject()
                row.values.forEach { (k, v) ->
                    when (v) {
                        null -> obj.put(k, org.json.JSONObject.NULL)
                        is ByteArray -> obj.put(k, v.joinToString("") { "%02x".format(it) })
                        is Long, is Int, is Double, is Float -> obj.put(k, v)
                        else -> obj.put(k, v.toString())
                    }
                }
                arr.put(obj)
            }
            offset += page.size
        }
        return arr.toString(2)
    }

    // ---------- Бэкапы ----------

    fun backup(context: Context, pkg: String, dbPath: String): String {
        val dir = File(backupsDir(context), pkg)
        dir.mkdirs()
        val name = dbPath.substringAfterLast("/")
        val stamp = java.text.SimpleDateFormat("yyyyMMdd_HHmmss", java.util.Locale.US).format(java.util.Date())
        val dst = File(dir, "${name}_$stamp.db")
        if (!RootShell.copyToAppDir(dbPath, dst)) throw DbException("Не удалось сделать бэкап через root")
        // WAL/SHM рядом
        listOf("-wal", "-shm").forEach { suf ->
            val src = dbPath + suf
            if (RootShell.stat(src) != null) {
                RootShell.copyToAppDir(src, File(dir, dst.name + suf))
            }
        }
        return dst.absolutePath
    }

    fun listBackups(context: Context, pkg: String): List<File> {
        val dir = File(backupsDir(context), pkg)
        if (!dir.exists()) return emptyList()
        return dir.listFiles()?.filter { it.name.endsWith(".db") }?.sortedByDescending { it.lastModified() } ?: emptyList()
    }

    fun restoreBackup(backupFile: File, origPath: String) {
        val st = RootShell.stat(origPath)
        val ok = if (st != null) RootShell.copyBack(backupFile, origPath, st.mode, st.uid, st.gid)
        else RootShell.copyBack(backupFile, origPath, 660, 0, 0)
        if (!ok) throw DbException("Не удалось восстановить бэкап через root")
    }

    // ---------- Экспорт в Загрузки ----------

    fun exportToDownloads(context: Context, fileName: String, bytes: ByteArray): String {
        return if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
            val values = android.content.ContentValues().apply {
                put(android.provider.MediaStore.Downloads.DISPLAY_NAME, fileName)
                put(android.provider.MediaStore.Downloads.MIME_TYPE, "application/octet-stream")
                put(android.provider.MediaStore.Downloads.RELATIVE_PATH, android.os.Environment.DIRECTORY_DOWNLOADS)
            }
            val uri = context.contentResolver.insert(android.provider.MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
                ?: throw DbException("Не удалось создать файл в Загрузках")
            context.contentResolver.openOutputStream(uri)?.use { it.write(bytes) }
            "Загрузки/$fileName"
        } else {
            val dir = context.getExternalFilesDir(android.os.Environment.DIRECTORY_DOWNLOADS)
            dir?.mkdirs()
            val f = File(dir, fileName)
            f.writeBytes(bytes)
            f.absolutePath
        }
    }

    // ---------- Глобальный поиск по всем базам приложения ----------

    fun searchAll(context: Context, pkg: String, query: String, maxHits: Int = 200): List<SearchHit> {
        val hits = mutableListOf<SearchHit>()
        val dbs = AppScanner.scanDatabases(pkg).filter { it.isSqlite }.take(60)
        val like = "%$query%"
        for (info in dbs) {
            if (hits.size >= maxHits) break
            val opened = try {
                openForEdit(context, pkg, info.path)
            } catch (_: Exception) {
                continue
            }
            try {
                val tables = listTables(opened.db)
                for (t in tables) {
                    if (hits.size >= maxHits) break
                    val cols = tableColumns(opened.db, t.name)
                    for (col in cols) {
                        if (hits.size >= maxHits) break
                        try {
                            val rows = fetchPage(
                                opened.db, t.name, 0, 5,
                                where = "CAST(\"${col.name}\" AS TEXT) LIKE ?",
                                args = arrayOf(like)
                            )
                            rows.forEach { row ->
                                hits += SearchHit(
                                    dbName = info.name,
                                    dbPath = info.path,
                                    table = t.name,
                                    column = col.name,
                                    valuePreview = row.values[col.name]?.let { previewValue(it) } ?: "NULL",
                                    rowId = row.rowId
                                )
                            }
                        } catch (_: Exception) {
                        }
                    }
                }
            } finally {
                opened.db.close()
            }
        }
        return hits
    }

    fun previewValue(v: Any?): String = when (v) {
        null -> "NULL"
        is ByteArray -> "BLOB ${v.size} Б: " + v.take(32).joinToString("") { "%02x".format(it) }
        else -> v.toString().take(120)
    }

    // ---------- Расшифровка / шифрование SQLCipher ----------

    /** Расшифровать SQLCipher-базу в обычную SQLite */
    fun decryptSqlCipher(context: Context, cipherPath: String, password: String, outFile: File): DecryptResult {
        ensureLibs(context)
        val cipherDb = try {
            openCipher(context, cipherPath, password)
        } catch (e: Exception) {
            return DecryptResult(false, e.message ?: "не удалось открыть зашифрованную базу")
        }
        outFile.delete()
        val plainDb = android.database.sqlite.SQLiteDatabase.openOrCreateDatabase(outFile.absolutePath, null)
        return try {
            copyDatabase(CipherDbHandle(cipherDb), PlainDbHandle(plainDb))
            DecryptResult(true, "Расшифровано. Таблицы скопированы в обычную SQLite.", outFile.absolutePath)
        } catch (e: Exception) {
            DecryptResult(false, "Ошибка копирования: ${e.message}")
        } finally {
            cipherDb.close()
            plainDb.close()
        }
    }

    /** Зашифровать обычную SQLite в SQLCipher */
    fun encryptSqlCipher(context: Context, plainPath: String, password: String, outFile: File): DecryptResult {
        ensureLibs(context)
        outFile.delete()
        val plainDb = android.database.sqlite.SQLiteDatabase.openDatabase(
            plainPath, null, android.database.sqlite.SQLiteDatabase.OPEN_READWRITE, null
        )
        val cipherDb = net.sqlcipher.database.SQLiteDatabase.openDatabase(
            outFile.absolutePath, password, null,
            net.sqlcipher.database.SQLiteDatabase.OPEN_READWRITE or net.sqlcipher.database.SQLiteDatabase.CREATE_IF_NECESSARY
        )
        return try {
            copyDatabase(PlainDbHandle(plainDb), CipherDbHandle(cipherDb))
            DecryptResult(true, "Зашифровано в SQLCipher.", outFile.absolutePath)
        } catch (e: Exception) {
            DecryptResult(false, "Ошибка шифрования: ${e.message}")
        } finally {
            plainDb.close()
            cipherDb.close()
        }
    }

    private fun copyDatabase(src: DbHandle, dst: DbHandle) {
        // Схема: таблицы, индексы, триггеры
        val schemas = mutableListOf<Pair<String, String>>()
        src.rawQuery(
            "SELECT type, sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%'", null
        ).use { c ->
            while (c.moveToNext()) schemas += c.getString(0) to c.getString(1)
        }
        schemas.filter { it.first == "table" }.forEach { dst.execSQL(it.second) }
        schemas.filter { it.first != "table" }.forEach {
            try {
                dst.execSQL(it.second)
            } catch (_: Exception) {
            }
        }
        // Данные
        val tables = listTables(src).map { it.name }
        tables.forEach { table ->
            var offset = 0
            while (true) {
                val page = fetchPage(src, table, offset, 500)
                if (page.isEmpty()) break
                page.forEach { row ->
                    val cv = ContentValues()
                    row.values.forEach { (k, v) ->
                        when (v) {
                            null -> cv.putNull(k)
                            is String -> cv.put(k, v)
                            is Long -> cv.put(k, v)
                            is Int -> cv.put(k, v)
                            is Double -> cv.put(k, v)
                            is Float -> cv.put(k, v)
                            is ByteArray -> cv.put(k, v)
                            is Boolean -> cv.put(k, v)
                            else -> cv.put(k, v.toString())
                        }
                    }
                    dst.insert(table, cv)
                }
                offset += page.size
            }
        }
    }
}
