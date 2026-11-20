package m.chatrulet.m

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.Send
import androidx.compose.material.icons.rounded.BlurOff
import androidx.compose.material.icons.rounded.BlurOn
import androidx.compose.material.icons.rounded.Block
import androidx.compose.material.icons.rounded.Cameraswitch
import androidx.compose.material.icons.rounded.ChatBubble
import androidx.compose.material.icons.rounded.Close
import androidx.compose.material.icons.rounded.EmojiEmotions
import androidx.compose.material.icons.rounded.Favorite
import androidx.compose.material.icons.rounded.Female
import androidx.compose.material.icons.rounded.Flag
import androidx.compose.material.icons.rounded.Male
import androidx.compose.material.icons.rounded.Mic
import androidx.compose.material.icons.rounded.MicOff
import androidx.compose.material.icons.rounded.SkipNext
import androidx.compose.material.icons.rounded.Videocam
import androidx.compose.material.icons.rounded.VideocamOff
import androidx.compose.material.icons.rounded.VolumeOff
import androidx.compose.material.icons.rounded.VolumeUp
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.lerp
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import dev.chrisbanes.haze.haze
import kotlinx.coroutines.delay
import org.webrtc.RendererCommon
import org.webrtc.SurfaceViewRenderer

val REACTIONS = listOf("👍", "😂", "❤️", "😮", "🔥")

@Composable
fun CallScreen(vm: AppViewModel) {
    val g = glass
    val ctx = LocalContext.current
    var showReport by remember { mutableStateOf(false) }
    var showBlockConfirm by remember { mutableStateOf(false) }
    var showReacts by remember { mutableStateOf(false) }
    var micOn by remember(vm.callId) { mutableStateOf(vm.engine.micOn) }
    var camOn by remember(vm.callId) { mutableStateOf(vm.engine.camOn) }
    var frontCam by remember(vm.callId) { mutableStateOf(vm.engine.front) }

    // ── рендереры WebRTC ──
    val remoteRenderer = remember { SurfaceViewRenderer(ctx) }
    val localRenderer = remember { SurfaceViewRenderer(ctx) }
    var renderersReady by remember { mutableStateOf(false) }

    LaunchedEffect(Unit) {
        try {
            remoteRenderer.init(vm.engine.egl.eglBaseContext, null)
            remoteRenderer.setScalingType(RendererCommon.ScalingType.SCALE_ASPECT_FILL)
            localRenderer.init(vm.engine.egl.eglBaseContext, null)
            localRenderer.setScalingType(RendererCommon.ScalingType.SCALE_ASPECT_FILL)
            localRenderer.setMirror(true)
            renderersReady = true
        } catch (t: Throwable) {
        }
    }
    LaunchedEffect(vm.callId, vm.remoteTrack, renderersReady) {
        if (renderersReady) {
            try {
                vm.remoteTrack?.addSink(remoteRenderer)
            } catch (t: Throwable) {
            }
            try {
                if (vm.engine.camOn) vm.engine.localVideo?.addSink(localRenderer)
                localRenderer.setMirror(vm.engine.front)
            } catch (t: Throwable) {
            }
        }
    }
    androidx.compose.runtime.DisposableEffect(Unit) {
        onDispose {
            try { remoteRenderer.release() } catch (t: Throwable) {}
            try { localRenderer.release() } catch (t: Throwable) {}
        }
    }

    // автоскрытие реакции
    LaunchedEffect(vm.reaction) {
        if (vm.reaction != null) {
            delay(1800)
            vm.reaction = null
        }
    }
    LaunchedEffect(vm.chatOpen) {
        if (vm.chatOpen) vm.markRead()
    }

    Box(Modifier.fillMaxSize().background(Color.Black)) {
        // удалённое видео на весь экран
        AndroidView(factory = { remoteRenderer }, modifier = Modifier.fillMaxSize())

        // затемнение для читаемости стекла
        Box(
            Modifier.fillMaxSize().background(
                Brush.verticalGradient(
                    listOf(Color.Black.copy(alpha = .45f), Color.Transparent, Color.Transparent, Color.Black.copy(alpha = .6f)),
                ),
            ),
        )

        // «подключение…»
        if (vm.phase == Phase.CONNECTING) {
            Column(
                Modifier.align(Alignment.Center).padding(32.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Box(
                    Modifier.size(88.dp).glassPane(g, CircleShape, g.fillStrong),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(Icons.Rounded.Videocam, null, tint = g.accent, modifier = Modifier.size(40.dp))
                }
                Spacer(Modifier.height(16.dp))
                Text(
                    tr("Connecting…", "Подключение…"),
                    color = Color.White, fontSize = 18.sp, fontWeight = FontWeight.SemiBold,
                )
                Spacer(Modifier.height(4.dp))
                Text(
                    tr("P2P tunnel is being established", "Устанавливаем P2P-туннель"),
                    color = Color.White.copy(alpha = .7f), fontSize = 13.sp,
                )
            }
        }

        // собеседник выключил камеру
        if (vm.phase == Phase.IN_CALL && (!vm.peerCamOn || !vm.remoteHasVideo)) {
            Column(
                Modifier.align(Alignment.Center).padding(32.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Box(
                    Modifier.size(72.dp).glassPane(g, CircleShape, g.fillStrong),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(Icons.Rounded.VideocamOff, null, tint = Color.White.copy(alpha = .8f), modifier = Modifier.size(32.dp))
                }
                Spacer(Modifier.height(10.dp))
                Text(
                    tr("Partner's camera is off", "Камера собеседника выключена"),
                    color = Color.White.copy(alpha = .85f), fontSize = 14.sp,
                )
            }
        }

        // ── верхняя панель ──
        Row(
            Modifier
                .align(Alignment.TopCenter)
                .padding(top = statusTop() + 8.dp, start = 12.dp, end = 12.dp)
                .fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Row(
                Modifier
                    .glassPane(g, CircleShape, Color.Black.copy(alpha = .35f))
                    .padding(horizontal = 14.dp, vertical = 8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(flagFor(vm.peer.country), fontSize = 18.sp)
                Spacer(Modifier.width(8.dp))
                Text(
                    vm.peer.nick.ifBlank { tr("Stranger", "Собеседник") },
                    color = Color.White, fontSize = 14.sp, fontWeight = FontWeight.SemiBold, maxLines = 1,
                )
                if (vm.peer.gender == "m") {
                    Spacer(Modifier.width(4.dp))
                    Icon(Icons.Rounded.Male, null, tint = Color(0xFF7EB6FF), modifier = Modifier.size(16.dp))
                } else if (vm.peer.gender == "f") {
                    Spacer(Modifier.width(4.dp))
                    Icon(Icons.Rounded.Female, null, tint = Color(0xFFFF9BCB), modifier = Modifier.size(16.dp))
                }
            }
            Spacer(Modifier.weight(1f))
            QualityDots(vm.quality)
            Spacer(Modifier.width(8.dp))
            Text(
                formatTime(vm.callSeconds) + if (Store.autoNext > 0 && vm.phase == Phase.IN_CALL)
                    " · ⏱" + (Store.autoNext - vm.callSeconds).coerceAtLeast(0) else "",
                color = Color.White, fontSize = 13.sp, fontWeight = FontWeight.Medium,
                modifier = Modifier
                    .glassPane(g, CircleShape, Color.Black.copy(alpha = .35f))
                    .padding(horizontal = 12.dp, vertical = 8.dp),
            )
            Spacer(Modifier.width(6.dp))
            CallIconButton(Icons.Rounded.Flag, tr("Report", "Пожаловаться")) { showReport = true }
            Spacer(Modifier.width(6.dp))
            CallIconButton(Icons.Rounded.Block, tr("Block", "Заблокировать")) { showBlockConfirm = true }
        }

        // ── своё видео (PiP) ──
        Box(
            Modifier
                .align(Alignment.TopEnd)
                .padding(top = statusTop() + 66.dp, end = 12.dp)
                .size(width = 104.dp, height = 150.dp)
                .clip(RoundedCornerShape(18.dp))
                .glassRim(g, RoundedCornerShape(18.dp)),
        ) {
            AndroidView(factory = { localRenderer }, modifier = Modifier.fillMaxSize())
            if (Store.blurCam) {
                Box(
                    Modifier.align(Alignment.TopEnd).padding(6.dp).size(22.dp)
                        .clip(CircleShape).background(Color.Black.copy(alpha = .5f)),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(Icons.Rounded.BlurOn, null, tint = Color.White, modifier = Modifier.size(13.dp))
                }
            }
        }

        // ── реакция-эмодзи по центру ──
        AnimatedVisibility(
            visible = vm.reaction != null,
            enter = scaleIn() + fadeIn(),
            exit = fadeOut(),
            modifier = Modifier.align(Alignment.Center),
        ) {
            Text(vm.reaction ?: "", fontSize = 96.sp)
        }

        // ── нижняя часть ──
        Column(Modifier.align(Alignment.BottomCenter).fillMaxWidth()) {
            // «печатает…»
            if (vm.peerTyping) {
                Text(
                    tr("typing…", "печатает…"),
                    color = Color.White.copy(alpha = .75f), fontSize = 12.sp,
                    modifier = Modifier.padding(start = 20.dp, bottom = 4.dp),
                )
            }

            // чат
            if (vm.chatOpen) {
                ChatPanel(vm)
            } else {
                // последние сообщения поверх видео
                val last = vm.messages.takeLast(2)
                Column(Modifier.padding(horizontal = 14.dp, vertical = 6.dp)) {
                    last.forEach { m ->
                        if (!m.mine) {
                            MessageBubble(m, Modifier.padding(bottom = 6.dp))
                        }
                    }
                }
            }

            // реакции
            if (showReacts) {
                Row(
                    Modifier
                        .align(Alignment.CenterHorizontally)
                        .padding(bottom = 8.dp)
                        .glassPane(g, CircleShape, Color.Black.copy(alpha = .45f))
                        .padding(horizontal = 10.dp, vertical = 6.dp),
                ) {
                    REACTIONS.forEach { e ->
                        Text(
                            e, fontSize = 26.sp,
                            modifier = Modifier
                                .padding(horizontal = 6.dp)
                                .clickable(
                                    indication = null,
                                    interactionSource = remember { MutableInteractionSource() },
                                ) {
                                    vm.sendReaction(e)
                                    showReacts = false
                                },
                        )
                    }
                }
            }

            // ряд медиа-кнопок
            Row(
                Modifier
                    .align(Alignment.CenterHorizontally)
                    .padding(bottom = 8.dp),
                horizontalArrangement = Arrangement.Center,
            ) {
                CallIconButton(
                    if (micOn) Icons.Rounded.Mic else Icons.Rounded.MicOff,
                    tr("Microphone", "Микрофон"),
                    active = !micOn,
                ) { vm.toggleMic(); micOn = vm.engine.micOn }
                Spacer(Modifier.width(8.dp))
                CallIconButton(
                    if (camOn) Icons.Rounded.Videocam else Icons.Rounded.VideocamOff,
                    tr("Camera", "Камера"),
                    active = !camOn,
                ) { vm.toggleCam(); camOn = vm.engine.camOn }
                Spacer(Modifier.width(8.dp))
                CallIconButton(
                    if (Store.blurCam) Icons.Rounded.BlurOn else Icons.Rounded.BlurOff,
                    tr("Blur my video", "Блюр моего видео"),
                    active = Store.blurCam,
                ) { vm.toggleBlur() }
                Spacer(Modifier.width(8.dp))
                CallIconButton(Icons.Rounded.Cameraswitch, tr("Switch camera", "Сменить камеру")) {
                    vm.switchCamera()
                    frontCam = vm.engine.front
                    try { localRenderer.setMirror(frontCam) } catch (t: Throwable) {}
                }
                Spacer(Modifier.width(8.dp))
                CallIconButton(
                    if (Store.speakerOn) Icons.Rounded.VolumeUp else Icons.Rounded.VolumeOff,
                    tr("Speaker", "Динамик"),
                    active = !Store.speakerOn,
                ) { vm.toggleSpeaker() }
                Spacer(Modifier.width(8.dp))
                CallIconButton(Icons.Rounded.EmojiEmotions, tr("Reactions", "Реакции"), active = showReacts) {
                    showReacts = !showReacts
                }
                Spacer(Modifier.width(8.dp))
                CallIconButton(Icons.Rounded.Favorite, tr("Like", "Нравится")) { vm.likePeer() }
            }

            // ряд действий
            Row(
                Modifier
                    .fillMaxWidth()
                    .padding(start = 14.dp, end = 14.dp, bottom = navBottom() + 12.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                // стоп
                Box(
                    Modifier
                        .size(54.dp)
                        .clip(CircleShape)
                        .background(Color(0xFFE53935).copy(alpha = .85f))
                        .glassRim(g.copy(stroke = Color.White.copy(alpha = .5f)), CircleShape)
                        .clickable(
                            indication = null,
                            interactionSource = remember { MutableInteractionSource() },
                        ) { vm.stop() },
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(Icons.Rounded.Close, tr("Stop", "Стоп"), tint = Color.White, modifier = Modifier.size(26.dp))
                }
                Spacer(Modifier.width(10.dp))
                // чат с бейджем непрочитанных
                Box {
                    CallIconButton(Icons.Rounded.ChatBubble, tr("Chat", "Чат"), active = vm.chatOpen, size = 54.dp) {
                        vm.chatOpen = !vm.chatOpen
                    }
                    if (vm.unread > 0 && !vm.chatOpen) {
                        Box(
                            Modifier
                                .align(Alignment.TopEnd)
                                .clip(CircleShape)
                                .background(Color(0xFFE53935))
                                .padding(horizontal = 6.dp, vertical = 1.dp),
                        ) {
                            Text("${vm.unread}", color = Color.White, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                        }
                    }
                }
                Spacer(Modifier.width(10.dp))
                // далее
                Box(
                    Modifier
                        .weight(1f)
                        .height(54.dp)
                        .clip(CircleShape)
                        .background(Brush.horizontalGradient(listOf(g.accent, lerp(g.accent, g.accent2, 0.65f))))
                        .glassRim(g.copy(stroke = Color.White.copy(alpha = .6f)), CircleShape)
                        .clickable(
                            indication = null,
                            interactionSource = remember { MutableInteractionSource() },
                        ) { vm.next() },
                    contentAlignment = Alignment.Center,
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            tr("Next", "Далее"),
                            color = Color.White, fontSize = 16.sp, fontWeight = FontWeight.Bold,
                        )
                        Spacer(Modifier.width(6.dp))
                        Icon(Icons.Rounded.SkipNext, null, tint = Color.White, modifier = Modifier.size(24.dp))
                    }
                }
            }
        }
    }

    if (showReport) {
        ReportDialog(
            onPick = { vm.report(it); showReport = false },
            onDismiss = { showReport = false },
        )
    }
    if (showBlockConfirm) {
        BlockConfirmDialog(
            onConfirm = { vm.blockPeer(); showBlockConfirm = false },
            onDismiss = { showBlockConfirm = false },
        )
    }
}

@Composable
private fun CallIconButton(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    desc: String,
    active: Boolean = false,
    size: androidx.compose.ui.unit.Dp = 46.dp,
    onClick: () -> Unit,
) {
    val g = glass
    Box(
        Modifier
            .size(size)
            .glassPane(
                g, CircleShape,
                if (active) g.accent.copy(alpha = .55f) else Color.Black.copy(alpha = .4f),
            )
            .clickable(
                indication = null,
                interactionSource = remember { MutableInteractionSource() },
            ) { onClick() },
        contentAlignment = Alignment.Center,
    ) {
        Icon(icon, desc, tint = Color.White, modifier = Modifier.size(22.dp))
    }
}

@Composable
private fun QualityDots(quality: Int) {
    Row(
        Modifier
            .glassPane(glass, CircleShape, Color.Black.copy(alpha = .35f))
            .padding(horizontal = 10.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        for (i in 1..3) {
            val on = quality >= i
            Box(
                Modifier
                    .padding(horizontal = 2.dp)
                    .size(width = 4.dp, height = (5 + i * 4).dp)
                    .clip(RoundedCornerShape(2.dp))
                    .background(
                        when {
                            !on -> Color.White.copy(alpha = .25f)
                            quality == 1 -> Color(0xFFFF5252)
                            quality == 2 -> Color(0xFFFFC107)
                            else -> Color(0xFF4CAF50)
                        },
                    ),
            )
        }
    }
}

@Composable
private fun MessageBubble(m: ChatMsg, modifier: Modifier = Modifier) {
    val g = glass
    Box(modifier.fillMaxWidth()) {
        Text(
            m.text,
            color = Color.White, fontSize = 14.sp,
            modifier = Modifier
                .align(if (m.mine) Alignment.CenterEnd else Alignment.CenterStart)
                .widthIn(max = 280.dp)
                .then(
                    if (m.mine) {
                        Modifier
                            .clip(RoundedCornerShape(16.dp))
                            .background(Brush.horizontalGradient(listOf(g.accent, lerp(g.accent, g.accent2, .6f))))
                    } else {
                        Modifier.glassPane(g, RoundedCornerShape(16.dp), Color.Black.copy(alpha = .4f))
                    },
                )
                .padding(horizontal = 12.dp, vertical = 8.dp),
        )
    }
}

@Composable
private fun ChatPanel(vm: AppViewModel) {
    val g = glass
    val listState = rememberLazyListState()
    var input by remember { mutableStateOf("") }
    LaunchedEffect(vm.messages.size) {
        if (vm.messages.isNotEmpty()) listState.animateScrollToItem(vm.messages.lastIndex)
    }
    Column(
        Modifier
            .fillMaxWidth()
            .height(300.dp)
            .padding(horizontal = 12.dp)
            .glassPane(g, RoundedCornerShape(Store.cornerRadius.dp), Color.Black.copy(alpha = .45f))
            .padding(10.dp),
    ) {
        LazyColumn(Modifier.weight(1f), state = listState) {
            items(vm.messages) { m ->
                MessageBubble(m, Modifier.padding(bottom = 6.dp))
            }
            if (vm.messages.isEmpty()) {
                item {
                    Text(
                        tr("Say hi! Messages are end-to-end encrypted 🔒", "Напиши привет! Сообщения зашифрованы 🔒"),
                        color = Color.White.copy(alpha = .6f), fontSize = 13.sp,
                        textAlign = TextAlign.Center,
                        modifier = Modifier.fillMaxWidth().padding(16.dp),
                    )
                }
            }
        }
        Spacer(Modifier.height(8.dp))
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                Modifier
                    .weight(1f)
                    .glassPane(g, CircleShape, Color.Black.copy(alpha = .35f))
                    .padding(horizontal = 16.dp, vertical = 12.dp),
            ) {
                if (input.isEmpty()) {
                    Text(tr("Message…", "Сообщение…"), color = Color.White.copy(alpha = .5f), fontSize = 14.sp)
                }
                BasicTextField(
                    value = input,
                    onValueChange = {
                        input = it
                        if (it.isNotEmpty()) vm.notifyTyping()
                    },
                    singleLine = true,
                    textStyle = TextStyle(color = Color.White, fontSize = 15.sp),
                    cursorBrush = SolidColor(g.accent),
                    modifier = Modifier.fillMaxWidth(),
                )
            }
            Spacer(Modifier.width(8.dp))
            CallIconButton(Icons.AutoMirrored.Rounded.Send, tr("Send", "Отправить")) {
                vm.sendMessage(input)
                input = ""
            }
        }
    }
    Spacer(Modifier.height(10.dp))
}

@Composable
private fun ReportDialog(onPick: (String) -> Unit, onDismiss: () -> Unit) {
    GlassDialog(onDismiss = onDismiss) {
        Text(
            tr("Report", "Пожаловаться"),
            color = glass.text, fontSize = 18.sp, fontWeight = FontWeight.Bold,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            tr("The user will also be blocked for you.", "Пользователь также будет заблокирован для тебя."),
            color = glass.textDim, fontSize = 13.sp,
        )
        Spacer(Modifier.height(10.dp))
        REPORT_REASONS.forEach { r ->
            DialogRow(tr(r.second, r.third), false) { onPick(r.first) }
        }
    }
}

@Composable
private fun BlockConfirmDialog(onConfirm: () -> Unit, onDismiss: () -> Unit) {
    GlassDialog(onDismiss = onDismiss) {
        Text(
            tr("Block this user?", "Заблокировать пользователя?"),
            color = glass.text, fontSize = 18.sp, fontWeight = FontWeight.Bold,
        )
        Spacer(Modifier.height(6.dp))
        Text(
            tr("You will never be matched again.", "Вы больше никогда не встретитесь."),
            color = glass.textDim, fontSize = 13.sp,
        )
        Spacer(Modifier.height(14.dp))
        GlassButton(tr("Block", "Заблокировать"), icon = Icons.Rounded.Block, onClick = onConfirm)
        Spacer(Modifier.height(8.dp))
        GlassButton(tr("Cancel", "Отмена"), primary = false, onClick = onDismiss)
    }
}

private fun formatTime(sec: Int): String = "%d:%02d".format(sec / 60, sec % 60)
