package com.zalchik.grust.data

import java.time.LocalDate

/** One day's note: the mood level (1..5) and an optional free-form thought. */
data class MoodEntry(
    val day: LocalDate,
    val level: Int,
    val note: String = "",
) {
    val mood: Mood get() = Mood.of(level)
}
