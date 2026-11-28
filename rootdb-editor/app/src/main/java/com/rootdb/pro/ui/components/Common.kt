package com.rootdb.pro.ui.components

import android.graphics.Bitmap
import androidx.compose.foundation.Image
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
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
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.core.graphics.drawable.toBitmap
import com.rootdb.pro.data.CryptoTools

/** Заголовок секции */
@Composable
fun SectionTitle(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = MaterialTheme.typography.titleMedium,
        fontWeight = FontWeight.Bold,
        color = MaterialTheme.colorScheme.primary,
        modifier = modifier.padding(horizontal = 20.dp, vertical = 8.dp)
    )
}

/** Поле поиска */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SearchField(query: String, onQuery: (String) -> Unit, placeholder: String, modifier: Modifier = Modifier) {
    OutlinedTextField(
        value = query,
        onValueChange = onQuery,
        placeholder = { Text(placeholder) },
        leadingIcon = { Icon(Icons.Filled.Search, contentDescription = null) },
        singleLine = true,
        shape = MaterialTheme.shapes.extraLarge,
        modifier = modifier.fillMaxWidth()
    )
}

/** Маленький бейдж */
@Composable
fun Badge(text: String, container: Color, content: Color, modifier: Modifier = Modifier) {
    Surface(
        color = container,
        shape = MaterialTheme.shapes.small,
        modifier = modifier
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.labelSmall,
            color = content,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp)
        )
    }
}

@Composable
fun LoadingBox(text: String = "Загрузка…") {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            CircularProgressIndicator()
            Spacer(Modifier.height(12.dp))
            Text(text, style = MaterialTheme.typography.bodyMedium)
        }
    }
}

@Composable
fun EmptyBox(text: String) {
    Box(Modifier.fillMaxSize().padding(32.dp), contentAlignment = Alignment.Center) {
        Text(text, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
fun ErrorBox(message: String, onRetry: (() -> Unit)? = null) {
    Box(Modifier.fillMaxSize().padding(32.dp), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Icon(Icons.Filled.Lock, contentDescription = null, tint = MaterialTheme.colorScheme.error, modifier = Modifier.size(48.dp))
            Spacer(Modifier.height(12.dp))
            Text(message, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.error)
            if (onRetry != null) {
                Spacer(Modifier.height(12.dp))
                Button(onClick = onRetry) { Text("Повторить") }
            }
        }
    }
}

/** Иконка приложения из Drawable */
@Composable
fun AppIcon(drawable: android.graphics.drawable.Drawable?, size: Int = 48) {
    val bitmap: Bitmap? = remember(drawable) {
        try {
            drawable?.toBitmap(size * 3, size * 3)
        } catch (_: Exception) {
            null
        }
    }
    if (bitmap != null) {
        Image(
            bitmap = bitmap.asImageBitmap(),
            contentDescription = null,
            modifier = Modifier.size(size.dp).clip(MaterialTheme.shapes.medium)
        )
    } else {
        Surface(color = MaterialTheme.colorScheme.primaryContainer, shape = MaterialTheme.shapes.medium) {
            Box(Modifier.size(size.dp), contentAlignment = Alignment.Center) {
                Icon(Icons.Filled.Lock, contentDescription = null, tint = MaterialTheme.colorScheme.onPrimaryContainer)
            }
        }
    }
}

/** Диалог подтверждения */
@Composable
fun ConfirmDialog(
    title: String,
    text: String,
    confirmText: String = "Да",
    onConfirm: () -> Unit,
    onDismiss: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = { Text(text) },
        confirmButton = { Button(onClick = onConfirm) { Text(confirmText) } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Отмена") } }
    )
}

/** Отображение значения ячейки */
fun displayValue(v: Any?): String = when (v) {
    null -> "NULL"
    is ByteArray -> "BLOB (${v.size} Б): " + v.take(48).joinToString("") { "%02x".format(it) }
    else -> v.toString()
}

/**
 * Нижний лист с расшифровкой значения: Base64/Hex/URL/JSON + AES + копирование.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ValueSheet(value: Any?, onDismiss: () -> Unit) {
    val raw = displayValue(value)
    val clipboard = LocalClipboardManager.current
    var aesKey by remember { mutableStateOf("") }
    var aesIv by remember { mutableStateOf("") }
    var aesMode by remember { mutableStateOf("CBC") }
    var aesResult by remember { mutableStateOf<String?>(null) }
    var aesError by remember { mutableStateOf<String?>(null) }

    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(
            Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text("Значение", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
            Surface(color = MaterialTheme.colorScheme.surfaceContainerHigh, shape = MaterialTheme.shapes.medium) {
                Text(
                    raw,
                    fontFamily = FontFamily.Monospace,
                    style = MaterialTheme.typography.bodySmall,
                    modifier = Modifier.padding(12.dp)
                )
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                AssistChip(onClick = { clipboard.setText(AnnotatedString(raw)) }, label = { Text("Копировать") }, leadingIcon = { Icon(Icons.Filled.ContentCopy, null) })
            }

            val decoded = remember(raw) { CryptoTools.decodeValue(raw) }
            if (decoded.isNotEmpty()) {
                Text("Расшифровка", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                decoded.forEach { d ->
                    Column {
                        Text(d.kind, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
                        Surface(color = MaterialTheme.colorScheme.secondaryContainer, shape = MaterialTheme.shapes.medium) {
                            Text(
                                d.text,
                                fontFamily = FontFamily.Monospace,
                                style = MaterialTheme.typography.bodySmall,
                                modifier = Modifier.padding(12.dp)
                            )
                        }
                        Spacer(Modifier.height(4.dp))
                    }
                }
            }

            Text("AES-расшифровка (если поле зашифровано)", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            OutlinedTextField(aesKey, { aesKey = it }, label = { Text("Ключ (hex или пароль)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(aesIv, { aesIv = it }, label = { Text("IV hex (для CBC)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                listOf("CBC", "ECB").forEach { m ->
                    AssistChip(
                        onClick = { aesMode = m },
                        label = { Text(m) },
                        enabled = true
                    )
                }
            }
            Button(
                onClick = {
                    aesError = null
                    aesResult = null
                    try {
                        val data = when {
                            raw.startsWith("BLOB") -> {
                                val hexPart = raw.substringAfter(": ").trim()
                                CryptoTools.hexToBytes(hexPart)
                            }
                            raw.matches(Regex("^[0-9a-fA-F]+$")) -> CryptoTools.hexToBytes(raw)
                            else -> android.util.Base64.decode(raw, android.util.Base64.DEFAULT)
                        }
                        aesResult = CryptoTools.aesDecrypt(data, aesKey, aesIv.ifBlank { null }, aesMode)
                    } catch (e: Exception) {
                        aesError = e.message ?: "не удалось расшифровать"
                    }
                },
                modifier = Modifier.fillMaxWidth()
            ) { Text("Расшифровать AES-$aesMode") }
            aesResult?.let {
                Text("Результат:", style = MaterialTheme.typography.labelLarge)
                Surface(color = MaterialTheme.colorScheme.tertiaryContainer, shape = MaterialTheme.shapes.medium) {
                    Text(it, fontFamily = FontFamily.Monospace, style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(12.dp))
                }
            }
            aesError?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            Spacer(Modifier.height(24.dp))
        }
    }
}
