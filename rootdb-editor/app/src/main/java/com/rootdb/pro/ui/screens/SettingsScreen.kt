package com.rootdb.pro.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.rootdb.pro.data.SettingsStore
import com.rootdb.pro.ui.components.SectionTitle

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(onThemeChanged: (Int) -> Unit, onDynamicColorChanged: (Boolean) -> Unit) {
    val context = LocalContext.current
    val settings = remember { SettingsStore(context) }

    var themeMode by remember { mutableIntStateOf(settings.themeMode) }
    var dynamicColor by remember { mutableStateOf(settings.dynamicColor) }
    var autoSave by remember { mutableStateOf(settings.autoSave) }
    var forceStop by remember { mutableStateOf(settings.forceStopAfterSave) }
    var showSystem by remember { mutableStateOf(settings.showSystemApps) }
    var pageSize by remember { mutableIntStateOf(settings.pageSize) }

    Scaffold(topBar = { TopAppBar(title = { Text("Настройки") }) }) { padding ->
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(padding).padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            SectionTitle("Оформление (Material Expressive)")
            Card(shape = MaterialTheme.shapes.extraLarge) {
                Column(Modifier.fillMaxWidth().padding(20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text("Тема", fontWeight = FontWeight.Bold)
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        FilterChip(selected = themeMode == 0, onClick = { themeMode = 0; settings.themeMode = 0; onThemeChanged(0) }, label = { Text("Системная") })
                        FilterChip(selected = themeMode == 1, onClick = { themeMode = 1; settings.themeMode = 1; onThemeChanged(1) }, label = { Text("Светлая") })
                        FilterChip(selected = themeMode == 2, onClick = { themeMode = 2; settings.themeMode = 2; onThemeChanged(2) }, label = { Text("Тёмная") })
                    }
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text("Динамический цвет (Material You)", fontWeight = FontWeight.Bold)
                            Text("Цвета под обои на Android 12+", style = MaterialTheme.typography.bodySmall)
                        }
                        Switch(checked = dynamicColor, onCheckedChange = {
                            dynamicColor = it
                            settings.dynamicColor = it
                            onDynamicColorChanged(it)
                        })
                    }
                }
            }

            SectionTitle("Редактирование")
            Card(shape = MaterialTheme.shapes.extraLarge) {
                Column(Modifier.fillMaxWidth().padding(20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text("Автосохранение", fontWeight = FontWeight.Bold)
                            Text("Писать правки в приложение сразу", style = MaterialTheme.typography.bodySmall)
                        }
                        Switch(checked = autoSave, onCheckedChange = { autoSave = it; settings.autoSave = it })
                    }
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text("Останавливать приложение после сохранения", fontWeight = FontWeight.Bold)
                            Text("Чтобы оно не перезаписало правки при следующем запуске", style = MaterialTheme.typography.bodySmall)
                        }
                        Switch(checked = forceStop, onCheckedChange = { forceStop = it; settings.forceStopAfterSave = it })
                    }
                    Text("Строк на страницу", fontWeight = FontWeight.Bold)
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        listOf(50, 100, 200).forEach { n ->
                            FilterChip(selected = pageSize == n, onClick = { pageSize = n; settings.pageSize = n }, label = { Text("$n") })
                        }
                    }
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text("Показывать системные приложения", fontWeight = FontWeight.Bold)
                            Text("По умолчанию список — только пользовательские", style = MaterialTheme.typography.bodySmall)
                        }
                        Switch(checked = showSystem, onCheckedChange = { showSystem = it; settings.showSystemApps = it })
                    }
                }
            }

            SectionTitle("О приложении")
            Card(shape = MaterialTheme.shapes.extraLarge) {
                Column(Modifier.fillMaxWidth().padding(20.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text("RootDB Pro v1.0.0", fontWeight = FontWeight.Bold)
                    Text(
                        "Root-редактор локальных баз: список приложений, сканирование SQLite/SQLCipher, правки, расшифровка, бэкапы и фоновая защита Guardian. Дизайн — Material Expressive.",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    Text(
                        "Используйте только на своём устройстве и для своих данных.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}
