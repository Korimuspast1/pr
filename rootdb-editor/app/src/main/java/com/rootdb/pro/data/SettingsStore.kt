package com.rootdb.pro.data

import android.content.Context
import android.content.SharedPreferences

/** Настройки приложения (тема, автосохранение, Guardian) */
class SettingsStore(context: Context) {
    private val prefs: SharedPreferences =
        context.getSharedPreferences("rootdb_settings", Context.MODE_PRIVATE)

    /** 0 — системная, 1 — светлая, 2 — тёмная */
    var themeMode: Int
        get() = prefs.getInt("theme_mode", 0)
        set(v) = prefs.edit().putInt("theme_mode", v).apply()

    var dynamicColor: Boolean
        get() = prefs.getBoolean("dynamic_color", true)
        set(v) = prefs.edit().putBoolean("dynamic_color", v).apply()

    /** Писать правки обратно в приложение сразу после каждого изменения */
    var autoSave: Boolean
        get() = prefs.getBoolean("auto_save", true)
        set(v) = prefs.edit().putBoolean("auto_save", v).apply()

    /** Останавливать целевое приложение после сохранения */
    var forceStopAfterSave: Boolean
        get() = prefs.getBoolean("force_stop_after_save", true)
        set(v) = prefs.edit().putBoolean("force_stop_after_save", v).apply()

    /** Показывать системные приложения в списке */
    var showSystemApps: Boolean
        get() = prefs.getBoolean("show_system", false)
        set(v) = prefs.edit().putBoolean("show_system", v).apply()

    /** Интервал опроса Guardian, сек */
    var guardianIntervalSec: Int
        get() = prefs.getInt("guardian_interval", 3)
        set(v) = prefs.edit().putInt("guardian_interval", v).apply()

    /** Размер страницы строк в просмотрщике таблиц */
    var pageSize: Int
        get() = prefs.getInt("page_size", 100)
        set(v) = prefs.edit().putInt("page_size", v).apply()
}
