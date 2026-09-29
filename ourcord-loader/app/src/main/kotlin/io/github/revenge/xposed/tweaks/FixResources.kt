package app.ourcord.xposed.tweaks

import android.content.res.Resources
import app.ourcord.xposed.OurCordConstants
import app.ourcord.xposed.hook
import app.ourcord.xposed.method
import app.ourcord.xposed.tweak

/**
 * Hooks [Resources.getIdentifier] to rewrite the package name to `com.discord` when the host app was repackaged.
 */
val fixResources by tweak {
    val hostPkg = appInfo.packageName
    if (hostPkg == OurCordConstants.TARGET_PACKAGE) return@tweak

    Resources::class.java.method(
        "getIdentifier",
        String::class.java,
        String::class.java,
        String::class.java,
    ).hook {
        before {
            if (args[2] == hostPkg) args[2] = OurCordConstants.TARGET_PACKAGE
        }
    }
}
