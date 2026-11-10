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
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Backup
import androidx.compose.material.icons.filled.Code
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Key
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Security
import androidx.compose.material.icons.filled.TableChart
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.rootdb.pro.data.DbManager
import com.rootdb.pro.data.GuardianService
import com.rootdb.pro.data.GuardianStore
import com.rootdb.pro.data.OpenedDb
import com.rootdb.pro.data.RootShell
import com.rootdb.pro.data.TableInfo
import com.rootdb.pro.data.WatchMode
import com.rootdb.pro.data.formatDateTime
import com.rootdb.pro.data.formatSize
import com.rootdb.pro.ui.components.Badge
import com.rootdb.pro.ui.components.ConfirmDialog
import com.rootdb.pro.ui.components.EmptyBox
import com.rootdb.pro.ui.components.ErrorBox
import com.rootdb.pro.ui.components.LoadingBox
import com.rootdb.pro.ui.components.SectionTitle
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DbScreen(
    pkg: String,
    dbPath: String,
    onBack: () -> Unit,
    onOpenTable: (String) -> Unit,
    onOpenSql: () -> Unit,
    onOpenDecrypt: () -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbar = remember { SnackbarHostState() }
    val dbName = dbPath.substringAfterLast("/")

    var opened by remember { mutableStateOf<OpenedDb?>(null) }
    var tables by remember { mutableStateOf<List<TableInfo>>(emptyList()) }
    var loading by remember { mutableStateOf(true) }
    var error by remember { mutableStateOf<String?>(null) }
    var password by remember { mutableStateOf("") }
    var needPassword by remember { mutableStateOf(false) }
    var showBackupDialog by remember { mutableStateOf(false) }
    var showRestoreDialog by remember { mutableStateOf(false) }
    var showGuardianDialog by remember { mutableStateOf(false) }
    var guardianMode by remember { mutableStateOf(WatchMode.RESTORE) }
    var backups by remember { mutableStateOf<List<java.io.File>>(emptyList()) }
    var dbSize by remember { mutableStateOf(0L) }

    suspend fun openDb(pass: String?) {
        loading = true
        error = null
        try {
            val o = withContext(Dispatchers.IO) { DbManager.openForEdit(context, pkg, dbPath, pass, tag = "main") }
            opened?.db?.close()
            opened = o
            tables = withContext(Dispatchers.IO) { DbManager.listTables(o.db) }
            dbSize = withContext(Dispatchers.IO) { RootShell.stat(dbPath)?.size ?: 0L }
            needPassword = false
        } catch (e: Exception) {
            val msg = e.message ?: "ошибка"
            if (msg.contains("зашифрована", ignoreCase = true)) {
                needPassword = true
                error = null
            } else {
                error = msg
            }
        }
        loading = false
    }

    LaunchedEffect(dbPath) { openDb(null) }
    DisposableEffect(Unit) { onDispose { opened?.db?.close() } }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(dbName) },
                navigationIcon = { IconButton(onClick = onBack) { Icon(Icons.Filled.ArrowBack, contentDescription = "Назад") } },
                actions = {
                    IconButton(onClick = { scope.launch { openDb(if (password.isBlank()) null else password) } }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Обновить")
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        when {
            loading -> LoadingBox("Открываю базу через root…")
            needPassword -> Column(
                Modifier.fillMaxSize().padding(padding).padding(20.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text("База зашифрована (SQLCipher)", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                Text("Введите ключ или пароль, либо откройте экран расшифровки для экспорта в обычную SQLite.")
                OutlinedTextField(
                    value = password,
                    onValueChange = { password = it },
                    label = { Text("Ключ / пароль") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
                Button(onClick = { scope.launch { openDb(password) } }, modifier = Modifier.fillMaxWidth()) {
                    Icon(Icons.Filled.Key, null)
                    Spacer(Modifier.padding(horizontal = 4.dp))
                    Text("Открыть с ключом")
                }
                OutlinedButton(onClick = onOpenDecrypt, modifier = Modifier.fillMaxWidth()) {
                    Text("Расшифровать в обычную SQLite")
                }
            }
            error != null -> ErrorBox(error!!) { scope.launch { openDb(null) } }
            else -> LazyColumn(
                Modifier.fillMaxSize().padding(padding).padding(horizontal = 16.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                item {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text(dbPath, style = MaterialTheme.typography.bodySmall)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Badge(
                                if (opened?.encrypted == true) "SQLCipher" else "SQLite",
                                if (opened?.encrypted == true) MaterialTheme.colorScheme.errorContainer else MaterialTheme.colorScheme.primaryContainer,
                                if (opened?.encrypted == true) MaterialTheme.colorScheme.onErrorContainer else MaterialTheme.colorScheme.onPrimaryContainer
                            )
                            Badge(formatSize(dbSize), MaterialTheme.colorScheme.surfaceContainerHigh, MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Button(onClick = onOpenSql) {
                                Icon(Icons.Filled.Code, null)
                                Spacer(Modifier.padding(horizontal = 4.dp))
                                Text("SQL-консоль")
                            }
                            OutlinedButton(onClick = onOpenDecrypt) {
                                Icon(Icons.Filled.Key, null)
                                Spacer(Modifier.padding(horizontal = 4.dp))
                                Text("Расшифровка")
                            }
                        }
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedButton(onClick = { showBackupDialog = true }) {
                                Icon(Icons.Filled.Backup, null)
                                Spacer(Modifier.padding(horizontal = 4.dp))
                                Text("Бэкап")
                            }
                            OutlinedButton(onClick = {
                                scope.launch {
                                    backups = withContext(Dispatchers.IO) { DbManager.listBackups(context, pkg) }
                                    showRestoreDialog = true
                                }
                            }) {
                                Icon(Icons.Filled.Refresh, null)
                                Spacer(Modifier.padding(horizontal = 4.dp))
                                Text("Восстановить")
                            }
                            OutlinedButton(onClick = { showGuardianDialog = true }) {
                                Icon(Icons.Filled.Security, null)
                                Spacer(Modifier.padding(horizontal = 4.dp))
                                Text("Guardian")
                            }
                        }
                        OutlinedButton(
                            onClick = {
                                scope.launch {
                                    try {
                                        val bytes = withContext(Dispatchers.IO) { opened?.workFile?.readBytes() ?: ByteArray(0) }
                                        val where = withContext(Dispatchers.IO) { DbManager.exportToDownloads(context, dbName, bytes) }
                                        snackbar.showSnackbar("Экспортировано: $where")
                                    } catch (e: Exception) {
                                        snackbar.showSnackbar("Ошибка экспорта: ${e.message}")
                                    }
                                }
                            },
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Icon(Icons.Filled.Download, null)
                            Spacer(Modifier.padding(horizontal = 4.dp))
                            Text("Экспорт файла базы в Загрузки")
                        }
                    }
                }
                item { SectionTitle("Таблицы (${tables.size})") }
                if (tables.isEmpty()) {
                    item { EmptyBox("Таблиц не найдено") }
                } else {
                    items(tables, key = { it.name }) { t ->
                        Card(
                            shape = MaterialTheme.shapes.large,
                            modifier = Modifier.fillMaxWidth().clickable { onOpenTable(t.name) }
                        ) {
                            ListItem(
                                headlineContent = { Text(t.name, fontWeight = FontWeight.Bold) },
                                supportingContent = {
                                    Text(if (t.rowCount >= 0) "Строк: ${t.rowCount}" else "Строк: ?")
                                },
                                leadingContent = { Icon(Icons.Filled.TableChart, null, tint = MaterialTheme.colorScheme.primary) }
                            )
                        }
                    }
                }
                item { Spacer(Modifier.height(16.dp)) }
            }
        }
    }

    if (showBackupDialog) {
        ConfirmDialog(
            title = "Сделать бэкап?",
            text = "Оригинальная база будет скопирована в бэкапы приложения (через root).",
            confirmText = "Бэкап",
            onConfirm = {
                showBackupDialog = false
                scope.launch {
                    try {
                        val path = withContext(Dispatchers.IO) { DbManager.backup(context, pkg, dbPath) }
                        snackbar.showSnackbar("Бэкап: $path")
                    } catch (e: Exception) {
                        snackbar.showSnackbar("Ошибка бэкапа: ${e.message}")
                    }
                }
            },
            onDismiss = { showBackupDialog = false }
        )
    }

    if (showRestoreDialog) {
        AlertDialog(
            onDismissRequest = { showRestoreDialog = false },
            title = { Text("Восстановить из бэкапа") },
            text = {
                if (backups.isEmpty()) {
                    Text("Бэкапов пока нет. Сделайте бэкап перед правками.")
                } else {
                    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        backups.forEach { f ->
                            Text(
                                "${f.name}\n${formatSize(f.length())} • ${formatDateTime(f.lastModified() / 1000)}",
                                modifier = Modifier.clickable {
                                    showRestoreDialog = false
                                    scope.launch {
                                        try {
                                            withContext(Dispatchers.IO) { DbManager.restoreBackup(f, dbPath) }
                                            withContext(Dispatchers.IO) { RootShell.forceStop(pkg) }
                                            snackbar.showSnackbar("Восстановлено. Приложение остановлено.")
                                            openDb(if (password.isBlank()) null else password)
                                        } catch (e: Exception) {
                                            snackbar.showSnackbar("Ошибка: ${e.message}")
                                        }
                                    }
                                }.padding(vertical = 6.dp)
                            )
                        }
                    }
                }
            },
            confirmButton = { TextButton(onClick = { showRestoreDialog = false }) { Text("Закрыть") } }
        )
    }

    if (showGuardianDialog) {
        AlertDialog(
            onDismissRequest = { showGuardianDialog = false },
            title = { Text("Guardian: защита базы") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Guardian следит за базой в фоне. При изменении:")
                    listOf(
                        WatchMode.RESTORE to "Восстановить правки из бэкапа + остановить приложение",
                        WatchMode.KILL to "Остановить приложение (force-stop)",
                        WatchMode.NOTIFY to "Только уведомление в лог"
                    ).forEach { (mode, desc) ->
                        Row(
                            Modifier.clickable { guardianMode = mode }.padding(vertical = 4.dp),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            Text(if (guardianMode == mode) "◉" else "○")
                            Text(desc)
                        }
                    }
                    Text(
                        "Для режима «Восстановить» сначала делается бэкап текущей (уже изменённой) базы.",
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            },
            confirmButton = {
                Button(onClick = {
                    showGuardianDialog = false
                    scope.launch {
                        try {
                            val backupPath = withContext(Dispatchers.IO) { DbManager.backup(context, pkg, dbPath) }
                            GuardianStore(context).addItem(
                                com.rootdb.pro.data.WatchItem(pkg, dbPath, backupPath, guardianMode, "$pkg/$dbName")
                            )
                            GuardianService.start(context)
                            snackbar.showSnackbar("Guardian включён для $dbName")
                        } catch (e: Exception) {
                            snackbar.showSnackbar("Ошибка: ${e.message}")
                        }
                    }
                }) { Text("Включить") }
            },
            dismissButton = { TextButton(onClick = { showGuardianDialog = false }) { Text("Отмена") } }
        )
    }
}
