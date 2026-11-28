package com.rootdb.pro

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Apps
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Security
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.rootdb.pro.data.SettingsStore
import com.rootdb.pro.ui.screens.AppDetailScreen
import com.rootdb.pro.ui.screens.AppsScreen
import com.rootdb.pro.ui.screens.DbScreen
import com.rootdb.pro.ui.screens.DecryptScreen
import com.rootdb.pro.ui.screens.FilesScreen
import com.rootdb.pro.ui.screens.GlobalSearchScreen
import com.rootdb.pro.ui.screens.GuardianScreen
import com.rootdb.pro.ui.screens.HomeScreen
import com.rootdb.pro.ui.screens.PrefsScreen
import com.rootdb.pro.ui.screens.SettingsScreen
import com.rootdb.pro.ui.screens.SqlConsoleScreen
import com.rootdb.pro.ui.screens.TableScreen
import com.rootdb.pro.ui.theme.RootDbTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        val settings = SettingsStore(this)
        setContent {
            var themeMode by remember { mutableIntStateOf(settings.themeMode) }
            var dynamicColor by remember { mutableStateOf(settings.dynamicColor) }
            RootDbTheme(themeMode = themeMode, dynamicColor = dynamicColor) {
                RootDbNavHost(
                    onThemeChanged = { themeMode = it },
                    onDynamicColorChanged = { dynamicColor = it }
                )
            }
        }
    }
}

private data class TopLevel(val route: String, val label: String, val icon: @Composable () -> Unit)

@Composable
private fun RootDbNavHost(onThemeChanged: (Int) -> Unit, onDynamicColorChanged: (Boolean) -> Unit) {
    val nav = rememberNavController()
    val topLevels = listOf(
        TopLevel("home", "Главная") { Icon(Icons.Filled.Home, null) },
        TopLevel("apps", "Приложения") { Icon(Icons.Filled.Apps, null) },
        TopLevel("guardian", "Guardian") { Icon(Icons.Filled.Security, null) },
        TopLevel("settings", "Настройки") { Icon(Icons.Filled.Settings, null) }
    )
    val backStack by nav.currentBackStackEntryAsState()
    val currentRoute = backStack?.destination?.route
    val showBar = topLevels.any { it.route == currentRoute }

    Scaffold(
        bottomBar = {
            if (showBar) {
                NavigationBar {
                    topLevels.forEach { t ->
                        NavigationBarItem(
                            selected = currentRoute == t.route,
                            onClick = {
                                nav.navigate(t.route) {
                                    popUpTo("home") { saveState = true }
                                    launchSingleTop = true
                                    restoreState = true
                                }
                            },
                            icon = t.icon,
                            label = { Text(t.label) }
                        )
                    }
                }
            }
        }
    ) { padding ->
        NavHost(
            navController = nav,
            startDestination = "home",
            modifier = Modifier.padding(padding)
        ) {
            composable("home") {
                HomeScreen(
                    onOpenApps = { nav.navigate("apps") },
                    onOpenGuardian = { nav.navigate("guardian") }
                )
            }
            composable("apps") {
                AppsScreen(onAppClick = { pkg -> nav.navigate("app/$pkg") })
            }
            composable("guardian") { GuardianScreen() }
            composable("settings") {
                SettingsScreen(onThemeChanged = onThemeChanged, onDynamicColorChanged = onDynamicColorChanged)
            }
            composable("app/{pkg}", arguments = listOf(navArgument("pkg") { type = NavType.StringType })) { entry ->
                val pkg = entry.arguments?.getString("pkg") ?: ""
                AppDetailScreen(
                    pkg = pkg,
                    onBack = { nav.popBackStack() },
                    onOpenDb = { path -> nav.navigate("db/$pkg/${android.net.Uri.encode(path)}") },
                    onOpenPrefs = { nav.navigate("prefs/$pkg") },
                    onOpenFiles = { nav.navigate("files/$pkg") },
                    onOpenSearch = { nav.navigate("search/$pkg") }
                )
            }
            composable(
                "db/{pkg}/{path}",
                arguments = listOf(
                    navArgument("pkg") { type = NavType.StringType },
                    navArgument("path") { type = NavType.StringType }
                )
            ) { entry ->
                val pkg = entry.arguments?.getString("pkg") ?: ""
                val path = entry.arguments?.getString("path") ?: ""
                DbScreen(
                    pkg = pkg,
                    dbPath = path,
                    onBack = { nav.popBackStack() },
                    onOpenTable = { table -> nav.navigate("table/$pkg/${android.net.Uri.encode(path)}/${android.net.Uri.encode(table)}") },
                    onOpenSql = { nav.navigate("sql/$pkg/${android.net.Uri.encode(path)}") },
                    onOpenDecrypt = { nav.navigate("decrypt/$pkg/${android.net.Uri.encode(path)}") }
                )
            }
            composable(
                "table/{pkg}/{path}/{table}",
                arguments = listOf(
                    navArgument("pkg") { type = NavType.StringType },
                    navArgument("path") { type = NavType.StringType },
                    navArgument("table") { type = NavType.StringType }
                )
            ) { entry ->
                TableScreen(
                    pkg = entry.arguments?.getString("pkg") ?: "",
                    dbPath = entry.arguments?.getString("path") ?: "",
                    table = entry.arguments?.getString("table") ?: "",
                    onBack = { nav.popBackStack() }
                )
            }
            composable(
                "sql/{pkg}/{path}",
                arguments = listOf(
                    navArgument("pkg") { type = NavType.StringType },
                    navArgument("path") { type = NavType.StringType }
                )
            ) { entry ->
                SqlConsoleScreen(
                    pkg = entry.arguments?.getString("pkg") ?: "",
                    dbPath = entry.arguments?.getString("path") ?: "",
                    onBack = { nav.popBackStack() }
                )
            }
            composable(
                "decrypt/{pkg}/{path}",
                arguments = listOf(
                    navArgument("pkg") { type = NavType.StringType },
                    navArgument("path") { type = NavType.StringType }
                )
            ) { entry ->
                DecryptScreen(
                    pkg = entry.arguments?.getString("pkg") ?: "",
                    dbPath = entry.arguments?.getString("path") ?: "",
                    onBack = { nav.popBackStack() }
                )
            }
            composable("prefs/{pkg}", arguments = listOf(navArgument("pkg") { type = NavType.StringType })) { entry ->
                PrefsScreen(pkg = entry.arguments?.getString("pkg") ?: "", onBack = { nav.popBackStack() })
            }
            composable("files/{pkg}", arguments = listOf(navArgument("pkg") { type = NavType.StringType })) { entry ->
                FilesScreen(pkg = entry.arguments?.getString("pkg") ?: "", onBack = { nav.popBackStack() })
            }
            composable("search/{pkg}", arguments = listOf(navArgument("pkg") { type = NavType.StringType })) { entry ->
                val spkg = entry.arguments?.getString("pkg") ?: ""
                GlobalSearchScreen(
                    pkg = spkg,
                    onBack = { nav.popBackStack() },
                    onOpenTable = { dbPath, table ->
                        nav.navigate("table/$spkg/${android.net.Uri.encode(dbPath)}/$table")
                    }
                )
            }
        }
    }
}
