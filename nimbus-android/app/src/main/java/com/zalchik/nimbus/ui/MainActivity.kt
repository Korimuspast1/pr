package com.zalchik.nimbus.ui

import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewmodels.viewModel
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
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
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import com.zalchik.nimbus.data.Paths
import com.zalchik.nimbus.engine.Update
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.screens.AdvancedScreen
import com.zalchik.nimbus.ui.screens.BalancersScreen
import com.zalchik.nimbus.ui.screens.DnsScreen
import com.zalchik.nimbus.ui.screens.HomeScreen
import com.zalchik.nimbus.ui.screens.ImportSheet
import com.zalchik.nimbus.ui.screens.LogsScreen
import com.zalchik.nimbus.ui.screens.NodePickerScreen
import com.zalchik.nimbus.ui.screens.RoutingScreen
import com.zalchik.nimbus.ui.screens.ServersScreen
import com.zalchik.nimbus.ui.screens.SettingsPage
import com.zalchik.nimbus.ui.screens.SettingsScreen
import com.zalchik.nimbus.ui.screens.SubscriptionsScreen
import com.zalchik.nimbus.ui.screens.writeLogExport
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.theme.NimbusTheme

/**
 * The one activity: a three-tab layout (connection, servers, settings) with the secondary screens
 * stacked over it. It also owns everything that has to be an activity — Android's VPN permission
 * prompt, the QR scanner, the file picker, and installing an update.
 */
class MainActivity : ComponentActivity() {
    private lateinit var app: AppState

    /** Text arriving from another app: a deep link, a share, or an opened file. */
    private var pendingImport = mutableStateOf<String?>(null)

    private val scanner = registerForActivityResult(ScanContract()) { result ->
        val text = result.contents
        if (text != null) app.import(text)
    }

    private val filePicker = registerForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        uri ?: return@registerForActivityResult
        val text = readUri(uri)
        if (text.isNullOrEmpty()) {
            app.say(I18n.tr("Не удалось прочитать файл"), error = true)
        } else {
            app.import(text)
        }
    }

    private val vpnPermission = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) {
        app.onPermissionResult(it.resultCode == RESULT_OK)
    }

    private val notificationPermission =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        app = viewModel<AppState>()
        handle(intent)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            notificationPermission.launch(android.Manifest.permission.POST_NOTIFICATIONS)
        }
        setContent {
            NimbusTheme(app.settings.theme) {
                NimbusApp(
                    app = app,
                    pendingImport = pendingImport.value,
                    onConsumeImport = { pendingImport.value = null },
                    onRequestVpn = { intent -> vpnPermission.launch(intent) },
                    onScanQr = { scanQr() },
                    onOpenFile = {
                        filePicker.launch(arrayOf("text/*", "application/json", "application/octet-stream"))
                    },
                    onInstall = { file -> install(file) },
                )
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handle(intent)
    }

    /** Whatever the intent carries — a link, a shared text, a file — becomes an import. */
    private fun handle(intent: Intent?) {
        intent ?: return
        when (intent.action) {
            Intent.ACTION_SEND -> intent.getStringExtra(Intent.EXTRA_TEXT)?.let { pendingImport.value = it }
            Intent.ACTION_VIEW -> {
                val data = intent.data
                if (data == null) return
                if (data.scheme == "content" || data.scheme == "file") {
                    pendingImport.value = readUri(data)
                } else {
                    pendingImport.value = data.toString()
                }
            }
        }
    }

    private fun readUri(uri: Uri): String? = try {
        contentResolver.openInputStream(uri)?.use { input ->
            input.reader().readText()
        }
    } catch (e: Exception) {
        null
    }

    private fun scanQr() {
        val options = ScanOptions()
            .setDesiredBarcodeFormats(ScanOptions.QR_CODE)
            .setPrompt(I18n.tr("Наведите камеру на QR-код"))
            .setBeepEnabled(false)
            .setOrientationLocked(false)
        scanner.launch(options)
    }

    /** Hand the downloaded APK to the system installer. */
    private fun install(file: java.io.File) {
        val uri = FileProvider.getUriForFile(this, "${packageName}.files", file)
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        startActivity(intent)
    }

    companion object {
        /** What the clipboard holds, if it looks like something Nimbus can import. */
        fun clipboardText(context: Context): String {
            val manager = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
            val clip = manager.primaryClip ?: return ""
            if (clip.itemCount == 0) return ""
            return clip.getItemAt(0)?.text?.toString() ?: ""
        }
    }
}

private enum class Tab(val label: String, val icon: ImageVector) {
    Home("Подключение", NiIcons.PowerOn),
    Servers("Серверы", NiIcons.List),
    Settings("Настройки", NiIcons.Settings),
}

@Composable
private fun NimbusApp(
    app: AppState,
    pendingImport: String?,
    onConsumeImport: () -> Unit,
    onRequestVpn: (Intent) -> Unit,
    onScanQr: () -> Unit,
    onOpenFile: () -> Unit,
    onInstall: (java.io.File) -> Unit,
) {
    val colors = NimbusTheme.colors
    val context = LocalContext.current
    var tab by remember { mutableStateOf(Tab.Home) }
    var page by remember { mutableStateOf(SettingsPage.None) }
    var importing by remember { mutableStateOf(false) }
    // The group whose nodes are on screen, or null while the picker is closed.
    var nodes by remember { mutableStateOf<String?>(null) }
    var download by remember { mutableStateOf<Update.Release?>(null) }
    var progress by remember { mutableStateOf(0f) }

    // Android's VPN prompt, when it is still unanswered.
    LaunchedEffect(app.permissionIntent) {
        app.permissionIntent?.let(onRequestVpn)
    }

    // Text from another app.
    LaunchedEffect(pendingImport) {
        val text = pendingImport
        if (!text.isNullOrBlank()) {
            onConsumeImport()
            app.import(text)
        }
    }

    // Notices as a small toast, as desktop paints them.
    val current by app.notice.collectAsState()

    Box(Modifier.fillMaxSize().background(colors.background)) {
        Column(Modifier.fillMaxSize()) {
            BrandBar(onOpenLogs = { page = SettingsPage.Logs })
            Box(Modifier.weight(1f)) {
                when (tab) {
                    Tab.Home -> HomeScreen(
                        app = app,
                        onOpenServers = { tab = Tab.Servers },
                        onOpenRouting = {
                            tab = Tab.Settings
                            page = SettingsPage.Routing
                        },
                        onOpenNodes = { nodes = it },
                    )

                    Tab.Servers -> ServersScreen(
                        app = app,
                        onImport = { importing = true },
                        onScanQr = onScanQr,
                        onOpenBalancers = {
                            tab = Tab.Settings
                            page = SettingsPage.Balancers
                        },
                    )

                    Tab.Settings -> SettingsScreen(
                        app = app,
                        page = page,
                        onOpen = { page = it },
                        onUpdate = { download = app.update },
                        onShareLogs = {
                            com.zalchik.nimbus.ui.components.shareFile(
                                context,
                                writeLogExport(app),
                                "text/plain",
                            )
                        },
                    )
                }
            }
            BottomBar(tab = tab, onSelect = { tab = it; page = SettingsPage.None })
        }

        // Secondary screens cover the tabs, as desktop's dialogs cover the window.
        when (page) {
            SettingsPage.Routing -> RoutingScreen(app) { page = SettingsPage.None }
            SettingsPage.Dns -> DnsScreen(app) { page = SettingsPage.None }
            SettingsPage.Advanced -> AdvancedScreen(app) { page = SettingsPage.None }
            SettingsPage.Subscriptions -> SubscriptionsScreen(app) { page = SettingsPage.None }
            SettingsPage.Balancers -> BalancersScreen(app, onOpenNodes = { nodes = it }) {
                page = SettingsPage.None
            }
            SettingsPage.Logs -> LogsScreen(app) { page = SettingsPage.None }
            SettingsPage.None -> Unit
        }

        nodes?.let { tag ->
            NodePickerScreen(app, groupTag = tag) { nodes = null }
        }

        current?.let { item ->
            Toast(
                text = item.text,
                error = item.error,
                modifier = Modifier.align(Alignment.BottomCenter),
            )
        }
    }

    if (importing) {
        ImportSheet(
            app = app,
            clipboard = MainActivity.clipboardText(context),
            onScanQr = {
                importing = false
                onScanQr()
            },
            onOpenFile = {
                importing = false
                onOpenFile()
            },
            onDismiss = { importing = false },
        )
    }

    download?.let { release ->
        UpdateDialog(
            release = release,
            progress = progress,
            onDownload = {
                progress = 0f
                app.downloadUpdate(
                    release,
                    onProgress = { done, total ->
                        progress = if (total > 0) done.toFloat() / total else 0f
                    },
                    onDone = { file ->
                        download = null
                        onInstall(file)
                    },
                )
            },
            onSkip = {
                app.skipUpdate(release.version)
                download = null
            },
            onDismiss = { download = null },
        )
    }
}

/** The top bar: the wordmark, the version, and the way to the log. */
@Composable
private fun BrandBar(onOpenLogs: () -> Unit) {
    val colors = NimbusTheme.colors
    Row(
        Modifier
            .fillMaxWidth()
            .padding(horizontal = 18.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            "nimbus",
            style = MaterialTheme.typography.titleLarge,
            color = colors.text,
        )
        Spacer(Modifier.width(8.dp))
        Text(
            Paths.VERSION,
            style = MaterialTheme.typography.labelSmall,
            color = colors.faint,
            modifier = Modifier
                .clip(RoundedCornerShape(5.dp))
                .border(1.dp, colors.border, RoundedCornerShape(5.dp))
                .padding(horizontal = 5.dp, vertical = 2.dp),
        )
        Spacer(Modifier.weight(1f))
        Icon(
            NiIcons.Logs,
            I18n.tr("Журнал"),
            tint = colors.muted,
            modifier = Modifier
                .size(34.dp)
                .clip(RoundedCornerShape(9.dp))
                .clickable(onClick = onOpenLogs)
                .padding(8.dp),
        )
    }
}

@Composable
private fun BottomBar(tab: Tab, onSelect: (Tab) -> Unit) {
    val colors = NimbusTheme.colors
    Row(
        Modifier
            .fillMaxWidth()
            .background(colors.panel)
            .border(1.dp, colors.border)
            .padding(vertical = 6.dp),
    ) {
        for (item in Tab.entries) {
            val active = item == tab
            Column(
                Modifier
                    .weight(1f)
                    .clip(RoundedCornerShape(10.dp))
                    .clickable { onSelect(item) }
                    .padding(vertical = 8.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(3.dp),
            ) {
                Icon(
                    item.icon,
                    null,
                    tint = if (active) colors.text else colors.muted,
                    modifier = Modifier.size(20.dp),
                )
                Text(
                    I18n.tr(item.label),
                    style = MaterialTheme.typography.labelSmall,
                    color = if (active) colors.text else colors.muted,
                )
            }
        }
    }
}

/** A short message over the content, as desktop paints its toasts. */
@Composable
private fun Toast(text: String, error: Boolean, modifier: Modifier = Modifier) {
    val colors = NimbusTheme.colors
    AnimatedVisibility(visible = true, modifier = modifier) {
        Box(
            Modifier
                .padding(bottom = 96.dp, start = 20.dp, end = 20.dp)
                .clip(RoundedCornerShape(11.dp))
                .background(colors.toast)
                .border(1.dp, if (error) colors.error else colors.border2, RoundedCornerShape(11.dp))
                .padding(horizontal = 16.dp, vertical = 12.dp),
        ) {
            Text(
                text,
                style = MaterialTheme.typography.bodyMedium,
                color = if (error) colors.error else colors.text,
            )
        }
    }
}

/** "Есть новая версия" with what changed, a progress bar while it downloads, and a skip. */
@Composable
private fun UpdateDialog(
    release: Update.Release,
    progress: Float,
    onDownload: () -> Unit,
    onSkip: () -> Unit,
    onDismiss: () -> Unit,
) {
    val colors = NimbusTheme.colors
    var notes by remember { mutableStateOf(false) }
    androidx.compose.material3.AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = colors.surface,
        title = {
            Text(
                I18n.tr("Новая версия {version}", values = mapOf("version" to release.version)),
                style = MaterialTheme.typography.titleMedium,
                color = colors.text,
            )
        },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(
                    I18n.tr("Сейчас {version}", values = mapOf("version" to Paths.VERSION)),
                    style = MaterialTheme.typography.bodySmall,
                    color = colors.muted,
                )
                if (progress > 0f) {
                    androidx.compose.material3.LinearProgressIndicator(
                        progress = { progress },
                        modifier = Modifier.fillMaxWidth().height(3.dp),
                        color = colors.accent,
                        trackColor = colors.surface2,
                    )
                }
                if (notes) {
                    Text(
                        Update.notes(release).joinToString("\n"),
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.text2,
                    )
                } else if (release.body.isNotBlank()) {
                    Text(
                        I18n.tr("Что нового"),
                        style = MaterialTheme.typography.labelLarge,
                        color = colors.text2,
                        modifier = Modifier
                            .clip(RoundedCornerShape(7.dp))
                            .clickable { notes = true }
                            .padding(vertical = 4.dp),
                    )
                }
            }
        },
        confirmButton = {
            androidx.compose.material3.TextButton(onClick = onDownload, enabled = progress == 0f) {
                Text(I18n.tr("Скачать"), color = colors.text)
            }
        },
        dismissButton = {
            Row {
                androidx.compose.material3.TextButton(onClick = onSkip) {
                    Text(I18n.tr("Пропустить"), color = colors.muted)
                }
                androidx.compose.material3.TextButton(onClick = onDismiss) {
                    Text(I18n.tr("Позже"), color = colors.muted)
                }
            }
        },
    )
}
