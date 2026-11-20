package lav.udalenka.alav;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;

import java.util.ArrayList;
import java.util.List;

/** Локальная SQLite-база сохранённых уведомлений. Никакого облака. */
final class Db extends SQLiteOpenHelper {

    private static final String NAME = "udalenka.db";
    private static final int VERSION = 1;
    private static Db instance;

    static synchronized Db get(Context context) {
        if (instance == null) {
            instance = new Db(context.getApplicationContext());
        }
        return instance;
    }

    private Db(Context context) {
        super(context, NAME, null, VERSION);
    }

    @Override
    public void onCreate(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE messages("
                + "_id INTEGER PRIMARY KEY AUTOINCREMENT,"
                + "pkg TEXT NOT NULL,"
                + "convo TEXT NOT NULL,"
                + "sender TEXT NOT NULL,"
                + "text TEXT NOT NULL,"
                + "ts INTEGER NOT NULL,"
                + "deleted INTEGER NOT NULL DEFAULT 0)");
        db.execSQL("CREATE UNIQUE INDEX idx_dedupe ON messages(pkg, convo, sender, ts, text)");
        db.execSQL("CREATE INDEX idx_convo ON messages(pkg, convo, ts)");
        db.execSQL("CREATE TABLE app_enabled(pkg TEXT PRIMARY KEY, enabled INTEGER NOT NULL)");
        db.execSQL("CREATE TABLE settings(k TEXT PRIMARY KEY, v TEXT)");
    }

    @Override
    public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
    }

    static final class AppRow {
        String pkg;
        int convos;
        int total;
        int deletedCount;
        long last;
    }

    static final class ConvoRow {
        String convo;
        String lastText;
        long lastTs;
        int total;
        boolean lastDeleted;
    }

    static final class Msg {
        long id;
        String sender;
        String text;
        long ts;
        boolean deleted;
        String convoPkg;
        String convoName;
    }

    /** Вставка с дедупликацией (MessagingStyle присылает весь диалог заново). */
    synchronized boolean insert(String pkg, String convo, String sender,
                                String text, long ts) {
        ContentValues v = new ContentValues();
        v.put("pkg", pkg);
        v.put("convo", convo);
        v.put("sender", sender);
        v.put("text", text);
        v.put("ts", ts);
        long id = getWritableDatabase().insertWithOnConflict(
                "messages", null, v, SQLiteDatabase.CONFLICT_IGNORE);
        return id >= 0;
    }

    /** Помечает последнее непомеченное сообщение диалога как удалённое. */
    synchronized void markLastDeleted(String pkg, String convo) {
        getWritableDatabase().execSQL(
                "UPDATE messages SET deleted=1 WHERE _id = ("
                        + "SELECT _id FROM messages WHERE pkg=? AND convo=? AND deleted=0 "
                        + "ORDER BY ts DESC LIMIT 1)",
                new Object[]{pkg, convo});
    }

    synchronized List<AppRow> listApps() {
        List<AppRow> out = new ArrayList<>();
        Cursor c = getReadableDatabase().rawQuery(
                "SELECT pkg, COUNT(DISTINCT convo), COUNT(*), "
                        + "SUM(deleted), MAX(ts) FROM messages GROUP BY pkg ORDER BY MAX(ts) DESC",
                null);
        while (c.moveToNext()) {
            AppRow r = new AppRow();
            r.pkg = c.getString(0);
            r.convos = c.getInt(1);
            r.total = c.getInt(2);
            r.deletedCount = c.getInt(3);
            r.last = c.getLong(4);
            out.add(r);
        }
        c.close();
        return out;
    }

    synchronized List<ConvoRow> listConvos(String pkg) {
        List<ConvoRow> out = new ArrayList<>();
        Cursor c = getReadableDatabase().rawQuery(
                "SELECT convo, COUNT(*), MAX(ts) FROM messages "
                        + "WHERE pkg=? GROUP BY convo ORDER BY MAX(ts) DESC",
                new String[]{pkg});
        while (c.moveToNext()) {
            ConvoRow r = new ConvoRow();
            r.convo = c.getString(0);
            r.total = c.getInt(1);
            r.lastTs = c.getLong(2);
            Cursor last = getReadableDatabase().rawQuery(
                    "SELECT text, deleted FROM messages WHERE pkg=? AND convo=? "
                            + "ORDER BY ts DESC LIMIT 1",
                    new String[]{pkg, r.convo});
            if (last.moveToNext()) {
                r.lastText = last.getString(0);
                r.lastDeleted = last.getInt(1) == 1;
            }
            last.close();
            out.add(r);
        }
        c.close();
        return out;
    }

    synchronized List<Msg> listMessages(String pkg, String convo, String query) {
        List<Msg> out = new ArrayList<>();
        String sql = "SELECT _id, sender, text, ts, deleted FROM messages "
                + "WHERE pkg=? AND convo=?";
        String[] args;
        if (query != null && query.length() > 0) {
            sql += " AND text LIKE ?";
            args = new String[]{pkg, convo, "%" + query + "%"};
        } else {
            args = new String[]{pkg, convo};
        }
        Cursor c = getReadableDatabase().rawQuery(sql + " ORDER BY ts ASC", args);
        while (c.moveToNext()) {
            Msg m = new Msg();
            m.id = c.getLong(0);
            m.sender = c.getString(1);
            m.text = c.getString(2);
            m.ts = c.getLong(3);
            m.deleted = c.getInt(4) == 1;
            out.add(m);
        }
        c.close();
        return out;
    }

    /** Глобальный поиск по тексту всех сохранённых сообщений. */
    synchronized List<Msg> globalSearch(String query) {
        List<Msg> out = new ArrayList<>();
        Cursor c = getReadableDatabase().rawQuery(
                "SELECT _id, pkg, convo, sender, text, ts, deleted FROM messages "
                        + "WHERE text LIKE ? ORDER BY ts DESC LIMIT 300",
                new String[]{"%" + query + "%"});
        while (c.moveToNext()) {
            Msg m = new Msg();
            m.id = c.getLong(0);
            m.convoPkg = c.getString(1);
            m.convoName = c.getString(2);
            m.sender = c.getString(3);
            m.text = c.getString(4);
            m.ts = c.getLong(5);
            m.deleted = c.getInt(6) == 1;
            out.add(m);
        }
        c.close();
        return out;
    }

    synchronized int totalCount() {
        Cursor c = getReadableDatabase().rawQuery("SELECT COUNT(*) FROM messages", null);
        int n = c.moveToNext() ? c.getInt(0) : 0;
        c.close();
        return n;
    }

    synchronized void clearAll() {
        getWritableDatabase().delete("messages", null, null);
    }

    synchronized void clearConvo(String pkg, String convo) {
        getWritableDatabase().delete("messages", "pkg=? AND convo=?",
                new String[]{pkg, convo});
    }

    /** Удаляет сообщения старше retentionDays (0 = хранить всегда). */
    synchronized void purgeOld() {
        int days = retentionDays();
        if (days <= 0) {
            return;
        }
        long cutoff = System.currentTimeMillis() - days * 24L * 60L * 60L * 1000L;
        getWritableDatabase().delete("messages", "ts < ?",
                new String[]{String.valueOf(cutoff)});
    }

    synchronized boolean appEnabled(String pkg) {
        Cursor c = getReadableDatabase().rawQuery(
                "SELECT enabled FROM app_enabled WHERE pkg=?", new String[]{pkg});
        Boolean result = null;
        if (c.moveToNext()) {
            result = c.getInt(0) == 1;
        }
        c.close();
        return result == null || result;
    }

    synchronized void setAppEnabled(String pkg, boolean enabled) {
        ContentValues v = new ContentValues();
        v.put("pkg", pkg);
        v.put("enabled", enabled ? 1 : 0);
        getWritableDatabase().insertWithOnConflict("app_enabled", null, v,
                SQLiteDatabase.CONFLICT_REPLACE);
    }

    synchronized int retentionDays() {
        Cursor c = getReadableDatabase().rawQuery(
                "SELECT v FROM settings WHERE k='retention'", null);
        int days = 0;
        if (c.moveToNext()) {
            try {
                days = Integer.parseInt(c.getString(0));
            } catch (NumberFormatException ignored) {
            }
        }
        c.close();
        return days;
    }

    synchronized void setRetentionDays(int days) {
        ContentValues v = new ContentValues();
        v.put("k", "retention");
        v.put("v", String.valueOf(days));
        getWritableDatabase().insertWithOnConflict("settings", null, v,
                SQLiteDatabase.CONFLICT_REPLACE);
    }
}
