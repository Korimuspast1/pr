package com.zalchik.grust.data

import android.content.Context

/** The diary lives in one SharedPreferences string: small, local, and no database to migrate. */
class MoodStore(context: Context) {
    private val prefs = context.getSharedPreferences("grust", Context.MODE_PRIVATE)

    fun load(): List<MoodEntry> = EntryCodec.decode(prefs.getString(KEY, null))

    fun save(entries: List<MoodEntry>) {
        prefs.edit().putString(KEY, EntryCodec.encode(entries)).apply()
    }

    private companion object {
        const val KEY = "diary"
    }
}
