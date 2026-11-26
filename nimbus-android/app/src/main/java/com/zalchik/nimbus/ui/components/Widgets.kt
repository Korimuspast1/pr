package com.zalchik.nimbus.ui.components

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.combinedClickable
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.nimbus.engine.Qr
import com.zalchik.nimbus.ui.theme.MonoStyle
import com.zalchik.nimbus.ui.theme.NimbusTheme

/**
 * The pieces every screen is built from, drawn the way the desktop draws them: hairline borders,
 * one-step-lighter surfaces, a pill for a chosen option, and no elevation anywhere.
 */

val CardShape = RoundedCornerShape(12.dp)
val PillShape = RoundedCornerShape(17.dp)
val InputShape = RoundedCornerShape(9.dp)

/** A card: surface colour, one-pixel border, rounded corners. */
@Composable
fun NimbusCard(
    modifier: Modifier = Modifier,
    onClick: (() -> Unit)? = null,
    onLongClick: (() -> Unit)? = null,
    content: @Composable () -> Unit,
) {
    val colors = NimbusTheme.colors
    val background = if (onClick != null) colors.surface else colors.surface
    Box(
        modifier
            .clip(CardShape)
            .background(background)
            .border(1.dp, colors.border, CardShape)
            .then(
                if (onClick != null) {
                    Modifier.combinedClickable(
                        onClick = onClick,
                        onLongClick = onLongClick,
                    )
                } else {
                    Modifier
                }
            )
    ) { content() }
}

/** The small grey label above a section, as desktop's `caption()`. */
@Composable
fun Caption(text: String, modifier: Modifier = Modifier) {
    Text(
        text.uppercase(),
        modifier = modifier,
        style = MaterialTheme.typography.labelSmall,
        color = NimbusTheme.colors.muted,
    )
}

/** A row inside a card: a title, an optional value on the right, and a click target. */
@Composable
fun SettingsRow(
    title: String,
    modifier: Modifier = Modifier,
    subtitle: String = "",
    value: String = "",
    icon: ImageVector? = null,
    onClick: (() -> Unit)? = null,
    trailing: (@Composable () -> Unit)? = null,
) {
    val colors = NimbusTheme.colors
    Row(
        modifier
            .fillMaxWidth()
            .then(if (onClick != null) Modifier.clickable(onClick = onClick) else Modifier)
            .padding(horizontal = 16.dp, vertical = 13.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (icon != null) {
            Icon(icon, null, tint = colors.text2, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(14.dp))
        }
        Column(Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.bodyLarge, color = colors.text)
            if (subtitle.isNotEmpty()) {
                Text(
                    subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.muted,
                    maxLines = 3,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
        if (value.isNotEmpty()) {
            Text(value, style = MaterialTheme.typography.bodyMedium, color = colors.muted)
            Spacer(Modifier.width(8.dp))
        }
        trailing?.invoke()
    }
}

@Composable
fun SwitchRow(
    title: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
    modifier: Modifier = Modifier,
    subtitle: String = "",
    icon: ImageVector? = null,
) {
    val colors = NimbusTheme.colors
    SettingsRow(
        title = title,
        subtitle = subtitle,
        icon = icon,
        modifier = modifier,
        onClick = { onCheckedChange(!checked) },
        trailing = {
            Switch(
                checked = checked,
                onCheckedChange = null,
                colors = SwitchDefaults.colors(
                    checkedThumbColor = colors.onAccent,
                    checkedTrackColor = colors.accent,
                    uncheckedThumbColor = colors.text2,
                    uncheckedTrackColor = colors.surface2,
                    uncheckedBorderColor = colors.border2,
                ),
            )
        },
    )
}

/**
 * A pill row of mutually exclusive options — the desktop's `Segmented`, used for the tunnel mode,
 * the routing mode and every either/or setting.
 */
@Composable
fun <T> Segmented(
    options: List<Pair<T, String>>,
    selected: T,
    onSelect: (T) -> Unit,
    modifier: Modifier = Modifier,
    tips: Map<T, String> = emptyMap(),
) {
    val colors = NimbusTheme.colors
    Row(
        modifier
            .clip(PillShape)
            .background(colors.surface)
            .border(1.dp, colors.border, PillShape)
            .padding(3.dp),
        horizontalArrangement = Arrangement.spacedBy(2.dp),
    ) {
        for ((key, label) in options) {
            val active = key == selected
            val background by animateFloatAsState(if (active) 1f else 0f, label = "pill")
            Box(
                Modifier
                    .weight(1f)
                    .clip(PillShape)
                    .background(
                        if (active) colors.accent.copy(alpha = background) else Color.Transparent
                    )
                    .clickable { onSelect(key) }
                    .padding(horizontal = 10.dp, vertical = 8.dp),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    label,
                    style = MaterialTheme.typography.labelLarge,
                    color = if (active) colors.onAccent else colors.text2,
                    fontWeight = if (active) FontWeight.SemiBold else FontWeight.Medium,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
    }
    val tip = tips[selected]
    if (!tip.isNullOrEmpty()) {
        Spacer(Modifier.height(6.dp))
        Text(tip, style = MaterialTheme.typography.bodySmall, color = colors.muted)
    }
}

/** The round connect button: white when connected, outlined when not. */
@Composable
fun PowerButton(
    connected: Boolean,
    busy: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    size: Int = 132,
) {
    val colors = NimbusTheme.colors
    val ring by animateFloatAsState(if (connected) 1f else 0f, label = "power")
    Box(
        modifier
            .size(size.dp)
            .clip(CircleShape)
            .background(if (connected) colors.accent else colors.surface)
            .border(
                (if (connected) 0 else 1).dp + (ring * 0).dp,
                if (connected) colors.accent else colors.border2,
                CircleShape,
            )
            .clickable(enabled = !busy, onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        if (busy) {
            androidx.compose.material3.CircularProgressIndicator(
                modifier = Modifier.size((size / 3).dp),
                color = if (connected) colors.onAccent else colors.text2,
                strokeWidth = 2.dp,
            )
        } else {
            Icon(
                if (connected) com.zalchik.nimbus.ui.NiIcons.PowerOn else com.zalchik.nimbus.ui.NiIcons.PowerOff,
                contentDescription = null,
                tint = if (connected) colors.onAccent else colors.text2,
                modifier = Modifier.size((size * 0.42f).dp),
            )
        }
    }
}

/** The speed graph under the counters: the last minutes of up and down, as desktop draws them. */
@Composable
fun SpeedGraph(
    up: List<Long>,
    down: List<Long>,
    modifier: Modifier = Modifier,
    height: Int = 46,
) {
    val colors = NimbusTheme.colors
    Canvas(modifier.fillMaxWidth().height(height.dp)) {
        val peak = (up + down).maxOrNull()?.coerceAtLeast(1024) ?: 1024L
        fun path(values: List<Long>): Path {
            val path = Path()
            if (values.size < 2) return path
            val step = size.width / (values.size - 1)
            values.forEachIndexed { index, value ->
                val x = index * step
                val y = size.height - (value.toFloat() / peak) * size.height * 0.92f
                if (index == 0) path.moveTo(x, y) else path.lineTo(x, y)
            }
            return path
        }
        drawPath(
            path(down),
            color = colors.text2,
            style = androidx.compose.ui.graphics.drawscope.Stroke(width = 1.5.dp.toPx()),
        )
        drawPath(
            path(up),
            color = colors.muted,
            style = androidx.compose.ui.graphics.drawscope.Stroke(width = 1.5.dp.toPx()),
        )
        drawLine(colors.border, Offset(0f, size.height - 0.5f), Offset(size.width, size.height - 0.5f))
    }
}

/** A QR code drawn straight from the encoder — no bitmap, no dependency on the display density. */
@Composable
fun QrImage(text: String, modifier: Modifier = Modifier, size: Int = 260) {
    val modules = remember(text) { runCatching { Qr.encode(text, 512, 2) }.getOrNull() }
    val colors = NimbusTheme.colors
    if (modules == null) {
        Box(
            modifier.size(size.dp).clip(CardShape).background(colors.surface2),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                com.zalchik.nimbus.i18n.I18n.tr("Слишком длинно для QR-кода"),
                style = MaterialTheme.typography.bodySmall,
                color = colors.muted,
                modifier = Modifier.padding(16.dp),
            )
        }
        return
    }
    Canvas(
        modifier
            .size(size.dp)
            .clip(CardShape)
            .background(Color.White)
            .padding(8.dp)
    ) {
        val rows = modules.size
        val cols = modules.firstOrNull()?.size ?: rows
        val cell = size.width / cols
        for (y in 0 until rows) {
            for (x in 0 until cols) {
                if (!modules[y][x]) continue
                drawRect(
                    color = Color.Black,
                    topLeft = Offset(x * cell, y * cell),
                    size = androidx.compose.ui.geometry.Size(cell + 0.6f, cell + 0.6f),
                )
            }
        }
    }
}

/** A speed or size, as the core formats it. */
@Composable
fun MonoText(text: String, modifier: Modifier = Modifier, color: Color = NimbusTheme.colors.text) {
    Text(text, modifier = modifier, style = MonoStyle, color = color)
}

/** One server in the list. */
@Composable
fun ServerRow(
    name: String,
    detail: String,
    delay: String,
    selected: Boolean,
    onClick: () -> Unit,
    onLongClick: () -> Unit,
    modifier: Modifier = Modifier,
    badge: String = "",
    unreachable: Boolean = false,
) {
    val colors = NimbusTheme.colors
    Row(
        modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(9.dp))
            .background(if (selected) colors.surface2 else Color.Transparent)
            .combinedClickable(onClick = onClick, onLongClick = onLongClick)
            .padding(horizontal = 12.dp, vertical = 11.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(8.dp)
                .clip(CircleShape)
                .background(
                    when {
                        !selected -> colors.border3
                        unreachable -> colors.error
                        else -> colors.ok
                    }
                )
        )
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(
                name,
                style = MaterialTheme.typography.bodyLarge,
                color = if (selected) colors.text else colors.text2,
                fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            if (detail.isNotEmpty()) {
                Text(
                    detail,
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.muted,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
        if (badge.isNotEmpty()) {
            Box(
                Modifier
                    .clip(PillShape)
                    .background(colors.surface2)
                    .border(1.dp, colors.border, PillShape)
                    .padding(horizontal = 8.dp, vertical = 3.dp)
            ) {
                Text(badge, style = MaterialTheme.typography.labelSmall, color = colors.muted)
            }
            Spacer(Modifier.width(8.dp))
        }
        Text(
            delay,
            style = MonoStyle.copy(fontSize = 12.sp),
            color = when {
                delay == "—" -> colors.faint
                unreachable -> colors.error
                else -> colors.muted
            },
        )
    }
}

/** An empty-state block: an icon, a line, and what to do about it. */
@Composable
fun EmptyState(icon: ImageVector, text: String, action: String = "", onAction: (() -> Unit)? = null) {
    val colors = NimbusTheme.colors
    Column(
        Modifier.fillMaxWidth().padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Icon(icon, null, tint = colors.faint, modifier = Modifier.size(34.dp))
        Text(
            text,
            style = MaterialTheme.typography.bodyMedium,
            color = colors.muted,
            textAlign = androidx.compose.ui.text.style.TextAlign.Center,
        )
        if (action.isNotEmpty() && onAction != null) {
            Text(
                action,
                style = MaterialTheme.typography.labelLarge,
                color = colors.text,
                modifier = Modifier
                    .clip(PillShape)
                    .border(1.dp, colors.border2, PillShape)
                    .clickable(onClick = onAction)
                    .padding(horizontal = 16.dp, vertical = 8.dp),
            )
        }
    }
}
