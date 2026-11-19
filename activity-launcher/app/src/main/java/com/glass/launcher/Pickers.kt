package com.glass.launcher

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Apps
import androidx.compose.material.icons.rounded.Clear
import androidx.compose.material.icons.rounded.Image
import androidx.compose.material.icons.rounded.Layers
import androidx.compose.material3.Icon
import androidx.compose.material3.Slider
import androidx.compose.material3.SliderDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

@Composable
fun GlassDialog(title: String, onDismiss: () -> Unit, content: @Composable ColumnScope.() -> Unit) {
    val g = glass
    val shape = RoundedCornerShape(34.dp)
    Dialog(onDismissRequest = onDismiss, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Column(
            Modifier
                .padding(18.dp)
                .fillMaxWidth()
                .heightIn(max = 640.dp)
                .clip(shape)
                .background(if (g.dark) Color(0xF2141A2C) else Color(0xF4F6F8FF))
                .background(Brush.linearGradient(listOf(g.accent.copy(alpha = .10f), Color.Transparent, g.accent2.copy(alpha = .10f))))
                .glassRim(g, shape)
                .padding(18.dp),
        ) {
            Text(title, color = g.text, fontSize = 20.sp, fontWeight = FontWeight.Bold, modifier = Modifier.padding(start = 6.dp, bottom = 12.dp))
            content()
        }
    }
}

@Composable
private fun PickRow(icon: androidx.compose.ui.graphics.ImageBitmap?, title: String, subtitle: String, onClick: () -> Unit) {
    val g = glass
    Row(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(20.dp)).glassClick(onClick = onClick).padding(horizontal = 8.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        AppIcon(icon, 40.dp)
        Spacer(Modifier.width(12.dp))
        Column(Modifier.weight(1f)) {
            Text(title, color = g.text, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
            Text(subtitle, color = g.textDim, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
        }
    }
}

@Composable
fun AppPickerDialog(title: String = tr("Choose app", "Выберите приложение"), onDismiss: () -> Unit, onPick: (AppEntry) -> Unit) {
    var q by remember { mutableStateOf("") }
    val apps = Pkg.apps ?: emptyList()
    val list = remember(apps, q) {
        apps.filter { q.isBlank() || it.label.contains(q, true) || it.pkg.contains(q, true) }.sortedBy { it.label.lowercase() }
    }
    GlassDialog(title, onDismiss) {
        GlassSearchField(q, { q = it }, tr("package / name", "пакет / название"))
        Spacer(Modifier.height(8.dp))
        LazyColumn(Modifier.weight(1f, fill = false)) {
            items(list, key = { it.pkg }) { a ->
                PickRow(rememberIcon(a.pkg), a.label, a.pkg) { onPick(a) }
            }
        }
    }
}

@Composable
fun ActivityPickerDialog(pkg: String, onDismiss: () -> Unit, onPick: (ActEntry) -> Unit) {
    val ctx = LocalContext.current
    var q by remember { mutableStateOf("") }
    val acts by produceState<List<ActEntry>?>(null, pkg) {
        value = withContext(Dispatchers.IO) { Pkg.activitiesOf(ctx, pkg) }
    }
    GlassDialog(tr("Choose activity", "Выберите активити"), onDismiss) {
        GlassSearchField(q, { q = it }, tr("Filter", "Фильтр"))
        Spacer(Modifier.height(8.dp))
        val list = (acts ?: emptyList()).filter { q.isBlank() || it.label.contains(q, true) || it.cls.contains(q, true) }
        if (acts == null) Text(tr("Loading…", "Загрузка…"), color = glass.textDim, modifier = Modifier.padding(12.dp))
        else if (list.isEmpty()) Text(tr("No activities", "Нет активити"), color = glass.textDim, modifier = Modifier.padding(12.dp))
        LazyColumn(Modifier.weight(1f, fill = false)) {
            items(list, key = { it.cls }) { a ->
                PickRow(rememberIcon(a.pkg, a.cls), a.label + if (a.exported) "" else "  🔒", a.shortClass) { onPick(a) }
            }
        }
    }
}

@Composable
fun IconPickerDialog(
    onDismiss: () -> Unit,
    onActivityIcon: () -> Unit,
    onAppIcon: () -> Unit,
    onGallery: () -> Unit,
    onClear: () -> Unit,
) {
    GlassDialog(tr("Shortcut icon", "Иконка ярлыка"), onDismiss) {
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            OptionRow(Icons.Rounded.Layers, tr("Activity icon", "Иконка активити"), onActivityIcon)
            OptionRow(Icons.Rounded.Apps, tr("Icon of another app", "Иконка другого приложения"), onAppIcon)
            OptionRow(Icons.Rounded.Image, tr("Image from gallery", "Картинка из галереи"), onGallery)
            OptionRow(Icons.Rounded.Clear, tr("Default", "По умолчанию"), onClear)
        }
    }
}

@Composable
private fun OptionRow(icon: ImageVector, text: String, onClick: () -> Unit) {
    val g = glass
    GlassCard(padding = PaddingValues(horizontal = 16.dp, vertical = 14.dp), onClick = onClick) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(icon, null, tint = g.accent)
            Spacer(Modifier.width(14.dp))
            Text(text, color = g.text, fontWeight = FontWeight.Medium)
        }
    }
}

@Composable
fun ColorPickerDialog(title: String, initial: Color, onDismiss: () -> Unit, onPick: (Color) -> Unit) {
    val g = glass
    val hsv = remember { FloatArray(3).also { android.graphics.Color.colorToHSV(initial.toArgb(), it) } }
    var h by remember { mutableFloatStateOf(hsv[0]) }
    var s by remember { mutableFloatStateOf(hsv[1]) }
    var v by remember { mutableFloatStateOf(hsv[2]) }
    val color = Color.hsv(h.coerceIn(0f, 360f), s.coerceIn(0f, 1f), v.coerceIn(0f, 1f))
    var hex by remember { mutableStateOf(String.format("%06X", initial.toArgb() and 0xFFFFFF)) }
    fun syncHex(c: Color) { hex = String.format("%06X", c.toArgb() and 0xFFFFFF) }
    GlassDialog(title, onDismiss) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                Modifier.size(64.dp).clip(CircleShape).background(color).border(2.dp, Color.White.copy(alpha = .8f), CircleShape),
            )
            Spacer(Modifier.width(14.dp))
            GlassField("HEX", hex, { txt ->
                hex = txt.removePrefix("#").take(6).uppercase()
                hex.toLongOrNull(16)?.takeIf { hex.length == 6 }?.let {
                    val arr = FloatArray(3)
                    android.graphics.Color.colorToHSV((0xFF000000 or it).toInt(), arr)
                    h = arr[0]; s = arr[1]; v = arr[2]
                }
            }, singleLine = true, mono = true, modifier = Modifier.weight(1f))
        }
        Spacer(Modifier.height(14.dp))
        val brushHue = Brush.horizontalGradient((0..6).map { Color.hsv(it * 60f % 360f, 1f, 1f) })
        ColorSlider(tr("Hue", "Тон"), h, 0f..360f, brushHue) { h = it; syncHex(Color.hsv(h, s, v)) }
        ColorSlider(tr("Saturation", "Насыщенность"), s, 0f..1f,
            Brush.horizontalGradient(listOf(Color.hsv(h, 0f, v), Color.hsv(h, 1f, v)))) { s = it; syncHex(Color.hsv(h, s, v)) }
        ColorSlider(tr("Brightness", "Яркость"), v, 0f..1f,
            Brush.horizontalGradient(listOf(Color.Black, Color.hsv(h, s, 1f)))) { v = it; syncHex(Color.hsv(h, s, v)) }
        Spacer(Modifier.height(10.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            GlassButton(tr("Cancel", "Отмена"), null, Modifier.weight(1f), primary = false, onClick = onDismiss)
            GlassButton(tr("Apply", "Применить"), null, Modifier.weight(1f)) { onPick(color) }
        }
    }
}

@Composable
private fun ColorSlider(label: String, value: Float, range: ClosedFloatingPointRange<Float>, track: Brush, onChange: (Float) -> Unit) {
    val g = glass
    Text(label, color = g.textDim, fontSize = 12.sp, modifier = Modifier.padding(start = 6.dp))
    Box(contentAlignment = Alignment.Center) {
        Box(Modifier.fillMaxWidth().padding(horizontal = 10.dp).height(10.dp).clip(CircleShape).background(track))
        Slider(
            value = value, onValueChange = onChange, valueRange = range,
            colors = SliderDefaults.colors(
                thumbColor = Color.White, activeTrackColor = Color.Transparent, inactiveTrackColor = Color.Transparent,
                activeTickColor = Color.Transparent, inactiveTickColor = Color.Transparent,
            ),
        )
    }
}
