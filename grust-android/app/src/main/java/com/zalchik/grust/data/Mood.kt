package com.zalchik.grust.data

import androidx.compose.ui.graphics.Color

/**
 * Five steps of a mood, from "heavy" to "light".
 *
 * The app is about sadness, so the scale is deliberately gentle: the third step is not called
 * "normal", because a day without colour is not a good day by default. Nothing here ever asks the
 * user to be happy - it only asks how it is today.
 */
enum class Mood(
    val level: Int,
    val emoji: String,
    val title: String,
    val hint: String,
    val tint: Color,
) {
    HEAVY(1, "\uD83D\uDE1E", "Тяжело", "совсем нет сил", Color(0xFF6C79A8)),
    SAD(2, "\uD83D\uDE14", "Грустно", "тоскливо и тихо", Color(0xFF7A87BE)),
    FLAT(3, "\uD83D\uDE10", "Никак", "ровно, без красок", Color(0xFF95A0CF)),
    OK(4, "\uD83D\uDE42", "Терпимо", "уже чуть легче", Color(0xFF9FC6DA)),
    LIGHT(5, "\uD83D\uDE0A", "Светло", "почти хорошо", Color(0xFFF0C77E)),
    ;

    companion object {
        fun of(level: Int): Mood = entries.firstOrNull { it.level == level } ?: FLAT
    }
}
