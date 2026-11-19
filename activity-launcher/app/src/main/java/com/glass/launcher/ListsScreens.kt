package com.glass.launcher

import android.text.format.DateUtils
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AddHome
import androidx.compose.material.icons.rounded.ArrowDownward
import androidx.compose.material.icons.rounded.ArrowUpward
import androidx.compose.material.icons.rounded.ContentCopy
import androidx.compose.material.icons.rounded.Delete
import androidx.compose.material.icons.rounded.DeleteSweep
import androidx.compose.material.icons.rounded.Edit
import androidx.compose.material.icons.rounded.FavoriteBorder
import androidx.compose.material.icons.rounded.History
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.Settings
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.chrisbanes.haze.HazeState

// ═══════════════════════════════════════════════════════════════ favorites

@Composable
fun FavoritesTopBar(haze: HazeState, nav: Nav) {
    GlassTopBar(
        haze, Store.titleFav.ifBlank { tr("Favorites", "Избранное") }, pro = true,
        subtitle = if (Store.favorites.isEmpty()) null else tr("${Store.favorites.size} shortcuts", "Ярлыков: ${Store.favorites.size}"),
    ) {
        GlassIconButton(Icons.Rounded.Settings, tr("Settings", "Настройки"), plain = true) { nav.push(Screen.Settings) }
    }
}

@Composable
fun ShortcutIcon(d: ShortcutData, size: androidx.compose.ui.unit.Dp = 48.dp) {
    val res = if (d.icon.isNotBlank()) rememberResIcon(d.icon) else null
    val act = if (d.pkg.isNotBlank()) rememberIcon(d.pkg, d.cls.ifBlank { null }) else null
    AppIcon(res ?: act, size)
}

@Composable
fun FavoritesContent(nav: Nav, pad: PaddingValues) {
    val ctx = LocalContext.current
    val g = glass
    LazyColumn(
        Modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = pad.calculateTopPadding(), bottom = pad.calculateBottomPadding()),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        if (Store.favorites.isEmpty()) {
            item(key = "empty") {
                EmptyState(Icons.Rounded.FavoriteBorder, tr("No favorites yet", "Пока пусто"),
                    tr("Open any activity and tap the heart to keep it here. Top 4 also appear on the app icon long-press menu.",
                        "Откройте любую активити и нажмите ♥, чтобы сохранить её здесь. Первые 4 появятся в меню долгого нажатия на иконку."))
            }
        }
        itemsIndexed(Store.favorites, key = { _, d -> d.id }) { i, d ->
            var menu by remember { mutableStateOf(false) }
            Box {
                GlassCard(
                    padding = PaddingValues(start = 14.dp, end = 8.dp, top = 12.dp, bottom = 12.dp),
                    onLongClick = { menu = true },
                    onClick = { nav.push(Screen.Editor(d)) },
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        ShortcutIcon(d, Store.iconSize.dp)
                        Spacer(Modifier.width(14.dp))
                        Column(Modifier.weight(1f)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text(d.displayName, color = g.text, fontSize = 17.sp, fontWeight = FontWeight.SemiBold,
                                    maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f, fill = false))
                                if (d.root) { Spacer(Modifier.width(6.dp)); Badge("ROOT", androidx.compose.ui.graphics.Color(0xFFEF4444)) }
                            }
                            Text(d.shortClass.ifBlank { d.action.ifBlank { d.pkg } }, color = g.textDim, fontSize = 13.sp,
                                maxLines = 1, overflow = TextOverflow.Ellipsis)
                        }
                        GlassIconButton(Icons.Rounded.PlayArrow, tr("Launch", "Запуск"), tint = g.accent, plain = true) {
                            Launcher.launchAndReport(ctx, d)
                        }
                    }
                }
                DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
                    DropdownMenuItem(text = { Text(tr("Launch", "Запустить")) }, leadingIcon = { Icon(Icons.Rounded.PlayArrow, null) },
                        onClick = { menu = false; Launcher.launchAndReport(ctx, d) })
                    DropdownMenuItem(text = { Text(tr("Edit", "Изменить")) }, leadingIcon = { Icon(Icons.Rounded.Edit, null) },
                        onClick = { menu = false; nav.push(Screen.Editor(d)) })
                    DropdownMenuItem(text = { Text(tr("Home screen shortcut", "Ярлык на рабочий стол")) },
                        leadingIcon = { Icon(Icons.Rounded.AddHome, null) },
                        onClick = { menu = false; Launcher.pinShortcut(ctx, d) })
                    if (i > 0) DropdownMenuItem(text = { Text(tr("Move up", "Выше")) }, leadingIcon = { Icon(Icons.Rounded.ArrowUpward, null) },
                        onClick = { menu = false; Store.moveFavorite(i, i - 1) })
                    if (i < Store.favorites.lastIndex) DropdownMenuItem(text = { Text(tr("Move down", "Ниже")) },
                        leadingIcon = { Icon(Icons.Rounded.ArrowDownward, null) },
                        onClick = { menu = false; Store.moveFavorite(i, i + 1) })
                    DropdownMenuItem(text = { Text(tr("Duplicate", "Дублировать")) }, leadingIcon = { Icon(Icons.Rounded.ContentCopy, null) },
                        onClick = {
                            menu = false
                            Store.favorites.add(i + 1, d.copy(id = java.util.UUID.randomUUID().toString(), name = d.displayName + " (2)"))
                            Store.saveFavorites()
                        })
                    DropdownMenuItem(text = { Text(tr("Remove", "Удалить")) }, leadingIcon = { Icon(Icons.Rounded.Delete, null) },
                        onClick = { menu = false; Store.removeFavorite(d) })
                }
            }
        }
    }
}

// ═════════════════════════════════════════════════════════════════ recents

@Composable
fun RecentsTopBar(haze: HazeState, nav: Nav) {
    var confirm by remember { mutableStateOf(false) }
    val g = glass
    GlassTopBar(haze, Store.titleRecents.ifBlank { tr("Recents", "Недавние") }, pro = true,
        subtitle = if (!Store.historyEnabled) tr("History is off", "История выключена") else null) {
        if (Store.recents.isNotEmpty()) {
            GlassIconButton(Icons.Rounded.DeleteSweep, tr("Clear", "Очистить"), plain = true) { confirm = true }
        }
        GlassIconButton(Icons.Rounded.Settings, tr("Settings", "Настройки"), plain = true) { nav.push(Screen.Settings) }
    }
    if (confirm) {
        AlertDialog(
            onDismissRequest = { confirm = false },
            title = { Text(tr("Clear history?", "Очистить историю?")) },
            confirmButton = { TextButton({ Store.clearRecents(); confirm = false }) { Text(tr("Clear", "Очистить")) } },
            dismissButton = { TextButton({ confirm = false }) { Text(tr("Cancel", "Отмена")) } },
            containerColor = g.fillStrong.copy(alpha = 0.97f),
            shape = RoundedCornerShape(30.dp),
        )
    }
}

@Composable
fun RecentsContent(nav: Nav, pad: PaddingValues) {
    val ctx = LocalContext.current
    val g = glass
    LazyColumn(
        Modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = pad.calculateTopPadding(), bottom = pad.calculateBottomPadding()),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        if (Store.recents.isEmpty()) {
            item(key = "empty") {
                EmptyState(Icons.Rounded.History, tr("Nothing launched yet", "Ещё ничего не запускалось"),
                    tr("Every activity you launch shows up here.", "Здесь появятся все запущенные активити."))
            }
        }
        itemsIndexed(Store.recents, key = { _, e -> e.data.key() + e.time }) { _, e ->
            val d = e.data
            GlassCard(
                padding = PaddingValues(start = 14.dp, end = 8.dp, top = 12.dp, bottom = 12.dp),
                onLongClick = { Store.removeRecent(e) },
                onClick = { nav.push(Screen.Editor(d)) },
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    ShortcutIcon(d, (Store.iconSize * 0.92f).dp)
                    Spacer(Modifier.width(14.dp))
                    Column(Modifier.weight(1f)) {
                        Text(d.displayName, color = g.text, fontSize = 16.sp, fontWeight = FontWeight.SemiBold,
                            maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Text(d.shortClass.ifBlank { d.action.ifBlank { d.pkg } }, color = g.textDim, fontSize = 12.sp,
                            maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Row {
                            Text(DateUtils.getRelativeTimeSpanString(e.time).toString(), color = g.accent, fontSize = 12.sp)
                            if (e.count > 1) Text("  ·  ×${e.count}", color = g.textDim, fontSize = 12.sp)
                        }
                    }
                    GlassIconButton(Icons.Rounded.PlayArrow, tr("Launch", "Запуск"), tint = g.accent, plain = true) {
                        Launcher.launchAndReport(ctx, d)
                    }
                }
            }
        }
    }
}
