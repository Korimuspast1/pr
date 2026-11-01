package lav.visaroom.alav;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/**
 * Stores the session (email + Firebase refresh token) encrypted with an
 * AES-256-GCM key held in the Android Keystore. The key never leaves the
 * device, so values copied out of the app folder are unreadable.
 */
final class TokenStore {

    private static final String PREFS = "s";
    private static final String KEY_EMAIL = "a";
    private static final String KEY_TOKEN = "b";
    private static final String ALIAS = "vr_session";
    private static final String TRANSFORM = "AES/GCM/NoPadding";

    private TokenStore() {
    }

    private static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    static void save(Context context, String email, String refreshToken) {
        try {
            SharedPreferences.Editor e = prefs(context).edit();
            if (email != null) {
                e.putString(KEY_EMAIL, encrypt(email));
            }
            if (refreshToken != null) {
                e.putString(KEY_TOKEN, encrypt(refreshToken));
            }
            e.apply();
        } catch (Exception ignored) {
            // If the keystore is unavailable the session simply is not kept.
        }
    }

    static String email(Context context) {
        return read(context, KEY_EMAIL);
    }

    static String refreshToken(Context context) {
        return read(context, KEY_TOKEN);
    }

    static void clear(Context context) {
        prefs(context).edit().clear().apply();
    }

    private static String read(Context context, String key) {
        try {
            String value = prefs(context).getString(key, null);
            if (value == null || value.length() == 0) {
                return null;
            }
            return decrypt(value);
        } catch (Exception ignored) {
            return null;
        }
    }

    private static SecretKey key() throws Exception {
        KeyStore keyStore = KeyStore.getInstance("AndroidKeyStore");
        keyStore.load(null);
        KeyStore.Entry entry = keyStore.getEntry(ALIAS, null);
        if (entry instanceof KeyStore.SecretKeyEntry) {
            return ((KeyStore.SecretKeyEntry) entry).getSecretKey();
        }
        KeyGenerator generator = KeyGenerator.getInstance(
                KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        generator.init(new KeyGenParameterSpec.Builder(ALIAS,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .build());
        return generator.generateKey();
    }

    private static String encrypt(String plain) throws Exception {
        Cipher cipher = Cipher.getInstance(TRANSFORM);
        cipher.init(Cipher.ENCRYPT_MODE, key());
        byte[] iv = cipher.getIV();
        byte[] data = cipher.doFinal(plain.getBytes(StandardCharsets.UTF_8));
        return Base64.encodeToString(iv, Base64.NO_WRAP)
                + "."
                + Base64.encodeToString(data, Base64.NO_WRAP);
    }

    private static String decrypt(String stored) throws Exception {
        int split = stored.indexOf('.');
        byte[] iv = Base64.decode(stored.substring(0, split), Base64.NO_WRAP);
        byte[] data = Base64.decode(stored.substring(split + 1), Base64.NO_WRAP);
        Cipher cipher = Cipher.getInstance(TRANSFORM);
        cipher.init(Cipher.DECRYPT_MODE, key(), new GCMParameterSpec(128, iv));
        return new String(cipher.doFinal(data), StandardCharsets.UTF_8);
    }
}
