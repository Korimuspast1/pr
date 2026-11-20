package lav.visaroom.alav;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/**
 * После перезагрузки устройства заново поднимает фоновые проверки
 * уведомлений: периодический JobScheduler (persisted-задача обычно сама
 * переживает ребут, но перестраховываемся) и, если платформа позволяет,
 * постоянное фоновое соединение.
 */
public class BootReceiver extends BroadcastReceiver {

    @Override
    public void onReceive(Context context, Intent intent) {
        if (context == null || intent == null
                || !Intent.ACTION_BOOT_COMPLETED.equals(intent.getAction())) {
            return;
        }
        try {
            NotificationFx.scheduleBackgroundChecks(context);
            NotificationFx.startSyncService(context);
        } catch (RuntimeException ignored) {
            // Автозапуск вспомогателен; падать при загрузке ОС нельзя.
        }
    }
}
