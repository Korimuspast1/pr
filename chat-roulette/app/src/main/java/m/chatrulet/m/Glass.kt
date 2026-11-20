package m.chatrulet.m

import android.os.Build
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.LocalIndication
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.ArrowBack
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.drawOutline
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.lerp
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import dev.chrisbanes.haze.HazeState
import dev.chrisbanes.haze.HazeStyle
import dev.chrisbanes.haze.HazeTint
import dev.chrisbanes.haze.haze
import dev.chrisbanes.haze.hazeChild
import kotlin.math.PI
import kotlin.math.cos
import kotlin.math.sin

// ════════════════════════════════════════════════════════════════════ palette

data class Accent(val nameEn: String, val nameRu: String, val a: Color, val b: Color)

val ACCENTS = listOf(
    Accent("Ocean", "Океан", Color(0xFF3D7BFF), Color(0xFF00C6FF)),
    Accent("Violet", "Фиалка", Color(0xFF8B5CF6), Color(0xFFEC4899)),
    Accent("Mint", "Мята", Color(0xFF10B981), Color(0xFF06B6D4)),
    Accent("Sunset", "Закат", Color(0xFFFF6B3D), Color(0xFFFF3D7F)),
    Accent("Gold", "Золото", Color(0xFFF59E0B), Color(0xFFEF4444)),
    Accent("Aurora", "Аврора", Color(0xFF22D3EE), Color(0xFFA855F7)),
    Accent("Graphite", "Графит", Color(0xFF64748B), Color(0xFF94A3B8)),
)

data class GlassColors(
    val dark: Boolean,
    val accent: Color,
    val accent2: Color,
    val base: Color,
    val text: Color,
    val textDim: Color,
    val fill: Color,
    val fillStrong: Color,
    val stroke: Color,
    val highlight: Color,
    val blobs: List<Color>,
    val blur: Dp,
    val refraction: Float = 0.6f,
)

val LocalGlass = staticCompositionLocalOf {
    GlassColors(false, ACCENTS[0].a, ACCENTS[0].b, Color(0xFFEEF1F8), Color.Black, Color.Gray,
        Color.White.copy(alpha = .5f), Color.White.copy(alpha = .7f), Color.White, Color.White, emptyList(), 24.dp)
}

val glass: GlassColors @Composable get() = LocalGlass.current

@Composable
fun GlassTheme(content: @Composable () -> Unit) {
    val dark = when (Store.theme) {
        "light" -> false
        "dark" -> true
        else -> isSystemInDarkTheme()
    }
    val ctx = LocalContext.current
    val dynamic = Store.accent < 0 && Build.VERSION.SDK_INT >= 31
    val (a, b) = if (dynamic) {
        val s = if (dark) dynamicDarkColorScheme(ctx) else dynamicLightColorScheme(ctx)
        s.primary to s.tertiary
    } else {
        val acc = ACCENTS[Store.accent.coerceIn(0, ACCENTS.lastIndex)]
        acc.a to acc.b
    }
    val op = Store.glassOpacity.coerceIn(0f, 1f)
    val colors = if (dark) GlassColors(
        dark = true, accent = lerp(a, Color.White, .15f), accent2 = b,
        base = Color(0xFF070A14),
        text = Color(0xFFF4F6FF), textDim = Color(0xFFA3A9C2),
        fill = Color.White.copy(alpha = 0.02f + 0.09f * op),
        fillStrong = Color(0xFF1A1F33).copy(alpha = 0.18f + 0.55f * op),
        stroke = Color.White.copy(alpha = 0.30f), highlight = Color.White.copy(alpha = 0.09f),
        blobs = listOf(a, b, lerp(a, b, .5f), Color(0xFF3B1E8C)),
        blur = Store.blur.dp, refraction = Store.refraction,
    ) else GlassColors(
        dark = false, accent = lerp(a, Color.Black, .08f), accent2 = b,
        base = Color(0xFFEFF2FA),
        text = Color(0xFF0E1222), textDim = Color(0xFF5B6178),
        fill = Color.White.copy(alpha = 0.10f + 0.36f * op),
        fillStrong = Color.White.copy(alpha = 0.20f + 0.55f * op),
        stroke = Color.White.copy(alpha = 0.95f), highlight = Color.White.copy(alpha = 0.45f),
        blobs = listOf(a, b, lerp(a, b, .5f), Color(0xFFFFB86B)),
        blur = Store.blur.dp, refraction = Store.refraction,
    )
    val scheme = if (dark) darkColorScheme(
        primary = colors.accent, secondary = colors.accent2, tertiary = colors.accent2,
        background = colors.base, surface = Color(0xFF151A2C), onSurface = colors.text,
        surfaceVariant = Color(0xFF22283D), onSurfaceVariant = colors.textDim,
        surfaceContainer = Color(0xFF1A2033), surfaceContainerHigh = Color(0xFF1E2438),
    ) else lightColorScheme(
        primary = colors.accent, secondary = colors.accent2, tertiary = colors.accent2,
        background = colors.base, surface = Color(0xFFF8F9FF), onSurface = colors.text,
        surfaceVariant = Color(0xFFE3E7F3), onSurfaceVariant = colors.textDim,
        surfaceContainer = Color(0xFFF2F4FB), surfaceContainerHigh = Color(0xFFF7F8FD),
    )
    val density = LocalDensity.current
    CompositionLocalProvider(
        LocalGlass provides colors,
        LocalDensity provides Density(density.density, density.fontScale * Store.fontScale.coerceIn(0.7f, 1.5f)),
    ) {
        MaterialTheme(colorScheme = scheme, content = content)
    }
}

// ═══════════════════════════════════════════════════════ liquid background

@Composable
private fun rememberClock(enabled: Boolean): Float {
    var t by remember { mutableFloatStateOf(0f) }
    LaunchedEffect(enabled) {
        if (!enabled) return@LaunchedEffect
        while (true) {
            withFrameNanos { t = ((it / 1_000_000L) % 3_600_000L) / 1000f }
        }
    }
    return t
}

@Composable
fun LiquidBackground(modifier: Modifier = Modifier) {
    val g = glass
    val ctx = LocalContext.current
    val reduceMotion = remember {
        try {
            android.provider.Settings.Global.getFloat(ctx.contentResolver, android.provider.Settings.Global.ANIMATOR_DURATION_SCALE, 1f) == 0f
        } catch (e: Throwable) {
            false
        }
    }
    val mode = Store.bgMode
    val t = rememberClock(Store.animatedBg && !reduceMotion && mode == "blobs")
    if (mode == "solid") {
        Box(modifier.fillMaxSize().background(g.base))
        return
    }
    if (mode == "gradient") {
        Box(
            modifier.fillMaxSize().background(g.base).background(
                Brush.linearGradient(
                    listOf(g.blobs[0].copy(alpha = .55f), g.blobs[2].copy(alpha = .35f), g.blobs[1].copy(alpha = .55f)),
                ),
            ),
        )
        return
    }
    Box(
        modifier
            .fillMaxSize()
            .background(g.base)
            .drawBehind {
                val w = size.width
                val h = size.height
                val r = maxOf(w, h)
                val alpha = if (g.dark) 0.55f else 0.50f
                val specs = listOf(
                    floatArrayOf(0.15f, 0.12f, 0.62f, 0.050f, 0f),
                    floatArrayOf(0.90f, 0.30f, 0.55f, 0.037f, 1.7f),
                    floatArrayOf(0.25f, 0.78f, 0.60f, 0.043f, 3.1f),
                    floatArrayOf(0.85f, 0.92f, 0.50f, 0.031f, 4.4f),
                )
                specs.forEachIndexed { i, s ->
                    val ph = t * s[3] * 2f * PI.toFloat() + s[4]
                    val cx = w * (s[0] + 0.16f * sin(ph))
                    val cy = h * (s[1] + 0.10f * cos(ph * 0.8f))
                    val c = g.blobs[i % g.blobs.size]
                    drawCircle(
                        brush = Brush.radialGradient(
                            listOf(c.copy(alpha = alpha), c.copy(alpha = alpha * 0.35f), Color.Transparent),
                            center = Offset(cx, cy), radius = r * s[2],
                        ),
                        radius = r * s[2], center = Offset(cx, cy),
                    )
                }
                drawRect(
                    Brush.verticalGradient(
                        listOf(g.base.copy(alpha = 0.0f), g.base.copy(alpha = if (g.dark) 0.35f else 0.25f)),
                    ),
                )
            },
    )
}

// ══════════════════════════════════════════════════════════ glass modifiers

fun Modifier.liquidGlass(
    g: GlassColors,
    shape: Shape,
    haze: HazeState?,
    tint: Color = g.fillStrong,
    elevation: Dp = 0.dp,
): Modifier {
    var m = this
    if (elevation > 0.dp) {
        m = m.shadow(elevation, shape, clip = false,
            ambientColor = Color.Black.copy(alpha = .25f), spotColor = Color.Black.copy(alpha = .25f))
    }
    m = m.clip(shape)
    m = if (haze != null) {
        m.hazeChild(
            haze,
            style = HazeStyle(
                backgroundColor = g.base,
                tints = listOf(HazeTint(tint.copy(alpha = tint.alpha * 0.6f))),
                blurRadius = g.blur,
                noiseFactor = 0.035f,
                fallbackTint = HazeTint(if (g.dark) Color(0xEE161B2D) else Color(0xEEF7F8FD)),
            ),
        )
    } else {
        m.background(tint)
    }
    return m.glassRim(g, shape)
}

fun Modifier.glassPane(g: GlassColors, shape: Shape, tint: Color = g.fill): Modifier =
    this.clip(shape).background(tint).glassRim(g, shape)

fun Modifier.glassRim(g: GlassColors, shape: Shape): Modifier = this
    .background(
        Brush.verticalGradient(
            0f to g.highlight,
            0.38f to Color.Transparent,
            0.85f to Color.Transparent,
            1f to Color.White.copy(alpha = if (g.dark) 0.04f else 0.14f),
        ),
    )
    .drawWithContent {
        drawContent()
        val outline = shape.createOutline(size, layoutDirection, this)
        val r = g.refraction.coerceIn(0f, 1f)
        val w = size.width
        val h = size.height
        drawOutline(
            outline,
            brush = Brush.verticalGradient(
                listOf(Color.White.copy(alpha = 0.22f * r), Color.White.copy(alpha = 0.02f * r), Color.White.copy(alpha = 0.12f * r)),
            ),
            style = Stroke(width = 7.dp.toPx()),
        )
        drawOutline(
            outline,
            brush = Brush.linearGradient(
                0f to Color.Transparent,
                0.65f to Color.Transparent,
                1f to g.accent2.copy(alpha = 0.35f * r),
                start = Offset(0f, 0f), end = Offset(w * 0.3f, h),
            ),
            style = Stroke(width = 2.5.dp.toPx()),
        )
        drawOutline(
            outline,
            brush = Brush.linearGradient(
                0f to g.stroke,
                0.28f to g.stroke.copy(alpha = g.stroke.alpha * 0.18f),
                0.62f to g.stroke.copy(alpha = g.stroke.alpha * 0.06f),
                1f to g.stroke.copy(alpha = g.stroke.alpha * (0.35f + 0.4f * r)),
                start = Offset(0f, 0f), end = Offset(w, h),
            ),
            style = Stroke(width = 1.2.dp.toPx()),
        )
        if (r > 0f && h > 0f) {
            drawCircle(
                Brush.radialGradient(
                    listOf(Color.White.copy(alpha = 0.35f * r), Color.Transparent),
                    center = Offset(w * 0.18f, h * 0.08f), radius = minOf(w, h) * 0.55f,
                ),
                radius = minOf(w, h) * 0.55f, center = Offset(w * 0.18f, h * 0.08f),
            )
        }
    }

@Composable
fun Modifier.pressScale(source: MutableInteractionSource, pressed: Float = 0.95f): Modifier {
    val isPressed by source.collectIsPressedAsState()
    val s by animateFloatAsState(
        if (isPressed) pressed else 1f,
        spring(dampingRatio = 0.45f, stiffness = 500f), label = "press",
    )
    return this.graphicsLayer {
        scaleX = s
        scaleY = s
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun Modifier.glassClick(onLongClick: (() -> Unit)? = null, onClick: () -> Unit): Modifier {
    val source = remember { MutableInteractionSource() }
    val haptic = LocalHapticFeedback.current
    return this
        .pressScale(source)
        .combinedClickable(
            interactionSource = source,
            indication = LocalIndication.current,
            onLongClick = if (onLongClick == null) null else {
                {
                    if (Store.haptics) haptic.performHapticFeedback(HapticFeedbackType.LongPress)
                    onLongClick()
                }
            },
            onClick = onClick,
        )
}

// ══════════════════════════════════════════════════════════════ scaffold

val TopBarHeight = 64.dp

@Composable
fun statusTop(): Dp = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()

@Composable
fun navBottom(): Dp = WindowInsets.navigationBars.asPaddingValues().calculateBottomPadding()

@Composable
fun GlassScaffold(
    topBar: @Composable (HazeState) -> Unit,
    content: @Composable (PaddingValues) -> Unit,
) {
    val hazeState = remember { HazeState() }
    val top = statusTop() + TopBarHeight + 16.dp
    Box(Modifier.fillMaxSize()) {
        Box(Modifier.fillMaxSize().haze(hazeState)) {
            LiquidBackground()
            content(PaddingValues(top = top, bottom = navBottom() + 24.dp))
        }
        Box(Modifier.align(Alignment.TopCenter).fillMaxWidth()) { topBar(hazeState) }
    }
}

@Composable
fun GlassTopBar(
    haze: HazeState,
    title: String,
    subtitle: String? = null,
    onBack: (() -> Unit)? = null,
    backDesc: String = "Back",
    pro: Boolean = false,
    actions: @Composable RowScope.() -> Unit = {},
) {
    val g = glass
    Row(
        Modifier
            .padding(top = statusTop() + 8.dp, start = 12.dp, end = 12.dp)
            .fillMaxWidth()
            .height(TopBarHeight - 8.dp)
            .liquidGlass(g, RoundedCornerShape((Store.cornerRadius + 2f).dp), haze, elevation = 10.dp)
            .padding(horizontal = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (onBack != null) {
            GlassIconButton(Icons.AutoMirrored.Rounded.ArrowBack, backDesc, onClick = onBack, plain = true)
        } else {
            Spacer(Modifier.width(12.dp))
        }
        Column(Modifier.weight(1f).padding(horizontal = 6.dp)) {
            Text(title, color = g.text, fontSize = 20.sp, fontWeight = FontWeight.SemiBold,
                maxLines = 1, overflow = TextOverflow.Ellipsis)
            if (subtitle != null || (pro && Store.PRO_ACTIVE)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    if (pro && Store.PRO_ACTIVE) {
                        ProBadge()
                        Spacer(Modifier.width(6.dp))
                    }
                    if (subtitle != null) {
                        Text(subtitle, color = g.textDim, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    }
                }
            }
        }
        actions()
    }
}

// ═════════════════════════════════════════════════════════════ components

@Composable
fun GlassIconButton(
    icon: ImageVector,
    description: String?,
    tint: Color? = null,
    plain: Boolean = false,
    size: Dp = 44.dp,
    onLongClick: (() -> Unit)? = null,
    onClick: () -> Unit,
) {
    val g = glass
    Box(
        Modifier
            .size(size)
            .then(if (plain) Modifier.clip(CircleShape) else Modifier.glassPane(g, CircleShape))
            .glassClick(onLongClick, onClick),
        contentAlignment = Alignment.Center,
    ) {
        Icon(icon, description, tint = tint ?: g.text, modifier = Modifier.size(22.dp))
    }
}

@Composable
fun GlassCard(
    modifier: Modifier = Modifier,
    shape: Shape = RoundedCornerShape(Store.cornerRadius.dp),
    padding: PaddingValues = PaddingValues(16.dp),
    tint: Color? = null,
    onLongClick: (() -> Unit)? = null,
    onClick: (() -> Unit)? = null,
    content: @Composable ColumnScope.() -> Unit,
) {
    val g = glass
    Column(
        modifier
            .fillMaxWidth()
            .glassPane(g, shape, tint ?: g.fill)
            .then(if (onClick != null) Modifier.glassClick(onLongClick, onClick) else Modifier)
            .padding(padding),
        content = content,
    )
}

@Composable
fun GlassButton(
    text: String,
    icon: ImageVector? = null,
    modifier: Modifier = Modifier,
    primary: Boolean = true,
    enabled: Boolean = true,
    onClick: () -> Unit,
) {
    val g = glass
    val shape = CircleShape
    val bg = if (primary) {
        Modifier
            .shadow(10.dp, shape, ambientColor = g.accent, spotColor = g.accent)
            .clip(shape)
            .background(Brush.horizontalGradient(listOf(g.accent, lerp(g.accent, g.accent2, 0.65f))))
            .glassRim(g.copy(highlight = Color.White.copy(alpha = .35f), stroke = Color.White.copy(alpha = .6f)), shape)
    } else Modifier.glassPane(g, shape)
    Row(
        modifier
            .fillMaxWidth()
            .height(54.dp)
            .graphicsLayer { alpha = if (enabled) 1f else 0.45f }
            .then(bg)
            .then(if (enabled) Modifier.glassClick(onClick = onClick) else Modifier)
            .padding(horizontal = 20.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        val fg = if (primary) Color.White else g.text
        if (icon != null) Icon(icon, null, tint = fg, modifier = Modifier.size(22.dp))
        Text(
            text, color = fg, fontWeight = FontWeight.SemiBold, fontSize = 15.sp,
            modifier = Modifier.weight(1f).padding(horizontal = 8.dp),
            textAlign = TextAlign.Center, maxLines = 1,
        )
        if (icon != null) Spacer(Modifier.width(22.dp))
    }
}

@Composable
fun GlassChip(text: String, selected: Boolean, icon: ImageVector? = null, onClick: () -> Unit) {
    val g = glass
    val shape = CircleShape
    Row(
        Modifier
            .height(38.dp)
            .then(
                if (selected) Modifier.clip(shape)
                    .background(Brush.horizontalGradient(listOf(g.accent, lerp(g.accent, g.accent2, .6f))))
                    .glassRim(g.copy(stroke = Color.White.copy(alpha = .55f)), shape)
                else Modifier.glassPane(g, shape),
            )
            .glassClick(onClick = onClick)
            .padding(horizontal = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (icon != null) {
            Icon(icon, null, tint = if (selected) Color.White else g.text, modifier = Modifier.size(16.dp))
            Spacer(Modifier.width(6.dp))
        }
        Text(text, color = if (selected) Color.White else g.text, fontSize = 13.sp,
            fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Medium, maxLines = 1)
    }
}

@Composable
fun GlassField(
    label: String,
    value: String,
    onValueChange: (String) -> Unit,
    modifier: Modifier = Modifier,
    placeholder: String = "",
) {
    val g = glass
    Column(modifier.fillMaxWidth()) {
        Text(label, color = g.textDim, fontSize = 12.sp, fontWeight = FontWeight.Medium,
            modifier = Modifier.padding(start = 16.dp, bottom = 4.dp))
        Row(
            Modifier
                .fillMaxWidth()
                .glassPane(g, RoundedCornerShape((Store.cornerRadius * 0.78f).dp), g.fillStrong.copy(alpha = g.fillStrong.alpha * 0.7f))
                .padding(horizontal = 16.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Box(Modifier.weight(1f)) {
                if (value.isEmpty() && placeholder.isNotEmpty()) {
                    Text(placeholder, color = g.textDim.copy(alpha = .7f), fontSize = 15.sp)
                }
                BasicTextField(
                    value = value,
                    onValueChange = onValueChange,
                    singleLine = true,
                    textStyle = TextStyle(color = g.text, fontSize = 15.sp),
                    cursorBrush = SolidColor(g.accent),
                    modifier = Modifier.fillMaxWidth(),
                )
            }
        }
    }
}

@Composable
fun SectionTitle(text: String, modifier: Modifier = Modifier) {
    Text(
        text.uppercase(), color = glass.textDim, fontSize = 12.sp, fontWeight = FontWeight.Bold,
        letterSpacing = 1.2.sp, modifier = modifier.padding(start = 8.dp, top = 12.dp, bottom = 6.dp),
    )
}

@Composable
fun EmptyState(icon: ImageVector, title: String, text: String) {
    val g = glass
    GlassCard(Modifier.padding(horizontal = 16.dp, vertical = 24.dp), padding = PaddingValues(28.dp)) {
        Box(
            Modifier.align(Alignment.CenterHorizontally).size(72.dp)
                .clip(CircleShape)
                .background(Brush.linearGradient(listOf(g.accent.copy(alpha = .5f), g.accent2.copy(alpha = .4f))))
                .glassRim(g, CircleShape),
            contentAlignment = Alignment.Center,
        ) { Icon(icon, null, tint = Color.White, modifier = Modifier.size(34.dp)) }
        Spacer(Modifier.height(16.dp))
        Text(title, color = g.text, fontSize = 18.sp, fontWeight = FontWeight.SemiBold,
            modifier = Modifier.align(Alignment.CenterHorizontally))
        Spacer(Modifier.height(6.dp))
        Text(text, color = g.textDim, fontSize = 14.sp,
            textAlign = TextAlign.Center,
            modifier = Modifier.align(Alignment.CenterHorizontally).widthIn(max = 320.dp))
    }
}

/** Градиентная пилюля «PRO ∞». */
@Composable
fun ProBadge(big: Boolean = false) {
    val g = glass
    Row(
        Modifier
            .clip(CircleShape)
            .background(Brush.linearGradient(listOf(Color(0xFFFFC94D), Color(0xFFFF7A59), Color(0xFFB45CFF))))
            .glassRim(g.copy(stroke = Color.White.copy(alpha = .8f), highlight = Color.White.copy(alpha = .4f)), CircleShape)
            .padding(horizontal = if (big) 14.dp else 8.dp, vertical = if (big) 5.dp else 2.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            if (big) "PRO  ∞" else "PRO ∞", color = Color.White, fontWeight = FontWeight.Black,
            fontSize = if (big) 15.sp else 10.sp, letterSpacing = 0.5.sp, maxLines = 1,
        )
    }
}
