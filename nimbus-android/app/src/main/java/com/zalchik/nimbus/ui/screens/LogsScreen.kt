package com.zalchik.nimbus.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.nimbus.data.Paths
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.NiIcons
import com.zalchik.nimbus.ui.components.Chip
import com.zalchik.nimbus.ui.components.EmptyState
import com.zalchik.nimbus.ui.components.copyToClipboard
import com.zalchik.nimbus.ui.components.shareFile
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.theme.MonoStyle
import com.zalchik.nimbus.ui.theme.NimbusTheme
import io.nekohasekai.libbox.Libbox

/** The core's own log, as sing-box writes it: level, message, and the time it arrived. */
@Composable
fun LogsScreen(app: AppState, onBack: () -> Unit) {
    val colors = NimbusTheme.colors
    val context = LocalContext.current
    val logs by app.logs.collectAsState()
    var filter by remember { mutableStateOf("all") }
    val list = rememberLazyListState()

    val shown = remember(logs, filter) {
        when (filter) {
            "error" -> logs.filter { it.level <= 0 }
            "warning" -> logs.filter { it.level <= 1 }
            else -> logs
        }
    }

    // Follow the tail, as a terminal does, unless the reader has scrolled back.
    val atEnd = remember(shown.size) { shown.size <= 1 || list.firstVisibleItemIndex >= shown.size - 4 }
    LaunchedEffect(shown.size, atEnd) {
        if (shown.isNotEmpty() && atEnd) list.animateScrollToItem(shown.size - 1)
    }

    ScreenScaffold(
        title = I18n.tr("Журнал"),
        onBack = onBack,
        actions = {
            Chip(I18n.tr("Очистить"), NiIcons.Delete) { app.clearLogs() }
        },
    ) {
        Column(Modifier.fillMaxSize()) {
            Row(
                Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                for ((key, label) in listOf(
                    "all" to I18n.tr("Всё"),
                    "warning" to I18n.tr("Предупр."),
                    "error" to I18n.tr("Ошибки"),
                )) {
                    val active = key == filter
                    Box(
                        Modifier
                            .clip(RoundedCornerShape(9.dp))
                            .background(if (active) colors.surface2 else colors.surface)
                            .clickable { filter = key }
                            .padding(horizontal = 12.dp, vertical = 7.dp),
                    ) {
                        Text(
                            label,
                            style = MaterialTheme.typography.labelLarge,
                            color = if (active) colors.text else colors.muted,
                        )
                    }
                }
                Spacer(Modifier.weight(1f))
                Icon(
                    NiIcons.Copy,
                    I18n.tr("Скопировать"),
                    tint = colors.muted,
                    modifier = Modifier
                        .size(34.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .clickable {
                            copyToClipboard(
                                context,
                                shown.joinToString("\n") { "${I18n.time(it.time)} ${levelName(it.level)} ${it.message}" },
                            )
                        }
                        .padding(8.dp),
                )
                Icon(
                    NiIcons.Share,
                    I18n.tr("Отправить"),
                    tint = colors.muted,
                    modifier = Modifier
                        .size(34.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .clickable { shareFile(context, writeLogExport(app), "text/plain") }
                        .padding(8.dp),
                )
            }

            if (shown.isEmpty()) {
                EmptyState(
                    icon = NiIcons.Logs,
                    text = I18n.tr("Журнал пуст. Он заполняется, когда туннель работает."),
                )
            } else {
                LazyColumn(
                    state = list,
                    modifier = Modifier.fillMaxSize().background(colors.background),
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(
                        start = 12.dp,
                        end = 12.dp,
                        bottom = 96.dp,
                    ),
                ) {
                    items(shown) { line ->
                        Row(Modifier.fillMaxWidth().padding(vertical = 3.dp)) {
                            Text(
                                I18n.time(line.time),
                                style = MonoStyle.copy(fontSize = 10.5.sp),
                                color = colors.faint,
                            )
                            Spacer(Modifier.width(10.dp))
                            Text(
                                levelName(line.level),
                                style = MonoStyle.copy(fontSize = 10.5.sp),
                                color = when (line.level) {
                                    0 -> colors.error
                                    1 -> colors.warn
                                    2 -> colors.text2
                                    else -> colors.faint
                                },
                            )
                            Spacer(Modifier.width(10.dp))
                            Text(
                                line.message,
                                style = MonoStyle.copy(fontSize = 11.sp),
                                color = colors.text2,
                                modifier = Modifier.weight(1f),
                            )
                        }
                    }
                }
            }
        }
    }
}

private fun levelName(level: Int): String = when (level) {
    0 -> "error"
    1 -> "warn"
    2 -> "info"
    3 -> "debug"
    else -> "trace"
}

/** Write the collected log to a file the user can send — support asks for it more than anything. */
fun writeLogExport(app: AppState): java.io.File {
    val lines = app.logs.value
    val file = java.io.File(Paths.logDir, "nimbus-log.txt")
    file.parentFile?.mkdirs()
    file.writeText(
        buildString {
            appendLine("nimbus ${Paths.VERSION}")
            appendLine("sing-box ${com.zalchik.nimbus.BuildConfig.SINGBOX_VERSION}")
            appendLine("core ${Libbox.version()}")
            appendLine("device ${com.zalchik.nimbus.engine.Device.name} · Android ${android.os.Build.VERSION.RELEASE}")
            appendLine("exported ${java.text.SimpleDateFormat("yyyy-MM-dd HH:mm:ss", java.util.Locale.US)
                .format(java.util.Date())}")
            appendLine()
            for (line in lines) appendLine("${I18n.time(line.time)} ${levelName(line.level)} ${line.message}")
        }
    )
    return file
}
