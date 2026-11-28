package com.zalchik.nimbus.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
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
import com.zalchik.nimbus.ui.components.GroupPickerSheet
import com.zalchik.nimbus.ui.components.NimbusCard
import com.zalchik.nimbus.ui.components.NimbusField
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.theme.NimbusTheme

/**
 * Everything that adds servers: paste links or a config, scan a QR code, open a file, or add a
 * subscription URL. One sheet, because on a phone they are the same gesture.
 *
 * [clipboard] is what the host activity read from the clipboard, so the paste button can offer it
 * before the user types anything.
 */
@Composable
fun ImportSheet(
    app: AppState,
    clipboard: String = "",
    onScanQr: () -> Unit,
    onOpenFile: () -> Unit,
    onDismiss: () -> Unit,
) {
    val colors = NimbusTheme.colors
    var text by remember { mutableStateOf("") }
    var url by remember { mutableStateOf("") }
    var group by remember { mutableStateOf("") }
    var pickingGroup by remember { mutableStateOf(false) }
    var addingUrl by remember { mutableStateOf(false) }

    val target = when {
        group.isEmpty() -> I18n.tr("Мои серверы")
        else -> app.store.subscription(group)?.title ?: app.store.group(group)?.name ?: group
    }

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
            Text(I18n.tr("Добавить серверы"), style = MaterialTheme.typography.titleMedium, color = colors.text)

            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Chip(I18n.tr("QR-код"), NiIcons.Scan, onClick = onScanQr)
                Chip(I18n.tr("Файл"), NiIcons.File, onClick = onOpenFile)
                Chip(I18n.tr("Подписка"), NiIcons.Cloud, onClick = { addingUrl = !addingUrl })
                if (clipboard.isNotBlank()) {
                    Chip(I18n.tr("Вставить"), NiIcons.Copy) { text = clipboard.trim() }
                }
            }

            if (addingUrl) {
                NimbusCard {
                    Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        Caption(I18n.tr("Адрес подписки"))
                        NimbusField(
                            value = url,
                            onValueChange = { url = it },
                            hint = "https://panel.example.com/sub/…",
                        )
                        Text(
                            I18n.tr("Ссылка из панели: Nimbus скачает серверы и будет обновлять их сам."),
                            style = MaterialTheme.typography.bodySmall,
                            color = colors.muted,
                        )
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Chip(I18n.tr("Добавить"), NiIcons.Check) {
                                val value = url.trim()
                                if (value.isNotEmpty()) {
                                    app.addSubscription(value)
                                    url = ""
                                    addingUrl = false
                                    onDismiss()
                                }
                            }
                            Chip(I18n.tr("Отмена"), NiIcons.Close) { addingUrl = false }
                        }
                    }
                }
            }

            Column {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Caption(I18n.tr("Ссылки или конфиг"), Modifier.weight(1f))
                    Chip(target, NiIcons.List) { pickingGroup = true }
                }
                Spacer(Modifier.height(8.dp))
                NimbusField(
                    value = text,
                    onValueChange = { text = it },
                    hint = I18n.tr("vless://…, vmess://…, несколько ссылок по строке, JSON или nimbus://…"),
                    singleLine = false,
                    minHeight = 150.dp,
                )
                Text(
                    I18n.tr("Принимаются ссылки Happ (включая зашифрованные), v2rayN/v2rayNG, clash, sing-box и обычный JSON Xray."),
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.faint,
                )
            }

            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Chip(I18n.tr("Добавить"), NiIcons.Add) {
                    val value = text.trim()
                    if (value.isNotEmpty()) {
                        app.import(value, group) { added -> if (added > 0) onDismiss() }
                        text = ""
                    }
                }
                Chip(I18n.tr("Закрыть"), NiIcons.Close, onClick = onDismiss)
            }
        }
    }

    if (pickingGroup) {
        GroupPickerSheet(
            groups = app.store.subscriptions.map { it.id to it.title } +
                app.store.groups.map { it.id to it.name },
            current = group,
            title = I18n.tr("Куда добавить"),
            onDismiss = { pickingGroup = false },
            onPick = {
                group = it
                pickingGroup = false
            },
            onCreate = { name ->
                group = app.addGroup(name).id
                pickingGroup = false
            },
        )
    }
}
