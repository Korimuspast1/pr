package com.zalchik.nimbus.i18n

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * Interface language, exactly as desktop's nimbus/i18n.py: Russian is the source, so every
 * string in the app is written in Russian and looked up in [LocaleEn] when the language is
 * English. Placeholders are the same `{name}` ones; `ctx` tells apart one Russian text meaning
 * different things (the English table then holds "<ctx>|<text>").
 *
 * [lang] is Compose state, so switching it re-renders the whole interface without a restart
 * (the desktop needed one: it translated module-level texts at import time).
 */
object I18n {
    val LANGUAGES = linkedMapOf("ru" to "Русский", "en" to "English")

    /** Systems in these languages get Russian on "auto"; everything else gets English. */
    private val RUSSIAN_SPEAKING = setOf("ru", "be", "kk", "ky", "tg")

    var lang by mutableStateOf("ru")
        private set

    /** "ru" or "en" from the interface language of the system. */
    fun systemLanguage(locale: Locale = Locale.getDefault()): String {
        val code = locale.language.lowercase()
        return if (code in RUSSIAN_SPEAKING) "ru" else "en"
    }

    /** "auto" | "ru" | "en" from the settings, resolved against the system when it says auto. */
    fun resolve(choice: String, locale: Locale = Locale.getDefault()): String =
        if (LANGUAGES.containsKey(choice)) choice else systemLanguage(locale)

    /** Called by the app at start and whenever the setting changes. */
    fun setLanguage(language: String) {
        lang = if (LANGUAGES.containsKey(language)) language else "ru"
    }

    fun tr(text: String, ctx: String = "", values: Map<String, Any?> = emptyMap()): String {
        var out = text
        if (lang != "ru") {
            val key = if (ctx.isEmpty()) text else "$ctx|$text"
            out = LocaleEn.TEXTS[key] ?: LocaleEn.TEXTS[text] ?: text
        }
        if (values.isNotEmpty()) {
            for ((name, value) in values) {
                out = out.replace("{$name}", value?.toString() ?: "")
            }
        }
        return out
    }

    /**
     * The word for n things: plural(5, "сервер", "сервера", "серверов") is "серверов" in Russian
     * and "servers" in English (the table maps the Russian "one" form to both English ones).
     */
    fun plural(n: Int, one: String, few: String, many: String): String {
        if (lang != "ru") {
            val forms = LocaleEn.PLURALS[one]
            if (forms != null) return if (n == 1) forms.first else forms.second
        }
        val mod10 = n % 10
        val mod100 = n % 100
        if (mod10 == 1 && mod100 != 11) return one
        if (mod10 in 2..4 && mod100 !in 12..14) return few
        return many
    }

    /** A day as people write it: 16.10.2026 in Russian, 2026-10-16 otherwise. */
    fun date(stampMillis: Long): String {
        val pattern = if (lang == "ru") "dd.MM.yyyy" else "yyyy-MM-dd"
        return SimpleDateFormat(pattern, Locale.US).format(Date(stampMillis))
    }

    /** A moment of day, for log lines and "updated at". */
    fun time(stampMillis: Long): String =
        SimpleDateFormat("HH:mm:ss", Locale.US).format(Date(stampMillis))
}

/** `tr("Подключиться")` — the shorthand every screen uses. */
fun tr(text: String, ctx: String = "", vararg values: Pair<String, Any?>): String =
    I18n.tr(text, ctx, values.toMap())

fun plural(n: Int, one: String, few: String, many: String): String = I18n.plural(n, one, few, many)
