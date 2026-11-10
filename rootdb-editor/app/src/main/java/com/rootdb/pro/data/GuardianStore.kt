package com.rootdb.pro.data

import android.content.Context
import android.content.SharedPreferences
import org.json.JSONArray
import org.json.JSONObject

/** Хранилище списка отслеживаемых баз Guardian + лог событий */
class GuardianStore(context: Context) {
    private val prefs: SharedPreferences =
        context.getSharedPreferences("rootdb_guardian", Context.MODE_PRIVATE)

    var enabled: Boolean
        get() = prefs.getBoolean("enabled", false)
        set(v) = prefs.edit().putBoolean("enabled", v).apply()

    fun loadItems(): List<WatchItem> {
        val raw = prefs.getString("items", null) ?: return emptyList()
        val arr = try {
            JSONArray(raw)
        } catch (_: Exception) {
            return emptyList()
        }
        val out = mutableListOf<WatchItem>()
        for (i in 0 until arr.length()) {
            val o = arr.optJSONObject(i) ?: continue
            out += WatchItem(
                pkg = o.optString("pkg"),
                dbPath = o.optString("dbPath"),
                backupPath = o.optString("backupPath"),
                mode = runCatching { WatchMode.valueOf(o.optString("mode", "RESTORE")) }.getOrDefault(WatchMode.RESTORE),
                label = o.optString("label")
            )
        }
        return out
    }

    fun saveItems(items: List<WatchItem>) {
        val arr = JSONArray()
        items.forEach {
            arr.put(
                JSONObject()
                    .put("pkg", it.pkg)
                    .put("dbPath", it.dbPath)
                    .put("backupPath", it.backupPath)
                    .put("mode", it.mode.name)
                    .put("label", it.label)
            )
        }
        prefs.edit().putString("items", arr.toString()).apply()
    }

    fun addItem(item: WatchItem) {
        val items = loadItems().filterNot { it.dbPath == item.dbPath } + item
        saveItems(items)
    }

    fun removeItem(dbPath: String) {
        saveItems(loadItems().filterNot { it.dbPath == dbPath })
    }

    fun loadEvents(): List<GuardianEvent> {
        val raw = prefs.getString("events", null) ?: return emptyList()
        val arr = try {
            JSONArray(raw)
        } catch (_: Exception) {
            return emptyList()
        }
        val out = mutableListOf<GuardianEvent>()
        for (i in 0 until arr.length()) {
            val o = arr.optJSONObject(i) ?: continue
            out += GuardianEvent(o.optLong("t"), o.optString("text"))
        }
        return out
    }

    fun addEvent(text: String) {
        val events = (listOf(GuardianEvent(System.currentTimeMillis(), text)) + loadEvents()).take(60)
        val arr = JSONArray()
        events.forEach { arr.put(JSONObject().put("t", it.timeMs).put("text", it.text)) }
        prefs.edit().putString("events", arr.toString()).apply()
    }
}
