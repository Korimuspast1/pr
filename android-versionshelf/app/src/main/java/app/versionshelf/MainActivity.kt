package app.versionshelf

import android.app.Activity
import android.app.AlertDialog
import android.content.Context
import android.content.SharedPreferences
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.text.Editable
import android.text.InputType
import android.text.TextWatcher
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.view.WindowInsets
import android.widget.EditText
import android.widget.FrameLayout
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.ProgressBar
import android.widget.ScrollView
import android.widget.TextView
import java.net.URI
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors

class MainActivity : Activity() {
    private val worker: ExecutorService = Executors.newFixedThreadPool(2)
    private val mainHandler = Handler(Looper.getMainLooper())
    private val catalogueRepository = CatalogRepository()

    private lateinit var preferences: SharedPreferences
    private lateinit var sourceLine: TextView
    private lateinit var stateLine: TextView
    private lateinit var listContainer: LinearLayout
    private lateinit var emptyState: TextView
    private lateinit var search: EditText
    private lateinit var countLine: TextView
    private lateinit var progress: ProgressBar

    private var apps: List<InstalledApp> = emptyList()
    private var catalogue: CatalogSnapshot? = null
    private var query: String = ""

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        preferences = getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)
        window.apply {
            statusBarColor = BLACK
            navigationBarColor = BLACK
            decorView.systemUiVisibility = 0
        }
        setContentView(buildScreen())
        rescanApps()
    }

    override fun onDestroy() {
        worker.shutdownNow()
        super.onDestroy()
    }

    @Suppress("DEPRECATION")
    private fun buildScreen(): View {
        val root = FrameLayout(this).apply {
            setBackgroundColor(BLACK)
            // Reserve the status/navigation-bar area even on Android 15 edge-to-edge devices.
            setOnApplyWindowInsetsListener { view, insets ->
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                    val bars = insets.getInsets(WindowInsets.Type.systemBars())
                    view.setPadding(0, bars.top, 0, bars.bottom)
                } else {
                    view.setPadding(0, insets.systemWindowInsetTop, 0, insets.systemWindowInsetBottom)
                }
                insets
            }
        }
        val scroll = ScrollView(this).apply {
            isFillViewport = true
            clipToPadding = false
            setPadding(dp(20), dp(18), dp(20), dp(32))
        }
        val content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
        }
        scroll.addView(content, ViewGroup.LayoutParams(MATCH, WRAP))
        root.addView(scroll, FrameLayout.LayoutParams(MATCH, MATCH))

        content.addView(label("VERSION / SHELF", 13, WHITE, bold = true, tracking = 0.20f))
        content.addView(text("Ваш частный архив проверенных версий", 16, MUTED).margins(top = 7, bottom = 24))

        val sourceCard = panel().apply {
            orientation = LinearLayout.VERTICAL
            addView(label("ИСТОЧНИК", 10, DIM, bold = true, tracking = 0.18f))
            sourceLine = text("", 16, WHITE, bold = true).margins(top = 8)
            addView(sourceLine)
            stateLine = text("", 13, MUTED).margins(top = 6)
            addView(stateLine)
        }
        content.addView(sourceCard.margins(bottom = 12))

        val actions = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }
        actions.addView(action("ОБНОВИТЬ КАТАЛОГ", filled = true) { syncCatalogue() }, LinearLayout.LayoutParams(0, dp(46), 1f))
        actions.addView(action("НАСТРОЙКИ", filled = false) { showSourceSettings() }, LinearLayout.LayoutParams(0, dp(46), 0.72f).withStartMargin(dp(8)))
        content.addView(actions.margins(bottom = 20))

        val appHeader = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            addView(label("ПРИЛОЖЕНИЯ", 10, DIM, bold = true, tracking = 0.18f), LinearLayout.LayoutParams(0, WRAP, 1f))
            addView(action("ПЕРЕСКАНИРОВАТЬ", filled = false) { rescanApps() }, LinearLayout.LayoutParams(dp(142), dp(34)))
        }
        content.addView(appHeader.margins(bottom = 7))
        countLine = text("Сканирование…", 13, MUTED).margins(bottom = 12)
        content.addView(countLine)
        search = EditText(this).apply {
            hint = "Поиск по названию или пакету"
            setHintTextColor(DIM)
            setTextColor(WHITE)
            textSize = 15f
            setSingleLine(true)
            inputType = InputType.TYPE_CLASS_TEXT
            background = rounded(CARD, dp(10), STROKE, 1)
            setPadding(dp(15), 0, dp(15), 0)
            addTextChangedListener(object : TextWatcher {
                override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) = Unit
                override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {
                    query = s?.toString()?.trim().orEmpty()
                    renderApps()
                }
                override fun afterTextChanged(s: Editable?) = Unit
            })
        }
        content.addView(search, LinearLayout.LayoutParams(MATCH, dp(48)).withMargins(bottom = dp(12)))

        listContainer = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        emptyState = text("", 15, MUTED).apply {
            gravity = Gravity.CENTER
            setPadding(dp(16), dp(28), dp(16), dp(28))
            visibility = View.GONE
        }
        content.addView(emptyState, LinearLayout.LayoutParams(MATCH, WRAP))
        content.addView(listContainer, LinearLayout.LayoutParams(MATCH, WRAP))

        progress = ProgressBar(this).apply {
            isIndeterminate = true
            visibility = View.GONE
        }
        root.addView(progress, FrameLayout.LayoutParams(dp(42), dp(42), Gravity.CENTER))

        updateSourceDescription()
        return root
    }

    private fun rescanApps() {
        loading(true, "Сканирование приложений…")
        worker.execute {
            try {
                val result = InstalledApps.scan(this)
                mainHandler.post {
                    apps = result
                    catalogue = null
                    loading(false, "Найдено ${apps.size} приложений. Каталог ещё не загружен.")
                    renderApps()
                }
            } catch (_: Exception) {
                mainHandler.post {
                    loading(false, "Не удалось прочитать список приложений.")
                    renderApps()
                }
            }
        }
    }

    private fun syncCatalogue() {
        if (apps.isEmpty()) {
            stateLine.text = "Сначала дождитесь сканирования приложений."
            return
        }
        val config = currentConfig()
        loading(true, "Загрузка ${config.sourceLabel} и проверка индекса…")
        worker.execute {
            try {
                val result = catalogueRepository.fetch(config, apps.mapTo(linkedSetOf()) { it.packageName })
                mainHandler.post {
                    catalogue = result
                    val supported = result.versionsByPackage.keys.size
                    loading(false, "${result.sourceLabel}: версии найдены для $supported из ${apps.size} приложений.")
                    renderApps()
                }
            } catch (error: Exception) {
                mainHandler.post {
                    loading(false, "Каталог не загружен: ${error.message ?: "ошибка сети"}")
                }
            }
        }
    }

    private fun renderApps() {
        if (!::listContainer.isInitialized) return
        listContainer.removeAllViews()
        val normalizedQuery = query.lowercase()
        val filtered = apps.filter {
            normalizedQuery.isBlank() || it.label.lowercase().contains(normalizedQuery) ||
                it.packageName.lowercase().contains(normalizedQuery)
        }
        countLine.text = when {
            apps.isEmpty() -> "Нет приложений с иконкой в лаунчере"
            normalizedQuery.isNotBlank() -> "${filtered.size} из ${apps.size}"
            else -> "${apps.size} приложений с иконкой в лаунчере"
        }
        emptyState.visibility = if (filtered.isEmpty()) View.VISIBLE else View.GONE
        emptyState.text = if (apps.isEmpty()) {
            "Не найдено обычных приложений. Системные скрытые пакеты намеренно не сканируются."
        } else {
            "По вашему запросу ничего не найдено."
        }
        filtered.forEach { app -> listContainer.addView(appRow(app).margins(bottom = 8)) }
    }

    private fun appRow(app: InstalledApp): View {
        return LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            background = rounded(CARD, dp(12), STROKE, 1)
            setPadding(dp(14), dp(12), dp(12), dp(12))
            isClickable = true
            isFocusable = true
            setOnClickListener { showAppVersions(app) }

            val iconView = ImageView(this@MainActivity).apply {
                setImageDrawable(app.icon)
                background = rounded(INK, dp(10), STROKE, 1)
                setPadding(dp(7), dp(7), dp(7), dp(7))
            }
            addView(iconView, LinearLayout.LayoutParams(dp(42), dp(42)))

            val textBlock = LinearLayout(this@MainActivity).apply { orientation = LinearLayout.VERTICAL }
            textBlock.addView(text(app.label, 16, WHITE, bold = true, maxLines = 1))
            textBlock.addView(text("${app.versionName}  ·  ${app.packageName}", 12, MUTED, maxLines = 1).margins(top = 3))
            val available = catalogue?.versionsByPackage?.get(app.packageName)?.size
            val availability = when {
                catalogue == null -> "каталог не загружен"
                available == null || available == 0 -> "нет в выбранном каталоге"
                else -> "$available верс. доступно"
            }
            textBlock.addView(label(availability.uppercase(), 9, if (available != null && available > 0) WHITE else DIM, bold = true, tracking = 0.12f).margins(top = 7))
            addView(textBlock, LinearLayout.LayoutParams(0, WRAP, 1f).withStartMargin(dp(12)))
            addView(text("›", 30, DIM).apply { gravity = Gravity.CENTER }, LinearLayout.LayoutParams(dp(20), MATCH))
        }
    }

    private fun showAppVersions(app: InstalledApp) {
        val dialogContent = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(22), dp(22), dp(22), dp(18))
            setBackgroundColor(BLACK)
        }
        dialogContent.addView(label("ПРИЛОЖЕНИЕ", 10, DIM, bold = true, tracking = 0.18f))
        dialogContent.addView(text(app.label, 24, WHITE, bold = true).margins(top = 8))
        dialogContent.addView(text("Установлено: ${app.versionName} (${app.versionCode})", 13, MUTED).margins(top = 5))
        dialogContent.addView(text(app.packageName, 12, DIM).margins(top = 2, bottom = 20))

        val scroll = ScrollView(this)
        val versionsBox = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        scroll.addView(versionsBox)
        dialogContent.addView(scroll, LinearLayout.LayoutParams(MATCH, dp(370)))

        val versionList = catalogue?.versionsByPackage?.get(app.packageName).orEmpty()
        if (catalogue == null) {
            versionsBox.addView(text("Нажмите «Обновить каталог», чтобы посмотреть доступные версии.", 15, MUTED).margins(top = 12))
        } else if (versionList.isEmpty()) {
            versionsBox.addView(text("В выбранном источнике нет версий этого приложения.", 15, MUTED).margins(top = 12))
            versionsBox.addView(text("F-Droid содержит только свободные приложения. Для своих APK используйте частный каталог в настройках.", 13, DIM).margins(top = 9))
        } else {
            versionsBox.addView(label("ВСЕ ДОСТУПНЫЕ ВЕРСИИ · ${catalogue?.sourceLabel?.uppercase()}", 10, DIM, bold = true, tracking = 0.14f).margins(bottom = 10))
            versionList.forEach { version -> versionsBox.addView(versionRow(app, version).margins(bottom = 8)) }
        }

        lateinit var dialog: AlertDialog
        val close = action("ЗАКРЫТЬ", filled = false) { dialog.dismiss() }.margins(top = 18)
        dialogContent.addView(close, LinearLayout.LayoutParams(MATCH, dp(46)))
        dialog = AlertDialog.Builder(this)
            .setView(dialogContent)
            .create()
        dialog.window?.setBackgroundDrawableResource(android.R.color.transparent)
        dialog.show()
        dialog.window?.setBackgroundDrawableResource(android.R.color.transparent)
        dialog.window?.setLayout(MATCH, WRAP)
    }

    private fun versionRow(app: InstalledApp, version: VersionRecord): View {
        return LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            background = rounded(CARD, dp(10), STROKE, 1)
            setPadding(dp(14), dp(12), dp(12), dp(12))
            val top = LinearLayout(this@MainActivity).apply {
                orientation = LinearLayout.HORIZONTAL
                gravity = Gravity.CENTER_VERTICAL
            }
            top.addView(text(version.versionName, 16, WHITE, bold = true), LinearLayout.LayoutParams(0, WRAP, 1f))
            top.addView(label(versionRelation(app, version), 9, relationColor(app, version), bold = true, tracking = 0.12f))
            addView(top)
            val date = version.releasedAt?.let { "  ·  $it" }.orEmpty()
            addView(text("code ${version.versionCode}$date", 12, MUTED).margins(top = 4, bottom = 10))
            addView(action("СКАЧАТЬ И ПРОВЕРИТЬ", filled = true) { beginDownload(app, version) }, LinearLayout.LayoutParams(MATCH, dp(40)))
        }
    }

    private fun beginDownload(app: InstalledApp, version: VersionRecord) {
        loading(true, "Скачивание ${app.label} ${version.versionName}…")
        worker.execute {
            val result = DownloadInstaller.downloadAndVerify(this, app, version)
            mainHandler.post {
                when (result) {
                    is DownloadInstaller.Result.Ready -> {
                        loading(false, "APK проверен. Android попросит подтвердить установку.")
                        try {
                            DownloadInstaller.beginSystemInstall(this, result.apk)
                        } catch (error: Exception) {
                            stateLine.text = "Не удалось открыть системный установщик: ${error.message}"
                        }
                    }
                    is DownloadInstaller.Result.Error -> loading(false, "Загрузка отменена: ${result.message}")
                }
            }
        }
    }

    private fun showSourceSettings() {
        var selected = currentConfig().type
        val initial = currentConfig()
        val content = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(22), dp(22), dp(22), dp(18))
            setBackgroundColor(BLACK)
        }
        content.addView(label("ИСТОЧНИК ВЕРСИЙ", 10, DIM, bold = true, tracking = 0.18f))
        content.addView(text("Выберите проверяемый каталог", 21, WHITE, bold = true).margins(top = 8, bottom = 12))
        content.addView(text("Приложение не ищет APK на случайных сайтах и принимает только HTTPS, SHA-256 и, если указано, сертификат подписи.", 13, MUTED).margins(bottom = 16))

        val fdroid = choice("F-DROID", "Официальный каталог свободных приложений", selected == CatalogType.FDROID)
        val privateChoice = choice("ЧАСТНЫЙ КАТАЛОГ", "Ваш индекс и APK, которыми вы вправе распоряжаться", selected == CatalogType.CUSTOM)
        content.addView(fdroid.margins(bottom = 8))
        content.addView(privateChoice.margins(bottom = 12))

        val customUrl = EditText(this).apply {
            hint = "https://example.org/catalog.json"
            setHintTextColor(DIM)
            setTextColor(WHITE)
            textSize = 14f
            setSingleLine(true)
            inputType = InputType.TYPE_TEXT_VARIATION_URI
            setText(if (initial.type == CatalogType.CUSTOM) initial.url else "")
            background = rounded(CARD, dp(9), STROKE, 1)
            setPadding(dp(13), 0, dp(13), 0)
            visibility = if (selected == CatalogType.CUSTOM) View.VISIBLE else View.GONE
        }
        content.addView(customUrl, LinearLayout.LayoutParams(MATCH, dp(47)).withMargins(bottom = dp(8)))
        content.addView(text("Структура private catalog описана в README. Не добавляйте чужие или непроверенные APK.", 12, DIM).margins(bottom = 16))

        fun select(type: CatalogType) {
            selected = type
            fdroid.isSelected = type == CatalogType.FDROID
            privateChoice.isSelected = type == CatalogType.CUSTOM
            styleChoice(fdroid, fdroid.isSelected)
            styleChoice(privateChoice, privateChoice.isSelected)
            customUrl.visibility = if (type == CatalogType.CUSTOM) View.VISIBLE else View.GONE
        }
        fdroid.setOnClickListener { select(CatalogType.FDROID) }
        privateChoice.setOnClickListener { select(CatalogType.CUSTOM) }
        select(selected)

        lateinit var dialog: AlertDialog
        content.addView(action("СОХРАНИТЬ", filled = true) {
            val url = if (selected == CatalogType.FDROID) CatalogConfig.DEFAULT_FDROID_URL else customUrl.text.toString().trim()
            if (!isValidHttps(url)) {
                customUrl.error = "Нужен корректный HTTPS-адрес"
                return@action
            }
            preferences.edit()
                .putString(PREF_TYPE, selected.name)
                .putString(PREF_URL, url)
                .apply()
            catalogue = null
            updateSourceDescription()
            renderApps()
            dialog.dismiss()
        }, LinearLayout.LayoutParams(MATCH, dp(46)))
        dialog = AlertDialog.Builder(this).setView(content).create()
        dialog.show()
        dialog.window?.setBackgroundDrawableResource(android.R.color.transparent)
        dialog.window?.setLayout(MATCH, WRAP)
    }

    private fun choice(title: String, subtitle: String, selected: Boolean): TextView = text("$title\n$subtitle", 14, WHITE, bold = true).apply {
        setLineSpacing(dp(3).toFloat(), 1f)
        setPadding(dp(14), dp(12), dp(14), dp(12))
        isClickable = true
        isFocusable = true
        isSelected = selected
        styleChoice(this, selected)
    }

    private fun styleChoice(view: TextView, selected: Boolean) {
        view.background = rounded(if (selected) WHITE else CARD, dp(10), if (selected) WHITE else STROKE, 1)
        view.setTextColor(if (selected) BLACK else WHITE)
    }

    private fun updateSourceDescription() {
        if (!::sourceLine.isInitialized) return
        val config = currentConfig()
        sourceLine.text = config.sourceLabel.uppercase()
        stateLine.text = if (catalogue == null) {
            "Выберите приложение и загрузите каталог, чтобы увидеть версии."
        } else {
            "Индекс загружен и проверяется перед каждым скачиванием."
        }
    }

    private fun currentConfig(): CatalogConfig {
        val type = runCatching { CatalogType.valueOf(preferences.getString(PREF_TYPE, CatalogType.FDROID.name)!!) }
            .getOrDefault(CatalogType.FDROID)
        val fallback = if (type == CatalogType.FDROID) CatalogConfig.DEFAULT_FDROID_URL else ""
        return CatalogConfig(type, preferences.getString(PREF_URL, fallback) ?: fallback)
    }

    private fun loading(active: Boolean, message: String) {
        if (::progress.isInitialized) progress.visibility = if (active) View.VISIBLE else View.GONE
        if (::stateLine.isInitialized) stateLine.text = message
    }

    private fun versionRelation(app: InstalledApp, version: VersionRecord): String = when {
        version.versionCode == app.versionCode -> "УСТАНОВЛЕНА"
        version.versionCode < app.versionCode -> "СТАРЕЕ"
        else -> "НОВЕЕ"
    }

    private fun relationColor(app: InstalledApp, version: VersionRecord): Int =
        if (version.versionCode == app.versionCode) DIM else WHITE

    private fun action(caption: String, filled: Boolean, onClick: () -> Unit): TextView = label(caption, 10, if (filled) BLACK else WHITE, bold = true, tracking = 0.12f).apply {
        gravity = Gravity.CENTER
        isClickable = true
        isFocusable = true
        background = rounded(if (filled) WHITE else CARD, dp(9), if (filled) WHITE else STROKE, 1)
        setOnClickListener { onClick() }
    }

    private fun panel(): LinearLayout = LinearLayout(this).apply {
        background = rounded(CARD, dp(12), STROKE, 1)
        setPadding(dp(16), dp(15), dp(16), dp(15))
    }

    private fun text(
        value: String,
        size: Int,
        color: Int,
        bold: Boolean = false,
        maxLines: Int = Int.MAX_VALUE,
    ): TextView = TextView(this).apply {
        this.text = value
        textSize = size.toFloat()
        setTextColor(color)
        typeface = Typeface.create("sans-serif", if (bold) Typeface.BOLD else Typeface.NORMAL)
        this.maxLines = maxLines
        if (maxLines == 1) ellipsize = android.text.TextUtils.TruncateAt.END
    }

    private fun label(value: String, size: Int, color: Int, bold: Boolean, tracking: Float = 0f): TextView =
        text(value, size, color, bold).apply { letterSpacing = tracking }

    private fun rounded(color: Int, radius: Int, strokeColor: Int, strokeWidth: Int): GradientDrawable =
        GradientDrawable().apply {
            setColor(color)
            cornerRadius = radius.toFloat()
            if (strokeWidth > 0) setStroke(strokeWidth, strokeColor)
        }

    private fun <T : View> T.margins(start: Int = 0, top: Int = 0, end: Int = 0, bottom: Int = 0): T {
        layoutParams = (layoutParams ?: ViewGroup.MarginLayoutParams(MATCH, WRAP)).let { source ->
            ViewGroup.MarginLayoutParams(source).apply { setMargins(start, top, end, bottom) }
        }
        return this
    }

    private fun LinearLayout.LayoutParams.withStartMargin(value: Int): LinearLayout.LayoutParams = apply { marginStart = value }

    private fun LinearLayout.LayoutParams.withMargins(
        start: Int = 0,
        top: Int = 0,
        end: Int = 0,
        bottom: Int = 0,
    ): LinearLayout.LayoutParams = apply { setMargins(start, top, end, bottom) }

    private fun dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()

    private fun isValidHttps(value: String): Boolean = try {
        val uri = URI(value)
        uri.scheme == "https" && !uri.host.isNullOrBlank() && uri.userInfo == null
    } catch (_: Exception) {
        false
    }

    private companion object {
        const val PREFERENCES = "versionshelf"
        const val PREF_TYPE = "catalogue_type"
        const val PREF_URL = "catalogue_url"
        const val MATCH = ViewGroup.LayoutParams.MATCH_PARENT
        const val WRAP = ViewGroup.LayoutParams.WRAP_CONTENT
        val BLACK = Color.rgb(11, 11, 11)
        val INK = Color.rgb(19, 19, 19)
        val CARD = Color.rgb(24, 24, 24)
        val STROKE = Color.rgb(54, 54, 54)
        val WHITE = Color.rgb(245, 245, 242)
        val MUTED = Color.rgb(171, 171, 167)
        val DIM = Color.rgb(126, 126, 122)
    }
}
