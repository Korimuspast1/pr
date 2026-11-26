package com.zalchik.nimbus.ui.components

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
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
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.NiIcons
import com.zalchik.nimbus.ui.theme.NimbusTheme

/**
 * The small dialogs and sheets the screens open. They all follow the desktop's: one text field
 * where one is needed, plain buttons, and a destructive action that says what it destroys.
 */

/** A one-line prompt with the current value pre-filled — rename, a URL, a user agent. */
@Composable
fun TextInputDialog(
    title: String,
    initial: String = "",
    hint: String = "",
    multiline: Boolean = false,
    confirmLabel: String = I18n.tr("Готово"),
    onConfirm: (String) -> Unit,
    onDismiss: () -> Unit,
    extra: (@Composable () -> Unit)? = null,
) {
    val colors = NimbusTheme.colors
    var value by remember(initial) { mutableStateOf(initial) }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = colors.surface,
        titleContentColor = colors.text,
        textContentColor = colors.text2,
        title = { Text(title, style = MaterialTheme.typography.titleMedium) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                NimbusField(
                    value = value,
                    onValueChange = { value = it },
                    hint = hint,
                    singleLine = !multiline,
                    minHeight = if (multiline) 120.dp else 48.dp,
                )
                extra?.invoke()
            }
        },
        confirmButton = {
            TextButton(onClick = { onConfirm(value.trim()) }) {
                Text(confirmLabel, color = colors.text, fontWeight = FontWeight.SemiBold)
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text(I18n.tr("Отмена"), color = colors.muted) }
        },
    )
}

/** "Удалить подписку?" with the name spelled out, so nothing is deleted by an accidental tap. */
@Composable
fun ConfirmDialog(
    title: String,
    message: String = "",
    confirmLabel: String = I18n.tr("Удалить"),
    destructive: Boolean = true,
    onConfirm: () -> Unit,
    onDismiss: () -> Unit,
) {
    val colors = NimbusTheme.colors
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = colors.surface,
        title = { Text(title, style = MaterialTheme.typography.titleMedium, color = colors.text) },
        text = {
            if (message.isNotEmpty()) {
                Text(message, style = MaterialTheme.typography.bodyMedium, color = colors.text2)
            }
        },
        confirmButton = {
            TextButton(onClick = onConfirm) {
                Text(
                    confirmLabel,
                    color = if (destructive) colors.error else colors.text,
                    fontWeight = FontWeight.SemiBold,
                )
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text(I18n.tr("Отмена"), color = colors.muted) }
        },
    )
}

/** The text field used inside dialogs and the import sheet. */
@Composable
fun NimbusField(
    value: String,
    onValueChange: (String) -> Unit,
    hint: String = "",
    singleLine: Boolean = true,
    minHeight: androidx.compose.ui.unit.Dp = 48.dp,
    modifier: Modifier = Modifier,
    trailing: (@Composable () -> Unit)? = null,
    leading: (@Composable () -> Unit)? = null,
) {
    val colors = NimbusTheme.colors
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        modifier = modifier.fillMaxWidth().heightIn(min = minHeight),
        singleLine = singleLine,
        placeholder = { Text(hint, color = colors.faint, style = MaterialTheme.typography.bodyMedium) },
        textStyle = MaterialTheme.typography.bodyMedium.copy(color = colors.text),
        shape = InputShape,
        colors = OutlinedTextFieldDefaults.colors(
            focusedTextColor = colors.text,
            unfocusedTextColor = colors.text,
            focusedBorderColor = colors.border3,
            unfocusedBorderColor = colors.border2,
            cursorColor = colors.text,
            focusedContainerColor = colors.surface2,
            unfocusedContainerColor = colors.surface2,
        ),
        leadingIcon = leading,
        trailingIcon = trailing,
    )
}

/** A bottom-sheet-like panel of actions, opened by a long press on a server or a subscription. */
@Composable
fun ActionSheet(
    title: String,
    actions: List<SheetAction>,
    onDismiss: () -> Unit,
) {
    val colors = NimbusTheme.colors
    androidx.compose.material3.ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = colors.panel,
        contentColor = colors.text,
        dragHandle = {
            Box(
                Modifier
                    .padding(top = 10.dp)
                    .size(width = 34.dp, height = 4.dp)
                    .clip(RoundedCornerShape(2.dp))
                    .background(colors.border3)
            )
        },
    ) {
        Text(
            title,
            style = MaterialTheme.typography.titleMedium,
            color = colors.text,
            maxLines = 2,
            modifier = Modifier.padding(horizontal = 20.dp, vertical = 6.dp),
        )
        Column(Modifier.verticalScroll(rememberScrollState()).padding(bottom = 24.dp)) {
            for (action in actions) {
                Row(
                    Modifier
                        .fillMaxWidth()
                        .clickable(enabled = action.enabled, onClick = {
                            onDismiss()
                            action.onClick()
                        })
                        .padding(horizontal = 20.dp, vertical = 13.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    if (action.icon != null) {
                        Icon(
                            action.icon,
                            null,
                            tint = when {
                                !action.enabled -> colors.faint
                                action.destructive -> colors.error
                                else -> colors.text2
                            },
                            modifier = Modifier.size(19.dp),
                        )
                        Spacer(Modifier.width(16.dp))
                    }
                    Column(Modifier.weight(1f)) {
                        Text(
                            action.label,
                            style = MaterialTheme.typography.bodyLarge,
                            color = when {
                                !action.enabled -> colors.faint
                                action.destructive -> colors.error
                                else -> colors.text
                            },
                        )
                        if (action.detail.isNotEmpty()) {
                            Text(
                                action.detail,
                                style = MaterialTheme.typography.bodySmall,
                                color = colors.muted,
                            )
                        }
                    }
                }
            }
        }
    }
}

data class SheetAction(
    val label: String,
    val onClick: () -> Unit,
    val icon: ImageVector? = null,
    val detail: String = "",
    val enabled: Boolean = true,
    val destructive: Boolean = false,
)

/** A QR code with its text and a copy button — sharing a link or a subscription to another device. */
@Composable
fun QrSheet(text: String, title: String, onDismiss: () -> Unit) {
    val colors = NimbusTheme.colors
    val context = LocalContext.current
    androidx.compose.material3.ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = colors.panel,
        contentColor = colors.text,
    ) {
        Column(
            Modifier.fillMaxWidth().padding(horizontal = 24.dp).padding(bottom = 28.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            Text(title, style = MaterialTheme.typography.titleMedium, color = colors.text)
            QrImage(text, size = 240)
            Text(
                text,
                style = MaterialTheme.typography.bodySmall,
                color = colors.muted,
                maxLines = 3,
            )
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Chip(I18n.tr("Скопировать"), NiIcons.Copy) {
                    copyToClipboard(context, text)
                }
                Chip(I18n.tr("Поделиться"), NiIcons.Share) { shareText(context, text) }
            }
        }
    }
}

@Composable
fun Chip(label: String, icon: ImageVector? = null, onClick: () -> Unit) {
    val colors = NimbusTheme.colors
    Row(
        Modifier
            .clip(PillShape)
            .background(colors.surface)
            .border(1.dp, colors.border2, PillShape)
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 9.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        if (icon != null) Icon(icon, null, tint = colors.text2, modifier = Modifier.size(16.dp))
        Text(label, style = MaterialTheme.typography.labelLarge, color = colors.text)
    }
}

/** The text a row shows for its own content — long, so it can be copied out of the sheet. */
@Composable
fun ReadOnlySheet(title: String, text: String, onDismiss: () -> Unit, mono: Boolean = true) {
    val colors = NimbusTheme.colors
    val context = LocalContext.current
    androidx.compose.material3.ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = colors.panel,
        contentColor = colors.text,
    ) {
        Column(Modifier.fillMaxWidth().padding(horizontal = 20.dp).padding(bottom = 24.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    title,
                    style = MaterialTheme.typography.titleMedium,
                    color = colors.text,
                    modifier = Modifier.weight(1f),
                )
                Text(
                    I18n.tr("Скопировать"),
                    style = MaterialTheme.typography.labelLarge,
                    color = colors.text2,
                    modifier = Modifier
                        .clip(PillShape)
                        .clickable { copyToClipboard(context, text) }
                        .padding(horizontal = 12.dp, vertical = 6.dp),
                )
            }
            Spacer(Modifier.height(10.dp))
            Text(
                text,
                style = if (mono) {
                    com.zalchik.nimbus.ui.theme.MonoStyle.copy(fontSize = 11.5.sp)
                } else {
                    MaterialTheme.typography.bodyMedium
                },
                color = colors.text2,
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(InputShape)
                    .background(colors.surface)
                    .border(1.dp, colors.border, InputShape)
                    .padding(12.dp)
                    .verticalScroll(rememberScrollState())
                    .heightIn(max = 420.dp),
            )
        }
    }
}

/** Pick a group (or "My servers") to move a server into, or a group for imported links. */
@Composable
fun GroupPickerSheet(
    groups: List<Pair<String, String>>,
    current: String = "",
    onPick: (String) -> Unit,
    onDismiss: () -> Unit,
    onCreate: (String) -> Unit,
    title: String = I18n.tr("Группа"),
) {
    val colors = NimbusTheme.colors
    var creating by remember { mutableStateOf(false) }
    var name by remember { mutableStateOf("") }
    androidx.compose.material3.ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = colors.panel,
        contentColor = colors.text,
    ) {
        Column(Modifier.fillMaxWidth().padding(horizontal = 20.dp).padding(bottom = 24.dp)) {
            Text(title, style = MaterialTheme.typography.titleMedium, color = colors.text)
            Spacer(Modifier.height(12.dp))
            if (creating) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    NimbusField(
                        value = name,
                        onValueChange = { name = it },
                        hint = I18n.tr("Название группы"),
                        modifier = Modifier.weight(1f),
                    )
                    Spacer(Modifier.width(10.dp))
                    Text(
                        I18n.tr("Создать"),
                        style = MaterialTheme.typography.labelLarge,
                        color = colors.text,
                        modifier = Modifier
                            .clip(PillShape)
                            .background(colors.surface2)
                            .clickable {
                                val value = name.trim()
                                if (value.isNotEmpty()) {
                                    onCreate(value)
                                    creating = false
                                    name = ""
                                }
                            }
                            .padding(horizontal = 14.dp, vertical = 12.dp),
                    )
                }
            } else {
                Column(Modifier.verticalScroll(rememberScrollState()).heightIn(max = 400.dp)) {
                    Row(
                        Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(9.dp))
                            .background(if (current.isEmpty()) colors.surface2 else Color.Transparent)
                            .clickable { onPick("") }
                            .padding(horizontal = 12.dp, vertical = 12.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Icon(NiIcons.CloudOff, null, tint = colors.muted, modifier = Modifier.size(17.dp))
                        Spacer(Modifier.width(12.dp))
                        Text(I18n.tr("Мои серверы"), style = MaterialTheme.typography.bodyLarge, color = colors.text)
                    }
                    for ((id, label) in groups) {
                        Row(
                            Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(9.dp))
                                .background(if (id == current) colors.surface2 else Color.Transparent)
                                .clickable { onPick(id) }
                                .padding(horizontal = 12.dp, vertical = 12.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Icon(
                                if (label.startsWith("•")) NiIcons.Cloud else NiIcons.List,
                                null,
                                tint = colors.muted,
                                modifier = Modifier.size(17.dp),
                            )
                            Spacer(Modifier.width(12.dp))
                            Text(
                                label.removePrefix("• "),
                                style = MaterialTheme.typography.bodyLarge,
                                color = colors.text,
                                modifier = Modifier.weight(1f),
                            )
                        }
                    }
                    Spacer(Modifier.height(8.dp))
                    Chip(I18n.tr("Новая группа"), NiIcons.Add) { creating = true }
                }
            }
        }
    }
}

fun copyToClipboard(context: Context, text: String, label: String = "nimbus") {
    val manager = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
    manager.setPrimaryClip(ClipData.newPlainText(label, text))
}

fun shareText(context: Context, text: String, title: String = "nimbus") {
    val intent = Intent(Intent.ACTION_SEND).apply {
        type = "text/plain"
        putExtra(Intent.EXTRA_TEXT, text)
        putExtra(Intent.EXTRA_SUBJECT, title)
    }
    context.startActivity(Intent.createChooser(intent, title))
}

/** A file the user can open elsewhere — a config export or the log. */
fun shareFile(context: Context, file: java.io.File, mime: String = "application/json") {
    val uri = androidx.core.content.FileProvider.getUriForFile(
        context,
        "${context.packageName}.files",
        file,
    )
    val intent = Intent(Intent.ACTION_SEND).apply {
        type = mime
        putExtra(Intent.EXTRA_STREAM, uri)
        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
    }
    context.startActivity(Intent.createChooser(intent, file.name))
}
