package com.rootdb.pro.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Apps
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Security
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.rootdb.pro.data.GuardianState
import com.rootdb.pro.data.RootShell
import com.rootdb.pro.ui.components.Badge
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HomeScreen(onOpenApps: () -> Unit, onOpenGuardian: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var rooted by remember { mutableStateOf<Boolean?>(null) }
    var manager by remember { mutableStateOf("…") }
    var checking by remember { mutableStateOf(true) }
    val guardianRunning by GuardianState.running.collectAsState()
    val guardianStatus by GuardianState.status.collectAsState()

    suspend fun checkRoot() {
        checking = true
        val r = withContext(Dispatchers.IO) { RootShell.isRooted() }
        val m = withContext(Dispatchers.IO) { if (r) RootShell.rootManager() else "Нет root" }
        rooted = r
        manager = m
        checking = false
    }

    LaunchedEffect(Unit) { checkRoot() }

    Scaffold(
        topBar = { TopAppBar(title = { Text("RootDB Pro") }) }
    ) { padding ->
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(padding).padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Text(
                "Редактор локальных баз с Root",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold
            )
            Text(
                "Список всех приложений, сканирование баз, правки, расшифровка SQLCipher и фоновая защита Guardian.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )

            // Карточка root-статуса
            Card(
                colors = CardDefaults.cardColors(
                    containerColor = if (rooted == true) MaterialTheme.colorScheme.primaryContainer
                    else MaterialTheme.colorScheme.errorContainer
                ),
                shape = MaterialTheme.shapes.extraLarge
            ) {
                Column(Modifier.fillMaxWidth().padding(20.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Icon(
                            if (rooted == true) Icons.Filled.Check else Icons.Filled.Close,
                            contentDescription = null,
                            tint = if (rooted == true) MaterialTheme.colorScheme.onPrimaryContainer
                            else MaterialTheme.colorScheme.onErrorContainer
                        )
                        Text(
                            when {
                                checking -> "Проверяю root…"
                                rooted == true -> "Root есть: $manager"
                                else -> "Root не выдан"
                            },
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Bold
                        )
                    }
                    Text(
                        if (rooted == true) "Можно читать и менять базы любых приложений."
                        else "Нажмите кнопку и разрешите Root в Magisk/KernelSU. Без root доступны только свои базы.",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    Button(
                        onClick = { scope.launch { checkRoot() } },
                        enabled = !checking
                    ) {
                        Icon(Icons.Filled.Refresh, null)
                        Spacer(Modifier.padding(horizontal = 4.dp))
                        Text("Запросить root")
                    }
                }
            }

            // Guardian
            Card(shape = MaterialTheme.shapes.extraLarge) {
                Column(Modifier.fillMaxWidth().padding(20.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Icon(Icons.Filled.Security, null, tint = MaterialTheme.colorScheme.primary)
                        Text("Guardian", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                        Badge(
                            if (guardianRunning) "Работает" else "Выкл",
                            if (guardianRunning) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceContainerHigh,
                            if (guardianRunning) MaterialTheme.colorScheme.onPrimaryContainer else MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Text(guardianStatus, style = MaterialTheme.typography.bodyMedium)
                    Text(
                        "Следит за изменёнными базами в фоне и не даёт приложению откатить правки.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    OutlinedButton(onClick = onOpenGuardian) { Text("Открыть Guardian") }
                }
            }

            // Быстрые действия
            Button(onClick = onOpenApps, modifier = Modifier.fillMaxWidth()) {
                Icon(Icons.Filled.Apps, null)
                Spacer(Modifier.padding(horizontal = 4.dp))
                Text("Все приложения и их базы")
            }
            Spacer(Modifier.height(8.dp))
            Text(
                "RootDB Pro v1.0.0 • Material Expressive",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}
