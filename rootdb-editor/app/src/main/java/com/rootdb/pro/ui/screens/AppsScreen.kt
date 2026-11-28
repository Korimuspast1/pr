package com.rootdb.pro.ui.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.rootdb.pro.data.AppInfo
import com.rootdb.pro.data.AppScanner
import com.rootdb.pro.data.SettingsStore
import com.rootdb.pro.ui.components.AppIcon
import com.rootdb.pro.ui.components.Badge
import com.rootdb.pro.ui.components.EmptyBox
import com.rootdb.pro.ui.components.LoadingBox
import com.rootdb.pro.ui.components.SearchField
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppsScreen(onAppClick: (String) -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val settings = remember { SettingsStore(context) }
    var apps by remember { mutableStateOf<List<AppInfo>>(emptyList()) }
    var loading by remember { mutableStateOf(true) }
    var query by remember { mutableStateOf("") }
    var filter by remember { mutableIntStateOf(if (settings.showSystemApps) 0 else 1) } // 0 все, 1 пользовательские, 2 системные

    suspend fun load() {
        loading = true
        val list = withContext(Dispatchers.IO) { AppScanner.listApps(context) }
        apps = list
        loading = false
    }

    LaunchedEffect(Unit) { load() }

    val filtered = remember(apps, query, filter) {
        apps.filter {
            val byFilter = when (filter) {
                0 -> true
                1 -> !it.isSystem
                else -> it.isSystem
            }
            val byQuery = query.isBlank() ||
                it.label.contains(query, ignoreCase = true) ||
                it.pkg.contains(query, ignoreCase = true)
            byFilter && byQuery
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Приложения (${filtered.size})") },
                actions = {
                    IconButton(onClick = { scope.launch { load() } }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Обновить")
                    }
                }
            )
        }
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(padding)) {
            Column(Modifier.padding(horizontal = 16.dp, vertical = 8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SearchField(query, { query = it }, "Поиск по имени или пакету")
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    FilterChip(selected = filter == 0, onClick = { filter = 0 }, label = { Text("Все") })
                    FilterChip(selected = filter == 1, onClick = { filter = 1 }, label = { Text("Пользовательские") })
                    FilterChip(selected = filter == 2, onClick = { filter = 2 }, label = { Text("Системные") })
                }
            }
            when {
                loading -> LoadingBox("Читаю список приложений…")
                filtered.isEmpty() -> EmptyBox("Ничего не найдено")
                else -> LazyColumn(Modifier.fillMaxSize()) {
                    items(filtered, key = { it.pkg }) { app ->
                        ListItem(
                            headlineContent = { Text(app.label) },
                            supportingContent = {
                                Column {
                                    Text(app.pkg, style = MaterialTheme.typography.bodySmall)
                                    if (app.versionName != null) {
                                        Text("v${app.versionName}", style = MaterialTheme.typography.bodySmall)
                                    }
                                }
                            },
                            leadingContent = { AppIcon(app.icon) },
                            trailingContent = {
                                if (app.isSystem) {
                                    Badge(
                                        "Система",
                                        MaterialTheme.colorScheme.surfaceContainerHigh,
                                        MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            },
                            modifier = Modifier.clickable { onAppClick(app.pkg) }
                        )
                    }
                }
            }
        }
    }
}
