package com.zalchik.nimbus.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.NiIcons
import com.zalchik.nimbus.ui.components.Caption
import com.zalchik.nimbus.ui.components.Chip
import com.zalchik.nimbus.ui.components.NimbusCard
import com.zalchik.nimbus.ui.components.NimbusField
import com.zalchik.nimbus.ui.components.Segmented
import com.zalchik.nimbus.ui.components.SettingsRow
import com.zalchik.nimbus.ui.components.SwitchRow
import com.zalchik.nimbus.ui.components.TextInputDialog
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.theme.NimbusTheme

/**
 * DNS: which resolver answers what. The remote server answers through the tunnel, the direct one
 * answers for the addresses that bypass it, and fake DNS hands out placeholders so the core can
 * see the domain a connection asks for.
 */
@Composable
fun DnsScreen(app: AppState, onBack: () -> Unit) {
    val colors = NimbusTheme.colors
    val settings = app.settings
    var editing by remember { mutableStateOf("") }

    ScreenScaffold(title = "DNS", onBack = onBack) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            NimbusCard {
                Column {
                    SettingsRow(
                        title = I18n.tr("Удалённый DNS"),
                        subtitle = settings.remoteDns,
                        icon = NiIcons.Public,
                        onClick = { editing = "remote" },
                    )
                    HairLine()
                    SettingsRow(
                        title = I18n.tr("Прямой DNS"),
                        subtitle = settings.directDns,
                        icon = NiIcons.Dns,
                        onClick = { editing = "direct" },
                    )
                }
            }
            Text(
                I18n.tr("Удалённый — DoH (https://…) или адрес:порт. Прямой — то же самое, но его ответы идут мимо туннеля."),
                style = MaterialTheme.typography.bodySmall,
                color = colors.muted,
                modifier = Modifier.padding(horizontal = 4.dp),
            )

            NimbusCard {
                Column {
                    SwitchRow(
                        I18n.tr("Подменять адреса (fakeip)"),
                        settings.fakeDns,
                        { value ->
                            app.saveSettings { it.fakeDns = value }
                            app.reloadService()
                        },
                        icon = NiIcons.Bolt,
                        subtitle = I18n.tr("Быстрее и надёжнее: домен виден даже когда приложение шлёт запрос первым"),
                    )
                    HairLine()
                    SwitchRow(
                        I18n.tr("Определять домен по трафику"),
                        settings.sniffing,
                        { value ->
                            app.saveSettings { it.sniffing = value }
                            app.reloadService()
                        },
                        icon = NiIcons.Search,
                        subtitle = I18n.tr("TLS/SNI, HTTP Host и QUIC — правила по домену работают даже без fakeip"),
                    )
                }
            }

            NimbusCard {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Caption(I18n.tr("Стратегия домена"))
                    Segmented(
                        options = listOf(
                            "AsIs" to "AsIs",
                            "IPIfNonMatch" to I18n.tr("IP если нет"),
                            "IPOnDemand" to I18n.tr("IP по запросу"),
                        ),
                        selected = settings.domainStrategy,
                        onSelect = { value ->
                            app.saveSettings { it.domainStrategy = value }
                            app.reloadService()
                        },
                        tips = mapOf(
                            "AsIs" to I18n.tr("Как ответил DNS — адрес или домен"),
                            "IPIfNonMatch" to I18n.tr("Правила сначала по домену, потом по адресу"),
                            "IPOnDemand" to I18n.tr("Разрешать домен в адрес, если правило требует адрес"),
                        ),
                    )
                }
            }
        }
    }

    editing.takeIf { it.isNotEmpty() }?.let { which ->
        val remote = which == "remote"
        TextInputDialog(
            title = if (remote) I18n.tr("Удалённый DNS") else I18n.tr("Прямой DNS"),
            initial = if (remote) settings.remoteDns else settings.directDns,
            hint = if (remote) "https://1.1.1.1/dns-query" else "8.8.8.8",
            onDismiss = { editing = "" },
            onConfirm = { value ->
                if (value.isNotEmpty()) {
                    app.saveSettings {
                        if (remote) it.remoteDns = value else it.directDns = value
                    }
                    app.reloadService()
                }
                editing = ""
            },
        )
    }
}

/**
 * The tunnel's own knobs: how it is built (mode, MTU, always-on) and what sing-box does to the
 * traffic inside it (multiplexing, fragmentation, log level).
 */
@Composable
fun AdvancedScreen(app: AppState, onBack: () -> Unit) {
    val colors = NimbusTheme.colors
    val settings = app.settings
    var editing by remember { mutableStateOf("") }

    ScreenScaffold(title = I18n.tr("Туннель"), onBack = onBack) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            NimbusCard {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Caption(I18n.tr("Режим"))
                    Segmented(
                        options = listOf("tun" to "TUN", "proxy" to I18n.tr("Прокси")),
                        selected = settings.tunnelMode,
                        onSelect = { value ->
                            app.saveSettings { it.tunnelMode = value }
                            app.reloadService()
                        },
                        tips = mapOf(
                            "tun" to I18n.tr("Виртуальный интерфейс: работает со всеми приложениями"),
                            "proxy" to I18n.tr("Только приложения, которые сами ходят через прокси"),
                        ),
                    )
                }
            }

            NimbusCard {
                Column {
                    SettingsRow(
                        title = "MTU",
                        subtitle = I18n.tr("Размер пакета виртуального интерфейса"),
                        icon = NiIcons.Tune,
                        value = settings.tunMtu.toString(),
                        onClick = { editing = "mtu" },
                    )
                    HairLine()
                    SwitchRow(
                        I18n.tr("Блокировать трафик без VPN"),
                        settings.killSwitch,
                        { value ->
                            app.saveSettings { it.killSwitch = value }
                            app.reloadService()
                        },
                        icon = NiIcons.Lock,
                        subtitle = I18n.tr("Если туннель упадёт, интернет пропадёт вместо утечки. Включается в настройках Android."),
                    )
                }
            }

            NimbusCard {
                Column {
                    SwitchRow(
                        I18n.tr("Мультиплексирование"),
                        settings.mux,
                        { value ->
                            app.saveSettings { it.mux = value }
                            app.reloadService()
                        },
                        icon = NiIcons.Bolt,
                        subtitle = I18n.tr("Несколько соединений в одном — быстрее на плохой связи, но не все серверы это умеют"),
                    )
                    if (settings.mux) {
                        HairLine()
                        SettingsRow(
                            title = I18n.tr("Потоков в соединении"),
                            subtitle = I18n.tr("Сколько соединений уместить в одно"),
                            value = settings.muxConcurrency.toString(),
                            onClick = { editing = "mux" },
                        )
                    }
                    HairLine()
                    SwitchRow(
                        I18n.tr("Фрагментация"),
                        settings.fragment,
                        { value ->
                            app.saveSettings { it.fragment = value }
                            app.reloadService()
                        },
                        icon = NiIcons.Tune,
                        subtitle = I18n.tr("Дробить пакеты, чтобы обойти блокировку по TLS-отпечатку"),
                    )
                    if (settings.fragment) {
                        HairLine()
                        SettingsRow(
                            title = I18n.tr("Параметры фрагментации"),
                            subtitle = "${settings.fragmentPackets} · ${settings.fragmentLength} · ${settings.fragmentInterval}",
                            onClick = { editing = "fragment" },
                        )
                    }
                }
            }

            NimbusCard {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Caption(I18n.tr("Уровень журнала"))
                    Segmented(
                        options = listOf(
                            "none" to I18n.tr("Выкл"),
                            "error" to I18n.tr("Ошибки"),
                            "warning" to I18n.tr("Предупр."),
                            "info" to I18n.tr("Инфо"),
                            "debug" to "Debug",
                        ),
                        selected = settings.logLevel,
                        onSelect = { value ->
                            app.saveSettings { it.logLevel = value }
                            app.reloadService()
                        },
                    )
                }
            }

            NimbusCard {
                Column {
                    SettingsRow(
                        title = I18n.tr("Адрес для проверки"),
                        subtitle = settings.pingUrl,
                        icon = NiIcons.Timer,
                        onClick = { editing = "pingUrl" },
                    )
                    HairLine()
                    SettingsRow(
                        title = I18n.tr("Таймаут проверки"),
                        subtitle = I18n.tr("Сколько секунд ждать ответа"),
                        value = "${settings.pingTimeout} с",
                        onClick = { editing = "pingTimeout" },
                    )
                }
            }
        }
    }

    editing.takeIf { it.isNotEmpty() }?.let { which ->
        when (which) {
            "mtu" -> TextInputDialog(
                title = "MTU",
                initial = settings.tunMtu.toString(),
                hint = "1500",
                onDismiss = { editing = "" },
                onConfirm = { value ->
                    value.toIntOrNull()?.takeIf { it in 576..9000 }?.let { mtu ->
                        app.saveSettings { it.tunMtu = mtu }
                        app.reloadService()
                    } ?: app.say(I18n.tr("MTU должен быть числом от 576 до 9000"), error = true)
                    editing = ""
                },
            )

            "mux" -> TextInputDialog(
                title = I18n.tr("Потоков в соединении"),
                initial = settings.muxConcurrency.toString(),
                onDismiss = { editing = "" },
                onConfirm = { value ->
                    value.toIntOrNull()?.coerceIn(1, 64)?.let { count ->
                        app.saveSettings { it.muxConcurrency = count }
                        app.reloadService()
                    }
                    editing = ""
                },
            )

            "fragment" -> FragmentDialog(app) { editing = "" }
            "pingUrl" -> TextInputDialog(
                title = I18n.tr("Адрес для проверки"),
                initial = settings.pingUrl,
                hint = "https://www.gstatic.com/generate_204",
                onDismiss = { editing = "" },
                onConfirm = { value ->
                    if (value.isNotEmpty()) app.saveSettings { it.pingUrl = value }
                    editing = ""
                },
            )

            "pingTimeout" -> TextInputDialog(
                title = I18n.tr("Таймаут проверки"),
                initial = settings.pingTimeout.toString(),
                hint = "5",
                onDismiss = { editing = "" },
                onConfirm = { value ->
                    value.toIntOrNull()?.coerceIn(1, 30)?.let { seconds ->
                        app.saveSettings { it.pingTimeout = seconds }
                    }
                    editing = ""
                },
            )
        }
    }
}

@Composable
private fun FragmentDialog(app: AppState, onDismiss: () -> Unit) {
    val settings = app.settings
    var packets by remember { mutableStateOf(settings.fragmentPackets) }
    var length by remember { mutableStateOf(settings.fragmentLength) }
    var interval by remember { mutableStateOf(settings.fragmentInterval) }
    val colors = NimbusTheme.colors

    androidx.compose.material3.AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = colors.surface,
        title = { Text(I18n.tr("Фрагментация"), color = colors.text) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Caption(I18n.tr("Пакеты"))
                NimbusField(value = packets, onValueChange = { packets = it }, hint = "tlshello")
                Caption(I18n.tr("Длина"))
                NimbusField(value = length, onValueChange = { length = it }, hint = "100-200")
                Caption(I18n.tr("Интервал, мс"))
                NimbusField(value = interval, onValueChange = { interval = it }, hint = "10-20")
            }
        },
        confirmButton = {
            androidx.compose.material3.TextButton(onClick = {
                app.saveSettings {
                    it.fragmentPackets = packets.trim()
                    it.fragmentLength = length.trim()
                    it.fragmentInterval = interval.trim()
                }
                app.reloadService()
                onDismiss()
            }) { Text(I18n.tr("Сохранить"), color = colors.text) }
        },
        dismissButton = {
            androidx.compose.material3.TextButton(onClick = onDismiss) {
                Text(I18n.tr("Отмена"), color = colors.muted)
            }
        },
    )
}

/** Subscriptions: how Nimbus asks a panel for its servers. */
@Composable
fun SubscriptionsScreen(app: AppState, onBack: () -> Unit) {
    val colors = NimbusTheme.colors
    val settings = app.settings
    var editing by remember { mutableStateOf("") }

    ScreenScaffold(title = I18n.tr("Подписки"), onBack = onBack) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            NimbusCard {
                Column {
                    SettingsRow(
                        title = I18n.tr("User-Agent"),
                        subtitle = settings.subUserAgent,
                        icon = NiIcons.Edit,
                        onClick = { editing = "ua" },
                    )
                    HairLine()
                    Text(
                        I18n.tr("Многие панели отдают ссылки только знакомому клиенту — поэтому по умолчанию стоит Happ."),
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.muted,
                        modifier = Modifier.padding(horizontal = 16.dp, vertical = 10.dp),
                    )
                }
            }

            NimbusCard {
                Column {
                    SwitchRow(
                        I18n.tr("Автообновление"),
                        settings.subAutoUpdate,
                        { value -> app.saveSettings { it.subAutoUpdate = value } },
                        icon = NiIcons.Refresh,
                        subtitle = I18n.tr("Обновлять подписки, когда пришло их время"),
                    )
                    HairLine()
                    SettingsRow(
                        title = I18n.tr("Интервал"),
                        subtitle = I18n.tr("Если панель не прислала свой"),
                        value = "${settings.subUpdateHours} ч",
                        onClick = { editing = "hours" },
                    )
                    HairLine()
                    SwitchRow(
                        I18n.tr("Обновлять при запуске"),
                        settings.updateOnLaunch,
                        { value -> app.saveSettings { it.updateOnLaunch = value } },
                        icon = NiIcons.Play,
                        subtitle = I18n.tr("Проверить просроченные подписки, когда приложение открыли"),
                    )
                }
            }

            NimbusCard {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Caption(I18n.tr("HWID — идентификация устройства"))
                    Segmented(
                        options = listOf(
                            "auto" to I18n.tr("Авто"),
                            "always" to I18n.tr("Всегда"),
                            "never" to I18n.tr("Никогда"),
                        ),
                        selected = settings.hwidMode,
                        onSelect = { value -> app.saveSettings { it.hwidMode = value } },
                        tips = mapOf(
                            "auto" to I18n.tr("Отправить, только если без него пришли пустые серверы"),
                            "always" to I18n.tr("Отправлять сразу — так делают мобильные клиенты"),
                            "never" to I18n.tr("Не отправлять вовсе"),
                        ),
                    )
                }
            }

            NimbusCard {
                Column {
                    SettingsRow(
                        title = I18n.tr("Свой HWID"),
                        subtitle = settings.hwidCustom.ifEmpty { I18n.tr("из адреса устройства") },
                        icon = NiIcons.Lock,
                        onClick = { editing = "hwid" },
                    )
                    HairLine()
                    SettingsRow(
                        title = I18n.tr("Имя устройства"),
                        subtitle = settings.hwidDevice.ifEmpty { com.zalchik.nimbus.engine.Device.name },
                        icon = NiIcons.Info,
                        onClick = { editing = "device" },
                    )
                }
            }

            if (app.store.subscriptions.isNotEmpty()) {
                Caption(I18n.tr("Ваши подписки"))
                NimbusCard {
                    Column {
                        app.store.subscriptions.forEachIndexed { index, subscription ->
                            if (index > 0) HairLine()
                            Row(
                                Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 12.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Column(Modifier.weight(1f)) {
                                    Text(subscription.title, style = MaterialTheme.typography.bodyLarge, color = colors.text)
                                    Text(
                                        if (subscription.error.isNotEmpty()) subscription.error
                                        else I18n.plural(
                                            app.store.serversOf(subscription.id).size,
                                            "{n} сервер", "{n} сервера", "{n} серверов",
                                        ).replace("{n}", app.store.serversOf(subscription.id).size.toString()),
                                        style = MaterialTheme.typography.bodySmall,
                                        color = if (subscription.error.isNotEmpty()) colors.error else colors.muted,
                                    )
                                }
                                Spacer(Modifier.width(8.dp))
                                Chip(I18n.tr("Обновить"), NiIcons.Refresh) { app.updateSubscription(subscription.id) }
                            }
                        }
                    }
                }
            }
        }
    }

    editing.takeIf { it.isNotEmpty() }?.let { which ->
        TextInputDialog(
            title = when (which) {
                "ua" -> I18n.tr("User-Agent")
                "hours" -> I18n.tr("Интервал, часов")
                "hwid" -> I18n.tr("Свой HWID")
                else -> I18n.tr("Имя устройства")
            },
            initial = when (which) {
                "ua" -> settings.subUserAgent
                "hours" -> settings.subUpdateHours.toString()
                "hwid" -> settings.hwidCustom
                else -> settings.hwidDevice
            },
            multiline = which == "ua",
            onDismiss = { editing = "" },
            onConfirm = { value ->
                when (which) {
                    "ua" -> app.saveSettings { it.subUserAgent = value }
                    "hours" -> value.toIntOrNull()?.coerceIn(0, 720)?.let { hours ->
                        app.saveSettings { it.subUpdateHours = hours }
                    }

                    "hwid" -> app.saveSettings { it.hwidCustom = value }
                    else -> app.saveSettings { it.hwidDevice = value }
                }
                editing = ""
            },
        )
    }
}
