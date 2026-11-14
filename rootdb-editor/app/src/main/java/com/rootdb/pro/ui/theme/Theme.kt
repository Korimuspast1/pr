package com.rootdb.pro.ui.theme

import android.os.Build
import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp

/**
 * Material Expressive: крупные скругления, emphasized-движение,
 * динамический цвет (Material You) на Android 12+.
 */
val EmphasizedEasing = CubicBezierEasing(0.05f, 0.7f, 0.1f, 1.0f)
val EmphasizedAccelerateEasing = CubicBezierEasing(0.3f, 0.0f, 0.8f, 0.15f)
val EmphasizedDecelerateEasing = CubicBezierEasing(0.05f, 0.7f, 0.1f, 1.0f)

private val ExpressiveShapes = Shapes(
    extraSmall = RoundedCornerShape(8.dp),
    small = RoundedCornerShape(12.dp),
    medium = RoundedCornerShape(16.dp),
    large = RoundedCornerShape(20.dp),
    extraLarge = RoundedCornerShape(28.dp)
)

private val LightColors = lightColorScheme(
    primary = Color(0xFF3D5AFE),
    onPrimary = Color.White,
    primaryContainer = Color(0xFFDCE1FF),
    onPrimaryContainer = Color(0xFF00105C),
    secondary = Color(0xFF565F71),
    secondaryContainer = Color(0xFFDAE2F9),
    tertiary = Color(0xFF705575),
    tertiaryContainer = Color(0xFFF0DBFD),
    error = Color(0xFFBA1A1A),
    errorContainer = Color(0xFFFFDAD6),
    surface = Color(0xFFFEF7FF),
    surfaceVariant = Color(0xFFE1E2EC),
    surfaceContainerLowest = Color.White,
    surfaceContainerLow = Color(0xFFF7F2FA),
    surfaceContainer = Color(0xFFF3EDF7),
    surfaceContainerHigh = Color(0xFFECE6F0),
    surfaceContainerHighest = Color(0xFFE6E0E9),
    onSurface = Color(0xFF1B1B1F),
    onSurfaceVariant = Color(0xFF44474F),
    outline = Color(0xFF74777F),
    outlineVariant = Color(0xFFC4C6D0)
)

private val DarkColors = darkColorScheme(
    primary = Color(0xFFB4C5FF),
    onPrimary = Color(0xFF002A78),
    primaryContainer = Color(0xFF0040D6),
    onPrimaryContainer = Color(0xFFDCE1FF),
    secondary = Color(0xFFBEC6DC),
    secondaryContainer = Color(0xFF3F4659),
    tertiary = Color(0xFFD0BCFF),
    tertiaryContainer = Color(0xFF573D5E),
    error = Color(0xFFFFB4AB),
    errorContainer = Color(0xFF93000A),
    surface = Color(0xFF131318),
    surfaceVariant = Color(0xFF44474F),
    surfaceContainerLowest = Color(0xFF0E0E13),
    surfaceContainerLow = Color(0xFF1B1B21),
    surfaceContainer = Color(0xFF1F1F26),
    surfaceContainerHigh = Color(0xFF2A2A31),
    surfaceContainerHighest = Color(0xFF35353C),
    onSurface = Color(0xFFE4E1E9),
    onSurfaceVariant = Color(0xFFC4C6D0),
    outline = Color(0xFF8E9099),
    outlineVariant = Color(0xFF44474F)
)

@Composable
fun RootDbTheme(
    themeMode: Int = 0, // 0 system, 1 light, 2 dark
    dynamicColor: Boolean = true,
    content: @Composable () -> Unit
) {
    val darkTheme = when (themeMode) {
        1 -> false
        2 -> true
        else -> isSystemInDarkTheme()
    }
    val context = LocalContext.current
    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }
        darkTheme -> DarkColors
        else -> LightColors
    }
    MaterialTheme(
        colorScheme = colorScheme,
        shapes = ExpressiveShapes,
        content = content
    )
}
