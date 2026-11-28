package cocaser.app;

import android.app.DownloadManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.database.Cursor;
import android.net.Uri;
import java.io.File;
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
    public long startUpdate(String url) {
        try {
            if (url == null) return -1;
            File old = new File(ctx.getExternalFilesDir("updates"), "app.apk");
            if (old.exists()) old.delete();
            DownloadManager dm = (DownloadManager) ctx.getSystemService(Context.DOWNLOAD_SERVICE);
            DownloadManager.Request req = new DownloadManager.Request(Uri.parse(url));
            req.setTitle("Telegram Gifts");
            req.setMimeType("application/vnd.android.package-archive");
            req.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE);
            req.setDestinationInExternalFilesDir(ctx, "updates", "app.apk");
            return dm.enqueue(req);
        } catch (Throwable t) {
            return -1;
        }
    }

    @JavascriptInterface
    public String updateState(long id) {
        try {
            if (id < 0) return "none";
            DownloadManager dm = (DownloadManager) ctx.getSystemService(Context.DOWNLOAD_SERVICE);
            Cursor c = dm.query(new DownloadManager.Query());
            if (c == null) return "none";
            String out = "none";
            int sCol = c.getColumnIndex("status");
            int iCol = c.getColumnIndex("_id");
            if (c.moveToFirst()) {
                do {
                    if (c.getLong(iCol) == id) {
                        int st = c.getInt(sCol);
                        out = st == DownloadManager.STATUS_SUCCESSFUL ? "done"
                            : st == DownloadManager.STATUS_FAILED ? "fail" : "run";
                        break;
                    }
                } while (c.moveToNext());
            }
            c.close();
            return out;
        } catch (Throwable t) {
            return "none";
        }
    }

    @JavascriptInterface
    public String installUpdate() {
        try {
            if (android.os.Build.VERSION.SDK_INT >= 26
                    && !ctx.getPackageManager().canRequestPackageInstalls()) {
                Intent si = new Intent("android.settings.MANAGE_UNKNOWN_APP_SOURCES");
                si.setData(Uri.parse("package:cocaser.app"));
                si.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                ctx.startActivity(si);
                return "settings";
            }
            File f = new File(ctx.getExternalFilesDir("updates"), "app.apk");
            if (!f.exists()) return "nofile";
            Intent it = new Intent(Intent.ACTION_VIEW);
            it.setDataAndType(Uri.parse("content://cocaser.app.updates/app.apk"),
                    "application/vnd.android.package-archive");
            it.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            it.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            ctx.startActivity(it);
            return "ok";
        } catch (Throwable t) {
            return "error";
        }
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
