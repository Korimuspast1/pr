package com.glass.launcher

import org.json.JSONArray
import org.json.JSONObject
import java.util.UUID

data class Extra(val key: String = "", val type: String = "string", val value: String = "") {
    fun toJson(): JSONObject = JSONObject().put("key", key).put("type", type).put("value", value)

    companion object {
        val TYPES = listOf("string", "int", "long", "bool", "float", "double", "uri", "string[]", "int[]", "long[]", "float[]", "null")
        fun fromJson(o: JSONObject) = Extra(o.optString("key"), o.optString("type", "string"), o.optString("value"))
    }
}

data class ShortcutData(
    val id: String = UUID.randomUUID().toString(),
    val name: String = "",
    val pkg: String = "",
    val cls: String = "",
    val icon: String = "",
    val action: String = "",
    val data: String = "",
    val mime: String = "",
    val categories: List<String> = emptyList(),
    val flags: Int = 0,
    val extras: List<Extra> = emptyList(),
    val root: Boolean = false,
    /** activity | service | foreground | broadcast */
    val target: String = "activity",
) {
    /** Identity used for favorites / recents de-duplication. */
    fun key(): String = listOf(target, pkg, cls, action, data, mime, categories.joinToString(","), flags.toString(),
        extras.joinToString(";") { "${it.type}:${it.key}=${it.value}" }).joinToString("|")

    val shortClass: String
        get() = if (pkg.isNotEmpty() && cls.startsWith(pkg)) cls.removePrefix(pkg) else cls

    val displayName: String
        get() = name.ifBlank { cls.substringAfterLast('.').ifBlank { action.substringAfterLast('.').ifBlank { pkg } } }

    fun toJson(): JSONObject = JSONObject()
        .put("id", id).put("name", name).put("pkg", pkg).put("cls", cls).put("icon", icon)
        .put("action", action).put("data", data).put("mime", mime)
        .put("categories", JSONArray(categories)).put("flags", flags)
        .put("extras", JSONArray().apply { extras.forEach { put(it.toJson()) } })
        .put("root", root)
        .put("target", target)

    companion object {
        fun fromJson(o: JSONObject): ShortcutData {
            val cats = o.optJSONArray("categories")
            val ex = o.optJSONArray("extras")
            return ShortcutData(
                id = o.optString("id").ifBlank { UUID.randomUUID().toString() },
                name = o.optString("name"),
                pkg = o.optString("pkg"),
                cls = o.optString("cls"),
                icon = o.optString("icon"),
                action = o.optString("action"),
                data = o.optString("data"),
                mime = o.optString("mime"),
                categories = if (cats == null) emptyList() else List(cats.length()) { cats.optString(it) },
                flags = o.optInt("flags"),
                extras = if (ex == null) emptyList() else List(ex.length()) { Extra.fromJson(ex.getJSONObject(it)) },
                root = o.optBoolean("root"),
                target = o.optString("target", "activity").ifBlank { "activity" },
            )
        }
    }
}

data class RecentEntry(val data: ShortcutData, val time: Long, val count: Int = 1) {
    fun toJson(): JSONObject = JSONObject().put("data", data.toJson()).put("time", time).put("count", count)

    companion object {
        fun fromJson(o: JSONObject) = RecentEntry(ShortcutData.fromJson(o.getJSONObject("data")), o.optLong("time"), o.optInt("count", 1))
    }
}
