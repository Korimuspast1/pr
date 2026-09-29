package app.ourcord.manager.ui.viewmodel.settings

import android.content.Context
import android.os.Environment
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import cafe.adriel.voyager.core.model.ScreenModel
import cafe.adriel.voyager.core.model.screenModelScope
import app.ourcord.manager.BuildConfig
import app.ourcord.manager.R
import app.ourcord.manager.domain.manager.InstallMethod
import app.ourcord.manager.domain.manager.PreferenceManager
import app.ourcord.manager.domain.manager.UpdateCheckerDuration
import app.ourcord.manager.installer.shizuku.ShizukuPermissions
import app.ourcord.manager.updatechecker.worker.UpdateWorker
import app.ourcord.manager.utils.showToast
import kotlinx.coroutines.launch
import java.io.File

class AdvancedSettingsViewModel(
    private val context: Context,
    private val prefs: PreferenceManager,
) : ScreenModel {
    private val cacheDir = context.externalCacheDir ?: File(Environment.getExternalStorageDirectory(), Environment.DIRECTORY_DOWNLOADS).resolve(
        BuildConfig.MANAGER_NAME).also { it.mkdirs() }

    fun clearCache() {
        cacheDir.deleteRecursively()
        context.showToast(R.string.msg_cleared_cache)
    }

    fun updateCheckerDuration(updateCheckerDuration: UpdateCheckerDuration) {
        val wm = WorkManager.getInstance(context)
        when (updateCheckerDuration) {
            UpdateCheckerDuration.DISABLED -> wm.cancelUniqueWork("app.ourcord.manager.UPDATE_CHECK")
            else -> wm.enqueueUniquePeriodicWork(
                "app.ourcord.manager.UPDATE_CHECK",
                ExistingPeriodicWorkPolicy.CANCEL_AND_REENQUEUE,
                PeriodicWorkRequestBuilder<UpdateWorker>(
                    updateCheckerDuration.time,
                    updateCheckerDuration.unit
                ).build()
            )
        }
    }

    fun setInstallMethod(method: InstallMethod) {
        when (method) {
            InstallMethod.SHIZUKU -> screenModelScope.launch {
                if (ShizukuPermissions.waitShizukuPermissions()) {
                    prefs.installMethod = InstallMethod.SHIZUKU
                } else {
                    context.showToast(R.string.msg_shizuku_denied)
                }
            }

            else -> prefs.installMethod = method
        }
    }

}