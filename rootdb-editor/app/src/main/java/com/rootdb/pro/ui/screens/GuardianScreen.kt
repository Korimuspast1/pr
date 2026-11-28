package com.rootdb.pro.ui.screens

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Security
import androidx.compose.material.icons.filled.Stop
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import com.rootdb.pro.data.GuardianService
import com.rootdb.pro.data.GuardianState
import com.rootdb.pro.data.GuardianStore
import com.rootdb.pro.data.SettingsStore
import com.rootdb.pro.data.WatchItem
import com.rootdb.pro.data.WatchMode
import com.rootdb.pro.ui.components.Badge
import com.rootdb.pro.ui.components.SectionTitle
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/** Экран Guardian: фоновая защита изменённых баз */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GuardianScreen() {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val settings = remember { SettingsStore(context) }
    val store = remember { GuardianStore(context) }
    val running by GuardianState.running.collectAsState()
    val status by GuardianState.status.collectAsState()
    val events by GuardianState.events.collectAsState()

    var items by remember { mutableStateOf<List<WatchItem>>(emptyList()) }
    var interval by remember { mutableIntStateOf(settings.guardianIntervalSec) }
    var notifGranted by remember { mutableStateOf(true) }

    val notifLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        notifGranted = granted
    }

    fun reload() {
        items = store.loadItems()
    }

    LaunchedEffect(Unit) {
        reload()
        notifGranted = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED
        } else true
    }

    Scaffold(topBar = { TopAppBar(title = { Text("Guardian") }) }) { padding ->
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(padding).padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Card(shape = MaterialTheme.shapes.extraLarge) {
                Column(Modifier.fillMaxWidth().padding(20.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Icon(Icons.Filled.Security, null, tint = MaterialTheme.colorScheme.primary)
                        Text("Фоновая защита", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                        Badge(
                            if (running) "Работает" else "Остановлен",
                            if (running) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceContainerHigh,
                            if (running) MaterialTheme.colorScheme.onPrimaryContainer else MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Text(status, style = MaterialTheme.typography.bodyMedium)
                    Text(
                        "Guardian опрашивает базы каждые N секунд. Если приложение перезапишет правки — Guardian восстановит их из бэкапа или остановит приложение.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Button(
                            onClick = {
                                GuardianService.start(context)
                                store.enabled = true
                                scope.launch { reload() }
                            },
                            enabled = !running
                        ) {
                            Icon(Icons.Filled.PlayArrow, null)
                            Text("Запустить")
                        }
                        OutlinedButton(
                            onClick = {
                                GuardianService.stop(context)
                                store.enabled = false
                            },
                            enabled = running
                        ) {
                            Icon(Icons.Filled.Stop, null)
                            Text("Остановить")
                        }
                    }
                    Text("Интервал опроса:", style = MaterialTheme.typography.labelLarge)
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        listOf(1, 3, 5, 10).forEach { sec ->
                            FilterChip(
                                selected = interval == sec,
                                onClick = {
                                    interval = sec
                                    settings.guardianIntervalSec = sec
                                },
                                label = { Text("$sec сек") }
                            )
                        }
                    }
                    if (!notifGranted && Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                        OutlinedButton(onClick = { notifLauncher.launch(Manifest.permission.POST_NOTIFICATIONS) }) {
                            Text("Разрешить уведомления")
                        }
                    }
                    OutlinedButton(onClick = {
                        try {
                            val intent = Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS).apply {
                                data = Uri.parse("package:${context.packageName}")
                            }
                            context.startActivity(intent)
                        } catch (_: Exception) {
                            context.startActivity(Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS))
                        }
                    }) {
                        Text("Исключить из оптимизации батареи")
                    }
                }
            }

            SectionTitle("Отслеживаемые базы (${items.size})")
            if (items.isEmpty()) {
                Text(
                    "Пока пусто. Добавьте базу на экране базы данных: кнопка «Guardian».",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            } else {
                items.forEach { item ->
                    Card(shape = MaterialTheme.shapes.large, modifier = Modifier.fillMaxWidth()) {
                        Row(
                            Modifier.fillMaxWidth().padding(14.dp),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Column(Modifier.weight(1f)) {
                                Text(item.label, fontWeight = FontWeight.Bold)
                                Text(
                                    when (item.mode) {
                                        WatchMode.RESTORE -> "Восстанавливать из бэкапа"
                                        WatchMode.KILL -> "Останавливать приложение"
                                        WatchMode.NOTIFY -> "Только уведомления"
                                    },
                                    style = MaterialTheme.typography.bodySmall
                                )
                            }
                            IconButton(onClick = {
                                store.removeItem(item.dbPath)
                                reload()
                            }) {
                                Icon(Icons.Filled.Delete, contentDescription = "Убрать")
                            }
                        }
                    }
                }
            }

            SectionTitle("Лог событий")
            if (events.isEmpty()) {
                Text("Событий пока нет.", color = MaterialTheme.colorScheme.onSurfaceVariant)
            } else {
                events.forEach { e ->
                    Text(
                        "• ${e.text}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}
