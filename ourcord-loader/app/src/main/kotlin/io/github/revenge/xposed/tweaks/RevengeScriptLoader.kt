package app.ourcord.xposed.tweaks

import app.ourcord.xposed.OurCordConstants
import app.ourcord.xposed.ensureDir
import app.ourcord.xposed.ensureFile
import app.ourcord.xposed.tweak
import app.ourcord.xposed.tweaks.base.InjectorScope
import app.ourcord.xposed.tweaks.base.registerScriptInjector
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import java.io.File

/**
 * Waits for script updates and loads the OurCord bundle. Depends on [app.ourcord.xposed.tweaks.base.scriptLoader].
 *
 * 1. Awaits the bundle download from [ourcordUpdater].
 * 2. Runs every file under `files/pyoncord/preloads/`.
 * 3. Loads `cache/ourcord/bundle.js` (downloaded copy) if present.
 * 4. Falls back to the in-APK `assets://ourcord.bundle` asset shipped with this module.
 */
val ourcordScriptLoader by tweak {
    val dataDir = appInfo.dataDir
    val filesDir = File(dataDir, OurCordConstants.FILES_DIR).apply { ensureDir() }
    val cacheDir = File(dataDir, OurCordConstants.CACHE_DIR).apply { ensureDir() }
    val preloadsDir = File(filesDir, OurCordConstants.PRELOADS_DIR).apply { ensureDir() }
    val mainScript = File(cacheDir, OurCordConstants.MAIN_SCRIPT_FILE).apply { ensureFile() }

    registerScriptInjector { scope: InjectorScope ->
        runOurCordScripts(scope, preloadsDir, mainScript)
    }
}

private fun runOurCordScripts(scope: InjectorScope, preloadsDir: File, mainScript: File) {
    val log = scope.tweakLog
    log.i("Running OurCord custom scripts...")

    runBlocking {
        try {
            withTimeout(OurCordUpdater.TIMEOUT) { OurCordUpdater.downloadReady.await() }
        } catch (e: Throwable) {
            log.w("Bundle download did not complete", e)
        }
    }

    try {
        preloadsDir.walk().filter { it.isFile }.sorted().forEach { f ->
            log.d("Running preload: ${f.absolutePath}")
            scope.runFile(f.absolutePath)
        }

        if (mainScript.exists()) {
            log.i("Loading downloaded bundle: ${mainScript.absolutePath}")
            scope.runFile(mainScript.absolutePath)
        } else {
            log.i("Downloaded bundle missing; falling back to ${OurCordConstants.FALLBACK_BUNDLE_ASSET}")
            scope.runAsset(OurCordConstants.FALLBACK_BUNDLE_ASSET)
        }
    } catch (e: Throwable) {
        log.e("Unable to run OurCord scripts", e)
    }
}

