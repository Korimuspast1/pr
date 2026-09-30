package com.windukk.mod;

import android.app.Application;
import android.content.Context;
import android.util.Log;

import com.windukk.stable.ResourceOverlay;

import dalvik.system.InMemoryDexClassLoader;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.lang.reflect.Method;
import java.nio.ByteBuffer;

import dev.ttydyn.profileflex.ProfileFlex;

/**
 * Точка входа динамического модуля TikTok You.
 *
 * Стабильная часть APK после проверки подписи загружает classes.dex и вызывает
 * ровно этот статический метод — сигнатура взята из официального модуля
 * (см. docs/ttydyn-format.md).
 *
 * Так как payloadType = full, наш модуль заменяет собой весь динамический код мода.
 * Чтобы не потерять функции TikTok You (регион, скачивание, фильтры), сначала
 * пытаемся подгрузить оригинальный dex и передать управление ему, а уже потом
 * ставим свои хуки.
 */
public final class Main {

    private static final String TAG = "ProfileFlex";
    /** Куда положить classes.dex из оригинального .ttydyn (необязательно). */
    private static final String BASE_DEX_FILE = "profile_flex_base.dex";
    private static boolean started;

    private Main() {
    }

    /**
     * Загрузчик может вызвать точку входа повторно после восстановления процесса.
     * Повторная установка хуков приводит к двойной подмене и лишним исключениям,
     * поэтому второй запуск безопасно игнорируем.
     */
    public static synchronized void start(Application application, Context context) {
        if (started) {
            Log.i(TAG, "start() already completed");
            return;
        }
        started = true;
        try {
            chainLoadOriginalMod(application, context);
            ProfileFlex.install(application, context);
        } catch (Throwable error) {
            // Падение модуля не должно ронять TikTok.
            Log.e(TAG, "start() failed; continuing without Profile Flex hooks", error);
        }
    }

    // ------------------------------------------------------------------
    // Цепочная загрузка оригинального модуля мода
    // ------------------------------------------------------------------
    private static void chainLoadOriginalMod(Application application, Context context) {
        byte[] dex = readBaseDex(context);
        if (dex == null || dex.length < 64) {
            Log.i(TAG, "base dex not provided — работают только хуки Profile Flex");
            return;
        }
        try {
            ClassLoader loader = new InMemoryDexClassLoader(ByteBuffer.wrap(dex), context.getClassLoader());
            Class<?> original = loader.loadClass("com.windukk.mod.Main");
            if (original == Main.class) {
                Log.w(TAG, "base dex == наш модуль, пропускаю (иначе рекурсия)");
                return;
            }
            Method start = original.getMethod("start", Application.class, Context.class);
            start.invoke(null, application, context);
            Log.i(TAG, "original mod started from base dex (" + dex.length + " bytes)");
        } catch (Throwable error) {
            Log.w(TAG, "chain load failed — продолжаю без функций мода", error);
        }
    }

    private static byte[] readBaseDex(Context context) {
        // 1) ресурс внутри самого .ttydyn (если сборка положила туда base.dex)
        try {
            InputStream stream = ResourceOverlay.open(context, "base.dex");
            if (stream != null) {
                return readAll(stream);
            }
        } catch (Throwable ignored) {
            // стабильная часть может не поддерживать произвольные ресурсы — это нормально
        }
        // 2) файл, положенный пользователем рядом с конфигом
        try {
            File directory = context.getExternalFilesDir(null);
            if (directory == null) {
                directory = context.getFilesDir();
            }
            File file = new File(directory, BASE_DEX_FILE);
            if (file.isFile()) {
                return readAll(new FileInputStream(file));
            }
        } catch (Throwable error) {
            Log.w(TAG, "cannot read " + BASE_DEX_FILE, error);
        }
        return null;
    }

    private static byte[] readAll(InputStream stream) throws Exception {
        try {
            ByteArrayOutputStream buffer = new ByteArrayOutputStream();
            byte[] chunk = new byte[16384];
            int read;
            while ((read = stream.read(chunk)) > 0) {
                buffer.write(chunk, 0, read);
            }
            return buffer.toByteArray();
        } finally {
            stream.close();
        }
    }
}
