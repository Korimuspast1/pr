package com.zalchik.grust.ui

import java.time.LocalDate
import java.time.format.TextStyle
import java.util.Locale

/**
 * Russian dates and numbers, written out by hand instead of through a locale.
 *
 * The app is in Russian only; depending on the phone's ICU data for month names would make the
 * diary look different on different devices for no reason.
 */
object Format {
    private val months = listOf(
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря",
    )
    private val monthsShort = listOf(
        "янв", "фев", "мар", "апр", "май", "июн", "июл", "авг", "сен", "окт", "ноя", "дек",
    )
    private val weekdays = listOf(
        "понедельник", "вторник", "среда", "четверг", "пятница", "суббота", "воскресенье",
    )

    fun longDate(day: LocalDate): String =
        "${day.dayOfMonth} ${months[day.monthValue - 1]}, ${weekdays[day.dayOfWeek.value - 1]}"

    fun shortDate(day: LocalDate): String =
        "${day.dayOfMonth} ${monthsShort[day.monthValue - 1]}"

    fun weekdayShort(day: LocalDate): String =
        day.dayOfWeek.getDisplayName(TextStyle.SHORT, Locale.forLanguageTag("ru"))
            .take(2)
            .replace(".", "")

    fun pluralDays(count: Int): String {
        val tail = count % 100
        if (tail in 11..14) return "дней"
        return when (count % 10) {
            1 -> "день"
            2, 3, 4 -> "дня"
            else -> "дней"
        }
    }

    fun oneDecimal(value: Double): String {
        val rounded = Math.round(value * 10) / 10.0
        val text = rounded.toString().replace('.', ',')
        return if (rounded == Math.floor(rounded)) "$text,0" else text
    }

    fun bytes(count: Int): String = when {
        count < 1024 -> "$count Б"
        count < 1024 * 1024 -> "${count / 1024} КБ"
        else -> "${oneDecimal(count / 1024.0 / 1024.0)} МБ"
    }
}
