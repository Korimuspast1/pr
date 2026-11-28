package com.zalchik.nimbus.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.zalchik.nimbus.data.RoutePreset
import com.zalchik.nimbus.data.newId
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.NiIcons
import com.zalchik.nimbus.ui.components.ActionSheet
import com.zalchik.nimbus.ui.components.Caption
import com.zalchik.nimbus.ui.components.Chip
import com.zalchik.nimbus.ui.components.ConfirmDialog
import com.zalchik.nimbus.ui.components.NimbusCard
import com.zalchik.nimbus.ui.components.NimbusField
import com.zalchik.nimbus.ui.components.Segmented
import com.zalchik.nimbus.ui.components.SettingsRow
import com.zalchik.nimbus.ui.components.SheetAction
import com.zalchik.nimbus.ui.components.SwitchRow
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.state.InstalledApps
import com.zalchik.nimbus.ui.theme.NimbusTheme

/**
 * Routing: what goes through the tunnel and what stays direct. Two switches decide the broad
 * behaviour (smart routing for Russian addresses, the provider's own rules); profiles add
 * per-app and per-site exceptions, which is what the desktop's preset dialog does.
 */
@Composable
fun RoutingScreen(app: AppState, onBack: () -> Unit) {
    val colors = NimbusTheme.colors
    val settings = app.settings
    var editing by remember { mutableStateOf<RoutePreset?>(null) }
    var removing by remember { mutableStateOf<RoutePreset?>(null) }
    var renaming by remember { mutableStateOf<RoutePreset?>(null) }
    var showPerApp by remember { mutableStateOf(false) }

    ScreenScaffold(title = I18n.tr("Маршрутизация"), onBack = onBack) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            NimbusCard {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Caption(I18n.tr("Режим"))
                    Segmented(
                        options = listOf(
                            "bypass_ru" to I18n.tr("Умная"),
                            "global" to I18n.tr("Весь трафик"),
                        ),
                        selected = settings.routingMode,
                        onSelect = { app.setRoutingMode(it) },
                        tips = mapOf(
                            "bypass_ru" to I18n.tr("Российские адреса и сайты идут напрямую"),
                            "global" to I18n.tr("Через туннель идёт всё, кроме профилей ниже"),
                        ),
                    )
                }
            }

            NimbusCard {
                Column {
                    SwitchRow(
                        I18n.tr("Правила провайдера"),
                        settings.useProviderRouting,
                        { app.setProviderRouting(it) },
                        icon = NiIcons.Shield,
                        subtitle = I18n.tr("Оставить в конфиге то, что прислала подписка"),
                    )
                    HairLine()
                    SwitchRow(
                        I18n.tr("Блокировать рекламу"),
                        settings.blockAds,
                        { value ->
                            app.saveSettings { it.blockAds = value }
                            app.reloadService()
                        },
                        icon = NiIcons.CloudOff,
                        subtitle = I18n.tr("Рекламные и счётчиковые домены не отвечают"),
                    )
                    HairLine()
                    SwitchRow(
                        I18n.tr("Обходить локальные адреса"),
                        settings.bypassPrivate,
                        { value ->
                            app.saveSettings { it.bypassPrivate = value }
                            app.reloadService()
                        },
                        icon = NiIcons.Dns,
                        subtitle = I18n.tr("Домашняя сеть и принтеры видны напрямую"),
                    )
                }
            }

            // Profiles: per-app and per-site exceptions.
            NimbusCard {
                Column {
                    Row(Modifier.padding(horizontal = 16.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                        Caption(I18n.tr("Профили"), Modifier.weight(1f))
                        Chip(I18n.tr("Добавить"), NiIcons.Add) {
                            editing = RoutePreset(newId(), I18n.tr("Новый профиль"))
                        }
                    }
                    if (settings.routePresets.isEmpty()) {
                        Text(
                            I18n.tr("Профилей нет. Профиль — это список приложений и сайтов, которые идут мимо туннеля (или только через него)."),
                            style = MaterialTheme.typography.bodySmall,
                            color = colors.muted,
                            modifier = Modifier.padding(horizontal = 16.dp).padding(bottom = 14.dp),
                        )
                    }
                    for (preset in settings.routePresets) {
                        val active = preset.id == settings.routePreset
                        SettingsRow(
                            title = preset.name,
                            subtitle = presetSummary(preset),
                            value = if (active) I18n.tr("включён") else "",
                            icon = if (active) NiIcons.Check else NiIcons.Tune,
                            onClick = { app.applyPreset(if (active) "" else preset.id) },
                            trailing = {
                                Icon(
                                    NiIcons.More,
                                    I18n.tr("Ещё"),
                                    tint = colors.muted,
                                    modifier = Modifier
                                        .size(20.dp)
                                        .clip(RoundedCornerShape(6.dp))
                                        .clickable { renaming = preset },
                                )
                            },
                        )
                        HairLine()
                    }
                }
            }

            NimbusCard {
                Column {
                    SwitchRow(
                        I18n.tr("Фильтр приложений"),
                        settings.perAppEnabled,
                        { value ->
                            app.saveSettings { it.perAppEnabled = value }
                            app.reloadService()
                        },
                        icon = NiIcons.Apps,
                        subtitle = I18n.tr("Туннель работает только для выбранных приложений — или во всех, кроме них"),
                    )
                    if (settings.perAppEnabled) {
                        HairLine()
                        SettingsRow(
                            title = I18n.tr("Выбранные приложения"),
                            subtitle = I18n.plural(
                                settings.perAppPackages.size,
                                "{n} приложение",
                                "{n} приложения",
                                "{n} приложений",
                            ).replace("{n}", settings.perAppPackages.size.toString()),
                            icon = NiIcons.List,
                            value = if (settings.perAppMode == RoutePreset.MODE_VPN) I18n.tr("только они") else I18n.tr("все, кроме них"),
                            onClick = { showPerApp = true },
                        )
                    }
                }
            }
        }
    }

    editing?.let { preset ->
        PresetDialog(
            preset = preset,
            onSave = { saved ->
                if (saved.name.isEmpty()) saved.name = I18n.tr("Профиль")
                app.updatePreset(saved)
                app.applyPreset(saved.id)
                editing = null
            },
            onDismiss = { editing = null },
        )
    }

    renaming?.let { preset ->
        ActionSheet(
            title = preset.name,
            onDismiss = { renaming = null },
            actions = listOf(
                SheetAction(I18n.tr("Изменить"), { editing = preset }, NiIcons.Edit),
                SheetAction(
                    if (preset.id == settings.routePreset) I18n.tr("Выключить") else I18n.tr("Включить"),
                    { app.applyPreset(if (preset.id == settings.routePreset) "" else preset.id) },
                    NiIcons.Check,
                ),
                SheetAction(I18n.tr("Удалить"), { removing = preset }, NiIcons.Delete, destructive = true),
            ),
        )
    }

    removing?.let { preset ->
        ConfirmDialog(
            title = I18n.tr("Удалить профиль?"),
            message = preset.name,
            onDismiss = { removing = null },
            onConfirm = {
                app.removePreset(preset.id)
                removing = null
            },
        )
    }

    if (showPerApp) {
        PerAppScreen(
            app = app,
            onDismiss = {
                showPerApp = false
                app.reloadService()
            },
        )
    }
}

private fun presetSummary(preset: RoutePreset): String {
    val parts = mutableListOf<String>()
    parts += if (preset.mode == RoutePreset.MODE_VPN) I18n.tr("только через VPN") else I18n.tr("мимо VPN")
    if (preset.packages.isNotEmpty()) {
        parts += I18n.plural(preset.packages.size, "{n} приложение", "{n} приложения", "{n} приложений")
            .replace("{n}", preset.packages.size.toString())
    }
    if (preset.sites.isNotEmpty()) {
        parts += I18n.plural(preset.sites.size, "{n} сайт", "{n} сайта", "{n} сайтов")
            .replace("{n}", preset.sites.size.toString())
    }
    if (!preset.provider) parts += I18n.tr("без правил провайдера")
    return parts.joinToString(" · ")
}

/** A profile's editor: name, direction, and the two lists it filters on. */
@Composable
fun PresetDialog(preset: RoutePreset, onSave: (RoutePreset) -> Unit, onDismiss: () -> Unit) {
    val colors = NimbusTheme.colors
    var draft by remember { mutableStateOf(preset.copy(packages = preset.packages.toMutableList(), sites = preset.sites.toMutableList())) }
    var addingSite by remember { mutableStateOf(false) }
    var site by remember { mutableStateOf("") }
    var pickingApp by remember { mutableStateOf(false) }

    androidx.compose.material3.ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = colors.panel,
        contentColor = colors.text,
    ) {
        Column(
            Modifier
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp)
                .padding(bottom = 28.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            Text(I18n.tr("Профиль маршрутизации"), style = MaterialTheme.typography.titleMedium, color = colors.text)
            NimbusField(
                value = draft.name,
                onValueChange = { value -> draft = draft.copy(name = value) },
                hint = I18n.tr("Название"),
            )
            Column {
                Caption(I18n.tr("Куда"))
                Spacer(Modifier.height(8.dp))
                Segmented(
                    options = listOf(
                        RoutePreset.MODE_BYPASS to I18n.tr("Мимо VPN"),
                        RoutePreset.MODE_VPN to I18n.tr("Только VPN"),
                    ),
                    selected = draft.mode,
                    onSelect = { value -> draft = draft.copy(mode = value) },
                    tips = mapOf(
                        RoutePreset.MODE_BYPASS to I18n.tr("Списки идут напрямую, остальное — через туннель"),
                        RoutePreset.MODE_VPN to I18n.tr("Через туннель идут только эти списки"),
                    ),
                )
            }

            Column {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Caption(I18n.tr("Сайты и домены"), Modifier.weight(1f))
                    Chip(I18n.tr("Добавить"), NiIcons.Add) { addingSite = true }
                }
                if (addingSite) {
                    Spacer(Modifier.height(8.dp))
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        NimbusField(
                            value = site,
                            onValueChange = { site = it },
                            hint = "example.com",
                            modifier = Modifier.weight(1f),
                        )
                        Spacer(Modifier.width(8.dp))
                        Chip(I18n.tr("ОК"), NiIcons.Check) {
                            val value = site.trim().removePrefix("www.").lowercase()
                            if (value.isNotEmpty() && value !in draft.sites) {
                                draft = draft.copy(sites = (draft.sites + value).toMutableList())
                            }
                            site = ""
                            addingSite = false
                        }
                    }
                }
                if (draft.sites.isEmpty()) {
                    Text(
                        I18n.tr("Домен или его окончание: .ru, example.com"),
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.faint,
                    )
                } else {
                    FlowTags(draft.sites) { removed -> draft = draft.copy(sites = (draft.sites - removed).toMutableList()) }
                }
            }

            Column {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Caption(I18n.tr("Приложения"), Modifier.weight(1f))
                    Chip(I18n.tr("Выбрать"), NiIcons.Apps) { pickingApp = true }
                }
                if (draft.packages.isEmpty()) {
                    Text(
                        I18n.tr("Можно оставить пустым — тогда работают только сайты"),
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.faint,
                    )
                } else {
                    FlowTags(draft.packages) { removed ->
                        draft = draft.copy(packages = (draft.packages - removed).toMutableList())
                    }
                }
            }

            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Chip(I18n.tr("Отмена"), NiIcons.Close, onClick = onDismiss)
                Chip(I18n.tr("Сохранить"), NiIcons.Check) { onSave(draft) }
            }
        }
    }

    if (pickingApp) {
        AppPicker(
            selected = draft.packages,
            onDone = { packages ->
                draft = draft.copy(packages = packages.toMutableList())
                pickingApp = false
            },
            onDismiss = { pickingApp = false },
        )
    }
}

/** Chips that can be tapped away — a profile's site and package lists. */
@Composable
private fun FlowTags(values: List<String>, onRemove: (String) -> Unit) {
    val colors = NimbusTheme.colors
    Column(verticalArrangement = Arrangement.spacedBy(6.dp), modifier = Modifier.padding(top = 8.dp)) {
        values.chunked(2).forEach { line ->
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                for (value in line) {
                    Row(
                        Modifier
                            .clip(RoundedCornerShape(7.dp))
                            .background(colors.surface2)
                            .border(1.dp, colors.border, RoundedCornerShape(7.dp))
                            .clickable { onRemove(value) }
                            .padding(horizontal = 9.dp, vertical = 6.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Text(
                            value,
                            style = MaterialTheme.typography.bodySmall,
                            color = colors.text2,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                            modifier = Modifier.width(140.dp),
                        )
                        Spacer(Modifier.width(5.dp))
                        Icon(NiIcons.Close, null, tint = colors.faint, modifier = Modifier.size(12.dp))
                    }
                }
            }
        }
    }
}

/** The installed-app picker behind a profile and behind the global per-app filter. */
@Composable
fun AppPicker(
    selected: List<String>,
    onDone: (List<String>) -> Unit,
    onDismiss: () -> Unit,
    title: String = I18n.tr("Приложения"),
) {
    val colors = NimbusTheme.colors
    val context = LocalContext.current
    var apps by remember { mutableStateOf<List<com.zalchik.nimbus.ui.state.InstalledApp>>(emptyList()) }
    var query by remember { mutableStateOf("") }
    var chosen by remember { mutableStateOf(selected.toSet()) }
    var showSystem by remember { mutableStateOf(false) }

    LaunchedEffect(showSystem) {
        apps = InstalledApps.load(context, showSystem)
    }

    androidx.compose.material3.ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = colors.panel,
        contentColor = colors.text,
    ) {
        Column(Modifier.fillMaxWidth().padding(horizontal = 16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(title, style = MaterialTheme.typography.titleMedium, color = colors.text, modifier = Modifier.weight(1f))
                Text(
                    I18n.tr("Готово") + " (" + chosen.size + ")",
                    style = MaterialTheme.typography.labelLarge,
                    color = colors.text,
                    modifier = Modifier
                        .clip(RoundedCornerShape(9.dp))
                        .background(colors.surface2)
                        .clickable { onDone(chosen.toList()) }
                        .padding(horizontal = 14.dp, vertical = 9.dp),
                )
            }
            Spacer(Modifier.height(12.dp))
            NimbusField(
                value = query,
                onValueChange = { query = it },
                hint = I18n.tr("Поиск приложения"),
                leading = { Icon(NiIcons.Search, null, tint = colors.faint, modifier = Modifier.size(18.dp)) },
            )
            Spacer(Modifier.height(8.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    I18n.tr("Системные"),
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.muted,
                    modifier = Modifier.weight(1f),
                )
                androidx.compose.material3.Switch(
                    checked = showSystem,
                    onCheckedChange = { showSystem = it },
                )
            }
            val needle = query.trim().lowercase()
            val shown = apps.filter {
                needle.isEmpty() || it.label.lowercase().contains(needle) || it.packageName.contains(needle)
            }
            LazyColumn(Modifier.height(420.dp)) {
                items(shown, key = { it.packageName }) { app ->
                    Row(
                        Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(9.dp))
                            .background(if (app.packageName in chosen) colors.surface2 else Color.Transparent)
                            .clickable {
                                chosen = if (app.packageName in chosen) chosen - app.packageName
                                else chosen + app.packageName
                            }
                            .padding(horizontal = 8.dp, vertical = 11.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Box(
                            Modifier
                                .size(18.dp)
                                .clip(RoundedCornerShape(5.dp))
                                .background(if (app.packageName in chosen) colors.accent else colors.surface)
                                .border(1.dp, if (app.packageName in chosen) colors.accent else colors.border3, RoundedCornerShape(5.dp)),
                            contentAlignment = Alignment.Center,
                        ) {
                            if (app.packageName in chosen) {
                                Icon(NiIcons.Check, null, tint = colors.onAccent, modifier = Modifier.size(13.dp))
                            }
                        }
                        Spacer(Modifier.width(12.dp))
                        Column(Modifier.weight(1f)) {
                            Text(app.label, style = MaterialTheme.typography.bodyMedium, color = colors.text,
                                maxLines = 1, overflow = TextOverflow.Ellipsis)
                            Text(app.packageName, style = MaterialTheme.typography.bodySmall, color = colors.faint,
                                maxLines = 1, overflow = TextOverflow.Ellipsis)
                        }
                    }
                }
            }
        }
    }
}

/** The global per-app filter, which is a profile of its own in the config. */
@Composable
private fun PerAppScreen(app: AppState, onDismiss: () -> Unit) {
    val colors = NimbusTheme.colors
    val settings = app.settings
    var picking by remember { mutableStateOf(false) }

    androidx.compose.material3.ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = colors.panel,
        contentColor = colors.text,
    ) {
        Column(
            Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            Text(I18n.tr("Фильтр приложений"), style = MaterialTheme.typography.titleMedium, color = colors.text)
            Text(
                I18n.tr("Работает, только когда включён TUN: sing-box сам узнаёт, какое приложение открыло соединение."),
                style = MaterialTheme.typography.bodySmall,
                color = colors.muted,
            )
            Segmented(
                options = listOf(
                    RoutePreset.MODE_VPN to I18n.tr("Только выбранные"),
                    RoutePreset.MODE_BYPASS to I18n.tr("Все, кроме выбранных"),
                ),
                selected = settings.perAppMode,
                onSelect = { value -> app.saveSettings { it.perAppMode = value } },
            )
            Row(verticalAlignment = Alignment.CenterVertically) {
                Caption(
                    I18n.plural(
                        settings.perAppPackages.size,
                        "{n} приложение",
                        "{n} приложения",
                        "{n} приложений",
                    ).replace("{n}", settings.perAppPackages.size.toString()),
                    Modifier.weight(1f),
                )
                Chip(I18n.tr("Выбрать"), NiIcons.Apps) { picking = true }
                if (settings.perAppPackages.isNotEmpty()) {
                    Spacer(Modifier.width(8.dp))
                    Chip(I18n.tr("Очистить"), NiIcons.Close) {
                        app.saveSettings { it.perAppPackages = mutableListOf() }
                    }
                }
            }
            if (settings.perAppPackages.isNotEmpty()) {
                NimbusCard {
                    Column(Modifier.padding(vertical = 4.dp)) {
                        for (packageName in settings.perAppPackages) {
                            SettingsRow(title = packageName, subtitle = "")
                        }
                    }
                }
            }
            Chip(I18n.tr("Закрыть"), NiIcons.Check, onClick = onDismiss)
        }
    }

    if (picking) {
        AppPicker(
            selected = settings.perAppPackages,
            title = I18n.tr("Фильтр приложений"),
            onDismiss = { picking = false },
            onDone = { packages ->
                app.saveSettings { it.perAppPackages = packages.toMutableList() }
                picking = false
            },
        )
    }
}

/** The title bar every secondary screen shares. */
@Composable
fun ScreenScaffold(title: String, onBack: () -> Unit, actions: (@Composable () -> Unit)? = null, content: @Composable () -> Unit) {
    val colors = NimbusTheme.colors
    Column(Modifier.fillMaxSize().background(colors.background)) {
        Row(
            Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                com.zalchik.nimbus.ui.NiIcons.Close,
                I18n.tr("Назад"),
                tint = colors.text2,
                modifier = Modifier
                    .size(38.dp)
                    .clip(RoundedCornerShape(10.dp))
                    .clickable(onClick = onBack)
                    .padding(9.dp),
            )
            Text(
                title,
                style = MaterialTheme.typography.titleMedium,
                color = colors.text,
                modifier = Modifier.weight(1f).padding(start = 6.dp),
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            actions?.invoke()
        }
        Box(Modifier.fillMaxSize().verticalScroll(rememberScrollState())) { content() }
    }
}

@Composable
fun HairLine() {
    Box(
        Modifier
            .fillMaxWidth()
            .height(1.dp)
            .background(NimbusTheme.colors.border)
    )
}
