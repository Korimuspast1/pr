package lav.visaroom.alav;

import android.content.Context;
import android.content.SharedPreferences;

/** Local, non-sensitive app settings. */
final class AppSettings {

    private static final String PREFS = "settings";

    static final int BG_WHITE = 0;
    static final int BG_LIGHT = 1;
    static final int BG_DARK = 2;

    private AppSettings() {
    }

    private static SharedPreferences prefs(Context c) {
        return c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
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
                return "Small";
            case 2:
                return "Large";
            default:
                return "Medium";
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
                return "Light gray";
            case BG_DARK:
                return "Dark";
            default:
                return "White";
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
