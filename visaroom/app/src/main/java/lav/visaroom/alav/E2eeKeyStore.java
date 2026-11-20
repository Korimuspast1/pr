package lav.visaroom.alav;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import org.json.JSONObject;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.security.MessageDigest;
import java.util.Arrays;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** Device-only storage for E2EE private keys and peer identity pins. */
final class E2eeKeyStore {
    static final int PIN_NEW = 0;
    static final int PIN_MATCH = 1;
    static final int PIN_CHANGED = 2;
    static final int PIN_ERROR = 3;

    private static final String PREFS = "e2ee_local_v1";
    private static final String MASTER_ALIAS = "vr_e2ee_local_v1";
    private static final Object LOCK = new Object();

    private E2eeKeyStore() {
    }

    static final class Pin {
        String peerUid;
        String signingPublic;
        String fingerprint;
    }

    static E2eeCrypto.LocalIdentity loadIdentity(Context context, String uid) {
        String key = identityKey(uid);
        String json = key == null ? null : decrypt(context, key, read(context, key));
        return E2eeCrypto.LocalIdentity.fromJsonString(json);
    }

    static boolean saveIdentity(Context context, String uid,
                                E2eeCrypto.LocalIdentity identity) {
        String key = identityKey(uid);
        String json = identity == null ? null : identity.toJsonString();
        String encrypted = key == null || json == null ? null
                : encrypt(context, key, json);
        return encrypted != null && write(context, key, encrypted);
    }

    static void deleteIdentity(Context context, String uid) {
        String key = identityKey(uid);
        if (context == null || key == null) {
            return;
        }
        prefs(context).edit().remove(key).apply();
    }

    static int pinStatus(Context context, String myUid,
                         E2eeCrypto.PublicBundle bundle) {
        if (bundle == null || bundle.uid == null) {
            return PIN_ERROR;
        }
        Pin existing = loadPin(context, myUid, bundle.uid);
        if (existing == null) {
            return PIN_NEW;
        }
        return bundle.fingerprint().equals(existing.fingerprint)
                && bundle.signingPublicEncoded().equals(existing.signingPublic)
                ? PIN_MATCH : PIN_CHANGED;
    }

    static Pin loadPin(Context context, String myUid, String peerUid) {
        String key = pinKey(myUid, peerUid);
        String json = key == null ? null : decrypt(context, key, read(context, key));
        if (json == null) {
            return null;
        }
        try {
            JSONObject in = new JSONObject(json);
            Pin out = new Pin();
            out.peerUid = in.optString("uid", "");
            out.signingPublic = in.optString("signPub", "");
            out.fingerprint = in.optString("fingerprint", "");
            byte[] pub = E2eeCrypto.decode(out.signingPublic, 256);
            return peerUid != null && peerUid.equals(out.peerUid)
                    && E2eeCrypto.publicKey(pub) != null
                    && E2eeCrypto.fingerprint(pub).equals(out.fingerprint)
                    ? out : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    static boolean savePin(Context context, String myUid,
                           E2eeCrypto.PublicBundle bundle) {
        if (bundle == null || bundle.uid == null) {
            return false;
        }
        String key = pinKey(myUid, bundle.uid);
        try {
            JSONObject json = new JSONObject();
            json.put("uid", bundle.uid);
            json.put("signPub", bundle.signingPublicEncoded());
            json.put("fingerprint", bundle.fingerprint());
            String encrypted = key == null ? null
                    : encrypt(context, key, json.toString());
            return encrypted != null && write(context, key, encrypted);
        } catch (Exception ignored) {
            return false;
        }
    }

    static boolean backupPromptDismissed(Context context, String uid) {
        String key = backupPromptKey(uid);
        return context != null && key != null && prefs(context).getBoolean(key, false);
    }

    static void setBackupPromptDismissed(Context context, String uid, boolean value) {
        String key = backupPromptKey(uid);
        if (context != null && key != null) {
            prefs(context).edit().putBoolean(key, value).apply();
        }
    }

    private static String identityKey(String uid) {
        String hash = hash(uid);
        return hash == null ? null : "identity_" + hash;
    }

    private static String pinKey(String myUid, String peerUid) {
        String myHash = hash(myUid);
        String peerHash = hash(peerUid);
        return myHash == null || peerHash == null ? null
                : "pin_" + myHash + "_" + peerHash;
    }

    private static String backupPromptKey(String uid) {
        String hash = hash(uid);
        return hash == null ? null : "backup_prompt_" + hash;
    }

    private static String hash(String value) {
        try {
            if (value == null || value.length() == 0 || value.length() > 256) {
                return null;
            }
            return E2eeCrypto.hex(MessageDigest.getInstance("SHA-256")
                    .digest(value.getBytes(StandardCharsets.UTF_8)));
        } catch (Exception ignored) {
            return null;
        }
    }

    private static SharedPreferences prefs(Context context) {
        return context.getApplicationContext()
                .getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    private static String read(Context context, String key) {
        try {
            return context == null || key == null ? null
                    : prefs(context).getString(key, null);
        } catch (Exception ignored) {
            return null;
        }
    }

    private static boolean write(Context context, String key, String value) {
        try {
            return context != null && key != null && value != null
                    && prefs(context).edit().putString(key, value).commit();
        } catch (Exception ignored) {
            return false;
        }
    }

    private static String encrypt(Context context, String prefKey, String plaintext) {
        synchronized (LOCK) {
            try {
                if (context == null || prefKey == null || plaintext == null
                        || plaintext.length() == 0 || plaintext.length() > 131072) {
                    return null;
                }
                SecretKey key = masterKey();
                if (key == null) {
                    return null;
                }
                Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
                cipher.init(Cipher.ENCRYPT_MODE, key);
                cipher.updateAAD(aad(prefKey));
                byte[] iv = cipher.getIV();
                byte[] encrypted = cipher.doFinal(
                        plaintext.getBytes(StandardCharsets.UTF_8));
                if (iv == null || iv.length != E2eeCrypto.GCM_IV_BYTES) {
                    return null;
                }
                byte[] out = new byte[iv.length + encrypted.length];
                System.arraycopy(iv, 0, out, 0, iv.length);
                System.arraycopy(encrypted, 0, out, iv.length, encrypted.length);
                return Base64.encodeToString(out, Base64.NO_WRAP);
            } catch (Exception ignored) {
                return null;
            }
        }
    }

    private static String decrypt(Context context, String prefKey, String stored) {
        synchronized (LOCK) {
            try {
                if (context == null || prefKey == null || stored == null
                        || stored.length() == 0 || stored.length() > 262144) {
                    return null;
                }
                SecretKey key = masterKey();
                byte[] combined = Base64.decode(stored, Base64.NO_WRAP);
                if (key == null || combined == null
                        || combined.length <= E2eeCrypto.GCM_IV_BYTES + 16) {
                    return null;
                }
                Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
                cipher.init(Cipher.DECRYPT_MODE, key, new GCMParameterSpec(
                        E2eeCrypto.GCM_TAG_BITS,
                        Arrays.copyOfRange(combined, 0, E2eeCrypto.GCM_IV_BYTES)));
                cipher.updateAAD(aad(prefKey));
                return new String(cipher.doFinal(combined,
                        E2eeCrypto.GCM_IV_BYTES,
                        combined.length - E2eeCrypto.GCM_IV_BYTES),
                        StandardCharsets.UTF_8);
            } catch (Exception ignored) {
                return null;
            }
        }
    }

    private static byte[] aad(String prefKey) {
        return ("visaroom-e2ee-local-v1|" + prefKey)
                .getBytes(StandardCharsets.UTF_8);
    }

    private static SecretKey masterKey() {
        try {
            KeyStore keyStore = KeyStore.getInstance("AndroidKeyStore");
            keyStore.load(null);
            KeyStore.Entry entry = keyStore.getEntry(MASTER_ALIAS, null);
            if (entry instanceof KeyStore.SecretKeyEntry) {
                return ((KeyStore.SecretKeyEntry) entry).getSecretKey();
            }
            KeyGenerator generator = KeyGenerator.getInstance(
                    KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
            KeyGenParameterSpec.Builder builder = new KeyGenParameterSpec.Builder(
                    MASTER_ALIAS,
                    KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                    .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                    .setKeySize(256)
                    .setRandomizedEncryptionRequired(true);
            if (Build.VERSION.SDK_INT >= 28) {
                builder.setUnlockedDeviceRequired(false);
            }
            generator.init(builder.build());
            return generator.generateKey();
        } catch (Exception ignored) {
            return null;
        }
    }
}
