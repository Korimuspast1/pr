package m.chatrulet.m

import android.Manifest
import android.content.pm.PackageManager
import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.viewmodel.compose.viewModel
import kotlinx.coroutines.delay

class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        Store.init(this)
        enableEdgeToEdge()

        setContent {
            val vm: AppViewModel = viewModel()
            var gateRules by remember { mutableStateOf(false) }

            val permLauncher = rememberLauncherForActivityResult(
                ActivityResultContracts.RequestMultiplePermissions(),
            ) { res ->
                val cam = res[Manifest.permission.CAMERA] == true
                val mic = res[Manifest.permission.RECORD_AUDIO] == true
                if (cam && mic) {
                    vm.startSearch()
                } else {
                    vm.toast = tr("Camera and microphone are required", "Нужны камера и микрофон")
                }
            }

            fun findWithPermissions() {
                val cam = ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) ==
                    PackageManager.PERMISSION_GRANTED
                val mic = ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) ==
                    PackageManager.PERMISSION_GRANTED
                if (cam && mic) {
                    vm.startSearch()
                } else {
                    permLauncher.launch(arrayOf(Manifest.permission.CAMERA, Manifest.permission.RECORD_AUDIO))
                }
            }

            GlassTheme {
                // Защита окна от скриншотов/записи экрана (опция в настройках)
                LaunchedEffect(Store.secureScreen) {
                    if (Store.secureScreen) {
                        window.setFlags(
                            WindowManager.LayoutParams.FLAG_SECURE,
                            WindowManager.LayoutParams.FLAG_SECURE,
                        )
                    } else {
                        window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE)
                    }
                }

                Box(Modifier.fillMaxSize()) {
                    when {
                        !Store.agreed18 -> {
                            if (gateRules) {
                                RulesScreen(onBack = { gateRules = false })
                            } else {
                                GateScreen(
                                    onAgree = { Store.agreed18 = true },
                                    onRules = { gateRules = true },
                                    onExit = { finish() },
                                )
                            }
                        }

                        else -> {
                            when (vm.screen) {
                                Screen.HOME -> HomeScreen(vm, onFind = ::findWithPermissions)
                                Screen.SETTINGS -> SettingsScreen(vm)
                                Screen.RULES -> RulesScreen(onBack = { vm.screen = Screen.HOME })
                                Screen.BLOCKED -> BlockedScreen(vm)
                            }
                            // Звонок поверх любого экрана
                            if (vm.phase == Phase.CONNECTING || vm.phase == Phase.IN_CALL) {
                                CallScreen(vm)
                            }
                        }
                    }

                    // Тост
                    val t = vm.toast
                    LaunchedEffect(t) {
                        if (t != null) {
                            delay(2600)
                            vm.toast = null
                        }
                    }
                    AnimatedVisibility(
                        visible = t != null,
                        enter = fadeIn(),
                        exit = fadeOut(),
                        modifier = Modifier
                            .align(Alignment.BottomCenter)
                            .padding(bottom = navBottom() + 110.dp),
                    ) {
                        val g = glass
                        Box(
                            Modifier
                                .glassPane(g, RoundedCornerShape(Store.cornerRadius.dp), g.fillStrong)
                                .padding(horizontal = 20.dp, vertical = 12.dp),
                        ) {
                            Text(t ?: "", color = g.text, fontSize = 14.sp)
                        }
                    }
                }
            }
        }
    }
}
