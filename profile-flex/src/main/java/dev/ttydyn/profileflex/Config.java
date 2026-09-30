package dev.ttydyn.profileflex;

import android.content.Context;
import android.util.Log;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;

/**
 * Настройки модуля.
 *
 * Публичного API настроек у стабильной части мода нет (в дексе официального модуля
 * экраны настроек рисует сам модуль), поэтому конфиг читается из JSON-файла:
 *
 *   Android/data/&lt;пакет TikTok&gt;/files/profile_flex.json
 *
 * Файл перечитывается автоматически при изменении — перезапуск приложения не нужен.
 */
public final class Config {

    private static final String TAG = "ProfileFlex";
    private static final String FILE_NAME = "profile_flex.json";
    private static final long RECHECK_MS = 3000L;

    private final File file;
    private long lastCheck;
    private long lastModified = -1L;

    public boolean enabled = true;
    /** Применять только к своему профилю (uid/uniqueId ниже) или ко всем пользователям. */
    public boolean selfOnly = true;
    public String uid = "";
    public String uniqueId = "";

    /** none | personal | business */
    public String badge = "personal";
    public String badgeLabel = "";

    public boolean spoofCounters = true;
    public long followerCount = 1000000L;
    public long followingCount = 42L;
    public long likeCount = 25000000L;
    public long videoCount = 128L;
    public long friendsCount = 0L;

    public boolean spoofVideoStats = false;
    public long videoDiggCount = 500000L;
    public long videoPlayCount = 10000000L;
    public long videoCommentCount = 12000L;
    public long videoShareCount = 8000L;

    public boolean verbose = false;

    private Config(File file) {
        this.file = file;
    }

    public static Config load(Context context) {
        File directory = context.getExternalFilesDir(null);
        if (directory == null) {
            directory = context.getFilesDir();
        }
        Config config = new Config(new File(directory, FILE_NAME));
        config.reloadIfChanged(true);
        return config;
    }

    public File path() {
        return file;
    }

    /** Дёшево: файл трогаем не чаще раза в 3 секунды и только если изменился mtime. */
    public void reloadIfChanged(boolean force) {
        long now = System.currentTimeMillis();
        if (!force && now - lastCheck < RECHECK_MS) {
            return;
        }
        lastCheck = now;
        if (!file.isFile()) {
            return;
        }
        long modified = file.lastModified();
        if (!force && modified == lastModified) {
            return;
        }
        try {
            apply(new JSONObject(read(file)));
            // Не запоминаем mtime до успешного разбора: после временно
            // недописанного файла конфиг автоматически попробуется снова.
            lastModified = modified;
            Log.i(TAG, "config reloaded from " + file);
        } catch (Throwable error) {
            Log.w(TAG, "bad config " + file + " (will retry)", error);
        }
    }

    private void apply(JSONObject json) {
        enabled = json.optBoolean("enabled", enabled);
        selfOnly = json.optBoolean("selfOnly", selfOnly);
        uid = json.optString("uid", uid);
        uniqueId = json.optString("uniqueId", uniqueId);

        badge = json.optString("badge", badge);
        badgeLabel = json.optString("badgeLabel", badgeLabel);

        spoofCounters = json.optBoolean("spoofCounters", spoofCounters);
        followerCount = json.optLong("followerCount", followerCount);
        followingCount = json.optLong("followingCount", followingCount);
        likeCount = json.optLong("likeCount", likeCount);
        videoCount = json.optLong("videoCount", videoCount);
        friendsCount = json.optLong("friendsCount", friendsCount);

        spoofVideoStats = json.optBoolean("spoofVideoStats", spoofVideoStats);
        videoDiggCount = json.optLong("videoDiggCount", videoDiggCount);
        videoPlayCount = json.optLong("videoPlayCount", videoPlayCount);
        videoCommentCount = json.optLong("videoCommentCount", videoCommentCount);
        videoShareCount = json.optLong("videoShareCount", videoShareCount);

        verbose = json.optBoolean("verbose", verbose);
    }

    private static String read(File source) throws Exception {
        InputStream stream = new FileInputStream(source);
        try {
            ByteArrayOutputStream buffer = new ByteArrayOutputStream();
            byte[] chunk = new byte[4096];
            int read;
            while ((read = stream.read(chunk)) > 0) {
                buffer.write(chunk, 0, read);
            }
            return buffer.toString("UTF-8");
        } finally {
            stream.close();
        }
    }

    /** verificationType, который ожидает UI TikTok: 0 — нет, 2 — личная, 3 — бизнес. */
    public int verificationType() {
        if ("business".equalsIgnoreCase(badge)) {
            return 3;
        }
        if ("none".equalsIgnoreCase(badge)) {
            return 0;
        }
        return 2;
    }

    public boolean badgeEnabled() {
        return !"none".equalsIgnoreCase(badge);
    }

    public String customVerify() {
        return badgeLabel == null || badgeLabel.length() == 0 ? "verified" : badgeLabel;
    }
}
