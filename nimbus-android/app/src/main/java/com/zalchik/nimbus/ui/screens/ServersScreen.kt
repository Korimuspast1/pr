package com.zalchik.nimbus.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.combinedClickable
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
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.nimbus.bg.ServiceStatus
import com.zalchik.nimbus.data.Balancer
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.data.Subscription
import com.zalchik.nimbus.engine.Links
import com.zalchik.nimbus.engine.Ping
import com.zalchik.nimbus.engine.Share
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.NiIcons
import com.zalchik.nimbus.ui.components.ActionSheet
import com.zalchik.nimbus.ui.components.Chip
import com.zalchik.nimbus.ui.components.ConfirmDialog
import com.zalchik.nimbus.ui.components.EmptyState
import com.zalchik.nimbus.ui.components.GroupPickerSheet
import com.zalchik.nimbus.ui.components.NimbusCard
import com.zalchik.nimbus.ui.components.NimbusField
import com.zalchik.nimbus.ui.components.QrSheet
import com.zalchik.nimbus.ui.components.ReadOnlySheet
import com.zalchik.nimbus.ui.components.ServerRow
import com.zalchik.nimbus.ui.components.SheetAction
import com.zalchik.nimbus.ui.components.TextInputDialog
import com.zalchik.nimbus.ui.components.copyToClipboard
import com.zalchik.nimbus.ui.components.shareText
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.theme.NimbusTheme

/** One thing the list can show: a section header, a server, a subscription or a balancer. */
private sealed class Row {
    data class Header(val text: String, val detail: String = "") : Row()
    data class Item(val server: Server, val unsupported: String?) : Row()
    data class Sub(val subscription: Subscription, val count: Int) : Row()
    data class Bal(val balancer: Balancer, val count: Int) : Row()
    data class Spacer(val key: String) : Row()
}

/**
 * The servers tab: everything that can be connected to — loose servers, subscriptions and
 * balancers — with the search field, the sort control and the per-row actions the desktop's list
 * has in its context menus.
 */
@Composable
fun ServersScreen(
    app: AppState,
    onImport: () -> Unit,
    onScanQr: () -> Unit,
    onOpenBalancers: () -> Unit,
) {
    val colors = NimbusTheme.colors
    val context = androidx.compose.ui.platform.LocalContext.current
    val service by app.service.collectAsState()
    val offline by app.offlineDelays.collectAsState()

    var query by remember { mutableStateOf("") }
    var group by remember { mutableStateOf("") }
    var sort by remember { mutableStateOf(app.settings.sortMode) }
    var actions by remember { mutableStateOf<Row?>(null) }
    var confirm by remember { mutableStateOf<Pair<String, String>?>(null) }
    var rename by remember { mutableStateOf<Pair<String, String>?>(null) }
    var move by remember { mutableStateOf<String?>(null) }
    var qr by remember { mutableStateOf<Pair<String, String>?>(null) }
    var view by remember { mutableStateOf<Pair<String, String>?>(null) }
    var showSort by remember { mutableStateOf(false) }

    val rows = remember(app.store, query, group, sort, offline, service.selectedId) {
        buildRows(app, query, group, sort)
    }

    Box(Modifier.fillMaxSize()) {
        Column(Modifier.fillMaxSize()) {
            // Header: how many servers, the search field, and the sort/group controls.
            Column(Modifier.padding(horizontal = 18.dp, vertical = 12.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        I18n.tr("Серверы") + " · " + app.store.servers.size,
                        style = MaterialTheme.typography.titleMedium,
                        color = colors.text,
                        modifier = Modifier.weight(1f),
                    )
                    if (app.busy.isNotEmpty()) {
                        CircularProgressIndicator(modifier = Modifier.size(13.dp), color = colors.muted, strokeWidth = 1.5.dp)
                        Spacer(Modifier.width(8.dp))
                        Text(app.busy, style = MaterialTheme.typography.bodySmall, color = colors.muted)
                    }
                }
                NimbusField(
                    value = query,
                    onValueChange = { query = it },
                    hint = I18n.tr("Поиск"),
                    leading = { Icon(NiIcons.Search, null, tint = colors.faint, modifier = Modifier.size(18.dp)) },
                    trailing = {
                        if (query.isNotEmpty()) {
                            Icon(
                                NiIcons.Close,
                                I18n.tr("Очистить"),
                                tint = colors.muted,
                                modifier = Modifier.size(18.dp).clickable { query = "" },
                            )
                        }
                    },
                )
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Box(Modifier.weight(1f)) {
                        Chip(
                            groupLabel(app, group),
                            NiIcons.List,
                        ) { showSort = !showSort }
                        if (showSort) {
                            GroupMenu(
                                app = app,
                                current = group,
                                onPick = {
                                    group = it
                                    showSort = false
                                },
                                sort = sort,
                                onSort = { mode ->
                                    sort = mode
                                    app.saveSettings { it.sortMode = mode }
                                },
                            )
                        }
                    }
                    Chip(I18n.tr("Задержка"), NiIcons.Timer) { app.ping() }
                    Chip(I18n.tr("Импорт"), NiIcons.Add, onClick = onImport)
                }
            }

            if (rows.isEmpty()) {
                EmptyState(
                    icon = NiIcons.Cloud,
                    text = if (app.store.servers.isEmpty()) {
                        I18n.tr("Серверов пока нет. Добавьте подписку, ссылку или конфиг.")
                    } else {
                        I18n.tr("Ничего не найдено")
                    },
                    action = if (app.store.servers.isEmpty()) I18n.tr("Добавить") else "",
                    onAction = if (app.store.servers.isEmpty()) onImport else null,
                )
            } else {
                LazyColumn(
                    Modifier.fillMaxSize(),
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(
                        start = 12.dp,
                        end = 12.dp,
                        bottom = 96.dp,
                    ),
                ) {
                    items(rows, key = { keyOf(it) }) { row ->
                        when (row) {
                            is Row.Header -> SectionHeader(row.text, row.detail)
                            is Row.Spacer -> Spacer(Modifier.height(8.dp))
                            is Row.Item -> {
                                val server = row.server
                                val selected = service.selectedId == server.id &&
                                    service.status != ServiceStatus.Stopped
                                val delay = app.delayOf(server)
                                ServerRow(
                                    name = server.name,
                                    detail = serverDetail(app, server),
                                    delay = Ping.format(delay),
                                    selected = service.selectedId == server.id,
                                    badge = server.protocol.uppercase(),
                                    unreachable = delay == -1,
                                    onClick = { app.select(server.id, connect = selected || service.status == ServiceStatus.Started) },
                                    onLongClick = { actions = row },
                                )
                            }

                            is Row.Sub -> SubscriptionRow(
                                row.subscription,
                                row.count,
                                onClick = { group = row.subscription.id; showSort = false },
                                onLongClick = { actions = row },
                            )

                            is Row.Bal -> BalancerRow(
                                row.balancer,
                                row.count,
                                selected = service.selectedId == row.balancer.id,
                                onClick = { app.select(row.balancer.id, connect = true) },
                                onLongClick = { actions = row },
                            )
                        }
                    }
                }
            }
        }
    }

    // -- what a long press opened -----------------------------------------------------------------

    actions?.let { row ->
        ActionSheet(
            title = when (row) {
                is Row.Item -> row.server.name
                is Row.Sub -> row.subscription.title
                is Row.Bal -> row.balancer.name
                else -> ""
            },
            onDismiss = { actions = null },
            actions = when (row) {
                is Row.Item -> serverActions(app, context, row.server, service.status == ServiceStatus.Started,
                    onQr = { title, text -> qr = title to text },
                    onView = { title, text -> view = title to text },
                    onRename = { rename = row.server.id to row.server.name },
                    onMove = { move = row.server.id },
                    onDelete = { confirm = I18n.tr("Удалить сервер?") to row.server.id },
                )

                is Row.Sub -> subscriptionActions(app, context, row.subscription,
                    onView = { title, text -> view = title to text },
                    onRename = { rename = row.subscription.id to row.subscription.customName },
                    onDelete = { confirm = I18n.tr("Удалить подписку?") to "sub:" + row.subscription.id },
                )

                is Row.Bal -> listOf(
                    SheetAction(I18n.tr("Открыть"), { onOpenBalancers() }, NiIcons.Tune),
                    SheetAction(I18n.tr("Проверить задержку"), { app.retestBalancer() }, NiIcons.Timer,
                        enabled = service.status == ServiceStatus.Started),
                    SheetAction(I18n.tr("Удалить"), { confirm = I18n.tr("Удалить балансировщик?") to "bal:" + row.balancer.id },
                        NiIcons.Delete, destructive = true),
                )

                else -> emptyList()
            },
        )
    }

    confirm?.let { (title, key) ->
        val message = when {
            key.startsWith("sub:") -> I18n.tr("Подписка и её серверы будут удалены.")
            key.startsWith("bal:") -> I18n.tr("Серверы останутся в списке.")
            else -> I18n.tr("Сервер будет удалён из списка.")
        }
        ConfirmDialog(
            title = title,
            message = message,
            onDismiss = { confirm = null },
            onConfirm = {
                when {
                    key.startsWith("sub:") -> app.removeSubscription(key.removePrefix("sub:"))
                    key.startsWith("bal:") -> app.removeBalancer(key.removePrefix("bal:"))
                    else -> app.removeServer(key)
                }
                confirm = null
            },
        )
    }

    rename?.let { (id, current) ->
        val isSub = app.store.subscription(id) != null
        TextInputDialog(
            title = if (isSub) I18n.tr("Название подписки") else I18n.tr("Название сервера"),
            initial = current,
            onDismiss = { rename = null },
            onConfirm = { value ->
                if (value.isNotEmpty()) {
                    if (isSub) app.renameSubscription(id, value) else app.renameServer(id, value)
                }
                rename = null
            },
        )
    }

    move?.let { id ->
        GroupPickerSheet(
            groups = app.store.subscriptions.map { it.id to it.title } +
                app.store.groups.map { it.id to it.name },
            current = app.store.server(id)?.subId ?: "",
            title = I18n.tr("Переместить сервер"),
            onDismiss = { move = null },
            onPick = { target ->
                app.moveServer(id, target)
                move = null
            },
            onCreate = { name ->
                val created = app.addGroup(name)
                app.moveServer(id, created.id)
                move = null
            },
        )
    }

    qr?.let { (title, text) -> QrSheet(text, title) { qr = null } }
    view?.let { (title, text) -> ReadOnlySheet(title, text) { view = null } }
}

/** The filter/sort menu that opens under the group chip. */
@Composable
private fun GroupMenu(
    app: AppState,
    current: String,
    onPick: (String) -> Unit,
    sort: String,
    onSort: (String) -> Unit,
) {
    val colors = NimbusTheme.colors
    NimbusCard(modifier = Modifier.padding(top = 6.dp)) {
        Column(Modifier.padding(10.dp), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(I18n.tr("Сортировка"), style = MaterialTheme.typography.labelSmall, color = colors.muted,
                modifier = Modifier.padding(start = 8.dp, bottom = 4.dp))
            for ((key, label) in listOf(
                "source" to I18n.tr("Как в списке"),
                "ping" to I18n.tr("По задержке"),
                "name" to I18n.tr("По имени"),
            )) {
                MenuLine(label, key == sort) { onSort(key) }
            }
            Spacer(Modifier.height(8.dp))
            Text(I18n.tr("Раздел"), style = MaterialTheme.typography.labelSmall, color = colors.muted,
                modifier = Modifier.padding(start = 8.dp, bottom = 4.dp))
            MenuLine(I18n.tr("Все"), current.isEmpty()) { onPick("") }
            MenuLine(I18n.tr("Мои серверы"), current == "mine") { onPick("mine") }
            for (subscription in app.store.subscriptions) {
                MenuLine(subscription.title, current == subscription.id) { onPick(subscription.id) }
            }
            for (group in app.store.groups) {
                MenuLine(group.name, current == group.id) { onPick(group.id) }
            }
        }
    }
}

@Composable
private fun MenuLine(label: String, active: Boolean, onClick: () -> Unit) {
    val colors = NimbusTheme.colors
    Row(
        Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .background(if (active) colors.surface2 else Color.Transparent)
            .clickable(onClick = onClick)
            .padding(horizontal = 8.dp, vertical = 9.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            label,
            style = MaterialTheme.typography.bodyMedium,
            color = if (active) colors.text else colors.text2,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.weight(1f),
        )
        if (active) Icon(NiIcons.Check, null, tint = colors.text, modifier = Modifier.size(15.dp))
    }
}

@Composable
private fun SectionHeader(text: String, detail: String) {
    val colors = NimbusTheme.colors
    Row(Modifier.padding(start = 6.dp, top = 14.dp, bottom = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(
            text.uppercase(),
            style = MaterialTheme.typography.labelSmall,
            color = colors.muted,
            fontWeight = FontWeight.SemiBold,
        )
        if (detail.isNotEmpty()) {
            Spacer(Modifier.width(8.dp))
            Text(detail, style = MaterialTheme.typography.labelSmall, color = colors.faint)
        }
    }
}

@Composable
private fun SubscriptionRow(subscription: Subscription, count: Int, onClick: () -> Unit, onLongClick: () -> Unit) {
    val colors = NimbusTheme.colors
    Row(
        Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(9.dp))
            .combinedClickable(onClick = onClick, onLongClick = onLongClick)
            .padding(horizontal = 12.dp, vertical = 11.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(NiIcons.Cloud, null, tint = colors.muted, modifier = Modifier.size(17.dp))
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(subscription.title, style = MaterialTheme.typography.bodyLarge, color = colors.text,
                maxLines = 1, overflow = TextOverflow.Ellipsis)
            Text(
                if (subscription.error.isNotEmpty()) subscription.error
                else I18n.plural(count, "{n} сервер", "{n} сервера", "{n} серверов").replace("{n}", count.toString()),
                style = MaterialTheme.typography.bodySmall,
                color = if (subscription.error.isNotEmpty()) colors.error else colors.muted,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
        }
    }
}

@Composable
private fun BalancerRow(balancer: Balancer, count: Int, selected: Boolean, onClick: () -> Unit, onLongClick: () -> Unit) {
    val colors = NimbusTheme.colors
    Row(
        Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(9.dp))
            .background(if (selected) colors.surface2 else Color.Transparent)
            .combinedClickable(onClick = onClick, onLongClick = onLongClick)
            .padding(horizontal = 12.dp, vertical = 11.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(NiIcons.Bolt, null, tint = colors.text2, modifier = Modifier.size(17.dp))
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(balancer.name, style = MaterialTheme.typography.bodyLarge, color = colors.text,
                fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal,
                maxLines = 1, overflow = TextOverflow.Ellipsis)
            Text(
                I18n.tr("Балансировщик") + " · " +
                    I18n.plural(count, "{n} узел", "{n} узла", "{n} узлов").replace("{n}", count.toString()) +
                    " · " + (if (balancer.isStable) I18n.tr("стабильный") else I18n.tr("быстрый")),
                style = MaterialTheme.typography.bodySmall,
                color = colors.muted,
            )
        }
        Text(Ping.format(balancer.latency), style = com.zalchik.nimbus.ui.theme.MonoStyle.copy(fontSize = 12.sp), color = colors.muted)
    }
}

private fun groupLabel(app: AppState, group: String): String = when {
    group.isEmpty() -> I18n.tr("Все разделы")
    group == "mine" -> I18n.tr("Мои серверы")
    else -> app.store.subscription(group)?.title ?: app.store.group(group)?.name ?: I18n.tr("Все разделы")
}

private fun serverDetail(app: AppState, server: Server): String {
    val owner = app.ownerOf(server)
    val nodes = Share.nodeCount(server)
    val parts = mutableListOf<String>()
    if (owner.isNotEmpty()) parts += owner
    if (nodes > 1) parts += I18n.plural(nodes, "{n} узел", "{n} узла", "{n} узлов").replace("{n}", nodes.toString())
    val domains = Links.serverDomains(Links.outboundsOf(server))
    domains.firstOrNull()?.let { parts += it }
    return parts.joinToString(" · ")
}

/** Everything the list shows, in the order it shows it. */
private fun buildRows(app: AppState, query: String, group: String, sort: String): List<Row> {
    val rows = mutableListOf<Row>()
    val needle = query.trim().lowercase()

    fun matches(server: Server) = needle.isEmpty() ||
        server.name.lowercase().contains(needle) ||
        server.protocol.lowercase().contains(needle) ||
        server.link.lowercase().contains(needle)

    fun sorted(servers: List<Server>): List<Server> = when (sort) {
        "name" -> servers.sortedBy { it.name.lowercase() }
        "ping" -> servers.sortedBy { it.latency ?: Int.MAX_VALUE }
        else -> servers
    }

    // Balancers first: they are what most people connect to.
    val balancers = app.store.balancers.filter { balancer ->
        needle.isEmpty() || balancer.name.lowercase().contains(needle)
    }
    if (balancers.isNotEmpty() && (group.isEmpty() || group == "mine")) {
        rows += Row.Header(I18n.tr("Балансировщики"), balancers.size.toString())
        rows += balancers.map { Row.Bal(it, app.store.members(it).size) }
        rows += Row.Spacer("bal")
    }

    // Subscriptions, each with its servers under it — unless a specific section is chosen.
    val sections = when {
        group == "mine" -> emptyList()
        group.isEmpty() -> app.store.subscriptions.map { it.id as Any } + app.store.groups.map { it.id as Any }
        else -> listOf<Any>(group)
    }
    var anyServer = false
    for (sectionId in sections) {
        val servers = sorted(app.store.serversOf(sectionId.toString()).filter(::matches))
        if (servers.isEmpty() && needle.isEmpty() && sections.size == 1) {
            // An explicitly chosen empty subscription still gets its header.
        } else if (servers.isEmpty()) continue
        val subscription = app.store.subscription(sectionId.toString())
        val label = subscription?.title ?: app.store.group(sectionId.toString())?.name ?: sectionId.toString()
        val detail = if (subscription != null && subscription.error.isNotEmpty()) subscription.error else servers.size.toString()
        rows += Row.Header(label, detail)
        rows += servers.map { Row.Item(it, Links.unsupportedReason(it)) }
        anyServer = anyServer || servers.isNotEmpty()
    }

    // Loose servers ("Мои серверы").
    if (group.isEmpty() || group == "mine") {
        val loose = sorted(
            app.store.servers.filter { it.subId.isEmpty() || (app.store.subscription(it.subId) == null && app.store.group(it.subId) == null) }
                .filter(::matches)
        )
        if (loose.isNotEmpty() || group == "mine") {
            rows += Row.Header(I18n.tr("Мои серверы"), loose.size.toString())
            rows += loose.map { Row.Item(it, Links.unsupportedReason(it)) }
            anyServer = anyServer || loose.isNotEmpty()
        }
    } else if (group !in app.store.subscriptions.map { it.id } && group !in app.store.groups.map { it.id }) {
        // Nothing matched that section.
    }
    if (!anyServer && rows.isEmpty()) return emptyList()
    return rows
}

private fun keyOf(row: Row): String = when (row) {
    is Row.Header -> "h:" + row.text
    is Row.Item -> "s:" + row.server.id
    is Row.Sub -> "u:" + row.subscription.id
    is Row.Bal -> "b:" + row.balancer.id
    is Row.Spacer -> "p:" + row.key
}

private fun serverActions(
    app: AppState,
    context: android.content.Context,
    server: Server,
    connected: Boolean,
    onQr: (String, String) -> Unit,
    onView: (String, String) -> Unit,
    onRename: () -> Unit,
    onMove: () -> Unit,
    onDelete: () -> Unit,
): List<SheetAction> {
    val link = Share.serverLink(server)
    val actions = mutableListOf<SheetAction>()
    actions += SheetAction(I18n.tr("Проверить задержку"), { app.ping(listOf(server)) }, NiIcons.Timer)
    if (link != null) {
        actions += SheetAction(I18n.tr("QR-код"), { onQr(server.name, link) }, NiIcons.Qr)
        actions += SheetAction(I18n.tr("Скопировать ссылку"), {
            copyToClipboard(context, link)
            app.say(I18n.tr("Ссылка скопирована"))
        }, NiIcons.Copy)
        actions += SheetAction(I18n.tr("Поделиться ссылкой"), { shareText(context, link, server.name) }, NiIcons.Share)
    }
    actions += SheetAction(I18n.tr("Показать конфиг"), {
        onView(server.name, app.configOf(server))
    }, NiIcons.File)
    actions += SheetAction(I18n.tr("Скопировать JSON"), {
        copyToClipboard(context, Share.exportJson(listOf(server), app.settings))
        app.say(I18n.tr("JSON скопирован"))
    }, NiIcons.Copy)
    val unsupported = Links.unsupportedReason(server)
    if (unsupported != null) {
        actions += SheetAction(I18n.tr("Почему не работает"), { onView(server.name, unsupported) }, NiIcons.Warning)
    }
    actions += SheetAction(I18n.tr("Переименовать"), onRename, NiIcons.Edit)
    actions += SheetAction(I18n.tr("Переместить в группу"), onMove, NiIcons.List)
    actions += SheetAction(I18n.tr("Удалить"), onDelete, NiIcons.Delete, destructive = true)
    return actions
}

private fun subscriptionActions(
    app: AppState,
    context: android.content.Context,
    subscription: Subscription,
    onView: (String, String) -> Unit,
    onRename: () -> Unit,
    onDelete: () -> Unit,
): List<SheetAction> = mutableListOf(
    SheetAction(I18n.tr("Обновить"), { app.updateSubscription(subscription.id) }, NiIcons.Refresh),
    SheetAction(I18n.tr("Скопировать ссылку"), {
        copyToClipboard(context, subscription.url)
        app.say(I18n.tr("Ссылка скопирована"))
    }, NiIcons.Copy),
    SheetAction(I18n.tr("Поделиться"), { shareText(context, app.deeplinkFor(subscription.url), subscription.title) }, NiIcons.Share),
    SheetAction(I18n.tr("Свойства"), {
        onView(subscription.title, describe(app, subscription))
    }, NiIcons.Info),
    SheetAction(I18n.tr("Переименовать"), onRename, NiIcons.Edit),
    SheetAction(
        if (subscription.sendHwid) I18n.tr("Не отправлять HWID") else I18n.tr("Отправлять HWID"),
        { app.setSubscriptionHwid(subscription.id, !subscription.sendHwid) },
        NiIcons.Lock,
        detail = I18n.tr("Панель требует идентификацию устройства"),
    ),
    SheetAction(I18n.tr("User-Agent"), {
        app.say(I18n.tr("Откройте свойства подписки в настройках"))
    }, NiIcons.Edit, enabled = false),
    SheetAction(I18n.tr("Удалить"), onDelete, NiIcons.Delete, destructive = true),
)

private fun describe(app: AppState, subscription: Subscription): String {
    val fields = linkedMapOf<String, String>(
        I18n.tr("Адрес") to subscription.url,
        I18n.tr("Серверов") to app.store.serversOf(subscription.id).size.toString(),
        I18n.tr("Обновлена") to (if (subscription.lastUpdated > 0) I18n.date(subscription.lastUpdated * 1000) else "—"),
        I18n.tr("Интервал") to (if (subscription.updateHours > 0) "${subscription.updateHours} ч" else I18n.tr("по настройке")),
        I18n.tr("User-Agent") to subscription.userAgent.ifEmpty { app.settings.subUserAgent },
        I18n.tr("HWID") to (if (subscription.sendHwid) I18n.tr("отправляется") else I18n.tr("авто")),
    )
    if (subscription.expire > 0) fields[I18n.tr("Истекает")] = I18n.date(subscription.expire * 1000)
    if (subscription.total > 0) {
        fields[I18n.tr("Трафик")] = "${io.nekohasekai.libbox.Libbox.formatBytes(subscription.upload)} / " +
            "${io.nekohasekai.libbox.Libbox.formatBytes(subscription.total)}"
    }
    if (subscription.announce.isNotEmpty()) fields[I18n.tr("Объявление")] = subscription.announce
    if (subscription.error.isNotEmpty()) fields[I18n.tr("Ошибка")] = subscription.error
    return fields.entries.joinToString("\n") { "${it.key}: ${it.value}" }
}
