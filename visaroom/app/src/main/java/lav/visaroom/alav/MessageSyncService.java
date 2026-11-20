package lav.visaroom.alav;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import android.os.IBinder;

import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

/**
 * Постоянное фоновое соединение (как «Background connection» в Telegram без
 * Google Play): лёгкий foreground-сервис, который каждые ~20 секунд спрашивает
 * у облака новые сообщения и показывает уведомления, даже когда приложение
 * закрыто. Сервис STICKY — Android перезапускает его после выгрузки.
 * Постоянное служебное уведомление — требование платформы для таких сервисов;
 * оно тихое (LOW) и не звенит.
 */
public class MessageSyncService extends Service {

    private static final String CHANNEL = "visaroom_sync_v1";
    private static final int ID = 1107;
    private static final long POLL_SECONDS = 20L;
    private ScheduledExecutorService executor;

    static Intent intent(Context context) {
        return new Intent(context, MessageSyncService.class);
    }

    @Override
    public void onCreate() {
        super.onCreate();
        startInForeground();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        startInForeground();
        if (executor == null || executor.isShutdown()) {
            executor = Executors.newSingleThreadScheduledExecutor();
            executor.scheduleWithFixedDelay(() -> {
                try {
                    NotificationFx.checkNow(getApplicationContext());
                } catch (Throwable ignored) {
                }
            }, 2L, POLL_SECONDS, TimeUnit.SECONDS);
        }
        return START_STICKY;
    }

    private void startInForeground() {
        NotificationManager manager = (NotificationManager)
                getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager != null
                && Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                && manager.getNotificationChannel(CHANNEL) == null) {
            NotificationChannel channel = new NotificationChannel(CHANNEL,
                    getString(R.string.background_connection),
                    NotificationManager.IMPORTANCE_LOW);
            channel.setDescription(getString(R.string.background_connection_desc));
            channel.setShowBadge(false);
            manager.createNotificationChannel(channel);
        }
        Intent open = new Intent(this, ChatsActivity.class);
        open.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        PendingIntent tap = PendingIntent.getActivity(this, 0, open,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        Notification.Builder builder = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? new Notification.Builder(this, CHANNEL)
                : new Notification.Builder(this);
        Notification notification = builder
                .setSmallIcon(R.drawable.ic_nav_chats)
                .setContentTitle(getString(R.string.app_name_tg))
                .setContentText(getString(R.string.background_connection_desc))
                .setContentIntent(tap)
                .setOngoing(true)
                .setShowWhen(false)
                .build();
        try {
            if (Build.VERSION.SDK_INT >= 34) {
                startForeground(ID, notification,
                        android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC);
            } else {
                startForeground(ID, notification);
            }
        } catch (RuntimeException ignored) {
            // Нет права на FGS (например, вызов из фона на Android 12+):
            // периодический JobScheduler продолжит работать без сервиса.
        }
    }

    @Override
    public void onDestroy() {
        if (executor != null) {
            executor.shutdownNow();
            executor = null;
        }
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }
}
