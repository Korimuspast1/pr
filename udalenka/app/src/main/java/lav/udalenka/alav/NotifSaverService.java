package lav.udalenka.alav;

import android.app.Notification;
import android.content.Intent;
import android.os.Bundle;
import android.service.notification.NotificationListenerService;
import android.service.notification.StatusBarNotification;
import android.util.Log;

import java.util.Arrays;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;
import java.util.regex.Pattern;

/**
 * Ловит уведомления со ВСЕХ приложений (Telegram, WhatsApp, TikTok, VK, …)
 * и складывает их текст в локальную базу. Сообщение удалят у вас на глазах —
 * у нас оно останется. Плейеры/музыка/системные прогресс-бары отбрасываются.
 */
public class NotifSaverService extends NotificationListenerService {

    static final String ACTION_UPDATED = "lav.udalenka.alav.UPDATED";
    private static final String TAG = "Udalenka";

    private static final Set<String> SKIP_CATEGORIES = new HashSet<>(Arrays.asList(
            Notification.CATEGORY_SERVICE,
            Notification.CATEGORY_PROGRESS,
            Notification.CATEGORY_TRANSPORT,
            Notification.CATEGORY_STATUS,
            Notification.CATEGORY_SYS,
            Notification.CATEGORY_ALARM,
            Notification.CATEGORY_REMINDER,
            Notification.CATEGORY_ERROR,
            Notification.CATEGORY_CALL,
            Notification.CATEGORY_NAVIGATION));

    /** Маркеры «сообщение удалено» на разных языках (WhatsApp и др.). */
    private static final Pattern DELETED_MARKER = Pattern.compile(
            "this message was deleted"
                    + "|это сообщение было удалено"
                    + "|сообщение было удалено"
                    + "|сообщение удалено"
                    + "|message was deleted"
                    + "|nachricht wurde gelöscht"
                    + "|este mensaje fue eliminado"
                    + "|ce message a été supprimé"
                    + "|bu mesaj silindi",
            Pattern.CASE_INSENSITIVE);

    @Override
    public void onListenerConnected() {
        Log.i(TAG, "listener connected");
    }

    @Override
    public void onNotificationPosted(StatusBarNotification sbn) {
        try {
            handle(sbn);
        } catch (Throwable t) {
            Log.e(TAG, "capture failed", t);
        }
    }

    private void handle(StatusBarNotification sbn) {
        String pkg = sbn.getPackageName();
        if (pkg == null || pkg.equals(getPackageName())) {
            return;
        }
        Notification n = sbn.getNotification();
        if (n == null) {
            return;
        }
        // Не сохраняем «вечные» уведомления (музыка, загрузки, навигация).
        if ((n.flags & Notification.FLAG_ONGOING_EVENT) != 0) {
            return;
        }
        if (sbn.isGroupSummary()) {
            return; // дочерние уведомления содержат тот же текст подробнее
        }
        if (n.category != null && SKIP_CATEGORIES.contains(n.category)) {
            return;
        }
        if (!Db.get(this).appEnabled(pkg)) {
            return;
        }
        Bundle ex = n.extras;
        if (ex == null) {
            return;
        }
        String convo = text(ex.getCharSequence(Notification.EXTRA_CONVERSATION_TITLE));
        String title = str(ex, Notification.EXTRA_TITLE);
        if (convo.length() == 0) {
            convo = title;
        }
        if (convo.length() == 0) {
            convo = Ui.appLabel(this, pkg);
        }

        boolean any = false;
        // Полная история из свёрнутого уведомления (MessagingStyle).
        android.os.Parcelable[] arr;
        try {
            arr = ex.getParcelableArray(Notification.EXTRA_MESSAGES);
        } catch (RuntimeException e) {
            arr = null;
        }
        if (arr != null && arr.length > 0) {
            for (android.os.Parcelable p : arr) {
                if (!(p instanceof Bundle)) {
                    continue;
                }
                Notification.MessagingStyle.Message m =
                        Notification.MessagingStyle.Message
                                .getMessageFromBundle((Bundle) p);
                if (m == null || m.getText() == null
                        || m.getText().length() == 0) {
                    continue;
                }
                String sender = m.getSenderPerson() != null
                        && m.getSenderPerson().getName() != null
                        ? m.getSenderPerson().getName().toString()
                        : (title.length() > 0 ? title : convo);
                long ts = m.getTimestamp() > 0
                        ? m.getTimestamp() : sbn.getPostTime();
                String body = m.getText().toString();
                if (DELETED_MARKER.matcher(body).find()) {
                    Db.get(this).markLastDeleted(pkg, convo);
                } else {
                    Db.get(this).insert(pkg, convo, sender, body, ts);
                }
                any = true;
            }
        } else {
            CharSequence big = ex.getCharSequence(Notification.EXTRA_BIG_TEXT);
            CharSequence small = ex.getCharSequence(Notification.EXTRA_TEXT);
            CharSequence body = big != null
                    && (small == null || big.length() >= small.length())
                    ? big : small;
            if (body != null && body.length() > 0) {
                String text = body.toString();
                if (DELETED_MARKER.matcher(text).find()) {
                    Db.get(this).markLastDeleted(pkg, convo);
                } else {
                    String sender = title.length() > 0 ? title : convo;
                    Db.get(this).insert(pkg, convo, sender, text,
                            sbn.getPostTime());
                }
                any = true;
            }
        }
        if (any) {
            Db.get(this).purgeOld();
            Intent refresh = new Intent(ACTION_UPDATED).setPackage(getPackageName());
            sendBroadcast(refresh);
        }
    }

    private static String str(Bundle ex, String key) {
        CharSequence c = ex.getCharSequence(key);
        return c == null ? "" : c.toString().trim();
    }

    private static String text(CharSequence c) {
        return c == null ? "" : c.toString().trim();
    }
}
