package com.glass.launcher

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Add
import androidx.compose.material.icons.rounded.AddHome
import androidx.compose.material.icons.rounded.ArrowDropDown
import androidx.compose.material.icons.rounded.Code
import androidx.compose.material.icons.rounded.ContentCopy
import androidx.compose.material.icons.rounded.Delete
import androidx.compose.material.icons.rounded.ExpandLess
import androidx.compose.material.icons.rounded.ExpandMore
import androidx.compose.material.icons.rounded.Favorite
import androidx.compose.material.icons.rounded.FavoriteBorder
import androidx.compose.material.icons.rounded.Link
import androidx.compose.material.icons.rounded.RocketLaunch
import androidx.compose.material.icons.rounded.Save
import androidx.compose.material.icons.rounded.Security
import androidx.compose.material.icons.rounded.Settings
import androidx.compose.material.icons.rounded.Share
import androidx.compose.material.icons.rounded.Tune
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun EditorScreen(nav: Nav, initial: ShortcutData) {
    val ctx = LocalContext.current
    val g = glass
    val scope = rememberCoroutineScope()
    var d by remember { mutableStateOf(initial) }
    var catsText by remember { mutableStateOf(initial.categories.joinToString(", ")) }
    var advanced by remember {
        mutableStateOf(initial.action.isNotBlank() || initial.data.isNotBlank() || initial.mime.isNotBlank() ||
            initial.categories.isNotEmpty() || initial.flags != 0 || initial.extras.isNotEmpty())
    }
    var actionMenu by remember { mutableStateOf(false) }
    var rootDialog by remember { mutableStateOf<String?>(null) }
    val fav = Store.isFavorite(d)
    val storedFav = Store.favorites.any { it.id == d.id }

    fun doLaunch() {
        when (val r = Launcher.launch(ctx, d)) {
            is LaunchResult.Ok -> {}
            is LaunchResult.Error -> if (r.canRoot) rootDialog = r.message else Launcher.toast(ctx, r.message)
        }
    }

    fun launchRoot() {
        scope.launch {
            val err = withContext(Dispatchers.IO) { Launcher.launchRoot(d) }
            if (err == null) Store.addRecent(d) else Launcher.toast(ctx, err)
        }
    }

    GlassScaffold(
        topBar = { haze ->
            GlassTopBar(haze, tr("Edit shortcut", "Редактор ярлыка"), onBack = { nav.pop() }) {
                if (d.pkg.isNotBlank()) {
                    GlassIconButton(Icons.Rounded.Settings, tr("App info", "О приложении"), plain = true) {
                        Launcher.openAppInfo(ctx, d.pkg.trim())
                    }
                }
                GlassIconButton(
                    if (fav) Icons.Rounded.Favorite else Icons.Rounded.FavoriteBorder,
                    tr("Favorite", "Избранное"), tint = if (fav) Color(0xFFFF4D79) else null, plain = true,
                ) { Store.toggleFavorite(d) }
                GlassIconButton(Icons.Rounded.Share, tr("Share", "Поделиться"), plain = true) { Launcher.shareText(ctx, d) }
            }
        },
    ) { pad ->
        Column(
            Modifier
                .fillMaxSize()
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(start = 16.dp, end = 16.dp, top = pad.calculateTopPadding(), bottom = pad.calculateBottomPadding()),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            // ───────────── header preview
            GlassCard(padding = PaddingValues(18.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    val resIcon = if (d.icon.isNotBlank()) rememberResIcon(d.icon) else null
                    val actIcon = if (d.pkg.isNotBlank()) rememberIcon(d.pkg.trim(), d.cls.trim().ifBlank { null }) else null
                    Box(
                        Modifier.clip(RoundedCornerShape(20.dp)).glassClick(onClick = {
                            if (d.pkg.isNotBlank() && d.cls.isNotBlank()) d = d.copy(icon = Pkg.iconResName(ctx, d.pkg.trim(), d.cls.trim()))
                        }).padding(4.dp),
                    ) { AppIcon(resIcon ?: actIcon, 64.dp) }
                    Spacer(Modifier.width(14.dp))
                    Column(Modifier.weight(1f)) {
                        Text(d.displayName, color = g.text, fontSize = 20.sp, fontWeight = FontWeight.Bold, maxLines = 2)
                        Text(d.pkg.ifBlank { tr("No package (implicit intent)", "Без пакета (неявный интент)") },
                            color = g.textDim, fontSize = 13.sp, maxLines = 1)
                        if (d.root) Badge("ROOT", Color(0xFFEF4444))
                    }
                }
            }

            // ───────────── basic fields
            GlassField(tr("Name", "Название"), d.name, { d = d.copy(name = it) }, singleLine = true)
            GlassField(tr("Package", "Пакет"), d.pkg, { d = d.copy(pkg = it) }, singleLine = true, placeholder = "com.example.app")
            GlassField(tr("Class", "Класс"), d.cls, { d = d.copy(cls = it) }, placeholder = "com.example.app.MainActivity")
            GlassField(tr("Icon", "Иконка"), d.icon, { d = d.copy(icon = it) }, placeholder = "package:mipmap/ic_launcher")

            // ───────────── advanced intent
            GlassCard(padding = PaddingValues(horizontal = 16.dp, vertical = 12.dp), onClick = { advanced = !advanced }) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Tune, null, tint = g.accent)
                    Spacer(Modifier.width(12.dp))
                    Column(Modifier.weight(1f)) {
                        Text(tr("Intent builder", "Конструктор интента"), color = g.text, fontWeight = FontWeight.SemiBold)
                        Text(tr("Action, data, MIME, categories, flags, extras", "Action, data, MIME, категории, флаги, extras"),
                            color = g.textDim, fontSize = 12.sp)
                    }
                    Icon(if (advanced) Icons.Rounded.ExpandLess else Icons.Rounded.ExpandMore, null, tint = g.textDim)
                }
            }
            AnimatedVisibility(advanced, enter = expandVertically() + fadeIn(), exit = shrinkVertically() + fadeOut()) {
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    GlassField("Action", d.action, { d = d.copy(action = it) }, mono = true,
                        placeholder = "android.intent.action.VIEW",
                        trailing = {
                            Box {
                                GlassIconButton(Icons.Rounded.ArrowDropDown, null, plain = true, size = 32.dp) { actionMenu = true }
                                DropdownMenu(expanded = actionMenu, onDismissRequest = { actionMenu = false }) {
                                    Launcher.COMMON_ACTIONS.forEach { a ->
                                        DropdownMenuItem(text = { Text(a, fontSize = 13.sp) }, onClick = {
                                            d = d.copy(action = a); actionMenu = false
                                        })
                                    }
                                }
                            }
                        })
                    GlassField("Data URI", d.data, { d = d.copy(data = it) }, mono = true, placeholder = "https://… / tel:… / content://…")
                    GlassField("MIME type", d.mime, { d = d.copy(mime = it) }, singleLine = true, mono = true, placeholder = "text/plain")
                    GlassField(tr("Categories (comma separated)", "Категории (через запятую)"), catsText, {
                        catsText = it
                        d = d.copy(categories = it.split(',').map { c -> c.trim() }.filter { c -> c.isNotEmpty() })
                    }, mono = true, placeholder = "android.intent.category.DEFAULT")
                    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Launcher.COMMON_CATEGORIES.forEach { c ->
                            val on = c in d.categories
                            GlassChip(c.substringAfterLast('.'), on) {
                                val list = if (on) d.categories - c else d.categories + c
                                d = d.copy(categories = list)
                                catsText = list.joinToString(", ")
                            }
                        }
                    }

                    SectionTitle(tr("Flags", "Флаги") + "  (0x" + Integer.toHexString(d.flags) + ")")
                    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Launcher.FLAGS.forEach { (name, f) ->
                            val on = d.flags and f != 0
                            GlassChip(name, on) { d = d.copy(flags = if (on) d.flags and f.inv() else d.flags or f) }
                        }
                    }

                    SectionTitle("Extras")
                    d.extras.forEachIndexed { i, e ->
                        GlassCard(padding = PaddingValues(12.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                GlassField(tr("Key", "Ключ"), e.key, { v -> d = d.copy(extras = d.extras.replace(i, e.copy(key = v))) },
                                    modifier = Modifier.weight(1f), singleLine = true, mono = true)
                                Spacer(Modifier.width(6.dp))
                                GlassIconButton(Icons.Rounded.Delete, tr("Remove", "Удалить"), plain = true) {
                                    d = d.copy(extras = d.extras.filterIndexed { j, _ -> j != i })
                                }
                            }
                            Spacer(Modifier.height(8.dp))
                            FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                Extra.TYPES.forEach { t ->
                                    GlassChip(t, e.type == t) { d = d.copy(extras = d.extras.replace(i, e.copy(type = t))) }
                                }
                            }
                            Spacer(Modifier.height(8.dp))
                            GlassField(tr("Value", "Значение"), e.value, { v -> d = d.copy(extras = d.extras.replace(i, e.copy(value = v))) },
                                mono = true)
                        }
                    }
                    GlassButton(tr("Add extra", "Добавить extra"), Icons.Rounded.Add, primary = false) {
                        d = d.copy(extras = d.extras + Extra())
                    }
                }
            }

            // ───────────── root switch
            GlassCard(padding = PaddingValues(horizontal = 16.dp, vertical = 10.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Security, null, tint = Color(0xFFEF4444))
                    Spacer(Modifier.width(12.dp))
                    Column(Modifier.weight(1f)) {
                        Text(tr("Always launch as root", "Всегда запускать через root"), color = g.text, fontWeight = FontWeight.Medium)
                        Text(tr("Opens non-exported activities via su", "Открывает неэкспортируемые активити через su"),
                            color = g.textDim, fontSize = 12.sp)
                    }
                    Switch(d.root, { d = d.copy(root = it) }, colors = glassSwitchColors())
                }
            }

            // ───────────── actions
            GlassButton(tr("Launch activity", "Запустить активити"), Icons.Rounded.RocketLaunch) { doLaunch() }
            GlassButton(tr("Create shortcut", "Создать ярлык"), Icons.Rounded.AddHome) {
                if (!Launcher.pinShortcut(ctx, d)) Launcher.toast(ctx, tr("Launcher does not support pinned shortcuts", "Лаунчер не поддерживает ярлыки"))
            }
            GlassButton(tr("Share shortcut", "Поделиться ярлыком"), Icons.Rounded.Share) { Launcher.shareText(ctx, d) }
            GlassButton(
                if (fav) tr("Remove from favorites", "Убрать из избранного") else tr("Add to favorites", "В избранное"),
                if (fav) Icons.Rounded.Favorite else Icons.Rounded.FavoriteBorder,
            ) { Store.toggleFavorite(d) }
            if (storedFav) {
                GlassButton(tr("Save changes", "Сохранить изменения"), Icons.Rounded.Save) {
                    Store.updateFavorite(d); Launcher.toast(ctx, tr("Saved", "Сохранено"))
                }
            }
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                GlassButton(tr("Root launch", "Root запуск"), Icons.Rounded.Security, Modifier.weight(1f), primary = false) { launchRoot() }
                GlassButton(tr("Intent URI", "Intent URI"), Icons.Rounded.Link, Modifier.weight(1f), primary = false) {
                    Launcher.copy(ctx, Launcher.intentUri(d), "intent")
                }
            }

            // ───────────── command preview
            SectionTitle(tr("ADB command", "ADB команда"))
            GlassCard(padding = PaddingValues(16.dp), onClick = { Launcher.copy(ctx, Launcher.adbCommand(d), "adb") }) {
                Row(verticalAlignment = Alignment.Top) {
                    Icon(Icons.Rounded.Code, null, tint = g.accent, modifier = Modifier.size(20.dp))
                    Spacer(Modifier.width(10.dp))
                    Text(Launcher.adbCommand(d), color = g.text, fontFamily = FontFamily.Monospace, fontSize = 12.sp,
                        modifier = Modifier.weight(1f))
                    Spacer(Modifier.width(6.dp))
                    Icon(Icons.Rounded.ContentCopy, null, tint = g.textDim, modifier = Modifier.size(18.dp))
                }
            }
        }
    }

    val rd = rootDialog
    if (rd != null) {
        AlertDialog(
            onDismissRequest = { rootDialog = null },
            title = { Text(tr("Launch failed", "Не удалось запустить")) },
            text = { Text(rd + "\n\n" + tr("Try launching with root (su)?", "Попробовать запустить через root (su)?")) },
            confirmButton = { TextButton({ rootDialog = null; launchRoot() }) { Text(tr("Use root", "Через root")) } },
            dismissButton = { TextButton({ rootDialog = null }) { Text(tr("Cancel", "Отмена")) } },
            containerColor = g.fillStrong.copy(alpha = 0.97f),
            shape = RoundedCornerShape(30.dp),
        )
    }
}

fun <T> List<T>.replace(index: Int, value: T): List<T> = mapIndexed { i, v -> if (i == index) value else v }

@Composable
fun glassSwitchColors() = SwitchDefaults.colors(
    checkedTrackColor = glass.accent,
    checkedThumbColor = Color.White,
    uncheckedTrackColor = glass.fill,
    uncheckedBorderColor = glass.stroke,
    uncheckedThumbColor = glass.textDim,
)
