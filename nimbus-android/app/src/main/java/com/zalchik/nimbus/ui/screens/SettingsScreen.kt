package com.zalchik.nimbus.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.zalchik.nimbus.data.Paths
import com.zalchik.nimbus.engine.Update
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.NiIcons
import com.zalchik.nimbus.ui.components.Caption
import com.zalchik.nimbus.ui.components.NimbusCard
import com.zalchik.nimbus.ui.components.ReadOnlySheet
import com.zalchik.nimbus.ui.components.Segmented
import com.zalchik.nimbus.ui.components.SettingsRow
import com.zalchik.nimbus.ui.components.SwitchRow
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.theme.NimbusTheme

/** Where the settings list can take you. */
enum class SettingsPage { None, Routing, Dns, Advanced, Subscriptions, Balancers, Logs }

/**
 * The settings tab: the desktop's settings dialog as a list, grouped the same way — connection,
 * routing, DNS, subscriptions, appearance, and the about block with the update check.
 */
@Composable
fun SettingsScreen(
    app: AppState,
    page: SettingsPage,
    onOpen: (SettingsPage) -> Unit,
    onUpdate: () -> Unit,
    onShareLogs: () -> Unit,
) {
    val colors = NimbusTheme.colors
    val settings = app.settings
    val context = LocalContext.current
    var about by remember { mutableStateOf(false) }

    Column(
        Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp)
            .padding(top = 12.dp, bottom = 96.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        Text(I18n.tr("Настройки"), style = MaterialTheme.typography.titleMedium, color = colors.text)

        NimbusCard {
            Column {
                SettingsRow(
                    title = I18n.tr("Маршрутизация"),
                    subtitle = routingSummary(settings),
                    icon = NiIcons.Shield,
                    onClick = { onOpen(SettingsPage.Routing) },
                )
                HairLine()
                SettingsRow(
                    title = "DNS",
                    subtitle = I18n.tr("Серверы, подмена адресов, определение домена"),
                    icon = NiIcons.Dns,
                    onClick = { onOpen(SettingsPage.Dns) },
                )
                HairLine()
                SettingsRow(
                    title = I18n.tr("Туннель"),
                    subtitle = I18n.tr("Режим, MTU, always-on, мультиплексирование, фрагментация"),
                    icon = NiIcons.Tune,
                    onClick = { onOpen(SettingsPage.Advanced) },
                )
                HairLine()
                SettingsRow(
                    title = I18n.tr("Балансировщики"),
                    subtitle = if (app.store.balancers.isEmpty()) I18n.tr("Нет")
                    else I18n.plural(app.store.balancers.size, "{n} балансировщик", "{n} балансировщика", "{n} балансировщиков")
                        .replace("{n}", app.store.balancers.size.toString()),
                    icon = NiIcons.Bolt,
                    onClick = { onOpen(SettingsPage.Balancers) },
                )
            }
        }

        NimbusCard {
            Column {
                SettingsRow(
                    title = I18n.tr("Подписки"),
                    subtitle = I18n.tr("User-Agent, автообновление, HWID"),
                    icon = NiIcons.Cloud,
                    value = app.store.subscriptions.size.toString(),
                    onClick = { onOpen(SettingsPage.Subscriptions) },
                )
                HairLine()
                SettingsRow(
                    title = I18n.tr("Обновить подписки"),
                    subtitle = I18n.tr("Скачать серверы заново"),
                    icon = NiIcons.Refresh,
                    onClick = { app.updateSubscriptions() },
                )
            }
        }

        NimbusCard {
            Column {
                SwitchRow(
                    I18n.tr("Подключаться при запуске"),
                    settings.autoConnect,
                    { value -> app.saveSettings { it.autoConnect = value } },
                    icon = NiIcons.Play,
                    subtitle = I18n.tr("Запускать туннель с выбранным сервером"),
                )
                HairLine()
                SwitchRow(
                    I18n.tr("Запускать после перезагрузки"),
                    settings.autostart,
                    { value -> app.saveSettings { it.autostart = value } },
                    icon = NiIcons.Bolt,
                    subtitle = I18n.tr("Нужно разрешение на автозапуск у производителя"),
                )
                HairLine()
                SwitchRow(
                    I18n.tr("Уведомлять о смене узла"),
                    settings.balancerNotify,
                    { value -> app.saveSettings { it.balancerNotify = value } },
                    icon = NiIcons.Info,
                    subtitle = I18n.tr("Балансировщик переключился — показать, куда"),
                )
            }
        }

        NimbusCard {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Caption(I18n.tr("Оформление"))
                Segmented(
                    options = listOf(
                        "dark" to I18n.tr("Тёмная"),
                        "light" to I18n.tr("Светлая"),
                        "auto" to I18n.tr("Системная"),
                    ),
                    selected = settings.theme,
                    onSelect = { value -> app.saveSettings { it.theme = value } },
                )
                Caption(I18n.tr("Язык"))
                Segmented(
                    options = listOf(
                        "auto" to I18n.tr("Системный"),
                        "ru" to "Русский",
                        "en" to "English",
                    ),
                    selected = settings.language,
                    onSelect = { value -> app.saveSettings { it.language = value } },
                )
            }
        }

        NimbusCard {
            Column {
                SettingsRow(
                    title = I18n.tr("Журнал"),
                    subtitle = I18n.tr("Строки sing-box и ошибки подключения"),
                    icon = NiIcons.Logs,
                    onClick = { onOpen(SettingsPage.Logs) },
                )
                HairLine()
                SettingsRow(
                    title = I18n.tr("Диагностика"),
                    subtitle = I18n.tr("Конфиг, который сейчас собирается"),
                    icon = NiIcons.File,
                    onClick = { about = true },
                )
                HairLine()
                SettingsRow(
                    title = I18n.tr("Отправить журнал"),
                    subtitle = I18n.tr("Файлом в другое приложение"),
                    icon = NiIcons.Share,
                    onClick = onShareLogs,
                )
                HairLine()
                val release = app.update
                SettingsRow(
                    title = if (release != null) I18n.tr("Обновить до {version}", values = mapOf("version" to release.version))
                    else I18n.tr("Проверить обновления"),
                    subtitle = when {
                        release != null -> release.body.lineSequence().firstOrNull { it.isNotBlank() } ?: ""
                        app.updateError.isNotEmpty() -> app.updateError
                        else -> I18n.tr("Nimbus {version}", values = mapOf("version" to Paths.VERSION))
                    },
                    icon = if (release != null) NiIcons.Download else NiIcons.Refresh,
                    value = if (release != null) I18n.tr("Есть") else "",
                    onClick = {
                        if (release != null) onUpdate() else app.checkUpdate(manual = true)
                    },
                )
                HairLine()
                SettingsRow(
                    title = I18n.tr("О программе"),
                    subtitle = "nimbus · sing-box ${com.zalchik.nimbus.BuildConfig.SINGBOX_VERSION}",
                    icon = NiIcons.Info,
                    onClick = { about = true },
                )
            }
        }

        Text(
            I18n.tr("Настройки и профили лежат в папке приложения и совпадают по формату с настольной версией."),
            style = MaterialTheme.typography.bodySmall,
            color = colors.faint,
            modifier = Modifier.padding(horizontal = 4.dp),
        )
    }

    if (about) {
        AboutSheet(app) { about = false }
    }
}

private fun routingSummary(settings: com.zalchik.nimbus.data.Settings): String {
    val parts = mutableListOf(
        if (settings.routingMode == "bypass_ru") I18n.tr("Умная") else I18n.tr("Весь трафик")
    )
    settings.activePreset?.let { parts += it.name }
    if (settings.perAppEnabled) parts += I18n.tr("фильтр приложений")
    if (!settings.useProviderRouting) parts += I18n.tr("без правил провайдера")
    return parts.joinToString(" · ")
}

/** Versions, the config that would run, and the licenses — one sheet. */
@Composable
private fun AboutSheet(app: AppState, onDismiss: () -> Unit) {
    val config = remember { app.currentConfig() }
    val versions = remember { app.versions() }
    ReadOnlySheet(
        title = I18n.tr("О программе"),
        text = buildString {
            versions.forEach { (key, value) -> appendLine("$key: $value") }
            appendLine()
            appendLine(I18n.tr("Текущий конфиг"))
            append(config.ifEmpty { I18n.tr("Сервер не выбран") })
        },
        onDismiss = onDismiss,
    )
}

/** What a release row shows before the user taps it. */
@Composable
fun UpdateRow(release: Update.Release?, onCheck: () -> Unit, onUpdate: () -> Unit) {
    val colors = NimbusTheme.colors
    NimbusCard(onClick = if (release != null) onUpdate else onCheck) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(
                if (release != null) NiIcons.Download else NiIcons.Refresh,
                null,
                tint = colors.text2,
                modifier = Modifier.size(19.dp),
            )
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    if (release != null) release.version else I18n.tr("Проверить обновления"),
                    style = MaterialTheme.typography.bodyLarge,
                    color = colors.text,
                )
                Text(
                    if (release != null) I18n.tr("Новая версия доступна") else Paths.VERSION,
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.muted,
                )
            }
        }
    }
}
