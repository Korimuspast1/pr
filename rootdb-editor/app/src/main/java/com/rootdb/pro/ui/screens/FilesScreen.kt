package com.rootdb.pro.ui.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.ArrowUpward
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.rootdb.pro.data.DirEntry
import com.rootdb.pro.data.RootShell
import com.rootdb.pro.data.formatDateTime
import com.rootdb.pro.data.formatSize
import com.rootdb.pro.ui.components.EmptyBox
import com.rootdb.pro.ui.components.LoadingBox
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** Root-браузер файлов приложения */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FilesScreen(pkg: String, onBack: () -> Unit) {
    val scope = rememberCoroutineScope()
    val rootDir = "/data/data/$pkg"
    var dir by remember { mutableStateOf(rootDir) }
    var entries by remember { mutableStateOf<List<DirEntry>>(emptyList()) }
    var loading by remember { mutableStateOf(true) }
    var preview by remember { mutableStateOf<Pair<String, String>?>(null) } // (имя, текст/hex)

    suspend fun load(path: String) {
        loading = true
        entries = withContext(Dispatchers.IO) { RootShell.listDir(path) }
        loading = false
    }

    LaunchedEffect(dir) { load(dir) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(dir.removePrefix(rootDir).ifBlank { "/" }) },
                navigationIcon = { IconButton(onClick = onBack) { Icon(Icons.Filled.ArrowBack, contentDescription = "Назад") } },
                actions = {
                    if (dir != rootDir) {
                        IconButton(onClick = { dir = dir.substringBeforeLast("/").ifBlank { rootDir } }) {
                            Icon(Icons.Filled.ArrowUpward, contentDescription = "Вверх")
                        }
                    }
                }
            )
        }
    ) { padding ->
        when {
            loading -> LoadingBox("Читаю каталог через root…")
            entries.isEmpty() -> EmptyBox("Пусто или нет доступа")
            else -> LazyColumn(Modifier.fillMaxSize().padding(padding)) {
                items(entries, key = { it.path }) { e ->
                    ListItem(
                        headlineContent = { Text(e.name, fontWeight = if (e.isDir) FontWeight.Bold else FontWeight.Normal) },
                        supportingContent = {
                            Text(if (e.isDir) "Папка" else "${formatSize(e.size)} • ${formatDateTime(e.modifiedSec)}")
                        },
                        leadingContent = {
                            Icon(
                                if (e.isDir) Icons.Filled.Folder else Icons.Filled.Description,
                                null,
                                tint = if (e.isDir) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        },
                        modifier = Modifier.clickable {
                            if (e.isDir) {
                                dir = e.path
                            } else {
                                scope.launch {
                                    loading = true
                                    val text = withContext(Dispatchers.IO) {
                                        val raw = RootShell.readText(e.path, 64_000)
                                        if (raw != null && raw.all { it.isLetterOrDigit() || it.isWhitespace() || it in ".,;:!?()[]{}\"'@#\$%^&*-_=+<>|/\\~`" }) {
                                            raw
                                        } else {
                                            val bytes = RootShell.headBytes(e.path, 1024)
                                            if (bytes != null) bytes.joinToString("") { "%02x ".format(it) } else "(не удалось прочитать)"
                                        }
                                    }
                                    preview = e.name to (text ?: "(пусто)")
                                    loading = false
                                }
                            }
                        }
                    )
                }
            }
        }
    }

    preview?.let { (name, text) ->
        AlertDialog(
            onDismissRequest = { preview = null },
            title = { Text(name) },
            text = {
                Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState())) {
                    Text(text, fontFamily = FontFamily.Monospace, style = MaterialTheme.typography.bodySmall)
                }
            },
            confirmButton = { TextButton(onClick = { preview = null }) { Text("Закрыть") } }
        )
    }
}
