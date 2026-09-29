package app.ourcord.manager.ui.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import app.ourcord.manager.domain.manager.PreferenceManager
import app.ourcord.manager.domain.manager.Theme
import org.koin.androidx.compose.get

@Composable
fun OurCordManagerTheme(
    content: @Composable () -> Unit
) {
    val prefs = get<PreferenceManager>()
    val dynamicColor = prefs.monet
    val darkTheme = when (prefs.theme) {
        Theme.SYSTEM -> isSystemInDarkTheme()
        Theme.DARK -> true
        Theme.LIGHT -> false
    }

    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }

        darkTheme -> darkColorScheme(
            primary = Color(0xFFA78BFA),
            onPrimary = Color(0xFF1B102D),
            secondary = Color(0xFF7C3AED),
            background = Color(0xFF100E16),
            surface = Color(0xFF191622),
            surfaceVariant = Color(0xFF252032),
            outline = Color(0xFF756B86),
        )
        else -> lightColorScheme(
            primary = Color(0xFF6D28D9),
            onPrimary = Color.White,
            secondary = Color(0xFF8B5CF6),
            background = Color(0xFFF8F7FC),
            surface = Color(0xFFFFFFFF),
            surfaceVariant = Color(0xFFEDE9F6),
            outline = Color(0xFF746B7D),
        )
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography,
        content = content
    )
}