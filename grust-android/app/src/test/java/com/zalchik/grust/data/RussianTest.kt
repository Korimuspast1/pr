package com.zalchik.grust.data

import java.time.LocalDate
import org.junit.Assert.assertEquals
import org.junit.Test

class RussianTest {

    @Test
    fun `a whole number keeps one decimal and does not double the tail`() {
        // This is the "2,0,0" that shipped in the first build: toString gave "2.0", the dot became a
        // comma, and then the missing decimal was appended anyway.
        assertEquals("2,0", Russian.oneDecimal(2.0))
        assertEquals("3,0", Russian.oneDecimal(3.0))
    }

    @Test
    fun `a fraction is rounded to one digit`() {
        assertEquals("1,9", Russian.oneDecimal(1.857))
        assertEquals("2,4", Russian.oneDecimal(2.44))
        assertEquals("1,0", Russian.oneDecimal(0.96))
    }

    @Test
    fun `the plural of days follows the Russian rule`() {
        assertEquals("день", Russian.pluralDays(1))
        assertEquals("дня", Russian.pluralDays(3))
        assertEquals("дней", Russian.pluralDays(5))
        assertEquals("дней", Russian.pluralDays(11))
        assertEquals("дней", Russian.pluralDays(12))
        assertEquals("дней", Russian.pluralDays(14))
        assertEquals("день", Russian.pluralDays(21))
        assertEquals("дня", Russian.pluralDays(22))
        assertEquals("дней", Russian.pluralDays(25))
        assertEquals("дней", Russian.pluralDays(0))
    }

    @Test
    fun `dates are written the way a diary would write them`() {
        val friday = LocalDate.of(2026, 11, 27)
        assertEquals("27 ноября, пятница", Russian.longDate(friday))
        assertEquals("27 ноя", Russian.shortDate(friday))
        assertEquals("пт", Russian.weekdayShort(friday))
        // Monday-first, the way the calendar is read here.
        assertEquals("пн", Russian.weekdayShort(LocalDate.of(2026, 11, 23)))
        assertEquals("вс", Russian.weekdayShort(LocalDate.of(2026, 11, 29)))
    }
}
