package app.ourcord.xposed.tweaks.bridge

import app.ourcord.bridge.asDelegate
import app.ourcord.reloadApp
import app.ourcord.xposed.api.registerNativeMethod
import app.ourcord.xposed.openFileGuarded
import app.ourcord.xposed.tweak
import java.io.File

/**
 * `ourcord.fs.*` + `ourcord.app.*` bridge methods.
 */
val additionalBridgeMethods by tweak {
    registerNativeMethod("ourcord.app.reload") {
        reloadApp()
    }

    withAppContext { ctx ->
        registerNativeMethod("ourcord.fs.getConstants") {
            mapOf(
                "data" to ctx.dataDir.absolutePath,
                "files" to ctx.filesDir.absolutePath,
                "cache" to ctx.cacheDir.absolutePath,
            )
        }

        registerNativeMethod("ourcord.fs.delete") { args ->
            val argv = args.asDelegate()
            val path by argv.string()
            val f = File(path)
            if (f.isDirectory) f.deleteRecursively() else f.delete()
        }

        registerNativeMethod("ourcord.fs.exists") { args ->
            val argv = args.asDelegate()
            val path by argv.string()
            File(path).exists()
        }

        registerNativeMethod("ourcord.fs.read") { args ->
            val argv = args.asDelegate()
            val path by argv.string()
            val file = File(path).also { it.openFileGuarded() }
            file.bufferedReader().use { it.readText() }
        }

        registerNativeMethod("ourcord.fs.write") { args ->
            val argv = args.asDelegate()
            val path by argv.string()
            val contents by argv.string()
            File(path).apply {
                if (isDirectory) throw Error("Path is a directory: $path")
                parentFile?.mkdirs()
                writeText(contents)
            }

            null
        }
    }
}
