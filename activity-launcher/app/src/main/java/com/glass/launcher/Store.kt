package com.glass.launcher

import android.content.Context
import android.content.SharedPreferences
import androidx.compose.runtime.MutableState
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import org.json.JSONArray
import org.json.JSONObject
import kotlin.reflect.KProperty

class PrefState<T>(private val key: String, private val def: T) {
    private var state: MutableState<T>? = null

    @Suppress("UNCHECKED_CAST")
    private fun read(): T {
        val p = Store.prefs
        return when (def) {
            is Boolean -> p.getBoolean(key, def) as T
            is Int -> p.getInt(key, def) as T
            is Float -> p.getFloat(key, def) as T
            is Long -> p.getLong(key, def) as T
            is String -> (p.getString(key, def) ?: def) as T
            else -> def
        }
    }

    private fun write(v: T) {
        val e = Store.prefs.edit()
        when (v) {
            is Boolean -> e.putBoolean(key, v)
            is Int -> e.putInt(key, v)
            is Float -> e.putFloat(key, v)
            is Long -> e.putLong(key, v)
            is String -> e.putString(key, v)
        }
        e.apply()
    }

    private fun st(): MutableState<T> = state ?: mutableStateOf(read()).also { state = it }

    operator fun getValue(thisRef: Any?, property: KProperty<*>): T = st().value
    operator fun setValue(thisRef: Any?, property: KProperty<*>, value: T) {
        st().value = value
        write(value)
    }
}

object Store {
    lateinit var prefs: SharedPreferences
    private lateinit var appContext: Context

    val favorites = mutableStateListOf<ShortcutData>()
    val recents = mutableStateListOf<RecentEntry>()

    // Appearance
    var theme by PrefState("theme", "system")          // system | light | dark
    var accent by PrefState("accent", 0)               // -1 = dynamic (Android 12+)
    var blur by PrefState("blur", 26f)                 // dp
    var glassOpacity by PrefState("glass_opacity", 0.5f)
    var animatedBg by PrefState("animated_bg", true)
    var lang by PrefState("lang", "system")            // system | en | ru

    // Behaviour
    var showSystem by PrefState("show_system", true)
    var sort by PrefState("sort", "name")              // name | package | installed | updated | activities
    var exportedOnly by PrefState("exported_only", false)
    var rootFallback by PrefState("root_fallback", false)
    var historyEnabled by PrefState("history", true)
    var haptics by PrefState("haptics", true)
    var dynamicShortcuts by PrefState("dynamic_shortcuts", true)
    var compactList by PrefState("compact", false)

    fun init(ctx: Context) {
        appContext = ctx.applicationContext
        prefs = ctx.getSharedPreferences("glass_launcher", Context.MODE_PRIVATE)
        favorites.clear()
        favorites.addAll(readList("favorites") { ShortcutData.fromJson(it) })
        recents.clear()
        recents.addAll(readList("recents") { RecentEntry.fromJson(it) })
    }

    private fun <T> readList(key: String, map: (JSONObject) -> T): List<T> = try {
        val arr = JSONArray(prefs.getString(key, "[]"))
        List(arr.length()) { map(arr.getJSONObject(it)) }
    } catch (e: Exception) {
        emptyList()
    }

    fun saveFavorites() {
        val arr = JSONArray()
        favorites.forEach { arr.put(it.toJson()) }
        prefs.edit().putString("favorites", arr.toString()).apply()
        Launcher.updateDynamicShortcuts(appContext)
    }

    private fun saveRecents() {
        val arr = JSONArray()
        recents.forEach { arr.put(it.toJson()) }
        prefs.edit().putString("recents", arr.toString()).apply()
    }

    fun isFavorite(d: ShortcutData) = favorites.any { it.key() == d.key() }

    fun toggleFavorite(d: ShortcutData): Boolean {
        val idx = favorites.indexOfFirst { it.key() == d.key() }
        return if (idx >= 0) {
            favorites.removeAt(idx); saveFavorites(); false
        } else {
            favorites.add(d); saveFavorites(); true
        }
    }

    fun updateFavorite(d: ShortcutData) {
        val idx = favorites.indexOfFirst { it.id == d.id || it.key() == d.key() }
        if (idx >= 0) favorites[idx] = d else favorites.add(d)
        saveFavorites()
    }

    fun removeFavorite(d: ShortcutData) {
        favorites.removeAll { it.id == d.id }
        saveFavorites()
    }

    fun moveFavorite(from: Int, to: Int) {
        if (from !in favorites.indices || to !in favorites.indices) return
        val item = favorites.removeAt(from)
        favorites.add(to, item)
        saveFavorites()
    }

    fun addRecent(d: ShortcutData) {
        if (!historyEnabled) return
        val idx = recents.indexOfFirst { it.data.key() == d.key() }
        var count = 1
        if (idx >= 0) {
            count = recents[idx].count + 1
            recents.removeAt(idx)
        }
        recents.add(0, RecentEntry(d, System.currentTimeMillis(), count))
        while (recents.size > 150) recents.removeAt(recents.lastIndex)
        saveRecents()
    }

    fun removeRecent(e: RecentEntry) {
        recents.remove(e); saveRecents()
    }

    fun clearRecents() {
        recents.clear(); saveRecents()
    }

    fun exportJson(): String = JSONObject()
        .put("app", "GlassActivityLauncher")
        .put("version", 1)
        .put("favorites", JSONArray().apply { favorites.forEach { put(it.toJson()) } })
        .toString(2)

    /** Returns number of imported favorites. */
    fun importJson(text: String): Int {
        val trimmed = text.trim()
        val arr = if (trimmed.startsWith("[")) JSONArray(trimmed) else JSONObject(trimmed).getJSONArray("favorites")
        var n = 0
        for (i in 0 until arr.length()) {
            val d = ShortcutData.fromJson(arr.getJSONObject(i))
            if (!isFavorite(d)) {
                favorites.add(d); n++
            }
        }
        saveFavorites()
        return n
    }
}
