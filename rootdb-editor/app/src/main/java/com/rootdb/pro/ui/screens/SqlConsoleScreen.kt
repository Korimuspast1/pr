package com.rootdb.pro.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Save
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
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
import com.rootdb.pro.data.DbManager
import com.rootdb.pro.data.OpenedDb
import com.rootdb.pro.data.RootShell
import com.rootdb.pro.data.SettingsStore
import com.rootdb.pro.data.SqlOutcome
import com.rootdb.pro.ui.components.ErrorBox
import com.rootdb.pro.ui.components.LoadingBox
import com.rootdb.pro.ui.components.displayValue
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SqlConsoleScreen(pkg: String, dbPath: String, onBack: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbar = remember { SnackbarHostState() }
    val settings = remember { SettingsStore(context) }
    val dbName = dbPath.substringAfterLast("/")

    var opened by remember { mutableStateOf<OpenedDb?>(null) }
    var loading by remember { mutableStateOf(true) }
    var error by remember { mutableStateOf<String?>(null) }
    var sql by remember { mutableStateOf("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;") }
    var outcome by remember { mutableStateOf<SqlOutcome?>(null) }
    var dirty by remember { mutableStateOf(false) }

    LaunchedEffect(dbPath) {
        try {
            opened = withContext(Dispatchers.IO) { DbManager.openForEdit(context, pkg, dbPath, tag = "sql") }
        } catch (e: Exception) {
            error = e.message
        }
        loading = false
    }
    DisposableEffect(Unit) { onDispose { opened?.db?.close() } }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("SQL: $dbName") },
                navigationIcon = { IconButton(onClick = onBack) { Icon(Icons.Filled.ArrowBack, contentDescription = "Назад") } },
                actions = {
                    if (dirty) {
                        IconButton(onClick = {
                            scope.launch {
                                try {
                                    val o = opened ?: return@launch
                                    withContext(Dispatchers.IO) { DbManager.saveBack(o) }
                                    if (settings.forceStopAfterSave) withContext(Dispatchers.IO) { RootShell.forceStop(pkg) }
                                    dirty = false
                                    snackbar.showSnackbar("Сохранено в приложение")
                                } catch (e: Exception) {
                                    snackbar.showSnackbar("Ошибка: ${e.message}")
                                }
                            }
                        }) { Icon(Icons.Filled.Save, contentDescription = "Сохранить") }
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        when {
            loading -> LoadingBox("Открываю базу…")
            error != null -> ErrorBox(error!!) { onBack() }
            else -> Column(
                Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(padding).padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                OutlinedTextField(
                    value = sql,
                    onValueChange = { sql = it },
                    label = { Text("SQL-запрос") },
                    modifier = Modifier.fillMaxWidth(),
                    minLines = 4,
                    textStyle = androidx.compose.ui.text.TextStyle(fontFamily = FontFamily.Monospace)
                )
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Button(onClick = {
                        scope.launch {
                            val o = opened ?: return@launch
                            outcome = withContext(Dispatchers.IO) { DbManager.execSql(o.db, sql) }
                            if (outcome is SqlOutcome.Affected) dirty = true
                        }
                    }) {
                        Icon(Icons.Filled.PlayArrow, null)
                        Text("Выполнить")
                    }
                    Button(onClick = {
                        sql = "SELECT name, sql FROM sqlite_master ORDER BY type, name;"
                    }) { Text("Схема") }
                }
                when (val o = outcome) {
                    null -> Text("Результат появится здесь.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                    is SqlOutcome.Error -> Text("Ошибка: ${o.message}", color = MaterialTheme.colorScheme.error)
                    is SqlOutcome.Affected -> Text(
                        "Выполнено. Изменено строк: ${if (o.count >= 0) o.count else "?"}",
                        fontWeight = FontWeight.Bold
                    )
                    is SqlOutcome.Rows -> {
                        Text("Строк: ${o.rows.size}", fontWeight = FontWeight.Bold)
                        o.rows.forEach { row ->
                            Card(shape = MaterialTheme.shapes.medium) {
                                Column(Modifier.padding(10.dp)) {
                                    row.forEach { (k, v) ->
                                        Text(
                                            "$k = ${displayValue(v)}",
                                            fontFamily = FontFamily.Monospace,
                                            style = MaterialTheme.typography.bodySmall
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
