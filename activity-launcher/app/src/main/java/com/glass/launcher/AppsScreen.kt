package com.glass.launcher

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
import androidx.compose.material.icons.automirrored.rounded.Sort
import androidx.compose.material.icons.rounded.Add
import androidx.compose.material.icons.rounded.Apps
import androidx.compose.material.icons.rounded.Bolt
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material.icons.rounded.ContentCopy
import androidx.compose.material.icons.rounded.Info
import androidx.compose.material.icons.rounded.Layers
import androidx.compose.material.icons.rounded.Lock
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.Refresh
import androidx.compose.material.icons.rounded.SearchOff
import androidx.compose.material.icons.rounded.Settings
import androidx.compose.material.icons.rounded.Shop
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.chrisbanes.haze.HazeState
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class AppsState {
    var query by mutableStateOf("")
    var mode by mutableStateOf(0)   // 0 apps, 1 activities, 2 intent action
    var filter by mutableStateOf(if (Store.showSystem) 0 else 1) // 0 all, 1 user, 2 system
}

fun ActEntry.toShortcut(ctx: android.content.Context) = ShortcutData(
    name = label, pkg = pkg, cls = cls, icon = Pkg.iconResName(ctx, pkg, cls),
)

@Composable
fun AppsTopBar(haze: HazeState, nav: Nav, st: AppsState) {
    val ctx = LocalContext.current
    val scope = rememberCoroutineScope()
    var sortOpen by remember { mutableStateOf(false) }
    val count = Pkg.apps?.size
    GlassTopBar(
        haze,
        title = tr("Packages", "Пакеты"),
        subtitle = if (count != null) tr("$count apps", "$count приложений") else null,
    ) {
        GlassIconButton(Icons.Rounded.Add, tr("New intent", "Новый интент"), plain = true) {
            nav.push(Screen.Editor(ShortcutData(name = tr("Custom intent", "Свой интент"))))
        }
        Box {
            GlassIconButton(Icons.AutoMirrored.Rounded.Sort, tr("Sort", "Сортировка"), plain = true) { sortOpen = true }
            DropdownMenu(expanded = sortOpen, onDismissRequest = { sortOpen = false }) {
                val opts = listOf(
                    "name" to tr("Name", "Имя"),
                    "package" to tr("Package", "Пакет"),
                    "installed" to tr("Install date", "Дата установки"),
                    "updated" to tr("Update date", "Дата обновления"),
                    "activities" to tr("Activity count", "Число активити"),
                )
                opts.forEach { (k, label) ->
                    DropdownMenuItem(
                        text = { Text(label) },
                        leadingIcon = { if (Store.sort == k) Icon(Icons.Rounded.Check, null) else Spacer(Modifier.size(24.dp)) },
                        onClick = { Store.sort = k; sortOpen = false },
                    )
                }
                DropdownMenuItem(
                    text = { Text(tr("Reload list", "Обновить список")) },
                    leadingIcon = { Icon(Icons.Rounded.Refresh, null) },
                    onClick = { sortOpen = false; scope.launch { Pkg.load(ctx, force = true) } },
                )
            }
        }
        GlassIconButton(Icons.Rounded.Settings, tr("Settings", "Настройки"), plain = true) { nav.push(Screen.Settings) }
    }
}

@Composable
fun AppsContent(nav: Nav, st: AppsState, pad: PaddingValues) {
    val ctx = LocalContext.current
    val g = glass
    val apps = Pkg.apps

    LaunchedEffect(st.mode) { if (st.mode == 1) Pkg.loadAllActivities(ctx) }

    val filteredApps = remember(apps, st.query, st.filter, Store.sort) {
        val q = st.query.trim()
        (apps ?: emptyList())
            .asSequence()
            .filter { st.filter == 0 || (st.filter == 1 && !it.isSystem) || (st.filter == 2 && it.isSystem) }
            .filter { q.isEmpty() || it.label.contains(q, true) || it.pkg.contains(q, true) }
            .let { s ->
                when (Store.sort) {
                    "package" -> s.sortedBy { it.pkg }
                    "installed" -> s.sortedByDescending { it.firstInstall }
                    "updated" -> s.sortedByDescending { it.lastUpdate }
                    "activities" -> s.sortedByDescending { it.activityCount }
                    else -> s.sortedBy { it.label.lowercase() }
                }
            }.toList()
    }
    val all = Pkg.allActivities
    val filteredActs = remember(all, st.query, Store.exportedOnly) {
        val q = st.query.trim()
        (all ?: emptyList()).filter {
            (!Store.exportedOnly || it.exported) &&
                (q.isEmpty() || it.label.contains(q, true) || it.cls.contains(q, true) || it.appLabel.contains(q, true))
        }.take(1500)
    }
    val actionResults by produceState<List<ActEntry>?>(null, st.query, st.mode) {
        if (st.mode != 2 || st.query.isBlank()) { value = null; return@produceState }
        delay(300)
        value = withContext(Dispatchers.IO) { Pkg.queryAction(ctx, st.query.trim()) }
    }

    LazyColumn(
        Modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = pad.calculateTopPadding(), bottom = pad.calculateBottomPadding()),
        verticalArrangement = Arrangement.spacedBy(if (Store.compactList) 6.dp else 10.dp),
    ) {
        item(key = "search") {
            GlassSearchField(
                st.query, { st.query = it },
                when (st.mode) {
                    0 -> tr("package / name", "пакет / название")
                    1 -> tr("activity / class / app", "активити / класс / приложение")
                    else -> tr("intent action, e.g. android.intent.action.SEND", "action, напр. android.intent.action.SEND")
                },
            )
        }
        item(key = "chips") {
            Row(
                Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(vertical = 2.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                GlassChip(tr("Apps", "Приложения"), st.mode == 0, Icons.Rounded.Apps) { st.mode = 0 }
                GlassChip(tr("Activities", "Активити"), st.mode == 1, Icons.Rounded.Layers) { st.mode = 1 }
                GlassChip(tr("Intent action", "Action"), st.mode == 2, Icons.Rounded.Bolt) { st.mode = 2 }
                Spacer(Modifier.width(8.dp))
                if (st.mode == 0) {
                    GlassChip(tr("All", "Все"), st.filter == 0) { st.filter = 0 }
                    GlassChip(tr("User", "Пользов."), st.filter == 1) { st.filter = 1 }
                    GlassChip(tr("System", "Системные"), st.filter == 2) { st.filter = 2 }
                } else if (st.mode == 1) {
                    GlassChip(tr("Exported only", "Только экспорт."), Store.exportedOnly, Icons.Rounded.Lock) {
                        Store.exportedOnly = !Store.exportedOnly
                    }
                }
            }
        }

        when (st.mode) {
            0 -> {
                if (apps == null || Pkg.loading) {
                    item(key = "loading") { LoadingCard(tr("Scanning packages…", "Сканирование пакетов…"), Pkg.progress) }
                }
                if (apps != null) {
                    item(key = "count") { CountLine(tr("${filteredApps.size} packages", "Пакетов: ${filteredApps.size}")) }
                    items(filteredApps, key = { "a_" + it.pkg }) { app -> AppRow(nav, app) }
                    if (filteredApps.isEmpty()) item(key = "empty") {
                        EmptyState(Icons.Rounded.SearchOff, tr("Nothing found", "Ничего не найдено"),
                            tr("Try another name or package", "Попробуйте другое имя или пакет"))
                    }
                }
            }
            1 -> {
                if (all == null) {
                    item(key = "loading") { LoadingCard(tr("Indexing all activities…", "Индексация всех активити…"), null) }
                } else {
                    item(key = "count") {
                        CountLine(tr("${filteredActs.size} of ${all.size} activities", "${filteredActs.size} из ${all.size} активити"))
                    }
                    items(filteredActs, key = { "x_" + it.pkg + "/" + it.cls }) { a -> ActivityRow(nav, a, showApp = true) }
                }
            }
            else -> {
                val res = actionResults
                if (st.query.isBlank()) {
                    item(key = "hint") {
                        GlassCard {
                            Text(tr("Find every activity that handles an intent action", "Найти все активити, принимающие action"),
                                color = g.text, fontWeight = FontWeight.SemiBold)
                            Spacer(Modifier.height(10.dp))
                            Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                listOf("android.intent.action.SEND", "android.intent.action.VIEW", "android.settings.SETTINGS",
                                    "android.intent.action.MAIN", "android.media.action.IMAGE_CAPTURE").forEach { a ->
                                    GlassChip(a.substringAfterLast('.'), false) { st.query = a }
                                }
                            }
                        }
                    }
                } else if (res == null) {
                    item(key = "loading") { LoadingCard(tr("Resolving…", "Поиск…"), null) }
                } else {
                    item(key = "count") { CountLine(tr("${res.size} handlers", "Обработчиков: ${res.size}")) }
                    items(res, key = { "r_" + it.pkg + "/" + it.cls }) { a ->
                        ActivityRow(nav, a, showApp = true, action = st.query.trim())
                    }
                }
            }
        }
    }
}

@Composable
fun CountLine(text: String) {
    Text(text, color = glass.textDim, fontSize = 13.sp, modifier = Modifier.padding(start = 8.dp))
}

@Composable
fun LoadingCard(text: String, progress: Float?) {
    val g = glass
    GlassCard {
        Text(text, color = g.text, fontWeight = FontWeight.Medium)
        Spacer(Modifier.height(12.dp))
        if (progress != null) {
            LinearProgressIndicator(progress = { progress }, modifier = Modifier.fillMaxWidth(), color = g.accent, trackColor = g.fill)
        } else {
            LinearProgressIndicator(modifier = Modifier.fillMaxWidth(), color = g.accent, trackColor = g.fill)
        }
    }
}

@Composable
fun AppRow(nav: Nav, app: AppEntry) {
    val g = glass
    val ctx = LocalContext.current
    var menu by remember { mutableStateOf(false) }
    val compact = Store.compactList
    Box {
        GlassCard(
            padding = PaddingValues(horizontal = 14.dp, vertical = if (compact) 8.dp else 12.dp),
            onLongClick = { menu = true },
            onClick = { nav.push(Screen.Package(app.pkg)) },
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                AppIcon(rememberIcon(app.pkg), if (compact) 40.dp else 52.dp)
                Spacer(Modifier.width(14.dp))
                Column(Modifier.weight(1f)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(app.label, color = g.text, fontSize = 17.sp, fontWeight = FontWeight.SemiBold,
                            maxLines = 1, overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f, fill = false))
                        if (!app.enabled) { Spacer(Modifier.width(6.dp)); Badge(tr("OFF", "ВЫКЛ"), Color(0xFFEF4444)) }
                        if (app.isSystem) { Spacer(Modifier.width(6.dp)); Badge(tr("SYS", "СИС"), g.accent2) }
                    }
                    if (!compact) {
                        Text("${app.versionName} (${app.versionCode})", color = g.textDim, fontSize = 13.sp, maxLines = 1,
                            overflow = TextOverflow.Ellipsis)
                    }
                    Text(app.pkg, color = g.textDim, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                }
                Spacer(Modifier.width(8.dp))
                Text("${app.activityCount}", color = g.accent, fontSize = 16.sp, fontWeight = FontWeight.Bold)
            }
        }
        DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
            DropdownMenuItem(text = { Text(tr("Launch app", "Запустить")) }, leadingIcon = { Icon(Icons.Rounded.PlayArrow, null) },
                onClick = { menu = false; Launcher.launchApp(ctx, app.pkg) })
            DropdownMenuItem(text = { Text(tr("App info", "О приложении")) }, leadingIcon = { Icon(Icons.Rounded.Info, null) },
                onClick = { menu = false; Launcher.openAppInfo(ctx, app.pkg) })
            DropdownMenuItem(text = { Text(tr("Copy package", "Копировать пакет")) }, leadingIcon = { Icon(Icons.Rounded.ContentCopy, null) },
                onClick = { menu = false; Launcher.copy(ctx, app.pkg) })
            DropdownMenuItem(text = { Text("Play Store") }, leadingIcon = { Icon(Icons.Rounded.Shop, null) },
                onClick = { menu = false; Launcher.openStore(ctx, app.pkg) })
        }
    }
}

@Composable
fun ActivityRow(nav: Nav, a: ActEntry, showApp: Boolean = false, action: String? = null) {
    val g = glass
    val ctx = LocalContext.current
    fun data() = a.toShortcut(ctx).let { if (action != null) it.copy(action = action) else it }
    GlassCard(
        padding = PaddingValues(start = 14.dp, end = 8.dp, top = 10.dp, bottom = 10.dp),
        onLongClick = { Launcher.copy(ctx, a.cls) },
        onClick = { nav.push(Screen.Editor(data())) },
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            AppIcon(rememberIcon(a.pkg, a.cls), 44.dp)
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(a.label, color = g.text, fontSize = 16.sp, fontWeight = FontWeight.SemiBold, maxLines = 1,
                        overflow = TextOverflow.Ellipsis, modifier = Modifier.weight(1f, fill = false))
                    if (a.isLauncher) { Spacer(Modifier.width(6.dp)); Badge("MAIN", g.accent) }
                    if (!a.exported) { Spacer(Modifier.width(6.dp)); Badge(tr("PRIVATE", "ЗАКРЫТА"), Color(0xFFF59E0B)) }
                    if (!a.enabled) { Spacer(Modifier.width(6.dp)); Badge(tr("OFF", "ВЫКЛ"), Color(0xFFEF4444)) }
                }
                if (showApp) Text(a.appLabel, color = g.accent, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text(a.shortClass, color = g.textDim, fontSize = 12.sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
            }
            GlassIconButton(
                if (a.exported) Icons.Rounded.PlayArrow else Icons.Rounded.Lock,
                tr("Launch", "Запустить"), tint = if (a.exported) g.accent else g.textDim, plain = true,
            ) {
                Launcher.launchAndReport(ctx, data())
            }
        }
    }
}
