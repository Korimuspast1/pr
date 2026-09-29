package app.ourcord.xposed

object OurCordConstants {
    const val TARGET_PACKAGE = "com.discord"
    const val TARGET_ACTIVITY = "$TARGET_PACKAGE.react_activities.ReactActivity"

    // @TODO: Migration to ourcord named dir
    const val FILES_DIR = "files/pyoncord"
    const val CACHE_DIR = "cache/ourcord"
    const val MAIN_SCRIPT_FILE = "bundle.js"
    const val PRELOADS_DIR = "preloads"

    const val LOADER_NAME = "OurCordXposed"
    val LOADER_VERSION
        get() = BuildConfig.VERSION_NAME
    val USER_AGENT
        get() = "OurCordXposed/$LOADER_VERSION"

    /**
     * Fallback Hermes bundle shipped inside this APK's `assets/` directory.
     * Loaded by [app.ourcord.xposed.tweaks.ourcordScriptLoader] when the cached `bundle.js` isn't available.
     */
    const val FALLBACK_BUNDLE_ASSET = "assets://ourcord.bundle"
}

/**
 * Hermes bytecode assets shipped inside the Xposed module APK.
 */
val scriptAssets = emptyList<String>()
