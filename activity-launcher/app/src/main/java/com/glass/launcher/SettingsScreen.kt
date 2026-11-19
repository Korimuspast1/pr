package com.glass.launcher

import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AutoAwesome
import androidx.compose.material.icons.rounded.BlurOn
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material.icons.rounded.DarkMode
import androidx.compose.material.icons.rounded.DeleteSweep
import androidx.compose.material.icons.rounded.Download
import androidx.compose.material.icons.rounded.History
import androidx.compose.material.icons.rounded.LightMode
import androidx.compose.material.icons.rounded.Opacity
import androidx.compose.material.icons.rounded.PhoneAndroid
import androidx.compose.material.icons.rounded.Refresh
import androidx.compose.material.icons.rounded.Security
import androidx.compose.material.icons.rounded.ShortText
import androidx.compose.material.icons.rounded.Shortcut
import androidx.compose.material.icons.rounded.Upload
import androidx.compose.material.icons.rounded.Vibration
import androidx.compose.material.icons.rounded.Visibility
import androidx.compose.material3.Icon
import androidx.compose.material3.Slider
import androidx.compose.material3.SliderDefaults
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@Composable
fun SettingsScreen(nav: Nav) {
    val ctx = LocalContext.current
    val g = glass
    val scope = rememberCoroutineScope()
    var rootStatus by remember { mutableStateOf<Boolean?>(null) }

    val exportLauncher = rememberLauncherForActivityResult(ActivityResultContracts.CreateDocument("application/json")) { uri ->
        if (uri != null) scope.launch {
            val ok = withContext(Dispatchers.IO) {
                try {
                    ctx.contentResolver.openOutputStream(uri)?.use { it.write(Store.exportJson().toByteArray()) }; true
                } catch (e: Throwable) { false }
            }
            Launcher.toast(ctx, if (ok) tr("Exported", "Экспортировано") else tr("Export failed", "Ошибка экспорта"))
        }
    }
    val importLauncher = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        if (uri != null) scope.launch {
            val text = withContext(Dispatchers.IO) {
                try { ctx.contentResolver.openInputStream(uri)?.use { it.readBytes().toString(Charsets.UTF_8) } } catch (e: Throwable) { null }
            }
            val n = try { if (text == null) -1 else Store.importJson(text) } catch (e: Throwable) { -1 }
            Launcher.toast(ctx, if (n >= 0) tr("Imported: $n", "Импортировано: $n") else tr("Invalid file", "Неверный файл"))
        }
    }

    GlassScaffold(topBar = { haze -> GlassTopBar(haze, tr("Settings", "Настройки"), onBack = { nav.pop() }) }) { pad ->
        Column(
            Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(start = 16.dp, end = 16.dp, top = pad.calculateTopPadding(), bottom = pad.calculateBottomPadding()),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            // ───────── appearance
            SectionTitle(tr("Liquid Glass", "Liquid Glass"))
            GlassCard {
                Text(tr("Theme", "Тема"), color = g.text, fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(10.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    GlassChip(tr("System", "Системная"), Store.theme == "system", Icons.Rounded.PhoneAndroid) { Store.theme = "system" }
                    GlassChip(tr("Light", "Светлая"), Store.theme == "light", Icons.Rounded.LightMode) { Store.theme = "light" }
                    GlassChip(tr("Dark", "Тёмная"), Store.theme == "dark", Icons.Rounded.DarkMode) { Store.theme = "dark" }
                }
                Spacer(Modifier.height(18.dp))
                Text(tr("Accent", "Акцент"), color = g.text, fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(10.dp))
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    if (Build.VERSION.SDK_INT >= 31) {
                        AccentDot(
                            Brush.sweepGradient(listOf(Color(0xFFFF5E5E), Color(0xFFFFD45E), Color(0xFF5EFF9B), Color(0xFF5EB8FF), Color(0xFFC55EFF), Color(0xFFFF5E5E))),
                            Store.accent < 0, tr("Dynamic", "Обои"),
                        ) { Store.accent = -1 }
                    }
                    ACCENTS.forEachIndexed { i, a ->
                        AccentDot(Brush.linearGradient(listOf(a.a, a.b)), Store.accent == i, if (isRu()) a.nameRu else a.nameEn) { Store.accent = i }
                    }
                }
                Spacer(Modifier.height(18.dp))
                SliderRow(Icons.Rounded.BlurOn, tr("Blur strength", "Сила размытия"), "${Store.blur.toInt()} dp",
                    Store.blur, 0f..48f) { Store.blur = it }
                SliderRow(Icons.Rounded.Opacity, tr("Glass density", "Плотность стекла"), "${(Store.glassOpacity * 100).toInt()}%",
                    Store.glassOpacity, 0f..1f) { Store.glassOpacity = it }
                if (Build.VERSION.SDK_INT < 32) {
                    Text(tr("Real-time backdrop blur needs Android 12L+. Older devices use frosted tint instead.",
                        "Живое размытие фона работает на Android 12L+. На старых версиях используется матовая подложка."),
                        color = g.textDim, fontSize = 12.sp)
                }
            }
            GlassCard(padding = PaddingValues(vertical = 4.dp)) {
                ToggleRow(Icons.Rounded.AutoAwesome, tr("Animated liquid background", "Живой анимированный фон"),
                    tr("Floating colour blobs behind the glass", "Плавающие цветные пятна под стеклом"), Store.animatedBg) { Store.animatedBg = it }
                ToggleRow(Icons.Rounded.ShortText, tr("Compact list", "Компактный список"), null, Store.compactList) { Store.compactList = it }
                ToggleRow(Icons.Rounded.Vibration, tr("Haptic feedback", "Виброотклик"), null, Store.haptics) { Store.haptics = it }
            }

            // ───────── language
            SectionTitle(tr("Language", "Язык"))
            GlassCard {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    GlassChip(tr("System", "Системный"), Store.lang == "system") { Store.lang = "system" }
                    GlassChip("English", Store.lang == "en") { Store.lang = "en" }
                    GlassChip("Русский", Store.lang == "ru") { Store.lang = "ru" }
                }
            }

            // ───────── behaviour
            SectionTitle(tr("Behaviour", "Поведение"))
            GlassCard(padding = PaddingValues(vertical = 4.dp)) {
                ToggleRow(Icons.Rounded.Visibility, tr("Show system apps by default", "Показывать системные по умолчанию"), null,
                    Store.showSystem) { Store.showSystem = it }
                ToggleRow(Icons.Rounded.Security, tr("Root fallback", "Root при отказе"),
                    tr("Automatically retry with su when an activity is protected", "Автоматически запускать через su, если активити защищена"),
                    Store.rootFallback) { Store.rootFallback = it }
                ToggleRow(Icons.Rounded.History, tr("Keep launch history", "Сохранять историю запусков"), null,
                    Store.historyEnabled) { Store.historyEnabled = it }
                ToggleRow(Icons.Rounded.Shortcut, tr("App icon shortcuts", "Ярлыки на иконке приложения"),
                    tr("Top 4 favorites in the long-press menu", "Первые 4 избранных в меню долгого нажатия"),
                    Store.dynamicShortcuts) { Store.dynamicShortcuts = it; Launcher.updateDynamicShortcuts(ctx) }
            }

            // ───────── root
            SectionTitle("Root")
            GlassCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text(tr("Root access", "Root-доступ"), color = g.text, fontWeight = FontWeight.SemiBold)
                        Text(
                            when (rootStatus) {
                                null -> tr("Not checked", "Не проверено")
                                true -> tr("Granted ✓", "Есть ✓")
                                false -> tr("Not available", "Недоступен")
                            },
                            color = when (rootStatus) { true -> Color(0xFF22C55E); false -> Color(0xFFEF4444); null -> g.textDim },
                            fontSize = 13.sp,
                        )
                    }
                    GlassButton(tr("Check", "Проверить"), Icons.Rounded.Security, Modifier.width(150.dp), primary = false) {
                        scope.launch { rootStatus = Launcher.checkRoot() }
                    }
                }
            }

            // ───────── data
            SectionTitle(tr("Data", "Данные"))
            GlassButton(tr("Export favorites (JSON)", "Экспорт избранного (JSON)"), Icons.Rounded.Upload, primary = false) {
                exportLauncher.launch("glass-launcher-favorites.json")
            }
            GlassButton(tr("Import favorites", "Импорт избранного"), Icons.Rounded.Download, primary = false) {
                importLauncher.launch(arrayOf("application/json", "text/plain", "*/*"))
            }
            GlassButton(tr("Refresh app list", "Обновить список приложений"), Icons.Rounded.Refresh, primary = false) {
                scope.launch { Pkg.load(ctx, force = true) }
            }
            GlassButton(tr("Clear history", "Очистить историю"), Icons.Rounded.DeleteSweep, primary = false) {
                Store.clearRecents(); Launcher.toast(ctx, tr("History cleared", "История очищена"))
            }

            // ───────── about
            SectionTitle(tr("About", "О приложении"))
            GlassCard {
                Text("Glass Activity Launcher ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})",
                    color = g.text, fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(4.dp))
                Text("Android ${Build.VERSION.RELEASE} · API ${Build.VERSION.SDK_INT} · ${Build.MANUFACTURER} ${Build.MODEL}",
                    color = g.textDim, fontSize = 13.sp)
                Spacer(Modifier.height(4.dp))
                Text(tr("Liquid Glass UI · Jetpack Compose", "Интерфейс Liquid Glass · Jetpack Compose"), color = g.textDim, fontSize = 13.sp)
            }
        }
    }
}

@Composable
private fun AccentDot(brush: Brush, selected: Boolean, label: String, onClick: () -> Unit) {
    val g = glass
    Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.width(60.dp)) {
        Box(
            Modifier
                .size(48.dp)
                .clip(CircleShape)
                .background(brush)
                .border(if (selected) 3.dp else 1.dp, if (selected) g.text else Color.White.copy(alpha = .6f), CircleShape)
                .glassClick(onClick = onClick),
            contentAlignment = Alignment.Center,
        ) {
            if (selected) Icon(Icons.Rounded.Check, null, tint = Color.White)
        }
        Spacer(Modifier.height(4.dp))
        Text(label, color = g.textDim, fontSize = 11.sp, maxLines = 1)
    }
}

@Composable
private fun ColumnScope.SliderRow(
    icon: ImageVector, title: String, value: String, v: Float, range: ClosedFloatingPointRange<Float>, onChange: (Float) -> Unit,
) {
    val g = glass
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, null, tint = g.accent, modifier = Modifier.size(20.dp))
        Spacer(Modifier.width(10.dp))
        Text(title, color = g.text, fontWeight = FontWeight.Medium, modifier = Modifier.weight(1f))
        Text(value, color = g.textDim, fontSize = 13.sp)
    }
    Slider(
        value = v, onValueChange = onChange, valueRange = range,
        colors = SliderDefaults.colors(thumbColor = g.accent, activeTrackColor = g.accent, inactiveTrackColor = g.fillStrong),
        modifier = Modifier.fillMaxWidth(),
    )
}

@Composable
private fun ToggleRow(icon: ImageVector, title: String, subtitle: String?, checked: Boolean, onChange: (Boolean) -> Unit) {
    val g = glass
    Row(
        Modifier.fillMaxWidth().glassClick(onClick = { onChange(!checked) }).padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(icon, null, tint = g.accent, modifier = Modifier.size(22.dp))
        Spacer(Modifier.width(14.dp))
        Column(Modifier.weight(1f)) {
            Text(title, color = g.text, fontWeight = FontWeight.Medium, fontSize = 15.sp)
            if (subtitle != null) Text(subtitle, color = g.textDim, fontSize = 12.sp)
        }
        Spacer(Modifier.width(8.dp))
        Switch(checked, onChange, colors = glassSwitchColors())
    }
}
