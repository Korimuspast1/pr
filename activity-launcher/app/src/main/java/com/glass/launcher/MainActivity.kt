package com.glass.launcher

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.spring
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Favorite
import androidx.compose.material.icons.rounded.History
import androidx.compose.material.icons.rounded.Search
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.IntOffset

sealed interface Screen {
    data object Tabs : Screen
    data class Package(val pkg: String) : Screen
    data class Editor(val data: ShortcutData) : Screen
    data object Settings : Screen
}

data class Entry(val screen: Screen, val depth: Int)

class Nav {
    val stack = mutableStateListOf<Screen>(Screen.Tabs)
    fun push(s: Screen) { stack.add(s) }
    fun pop() { if (stack.size > 1) stack.removeAt(stack.lastIndex) }
}

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)
        setContent {
            GlassTheme {
                val g = glass
                LaunchedEffect(g.dark) {
                    val t = Color.Transparent.toArgb()
                    val style = if (g.dark) SystemBarStyle.dark(t) else SystemBarStyle.light(t, 0x33000000)
                    enableEdgeToEdge(statusBarStyle = style, navigationBarStyle = style)
                }
                Root()
            }
        }
    }
}

@Composable
fun Root() {
    val nav = remember { Nav() }
    val ctx = LocalContext.current
    var tab by rememberSaveable { mutableIntStateOf(0) }
    LaunchedEffect(Unit) { Pkg.load(ctx) }

    BackHandler(enabled = nav.stack.size > 1) { nav.pop() }
    BackHandler(enabled = nav.stack.size == 1 && tab != 0) { tab = 0 }

    val entry = Entry(nav.stack.last(), nav.stack.size)
    AnimatedContent(
        targetState = entry,
        transitionSpec = {
            val push = targetState.depth >= initialState.depth
            val sp = spring<IntOffset>(dampingRatio = 0.85f, stiffness = 420f)
            if (push) {
                (slideInHorizontally(sp) { it / 3 } + fadeIn() + scaleIn(initialScale = 0.96f)) togetherWith
                    (fadeOut() + scaleOut(targetScale = 0.94f))
            } else {
                (fadeIn() + scaleIn(initialScale = 0.94f)) togetherWith
                    (slideOutHorizontally(sp) { it / 3 } + fadeOut() + scaleOut(targetScale = 0.96f))
            }
        },
        contentKey = { it.screen to it.depth },
        label = "nav",
        modifier = Modifier.fillMaxSize(),
    ) { e ->
        Box(Modifier.fillMaxSize()) {
            when (val s = e.screen) {
                Screen.Tabs -> TabsHost(nav, tab) { tab = it }
                is Screen.Package -> PackageScreen(nav, s.pkg)
                is Screen.Editor -> EditorScreen(nav, s.data)
                Screen.Settings -> SettingsScreen(nav)
            }
        }
    }
}

@Composable
fun TabsHost(nav: Nav, tab: Int, onTab: (Int) -> Unit) {
    val items = listOf(
        NavItem(tr("All", "Все"), Icons.Rounded.Search),
        NavItem(tr("Favorites", "Избранное"), Icons.Rounded.Favorite),
        NavItem(tr("Recents", "Недавние"), Icons.Rounded.History),
    )
    val appsState = remember { AppsState() }
    GlassScaffold(
        topBar = { haze ->
            AnimatedContent(
                targetState = tab,
                transitionSpec = { (fadeIn() + scaleIn(initialScale = 0.97f)) togetherWith fadeOut() },
                label = "topbar",
            ) { t ->
                when (t) {
                    0 -> AppsTopBar(haze, nav, appsState)
                    1 -> FavoritesTopBar(haze, nav)
                    else -> RecentsTopBar(haze, nav)
                }
            }
        },
        bottomBar = { haze -> GlassBottomBar(haze, items, tab, onTab) },
    ) { pad ->
        AnimatedContent(
            targetState = tab,
            transitionSpec = {
                val dir = if (targetState > initialState) 1 else -1
                val sp = spring<IntOffset>(dampingRatio = 0.9f, stiffness = 500f)
                (slideInHorizontally(sp) { dir * it / 5 } + fadeIn()) togetherWith
                    (slideOutHorizontally(sp) { -dir * it / 5 } + fadeOut())
            },
            label = "tabs",
        ) { t ->
            when (t) {
                0 -> AppsContent(nav, appsState, pad)
                1 -> FavoritesContent(nav, pad)
                else -> RecentsContent(nav, pad)
            }
        }
    }
}
