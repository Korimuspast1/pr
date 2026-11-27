package com.zalchik.grust.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color

/** Dusk over rain: deep indigo, muted lavender, and one warm lamp for the things that help. */
object Palette {
    val Night = Color(0xFF0B0F1E)
    val Deep = Color(0xFF121A33)
    val Dusk = Color(0xFF1E2A4A)
    val Wall = Color(0xFF27324F)
    val Lavender = Color(0xFFAAB6E8)
    val Mist = Color(0xFF6E7BA6)
    val Lamp = Color(0xFFF0C77E)
    val Rain = Color(0xFF9FC6DA)

    /** Body text on the night background. */
    val onNight = Color(0xFFE6E9F5)

    /** Destructive actions only - never used to colour a mood. */
    val Error = Color(0xFFE3A0A6)

    /** The background of every screen: the sky going down from dusk to night. */
    val sky = Brush.verticalGradient(listOf(Dusk, Deep, Night))
}

private val GrustColors = darkColorScheme(
    primary = Palette.Lavender,
    onPrimary = Palette.Night,
    secondary = Palette.Rain,
    onSecondary = Palette.Night,
    tertiary = Palette.Lamp,
    onTertiary = Palette.Night,
    background = Palette.Night,
    onBackground = Color(0xFFE6E9F5),
    surface = Palette.Deep,
    onSurface = Color(0xFFE6E9F5),
    surfaceVariant = Palette.Wall,
    onSurfaceVariant = Color(0xFFB6BFDC),
    outline = Color(0xFF6E7BA6),
    error = Color(0xFFE3A0A6),
)

@Composable
fun GrustTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = GrustColors, content = content)
}
