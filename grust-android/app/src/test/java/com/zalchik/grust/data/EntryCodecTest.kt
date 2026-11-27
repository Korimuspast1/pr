package com.zalchik.grust.data

import java.time.LocalDate
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class EntryCodecTest {

    private val day = LocalDate.of(2026, 11, 27)

    @Test
    fun `entries survive a round trip`() {
        val entries = listOf(MoodEntry(day, 4, "тихий вечер"), MoodEntry(day.minusDays(1), 2, ""))
        val decoded = EntryCodec.decode(EntryCodec.encode(entries))
        assertEquals(entries.sortedBy { it.day }, decoded)
    }

    @Test
    fun `notes with quotes and newlines survive`() {
        val note = "он сказал \"ничего\"\nи ушёл"
        val decoded = EntryCodec.decode(EntryCodec.encode(listOf(MoodEntry(day, 1, note))))
        assertEquals(note, decoded.single().note)
    }

    @Test
    fun `nothing stored is an empty diary`() {
        assertTrue(EntryCodec.decode(null).isEmpty())
        assertTrue(EntryCodec.decode("").isEmpty())
    }

    @Test
    fun `a broken blob costs the diary, not the app`() {
        assertTrue(EntryCodec.decode("{не json").isEmpty())
    }

    @Test
    fun `rows with a bad level are dropped and the rest kept`() {
        val raw = """[{"d":20440,"l":9},{"d":20441,"l":3,"n":"ок"}]"""
        val decoded = EntryCodec.decode(raw)
        assertEquals(1, decoded.size)
        assertEquals(3, decoded.single().level)
    }

    @Test
    fun `a repeated day keeps one row`() {
        val raw = """[{"d":20440,"l":1},{"d":20440,"l":5}]"""
        assertEquals(1, EntryCodec.decode(raw).size)
    }

    @Test
    fun `encoded diary is sorted by day`() {
        val entries = listOf(MoodEntry(day, 1), MoodEntry(day.minusDays(3), 5))
        val decoded = EntryCodec.decode(EntryCodec.encode(entries))
        assertEquals(listOf(day.minusDays(3), day), decoded.map { it.day })
    }
}
