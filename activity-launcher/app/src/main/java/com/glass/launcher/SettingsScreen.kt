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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.ui.graphics.toArgb
import androidx.compose.material.icons.rounded.Contrast
import androidx.compose.material.icons.rounded.Flare
import androidx.compose.material.icons.rounded.FormatColorFill
import androidx.compose.material.icons.rounded.FormatSize
import androidx.compose.material.icons.rounded.Gradient
import androidx.compose.material.icons.rounded.KeyboardArrowDown
import androidx.compose.material.icons.rounded.KeyboardArrowUp
import androidx.compose.material.icons.rounded.Label
import androidx.compose.material.icons.rounded.Numbers
import androidx.compose.material.icons.rounded.Palette
import androidx.compose.material.icons.rounded.PhotoSizeSelectLarge
import androidx.compose.material.icons.rounded.RestartAlt
import androidx.compose.material.icons.rounded.RoundedCorner
import androidx.compose.material.icons.rounded.Wallpaper
import androidx.compose.material.icons.rounded.WorkspacePremium
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
    var colorEdit by remember { mutableStateOf(0) } // 0 none, 1 = colour A, 2 = colour B
    val wallpaperLauncher = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        if (uri != null) scope.launch {
            val path = withContext(Dispatchers.IO) {
                java.io.File(ctx.filesDir, "wallpaper").listFiles()?.forEach { it.delete() }
                Media.saveScaled(ctx, uri, "wallpaper", "bg_${System.currentTimeMillis()}.jpg", 1600, false)
            }
            if (path != null) {
                Store.bgImage = path
                Store.bgMode = "image"
            } else Launcher.toast(ctx, tr("Could not load image", "Не удалось загрузить картинку"))
        }
    }

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
            // ───────── PRO
            ProCard()

            // ───────── appearance
            SectionTitle(tr("Liquid Glass", "Liquid Glass"))
            GlassCard {
                Text(tr("Theme", "Тема"), color = g.text, fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(10.dp))
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
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
                    AccentDot(Brush.linearGradient(listOf(Color(Store.customA), Color(Store.customB))), Store.accent == -2,
                        tr("Custom", "Свой")) { Store.accent = -2; colorEdit = 1 }
                }
                if (Store.accent == -2) {
                    Spacer(Modifier.height(10.dp))
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        GlassButton(tr("Colour 1", "Цвет 1"), Icons.Rounded.Palette, Modifier.weight(1f), primary = false) { colorEdit = 1 }
                        GlassButton(tr("Colour 2", "Цвет 2"), Icons.Rounded.Palette, Modifier.weight(1f), primary = false) { colorEdit = 2 }
                    }
                }
                Spacer(Modifier.height(18.dp))
                SliderRow(Icons.Rounded.BlurOn, tr("Blur strength", "Сила размытия"), "${Store.blur.toInt()} dp",
                    Store.blur, 0f..48f) { Store.blur = it }
                SliderRow(Icons.Rounded.Opacity, tr("Glass density", "Плотность стекла"), "${(Store.glassOpacity * 100).toInt()}%",
                    Store.glassOpacity, 0f..1f) { Store.glassOpacity = it }
                SliderRow(Icons.Rounded.Flare, tr("Refraction & rim light", "Преломление и блики"), "${(Store.refraction * 100).toInt()}%",
                    Store.refraction, 0f..1f) { Store.refraction = it }
                Spacer(Modifier.height(6.dp))
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    GlassChip(tr("Crystal", "Кристалл"), false) { Store.blur = 14f; Store.glassOpacity = 0.15f; Store.refraction = 0.9f }
                    GlassChip(tr("Clear", "Прозрачное"), false) { Store.blur = 22f; Store.glassOpacity = 0.3f; Store.refraction = 0.7f }
                    GlassChip(tr("Frosted", "Матовое"), false) { Store.blur = 40f; Store.glassOpacity = 0.6f; Store.refraction = 0.5f }
                    GlassChip(tr("Solid", "Плотное"), false) { Store.blur = 30f; Store.glassOpacity = 0.95f; Store.refraction = 0.3f }
                }
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

            // ───────── background
            SectionTitle(tr("Background", "Фон"))
            GlassCard {
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    GlassChip(tr("Liquid", "Жидкий"), Store.bgMode == "blobs", Icons.Rounded.AutoAwesome) { Store.bgMode = "blobs" }
                    GlassChip(tr("Gradient", "Градиент"), Store.bgMode == "gradient", Icons.Rounded.Gradient) { Store.bgMode = "gradient" }
                    GlassChip(tr("Solid", "Однотонный"), Store.bgMode == "solid", Icons.Rounded.FormatColorFill) { Store.bgMode = "solid" }
                    GlassChip(tr("Photo", "Фото"), Store.bgMode == "image", Icons.Rounded.Wallpaper) {
                        if (Store.bgImage.isBlank()) wallpaperLauncher.launch("image/*") else Store.bgMode = "image"
                    }
                }
                Spacer(Modifier.height(12.dp))
                GlassButton(tr("Choose photo…", "Выбрать фото…"), Icons.Rounded.Wallpaper, primary = false) { wallpaperLauncher.launch("image/*") }
                if (Store.bgMode == "image") {
                    Spacer(Modifier.height(10.dp))
                    SliderRow(Icons.Rounded.Contrast, tr("Photo dimming", "Затемнение фото"), "${(Store.bgDim * 100).toInt()}%",
                        Store.bgDim, 0f..0.9f) { Store.bgDim = it }
                }
            }

            // ───────── interface
            SectionTitle(tr("Interface", "Интерфейс"))
            GlassCard {
                SliderRow(Icons.Rounded.RoundedCorner, tr("Corner radius", "Скругление"), "${Store.cornerRadius.toInt()} dp",
                    Store.cornerRadius, 4f..40f) { Store.cornerRadius = it }
                SliderRow(Icons.Rounded.FormatSize, tr("Text size", "Размер текста"), "${(Store.fontScale * 100).toInt()}%",
                    Store.fontScale, 0.8f..1.4f) { Store.fontScale = it }
                SliderRow(Icons.Rounded.PhotoSizeSelectLarge, tr("Icon size", "Размер иконок"), "${Store.iconSize.toInt()} dp",
                    Store.iconSize, 32f..72f) { Store.iconSize = it }
            }
            GlassCard(padding = PaddingValues(vertical = 4.dp)) {
                ToggleRow(Icons.Rounded.Label, tr("Badges (SYS, MAIN, PRIVATE…)", "Метки (СИС, MAIN, ЗАКРЫТА…)"), null, Store.showBadges) { Store.showBadges = it }
                ToggleRow(Icons.Rounded.Numbers, tr("Activity counters", "Счётчики активити"), null, Store.showCounts) { Store.showCounts = it }
                ToggleRow(Icons.Rounded.ShortText, tr("Tab bar labels", "Подписи в панели вкладок"), null, Store.navLabels) { Store.navLabels = it }
            }
            GlassCard {
                Text(tr("Tab bar order", "Порядок вкладок"), color = g.text, fontWeight = FontWeight.SemiBold)
                Text(tr("Tip: grab the bubble in the tab bar and drag it left / right.",
                    "Совет: зажмите «каплю» в нижней панели и тащите влево / вправо."), color = g.textDim, fontSize = 12.sp)
                Spacer(Modifier.height(10.dp))
                val names = listOf(tr("All", "Все"), tr("Favorites", "Избранное"), tr("Recents", "Недавние"))
                val order = parseTabOrder(Store.tabOrder)
                order.forEachIndexed { idx, id ->
                    Row(Modifier.fillMaxWidth().padding(vertical = 3.dp), verticalAlignment = Alignment.CenterVertically) {
                        Text("${idx + 1}.  ${names[id]}", color = g.text, modifier = Modifier.weight(1f))
                        GlassIconButton(Icons.Rounded.KeyboardArrowUp, null, size = 36.dp) {
                            if (idx > 0) {
                                val m = order.toMutableList(); m.add(idx - 1, m.removeAt(idx)); Store.tabOrder = m.joinToString(",")
                            }
                        }
                        Spacer(Modifier.width(6.dp))
                        GlassIconButton(Icons.Rounded.KeyboardArrowDown, null, size = 36.dp) {
                            if (idx < order.lastIndex) {
                                val m = order.toMutableList(); m.add(idx + 1, m.removeAt(idx)); Store.tabOrder = m.joinToString(",")
                            }
                        }
                    }
                }
                Spacer(Modifier.height(12.dp))
                Text(tr("Start tab", "Стартовая вкладка"), color = g.text, fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(8.dp))
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    names.forEachIndexed { i, n -> GlassChip(n, Store.startTab == i) { Store.startTab = i } }
                }
            }
            GlassCard {
                Text(tr("Screen titles", "Заголовки экранов"), color = g.text, fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(10.dp))
                GlassField(tr("All apps", "Все приложения"), Store.titleApps, { Store.titleApps = it }, singleLine = true, placeholder = tr("Packages", "Пакеты"))
                Spacer(Modifier.height(8.dp))
                GlassField(tr("Favorites", "Избранное"), Store.titleFav, { Store.titleFav = it }, singleLine = true, placeholder = tr("Favorites", "Избранное"))
                Spacer(Modifier.height(8.dp))
                GlassField(tr("Recents", "Недавние"), Store.titleRecents, { Store.titleRecents = it }, singleLine = true, placeholder = tr("Recents", "Недавние"))
            }

            // ───────── language
            SectionTitle(tr("Language", "Язык"))
            GlassCard {
                Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
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
            GlassButton(tr("Reset appearance to defaults", "Сбросить оформление"), Icons.Rounded.RestartAlt, primary = false) {
                Store.blur = 26f; Store.glassOpacity = 0.5f; Store.refraction = 0.6f; Store.cornerRadius = 26f
                Store.fontScale = 1f; Store.iconSize = 52f; Store.bgMode = "blobs"; Store.accent = 0; Store.theme = "system"
                Store.showBadges = true; Store.showCounts = true; Store.navLabels = true; Store.tabOrder = "0,1,2"
                Store.titleApps = ""; Store.titleFav = ""; Store.titleRecents = ""
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
    if (colorEdit != 0) {
        val initial = Color(if (colorEdit == 1) Store.customA else Store.customB)
        ColorPickerDialog(
            if (colorEdit == 1) tr("Accent colour 1", "Цвет акцента 1") else tr("Accent colour 2", "Цвет акцента 2"),
            initial, onDismiss = { colorEdit = 0 },
        ) { c ->
            if (colorEdit == 1) Store.customA = c.toArgb() else Store.customB = c.toArgb()
            Store.accent = -2
            colorEdit = 0
        }
    }
}

@Composable
private fun ProCard() {
    val g = glass
    val shape = RoundedCornerShape(Store.cornerRadius.dp)
    Column(
        Modifier
            .padding(top = 4.dp)
            .fillMaxWidth()
            .clip(shape)
            .background(Brush.linearGradient(listOf(Color(0xFFFFB84D), Color(0xFFFF6A6A), Color(0xFF9B5CFF), Color(0xFF4F7CFF))))
            .glassRim(g.copy(stroke = Color.White.copy(alpha = .85f), highlight = Color.White.copy(alpha = .35f), refraction = 1f), shape)
            .padding(20.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                Modifier.size(52.dp).clip(CircleShape).background(Color.White.copy(alpha = .22f))
                    .border(1.dp, Color.White.copy(alpha = .7f), CircleShape),
                contentAlignment = Alignment.Center,
            ) { Icon(Icons.Rounded.WorkspacePremium, null, tint = Color.White, modifier = Modifier.size(30.dp)) }
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Text("Glass Launcher PRO", color = Color.White, fontSize = 20.sp, fontWeight = FontWeight.Black)
                Text(tr("Subscription active", "Подписка активна"), color = Color.White.copy(alpha = .9f), fontSize = 14.sp,
                    fontWeight = FontWeight.SemiBold)
            }
            Text("∞", color = Color.White, fontSize = 38.sp, fontWeight = FontWeight.Black)
        }
        Spacer(Modifier.height(14.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            ProPill(tr("Expires: never", "Истекает: никогда"))
            ProPill(tr("All features unlocked", "Все функции открыты"))
        }
    }
}

@Composable
private fun ProPill(text: String) {
    Box(
        Modifier.clip(CircleShape).background(Color.White.copy(alpha = .22f)).border(0.8.dp, Color.White.copy(alpha = .6f), CircleShape)
            .padding(horizontal = 10.dp, vertical = 5.dp),
    ) { Text(text, color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.Bold, maxLines = 1) }
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
