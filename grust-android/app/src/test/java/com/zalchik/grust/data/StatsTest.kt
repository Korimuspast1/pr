package com.zalchik.grust.data

import java.time.LocalDate
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class StatsTest {

    private val today: LocalDate = LocalDate.of(2026, 11, 27)

    private fun entry(daysAgo: Long, level: Int, note: String = "") =
        MoodEntry(today.minusDays(daysAgo), level, note)

    @Test
    fun `no entries is no streak`() {
        assertEquals(0, Stats.streak(emptyList(), today))
    }

    @Test
    fun `streak counts consecutive days ending today`() {
        val entries = listOf(entry(0, 3), entry(1, 2), entry(2, 4), entry(4, 1))
        assertEquals(3, Stats.streak(entries, today))
    }

    @Test
    fun `a day not written yet does not break the streak`() {
        // Yesterday and the day before are filled, today is still empty: that is 2, not 0.
        val entries = listOf(entry(1, 2), entry(2, 3))
        assertEquals(2, Stats.streak(entries, today))
    }

    @Test
    fun `two missing days break the streak`() {
        val entries = listOf(entry(2, 2), entry(3, 3))
        assertEquals(0, Stats.streak(entries, today))
    }

    @Test
    fun `window is oldest first and keeps empty days`() {
        val entries = listOf(entry(0, 5), entry(2, 1))
        val window = Stats.window(entries, today, 3)
        assertEquals(3, window.size)
        // index 0 is two days ago, index 2 is today
        assertEquals(1, window[0]?.level)
        assertNull(window[1])
        assertEquals(5, window[2]?.level)
    }

    @Test
    fun `average ignores days without an entry`() {
        val window = Stats.window(listOf(entry(0, 4), entry(1, 2)), today, 5)
        assertEquals(3.0, Stats.average(window)!!, 0.0001)
        assertNull(Stats.average(Stats.window(emptyList(), today, 5)))
    }

    @Test
    fun `mood for average rounds to the nearest step`() {
        assertEquals(Mood.SAD, Stats.moodFor(2.4))
        assertEquals(Mood.FLAT, Stats.moodFor(2.6))
        assertEquals(Mood.LIGHT, Stats.moodFor(99.0))
        assertNull(Stats.moodFor(null))
    }

    @Test
    fun `distribution counts every step in scale order`() {
        val entries = listOf(entry(0, 2), entry(1, 2), entry(2, 5))
        val distribution = Stats.distribution(entries)
        assertEquals(listOf(Mood.HEAVY, Mood.SAD, Mood.FLAT, Mood.OK, Mood.LIGHT), distribution.map { it.first })
        assertEquals(listOf(0, 2, 0, 0, 1), distribution.map { it.second })
    }

    @Test
    fun `dominant prefers the heavier step on a tie`() {
        val entries = listOf(entry(0, 1), entry(1, 4))
        assertEquals(Mood.HEAVY, Stats.dominant(entries))
    }

    @Test
    fun `mood of an unknown level falls back to the middle`() {
        assertEquals(Mood.FLAT, Mood.of(9))
        assertEquals(Mood.FLAT, Mood.of(0))
    }

    @Test
    fun `every phrase and prompt list is not empty`() {
        assertTrue(Words.support.isNotEmpty())
        assertTrue(Words.prompts.isNotEmpty())
        // A phrase of the day is stable for a day and different across a run of days.
        assertEquals(Words.supportOfDay(today), Words.supportOfDay(today))
        assertTrue(Words.supportOfDay(today) in Words.support)
    }
}
