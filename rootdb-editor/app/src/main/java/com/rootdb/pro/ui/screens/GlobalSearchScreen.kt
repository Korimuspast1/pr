package com.rootdb.pro.ui.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
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
import com.rootdb.pro.data.SearchHit
import com.rootdb.pro.ui.components.EmptyBox
import com.rootdb.pro.ui.components.LoadingBox
import com.rootdb.pro.ui.components.SearchField
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** Глобальный поиск значения по всем базам приложения */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GlobalSearchScreen(
    pkg: String,
    onBack: () -> Unit,
    onOpenTable: (String, String) -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var query by remember { mutableStateOf("") }
    var hits by remember { mutableStateOf<List<SearchHit>>(emptyList()) }
    var searching by remember { mutableStateOf(false) }
    var searched by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Поиск по базам") },
                navigationIcon = { IconButton(onClick = onBack) { Icon(Icons.Filled.ArrowBack, contentDescription = "Назад") } }
            )
        }
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(padding)) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SearchField(query, { query = it }, "Что ищем? (например, coins, premium, token)")
                Button(
                    onClick = {
                        scope.launch {
                            searching = true
                            searched = true
                            hits = withContext(Dispatchers.IO) { DbManager.searchAll(context, pkg, query.trim()) }
                            searching = false
                        }
                    },
                    enabled = !searching && query.isNotBlank(),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Icon(Icons.Filled.Search, null)
                    Text(if (searching) "Ищу по всем базам…" else "Найти во всех базах")
                }
            }
            when {
                searching -> LoadingBox("Сканирую все базы и таблицы…")
                searched && hits.isEmpty() -> EmptyBox("Ничего не найдено")
                hits.isNotEmpty() -> LazyColumn(Modifier.fillMaxSize()) {
                    items(hits, key = { "${it.dbPath}|${it.table}|${it.column}|${it.rowId}|${it.valuePreview}" }) { hit ->
                        Card(
                            shape = MaterialTheme.shapes.medium,
                            modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp)
                                .clickable { onOpenTable(hit.dbPath, hit.table) }
                        ) {
                            ListItem(
                                headlineContent = { Text(hit.valuePreview, fontWeight = FontWeight.Bold) },
                                supportingContent = {
                                    Text("${hit.dbName} → ${hit.table}.${hit.column}" + (hit.rowId?.let { " (rowid=$it)" } ?: ""))
                                }
                            )
                        }
                    }
                }
                else -> EmptyBox("Введите запрос и нажмите «Найти». Поиск идёт по всем SQLite-базам приложения.")
            }
        }
    }
}
