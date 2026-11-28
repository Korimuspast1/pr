package com.zalchik.nimbus.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
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
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.nimbus.bg.ServiceStatus
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.engine.Balancers
import com.zalchik.nimbus.engine.ExitIp
import com.zalchik.nimbus.engine.Selected
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.NiIcons
import com.zalchik.nimbus.ui.components.Caption
import com.zalchik.nimbus.ui.components.MonoText
import com.zalchik.nimbus.ui.components.NimbusCard
import com.zalchik.nimbus.ui.components.PowerButton
import com.zalchik.nimbus.ui.components.Segmented
import com.zalchik.nimbus.ui.components.SpeedGraph
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.state.ExitState
import com.zalchik.nimbus.ui.theme.MonoStyle
import com.zalchik.nimbus.ui.theme.NimbusTheme
import io.nekohasekai.libbox.Libbox

/**
 * The home tab: the desktop sidebar, laid out top to bottom — the power button, what is connected
 * and where it exits, the live speeds, then the mode and routing controls that change how the
 * tunnel is built.
 */
@Composable
fun HomeScreen(
    app: AppState,
    onOpenServers: () -> Unit,
    onOpenRouting: () -> Unit,
    onOpenNodes: (String) -> Unit,
) {
    val colors = NimbusTheme.colors
    val service by app.service.collectAsState()
    val selected = app.selected()
    var history by remember { mutableStateOf(Pair<List<Long>, List<Long>>(emptyList(), emptyList())) }

    // Keep the last minute of speeds so the graph has something to draw between ticks.
    LaunchedEffect(service.uplink, service.downlink, service.trafficAvailable) {
        if (!service.trafficAvailable) {
            history = Pair(emptyList(), emptyList())
            return@LaunchedEffect
        }
        val (up, down) = history
        history = Pair((up + service.uplink).takeLast(60), (down + service.downlink).takeLast(60))
    }

    Column(
        Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 18.dp)
            .padding(bottom = 28.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        PowerRow(
            connected = service.status == ServiceStatus.Started,
            busy = service.status == ServiceStatus.Starting || service.status == ServiceStatus.Stopping,
            onToggle = { app.toggle() },
            title = when (service.status) {
                ServiceStatus.Started -> I18n.tr("Подключено")
                ServiceStatus.Starting -> I18n.tr("Подключение…")
                ServiceStatus.Stopping -> I18n.tr("Отключение…")
                ServiceStatus.Stopped -> I18n.tr("Отключено")
            },
            subtitle = statusLine(app, selected, service.selectedName),
        )

        service.error?.takeIf { it.isNotEmpty() }?.let { message ->
            NimbusCard {
                Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
                    Icon(NiIcons.Warning, null, tint = colors.error, modifier = Modifier.size(17.dp))
                    Spacer(Modifier.width(12.dp))
                    Text(
                        message,
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.text2,
                        modifier = Modifier.weight(1f),
                    )
                }
            }
        }

        app.selectionUnsupported()?.let { reason ->
            NimbusCard {
                Row(Modifier.padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
                    Icon(NiIcons.CloudOff, null, tint = colors.warn, modifier = Modifier.size(17.dp))
                    Spacer(Modifier.width(12.dp))
                    Column(Modifier.weight(1f)) {
                        Text(
                            I18n.tr("Этот сервер не запустится на телефоне"),
                            style = MaterialTheme.typography.bodyMedium,
                            color = colors.text,
                        )
                        Text(reason, style = MaterialTheme.typography.bodySmall, color = colors.muted)
                    }
                }
            }
        }

        // The current server chip: tap to change it, long-press for what it is.
        NimbusCard(onClick = onOpenServers) {
            Row(Modifier.padding(horizontal = 16.dp, vertical = 14.dp), verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text(
                        I18n.tr("Сервер"),
                        style = MaterialTheme.typography.labelSmall,
                        color = colors.muted,
                    )
                    Spacer(Modifier.height(3.dp))
                    Text(
                        selected?.let { nameOf(it) } ?: I18n.tr("Не выбран"),
                        style = MaterialTheme.typography.titleSmall,
                        color = if (selected != null) colors.text else colors.faint,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
                Icon(NiIcons.List, null, tint = colors.muted, modifier = Modifier.size(18.dp))
            }
        }

        // The node traffic goes through right now: a balancer's, or a provider config's own group.
        // Tap for every node of that group and the delay the core measured for each.
        val nodeGroup = service.groups.firstOrNull { it.tag == Balancers.TAG }
            ?: service.groups.firstOrNull { it.selectable }
            ?: service.groups.firstOrNull()
        val nodeTag = service.currentNode?.takeIf { it.isNotEmpty() }
            ?: nodeGroup?.selected?.takeIf { it.isNotEmpty() }
        if (nodeGroup != null && nodeTag != null) {
            val members = (selected as? Selected.OfBalancer)?.members ?: emptyList()
            NimbusCard(onClick = { onOpenNodes(nodeGroup.tag) }) {
                Row(Modifier.padding(horizontal = 16.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                    Icon(NiIcons.Bolt, null, tint = colors.text2, modifier = Modifier.size(16.dp))
                    Spacer(Modifier.width(12.dp))
                    Column(Modifier.weight(1f)) {
                        Text(
                            Balancers.memberOf(nodeTag, members)?.name ?: Balancers.plain(nodeTag),
                            style = MaterialTheme.typography.bodyMedium,
                            color = colors.text2,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                        )
                        Caption(I18n.tr("Узел"))
                    }
                    Text(
                        I18n.tr("Проверить"),
                        style = MaterialTheme.typography.labelLarge,
                        color = colors.text,
                        modifier = Modifier
                            .clip(RoundedCornerShape(9.dp))
                            .clickable { app.testGroup(nodeGroup.tag) }
                            .padding(horizontal = 10.dp, vertical = 6.dp),
                    )
                }
            }
        }

        ExitIpCard(
            exit = app.exit,
            connected = service.status == ServiceStatus.Started,
            onRefresh = { app.checkExitIp(manual = true) },
        )

        if (service.status == ServiceStatus.Started) {
            NimbusCard {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        TrafficColumn(
                            I18n.tr("Отдача"),
                            Libbox.formatBitrate(service.uplink),
                            colors.muted,
                            Modifier.weight(1f),
                        )
                        TrafficColumn(
                            I18n.tr("Приём"),
                            Libbox.formatBitrate(service.downlink),
                            colors.text,
                            Modifier.weight(1f),
                            end = true,
                        )
                    }
                    SpeedGraph(up = history.first, down = history.second)
                    Row {
                        Text(
                            "${I18n.tr("Соединений")}: ${service.connections}",
                            style = MaterialTheme.typography.bodySmall,
                            color = colors.faint,
                            modifier = Modifier.weight(1f),
                        )
                        if (service.startedAt > 0) {
                            Text(
                                Libbox.formatDuration((System.currentTimeMillis() / 1000 - service.startedAt) * 1000),
                                style = MonoStyle.copy(fontSize = 11.5.sp),
                                color = colors.faint,
                            )
                        }
                    }
                }
            }
        }

        NimbusCard {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(I18n.tr("Режим"), style = MaterialTheme.typography.labelSmall, color = colors.muted)
                Segmented(
                    options = listOf("proxy" to I18n.tr("Прокси"), "tun" to "TUN"),
                    selected = app.settings.tunnelMode,
                    onSelect = { mode ->
                        app.saveSettings { it.tunnelMode = mode }
                        app.reloadService()
                    },
                    tips = mapOf(
                        "proxy" to I18n.tr("Только приложения, которые умеют работать через прокси"),
                        "tun" to I18n.tr("Весь трафик устройства через виртуальный интерфейс"),
                    ),
                )
            }
        }

        RoutingCard(app, onOpenRouting)
    }
}

/** The line under "Подключено": the protocol of what actually runs. */
private fun statusLine(app: AppState, selected: Selected?, name: String): String = when {
    selected == null -> I18n.tr("Выберите сервер, чтобы подключиться")
    selected is Selected.OfBalancer -> I18n.tr("Балансировщик") + " · " +
        I18n.plural(selected.members.size, "{n} узел", "{n} узла", "{n} узлов")
        .replace("{n}", selected.members.size.toString())
    selected is Selected.OfServer -> selected.server.protocol.uppercase() +
        if (selected.server.subId.isNotEmpty() && app.store.server(selected.server.id) != null) {
            " · " + app.ownerOf(selected.server)
        } else {
            ""
        }
    else -> name
}

private fun nameOf(selected: Selected): String = when (selected) {
    is Selected.OfServer -> selected.server.name
    is Selected.OfBalancer -> selected.balancer.name
}

@Composable
private fun PowerRow(
    connected: Boolean,
    busy: Boolean,
    onToggle: () -> Unit,
    title: String,
    subtitle: String,
) {
    val colors = NimbusTheme.colors
    NimbusCard {
        Column(
            Modifier.fillMaxWidth().padding(vertical = 22.dp, horizontal = 18.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            PowerButton(connected = connected, busy = busy, onClick = onToggle, size = 118)
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Text(
                    title,
                    style = MaterialTheme.typography.titleMedium,
                    color = colors.text,
                    fontWeight = FontWeight.SemiBold,
                )
                if (subtitle.isNotEmpty()) {
                    Text(
                        subtitle,
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.muted,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
            }
        }
    }
}

@Composable
private fun TrafficColumn(label: String, value: String, color: Color, modifier: Modifier = Modifier, end: Boolean = false) {
    val colors = NimbusTheme.colors
    Column(
        modifier,
        horizontalAlignment = if (end) Alignment.End else Alignment.Start,
    ) {
        Text(label.uppercase(), style = MaterialTheme.typography.labelSmall, color = colors.muted)
        Spacer(Modifier.height(2.dp))
        MonoText(value, color = color)
    }
}

/** The exit address and the country it belongs to, with a copy on tap. */
@Composable
private fun ExitIpCard(exit: ExitState, connected: Boolean, onRefresh: () -> Unit) {
    val colors = NimbusTheme.colors
    val context = androidx.compose.ui.platform.LocalContext.current
    val ip = exit.ip
    NimbusCard(onClick = if (ip.isNotEmpty()) {
        { com.zalchik.nimbus.ui.components.copyToClipboard(context, ip) }
    } else {
        null
    }) {
        Row(Modifier.padding(horizontal = 16.dp, vertical = 13.dp), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(I18n.tr("Ваш адрес"), style = MaterialTheme.typography.labelSmall, color = colors.muted)
                Spacer(Modifier.height(3.dp))
                when {
                    !connected -> Text(I18n.tr("Нет подключения"), style = MaterialTheme.typography.bodyMedium, color = colors.faint)
                    exit.status == "checking" -> Row(verticalAlignment = Alignment.CenterVertically) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(11.dp),
                            color = colors.muted,
                            strokeWidth = 1.5.dp,
                        )
                        Spacer(Modifier.width(8.dp))
                        Text(I18n.tr("Проверка…"), style = MaterialTheme.typography.bodyMedium, color = colors.muted)
                    }

                    exit.status == "ok" -> Row(verticalAlignment = Alignment.CenterVertically) {
                        MonoText(exit.ip)
                        exit.country?.let { code ->
                            Spacer(Modifier.width(8.dp))
                            Text(
                                code.uppercase(),
                                style = MaterialTheme.typography.labelSmall,
                                color = colors.text2,
                                modifier = Modifier
                                    .clip(RoundedCornerShape(4.dp))
                                    .background(colors.surface2)
                                    .border(1.dp, colors.border, RoundedCornerShape(4.dp))
                                    .padding(horizontal = 5.dp, vertical = 1.dp),
                            )
                            val country = ExitIp.countryName(code)
                            if (!country.isNullOrEmpty()) {
                                Spacer(Modifier.width(6.dp))
                                Text(country, style = MaterialTheme.typography.bodySmall, color = colors.muted)
                            }
                        }
                    }

                    else -> Text(I18n.tr("Не удалось узнать"), style = MaterialTheme.typography.bodyMedium, color = colors.error)
                }
            }
            if (connected) {
                Icon(
                    NiIcons.Refresh,
                    I18n.tr("Обновить"),
                    tint = colors.muted,
                    modifier = Modifier
                        .size(18.dp)
                        .clip(CircleShape)
                        .clickable(onClick = onRefresh),
                )
            }
        }
    }
}

/** Mode + routing preset, the two controls that shape every connection. */
@Composable
private fun RoutingCard(app: AppState, onOpenRouting: () -> Unit) {
    val colors = NimbusTheme.colors
    val settings: Settings = app.settings
    val preset = settings.activePreset
    NimbusCard {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    I18n.tr("Маршрутизация"),
                    style = MaterialTheme.typography.labelSmall,
                    color = colors.muted,
                    modifier = Modifier.weight(1f),
                )
                Text(
                    I18n.tr("Настроить"),
                    style = MaterialTheme.typography.labelLarge,
                    color = colors.text2,
                    modifier = Modifier
                        .clip(RoundedCornerShape(9.dp))
                        .clickable(onClick = onOpenRouting)
                        .padding(horizontal = 8.dp, vertical = 4.dp),
                )
            }
            Segmented(
                options = listOf(
                    "bypass_ru" to I18n.tr("Умная"),
                    "global" to I18n.tr("Весь трафик"),
                ),
                selected = settings.routingMode,
                onSelect = { app.setRoutingMode(it) },
            )
            Row(
                Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(9.dp))
                    .background(colors.surface2)
                    .border(1.dp, colors.border, RoundedCornerShape(9.dp))
                    .clickable(onClick = onOpenRouting)
                    .padding(horizontal = 12.dp, vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(NiIcons.Shield, null, tint = colors.muted, modifier = Modifier.size(16.dp))
                Spacer(Modifier.width(10.dp))
                Text(
                    preset?.name ?: I18n.tr("Без профиля"),
                    style = MaterialTheme.typography.bodyMedium,
                    color = colors.text2,
                    modifier = Modifier.weight(1f),
                )
                Icon(NiIcons.Dropdown, null, tint = colors.faint, modifier = Modifier.size(18.dp))
            }
            if (settings.perAppEnabled) {
                Text(
                    I18n.tr("Включён фильтр приложений"),
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.muted,
                )
            }
            if (settings.useProviderRouting) {
                Text(
                    I18n.tr("Правила провайдера включены"),
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.ok,
                )
            }
        }
    }
}
