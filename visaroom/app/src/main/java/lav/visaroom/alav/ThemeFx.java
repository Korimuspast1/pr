package lav.visaroom.alav;

import android.content.Context;
import android.content.res.Configuration;

/**
 * Applies the app-wide Appearance preference before an Activity inflates
 * its layout.  Forcing the night qualifier here makes Android resolve the
 * matching values-night colors/styles and drawable-night surfaces without
 * adding AppCompat or any other dependency.
 */
final class ThemeFx {
    private ThemeFx() {
    }

    static Context wrap(Context base) {
        int mode = AppSettings.themeMode(base);
        if (mode == AppSettings.THEME_SYSTEM) {
            return base;
        }
        Configuration config = new Configuration(base.getResources().getConfiguration());
        int night = mode == AppSettings.THEME_DARK
                ? Configuration.UI_MODE_NIGHT_YES
                : Configuration.UI_MODE_NIGHT_NO;
        config.uiMode = (config.uiMode & ~Configuration.UI_MODE_NIGHT_MASK) | night;
        return base.createConfigurationContext(config);
    }

    static boolean isDark(Context context) {
        int mode = AppSettings.themeMode(context);
        if (mode == AppSettings.THEME_DARK) {
            return true;
        }
        if (mode == AppSettings.THEME_LIGHT) {
            return false;
        }
        int night = context.getResources().getConfiguration().uiMode
                & Configuration.UI_MODE_NIGHT_MASK;
        return night == Configuration.UI_MODE_NIGHT_YES;
    }
}
