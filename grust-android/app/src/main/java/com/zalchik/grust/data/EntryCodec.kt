package com.zalchik.grust.data

import java.time.LocalDate
import org.json.JSONArray
import org.json.JSONObject

/**
 * The on-disk shape of the diary: a JSON array of {d: epochDay, l: level, n: note}.
 *
 * Kept apart from [MoodStore] on purpose - it touches no Android class, so a JVM unit test can
 * check that a diary written by one version of the app is read back by the next one.
 */
object EntryCodec {
    private const val DAY = "d"
    private const val LEVEL = "l"
    private const val NOTE = "n"

    fun encode(entries: List<MoodEntry>): String {
        val array = JSONArray()
        entries.sortedBy { it.day }.forEach { entry ->
            array.put(
                JSONObject()
                    .put(DAY, entry.day.toEpochDay())
                    .put(LEVEL, entry.level)
                    .put(NOTE, entry.note),
            )
        }
        return array.toString()
    }

    /**
     * Reads whatever is stored and never throws: a truncated or hand-edited blob costs the user the
     * broken rows, not the whole diary.
     */
    fun decode(raw: String?): List<MoodEntry> {
        if (raw.isNullOrBlank()) return emptyList()
        return try {
            val array = JSONArray(raw)
            (0 until array.length()).mapNotNull { index ->
                val row = array.optJSONObject(index) ?: return@mapNotNull null
                val day = row.optLong(DAY, Long.MIN_VALUE)
                val level = row.optInt(LEVEL, 0)
                if (day == Long.MIN_VALUE || level !in 1..5) return@mapNotNull null
                MoodEntry(LocalDate.ofEpochDay(day), level, row.optString(NOTE, ""))
            }.distinctBy { it.day }.sortedBy { it.day }
        } catch (_: Exception) {
            emptyList()
        }
    }
}
