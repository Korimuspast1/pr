package com.glass.launcher

import android.text.format.DateFormat
import android.text.format.Formatter
import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.CheckCircle
import androidx.compose.material.icons.rounded.ContentCopy
import androidx.compose.material.icons.rounded.Delete
import androidx.compose.material.icons.rounded.Info
import androidx.compose.material.icons.rounded.Lock
import androidx.compose.material.icons.rounded.MoreVert
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.RadioButtonUnchecked
import androidx.compose.material.icons.rounded.Share
import androidx.compose.material.icons.rounded.Shop
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.util.Date

@Composable
fun PackageScreen(nav: Nav, pkg: String) {
    val ctx = LocalContext.current
    val g = glass
    val scope = rememberCoroutineScope()
    val details by produceState<PkgDetails?>(null, pkg) {
        value = withContext(Dispatchers.IO) { Pkg.details(ctx, pkg) }
    }
    var tab by remember { mutableIntStateOf(0) }
    var query by remember { mutableStateOf("") }
    var exportedOnly by remember { mutableStateOf(Store.exportedOnly) }
    var menu by remember { mutableStateOf(false) }
    val d = details
    val label = d?.app?.label ?: Pkg.apps?.firstOrNull { it.pkg == pkg }?.label ?: pkg

    GlassScaffold(
        topBar = { haze ->
            GlassTopBar(haze, title = label, subtitle = pkg, onBack = { nav.pop() }) {
                GlassIconButton(Icons.Rounded.Info, tr("App info", "О приложении"), plain = true) { Launcher.openAppInfo(ctx, pkg) }
                Box {
                    GlassIconButton(Icons.Rounded.MoreVert, tr("More", "Ещё"), plain = true) { menu = true }
                    DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
                        DropdownMenuItem(text = { Text(tr("Copy package name", "Копировать имя пакета")) },
                            leadingIcon = { Icon(Icons.Rounded.ContentCopy, null) },
                            onClick = { menu = false; Launcher.copy(ctx, pkg) })
                        DropdownMenuItem(text = { Text("Play Store") }, leadingIcon = { Icon(Icons.Rounded.Shop, null) },
                            onClick = { menu = false; Launcher.openStore(ctx, pkg) })
                        DropdownMenuItem(text = { Text(tr("Share APK", "Поделиться APK")) }, leadingIcon = { Icon(Icons.Rounded.Share, null) },
                            onClick = { menu = false; scope.launch { Launcher.shareApk(ctx, pkg, label) } })
                        DropdownMenuItem(text = { Text(tr("Uninstall", "Удалить")) }, leadingIcon = { Icon(Icons.Rounded.Delete, null) },
                            onClick = { menu = false; Launcher.uninstall(ctx, pkg) })
                    }
                }
            }
        },
    ) { pad ->
        LazyColumn(
            Modifier.fillMaxSize(),
            contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = pad.calculateTopPadding(), bottom = pad.calculateBottomPadding()),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            item(key = "header") {
                GlassCard(padding = PaddingValues(20.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        AppIcon(rememberIcon(pkg), 72.dp)
                        Spacer(Modifier.width(16.dp))
                        Column(Modifier.weight(1f)) {
                            Text(label, color = g.text, fontSize = 22.sp, fontWeight = FontWeight.Bold, maxLines = 2,
                                overflow = TextOverflow.Ellipsis)
                            if (d != null) Text("${d.app.versionName} (${d.app.versionCode})", color = g.textDim, fontSize = 14.sp)
                            Text(pkg, color = g.textDim, fontSize = 12.sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
                        }
                    }
                    Spacer(Modifier.height(18.dp))
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                        QuickAction(Icons.Rounded.PlayArrow, tr("Launch", "Запуск")) { Launcher.launchApp(ctx, pkg) }
                        QuickAction(Icons.Rounded.Info, tr("Info", "Инфо")) { Launcher.openAppInfo(ctx, pkg) }
                        QuickAction(Icons.Rounded.Share, "APK") { scope.launch { Launcher.shareApk(ctx, pkg, label) } }
                        QuickAction(Icons.Rounded.Delete, tr("Remove", "Удалить")) { Launcher.uninstall(ctx, pkg) }
                    }
                }
            }
            if (d == null) {
                item(key = "loading") { LoadingCard(tr("Reading package…", "Чтение пакета…"), null) }
                return@LazyColumn
            }
            item(key = "info") { InfoCard(d) }
            item(key = "tabs") {
                Row(
                    Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(top = 6.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    val tabs = listOf(
                        tr("Activities", "Активити") to d.activities.size,
                        tr("Services", "Сервисы") to d.services.size,
                        tr("Receivers", "Ресиверы") to d.receivers.size,
                        tr("Providers", "Провайдеры") to d.providers.size,
                        tr("Permissions", "Разрешения") to d.permissions.size,
                    )
                    tabs.forEachIndexed { i, (n, c) -> GlassChip("$n $c", tab == i) { tab = i } }
                }
            }
            when (tab) {
                0 -> {
                    item(key = "q") {
                        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            GlassSearchField(query, { query = it }, tr("Filter activities", "Фильтр активити"))
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                GlassChip(tr("Exported only", "Только экспортируемые"), exportedOnly, Icons.Rounded.Lock) {
                                    exportedOnly = !exportedOnly
                                }
                            }
                        }
                    }
                    val list = d.activities.filter {
                        (!exportedOnly || it.exported) &&
                            (query.isBlank() || it.label.contains(query, true) || it.cls.contains(query, true))
                    }
                    items(list, key = { "act_" + it.cls }) { a -> ActivityRow(nav, a) }
                }
                1 -> items(d.services, key = { "s_" + it.name }) { CompRow(it, pkg) }
                2 -> items(d.receivers, key = { "r_" + it.name }) { CompRow(it, pkg) }
                3 -> items(d.providers, key = { "p_" + it.name }) { CompRow(it, pkg) }
                else -> items(d.permissions, key = { "perm_" + it.name }) { p ->
                    GlassCard(padding = PaddingValues(14.dp), onClick = { Launcher.copy(ctx, p.name) }) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(
                                if (p.granted) Icons.Rounded.CheckCircle else Icons.Rounded.RadioButtonUnchecked, null,
                                tint = if (p.granted) Color(0xFF22C55E) else g.textDim, modifier = Modifier.size(20.dp),
                            )
                            Spacer(Modifier.width(12.dp))
                            Column(Modifier.weight(1f)) {
                                Text(p.name.substringAfterLast('.'), color = g.text, fontWeight = FontWeight.Medium, fontSize = 14.sp)
                                Text(p.name, color = g.textDim, fontSize = 11.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun QuickAction(icon: ImageVector, label: String, onClick: () -> Unit) {
    val g = glass
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        GlassIconButton(icon, label, tint = g.accent, size = 52.dp, onClick = onClick)
        Spacer(Modifier.height(6.dp))
        Text(label, color = g.textDim, fontSize = 12.sp, textAlign = TextAlign.Center)
    }
}

@Composable
private fun InfoCard(d: PkgDetails) {
    val g = glass
    val ctx = LocalContext.current
    val df = remember { DateFormat.getMediumDateFormat(ctx) }
    val tf = remember { DateFormat.getTimeFormat(ctx) }
    fun date(t: Long) = df.format(Date(t)) + " " + tf.format(Date(t))
    val rows = buildList {
        add(tr("Target SDK", "Target SDK") to d.app.targetSdk.toString())
        if (d.minSdk > 0) add(tr("Min SDK", "Min SDK") to d.minSdk.toString())
        add("UID" to d.uid.toString())
        add(tr("Installed", "Установлено") to date(d.app.firstInstall))
        add(tr("Updated", "Обновлено") to date(d.app.lastUpdate))
        add(tr("Size", "Размер") to Formatter.formatShortFileSize(ctx, d.apkSize) + if (d.splitCount > 0) " (+${d.splitCount} split)" else "")
        add(tr("Installer", "Установщик") to (d.installer ?: "—"))
        add(tr("Type", "Тип") to if (d.app.isSystem) tr("System", "Системное") else tr("User", "Пользовательское"))
        add(tr("State", "Состояние") to if (d.app.enabled) tr("Enabled", "Включено") else tr("Disabled", "Отключено"))
        add("APK" to d.apkPath)
        add(tr("Data dir", "Данные") to d.dataDir)
        if (d.sha256 != null) add("SHA-256" to d.sha256)
    }
    GlassCard(padding = PaddingValues(horizontal = 18.dp, vertical = 12.dp)) {
        rows.forEachIndexed { i, (k, v) ->
            Row(
                Modifier.fillMaxWidth().padding(vertical = 7.dp),
                verticalAlignment = Alignment.Top,
            ) {
                Text(k, color = g.textDim, fontSize = 13.sp, modifier = Modifier.width(104.dp))
                Text(
                    v, color = g.text, fontSize = 13.sp, modifier = Modifier.weight(1f),
                    fontFamily = if (k == "SHA-256" || k == "APK") FontFamily.Monospace else FontFamily.Default,
                )
                Box(Modifier.size(28.dp).padding(start = 6.dp)) {
                    GlassIconButton(Icons.Rounded.ContentCopy, null, tint = g.textDim, plain = true, size = 22.dp) {
                        Launcher.copy(ctx, v, k)
                    }
                }
            }
            if (i != rows.lastIndex) Box(Modifier.fillMaxWidth().height(0.5.dp).background(g.textDim.copy(alpha = .18f)))
        }
    }
}

@Composable
private fun CompRow(c: CompEntry, pkg: String) {
    val g = glass
    val ctx = LocalContext.current
    GlassCard(padding = PaddingValues(14.dp), onClick = { Launcher.copy(ctx, c.name) }) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(c.name.substringAfterLast('.'), color = g.text, fontWeight = FontWeight.SemiBold, fontSize = 15.sp,
                    maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text(c.shortName(pkg), color = g.textDim, fontSize = 12.sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
                if (c.extra != null) Text(c.extra, color = g.accent, fontSize = 11.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                if (c.permission != null) Text(c.permission, color = g.textDim, fontSize = 11.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
            }
            Spacer(Modifier.width(8.dp))
            if (c.exported) Badge(tr("EXPORTED", "ЭКСПОРТ"), g.accent) else Badge(tr("PRIVATE", "ЗАКРЫТ"), Color(0xFFF59E0B))
            if (!c.enabled) { Spacer(Modifier.width(4.dp)); Badge(tr("OFF", "ВЫКЛ"), Color(0xFFEF4444)) }
        }
    }
}
