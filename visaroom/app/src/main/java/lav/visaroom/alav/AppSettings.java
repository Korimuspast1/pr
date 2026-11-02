package lav.visaroom.alav;

import android.content.Context;
import android.content.SharedPreferences;

/** Local, non-sensitive app settings. */
final class AppSettings {

    private static final String PREFS = "settings";

    static final int BG_WHITE = 0;
    static final int BG_LIGHT = 1;
    static final int BG_DARK = 2;

    static final int THEME_SYSTEM = 0;
    static final int THEME_LIGHT = 1;
    static final int THEME_DARK = 2;

    static final int LANG_SYSTEM = 0;
    static final int LANG_ENGLISH = 1;
    static final int LANG_RUSSIAN = 2;
    static final int LANG_UKRAINIAN = 3;
    static final int LANG_SPANISH = 4;
    static final int LANG_GERMAN = 5;
    static final int LANG_FRENCH = 6;
    static final int LANG_TURKISH = 7;
    static final int LANG_PORTUGUESE = 8;

    private AppSettings() {
    }

    private static SharedPreferences prefs(Context c) {
        return c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    static int themeMode(Context c) {
        int mode = prefs(c).getInt("appearance_theme", THEME_SYSTEM);
        return mode >= THEME_SYSTEM && mode <= THEME_DARK ? mode : THEME_SYSTEM;
    }

    static void setThemeMode(Context c, int mode) {
        int safe = mode >= THEME_SYSTEM && mode <= THEME_DARK ? mode : THEME_SYSTEM;
        prefs(c).edit().putInt("appearance_theme", safe).apply();
    }

    static int language(Context c) {
        int value = prefs(c).getInt("app_language", LANG_SYSTEM);
        return value >= LANG_SYSTEM && value <= LANG_PORTUGUESE
                ? value : LANG_SYSTEM;
    }

    static void setLanguage(Context c, int value) {
        int safe = value >= LANG_SYSTEM && value <= LANG_PORTUGUESE
                ? value : LANG_SYSTEM;
        prefs(c).edit().putInt("app_language", safe).commit();
    }

    static String languageTag(Context c) {
        switch (language(c)) {
            case LANG_ENGLISH:
                return "en";
            case LANG_RUSSIAN:
                return "ru";
            case LANG_UKRAINIAN:
                return "uk";
            case LANG_SPANISH:
                return "es";
            case LANG_GERMAN:
                return "de";
            case LANG_FRENCH:
                return "fr";
            case LANG_TURKISH:
                return "tr";
            case LANG_PORTUGUESE:
                return "pt";
            default:
                return "";
        }
    }

    static String[] languageOptions(Context c) {
        return new String[]{
                c.getString(R.string.language_system),
                "English",
                "Русский",
                "Українська",
                "Español",
                "Deutsch",
                "Français",
                "Türkçe",
                "Português"};
    }

    static String languageLabel(Context c) {
        return languageOptions(c)[language(c)];
    }

    static String themeLabel(Context c) {
        switch (themeMode(c)) {
            case THEME_LIGHT:
                return c.getString(R.string.theme_light);
            case THEME_DARK:
                return c.getString(R.string.theme_dark);
            default:
                return c.getString(R.string.language_system);
        }
    }

    static int textSizeIndex(Context c) {
        return prefs(c).getInt("text_size", 1);
    }

    static void setTextSizeIndex(Context c, int value) {
        prefs(c).edit().putInt("text_size", value).apply();
    }

    static int textSizeSp(Context c) {
        switch (textSizeIndex(c)) {
            case 0:
                return 14;
            case 2:
                return 18;
            default:
                return 16;
        }
    }

    static String textSizeLabel(Context c) {
        switch (textSizeIndex(c)) {
            case 0:
                return c.getString(R.string.text_size_small);
            case 2:
                return c.getString(R.string.text_size_large);
            default:
                return c.getString(R.string.text_size_medium);
        }
    }

    static int chatBackground(Context c) {
        return prefs(c).getInt("chat_bg", BG_WHITE);
    }

    static void setChatBackground(Context c, int value) {
        prefs(c).edit().putInt("chat_bg", value).apply();
    }

    static String chatBackgroundLabel(Context c) {
        switch (chatBackground(c)) {
            case BG_LIGHT:
                return c.getString(R.string.chat_background_light_gray);
            case BG_DARK:
                return c.getString(R.string.theme_dark);
            default:
                return c.getString(R.string.chat_background_white);
        }
    }

    static boolean showTimestamps(Context c) {
        return prefs(c).getBoolean("timestamps", true);
    }

    static void setShowTimestamps(Context c, boolean value) {
        prefs(c).edit().putBoolean("timestamps", value).apply();
    }

    static boolean enterToSend(Context c) {
        return prefs(c).getBoolean("enter_send", true);
    }

    static void setEnterToSend(Context c, boolean value) {
        prefs(c).edit().putBoolean("enter_send", value).apply();
    }

    static boolean notifications(Context c) {
        return prefs(c).getBoolean("notifications", true);
    }

    static void setNotifications(Context c, boolean value) {
        prefs(c).edit().putBoolean("notifications", value).apply();
    }

    static boolean sounds(Context c) {
        return prefs(c).getBoolean("sounds", false);
    }

    static void setSounds(Context c, boolean value) {
        prefs(c).edit().putBoolean("sounds", value).apply();
    }

    static boolean readReceipts(Context c) {
        return prefs(c).getBoolean("read_receipts", true);
    }

    static void setReadReceipts(Context c, boolean value) {
        prefs(c).edit().putBoolean("read_receipts", value).apply();
    }
}
