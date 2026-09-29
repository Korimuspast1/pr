package app.ourcord.xposed.tweaks.plugins

import app.ourcord.Logger
import app.ourcord.xposed.api.HostScope
import app.ourcord.xposed.api.callJSMethod
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.launch

internal const val EVENT_DOWNLOAD_PROGRESS = "ourcord.plugins.events.downloadProgress"
internal const val EVENT_PLUGIN_INSTALL_READY = "ourcord.plugins.events.pluginInstallReady"
internal const val EVENT_PLUGIN_INSTALL_RESULT = "ourcord.plugins.events.pluginInstallResult"
internal const val EVENT_PLUGIN_UPDATED = "ourcord.plugins.events.pluginUpdated"
internal const val EVENT_PLUGIN_ERRORED = "ourcord.plugins.events.pluginErrored"
internal const val EVENT_REPO_STATE_UPDATE = "ourcord.plugins.events.repoStateUpdate"

/**
 * Fire-and-forget native-to-JS event. Failures are only logged.
 *
 * JS event handlers must resolve immediately (the callJSMethod reply queue is positional),
 * async answers must be through a separate bridge method instead.
 */
internal fun HostScope.emitPluginEvent(
    scope: CoroutineScope,
    log: Logger,
    name: String,
    payload: Map<String, Any?>,
) {
    scope.launch {
        runCatching { callJSMethod(name, listOf(payload)) }
            .onFailure { log.e("Failed to emit $name", it) }
    }
}
