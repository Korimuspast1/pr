package com.windukk.mod;

import android.app.Application;
import android.content.Context;
import android.util.Log;

import dev.ttydyn.profileflex.ProfileFlex;

/**
 * Точка входа динамического модуля TikTok You.
 *
 * Стабильная часть APK после проверки подписи загружает classes.dex и вызывает
 * ровно этот статический метод — сигнатура взята из официального модуля
 * (см. docs/ttydyn-format.md).
 */
public final class Main {

    private static final String TAG = "ProfileFlex";

    private Main() {
    }

    public static void start(Application application, Context context) {
        try {
            ProfileFlex.install(application, context);
        } catch (Throwable error) {
            // Падение модуля не должно ронять TikTok.
            Log.e(TAG, "start() failed", error);
        }
    }
}
