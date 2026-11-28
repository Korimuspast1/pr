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
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Key
import androidx.compose.material.icons.filled.LockOpen
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
import com.rootdb.pro.data.RootShell
import com.rootdb.pro.data.formatSize
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

/**
 * Расшифровка баз:
 * - SQLCipher → обычная SQLite (нужен ключ/пароль)
 * - обычная SQLite → SQLCipher (зашифровать)
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DecryptScreen(pkg: String, dbPath: String, onBack: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbar = remember { SnackbarHostState() }
    val dbName = dbPath.substringAfterLast("/")

    var password by remember { mutableStateOf("") }
    var mode by remember { mutableStateOf("decrypt") } // decrypt | encrypt
    var busy by remember { mutableStateOf(false) }
    var resultText by remember { mutableStateOf<String?>(null) }
    var outFile by remember { mutableStateOf<File?>(null) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Расшифровка: $dbName") },
                navigationIcon = { IconButton(onClick = onBack) { Icon(Icons.Filled.ArrowBack, contentDescription = "Назад") } }
            )
        },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(padding).padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Text(
                "SQLCipher-базы не имеют заголовка SQLite. Для расшифровки нужен тот же ключ/пароль, что использует приложение. Поддерживается пароль и hex-ключ (64 символа).",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                FilterChip(selected = mode == "decrypt", onClick = { mode = "decrypt" }, label = { Text("SQLCipher → SQLite") })
                FilterChip(selected = mode == "encrypt", onClick = { mode = "encrypt" }, label = { Text("SQLite → SQLCipher") })
            }
            OutlinedTextField(
                value = password,
                onValueChange = { password = it },
                label = { Text("Ключ / пароль") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth()
            )
            Button(
                onClick = {
                    scope.launch {
                        busy = true
                        resultText = null
                        try {
                            val workDir = File(context.cacheDir, "decrypt/$pkg").apply { mkdirs() }
                            val srcCopy = File(workDir, dbName)
                            if (!withContext(Dispatchers.IO) { RootShell.copyToAppDir(dbPath, srcCopy) }) {
                                throw Exception("Не удалось скопировать базу через root")
                            }
                            val outName = if (mode == "decrypt") "${dbName}_decrypted.db" else "${dbName}_encrypted.db"
                            val out = File(workDir, outName)
                            val res = withContext(Dispatchers.IO) {
                                if (mode == "decrypt") DbManager.decryptSqlCipher(context, srcCopy.absolutePath, password, out)
                                else DbManager.encryptSqlCipher(context, srcCopy.absolutePath, password, out)
                            }
                            resultText = res.message
                            if (res.ok) outFile = out
                        } catch (e: Exception) {
                            resultText = "Ошибка: ${e.message}"
                        }
                        busy = false
                    }
                },
                enabled = !busy && password.isNotBlank(),
                modifier = Modifier.fillMaxWidth()
            ) {
                Icon(if (mode == "decrypt") Icons.Filled.LockOpen else Icons.Filled.Key, null)
                Text(if (busy) "Работаю…" else if (mode == "decrypt") "Расшифровать" else "Зашифровать")
            }
            resultText?.let {
                Card(shape = MaterialTheme.shapes.large) {
                    Text(it, modifier = Modifier.padding(16.dp), fontWeight = FontWeight.Bold)
                }
            }
            outFile?.let { f ->
                Text("Готово: ${f.name} (${formatSize(f.length())})", style = MaterialTheme.typography.bodyMedium)
                Button(
                    onClick = {
                        scope.launch {
                            try {
                                val bytes = withContext(Dispatchers.IO) { f.readBytes() }
                                val where = withContext(Dispatchers.IO) { DbManager.exportToDownloads(context, f.name, bytes) }
                                snackbar.showSnackbar("Экспортировано: $where")
                            } catch (e: Exception) {
                                snackbar.showSnackbar("Ошибка: ${e.message}")
                            }
                        }
                    },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Icon(Icons.Filled.Download, null)
                    Text("Скачать в Загрузки")
                }
                Text(
                    "Расшифрованную базу можно открыть как обычную: положите файл в приложение через root или используйте SQL-консоль.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}
