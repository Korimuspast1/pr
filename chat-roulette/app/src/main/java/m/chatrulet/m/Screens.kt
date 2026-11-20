package m.chatrulet.m

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.ArrowBack
import androidx.compose.material.icons.rounded.Block
import androidx.compose.material.icons.rounded.BlurOn
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material.icons.rounded.Close
import androidx.compose.material.icons.rounded.DarkMode
import androidx.compose.material.icons.rounded.Female
import androidx.compose.material.icons.rounded.Gavel
import androidx.compose.material.icons.rounded.Info
import androidx.compose.material.icons.rounded.LightMode
import androidx.compose.material.icons.rounded.Male
import androidx.compose.material.icons.rounded.Palette
import androidx.compose.material.icons.rounded.Person
import androidx.compose.material.icons.rounded.PhoneAndroid
import androidx.compose.material.icons.rounded.Public
import androidx.compose.material.icons.rounded.Search
import androidx.compose.material.icons.rounded.Security
import androidx.compose.material.icons.rounded.Settings
import androidx.compose.material.icons.rounded.Timer
import androidx.compose.material.icons.rounded.Translate
import androidx.compose.material.icons.rounded.Videocam
import androidx.compose.material.icons.rounded.Star
import androidx.compose.material.icons.rounded.VolumeUp
import androidx.compose.material3.Icon
import androidx.compose.material3.Slider
import androidx.compose.material3.SliderDefaults
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

// ════════════════════════════════════════════════════════ каталоги


val LANGS = listOf("ru", "en", "de", "fr", "es", "pt", "it", "tr", "uk", "pl", "ar", "ja")

fun langName(code: String): String = try {
    val l = Locale(code)
    l.getDisplayLanguage(if (isRu()) Locale("ru") else Locale.ENGLISH).replaceFirstChar { it.uppercase() }
} catch (e: Throwable) {
    code
}

val POPULAR_COUNTRIES = listOf("RU", "UA", "BY", "KZ", "US", "GB", "DE", "FR", "TR", "PL", "IT", "ES", "BR", "IN", "JP")

val REPORT_REASONS = listOf(
    Triple("nudity", "Nudity / 18+ content", "Нагота / контент 18+"),
    Triple("spam", "Spam or scam", "Спам или мошенничество"),
    Triple("abuse", "Insults / harassment", "Оскорбления / травля"),
    Triple("minor", "Looks underage", "Похоже, несовершеннолетний"),
    Triple("other", "Other", "Другое"),
)

// ════════════════════════════════════════════════════════ 18+ gate

@Composable
fun GateScreen(onAgree: () -> Unit, onRules: () -> Unit, onExit: () -> Unit) {
    val g = glass
    Box(Modifier.fillMaxSize()) {
        LiquidBackground()
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            GlassCard(
                Modifier.fillMaxWidth(),
                padding = PaddingValues(26.dp),
            ) {
                Box(
                    Modifier.align(Alignment.CenterHorizontally).size(84.dp)
                        .clip(CircleShape)
                        .glassPane(g, CircleShape, g.fillStrong),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(Icons.Rounded.Videocam, null, tint = g.accent, modifier = Modifier.size(42.dp))
                }
                Spacer(Modifier.height(16.dp))
                Row(Modifier.align(Alignment.CenterHorizontally), verticalAlignment = Alignment.CenterVertically) {
                    Text("Glass Roulette", color = g.text, fontSize = 24.sp, fontWeight = FontWeight.Bold)
                }
                Spacer(Modifier.height(6.dp))
                Text(
                    tr("Random video chat", "Случайные видеознакомства"),
                    color = g.textDim, fontSize = 14.sp,
                    modifier = Modifier.align(Alignment.CenterHorizontally),
                )
                Spacer(Modifier.height(22.dp))
                Text(
                    "18+",
                    color = g.accent, fontSize = 44.sp, fontWeight = FontWeight.Black,
                    modifier = Modifier.align(Alignment.CenterHorizontally),
                )
                Spacer(Modifier.height(8.dp))
                Text(
                    tr(
                        "This app is for adults only. Confirm that you are 18 or older and accept the rules: no nudity, no spam, be polite.",
                        "Приложение только для взрослых. Подтверди, что тебе есть 18, и прими правила: без наготы, без спама, уважай собеседников.",
                    ),
                    color = g.textDim, fontSize = 14.sp, textAlign = TextAlign.Center,
                    modifier = Modifier.align(Alignment.CenterHorizontally),
                )
                Spacer(Modifier.height(20.dp))
                GlassButton(
                    tr("I am 18 or older", "Мне есть 18"),
                    icon = Icons.Rounded.Check,
                    onClick = onAgree,
                )
                Spacer(Modifier.height(10.dp))
                GlassButton(tr("Read the rules", "Читать правила"), icon = Icons.Rounded.Gavel, primary = false, onClick = onRules)
                Spacer(Modifier.height(10.dp))
                GlassButton(tr("Exit", "Выход"), icon = Icons.Rounded.Close, primary = false, onClick = onExit)
            }
        }
    }
}

// ════════════════════════════════════════════════════════ главный экран

@Composable
fun HomeScreen(vm: AppViewModel, onFind: () -> Unit) {
    val g = glass
    var countryDialog by remember { mutableStateOf(false) }

    GlassScaffold(
        topBar = { haze ->
            GlassTopBar(
                haze,
                title = "Glass Roulette",
            ) {
                if (Store.bgMode == "gta") GtaBadge()
                GlassIconButton(Icons.Rounded.Settings, tr("Settings", "Настройки")) { vm.screen = Screen.SETTINGS }
            }
        },
    ) { pad ->
        Column(
            Modifier.fillMaxSize().padding(pad).verticalScroll(rememberScrollState()).padding(horizontal = 16.dp),
        ) {
            // ── поиск / отмена ──
            if (vm.phase == Phase.SEARCHING) {
                SearchingCard(onCancel = { vm.cancelSearch() })
            } else {
                GlassCard {
                    Box(
                        Modifier.align(Alignment.CenterHorizontally).size(72.dp)
                            .clip(CircleShape)
                            .glassPane(g, CircleShape, g.fillStrong),
                        contentAlignment = Alignment.Center,
                    ) {
                        Icon(Icons.Rounded.Videocam, null, tint = g.accent, modifier = Modifier.size(34.dp))
                    }
                    Spacer(Modifier.height(14.dp))
                    GlassButton(
                        tr("Find someone", "Найти собеседника"),
                        icon = Icons.Rounded.Search,
                        enabled = vm.phase == Phase.IDLE,
                        onClick = onFind,
                    )
                }
            }

            // ── фильтры ──
            SectionTitle(tr("Search filters", "Фильтры поиска"))
            GlassCard {
                // страна
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Public, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr("Country", "Страна"), color = g.text, fontSize = 14.sp, modifier = Modifier.weight(1f))
                    GlassChip(
                        if (Store.countryFilter.isBlank()) tr("Whole world", "Весь мир")
                        else flagFor(Store.countryFilter) + " " + countryName(Store.countryFilter),
                        selected = Store.countryFilter.isNotBlank(),
                    ) { countryDialog = true }
                }
                Spacer(Modifier.height(12.dp))
                // пол
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Person, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr("Looking for", "Ищу"), color = g.text, fontSize = 14.sp)
                    Spacer(Modifier.width(10.dp))
                    Row(Modifier.horizontalScroll(rememberScrollState())) {
                        GlassChip(tr("Any", "Всех"), Store.genderFilter.isBlank()) { Store.genderFilter = "" }
                        Spacer(Modifier.width(6.dp))
                        GlassChip(tr("Men", "Парней"), Store.genderFilter == "m", Icons.Rounded.Male) { Store.genderFilter = "m" }
                        Spacer(Modifier.width(6.dp))
                        GlassChip(tr("Women", "Девушек"), Store.genderFilter == "f", Icons.Rounded.Female) { Store.genderFilter = "f" }
                    }
                }
                Spacer(Modifier.height(12.dp))
                // язык
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Translate, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr("Language", "Язык"), color = g.text, fontSize = 14.sp)
                    Spacer(Modifier.width(10.dp))
                    Row(Modifier.horizontalScroll(rememberScrollState())) {
                        GlassChip(tr("Any", "Любой"), Store.langFilter.isBlank()) { Store.langFilter = "" }
                        for (l in LANGS) {
                            Spacer(Modifier.width(6.dp))
                            GlassChip(langName(l), Store.langFilter == l) { Store.langFilter = l }
                        }
                    }
                }
            }

            // ── правила и блокировки ──
            Spacer(Modifier.height(14.dp))
            Row {
                GlassCard(Modifier.weight(1f), onClick = { vm.screen = Screen.RULES }) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Rounded.Gavel, tr("Rules", "Правила"), tint = g.accent)
                        Spacer(Modifier.width(10.dp))
                        Text(tr("Rules", "Правила"), color = g.text, fontSize = 15.sp, fontWeight = FontWeight.Medium)
                    }
                }
                Spacer(Modifier.width(10.dp))
                GlassCard(Modifier.weight(1f), onClick = { vm.screen = Screen.BLOCKED }) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Rounded.Block, tr("Blocked", "Заблокированные"), tint = g.accent)
                        Spacer(Modifier.width(10.dp))
                        Text(tr("Blocked", "Чёрный список"), color = g.text, fontSize = 15.sp, fontWeight = FontWeight.Medium)
                    }
                }
            }

            // ── статистика ──
            SectionTitle(tr("My stats", "Моя статистика"))
            GlassCard {
                Row {
                    StatCell(tr("Calls", "Звонков"), "${Store.statsCalls}", Modifier.weight(1f))
                    StatCell(tr("Minutes", "Минут"), "${Store.statsMinutes}", Modifier.weight(1f))
                    StatCell(tr("Blocked", "Заблокировано"), "${Store.blocked.size}", Modifier.weight(1f))
                }
            }
            Spacer(Modifier.height(24.dp))
        }
    }

    if (countryDialog) {
        CountryDialog(
            current = Store.countryFilter,
            anyLabel = tr("Whole world", "Весь мир"),
            onPick = { Store.countryFilter = it; countryDialog = false },
            onDismiss = { countryDialog = false },
        )
    }
}

/** Мини-бейдж «GTA VI» в топ-баре, когда выбран стиль GTA. */
@Composable
fun GtaBadge() {
    Row(
        Modifier
            .padding(end = 8.dp)
            .clip(CircleShape)
            .background(
                androidx.compose.ui.graphics.Brush.horizontalGradient(
                    listOf(Color(0xFF2DE2FF), Color(0xFFFF3D7F)),
                ),
            )
            .padding(horizontal = 10.dp, vertical = 5.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(Icons.Rounded.Star, null, tint = Color.White, modifier = Modifier.size(12.dp))
        Spacer(Modifier.width(4.dp))
        Text("GTA VI", color = Color.White, fontSize = 11.sp, fontWeight = FontWeight.Black, letterSpacing = 1.sp)
    }
}

@Composable
private fun StatCell(label: String, value: String, modifier: Modifier = Modifier) {
    val g = glass
    Column(modifier, horizontalAlignment = Alignment.CenterHorizontally) {
        Text(value, color = g.accent, fontSize = 22.sp, fontWeight = FontWeight.Bold)
        Text(label, color = g.textDim, fontSize = 12.sp, textAlign = TextAlign.Center)
    }
}

@Composable
private fun SearchingCard(onCancel: () -> Unit) {
    val g = glass
    val pulse by rememberInfiniteTransition(label = "pulse").animateFloat(
        1f, 1.25f,
        infiniteRepeatable(tween(900), RepeatMode.Reverse),
        label = "p",
    )
    GlassCard {
        Box(
            Modifier.align(Alignment.CenterHorizontally).size(84.dp),
            contentAlignment = Alignment.Center,
        ) {
            Box(
                Modifier.size(84.dp).scale(pulse).clip(CircleShape)
                    .glassPane(g, CircleShape, g.accent.copy(alpha = .25f)),
            )
            Icon(Icons.Rounded.Search, null, tint = g.accent, modifier = Modifier.size(34.dp))
        }
        Spacer(Modifier.height(12.dp))
        Text(
            tr("Looking for a partner…", "Ищем собеседника…"),
            color = g.text, fontSize = 16.sp, fontWeight = FontWeight.SemiBold,
            modifier = Modifier.align(Alignment.CenterHorizontally),
        )
        Spacer(Modifier.height(4.dp))
        Text(
            tr("Keep the app open", "Не закрывай приложение"),
            color = g.textDim, fontSize = 13.sp,
            modifier = Modifier.align(Alignment.CenterHorizontally),
        )
        Spacer(Modifier.height(14.dp))
        GlassButton(tr("Cancel", "Отмена"), icon = Icons.Rounded.Close, primary = false, onClick = onCancel)
    }
}

// ════════════════════════════════════════════════════════ настройки

@Composable
fun SettingsScreen(vm: AppViewModel) {
    val g = glass
    var countryDialog by remember { mutableStateOf(false) }

    GlassScaffold(
        topBar = { haze ->
            GlassTopBar(
                haze,
                title = tr("Settings", "Настройки"),
                onBack = { vm.screen = Screen.HOME },
                backDesc = tr("Back", "Назад"),
            )
        },
    ) { pad ->
        Column(
            Modifier.fillMaxSize().padding(pad).verticalScroll(rememberScrollState()).padding(horizontal = 16.dp),
        ) {
            // ── профиль ──
            SectionTitle(tr("Profile", "Профиль"))
            GlassCard {
                GlassField(tr("Nickname", "Ник"), Store.nick, { Store.nick = it.take(20) })
                Spacer(Modifier.height(12.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Public, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr("My country", "Моя страна"), color = g.text, fontSize = 14.sp, modifier = Modifier.weight(1f))
                    GlassChip(flagFor(Store.myCountry()) + " " + countryName(Store.myCountry()), Store.country.isNotBlank()) {
                        countryDialog = true
                    }
                }
                Spacer(Modifier.height(12.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Person, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr("I am", "Я"), color = g.text, fontSize = 14.sp)
                    Spacer(Modifier.width(10.dp))
                    GlassChip(tr("Not set", "Не указано"), Store.gender.isBlank()) { Store.gender = "" }
                    Spacer(Modifier.width(6.dp))
                    GlassChip(tr("Male", "Парень"), Store.gender == "m", Icons.Rounded.Male) { Store.gender = "m" }
                    Spacer(Modifier.width(6.dp))
                    GlassChip(tr("Female", "Девушка"), Store.gender == "f", Icons.Rounded.Female) { Store.gender = "f" }
                }
            }

            // ── звонок ──
            SectionTitle(tr("Calls", "Звонки"))
            GlassCard {
                SwitchRow(Icons.Rounded.BlurOn, tr("Blur my camera by default", "Блюрить мою камеру по умолчанию"), Store.blurCam) {
                    Store.blurCam = it
                }
                SwitchRow(Icons.Rounded.VolumeUp, tr("Speakerphone in calls", "Громкая связь в звонке"), Store.speakerOn) {
                    Store.speakerOn = it
                }
                SwitchRow(
                    Icons.Rounded.Security,
                    tr("Screenshot protection (FLAG_SECURE)", "Защита от скриншотов (FLAG_SECURE)"),
                    Store.secureScreen,
                ) { Store.secureScreen = it }
                Spacer(Modifier.height(6.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Timer, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr("Auto-next after", "Авто-далее через"), color = g.text, fontSize = 14.sp)
                    Spacer(Modifier.width(10.dp))
                    Row(Modifier.horizontalScroll(rememberScrollState())) {
                        GlassChip(tr("Off", "Выкл"), Store.autoNext == 0) { Store.autoNext = 0 }
                        for (s in listOf(15, 30, 60)) {
                            Spacer(Modifier.width(6.dp))
                            GlassChip("${s}s", Store.autoNext == s) { Store.autoNext = s }
                        }
                    }
                }
            }

            // ── внешний вид ──
            SectionTitle(tr("Appearance", "Внешний вид"))
            GlassCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.DarkMode, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr("Theme", "Тема"), color = g.text, fontSize = 14.sp)
                    Spacer(Modifier.width(10.dp))
                    Row(Modifier.horizontalScroll(rememberScrollState())) {
                        GlassChip(tr("Auto", "Авто"), Store.theme == "system", Icons.Rounded.PhoneAndroid) { Store.theme = "system" }
                        Spacer(Modifier.width(6.dp))
                        GlassChip(tr("Light", "Светлая"), Store.theme == "light", Icons.Rounded.LightMode) { Store.theme = "light" }
                        Spacer(Modifier.width(6.dp))
                        GlassChip(tr("Dark", "Тёмная"), Store.theme == "dark", Icons.Rounded.DarkMode) { Store.theme = "dark" }
                    }
                }
                Spacer(Modifier.height(12.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Palette, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr("Accent", "Акцент"), color = g.text, fontSize = 14.sp)
                    Spacer(Modifier.width(10.dp))
                    Row(Modifier.horizontalScroll(rememberScrollState())) {
                        if (android.os.Build.VERSION.SDK_INT >= 31) {
                            GlassChip(tr("System", "Системный"), Store.accent < 0) { Store.accent = -1 }
                            Spacer(Modifier.width(6.dp))
                        }
                        ACCENTS.forEachIndexed { i, a ->
                            if (i > 0) Spacer(Modifier.width(6.dp))
                            GlassChip(tr(a.nameEn, a.nameRu), Store.accent == i) { Store.accent = i }
                        }
                    }
                }
                GlassSlider(tr("Blur", "Размытие"), Store.blur, 8f..48f, { "${it.toInt()} dp" }) { Store.blur = it }
                GlassSlider(tr("Opacity", "Непрозрачность"), Store.glassOpacity, 0.1f..1f, { "${(it * 100).toInt()}%" }) { Store.glassOpacity = it }
                GlassSlider(tr("Corner radius", "Скругление"), Store.cornerRadius, 10f..38f, { "${it.toInt()} dp" }) { Store.cornerRadius = it }
                GlassSlider(tr("Font scale", "Масштаб шрифта"), Store.fontScale, 0.8f..1.3f, { "${(it * 100).toInt()}%" }) { Store.fontScale = it }
                GlassSlider(tr("Glass shine", "Блик стекла"), Store.refraction, 0f..1f, { "${(it * 100).toInt()}%" }) { Store.refraction = it }
                Spacer(Modifier.height(6.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Star, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text(tr("Background style", "Стиль фона"), color = g.text, fontSize = 14.sp)
                    Spacer(Modifier.width(10.dp))
                    Row(Modifier.horizontalScroll(rememberScrollState())) {
                        GlassChip(tr("Blobs", "Пятна"), Store.bgMode == "blobs") { Store.bgMode = "blobs" }
                        Spacer(Modifier.width(6.dp))
                        GlassChip("GTA VI", Store.bgMode == "gta") {
                            Store.bgMode = "gta"
                            if (Store.accent >= 0) Store.accent = 2
                        }
                        Spacer(Modifier.width(6.dp))
                        GlassChip(tr("Gradient", "Градиент"), Store.bgMode == "gradient") { Store.bgMode = "gradient" }
                        Spacer(Modifier.width(6.dp))
                        GlassChip(tr("Solid", "Однотонный"), Store.bgMode == "solid") { Store.bgMode = "solid" }
                    }
                }
                SwitchRow(Icons.Rounded.Videocam, tr("Animated background", "Анимированный фон"), Store.animatedBg) { Store.animatedBg = it }
            }

            // ── эффект касания ──
            SectionTitle(tr("Touch effect", "Эффект касания"))
            GlassCard {
                SwitchRow(Icons.Rounded.Star, tr("Ripple wave with glow", "Волна с подсветкой"), Store.touchRipples) { Store.touchRipples = it }
                SwitchRow(Icons.Rounded.VolumeUp, tr("Touch sound", "Звук касания"), Store.touchSound) { Store.touchSound = it }
                SwitchRow(Icons.Rounded.Timer, tr("Touch vibration", "Вибрация касания"), Store.touchVibrate) { Store.touchVibrate = it }
            }

            // ── для разработчика ──
            SectionTitle(tr("For developers", "Для разработчика"))
            GlassCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Person, null, tint = g.accent, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Column(Modifier.weight(1f)) {
                        Text(tr("Test bot call", "Тестовый звонок с ботом"), color = g.text, fontSize = 14.sp)
                        Text(
                            tr("Instantly connects to an automated partner: shows video, answers in chat, simulates typing and reactions.",
                               "Мгновенно соединяет с автоматическим собеседником: показывает видео, отвечает в чате, печатает и шлёт реакции."),
                            color = g.textDim, fontSize = 12.sp,
                        )
                    }
                    Switch(
                        checked = Store.devBot,
                        onCheckedChange = { Store.devBot = it },
                        colors = SwitchDefaults.colors(checkedTrackColor = g.accent, checkedThumbColor = Color.White),
                    )
                }
            }

            // ── язык ──
            SectionTitle(tr("App language", "Язык приложения"))
            GlassCard {
                Row(Modifier.horizontalScroll(rememberScrollState())) {
                    GlassChip(tr("Auto", "Авто"), Store.lang == "system") { Store.lang = "system" }
                    Spacer(Modifier.width(6.dp))
                    GlassChip("English", Store.lang == "en") { Store.lang = "en" }
                    Spacer(Modifier.width(6.dp))
                    GlassChip("Русский", Store.lang == "ru") { Store.lang = "ru" }
                }
            }

            // ── о приложении ──
            SectionTitle(tr("About", "О приложении"))
            GlassCard {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Security, null, tint = g.accent, modifier = Modifier.size(20.dp))
                    Spacer(Modifier.width(10.dp))
                    Column(Modifier.weight(1f)) {
                        Text(tr("End-to-end encryption", "Сквозное шифрование"), color = g.text, fontSize = 14.sp, fontWeight = FontWeight.SemiBold)
                        Text(
                            "ECDH P-256 + SHA-256 + AES-256-GCM\n" + tr("Keys live only during a call", "Ключи живут только во время звонка"),
                            color = g.textDim, fontSize = 12.sp,
                        )
                    }
                }
                Spacer(Modifier.height(12.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Rounded.Info, null, tint = g.accent, modifier = Modifier.size(20.dp))
                    Spacer(Modifier.width(10.dp))
                    Column(Modifier.weight(1f)) {
                        Text("Glass Roulette " + BuildConfig.VERSION_NAME, color = g.text, fontSize = 14.sp)
                        Text(
                            tr("All features are free forever", "Все функции бесплатны навсегда"),
                            color = g.textDim, fontSize = 12.sp,
                        )
                    }
                }
            }
            Spacer(Modifier.height(24.dp))
        }
    }

    if (countryDialog) {
        CountryDialog(
            current = Store.country,
            anyLabel = tr("Auto (by device)", "Авто (по устройству)"),
            onPick = { Store.country = it; countryDialog = false },
            onDismiss = { countryDialog = false },
        )
    }
}

@Composable
fun SwitchRow(icon: ImageVector, label: String, checked: Boolean, onChange: (Boolean) -> Unit) {
    val g = glass
    Row(Modifier.fillMaxWidth().padding(vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, null, tint = g.accent, modifier = Modifier.size(18.dp))
        Spacer(Modifier.width(8.dp))
        Text(label, color = g.text, fontSize = 14.sp, modifier = Modifier.weight(1f))
        Switch(
            checked = checked,
            onCheckedChange = onChange,
            colors = SwitchDefaults.colors(
                checkedTrackColor = g.accent,
                checkedThumbColor = Color.White,
            ),
        )
    }
}

@Composable
fun GlassSlider(label: String, value: Float, range: ClosedFloatingPointRange<Float>, format: (Float) -> String, onChange: (Float) -> Unit) {
    val g = glass
    Column(Modifier.fillMaxWidth().padding(top = 8.dp)) {
        Row {
            Text(label, color = g.text, fontSize = 14.sp, modifier = Modifier.weight(1f))
            Text(format(value), color = g.textDim, fontSize = 13.sp)
        }
        Slider(
            value = value,
            onValueChange = onChange,
            valueRange = range,
            colors = SliderDefaults.colors(
                thumbColor = g.accent,
                activeTrackColor = g.accent,
                inactiveTrackColor = g.fillStrong,
            ),
        )
    }
}

// ════════════════════════════════════════════════════════ правила

@Composable
fun RulesScreen(onBack: () -> Unit) {
    val g = glass
    val rules = listOf(
        tr("You must be 18 or older.", "Тебе должно быть 18 лет или больше."),
        tr("No nudity or sexual content on camera.", "Никакой наготы и контента сексуального характера."),
        tr("No insults, racism, harassment or threats.", "Никаких оскорблений, расизма, травли и угроз."),
        tr("No spam, advertising or scam.", "Никакого спама, рекламы и мошенничества."),
        tr("Do not show other people's personal data.", "Не показывай чужие личные данные."),
        tr("Do not record or photograph strangers without consent.", "Не записывай и не фотографируй собеседников без согласия."),
        tr("Report violators — they will be blocked for you forever.", "Жалуйся на нарушителей — они навсегда попадут в твой чёрный список."),
        tr("Chats are end-to-end encrypted (AES-256-GCM) and are not stored anywhere.", "Чаты зашифрованы сквозным шифрованием (AES-256-GCM) и нигде не хранятся."),
    )
    GlassScaffold(
        topBar = { haze ->
            GlassTopBar(haze, title = tr("Rules", "Правила"), onBack = onBack, backDesc = tr("Back", "Назад"))
        },
    ) { pad ->
        Column(Modifier.fillMaxSize().padding(pad).verticalScroll(rememberScrollState()).padding(horizontal = 16.dp)) {
            GlassCard {
                rules.forEachIndexed { i, r ->
                    Row(Modifier.padding(vertical = 6.dp)) {
                        Text("${i + 1}.", color = g.accent, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                        Spacer(Modifier.width(8.dp))
                        Text(r, color = g.text, fontSize = 14.sp)
                    }
                }
            }
            Spacer(Modifier.height(24.dp))
        }
    }
}

// ════════════════════════════════════════════════════════ чёрный список

@Composable
fun BlockedScreen(vm: AppViewModel) {
    val fmt = remember { SimpleDateFormat("dd.MM.yyyy HH:mm", Locale.getDefault()) }
    GlassScaffold(
        topBar = { haze ->
            GlassTopBar(
                haze,
                title = tr("Blocked", "Чёрный список"),
                subtitle = tr("They will never match with you again", "Они больше никогда с тобой не встретятся"),
                onBack = { vm.screen = Screen.HOME },
                backDesc = tr("Back", "Назад"),
            )
        },
    ) { pad ->
        Column(Modifier.fillMaxSize().padding(pad).padding(horizontal = 16.dp)) {
            if (Store.blocked.isEmpty()) {
                EmptyState(
                    Icons.Rounded.Block,
                    tr("Nobody is blocked", "Никто не заблокирован"),
                    tr("People you block in a call will appear here. The list is stored encrypted (AES-256).", "Люди, которых ты заблокируешь в звонке, появятся здесь. Список хранится зашифрованным (AES-256)."),
                )
            } else {
                LazyColumn {
                    items(Store.blocked.toList()) { b ->
                        GlassCard(Modifier.padding(vertical = 5.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Column(Modifier.weight(1f)) {
                                    Text(
                                        b.label.ifBlank { b.uid.take(8) },
                                        color = glass.text, fontSize = 15.sp, fontWeight = FontWeight.Medium,
                                    )
                                    Text(fmt.format(Date(b.ts)), color = glass.textDim, fontSize = 12.sp)
                                }
                                GlassIconButton(Icons.Rounded.Close, tr("Unblock", "Разблокировать")) {
                                    Store.unblock(b.uid)
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

// ════════════════════════════════════════════════════════ диалоги

@Composable
fun CountryDialog(current: String, anyLabel: String, onPick: (String) -> Unit, onDismiss: () -> Unit) {
    val g = glass
    var query by remember { mutableStateOf("") }
    val all = remember(query) {
        val codes = Locale.getISOCountries().toList()
        val sorted = codes.sortedBy { countryName(it) }
        val popular = POPULAR_COUNTRIES.filter { it in codes }
        (popular + sorted).distinct().filter {
            query.isBlank() || countryName(it).contains(query, ignoreCase = true) || it.contains(query, ignoreCase = true)
        }
    }
    GlassDialog(onDismiss = onDismiss) {
        GlassField(tr("Search country", "Поиск страны"), query, { query = it }, placeholder = "RU, Germany, Япония…")
        Spacer(Modifier.height(10.dp))
        LazyColumn(Modifier.heightIn(max = 380.dp)) {
            item {
                DialogRow(anyLabel, current.isBlank()) { onPick("") }
            }
            items(all) { code ->
                DialogRow(flagFor(code) + "  " + countryName(code), current == code) { onPick(code) }
            }
        }
    }
}

@Composable
fun DialogRow(text: String, selected: Boolean, onClick: () -> Unit) {
    val g = glass
    Row(
        Modifier.fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 10.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(text, color = g.text, fontSize = 15.sp, modifier = Modifier.weight(1f))
        if (selected) Icon(Icons.Rounded.Check, null, tint = g.accent, modifier = Modifier.size(18.dp))
    }
}

@Composable
fun GlassDialog(onDismiss: () -> Unit, content: @Composable () -> Unit) {
    val g = glass
    Box(
        Modifier.fillMaxSize()
            .background(Color.Black.copy(alpha = 0.5f))
            .clickable(onClick = onDismiss)
            .padding(24.dp),
        contentAlignment = Alignment.Center,
    ) {
        GlassCard(
            Modifier
                .fillMaxWidth()
                .clickable(
                    indication = null,
                    interactionSource = remember { androidx.compose.foundation.interaction.MutableInteractionSource() },
                ) {},
            tint = g.fillStrong,
        ) {
            content()
        }
    }
}
