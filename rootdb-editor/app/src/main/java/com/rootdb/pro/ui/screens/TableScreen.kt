package com.rootdb.pro.ui.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Save
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.rootdb.pro.data.ColumnInfo
import com.rootdb.pro.data.DbManager
import com.rootdb.pro.data.OpenedDb
import com.rootdb.pro.data.RootShell
import com.rootdb.pro.data.RowData
import com.rootdb.pro.data.SettingsStore
import com.rootdb.pro.ui.components.ErrorBox
import com.rootdb.pro.ui.components.LoadingBox
import com.rootdb.pro.ui.components.SearchField
import com.rootdb.pro.ui.components.ValueSheet
import com.rootdb.pro.ui.components.displayValue
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TableScreen(pkg: String, dbPath: String, table: String, onBack: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbar = remember { SnackbarHostState() }
    val settings = remember { SettingsStore(context) }
    val pageSize = settings.pageSize

    var opened by remember { mutableStateOf<OpenedDb?>(null) }
    var columns by remember { mutableStateOf<List<ColumnInfo>>(emptyList()) }
    var rows by remember { mutableStateOf<List<RowData>>(emptyList()) }
    var total by remember { mutableStateOf(0L) }
    var offset by remember { mutableStateOf(0) }
    var loading by remember { mutableStateOf(true) }
    var error by remember { mutableStateOf<String?>(null) }
    var query by remember { mutableStateOf("") }
    var dirty by remember { mutableStateOf(false) }
    var editRow by remember { mutableStateOf<RowData?>(null) }
    var addMode by remember { mutableStateOf(false) }
    var sheetValue by remember { mutableStateOf<Any?>(null) }
    var sheetOpen by remember { mutableStateOf(false) }

    fun buildWhere(q: String, cols: List<ColumnInfo>): Pair<String?, Array<String>?> {
        if (q.isBlank() || cols.isEmpty()) return null to null
        val like = "%$q%"
        val clause = cols.joinToString(" OR ") { "CAST(\"${it.name}\" AS TEXT) LIKE ?" }
        return clause to Array(cols.size) { like }
    }

    suspend fun reload(reset: Boolean) {
        val o = opened ?: return
        loading = true
        try {
            if (reset) {
                offset = 0
                rows = emptyList()
            }
            val (where, args) = buildWhere(query, columns)
            val page = withContext(Dispatchers.IO) { DbManager.fetchPage(o.db, table, offset, pageSize, where, args) }
            rows = rows + page
            offset += page.size
            total = withContext(Dispatchers.IO) { DbManager.countRows(o.db, table, where, args) }
        } catch (e: Exception) {
            error = e.message
        }
        loading = false
    }

    suspend fun saveNow() {
        val o = opened ?: return
        try {
            withContext(Dispatchers.IO) { DbManager.saveBack(o) }
            if (settings.forceStopAfterSave) {
                withContext(Dispatchers.IO) { RootShell.forceStop(pkg) }
            }
            dirty = false
            snackbar.showSnackbar("Сохранено в приложение" + if (settings.forceStopAfterSave) " (приложение остановлено)" else "")
        } catch (e: Exception) {
            snackbar.showSnackbar("Ошибка сохранения: ${e.message}")
        }
    }

    suspend fun afterChange() {
        dirty = true
        if (settings.autoSave) saveNow()
        reload(reset = true)
    }

    LaunchedEffect(dbPath, table) {
        try {
            val o = withContext(Dispatchers.IO) { DbManager.openForEdit(context, pkg, dbPath, tag = "table") }
            opened = o
            columns = withContext(Dispatchers.IO) { DbManager.tableColumns(o.db, table) }
            reload(reset = true)
        } catch (e: Exception) {
            error = e.message
            loading = false
        }
    }
    DisposableEffect(Unit) { onDispose { opened?.db?.close() } }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(table) },
                navigationIcon = { IconButton(onClick = onBack) { Icon(Icons.Filled.ArrowBack, contentDescription = "Назад") } },
                actions = {
                    if (dirty) {
                        IconButton(onClick = { scope.launch { saveNow() } }) {
                            Icon(Icons.Filled.Save, contentDescription = "Сохранить")
                        }
                    }
                    IconButton(onClick = { addMode = true }) {
                        Icon(Icons.Filled.Add, contentDescription = "Добавить строку")
                    }
                    IconButton(onClick = {
                        scope.launch {
                            try {
                                val o = opened ?: return@launch
                                val csv = withContext(Dispatchers.IO) { DbManager.exportCsv(o.db, table) }
                                val where = withContext(Dispatchers.IO) {
                                    DbManager.exportToDownloads(context, "${table}.csv", csv.toByteArray(Charsets.UTF_8))
                                }
                                snackbar.showSnackbar("CSV: $where")
                            } catch (e: Exception) {
                                snackbar.showSnackbar("Ошибка: ${e.message}")
                            }
                        }
                    }) {
                        Icon(Icons.Filled.Download, contentDescription = "Экспорт CSV")
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        when {
            loading && rows.isEmpty() && opened == null -> LoadingBox("Открываю таблицу…")
            error != null && opened == null -> ErrorBox(error!!) { onBack() }
            else -> Column(Modifier.fillMaxSize().padding(padding)) {
                Column(Modifier.padding(horizontal = 16.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    SearchField(query, { query = it; scope.launch { reload(reset = true) } }, "Поиск по значениям")
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        FilterChip(selected = false, onClick = {}, label = { Text("Строк: $total") })
                        FilterChip(selected = false, onClick = {}, label = { Text("Колонок: ${columns.size}") })
                        if (dirty) FilterChip(selected = true, onClick = { scope.launch { saveNow() } }, label = { Text("● Не сохранено — нажать") })
                    }
                    Text(
                        "Нажмите на строку — редактировать. На значение — расшифровка.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                LazyColumn(Modifier.fillMaxSize(), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    itemsIndexed(rows, key = { i, r -> r.rowId?.toString() ?: "r$i" }) { _, row ->
                        Card(
                            shape = MaterialTheme.shapes.medium,
                            modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp)
                                .clickable { editRow = row }
                        ) {
                            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                                if (row.rowId != null) {
                                    Text("rowid=${row.rowId}", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                                row.values.forEach { (col, v) ->
                                    Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                        Text("$col:", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold)
                                        Text(
                                            displayValue(v),
                                            style = MaterialTheme.typography.bodySmall,
                                            fontFamily = FontFamily.Monospace,
                                            modifier = Modifier.clickable {
                                                sheetValue = v
                                                sheetOpen = true
                                            }
                                        )
                                    }
                                }
                            }
                        }
                    }
                    if (rows.size < total) {
                        item {
                            Button(
                                onClick = { scope.launch { reload(reset = false) } },
                                modifier = Modifier.fillMaxWidth().padding(16.dp)
                            ) { Text("Загрузить ещё (${rows.size} / $total)") }
                        }
                    }
                    item { Spacer(Modifier.height(16.dp)) }
                }
            }
        }
    }

    // Диалог редактирования строки
    editRow?.let { row ->
        RowEditDialog(
            table = table,
            columns = columns,
            initial = row.values,
            onDismiss = { editRow = null },
            onSave = { values ->
                editRow = null
                scope.launch {
                    try {
                        val o = opened ?: return@launch
                        withContext(Dispatchers.IO) {
                            values.forEach { (col, v) ->
                                if (v != row.values[col]) DbManager.updateCell(o.db, table, col, row, v)
                            }
                        }
                        afterChange()
                    } catch (e: Exception) {
                        snackbar.showSnackbar("Ошибка: ${e.message}")
                    }
                }
            },
            onDelete = {
                editRow = null
                scope.launch {
                    try {
                        val o = opened ?: return@launch
                        withContext(Dispatchers.IO) { DbManager.deleteRow(o.db, table, row) }
                        afterChange()
                    } catch (e: Exception) {
                        snackbar.showSnackbar("Ошибка: ${e.message}")
                    }
                }
            }
        )
    }

    // Диалог добавления строки
    if (addMode) {
        RowEditDialog(
            table = table,
            columns = columns,
            initial = emptyMap(),
            onDismiss = { addMode = false },
            onSave = { values ->
                addMode = false
                scope.launch {
                    try {
                        val o = opened ?: return@launch
                        withContext(Dispatchers.IO) { DbManager.insertRow(o.db, table, values) }
                        afterChange()
                    } catch (e: Exception) {
                        snackbar.showSnackbar("Ошибка: ${e.message}")
                    }
                }
            },
            onDelete = null
        )
    }

    if (sheetOpen) {
        ValueSheet(value = sheetValue, onDismiss = { sheetOpen = false })
    }
}

@Composable
private fun RowEditDialog(
    table: String,
    columns: List<ColumnInfo>,
    initial: Map<String, Any?>,
    onDismiss: () -> Unit,
    onSave: (Map<String, Any?>) -> Unit,
    onDelete: (() -> Unit)?
) {
    var fields by remember(table, initial) {
        mutableStateOf(
            columns.associate { col ->
                val v = initial[col.name]
                col.name to ((if (v == null) "" else displayValue(v)) to (v == null))
            }
        )
    }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (initial.isEmpty()) "Новая строка" else "Редактировать строку") },
        text = {
            Column(
                Modifier.fillMaxWidth().verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                columns.forEach { col ->
                    val (text, isNull) = fields[col.name] ?: ("" to false)
                    Row(verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) {
                        OutlinedTextField(
                            value = if (isNull) "NULL" else text,
                            onValueChange = { newText ->
                                fields = fields.toMutableMap().apply { put(col.name, newText to false) }
                            },
                            label = { Text("${col.name} (${col.type.ifBlank { "TEXT" }})") },
                            singleLine = true,
                            enabled = !isNull,
                            modifier = Modifier.weight(1f)
                        )
                        FilterChip(
                            selected = isNull,
                            onClick = {
                                fields = fields.toMutableMap().apply {
                                    put(col.name, (fields[col.name]?.first ?: "") to !isNull)
                                }
                            },
                            label = { Text("NULL") }
                        )
                    }
                }
                Text(
                    "Пустое поле = пустая строка. Для NULL используйте чип NULL.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        },
        confirmButton = {
            Button(onClick = {
                val values = fields.mapValues { (_, pair) -> if (pair.second) null else pair.first }
                onSave(values)
            }) { Text("Сохранить") }
        },
        dismissButton = {
            Row {
                if (onDelete != null) {
                    TextButton(onClick = onDelete) {
                        Text("Удалить", color = MaterialTheme.colorScheme.error)
                    }
                }
                TextButton(onClick = onDismiss) { Text("Отмена") }
            }
        }
    )
}
