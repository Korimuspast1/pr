package lav.visaroom.alav;

import android.content.Context;
import android.content.res.Configuration;
import android.os.LocaleList;

import java.util.Locale;

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
        String languageTag = AppSettings.languageTag(base);
        if (mode == AppSettings.THEME_SYSTEM && languageTag.length() == 0) {
            return base;
        }
        Configuration config = new Configuration(base.getResources().getConfiguration());
        if (languageTag.length() > 0) {
            Locale locale = Locale.forLanguageTag(languageTag);
            config.setLocale(locale);
            config.setLocales(new LocaleList(locale));
        }
        if (mode != AppSettings.THEME_SYSTEM) {
            int night = mode == AppSettings.THEME_DARK
                    ? Configuration.UI_MODE_NIGHT_YES
                    : Configuration.UI_MODE_NIGHT_NO;
            config.uiMode = (config.uiMode & ~Configuration.UI_MODE_NIGHT_MASK) | night;
        }
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
