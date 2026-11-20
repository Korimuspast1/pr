package lav.visaroom.alav;

import android.Manifest;
import android.app.Activity;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.job.JobInfo;
import android.app.job.JobScheduler;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.media.RingtoneManager;
import android.net.Uri;
import android.os.Build;

import java.util.List;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Local message notifications for the Firebase-only client.
 *
 * The app checks only the signed-in account's bounded cloud chat
 * indexes; no message history is copied into local storage. The only
 * persisted notification state is a per-chat last-notified message ID
 * and timestamp, which is necessary to avoid repeating the same alert
 * after every poll. A periodic framework job repeats the lightweight
 * check when Android permits background work.
 */
final class NotificationFx {

    static final int REQUEST_POST_NOTIFICATIONS = 902;

    private static final int JOB_ID = 1101;
    private static final String CHANNEL_SOUND = "visaroom_messages_sound_v1";
    private static final String CHANNEL_SILENT = "visaroom_messages_silent_v1";
    private static final String PREFS = "notification_state";
    private static final AtomicBoolean CHECKING = new AtomicBoolean(false);

    private static Context appContext;
    private static ScheduledExecutorService foregroundExecutor;
    private static volatile String activeDirectPeerUid = "";
    private static volatile boolean chatsVisible;

    private NotificationFx() {
    }

    static void setActiveDirectPeer(String peerUid) {
        activeDirectPeerUid = peerUid == null ? "" : peerUid;
    }

    static void setChatsVisible(boolean visible) {
        chatsVisible = visible;
    }

    static boolean hasPermission(Context context) {
        if (context == null) {
            return false;
        }
        NotificationManager manager = (NotificationManager)
                context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null || !manager.areNotificationsEnabled()) {
            return false;
        }
        return Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU
                || context.checkSelfPermission(
                        Manifest.permission.POST_NOTIFICATIONS)
                == PackageManager.PERMISSION_GRANTED;
    }

    static void requestPermission(Activity activity) {
        if (activity == null
                || Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) {
            return;
        }
        try {
            if (!AppSettings.notifications(activity)
                    || activity.checkSelfPermission(
                            Manifest.permission.POST_NOTIFICATIONS)
                    == PackageManager.PERMISSION_GRANTED) {
                return;
            }
            activity.requestPermissions(
                    new String[]{Manifest.permission.POST_NOTIFICATIONS},
                    REQUEST_POST_NOTIFICATIONS);
        } catch (RuntimeException ignored) {
            // A notification-permission prompt must never prevent the
            // chat list (or any other screen) from opening.
        }
    }

    static synchronized void startForegroundChecks(Context context) {
        if (context == null) {
            return;
        }
        appContext = context.getApplicationContext();
        // Notifications are auxiliary to opening ChatsActivity. A vendor
        // or platform rejection anywhere in their setup must not abort
        // Activity.onCreate/onResume.
        try {
            ensureChannels(appContext);
        } catch (RuntimeException ignored) {
            // Foreground polling can still run without a channel UI.
        }
        scheduleBackgroundChecks(appContext);
        if (foregroundExecutor != null && !foregroundExecutor.isShutdown()) {
            return;
        }
        try {
            foregroundExecutor = Executors.newSingleThreadScheduledExecutor();
            foregroundExecutor.scheduleWithFixedDelay(
                    () -> checkNow(appContext), 1L, 4L, TimeUnit.SECONDS);
        } catch (RuntimeException ignored) {
            if (foregroundExecutor != null) {
                foregroundExecutor.shutdownNow();
                foregroundExecutor = null;
            }
        }
    }

    static void scheduleBackgroundChecks(Context context) {
        if (context == null) {
            return;
        }
        try {
            Context app = context.getApplicationContext();
            JobScheduler scheduler = (JobScheduler)
                    app.getSystemService(Context.JOB_SCHEDULER_SERVICE);
            if (scheduler == null) {
                return;
            }
            String refreshToken = TokenStore.refreshToken(app);
            if (!AppSettings.notifications(app)
                    || refreshToken == null || refreshToken.length() == 0) {
                scheduler.cancel(JOB_ID);
                return;
            }
            if (scheduler.getPendingJob(JOB_ID) != null) {
                return;
            }
            JobInfo.Builder builder = new JobInfo.Builder(JOB_ID,
                    new ComponentName(app, MessageCheckJobService.class))
                    .setPeriodic(TimeUnit.MINUTES.toMillis(15));
            // Android requires ACCESS_NETWORK_STATE for a connectivity
            // constraint. It is declared in the manifest; keep this
            // runtime guard so a platform/manifest mismatch degrades to
            // an unconstrained periodic check instead of crashing.
            if (app.checkSelfPermission(
                    Manifest.permission.ACCESS_NETWORK_STATE)
                    == PackageManager.PERMISSION_GRANTED) {
                builder.setRequiredNetworkType(JobInfo.NETWORK_TYPE_ANY);
            }
            // Persisting a job across reboot requires the boot permission.
            if (app.checkSelfPermission(
                    Manifest.permission.RECEIVE_BOOT_COMPLETED)
                    == PackageManager.PERMISSION_GRANTED) {
                builder.setPersisted(true);
            }
            scheduler.schedule(builder.build());
        } catch (RuntimeException ignored) {
            // Background notification scheduling is optional. In
            // particular, JobScheduler rejects malformed/unsupported
            // requests with SecurityException or IllegalArgumentException;
            // neither may take down the signed-in UI.
        }
    }

    static void cancelBackgroundChecks(Context context) {
        if (context == null) {
            return;
        }
        try {
            JobScheduler scheduler = (JobScheduler) context
                    .getApplicationContext()
                    .getSystemService(Context.JOB_SCHEDULER_SERVICE);
            if (scheduler != null) {
                scheduler.cancel(JOB_ID);
            }
        } catch (RuntimeException ignored) {
            // See scheduleBackgroundChecks: this work is auxiliary.
        }
    }

    static void onSignedOut(Context context) {
        SessionTokens.clear();
        setActiveDirectPeer("");
        setChatsVisible(false);
        cancelAll(context);
        if (context != null && TokenStore.accountCount(context) == 0) {
            cancelBackgroundChecks(context);
        }
    }

    /** Network I/O: call only from a background thread. */
    static void checkNow(Context context) {
        if (context == null || !CHECKING.compareAndSet(false, true)) {
            return;
        }
        try {
            Context app = context.getApplicationContext();
            if (!AppSettings.notifications(app)) {
                return;
            }
            AuthClient.init(app);
            String uid = TokenStore.uid(app);
            if (!AuthClient.validUid(uid)) {
                return;
            }
            String token = SessionTokens.token(app, uid);
            if (token == null) {
                return;
            }
            String activeUid = TokenStore.uid(app);
            if (!uid.equals(activeUid)) {
                return;
            }
            List<VrProto.ChatSummary> summaries =
                    VrProto.listChatPreviews(token, uid);
            if (summaries != null) {
                onSummaries(app, uid, summaries);
            }
        } finally {
            CHECKING.set(false);
        }
    }

    static synchronized void onSummaries(Context context, String myUid,
                                         List<VrProto.ChatSummary> summaries) {
        if (context == null || !AuthClient.validUid(myUid)
                || summaries == null) {
            return;
        }
        Context app = context.getApplicationContext();
        SharedPreferences prefs = app.getSharedPreferences(
                PREFS, Context.MODE_PRIVATE);
        SharedPreferences.Editor editor = prefs.edit();
        boolean initialized = prefs.getBoolean("init_" + myUid, false);
        boolean canNotify = AppSettings.notifications(app)
                && hasPermission(app);

        for (VrProto.ChatSummary summary : summaries) {
            if (summary == null || !AuthClient.validUid(summary.peerUid)
                    || myUid.equals(summary.peerUid)
                    || summary.lastMessageId == null
                    || summary.lastMessageId.length() == 0
                    || summary.lastMessageAt <= 0) {
                continue;
            }
            String displayName = displayName(summary);
            if (displayName.length() > 0) {
                editor.putString(nameKey(myUid, summary.peerUid), displayName);
            }
            String idKey = "id_" + myUid + "_" + summary.chatId;
            String atKey = "at_" + myUid + "_" + summary.chatId;
            String seenId = prefs.getString(idKey, "");
            long seenAt = prefs.getLong(atKey, 0L);
            if (!initialized) {
                editor.putString(idKey, summary.lastMessageId);
                editor.putLong(atKey, summary.lastMessageAt);
                continue;
            }
            boolean incoming = summary.peerUid.equals(summary.lastSenderUid);
            boolean unread = incoming
                    && summary.lastMessageAt > summary.lastReadAt;
            boolean fresh = unread
                    && summary.lastMessageAt > seenAt
                    && !summary.lastMessageId.equals(seenId);
            if (fresh) {
                boolean viewingThisChat = summary.peerUid.equals(
                        activeDirectPeerUid);
                if (canNotify && !viewingThisChat && !chatsVisible) {
                    showNotification(app, prefs, myUid, summary);
                }
                editor.putString(idKey, summary.lastMessageId);
                editor.putLong(atKey, summary.lastMessageAt);
            } else if (!unread && summary.lastMessageAt >= seenAt) {
                editor.putString(idKey, summary.lastMessageId);
                editor.putLong(atKey, summary.lastMessageAt);
            }
        }
        if (!initialized) {
            editor.putBoolean("init_" + myUid, true);
        }
        editor.apply();
    }

    static void cancelForPeer(Context context, String myUid, String peerUid) {
        if (context == null || !AuthClient.validUid(myUid)
                || !AuthClient.validUid(peerUid)
                || myUid.equals(peerUid)) {
            return;
        }
        String chatId = VrProto.directChatId(myUid, peerUid);
        NotificationManager manager = (NotificationManager) context
                .getApplicationContext()
                .getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager != null && chatId.length() > 0) {
            manager.cancel(notificationId(myUid, chatId));
        }
    }

    static void cancelAll(Context context) {
        if (context == null) {
            return;
        }
        NotificationManager manager = (NotificationManager) context
                .getApplicationContext()
                .getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager != null) {
            manager.cancelAll();
        }
    }

    private static void showNotification(Context context,
                                         SharedPreferences prefs,
                                         String myUid,
                                         VrProto.ChatSummary summary) {
        ensureChannels(context);
        NotificationManager manager = (NotificationManager)
                context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null) {
            return;
        }
        String title = displayName(summary);
        if (title.length() == 0) {
            title = prefs.getString(nameKey(myUid, summary.peerUid), "");
        }
        if (title == null || title.length() == 0) {
            title = "visaroom";
        }
        String body = previewText(summary.lastText);
        Intent intent = new Intent(context, ChatsActivity.class);
        intent.setAction("lav.visaroom.alav.OPEN_CHAT." + summary.chatId);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK
                | Intent.FLAG_ACTIVITY_CLEAR_TOP
                | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        intent.putExtra(ChatsActivity.EXTRA_OPEN_PEER_UID, summary.peerUid);
        intent.putExtra(ChatsActivity.EXTRA_OPEN_PEER_USERNAME,
                summary.username == null ? "" : summary.username);
        int id = notificationId(myUid, summary.chatId);
        PendingIntent pendingIntent = PendingIntent.getActivity(
                context, id, intent,
                PendingIntent.FLAG_UPDATE_CURRENT
                        | PendingIntent.FLAG_IMMUTABLE);

        Notification publicNotification = new Notification.Builder(
                context, channelId(context))
                .setSmallIcon(R.drawable.ic_nav_chats)
                .setContentTitle("visaroom")
                .setContentText("New message")
                .build();
        Notification notification = new Notification.Builder(
                context, channelId(context))
                .setSmallIcon(R.drawable.ic_nav_chats)
                .setContentTitle(title)
                .setContentText(body)
                .setStyle(new Notification.BigTextStyle().bigText(body))
                .setContentIntent(pendingIntent)
                .setAutoCancel(true)
                .setCategory(Notification.CATEGORY_MESSAGE)
                .setVisibility(Notification.VISIBILITY_PRIVATE)
                .setPublicVersion(publicNotification)
                .setWhen(summary.lastMessageAt)
                .setShowWhen(true)
                .setGroup("visaroom_messages")
                .build();
        manager.notify(id, notification);
    }

    private static void ensureChannels(Context context) {
        NotificationManager manager = (NotificationManager)
                context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null) {
            return;
        }
        if (manager.getNotificationChannel(CHANNEL_SOUND) == null) {
            NotificationChannel channel = new NotificationChannel(
                    CHANNEL_SOUND, "Messages",
                    NotificationManager.IMPORTANCE_HIGH);
            channel.setDescription("New visaroom messages");
            Uri sound = RingtoneManager.getDefaultUri(
                    RingtoneManager.TYPE_NOTIFICATION);
            channel.setSound(sound, Notification.AUDIO_ATTRIBUTES_DEFAULT);
            channel.enableVibration(true);
            manager.createNotificationChannel(channel);
        }
        if (manager.getNotificationChannel(CHANNEL_SILENT) == null) {
            NotificationChannel channel = new NotificationChannel(
                    CHANNEL_SILENT, "Messages",
                    NotificationManager.IMPORTANCE_HIGH);
            channel.setDescription("New visaroom messages");
            channel.setSound(null, null);
            channel.enableVibration(true);
            manager.createNotificationChannel(channel);
        }
    }

    private static String channelId(Context context) {
        return AppSettings.sounds(context) ? CHANNEL_SOUND : CHANNEL_SILENT;
    }

    private static String displayName(VrProto.ChatSummary summary) {
        if (summary.username != null && summary.username.length() > 0) {
            return "@" + summary.username;
        }
        return summary.publicId > 0 ? "ID " + summary.publicId : "";
    }

    private static String previewText(String raw) {
        String text = raw == null ? "" : raw.replace('\n', ' ').trim();
        if (VrProto.VOICE_TEXT.equals(text)) {
            text = "Voice message";
        } else if ("Wallpaper suggestion".equals(text)) {
            text = "Wallpaper suggestion";
        } else if (text.length() == 0) {
            text = "New message";
        }
        return text.length() <= 180 ? text : text.substring(0, 177) + "...";
    }

    private static String nameKey(String myUid, String peerUid) {
        return "name_" + myUid + "_" + peerUid;
    }

    private static int notificationId(String myUid, String chatId) {
        return 10000 + (int) ((myUid + "\n" + chatId).hashCode() & 0x3fffff);
    }
}
