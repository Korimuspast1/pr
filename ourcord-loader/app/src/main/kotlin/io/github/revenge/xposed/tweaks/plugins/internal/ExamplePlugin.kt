package app.ourcord.xposed.tweaks.plugins.internal

import app.ourcord.plugins.API_VERSION
import app.ourcord.plugins.PluginManifest
import app.ourcord.xposed.api.registerNativeMethod
import app.ourcord.xposed.tweaks.plugins.InternalPluginFlags

private val manifest = PluginManifest(
    id = "ourcord.example",
    name = "Example Plugin",
    description = "Example plugin.",
    author = "OurCord",
    version = API_VERSION,
)

internal val examplePlugin =
    internalPlugin(manifest, setOf(InternalPluginFlags.INTERNAL, InternalPluginFlags.ESSENTIAL)) {
        start {
            log.i("started in ${appInfo.packageName}")
            registerNativeMethod("ourcord.example.test") { args ->
                log.i("ourcord.example.test($args)")
                null
            }

            registerNativeMethod("ourcord.example.test.error") {
                errors.tryEmit(Exception("Test exception!"))
            }
        }
    }