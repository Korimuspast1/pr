package m.chatrulet.m

import java.util.Locale

fun isRu(): Boolean = when (Store.lang) {
    "ru" -> true
    "en" -> false
    else -> Locale.getDefault().language == "ru"
}

/** Tiny inline translation helper: tr("English", "Русский"). Reads snapshot state so UI recomposes on change. */
fun tr(en: String, ru: String): String = if (isRu()) ru else en

/** Флаг по ISO-коду страны (🇷🇺 из "RU"). */
fun flagFor(code: String): String {
    if (code.length != 2) return "🌍"
    return try {
        val a = Character.toChars(0x1F1E6 + (code[0].uppercaseChar() - 'A'))
        val b = Character.toChars(0x1F1E6 + (code[1].uppercaseChar() - 'A'))
        String(a) + String(b)
    } catch (e: Throwable) {
        "🌍"
    }
}

fun countryName(code: String): String = try {
    Locale("", code).getDisplayCountry(if (isRu()) Locale("ru") else Locale.ENGLISH)
} catch (e: Throwable) {
    code
}
