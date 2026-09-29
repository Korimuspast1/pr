package app.ourcord.xposed.tweaks.plugins.internal

import app.ourcord.plugins.PluginBuilder
import app.ourcord.plugins.PluginDependency
import app.ourcord.plugins.PluginManifest
import app.ourcord.plugins.plugin
import app.ourcord.xposed.tweaks.plugins.InternalPluginFlags
import app.ourcord.xposed.tweaks.plugins.PluginFactory

internal fun internalPlugin(
    manifest: PluginManifest,
    flags: Set<InternalPluginFlags> = emptySet(),
    block: PluginBuilder.() -> Unit
) = PluginFactory(plugin(block), manifest.withReservedDependencies(), flags)

/**
 * Injects the reserved dependencies ([RESERVED_DEPENDENCY_IDS]) at the ANY range,
 * to match requirements for all plugins (internal & external).
 */
private fun PluginManifest.withReservedDependencies(): PluginManifest {
    if (id in RESERVED_DEPENDENCY_IDS) return this
    val missing = RESERVED_DEPENDENCY_IDS - dependencies.keys
    if (missing.isEmpty()) return this
    return copy(dependencies = dependencies + missing.associateWith { PluginDependency() })
}

internal val internalPlugins: List<PluginFactory> by lazy {
    listOf(apiProviderPlugin, discordProviderPlugin, recoveryPlugin, noTrackPlugin, preventOtaUpdatesPlugin)
}