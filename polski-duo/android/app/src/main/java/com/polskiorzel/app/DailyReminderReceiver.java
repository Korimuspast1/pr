package com.polskiorzel.app;

import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import androidx.core.app.NotificationCompat;

public class DailyReminderReceiver extends BroadcastReceiver {
    public static final String CHANNEL_ID = "polskiorzel_daily_channel";
    public static final int NOTIFICATION_ID = 7701;

    @Override
    public void onReceive(Context context, Intent intent) {
        NotificationManager nm =
            (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm == null) return;

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel channel = new NotificationChannel(
                CHANNEL_ID,
                "Ежедневная тренировка польского",
                NotificationManager.IMPORTANCE_DEFAULT
            );
            channel.setDescription("Напоминания от Орлика Белика о словах и серии занятий");
            nm.createNotificationChannel(channel);
        }

        Intent launchIntent = new Intent(context, MainActivity.class);
        launchIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        PendingIntent pendingIntent = PendingIntent.getActivity(
            context,
            0,
            launchIntent,
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
        );

        NotificationCompat.Builder builder = new NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.mipmap.ic_launcher)
            .setContentTitle("PolskiOrzeł ждёт тебя!")
            .setContentText("Повтори 5 польских слов за 2 минуты и сохрани ударный режим.")
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .setAutoCancel(true)
            .setContentIntent(pendingIntent);

        nm.notify(NOTIFICATION_ID, builder.build());
    }
}
