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
import androidx.compose.material.icons.filled.Database
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Tune
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
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
import com.rootdb.pro.data.AppInfo
import com.rootdb.pro.data.AppScanner
import com.rootdb.pro.data.DbFileInfo
import com.rootdb.pro.data.formatDateTime
import com.rootdb.pro.data.formatSize
import com.rootdb.pro.ui.components.AppIcon
import com.rootdb.pro.ui.components.Badge
import com.rootdb.pro.ui.components.EmptyBox
import com.rootdb.pro.ui.components.LoadingBox
import com.rootdb.pro.ui.components.SectionTitle
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppDetailScreen(
    pkg: String,
    onBack: () -> Unit,
    onOpenDb: (String) -> Unit,
    onOpenPrefs: () -> Unit,
    onOpenFiles: () -> Unit,
    onOpenSearch: () -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var app by remember { mutableStateOf<AppInfo?>(null) }
    var dbs by remember { mutableStateOf<List<DbFileInfo>>(emptyList()) }
    var dataSize by remember { mutableStateOf(0L) }
    var loading by remember { mutableStateOf(true) }
    var error by remember { mutableStateOf<String?>(null) }

    suspend fun scan() {
        loading = true
        error = null
        try {
            val info = withContext(Dispatchers.IO) {
                AppScanner.listApps(context).firstOrNull { it.pkg == pkg }
            }
            app = info
            val found = withContext(Dispatchers.IO) { AppScanner.scanDatabases(pkg) }
            dbs = found
            dataSize = withContext(Dispatchers.IO) { AppScanner.dataSize(pkg) }
        } catch (e: Exception) {
            error = e.message
        }
        loading = false
    }

    LaunchedEffect(pkg) { scan() }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(app?.label ?: pkg) },
                navigationIcon = {
                    IconButton(onClick = onBack) { Icon(Icons.Filled.ArrowBack, contentDescription = "Назад") }
                },
                actions = {
                    IconButton(onClick = { scope.launch { scan() } }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Сканировать заново")
                    }
                }
            )
        }
    ) { padding ->
        when {
            loading -> LoadingBox("Сканирую базы через root…")
            error != null -> EmptyBox("Ошибка: $error")
            else -> LazyColumn(
                Modifier.fillMaxSize().padding(padding).padding(horizontal = 16.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                item {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                            AppIcon(app?.icon, size = 64)
                            Column {
                                Text(app?.label ?: pkg, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                                Text(pkg, style = MaterialTheme.typography.bodySmall)
                                Text(
                                    "Данные: ${formatSize(dataSize)} • Базы: ${dbs.size}",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Button(onClick = onOpenSearch) {
                                Icon(Icons.Filled.Search, null)
                                Spacer(Modifier.padding(horizontal = 4.dp))
                                Text("Поиск по базам")
                            }
                            OutlinedButton(onClick = onOpenPrefs) {
                                Icon(Icons.Filled.Tune, null)
                                Spacer(Modifier.padding(horizontal = 4.dp))
                                Text("SharedPrefs")
                            }
                            OutlinedButton(onClick = onOpenFiles) {
                                Icon(Icons.Filled.Folder, null)
                                Spacer(Modifier.padding(horizontal = 4.dp))
                                Text("Файлы")
                            }
                        }
                    }
                }
                item { SectionTitle("Базы данных (${dbs.size})") }
                if (dbs.isEmpty()) {
                    item { EmptyBox("Базы не найдены. Попробуйте обновить.") }
                } else {
                    items(dbs, key = { it.path }) { db ->
                        Card(
                            shape = MaterialTheme.shapes.large,
                            modifier = Modifier.fillMaxWidth().clickable { onOpenDb(db.path) }
                        ) {
                            ListItem(
                                headlineContent = { Text(db.name, fontWeight = FontWeight.Bold) },
                                supportingContent = {
                                    Column {
                                        Text("${formatSize(db.size)} • ${formatDateTime(db.modifiedSec)}")
                                        Text(db.path, style = MaterialTheme.typography.bodySmall)
                                    }
                                },
                                leadingContent = { Icon(Icons.Filled.Database, null, tint = MaterialTheme.colorScheme.primary) },
                                trailingContent = {
                                    Column(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                                        when {
                                            db.isWal -> Badge("WAL", MaterialTheme.colorScheme.surfaceContainerHigh, MaterialTheme.colorScheme.onSurfaceVariant)
                                            db.isShm -> Badge("SHM", MaterialTheme.colorScheme.surfaceContainerHigh, MaterialTheme.colorScheme.onSurfaceVariant)
                                            db.isJournal -> Badge("JOURNAL", MaterialTheme.colorScheme.surfaceContainerHigh, MaterialTheme.colorScheme.onSurfaceVariant)
                                            db.isEncrypted -> Badge("SQLCipher?", MaterialTheme.colorScheme.errorContainer, MaterialTheme.colorScheme.onErrorContainer)
                                            db.isSqlite -> Badge("SQLite", MaterialTheme.colorScheme.primaryContainer, MaterialTheme.colorScheme.onPrimaryContainer)
                                            else -> Badge("Файл", MaterialTheme.colorScheme.surfaceContainerHigh, MaterialTheme.colorScheme.onSurfaceVariant)
                                        }
                                    }
                                }
                            )
                        }
                    }
                }
                item { Spacer(Modifier.height(16.dp)) }
            }
        }
    }
}
