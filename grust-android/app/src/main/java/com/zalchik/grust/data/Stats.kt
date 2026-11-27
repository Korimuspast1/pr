package com.zalchik.grust.data

import java.time.LocalDate
import kotlin.math.roundToInt

/**
 * Everything the statistics screen shows, as pure functions of the diary.
 *
 * No Android, no clock of its own: every function takes "today" as an argument, which is also what
 * makes the numbers testable.
 */
object Stats {

    /**
     * Days in a row that have an entry.
     *
     * A day that is not written yet does not break a streak: if yesterday is filled and today is
     * still empty, the streak is counted up to yesterday. Otherwise opening the app in the morning
     * would show the effort of the past week as zero.
     */
    fun streak(entries: List<MoodEntry>, today: LocalDate): Int {
        val days = entries.mapTo(HashSet()) { it.day }
        if (days.isEmpty()) return 0
        var cursor = when {
            today in days -> today
            today.minusDays(1) in days -> today.minusDays(1)
            else -> return 0
        }
        var count = 0
        while (cursor in days) {
            count++
            cursor = cursor.minusDays(1)
        }
        return count
    }

    /** The last [days] days, oldest first, with a null where nothing was written. */
    fun window(entries: List<MoodEntry>, today: LocalDate, days: Int): List<MoodEntry?> {
        val byDay = entries.associateBy { it.day }
        return (days - 1 downTo 0).map { back -> byDay[today.minusDays(back.toLong())] }
    }

    fun average(window: List<MoodEntry?>): Double? {
        val values = window.mapNotNull { it?.level }
        return if (values.isEmpty()) null else values.average()
    }

    fun moodFor(average: Double?): Mood? =
        average?.let { Mood.of(it.roundToInt().coerceIn(1, 5)) }

    /** How many times each of the five steps was chosen, in scale order. */
    fun distribution(entries: List<MoodEntry>): List<Pair<Mood, Int>> =
        Mood.entries.map { mood -> mood to entries.count { it.level == mood.level } }

    /** The most frequent step; ties go to the heavier one, which is the honest choice here. */
    fun dominant(entries: List<MoodEntry>): Mood? =
        distribution(entries).filter { it.second > 0 }.maxByOrNull { it.second }?.first
}
