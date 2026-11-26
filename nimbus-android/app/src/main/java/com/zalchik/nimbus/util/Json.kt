package com.zalchik.nimbus.util

import org.json.JSONArray
import org.json.JSONObject
import org.json.JSONTokener

/**
 * The JSON tree Nimbus builds and edits, on top of Android's own org.json.
 *
 * Configs here are edited the way the desktop edits Python dicts (a provider's config gets its
 * inbounds replaced, its balancer tags remapped, allowInsecure stripped…), so the helpers keep
 * mutation and skip absent values instead of a typed, immutable model.
 */

/** Anything JSON: JSONObject, JSONArray, String, Number, Boolean or null. */
typealias Json = Any?

fun parseJson(text: String): Json = try {
    JSONTokener(text).nextValue()
} catch (_: Exception) {
    null
}

fun parseObject(text: String): JSONObject? = parseJson(text) as? JSONObject

/** JSON text. `indent` > 0 pretty-prints; non-ASCII is left as it is, as on desktop. */
fun stringify(node: Json, indent: Int = 0): String = when (node) {
    is JSONObject -> if (indent > 0) node.toString(indent) else node.toString()
    is JSONArray -> if (indent > 0) node.toString(indent) else node.toString()
    null -> "null"
    is String -> JSONObject.quote(node)
    else -> node.toString()
}

/** An object of the pairs whose value isn't null (absent options stay absent, as `omitempty`). */
fun obj(vararg pairs: Pair<String, Json>): JSONObject {
    val out = JSONObject()
    for ((key, value) in pairs) {
        if (value != null) out.put(key, value)
    }
    return out
}

/** An object of the pairs whose value is neither null nor an empty string/collection. */
fun objNonEmpty(vararg pairs: Pair<String, Json>): JSONObject {
    val out = JSONObject()
    for ((key, value) in pairs) {
        if (value != null && !isEmptyJson(value)) out.put(key, value)
    }
    return out
}

fun isEmptyJson(value: Json): Boolean = when (value) {
    null -> true
    is String -> value.isEmpty()
    is JSONArray -> value.length() == 0
    is JSONObject -> value.length() == 0
    is Boolean -> !value
    else -> false
}

fun arr(vararg items: Json): JSONArray {
    val out = JSONArray()
    for (item in items) if (item != null) out.put(item)
    return out
}

fun arr(items: Iterable<Json>): JSONArray {
    val out = JSONArray()
    for (item in items) if (item != null) out.put(item)
    return out
}

/** Every element of a list, as JSON values (a list of strings becomes a JSON array). */
fun listOfJson(value: Json): List<Json> = when (value) {
    is JSONArray -> (0 until value.length()).map { value.opt(it) }
    null -> emptyList()
    else -> listOf(value)
}

fun listOfStrings(value: Json): List<String> = listOfJson(value).map { it.toString() }

// -- accessors that never throw ---------------------------------------------------------------

fun JSONObject.str(key: String, fallback: String = ""): String = opt(key)?.let {
    if (it == JSONObject.NULL) null else it.toString()
} ?: fallback

fun JSONObject.strOrNull(key: String): String? = opt(key)?.takeIf { it != JSONObject.NULL }?.toString()

fun JSONObject.int(key: String, fallback: Int = 0): Int = when (val value = opt(key)) {
    is Number -> value.toInt()
    is String -> value.toIntOrNull() ?: fallback
    else -> fallback
}

fun JSONObject.long(key: String, fallback: Long = 0): Long = when (val value = opt(key)) {
    is Number -> value.toLong()
    is String -> value.toLongOrNull() ?: fallback
    else -> fallback
}

fun JSONObject.bool(key: String, fallback: Boolean = false): Boolean = when (val value = opt(key)) {
    is Boolean -> value
    is String -> when (value.lowercase()) {
        "true" -> true
        "false" -> false
        else -> fallback
    }
    is Number -> value.toInt() != 0
    else -> fallback
}

fun JSONObject.objOrNull(key: String): JSONObject? = opt(key) as? JSONObject

fun JSONObject.objOrNew(key: String): JSONObject = objOrNull(key) ?: JSONObject().also { put(key, it) }

fun JSONObject.arrOrEmpty(key: String): JSONArray = (opt(key) as? JSONArray) ?: JSONArray()

fun JSONArray.objects(): List<JSONObject> = (0 until length()).mapNotNull { optJSONObject(it) }

fun JSONArray.strings(): List<String> = (0 until length()).mapNotNull { opt(it)?.takeIf { v -> v != JSONObject.NULL }?.toString() }

fun JSONArray.copy(): JSONArray {
    val out = JSONArray()
    for (i in 0 until length()) out.put(deepCopyJson(opt(i)))
    return out
}

/** A deep copy, so editing a built config never reaches the stored one. */
fun deepCopyJson(node: Json): Json = when (node) {
    is JSONObject -> node.copy()
    is JSONArray -> node.copy()
    else -> node
}

fun JSONObject.copy(): JSONObject {
    val out = JSONObject()
    for (key in keys()) out.put(key, deepCopyJson(opt(key)))
    return out
}

/** Put only when the value isn't null: the config keeps provider fields it doesn't touch. */
fun JSONObject.putIf(key: String, value: Json): JSONObject {
    if (value != null) put(key, value)
    return this
}

fun JSONObject.putIfNonEmpty(key: String, value: Json): JSONObject {
    if (!isEmptyJson(value)) put(key, value)
    return this
}

/** Remove `key` everywhere in the tree, as desktop's drop_allow_insecure does. */
fun dropKey(node: Json, key: String) {
    when (node) {
        is JSONObject -> {
            node.remove(key)
            for (name in node.keys()) dropKey(node.opt(name), key)
        }
        is JSONArray -> for (i in 0 until node.length()) dropKey(node.opt(i), key)
    }
}

/** Every string in the tree, with the key it sits under. */
fun walkStrings(node: Json, key: String = "", visit: (key: String, value: String) -> Unit) {
    when (node) {
        is JSONObject -> for (name in node.keys()) walkStrings(node.opt(name), name, visit)
        is JSONArray -> for (i in 0 until node.length()) walkStrings(node.opt(i), key, visit)
        is String -> visit(key, node)
    }
}
