package lav.visaroom.alav;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/**
 * Stores up to three account sessions (email + Firebase refresh token)
 * encrypted with an AES-256-GCM key held in the Android Keystore. The
 * active account is also mirrored into the original single-account keys,
 * so older call sites keep working while account switching is handled here.
 */
final class TokenStore {

    static final int MAX_ACCOUNTS = 3;

    private static final String PREFS = "s";
    private static final String KEY_EMAIL = "a";
    private static final String KEY_TOKEN = "b";
    private static final String KEY_UID = "c";
    private static final String KEY_2FA_MARKER = "d";
    private static final String KEY_2FA_VERIFIER = "e";
    private static final String KEY_ACTIVE = "acct_active";
    private static final String ACCOUNT_PREFIX = "acct_";
    private static final String ALIAS = "vr_session";
    private static final String TRANSFORM = "AES/GCM/NoPadding";

    static final class Account {
        int slot;
        String email = "";
        String refreshToken = "";
        String uid = "";
        String twoFaMarker = "";
        String twoFaVerifier = "";
    }

    private TokenStore() {
    }

    private static SharedPreferences prefs(Context context) {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    static String profilePrefsName(String uid) {
        if (uid == null || uid.length() == 0) {
            return "profile_cache";
        }
        return "profile_cache_" + uid.replaceAll("[^A-Za-z0-9]", "_");
    }

    /**
     * Per-account local profile cache. The old single-account cache is
     * copied once for the first account that opens after the upgrade.
     */
    static SharedPreferences profilePrefs(Context context, String uid) {
        SharedPreferences legacy = context.getSharedPreferences(
                "profile_cache", Context.MODE_PRIVATE);
        if (uid == null || uid.length() == 0) {
            return legacy;
        }
        SharedPreferences perAccount = context.getSharedPreferences(
                profilePrefsName(uid), Context.MODE_PRIVATE);
        if (perAccount.getAll().isEmpty()) {
            String migratedUid = legacy.getString("__migrated_uid", "");
            if (!legacy.getAll().isEmpty()
                    && (migratedUid == null || migratedUid.length() == 0
                    || uid.equals(migratedUid))) {
                SharedPreferences.Editor editor = perAccount.edit();
                for (Map.Entry<String, ?> entry : legacy.getAll().entrySet()) {
                    String key = entry.getKey();
                    Object value = entry.getValue();
                    if ("__migrated_uid".equals(key)) {
                        continue;
                    }
                    if (value instanceof String) {
                        editor.putString(key, (String) value);
                    } else if (value instanceof Boolean) {
                        editor.putBoolean(key, (Boolean) value);
                    } else if (value instanceof Long) {
                        editor.putLong(key, (Long) value);
                    } else if (value instanceof Integer) {
                        editor.putInt(key, (Integer) value);
                    } else if (value instanceof Float) {
                        editor.putFloat(key, (Float) value);
                    }
                }
                editor.commit();
                legacy.edit().putString("__migrated_uid", uid).commit();
            }
        }
        return perAccount;
    }

    // ---------- legacy active-account API ----------

    static void save(Context context, String email, String refreshToken) {
        try {
            SharedPreferences.Editor e = prefs(context).edit();
            if (email != null) {
                e.putString(KEY_EMAIL, encrypt(email));
            }
            if (refreshToken != null) {
                e.putString(KEY_TOKEN, encrypt(refreshToken));
            }
            e.commit();
            syncActiveFromLegacy(context);
        } catch (Exception ignored) {
            // If the keystore is unavailable the session simply is not kept.
        }
    }

    static void saveUid(Context context, String uid) {
        if (uid == null || uid.length() == 0) {
            return;
        }
        try {
            prefs(context).edit().putString(KEY_UID, encrypt(uid)).commit();
            syncActiveFromLegacy(context);
        } catch (Exception ignored) {
        }
    }

    static String uid(Context context) {
        return read(context, KEY_UID);
    }

    static void saveTwoFa(Context context, String marker, String verifier) {
        try {
            SharedPreferences.Editor e = prefs(context).edit();
            if (marker == null || marker.length() == 0) {
                e.remove(KEY_2FA_MARKER);
            } else {
                e.putString(KEY_2FA_MARKER, encrypt(marker));
            }
            if (verifier == null || verifier.length() == 0) {
                e.remove(KEY_2FA_VERIFIER);
            } else {
                e.putString(KEY_2FA_VERIFIER, encrypt(verifier));
            }
            e.commit();
            syncActiveFromLegacy(context);
        } catch (Exception ignored) {
        }
    }

    static String twoFaMarker(Context context) {
        return read(context, KEY_2FA_MARKER);
    }

    static String twoFaVerifier(Context context) {
        return read(context, KEY_2FA_VERIFIER);
    }

    static String twoFaVerifierForUid(Context context, String uid) {
        ensureMigrated(context);
        if (uid != null && uid.length() > 0) {
            for (int slot = 0; slot < MAX_ACCOUNTS; slot++) {
                Account account = readAccount(context, slot);
                if (uid.equals(account.uid)) {
                    return account.twoFaVerifier.length() == 0
                            ? null : account.twoFaVerifier;
                }
            }
        }
        String activeUid = uid(context);
        return uid != null && uid.equals(activeUid) ? twoFaVerifier(context) : null;
    }

    static String email(Context context) {
        return read(context, KEY_EMAIL);
    }

    static String refreshToken(Context context) {
        return read(context, KEY_TOKEN);
    }

    /** Removes the active saved account and activates another one, if any. */
    static void clear(Context context) {
        ensureMigrated(context);
        int slot = activeSlot(context);
        if (slot >= 0) {
            removeAccount(context, slot);
        } else {
            clearLegacy(context);
        }
    }

    static void clearAll(Context context) {
        prefs(context).edit().clear().commit();
    }

    // ---------- multi-account API ----------

    static synchronized List<Account> accounts(Context context) {
        ensureMigrated(context);
        List<Account> result = new ArrayList<>();
        for (int slot = 0; slot < MAX_ACCOUNTS; slot++) {
            Account account = readAccount(context, slot);
            if (account.refreshToken.length() > 0) {
                result.add(account);
            }
        }
        return result;
    }

    static synchronized int accountCount(Context context) {
        return accounts(context).size();
    }

    static synchronized int activeSlot(Context context) {
        ensureMigrated(context);
        int slot = prefs(context).getInt(KEY_ACTIVE, -1);
        if (slot >= 0 && slot < MAX_ACCOUNTS
                && readAccount(context, slot).refreshToken.length() > 0) {
            return slot;
        }
        for (int i = 0; i < MAX_ACCOUNTS; i++) {
            Account account = readAccount(context, i);
            if (account.refreshToken.length() > 0) {
                prefs(context).edit().putInt(KEY_ACTIVE, i).commit();
                mirror(context, account);
                return i;
            }
        }
        return -1;
    }

    /**
     * Saves a signed-in account and makes it active. Returns false when
     * three different accounts are already saved on this device.
     */
    static synchronized boolean activateAccount(Context context, String email,
                                                String refreshToken, String uid) {
        if (refreshToken == null || refreshToken.length() == 0) {
            return false;
        }
        ensureMigrated(context);
        Account target = null;
        int freeSlot = -1;
        for (int slot = 0; slot < MAX_ACCOUNTS; slot++) {
            Account account = readAccount(context, slot);
            if (account.refreshToken.length() == 0) {
                if (freeSlot < 0) {
                    freeSlot = slot;
                }
                continue;
            }
            boolean sameUid = uid != null && uid.length() > 0 && uid.equals(account.uid);
            boolean sameEmail = email != null && email.length() > 0
                    && email.equalsIgnoreCase(account.email);
            if (sameUid || sameEmail) {
                target = account;
                break;
            }
        }
        if (target == null) {
            if (freeSlot < 0) {
                return false;
            }
            target = new Account();
            target.slot = freeSlot;
        }
        if (email != null && email.length() > 0) {
            target.email = email;
        }
        target.refreshToken = refreshToken;
        if (uid != null && uid.length() > 0) {
            target.uid = uid;
        }
        writeAccount(context, target);
        prefs(context).edit().putInt(KEY_ACTIVE, target.slot).commit();
        mirror(context, target);
        return true;
    }

    static synchronized boolean switchAccount(Context context, int slot) {
        ensureMigrated(context);
        if (slot < 0 || slot >= MAX_ACCOUNTS) {
            return false;
        }
        Account account = readAccount(context, slot);
        if (account.refreshToken.length() == 0) {
            return false;
        }
        prefs(context).edit().putInt(KEY_ACTIVE, slot).commit();
        mirror(context, account);
        return true;
    }

    static synchronized void removeAccount(Context context, int slot) {
        ensureMigrated(context);
        if (slot < 0 || slot >= MAX_ACCOUNTS) {
            return;
        }
        boolean wasActive = prefs(context).getInt(KEY_ACTIVE, -1) == slot;
        SharedPreferences.Editor editor = prefs(context).edit();
        for (String suffix : new String[]{"e", "t", "u", "m", "v"}) {
            editor.remove(accountKey(slot, suffix));
        }
        editor.commit();

        if (!wasActive) {
            return;
        }
        for (int i = 0; i < MAX_ACCOUNTS; i++) {
            Account next = readAccount(context, i);
            if (next.refreshToken.length() > 0) {
                prefs(context).edit().putInt(KEY_ACTIVE, i).commit();
                mirror(context, next);
                return;
            }
        }
        prefs(context).edit().putInt(KEY_ACTIVE, -1).commit();
        clearLegacy(context);
    }

    // ---------- storage helpers ----------

    private static synchronized void ensureMigrated(Context context) {
        for (int slot = 0; slot < MAX_ACCOUNTS; slot++) {
            if (readAccount(context, slot).refreshToken.length() > 0) {
                return;
            }
        }
        Account legacy = legacyAccount(context);
        if (legacy.refreshToken.length() == 0) {
            return;
        }
        legacy.slot = 0;
        writeAccount(context, legacy);
        if (!prefs(context).contains(KEY_ACTIVE)) {
            prefs(context).edit().putInt(KEY_ACTIVE, 0).commit();
        }
    }

    private static synchronized void syncActiveFromLegacy(Context context) {
        ensureMigrated(context);
        Account legacy = legacyAccount(context);
        if (legacy.refreshToken.length() == 0) {
            return;
        }
        int slot = prefs(context).getInt(KEY_ACTIVE, -1);
        if (slot < 0 || slot >= MAX_ACCOUNTS) {
            slot = -1;
            for (int i = 0; i < MAX_ACCOUNTS; i++) {
                if (readAccount(context, i).refreshToken.length() == 0) {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) {
                return;
            }
            prefs(context).edit().putInt(KEY_ACTIVE, slot).commit();
        }
        legacy.slot = slot;
        writeAccount(context, legacy);
    }

    private static Account legacyAccount(Context context) {
        Account account = new Account();
        account.slot = prefs(context).getInt(KEY_ACTIVE, 0);
        account.email = emptyIfNull(read(context, KEY_EMAIL));
        account.refreshToken = emptyIfNull(read(context, KEY_TOKEN));
        account.uid = emptyIfNull(read(context, KEY_UID));
        account.twoFaMarker = emptyIfNull(read(context, KEY_2FA_MARKER));
        account.twoFaVerifier = emptyIfNull(read(context, KEY_2FA_VERIFIER));
        return account;
    }

    private static Account readAccount(Context context, int slot) {
        Account account = new Account();
        account.slot = slot;
        account.email = emptyIfNull(read(context, accountKey(slot, "e")));
        account.refreshToken = emptyIfNull(read(context, accountKey(slot, "t")));
        account.uid = emptyIfNull(read(context, accountKey(slot, "u")));
        account.twoFaMarker = emptyIfNull(read(context, accountKey(slot, "m")));
        account.twoFaVerifier = emptyIfNull(read(context, accountKey(slot, "v")));
        return account;
    }

    private static void writeAccount(Context context, Account account) {
        try {
            SharedPreferences.Editor editor = prefs(context).edit();
            putEncrypted(editor, accountKey(account.slot, "e"), account.email);
            putEncrypted(editor, accountKey(account.slot, "t"), account.refreshToken);
            putEncrypted(editor, accountKey(account.slot, "u"), account.uid);
            putEncrypted(editor, accountKey(account.slot, "m"), account.twoFaMarker);
            putEncrypted(editor, accountKey(account.slot, "v"), account.twoFaVerifier);
            editor.commit();
        } catch (Exception ignored) {
        }
    }

    private static void mirror(Context context, Account account) {
        try {
            SharedPreferences.Editor editor = prefs(context).edit();
            putEncrypted(editor, KEY_EMAIL, account.email);
            putEncrypted(editor, KEY_TOKEN, account.refreshToken);
            putEncrypted(editor, KEY_UID, account.uid);
            putEncrypted(editor, KEY_2FA_MARKER, account.twoFaMarker);
            putEncrypted(editor, KEY_2FA_VERIFIER, account.twoFaVerifier);
            editor.commit();
        } catch (Exception ignored) {
        }
    }

    private static void clearLegacy(Context context) {
        prefs(context).edit()
                .remove(KEY_EMAIL)
                .remove(KEY_TOKEN)
                .remove(KEY_UID)
                .remove(KEY_2FA_MARKER)
                .remove(KEY_2FA_VERIFIER)
                .commit();
    }

    private static void putEncrypted(SharedPreferences.Editor editor,
                                     String key, String value) throws Exception {
        if (value == null || value.length() == 0) {
            editor.remove(key);
        } else {
            editor.putString(key, encrypt(value));
        }
    }

    private static String accountKey(int slot, String suffix) {
        return ACCOUNT_PREFIX + slot + "_" + suffix;
    }

    private static String emptyIfNull(String value) {
        return value == null ? "" : value;
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
