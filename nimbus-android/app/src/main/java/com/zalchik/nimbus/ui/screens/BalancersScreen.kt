package com.zalchik.nimbus.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.zalchik.nimbus.bg.ServiceStatus
import com.zalchik.nimbus.data.Balancer
import com.zalchik.nimbus.data.newId
import com.zalchik.nimbus.engine.Balancers
import com.zalchik.nimbus.engine.Ping
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.NiIcons
import com.zalchik.nimbus.ui.components.Caption
import com.zalchik.nimbus.ui.components.Chip
import com.zalchik.nimbus.ui.components.ConfirmDialog
import com.zalchik.nimbus.ui.components.EmptyState
import com.zalchik.nimbus.ui.components.NimbusCard
import com.zalchik.nimbus.ui.components.NimbusField
import com.zalchik.nimbus.ui.components.Segmented
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.theme.NimbusTheme

/**
 * Balancers: a set of servers that sing-box keeps on the fastest of them. The editor picks the
 * members — one by one or by whole subscription, so servers a panel adds later join too — and
 * chooses whether to hold a node until it fails or to follow the best delay immediately.
 */
@Composable
fun BalancersScreen(
    app: AppState,
    onOpenNodes: (String) -> Unit = {},
    onBack: () -> Unit,
) {
    val colors = NimbusTheme.colors
    val service by app.service.collectAsState()
    var editing by remember { mutableStateOf<Balancer?>(null) }
    var removing by remember { mutableStateOf<Balancer?>(null) }

    ScreenScaffold(
        title = I18n.tr("Балансировщики"),
        onBack = onBack,
        actions = {
            Chip(I18n.tr("Добавить"), NiIcons.Add) {
                editing = Balancer(newId(), I18n.tr("Новый балансировщик"))
            }
        },
    ) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            if (app.store.balancers.isEmpty()) {
                EmptyState(
                    icon = NiIcons.Bolt,
                    text = I18n.tr("Балансировщик сам переключается на самый быстрый сервер из выбранных."),
                    action = I18n.tr("Создать"),
                    onAction = { editing = Balancer(newId(), I18n.tr("Новый балансировщик")) },
                )
            }
            for (balancer in app.store.balancers) {
                val members = app.store.members(balancer)
                val running = service.selectedId == balancer.id && service.status == ServiceStatus.Started
                val live = running && service.currentNode?.isNotEmpty() == true
                NimbusCard(onClick = { app.select(balancer.id, connect = true) }) {
                    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(
                                NiIcons.Bolt,
                                null,
                                tint = if (running) colors.ok else colors.text2,
                                modifier = Modifier.size(17.dp),
                            )
                            Spacer(Modifier.width(10.dp))
                            Text(
                                balancer.name,
                                style = MaterialTheme.typography.titleSmall,
                                color = colors.text,
                                modifier = Modifier.weight(1f),
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis,
                            )
                            Text(
                                Ping.format(balancer.latency),
                                style = com.zalchik.nimbus.ui.theme.MonoStyle,
                                color = colors.muted,
                            )
                        }
                        Text(
                            I18n.plural(members.size, "{n} узел", "{n} узла", "{n} узлов")
                                .replace("{n}", members.size.toString()) +
                                " · " + (if (balancer.isStable) I18n.tr("держит узел") else I18n.tr("всегда лучший")) +
                                " · " + I18n.tr("проверка каждые {n} с", values = mapOf("n" to balancer.interval.toString())),
                            style = MaterialTheme.typography.bodySmall,
                            color = colors.muted,
                        )
                        if (live) {
                            // The node tag, as the server name it belongs to; tap for the whole list.
                            val node = Balancers.memberOf(service.currentNode, members)?.name
                                ?: Balancers.plain(service.currentNode ?: "")
                            Row(
                                Modifier
                                    .clip(RoundedCornerShape(9.dp))
                                    .clickable { onOpenNodes(Balancers.TAG) }
                                    .padding(vertical = 2.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Box(
                                    Modifier.size(7.dp).clip(RoundedCornerShape(4.dp)).background(colors.ok)
                                )
                                Spacer(Modifier.width(8.dp))
                                Text(
                                    node,
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = colors.text2,
                                    modifier = Modifier.weight(1f),
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis,
                                )
                                Icon(NiIcons.List, null, tint = colors.faint, modifier = Modifier.size(15.dp))
                            }
                        }
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Chip(I18n.tr("Изменить"), NiIcons.Edit) { editing = balancer }
                            if (running) Chip(I18n.tr("Проверить"), NiIcons.Timer) { app.retestBalancer() }
                            Chip(I18n.tr("Удалить"), NiIcons.Delete) { removing = balancer }
                        }
                    }
                }
            }
        }
    }

    editing?.let { balancer ->
        BalancerEditor(app, balancer, onDismiss = { editing = null })
    }

    removing?.let { balancer ->
        ConfirmDialog(
            title = I18n.tr("Удалить балансировщик?"),
            message = balancer.name,
            onDismiss = { removing = null },
            onConfirm = {
                app.removeBalancer(balancer.id)
                removing = null
            },
        )
    }
}

@Composable
private fun BalancerEditor(app: AppState, balancer: Balancer, onDismiss: () -> Unit) {
    val colors = NimbusTheme.colors
    var draft by remember {
        mutableStateOf(
            balancer.copy(serverIds = balancer.serverIds.toMutableList(), subIds = balancer.subIds.toMutableList())
        )
    }
    var query by remember { mutableStateOf("") }
    val members = remember(draft, app.store) { app.store.members(draft) }

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
            Text(I18n.tr("Балансировщик"), style = MaterialTheme.typography.titleMedium, color = colors.text)
            NimbusField(
                value = draft.name,
                onValueChange = { value -> draft = draft.copy(name = value) },
                hint = I18n.tr("Название"),
            )

            Column {
                Caption(I18n.tr("Как выбирать"))
                Spacer(Modifier.height(8.dp))
                Segmented(
                    options = listOf(
                        Balancer.MODE_STABLE to I18n.tr("Держать узел"),
                        Balancer.MODE_FASTEST to I18n.tr("Всегда лучший"),
                    ),
                    selected = draft.mode,
                    onSelect = { value -> draft = draft.copy(mode = value) },
                    tips = mapOf(
                        Balancer.MODE_STABLE to I18n.tr("Переключаться, только когда узел упал или другой заметно быстрее"),
                        Balancer.MODE_FASTEST to I18n.tr("Переключаться на самый быстрый сразу — обрывы соединений возможны"),
                    ),
                )
            }

            Row(verticalAlignment = Alignment.CenterVertically) {
                Caption(I18n.tr("Интервал проверки, с"), Modifier.weight(1f))
                Text(
                    draft.interval.toString(),
                    style = com.zalchik.nimbus.ui.theme.MonoStyle,
                    color = colors.text,
                )
            }
            androidx.compose.material3.Slider(
                value = draft.interval.toFloat(),
                onValueChange = { value -> draft = draft.copy(interval = value.toInt()) },
                valueRange = 10f..300f,
                steps = 9,
            )

            // Whole sources: their servers join the balancer, including new ones.
            Column {
                Caption(I18n.tr("Источники целиком"))
                Spacer(Modifier.height(8.dp))
                val sources = app.store.subscriptions.map { it.id to it.title } +
                    app.store.groups.map { it.id to it.name } +
                    ("" to I18n.tr("Мои серверы"))
                for ((id, label) in sources) {
                    val checked = id in draft.subIds
                    CheckLine(label, checked) {
                        draft = if (checked) {
                            draft.copy(subIds = (draft.subIds - id).toMutableList())
                        } else {
                            draft.copy(subIds = (draft.subIds + id).toMutableList())
                        }
                    }
                }
            }

            // Individual servers.
            Column {
                Caption(I18n.tr("Отдельные серверы"))
                Spacer(Modifier.height(8.dp))
                NimbusField(
                    value = query,
                    onValueChange = { query = it },
                    hint = I18n.tr("Поиск"),
                    leading = { Icon(NiIcons.Search, null, tint = colors.faint, modifier = Modifier.size(17.dp)) },
                )
                val needle = query.trim().lowercase()
                val candidates = app.store.servers
                    .filter { needle.isEmpty() || it.name.lowercase().contains(needle) }
                    .take(60)
                for (server in candidates) {
                    val checked = server.id in draft.serverIds
                    CheckLine(
                        server.name,
                        checked,
                        detail = Ping.format(app.delayOf(server)),
                    ) {
                        draft = if (checked) {
                            draft.copy(serverIds = (draft.serverIds - server.id).toMutableList())
                        } else {
                            draft.copy(serverIds = (draft.serverIds + server.id).toMutableList())
                        }
                    }
                }
            }

            Text(
                I18n.tr("Сейчас в наборе: {n}", values = mapOf("n" to members.size.toString())),
                style = MaterialTheme.typography.bodySmall,
                color = if (members.isEmpty()) colors.error else colors.muted,
            )

            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Chip(I18n.tr("Отмена"), NiIcons.Close, onClick = onDismiss)
                Chip(I18n.tr("Сохранить"), NiIcons.Check) {
                    if (draft.name.trim().isEmpty()) draft = draft.copy(name = I18n.tr("Балансировщик"))
                    app.updateBalancer(draft)
                    onDismiss()
                }
            }
        }
    }
}

@Composable
private fun CheckLine(label: String, checked: Boolean, detail: String = "", onClick: () -> Unit) {
    val colors = NimbusTheme.colors
    Row(
        Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(9.dp))
            .background(if (checked) colors.surface2 else Color.Transparent)
            .border(1.dp, if (checked) colors.border2 else colors.border, RoundedCornerShape(9.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(18.dp)
                .clip(RoundedCornerShape(5.dp))
                .background(if (checked) colors.accent else colors.surface)
                .border(1.dp, if (checked) colors.accent else colors.border3, RoundedCornerShape(5.dp)),
            contentAlignment = Alignment.Center,
        ) {
            if (checked) Icon(NiIcons.Check, null, tint = colors.onAccent, modifier = Modifier.size(13.dp))
        }
        Spacer(Modifier.width(12.dp))
        Text(
            label,
            style = MaterialTheme.typography.bodyMedium,
            color = colors.text,
            modifier = Modifier.weight(1f),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
        )
        if (detail.isNotEmpty()) {
            Text(detail, style = com.zalchik.nimbus.ui.theme.MonoStyle, color = colors.muted)
        }
    }
}
