package m.chatrulet.m

import android.content.Context
import android.content.SharedPreferences
import androidx.compose.runtime.MutableState
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import org.json.JSONArray
import org.json.JSONObject
import java.util.Locale
import kotlin.random.Random
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

data class BlockEntry(val uid: String, val label: String, val ts: Long) {
    fun toJson(): JSONObject = JSONObject().put("u", uid).put("l", label).put("t", ts)
    companion object {
        fun fromJson(o: JSONObject) = BlockEntry(o.optString("u"), o.optString("l"), o.optLong("t"))
    }
}

object Store {
    lateinit var prefs: SharedPreferences
        private set

    // ── Внешний вид ──
    var theme by PrefState("theme", "system")          // system | light | dark
    var accent by PrefState("accent", 1)               // индекс в ACCENTS
    var blur by PrefState("blur", 26f)                 // dp
    var glassOpacity by PrefState("glass_opacity", 0.5f)
    var animatedBg by PrefState("animated_bg", true)
    var bgMode by PrefState("bg_mode", "blobs")        // blobs | gradient | solid
    var cornerRadius by PrefState("corner", 26f)
    var fontScale by PrefState("font_scale", 1f)
    var refraction by PrefState("refraction", 0.6f)
    var haptics by PrefState("haptics", true)
    var lang by PrefState("lang", "system")            // system | en | ru

    // ── Профиль и фильтры ──
    var nick by PrefState("nick", "")
    var country by PrefState("country", "")            // своя страна ("" = авто)
    var countryFilter by PrefState("country_filter", "")
    var gender by PrefState("gender", "")              // "" | "m" | "f"
    var genderFilter by PrefState("gender_filter", "")
    var langFilter by PrefState("lang_filter", "")
    var interests by PrefState("interests", "")        // csv ключей

    // ── Звонок ──
    var blurCam by PrefState("blur_cam", false)        // блюр своей камеры по умолчанию
    var speakerOn by PrefState("speaker", true)
    var autoNext by PrefState("auto_next", 0)          // сек; 0 = выкл
    var secureScreen by PrefState("secure_screen", false)
    var devBot by PrefState("dev_bot", false)          // тест: мгновенный звонок с ботом

    // ── Эффект касания (волна + глоу) ──
    var touchRipples by PrefState("touch_ripples", true)
    var touchSound by PrefState("touch_sound", false)
    var touchVibrate by PrefState("touch_vibrate", true)

    // ── Служебное ──
    var agreed18 by PrefState("agreed18", false)
    var statsCalls by PrefState("stats_calls", 0)
    var statsMinutes by PrefState("stats_minutes", 0)

    val blocked = mutableStateListOf<BlockEntry>()
    val liked = mutableStateListOf<String>()           // uid понравившихся (локально)

    fun init(ctx: Context) {
        prefs = ctx.getSharedPreferences("glass_roulette", Context.MODE_PRIVATE)
        Vault.init()
        loadBlocked()
        if (nick.isBlank()) {
            nick = tr("Гость", "Guest") + "-" + Random.nextInt(1000, 9999)
        }
    }

    fun myCountry(): String = country.ifBlank { Locale.getDefault().country }.uppercase()
    fun myLang(): String = when (lang) {
        "system" -> Locale.getDefault().language
        else -> lang
    }
    fun interestsSet(): Set<String> = interests.split(',').filter { it.isNotBlank() }.toSet()

    fun isBlocked(uid: String) = blocked.any { it.uid == uid }

    fun block(uid: String, label: String) {
        if (uid.isBlank() || isBlocked(uid)) return
        blocked.add(0, BlockEntry(uid, label, System.currentTimeMillis()))
        saveBlocked()
    }

    fun unblock(uid: String) {
        blocked.removeAll { it.uid == uid }
        saveBlocked()
    }

    private fun saveBlocked() {
        val arr = JSONArray()
        blocked.forEach { arr.put(it.toJson()) }
        // Список блокировок хранится зашифрованным (AES-256-GCM, ключ в AndroidKeyStore).
        prefs.edit().putString("blocked_vault", Vault.encrypt(arr.toString())).apply()
    }

    private fun loadBlocked() {
        blocked.clear()
        val raw = prefs.getString("blocked_vault", "") ?: ""
        if (raw.isBlank()) return
        try {
            val plain = Vault.decrypt(raw) ?: return
            val arr = JSONArray(plain)
            for (i in 0 until arr.length()) blocked.add(BlockEntry.fromJson(arr.getJSONObject(i)))
        } catch (e: Throwable) {
            // повреждённое хранилище — начинаем с чистого листа
        }
    }
}
