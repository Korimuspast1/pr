package com.glass.launcher

import android.app.Activity
import android.os.Bundle
import org.json.JSONObject

/** Trampoline used by pinned / dynamic shortcuts: rebuilds the stored intent and launches it. */
class ShortcutActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val json = intent?.getStringExtra(Launcher.EXTRA_JSON)
        if (json != null) {
            try {
                val d = ShortcutData.fromJson(JSONObject(json))
                Launcher.launchAndReport(this, d)
            } catch (e: Throwable) {
                Launcher.toast(this, e.message ?: "Error")
            }
        }
        finish()
    }
}
