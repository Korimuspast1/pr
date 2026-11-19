package com.glass.launcher

import java.util.Locale

fun isRu(): Boolean = when (Store.lang) {
    "ru" -> true
    "en" -> false
    else -> Locale.getDefault().language == "ru"
}

/** Tiny inline translation helper: tr("English", "Русский"). Reads snapshot state so UI recomposes on change. */
fun tr(en: String, ru: String): String = if (isRu()) ru else en
