package com.zalchik.grust.data

import java.time.LocalDate

/**
 * Russian dates and numbers, written out by hand instead of through a locale.
 *
 * The app is in Russian only; depending on the phone's ICU data for month names would make the
 * diary look different on different devices for no reason. Nothing here touches Android, so the
 * plural rule - which is easy to get wrong - is covered by unit tests.
 */
object Russian {

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

    /** Monday-first, two letters: "пн", "вт", … for the two-week strip. */
    private val weekdaysShort = listOf("пн", "вт", "ср", "чт", "пт", "сб", "вс")

    fun longDate(day: LocalDate): String =
        "${day.dayOfMonth} ${months[day.monthValue - 1]}, ${weekdays[day.dayOfWeek.value - 1]}"

    fun shortDate(day: LocalDate): String =
        "${day.dayOfMonth} ${monthsShort[day.monthValue - 1]}"

    fun weekdayShort(day: LocalDate): String = weekdaysShort[day.dayOfWeek.value - 1]

    fun pluralDays(count: Int): String {
        val tail = count % 100
        if (tail in 11..14) return "дней"
        return when (count % 10) {
            1 -> "день"
            2, 3, 4 -> "дня"
            else -> "дней"
        }
    }

    /** 2.0 -> "2,0", 1.857 -> "1,9": built from tenths, so no string round-trip can double the tail. */
    fun oneDecimal(value: Double): String {
        val tenths = Math.round(value * 10)
        return "${tenths / 10},${Math.abs(tenths % 10)}"
    }
}
