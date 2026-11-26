package com.zalchik.nimbus.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import com.zalchik.nimbus.R

/**
 * Nimbus' own palette, the same values the desktop paints with (nimbus/ui/theme.py): a near-black
 * window, cards one step lighter, hairline borders, and white as the accent — no Material color
 * roles, because the app's look is the desktop's, not Android's.
 */
data class NimbusColors(
    val background: Color,
    val panel: Color,
    val surface: Color,
    val surface2: Color,
    val raised: Color,
    val pressed: Color,
    val hover: Color,
    val border: Color,
    val border2: Color,
    val border3: Color,
    val focus: Color,
    val text: Color,
    val text2: Color,
    val muted: Color,
    val faint: Color,
    val accent: Color,
    val onAccent: Color,
    val accentHover: Color,
    val accentPress: Color,
    val toast: Color,
    /** Connected / OK, warning and error, which the desktop draws with the same three hues. */
    val ok: Color,
    val warn: Color,
    val error: Color,
)

val DarkColors = NimbusColors(
    background = Color(0xFF0A0A0A),
    panel = Color(0xFF0F0F0F),
    surface = Color(0xFF151515),
    surface2 = Color(0xFF1B1B1B),
    raised = Color(0xFF222222),
    pressed = Color(0xFF262626),
    hover = Color(0xFF181818),
    border = Color(0xFF232323),
    border2 = Color(0xFF303030),
    border3 = Color(0xFF454545),
    focus = Color(0xFF5A5A5A),
    text = Color(0xFFF2F2F2),
    text2 = Color(0xFFC4C4C4),
    muted = Color(0xFF7C7C7C),
    faint = Color(0xFF4A4A4A),
    accent = Color(0xFFFFFFFF),
    onAccent = Color(0xFF000000),
    accentHover = Color(0xFFDCDCDC),
    accentPress = Color(0xFFBDBDBD),
    toast = Color(0xFF141414),
    ok = Color(0xFF5BD47C),
    warn = Color(0xFFE0B341),
    error = Color(0xFFE5645E),
)

val LightColors = NimbusColors(
    background = Color(0xFFF7F7F6),
    panel = Color(0xFFEFEFED),
    surface = Color(0xFFFFFFFF),
    surface2 = Color(0xFFE9E9E7),
    raised = Color(0xFFE2E2E0),
    pressed = Color(0xFFDFDFDC),
    hover = Color(0xFFF1F1EF),
    border = Color(0xFFE0E0DD),
    border2 = Color(0xFFCDCDCA),
    border3 = Color(0xFFB3B3B0),
    focus = Color(0xFF8F8F8C),
    text = Color(0xFF141414),
    text2 = Color(0xFF3A3A3A),
    muted = Color(0xFF707074),
    faint = Color(0xFFA9A9A6),
    accent = Color(0xFF111111),
    onAccent = Color(0xFFFFFFFF),
    accentHover = Color(0xFF2E2E2E),
    accentPress = Color(0xFF474747),
    toast = Color(0xFFFFFFFF),
    ok = Color(0xFF2E9E52),
    warn = Color(0xFFA9761A),
    error = Color(0xFFC0392B),
)

private val LocalNimbusColors = staticCompositionLocalOf { DarkColors }

object NimbusTheme {
    val colors: NimbusColors
        @Composable @ReadOnlyComposable get() = LocalNimbusColors.current
}

/** The desktop's two families, bundled as font resources so nothing is downloaded. */
val Montserrat = FontFamily(
    Font(R.font.montserrat_regular, FontWeight.Normal),
    Font(R.font.montserrat_medium, FontWeight.Medium),
    Font(R.font.montserrat_semibold, FontWeight.SemiBold),
    Font(R.font.montserrat_bold, FontWeight.Bold),
)

val PlexMono = FontFamily(
    Font(R.font.ibm_plex_mono_regular, FontWeight.Normal),
    Font(R.font.ibm_plex_mono_medium, FontWeight.Medium),
)

val NimbusTypography = Typography(
    // Sizes follow the desktop's point sizes scaled to a phone's reading distance.
    titleLarge = TextStyle(fontFamily = Montserrat, fontWeight = FontWeight.SemiBold, fontSize = 22.sp, letterSpacing = (-0.3).sp),
    titleMedium = TextStyle(fontFamily = Montserrat, fontWeight = FontWeight.SemiBold, fontSize = 17.sp, letterSpacing = (-0.2).sp),
    titleSmall = TextStyle(fontFamily = Montserrat, fontWeight = FontWeight.Medium, fontSize = 15.sp),
    bodyLarge = TextStyle(fontFamily = Montserrat, fontWeight = FontWeight.Normal, fontSize = 15.sp, lineHeight = 21.sp),
    bodyMedium = TextStyle(fontFamily = Montserrat, fontWeight = FontWeight.Normal, fontSize = 13.5.sp, lineHeight = 19.sp),
    bodySmall = TextStyle(fontFamily = Montserrat, fontWeight = FontWeight.Normal, fontSize = 12.sp, lineHeight = 17.sp),
    labelLarge = TextStyle(fontFamily = Montserrat, fontWeight = FontWeight.Medium, fontSize = 13.sp),
    labelMedium = TextStyle(fontFamily = Montserrat, fontWeight = FontWeight.Medium, fontSize = 11.5.sp),
    labelSmall = TextStyle(fontFamily = Montserrat, fontWeight = FontWeight.SemiBold, fontSize = 10.sp, letterSpacing = 0.4.sp),
)

/** Tabular numbers for speeds and delays, as the desktop's `tabular=True`. */
val MonoStyle = TextStyle(fontFamily = PlexMono, fontWeight = FontWeight.Medium, fontSize = 13.sp)

/** "dark" | "light" | "auto", as the setting spells it. */
@Composable
fun NimbusTheme(theme: String = "auto", content: @Composable () -> Unit) {
    val dark = when (theme) {
        "dark" -> true
        "light" -> false
        else -> isSystemInDarkTheme()
    }
    val colors = if (dark) DarkColors else LightColors
    CompositionLocalProvider(LocalNimbusColors provides colors) {
        MaterialTheme(
            colorScheme = if (dark) {
                androidx.compose.material3.darkColorScheme(
                    primary = colors.accent,
                    onPrimary = colors.onAccent,
                    background = colors.background,
                    onBackground = colors.text,
                    surface = colors.surface,
                    onSurface = colors.text,
                    surfaceVariant = colors.surface2,
                    onSurfaceVariant = colors.text2,
                    outline = colors.border2,
                    error = colors.error,
                )
            } else {
                androidx.compose.material3.lightColorScheme(
                    primary = colors.accent,
                    onPrimary = colors.onAccent,
                    background = colors.background,
                    onBackground = colors.text,
                    surface = colors.surface,
                    onSurface = colors.text,
                    surfaceVariant = colors.surface2,
                    onSurfaceVariant = colors.text2,
                    outline = colors.border2,
                    error = colors.error,
                )
            },
            typography = NimbusTypography,
            content = content,
        )
    }
}
