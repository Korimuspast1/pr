package app.ourcord.xposed

import android.app.Activity
import android.content.Context
import android.content.pm.ApplicationInfo
import de.robv.android.xposed.IXposedHookLoadPackage
import de.robv.android.xposed.IXposedHookZygoteInit
import de.robv.android.xposed.callbacks.XC_LoadPackage
import app.ourcord.bridge.OurCordBridge
import app.ourcord.xposed.api.HostScope
import app.ourcord.xposed.tweaks.*
import app.ourcord.xposed.tweaks.base.lifecycleSupport
import app.ourcord.xposed.tweaks.base.scriptLoader
import app.ourcord.xposed.tweaks.bridge.OurCordBridgeRegistry
import app.ourcord.xposed.tweaks.bridge.additionalBridgeMethods
import app.ourcord.xposed.tweaks.bridge.ourcordBridgeSupport
import app.ourcord.xposed.tweaks.legacy.appearance.fonts
import app.ourcord.xposed.tweaks.legacy.appearance.sysColors
import app.ourcord.xposed.tweaks.legacy.appearance.themes
import app.ourcord.xposed.tweaks.legacy.ourcordPayloadGlobal
import app.ourcord.xposed.tweaks.discordVersionRetriever
import app.ourcord.xposed.tweaks.plugins.pluginLoader
import app.ourcord.xposed.tweaks.plugins.pluginStates
import app.ourcord.xposed.tweaks.plugins.repos.pluginRepos

private lateinit var modulePath: String

@Suppress("UNUSED")
class Main : IXposedHookLoadPackage, IXposedHookZygoteInit {
    @Volatile
    private var hooked = false

    private val tweaks: List<TweakSpec> = listOf(
        // Framework
        lifecycleSupport,
        ourcordBridgeSupport,
        scriptLoader,
        discordVersionRetriever,

        // Static patches
        fixResources,

        // Persistence
        caches,
        pluginStates,
        pluginRepos,

        // Async updater
        ourcordUpdater,

        // Consumers
        discordDevSupport,
        additionalBridgeMethods,
        fonts,
        themes,
        sysColors,
        pluginLoader,
        ourcordScriptLoader,
        ourcordPayloadGlobal,
    )

    override fun initZygote(startupParam: IXposedHookZygoteInit.StartupParam) {
        modulePath = startupParam.modulePath
    }

    override fun handleLoadPackage(param: XC_LoadPackage.LoadPackageParam) {
        // Only hook the main process.
        // Discord uses ProcessPhoenix to spawn a ":phoenix" process to restart the app. It will cause concurrency issues.
        if (param.processName != param.packageName) return

        if (hooked) return
        hooked = true

        val ctx = HostScopeImpl(
            modulePath = modulePath,
            appInfo = param.appInfo,
            classLoader = param.classLoader,
        )
        for (spec in tweaks) spec.applyTo(ctx)
    }
}

private class HostScopeImpl(
    override val modulePath: String,
    override val appInfo: ApplicationInfo,
    override val classLoader: ClassLoader,
) : HostScope {
    override val bridge: OurCordBridge get() = OurCordBridgeRegistry
    override fun withAppContext(block: (Context) -> Unit) = app.ourcord.xposed.tweaks.base.withAppContext(block)
    override fun withAppActivity(block: (Activity) -> Unit) =
        app.ourcord.xposed.tweaks.base.withAppActivity(block)
}