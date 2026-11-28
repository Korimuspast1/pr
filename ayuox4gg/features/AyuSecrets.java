/*
 * This is the source code of AyuGram for Android.
 *
 * We do not and cannot prevent the use of our code,
 * but be respectful and credit the original author.
 *
 * Copyright @Radolyn, 2023
 */

package com.radolyn.ayugram.security;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import android.util.Log;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/**
 * Small, dependency-free encrypted storage for long-lived secrets such as the
 * AyuSync token.
 *
 * <p>Values are encrypted with AES-256-GCM using a key that lives in the Android
 * Keystore and never leaves it. The ciphertext is stored in a separate
 * SharedPreferences file, so the plaintext token can no longer be pulled from a
 * backup, a bug report or a rooted device's prefs XML.
 *
 * <p>Design notes:
 * <ul>
 *   <li>No new libraries: everything here is platform API 23+.</li>
 *   <li>Random IV per value, stored next to the ciphertext (GCM standard).</li>
 *   <li>On any failure the class falls back to "not available" rather than
 *       throwing: a broken Keystore must never brick the sync screen.</li>
 *   <li>The legacy plaintext value is migrated once and then removed.</li>
 * </ul>
 *
 * <p><b>Not yet wired in.</b> This file is a building block: `AyuConfig` still
 * reads the token from plain preferences. Wiring it means replacing
 * {@code getSyncServerToken()} with {@link #getString} and the prefs write with
 * {@link #putString} — see {@code security/FINDINGS.md} (M1).
 */
public final class AyuSecrets {

    private static final String TAG = "AyuSecrets";
    private static final String KEYSTORE = "AndroidKeyStore";
    private static final String KEY_ALIAS = "ayuox4gg.secrets.v1";
    private static final String PREFS_FILE = "ayu_secrets";
    private static final String TRANSFORMATION = "AES/GCM/NoPadding";
    private static final int GCM_TAG_BITS = 128;
    private static final int IV_BYTES = 12;
    private static final char SEPARATOR = ':';

    private AyuSecrets() {
    }

    /** True when encrypted storage can be used on this device. */
    public static boolean isAvailable() {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.M;
    }

    public static String getString(Context context, String key, String fallback) {
        if (context == null || key == null || !isAvailable()) {
            return fallback;
        }
        String stored = prefs(context).getString(key, null);
        if (stored == null) {
            return fallback;
        }
        try {
            return decrypt(stored);
        } catch (Exception e) {
            Log.w(TAG, "Could not decrypt " + key + ", dropping the stored value", e);
            prefs(context).edit().remove(key).apply();
            return fallback;
        }
    }

    public static void putString(Context context, String key, String value) {
        if (context == null || key == null || !isAvailable()) {
            return;
        }
        if (value == null) {
            prefs(context).edit().remove(key).apply();
            return;
        }
        try {
            prefs(context).edit().putString(key, encrypt(value)).apply();
        } catch (Exception e) {
            // Never store a secret in plaintext as a fallback.
            Log.w(TAG, "Could not encrypt " + key + ", dropping it", e);
            prefs(context).edit().remove(key).apply();
        }
    }

    public static boolean contains(Context context, String key) {
        return context != null && isAvailable() && prefs(context).contains(key);
    }

    /** One-shot migration from the legacy plaintext preferences. */
    public static void migrateLegacy(SharedPreferences legacy, Context context, String... keys) {
        if (legacy == null || context == null || !isAvailable()) {
            return;
        }
        SharedPreferences.Editor legacyEditor = null;
        for (String key : keys) {
            String plain = legacy.getString(key, null);
            if (plain == null || plain.isEmpty()) {
                continue;
            }
            if (!contains(context, key)) {
                putString(context, key, plain);
            }
            if (legacyEditor == null) {
                legacyEditor = legacy.edit();
            }
            legacyEditor.remove(key);
        }
        if (legacyEditor != null) {
            legacyEditor.apply();
        }
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------
    private static SharedPreferences prefs(Context context) {
        return context.getApplicationContext()
                .getSharedPreferences(PREFS_FILE, Context.MODE_PRIVATE);
    }

    private static SecretKey key() throws Exception {
        KeyStore keyStore = KeyStore.getInstance(KEYSTORE);
        keyStore.load(null);
        if (keyStore.containsAlias(KEY_ALIAS)) {
            return (SecretKey) keyStore.getKey(KEY_ALIAS, null);
        }
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE);
        generator.init(new KeyGenParameterSpec.Builder(
                KEY_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                // Deliberately not tied to user authentication: the sync client
                // must be able to reconnect while the screen is locked.
                .setUserAuthenticationRequired(false)
                .build());
        return generator.generateKey();
    }

    static String encrypt(String plaintext) throws Exception {
        Cipher cipher = Cipher.getInstance(TRANSFORMATION);
        cipher.init(Cipher.ENCRYPT_MODE, key());
        byte[] iv = cipher.getIV();
        if (iv == null || iv.length != IV_BYTES) {
            iv = new byte[IV_BYTES];
            new java.security.SecureRandom().nextBytes(iv);
            cipher.init(Cipher.ENCRYPT_MODE, key(), new GCMParameterSpec(GCM_TAG_BITS, iv));
        }
        byte[] ciphertext = cipher.doFinal(plaintext.getBytes(StandardCharsets.UTF_8));
        return Base64.encodeToString(iv, Base64.NO_WRAP) + SEPARATOR
                + Base64.encodeToString(ciphertext, Base64.NO_WRAP);
    }

    static String decrypt(String stored) throws Exception {
        int split = stored.indexOf(SEPARATOR);
        if (split <= 0) {
            throw new IllegalArgumentException("malformed value");
        }
        byte[] iv = Base64.decode(stored.substring(0, split), Base64.NO_WRAP);
        byte[] ciphertext = Base64.decode(stored.substring(split + 1), Base64.NO_WRAP);
        Cipher cipher = Cipher.getInstance(TRANSFORMATION);
        cipher.init(Cipher.DECRYPT_MODE, key(), new GCMParameterSpec(GCM_TAG_BITS, iv));
        return new String(cipher.doFinal(ciphertext), StandardCharsets.UTF_8);
    }
}
