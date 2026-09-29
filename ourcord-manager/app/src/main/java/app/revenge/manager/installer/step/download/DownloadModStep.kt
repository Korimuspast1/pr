package app.ourcord.manager.installer.step.download

import androidx.compose.runtime.Stable
import app.ourcord.manager.R
import app.ourcord.manager.installer.step.download.base.DownloadStep
import java.io.File

/**
 * Downloads the OurCord XPosed module
 *
 * https://github.com/Korimuspast1/pr/releases/tag/ourcord-v2.0.1
 */
@Stable
class DownloadModStep(
    workingDir: File
): DownloadStep() {

    override val nameRes = R.string.step_dl_mod

    override val downloadFullUrl: String = "https://github.com/Korimuspast1/pr/releases/download/ourcord-v2.0.1/OurCord-Loader.apk"
    override val destination = preferenceManager.moduleLocation
    override val workingCopy = workingDir.resolve("xposed.apk")

}
