package lav.udalenka.alav;

import android.content.Context;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.graphics.drawable.Drawable;
import android.provider.Settings;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

final class Ui {

    private Ui() {
    }

    static int dp(Context c, int v) {
        return (int) (v * c.getResources().getDisplayMetrics().density + 0.5f);
    }

    static String appLabel(Context c, String pkg) {
        try {
            PackageManager pm = c.getPackageManager();
            ApplicationInfo info = pm.getApplicationInfo(pkg, 0);
            CharSequence label = pm.getApplicationLabel(info);
            return label == null ? pkg : label.toString();
        } catch (PackageManager.NameNotFoundException e) {
            return pkg;
        }
    }

    static Drawable appIcon(Context c, String pkg) {
        try {
            return c.getPackageManager().getApplicationIcon(pkg);
        } catch (PackageManager.NameNotFoundException e) {
            return c.getDrawable(R.drawable.ic_app_fallback);
        }
    }

    /** Стабильный цвет аватара по имени (как в Telegram). */
    static int avatarColor(String key) {
        int[] palette = {
                0xFF3390EC, 0xFFF54E4E, 0xFFFA8C23, 0xFF9A6BF0,
                0xFF4FCD5E, 0xFF00B8C4, 0xFFE864A0
        };
        int h = key == null ? 0 : Math.abs(key.hashCode());
        return palette[h % palette.length];
    }

    static String relTime(long ts) {
        long now = System.currentTimeMillis();
        long diff = now - ts;
        SimpleDateFormat fmt;
        if (diff < 60_000L) {
            return "now";
        }
        if (diff < 3_600_000L) {
            return (diff / 60_000L) + "m";
        }
        if (sameDay(ts, now)) {
            fmt = new SimpleDateFormat("HH:mm", Locale.getDefault());
        } else if (diff < 6L * 24L * 3600L * 1000L) {
            fmt = new SimpleDateFormat("EEE", Locale.getDefault());
        } else {
            fmt = new SimpleDateFormat("dd.MM.yy", Locale.getDefault());
        }
        return fmt.format(new Date(ts));
    }

    static String fullTime(long ts) {
        return new SimpleDateFormat("dd.MM.yyyy HH:mm:ss", Locale.getDefault())
                .format(new Date(ts));
    }

    private static boolean sameDay(long a, long b) {
        SimpleDateFormat f = new SimpleDateFormat("yyyyDDD", Locale.US);
        return f.format(new Date(a)).equals(f.format(new Date(b)));
    }

    /** Включён ли доступ к уведомлениям для нашего listener-сервиса. */
    static boolean listenerEnabled(Context c) {
        String flat = Settings.Secure.getString(c.getContentResolver(),
                "enabled_notification_listeners");
        return flat != null && flat.contains(c.getPackageName());
    }

    static void openListenerSettings(Context c) {
        Intent intent = new Intent(
                "android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS");
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        try {
            c.startActivity(intent);
        } catch (RuntimeException ignored) {
        }
    }
}
