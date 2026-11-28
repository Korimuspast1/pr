package cocaser.app;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Vibrator;
import android.webkit.JavascriptInterface;

public class NativeBridge {
    private final Context ctx;

    public NativeBridge(Context ctx) {
        this.ctx = ctx;
    }

    @JavascriptInterface
    public String getPref(String key) {
        if (key == null) return "";
        SharedPreferences sp = ctx.getSharedPreferences("cocaser_prefs", Context.MODE_PRIVATE);
        String v = sp.getString(key, "");
        return v != null ? v : "";
    }

    @JavascriptInterface
    public void setPref(String key, String val) {
        if (key == null) return;
        SharedPreferences sp = ctx.getSharedPreferences("cocaser_prefs", Context.MODE_PRIVATE);
        SharedPreferences.Editor ed = sp.edit();
        if (val == null || val.isEmpty()) {
            ed.remove(key);
        } else {
            ed.putString(key, val);
        }
        ed.apply();
    }

    @JavascriptInterface
    public void vibrate(int ms) {
        try {
            if (ms <= 0) return;
            Vibrator v = (Vibrator) ctx.getSystemService(Context.VIBRATOR_SERVICE);
            if (v != null) {
                v.vibrate((long) ms);
            }
        } catch (Throwable ignored) {
        }
    }
}
