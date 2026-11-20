package lav.visaroom.alav;

import android.util.Base64;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.security.KeyFactory;
import java.security.KeyPair;
import java.security.KeyPairGenerator;
import java.security.MessageDigest;
import java.security.PrivateKey;
import java.security.PublicKey;
import java.security.SecureRandom;
import java.security.Signature;
import java.security.spec.ECGenParameterSpec;
import java.security.spec.PKCS8EncodedKeySpec;
import java.security.spec.X509EncodedKeySpec;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;

import javax.crypto.Cipher;
import javax.crypto.KeyAgreement;
import javax.crypto.Mac;
import javax.crypto.SecretKeyFactory;
import javax.crypto.spec.GCMParameterSpec;
import javax.crypto.spec.PBEKeySpec;
import javax.crypto.spec.SecretKeySpec;

/**
 * visaroom direct-chat end-to-end encryption primitives.
 *
 * <p>The server only ever receives public keys and ciphertext.  A long-term
 * P-256 identity key signs a rotating P-256 pre-key and every message
 * envelope.  Each message gets an ephemeral ECDH key pair and a random
 * 256-bit content key; that content key is wrapped independently for the
 * recipient and for the sender so both participants can read the cloud
 * history on devices that hold (or restore) their own private keys.</p>
 */
final class E2eeCrypto {
    static final int VERSION = 1;
    static final int BACKUP_ITERATIONS = 310000;
    static final int MIN_BACKUP_ITERATIONS = 200000;
    static final int MAX_BACKUP_ITERATIONS = 1000000;
    static final int KEY_BYTES = 32;
    static final int GCM_IV_BYTES = 12;
    static final int GCM_TAG_BITS = 128;
    static final int WRAPPED_KEY_BYTES = GCM_IV_BYTES + KEY_BYTES + 16;
    static final int MAX_CIPHERTEXT_BYTES = 70000;
    static final int FILE_NONCE_PREFIX_BYTES = 4;

    private static final SecureRandom RANDOM = new SecureRandom();
    private static final long DAY_MS = 24L * 60L * 60L * 1000L;
    private static final long PREKEY_LIFETIME_MS = 365L * DAY_MS;
    private static final long PREKEY_ROTATE_BEFORE_MS = 7L * DAY_MS;
    private static final int MAX_PREKEYS = 24;

    private E2eeCrypto() {
    }

    static final class PreKey {
        String id;
        KeyPair keyPair;
        long createdAt;
        long expiresAt;

        JSONObject toJson() throws Exception {
            JSONObject out = new JSONObject();
            out.put("id", id);
            out.put("pub", encode(keyPair.getPublic().getEncoded()));
            out.put("priv", encode(keyPair.getPrivate().getEncoded()));
            out.put("createdAt", createdAt);
            out.put("expiresAt", expiresAt);
            return out;
        }

        static PreKey fromJson(JSONObject value) {
            try {
                if (value == null) {
                    return null;
                }
                PreKey out = new PreKey();
                out.id = value.optString("id", "");
                out.createdAt = value.optLong("createdAt", 0);
                out.expiresAt = value.optLong("expiresAt", 0);
                PublicKey pub = publicKey(decode(value.optString("pub", ""), 256));
                PrivateKey priv = privateKey(decode(value.optString("priv", ""), 512));
                if (!validPreKeyId(out.id) || pub == null || priv == null
                        || out.createdAt <= 0 || out.expiresAt <= out.createdAt) {
                    return null;
                }
                out.keyPair = new KeyPair(pub, priv);
                return out;
            } catch (Exception ignored) {
                return null;
            }
        }
    }

    static final class PublicBundle {
        String uid;
        byte[] signingPublic;
        String preKeyId;
        byte[] preKeyPublic;
        byte[] preKeySignature;
        long preKeyCreatedAt;
        long preKeyExpiresAt;
        long updatedAt;

        String signingPublicEncoded() {
            return encode(signingPublic);
        }

        String preKeyPublicEncoded() {
            return encode(preKeyPublic);
        }

        String preKeySignatureEncoded() {
            return encode(preKeySignature);
        }

        String fingerprint() {
            return E2eeCrypto.fingerprint(signingPublic);
        }

        boolean verifySignedPreKey() {
            PublicKey identity = publicKey(signingPublic);
            PublicKey preKey = publicKey(preKeyPublic);
            return identity != null && preKey != null
                    && validPreKeyId(preKeyId)
                    && preKeyCreatedAt > 0
                    && preKeyExpiresAt > preKeyCreatedAt
                    && verify(identity, preKeySignature,
                            signedPreKeyBytes(uid, signingPublic, preKeyId,
                                    preKeyPublic, preKeyCreatedAt, preKeyExpiresAt));
        }

        boolean verifyForEncryption(long now) {
            return verifySignedPreKey()
                    && preKeyCreatedAt <= now + 5L * 60L * 1000L
                    && preKeyExpiresAt > now;
        }

        static PublicBundle fromEncoded(String uid, String signingPublic,
                                        String preKeyId, String preKeyPublic,
                                        String preKeySignature,
                                        long preKeyCreatedAt, long preKeyExpiresAt,
                                        long updatedAt) {
            PublicBundle out = new PublicBundle();
            out.uid = uid;
            out.signingPublic = decode(signingPublic, 256);
            out.preKeyId = preKeyId;
            out.preKeyPublic = decode(preKeyPublic, 256);
            out.preKeySignature = decode(preKeySignature, 256);
            out.preKeyCreatedAt = preKeyCreatedAt;
            out.preKeyExpiresAt = preKeyExpiresAt;
            out.updatedAt = updatedAt;
            if (out.uid == null || out.uid.length() == 0 || out.uid.length() > 128
                    || publicKey(out.signingPublic) == null
                    || publicKey(out.preKeyPublic) == null
                    || out.preKeySignature == null
                    || out.preKeySignature.length < 64
                    || out.preKeySignature.length > 96
                    || !validPreKeyId(out.preKeyId)
                    || out.preKeyCreatedAt <= 0
                    || out.preKeyExpiresAt <= out.preKeyCreatedAt
                    || out.updatedAt <= 0) {
                return null;
            }
            return out;
        }
    }

    static final class LocalIdentity {
        KeyPair signingKeyPair;
        final ArrayList<PreKey> preKeys = new ArrayList<>();
        byte[] wallpaperRootKey;

        PreKey currentPreKey() {
            return preKeys.isEmpty() ? null : preKeys.get(preKeys.size() - 1);
        }

        PreKey findPreKey(String id) {
            if (id == null) {
                return null;
            }
            for (PreKey preKey : preKeys) {
                if (id.equals(preKey.id)) {
                    return preKey;
                }
            }
            return null;
        }

        PublicBundle publicBundle(String uid, long updatedAt) {
            PreKey current = currentPreKey();
            if (uid == null || signingKeyPair == null || current == null) {
                return null;
            }
            PublicBundle out = new PublicBundle();
            out.uid = uid;
            out.signingPublic = signingKeyPair.getPublic().getEncoded();
            out.preKeyId = current.id;
            out.preKeyPublic = current.keyPair.getPublic().getEncoded();
            out.preKeyCreatedAt = current.createdAt;
            out.preKeyExpiresAt = current.expiresAt;
            out.updatedAt = updatedAt;
            out.preKeySignature = sign(signingKeyPair.getPrivate(),
                    signedPreKeyBytes(uid, out.signingPublic, out.preKeyId,
                            out.preKeyPublic, out.preKeyCreatedAt,
                            out.preKeyExpiresAt));
            return out.preKeySignature == null ? null : out;
        }

        boolean rotatePreKeyIfNeeded(long now) {
            PreKey current = currentPreKey();
            if (current != null && current.expiresAt > now + PREKEY_ROTATE_BEFORE_MS) {
                return false;
            }
            PreKey fresh = generatePreKey(now);
            if (fresh == null) {
                return false;
            }
            preKeys.add(fresh);
            while (preKeys.size() > MAX_PREKEYS) {
                // Keep old keys for old cloud history.  The generous cap is
                // only a corruption bound; at one rotation a year it covers
                // far longer than this protocol version should live.
                preKeys.remove(0);
            }
            return true;
        }

        byte[] wallpaperKey(String chatId, String wallpaperId) {
            if (wallpaperRootKey == null || wallpaperRootKey.length != KEY_BYTES
                    || chatId == null || wallpaperId == null) {
                return null;
            }
            return hkdf(wallpaperRootKey,
                    sha256(canonical("VISAROOM-E2EE-WALLPAPER-SALT-v1",
                            chatId, wallpaperId)),
                    "VISAROOM-E2EE-WALLPAPER-v1".getBytes(StandardCharsets.UTF_8),
                    KEY_BYTES);
        }

        String toJsonString() {
            try {
                JSONObject out = new JSONObject();
                out.put("v", VERSION);
                out.put("signPub", encode(signingKeyPair.getPublic().getEncoded()));
                out.put("signPriv", encode(signingKeyPair.getPrivate().getEncoded()));
                out.put("wallpaperRoot", encode(wallpaperRootKey));
                JSONArray keys = new JSONArray();
                for (PreKey preKey : preKeys) {
                    keys.put(preKey.toJson());
                }
                out.put("preKeys", keys);
                return out.toString();
            } catch (Exception ignored) {
                return null;
            }
        }

        static LocalIdentity fromJsonString(String value) {
            try {
                if (value == null || value.length() == 0 || value.length() > 100000) {
                    return null;
                }
                JSONObject in = new JSONObject(value);
                if (in.optInt("v", 0) != VERSION) {
                    return null;
                }
                PublicKey signPub = publicKey(decode(in.optString("signPub", ""), 256));
                PrivateKey signPriv = privateKey(decode(in.optString("signPriv", ""), 512));
                byte[] wallpaperRoot = decode(in.optString("wallpaperRoot", ""), 64);
                JSONArray keys = in.optJSONArray("preKeys");
                if (signPub == null || signPriv == null || wallpaperRoot == null
                        || wallpaperRoot.length != KEY_BYTES || keys == null
                        || keys.length() == 0 || keys.length() > MAX_PREKEYS) {
                    return null;
                }
                LocalIdentity out = new LocalIdentity();
                out.signingKeyPair = new KeyPair(signPub, signPriv);
                out.wallpaperRootKey = wallpaperRoot;
                for (int i = 0; i < keys.length(); i++) {
                    PreKey preKey = PreKey.fromJson(keys.optJSONObject(i));
                    if (preKey == null || out.findPreKey(preKey.id) != null) {
                        return null;
                    }
                    out.preKeys.add(preKey);
                }
                return out.currentPreKey() == null ? null : out;
            } catch (Exception ignored) {
                return null;
            }
        }
    }

    static final class Session {
        final String myUid;
        final LocalIdentity identity;
        final PublicBundle ownBundle;
        final HashMap<String, PublicBundle> peerBundles = new HashMap<>();
        final HashMap<String, Long> peerBundleCheckedAt = new HashMap<>();

        Session(String myUid, LocalIdentity identity) {
            this.myUid = myUid;
            this.identity = identity;
            this.ownBundle = identity == null ? null
                    : identity.publicBundle(myUid, System.currentTimeMillis());
        }

        boolean usable() {
            return myUid != null && identity != null && ownBundle != null;
        }

        PublicBundle bundleFor(String uid) {
            if (uid == null) {
                return null;
            }
            return uid.equals(myUid) ? ownBundle : peerBundles.get(uid);
        }
    }

    static final class Envelope {
        byte[] ephemeralPublic;
        byte[] recipientWrappedKey;
        byte[] selfWrappedKey;
        byte[] ciphertext;
        byte[] signature;
        String senderFingerprint;
        String recipientPreKeyId;
        String selfPreKeyId;
    }

    static final class Decrypted {
        byte[] plaintext;
        byte[] contentKey;
    }

    static final class BackupData {
        int iterations;
        byte[] salt;
        byte[] ciphertext;
        long updatedAt;
    }

    static LocalIdentity generateIdentity(long now) {
        LocalIdentity out = new LocalIdentity();
        out.signingKeyPair = generateEcKeyPair();
        out.wallpaperRootKey = randomBytes(KEY_BYTES);
        PreKey first = generatePreKey(now);
        if (out.signingKeyPair == null || out.wallpaperRootKey == null || first == null) {
            return null;
        }
        out.preKeys.add(first);
        return out;
    }

    private static PreKey generatePreKey(long now) {
        KeyPair pair = generateEcKeyPair();
        if (pair == null || now <= 0) {
            return null;
        }
        PreKey out = new PreKey();
        out.keyPair = pair;
        out.createdAt = now;
        out.expiresAt = now + PREKEY_LIFETIME_MS;
        out.id = "p" + Long.toString(Math.max(1, now), 36) + hex(randomBytes(8));
        return validPreKeyId(out.id) ? out : null;
    }

    private static KeyPair generateEcKeyPair() {
        try {
            KeyPairGenerator generator = KeyPairGenerator.getInstance("EC");
            generator.initialize(new ECGenParameterSpec("secp256r1"), RANDOM);
            return generator.generateKeyPair();
        } catch (Exception ignored) {
            return null;
        }
    }

    static Envelope encryptPayload(Session session, PublicBundle recipient,
                                   byte[] aad, byte[] plaintext) {
        return encryptPayload(session, recipient, aad, plaintext,
                randomBytes(KEY_BYTES));
    }

    static Envelope encryptPayload(Session session, PublicBundle recipient,
                                   byte[] aad, byte[] plaintext,
                                   byte[] contentKey) {
        if (session == null || !session.usable() || recipient == null || aad == null
                || aad.length == 0 || aad.length > 8192 || plaintext == null
                || plaintext.length == 0
                || plaintext.length > MAX_CIPHERTEXT_BYTES - GCM_IV_BYTES - 16
                || contentKey == null || contentKey.length != KEY_BYTES
                || !recipient.verifyForEncryption(System.currentTimeMillis())
                || !session.ownBundle.verifySignedPreKey()) {
            return null;
        }
        KeyPair ephemeral = generateEcKeyPair();
        if (ephemeral == null) {
            return null;
        }
        Envelope out = new Envelope();
        out.ephemeralPublic = ephemeral.getPublic().getEncoded();
        out.senderFingerprint = session.ownBundle.fingerprint();
        out.recipientPreKeyId = recipient.preKeyId;
        out.selfPreKeyId = session.ownBundle.preKeyId;
        out.recipientWrappedKey = wrapContentKey(ephemeral.getPrivate(),
                publicKey(recipient.preKeyPublic), aad, recipient.uid,
                recipient.preKeyId, "recipient", contentKey);
        out.selfWrappedKey = wrapContentKey(ephemeral.getPrivate(),
                publicKey(session.ownBundle.preKeyPublic), aad, session.myUid,
                session.ownBundle.preKeyId, "self", contentKey);
        byte[] payloadKey = derivePayloadKey(contentKey, aad);
        out.ciphertext = payloadKey == null ? null
                : encryptCombined(payloadKey, plaintext,
                        canonical("VISAROOM-E2EE-PAYLOAD-AAD-v1", aad));
        if (out.recipientWrappedKey == null || out.selfWrappedKey == null
                || out.ciphertext == null) {
            return null;
        }
        out.signature = sign(session.identity.signingKeyPair.getPrivate(),
                envelopeSignatureBytes(aad, out));
        return out.signature == null ? null : out;
    }

    static Decrypted decryptPayload(Session session, PublicBundle senderBundle,
                                    Envelope envelope, byte[] aad,
                                    boolean mineIsSender) {
        if (session == null || !session.usable() || senderBundle == null
                || envelope == null || aad == null || aad.length == 0
                || aad.length > 8192 || !validEnvelope(envelope)
                || !senderBundle.fingerprint().equals(envelope.senderFingerprint)
                || !senderBundle.verifySignedPreKey()) {
            return null;
        }
        PublicKey senderIdentity = publicKey(senderBundle.signingPublic);
        if (senderIdentity == null || !verify(senderIdentity, envelope.signature,
                envelopeSignatureBytes(aad, envelope))) {
            return null;
        }
        String targetPreKeyId = mineIsSender
                ? envelope.selfPreKeyId : envelope.recipientPreKeyId;
        byte[] wrapped = mineIsSender
                ? envelope.selfWrappedKey : envelope.recipientWrappedKey;
        String label = mineIsSender ? "self" : "recipient";
        PreKey ownPreKey = session.identity.findPreKey(targetPreKeyId);
        PublicKey ephemeral = publicKey(envelope.ephemeralPublic);
        if (ownPreKey == null || ephemeral == null) {
            return null;
        }
        byte[] contentKey = unwrapContentKey(ownPreKey.keyPair.getPrivate(),
                ephemeral, aad, session.myUid, targetPreKeyId, label, wrapped);
        byte[] payloadKey = contentKey == null ? null
                : derivePayloadKey(contentKey, aad);
        byte[] plaintext = payloadKey == null ? null
                : decryptCombined(payloadKey, envelope.ciphertext,
                        canonical("VISAROOM-E2EE-PAYLOAD-AAD-v1", aad),
                        MAX_CIPHERTEXT_BYTES);
        if (plaintext == null || plaintext.length == 0) {
            return null;
        }
        Decrypted out = new Decrypted();
        out.plaintext = plaintext;
        out.contentKey = contentKey;
        return out;
    }

    static boolean validEnvelope(Envelope envelope) {
        return envelope != null
                && publicKey(envelope.ephemeralPublic) != null
                && envelope.recipientWrappedKey != null
                && envelope.recipientWrappedKey.length == WRAPPED_KEY_BYTES
                && envelope.selfWrappedKey != null
                && envelope.selfWrappedKey.length == WRAPPED_KEY_BYTES
                && envelope.ciphertext != null
                && envelope.ciphertext.length >= GCM_IV_BYTES + 17
                && envelope.ciphertext.length <= MAX_CIPHERTEXT_BYTES
                && envelope.signature != null
                && envelope.signature.length >= 64
                && envelope.signature.length <= 96
                && validFingerprint(envelope.senderFingerprint)
                && validPreKeyId(envelope.recipientPreKeyId)
                && validPreKeyId(envelope.selfPreKeyId);
    }

    static byte[] fileKey(byte[] contentKey, byte[] fileContext) {
        if (contentKey == null || contentKey.length != KEY_BYTES
                || fileContext == null || fileContext.length == 0
                || fileContext.length > 8192) {
            return null;
        }
        return hkdf(contentKey, sha256(fileContext),
                "VISAROOM-E2EE-FILE-v1".getBytes(StandardCharsets.UTF_8), KEY_BYTES);
    }

    static byte[] encryptFileChunk(byte[] fileKey, byte[] noncePrefix, int index,
                                   byte[] plaintext, int offset, int length,
                                   byte[] fileContext) {
        return fileChunk(Cipher.ENCRYPT_MODE, fileKey, noncePrefix, index,
                plaintext, offset, length, fileContext);
    }

    static byte[] decryptFileChunk(byte[] fileKey, byte[] noncePrefix, int index,
                                   byte[] ciphertext, byte[] fileContext) {
        return fileChunk(Cipher.DECRYPT_MODE, fileKey, noncePrefix, index,
                ciphertext, 0, ciphertext == null ? 0 : ciphertext.length,
                fileContext);
    }

    private static byte[] fileChunk(int mode, byte[] fileKey, byte[] noncePrefix,
                                    int index, byte[] input, int offset, int length,
                                    byte[] fileContext) {
        if (fileKey == null || fileKey.length != KEY_BYTES || noncePrefix == null
                || noncePrefix.length != FILE_NONCE_PREFIX_BYTES || index < 0
                || input == null || offset < 0 || length <= 0
                || offset > input.length - length || fileContext == null) {
            return null;
        }
        try {
            byte[] iv = new byte[GCM_IV_BYTES];
            System.arraycopy(noncePrefix, 0, iv, 0, FILE_NONCE_PREFIX_BYTES);
            long value = index;
            for (int i = GCM_IV_BYTES - 1; i >= FILE_NONCE_PREFIX_BYTES; i--) {
                iv[i] = (byte) (value & 0xff);
                value >>>= 8;
            }
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(mode, new SecretKeySpec(fileKey, "AES"),
                    new GCMParameterSpec(GCM_TAG_BITS, iv));
            cipher.updateAAD(canonical("VISAROOM-E2EE-FILE-CHUNK-v1",
                    fileContext, (long) index));
            return cipher.doFinal(input, offset, length);
        } catch (Exception ignored) {
            return null;
        }
    }

    static byte[] encryptWallpaper(byte[] key, byte[] plaintext, byte[] aad) {
        if (key == null || key.length != KEY_BYTES || plaintext == null
                || plaintext.length == 0 || plaintext.length > 716800 || aad == null) {
            return null;
        }
        return encryptCombined(key, plaintext, aad);
    }

    static byte[] decryptWallpaper(byte[] key, byte[] ciphertext, byte[] aad) {
        if (key == null || key.length != KEY_BYTES || aad == null) {
            return null;
        }
        return decryptCombined(key, ciphertext, aad, 716800 + GCM_IV_BYTES + 16);
    }

    static BackupData encryptBackup(String uid, LocalIdentity identity,
                                    char[] password, long updatedAt) {
        if (uid == null || identity == null || password == null
                || password.length < 8 || password.length > 256 || updatedAt <= 0) {
            return null;
        }
        String json = identity.toJsonString();
        byte[] salt = randomBytes(16);
        byte[] key = pbkdf2(password, salt, BACKUP_ITERATIONS);
        if (json == null || salt == null || key == null) {
            return null;
        }
        byte[] ciphertext = encryptCombined(key,
                json.getBytes(StandardCharsets.UTF_8),
                canonical("VISAROOM-E2EE-BACKUP-v1", uid,
                        (long) BACKUP_ITERATIONS, salt));
        if (ciphertext == null) {
            return null;
        }
        BackupData out = new BackupData();
        out.iterations = BACKUP_ITERATIONS;
        out.salt = salt;
        out.ciphertext = ciphertext;
        out.updatedAt = updatedAt;
        return out;
    }

    static LocalIdentity decryptBackup(String uid, BackupData backup,
                                       char[] password) {
        if (uid == null || backup == null || password == null
                || password.length < 8 || password.length > 256
                || backup.iterations < MIN_BACKUP_ITERATIONS
                || backup.iterations > MAX_BACKUP_ITERATIONS
                || backup.salt == null || backup.salt.length < 16
                || backup.salt.length > 32 || backup.ciphertext == null
                || backup.ciphertext.length < GCM_IV_BYTES + 17
                || backup.ciphertext.length > 131072) {
            return null;
        }
        byte[] key = pbkdf2(password, backup.salt, backup.iterations);
        byte[] plain = key == null ? null : decryptCombined(key, backup.ciphertext,
                canonical("VISAROOM-E2EE-BACKUP-v1", uid,
                        (long) backup.iterations, backup.salt), 131072);
        return plain == null ? null
                : LocalIdentity.fromJsonString(new String(plain, StandardCharsets.UTF_8));
    }

    private static byte[] wrapContentKey(PrivateKey ephemeralPrivate,
                                         PublicKey targetPublic, byte[] aad,
                                         String targetUid, String targetPreKeyId,
                                         String label, byte[] contentKey) {
        byte[] shared = agree(ephemeralPrivate, targetPublic);
        byte[] kek = shared == null ? null : hkdf(shared,
                sha256(canonical("VISAROOM-E2EE-WRAP-SALT-v1", aad, targetUid,
                        targetPreKeyId, label)),
                "VISAROOM-E2EE-WRAP-v1".getBytes(StandardCharsets.UTF_8), KEY_BYTES);
        return kek == null ? null : encryptCombined(kek, contentKey,
                canonical("VISAROOM-E2EE-WRAP-AAD-v1", aad, targetUid,
                        targetPreKeyId, label));
    }

    private static byte[] unwrapContentKey(PrivateKey ownPrivate,
                                           PublicKey ephemeralPublic, byte[] aad,
                                           String targetUid, String targetPreKeyId,
                                           String label, byte[] wrapped) {
        byte[] shared = agree(ownPrivate, ephemeralPublic);
        byte[] kek = shared == null ? null : hkdf(shared,
                sha256(canonical("VISAROOM-E2EE-WRAP-SALT-v1", aad, targetUid,
                        targetPreKeyId, label)),
                "VISAROOM-E2EE-WRAP-v1".getBytes(StandardCharsets.UTF_8), KEY_BYTES);
        byte[] key = kek == null ? null : decryptCombined(kek, wrapped,
                canonical("VISAROOM-E2EE-WRAP-AAD-v1", aad, targetUid,
                        targetPreKeyId, label), WRAPPED_KEY_BYTES);
        return key != null && key.length == KEY_BYTES ? key : null;
    }

    private static byte[] derivePayloadKey(byte[] contentKey, byte[] aad) {
        return hkdf(contentKey, sha256(aad),
                "VISAROOM-E2EE-PAYLOAD-v1".getBytes(StandardCharsets.UTF_8), KEY_BYTES);
    }

    private static byte[] envelopeSignatureBytes(byte[] aad, Envelope envelope) {
        return canonical("VISAROOM-E2EE-MESSAGE-SIGNATURE-v1", aad,
                envelope.ephemeralPublic, envelope.recipientWrappedKey,
                envelope.selfWrappedKey, envelope.ciphertext,
                envelope.senderFingerprint, envelope.recipientPreKeyId,
                envelope.selfPreKeyId);
    }

    private static byte[] signedPreKeyBytes(String uid, byte[] identityPublic,
                                            String preKeyId, byte[] preKeyPublic,
                                            long createdAt, long expiresAt) {
        return canonical("VISAROOM-E2EE-SIGNED-PREKEY-v1", uid, identityPublic,
                preKeyId, preKeyPublic, createdAt, expiresAt);
    }

    private static byte[] agree(PrivateKey privateKey, PublicKey publicKey) {
        try {
            if (privateKey == null || publicKey == null) {
                return null;
            }
            KeyAgreement agreement = KeyAgreement.getInstance("ECDH");
            agreement.init(privateKey);
            agreement.doPhase(publicKey, true);
            byte[] secret = agreement.generateSecret();
            return secret != null && secret.length >= 16 && secret.length <= 64
                    ? secret : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    private static byte[] sign(PrivateKey key, byte[] data) {
        try {
            Signature signature = Signature.getInstance("SHA256withECDSA");
            signature.initSign(key, RANDOM);
            signature.update(data);
            byte[] out = signature.sign();
            return out != null && out.length >= 64 && out.length <= 96 ? out : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    private static boolean verify(PublicKey key, byte[] value, byte[] data) {
        try {
            if (key == null || value == null || data == null) {
                return false;
            }
            Signature signature = Signature.getInstance("SHA256withECDSA");
            signature.initVerify(key);
            signature.update(data);
            return signature.verify(value);
        } catch (Exception ignored) {
            return false;
        }
    }

    static byte[] encryptCombined(byte[] key, byte[] plaintext, byte[] aad) {
        try {
            if (key == null || key.length != KEY_BYTES || plaintext == null || aad == null) {
                return null;
            }
            byte[] iv = randomBytes(GCM_IV_BYTES);
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, new SecretKeySpec(key, "AES"),
                    new GCMParameterSpec(GCM_TAG_BITS, iv));
            cipher.updateAAD(aad);
            byte[] encrypted = cipher.doFinal(plaintext);
            byte[] out = new byte[iv.length + encrypted.length];
            System.arraycopy(iv, 0, out, 0, iv.length);
            System.arraycopy(encrypted, 0, out, iv.length, encrypted.length);
            return out;
        } catch (Exception ignored) {
            return null;
        }
    }

    static byte[] decryptCombined(byte[] key, byte[] combined, byte[] aad, int maxBytes) {
        try {
            if (key == null || key.length != KEY_BYTES || combined == null
                    || combined.length < GCM_IV_BYTES + 16 || combined.length > maxBytes
                    || aad == null) {
                return null;
            }
            byte[] iv = Arrays.copyOfRange(combined, 0, GCM_IV_BYTES);
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, new SecretKeySpec(key, "AES"),
                    new GCMParameterSpec(GCM_TAG_BITS, iv));
            cipher.updateAAD(aad);
            return cipher.doFinal(combined, GCM_IV_BYTES,
                    combined.length - GCM_IV_BYTES);
        } catch (Exception ignored) {
            return null;
        }
    }

    private static byte[] pbkdf2(char[] password, byte[] salt, int iterations) {
        PBEKeySpec spec = null;
        try {
            spec = new PBEKeySpec(password, salt, iterations, KEY_BYTES * 8);
            byte[] out = SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256")
                    .generateSecret(spec).getEncoded();
            return out != null && out.length == KEY_BYTES ? out : null;
        } catch (Exception ignored) {
            return null;
        } finally {
            if (spec != null) {
                spec.clearPassword();
            }
        }
    }

    static byte[] hkdf(byte[] ikm, byte[] salt, byte[] info, int length) {
        try {
            if (ikm == null || ikm.length == 0 || info == null || length <= 0
                    || length > 255 * 32) {
                return null;
            }
            byte[] actualSalt = salt == null || salt.length == 0 ? new byte[32] : salt;
            Mac extract = Mac.getInstance("HmacSHA256");
            extract.init(new SecretKeySpec(actualSalt, "HmacSHA256"));
            byte[] prk = extract.doFinal(ikm);
            Mac expand = Mac.getInstance("HmacSHA256");
            expand.init(new SecretKeySpec(prk, "HmacSHA256"));
            ByteArrayOutputStream out = new ByteArrayOutputStream(length);
            byte[] previous = new byte[0];
            int counter = 1;
            while (out.size() < length) {
                expand.reset();
                expand.update(previous);
                expand.update(info);
                expand.update((byte) counter++);
                previous = expand.doFinal();
                out.write(previous, 0, Math.min(previous.length, length - out.size()));
            }
            return out.toByteArray();
        } catch (Exception ignored) {
            return null;
        }
    }

    static byte[] canonical(String label, Object... parts) {
        try {
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            writePart(out, label == null ? "" : label);
            if (parts != null) {
                for (Object part : parts) {
                    writePart(out, part);
                }
            }
            return out.toByteArray();
        } catch (Exception ignored) {
            return new byte[0];
        }
    }

    private static void writePart(ByteArrayOutputStream out, Object part) throws Exception {
        byte[] bytes;
        if (part instanceof byte[]) {
            bytes = (byte[]) part;
        } else if (part instanceof Number) {
            bytes = Long.toString(((Number) part).longValue())
                    .getBytes(StandardCharsets.UTF_8);
        } else {
            bytes = String.valueOf(part == null ? "" : part)
                    .getBytes(StandardCharsets.UTF_8);
        }
        out.write((bytes.length >>> 24) & 0xff);
        out.write((bytes.length >>> 16) & 0xff);
        out.write((bytes.length >>> 8) & 0xff);
        out.write(bytes.length & 0xff);
        out.write(bytes);
    }

    static PublicKey publicKey(byte[] encoded) {
        try {
            if (encoded == null || encoded.length < 32 || encoded.length > 256) {
                return null;
            }
            PublicKey out = KeyFactory.getInstance("EC")
                    .generatePublic(new X509EncodedKeySpec(encoded));
            return Arrays.equals(out.getEncoded(), encoded) ? out : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    static PrivateKey privateKey(byte[] encoded) {
        try {
            if (encoded == null || encoded.length < 32 || encoded.length > 512) {
                return null;
            }
            PrivateKey out = KeyFactory.getInstance("EC")
                    .generatePrivate(new PKCS8EncodedKeySpec(encoded));
            return Arrays.equals(out.getEncoded(), encoded) ? out : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    static String fingerprint(byte[] publicKey) {
        byte[] digest = sha256(publicKey);
        return digest == null ? "" : hex(digest);
    }

    static String safetyCode(String uidA, byte[] publicA,
                             String uidB, byte[] publicB) {
        if (uidA == null || uidB == null || publicA == null || publicB == null) {
            return "";
        }
        byte[] data = uidA.compareTo(uidB) <= 0
                ? canonical("VISAROOM-E2EE-SAFETY-v1", uidA, publicA, uidB, publicB)
                : canonical("VISAROOM-E2EE-SAFETY-v1", uidB, publicB, uidA, publicA);
        byte[] digest = sha256(data);
        for (int i = 0; i < 999 && digest != null; i++) {
            digest = sha256(digest);
        }
        if (digest == null) {
            return "";
        }
        String hexValue = hex(digest).substring(0, 48).toUpperCase();
        StringBuilder out = new StringBuilder();
        for (int i = 0; i < hexValue.length(); i += 4) {
            if (out.length() > 0) {
                out.append(' ');
            }
            out.append(hexValue, i, Math.min(hexValue.length(), i + 4));
        }
        return out.toString();
    }

    static byte[] sha256(byte[] value) {
        try {
            return value == null ? null
                    : MessageDigest.getInstance("SHA-256").digest(value);
        } catch (Exception ignored) {
            return null;
        }
    }

    static byte[] randomBytes(int length) {
        if (length <= 0 || length > 1048576) {
            return null;
        }
        byte[] out = new byte[length];
        RANDOM.nextBytes(out);
        return out;
    }

    static String encode(byte[] value) {
        return value == null ? "" : Base64.encodeToString(value,
                Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING);
    }

    static byte[] decode(String value, int maxBytes) {
        try {
            if (value == null || value.length() == 0 || value.length() > maxBytes * 2 + 8
                    || value.length() % 4 == 1) {
                return null;
            }
            for (int i = 0; i < value.length(); i++) {
                char c = value.charAt(i);
                boolean ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                        || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok) {
                    return null;
                }
            }
            byte[] out = Base64.decode(value, Base64.URL_SAFE | Base64.NO_WRAP);
            return out != null && out.length <= maxBytes && encode(out).equals(value)
                    ? out : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    static String hex(byte[] value) {
        if (value == null) {
            return "";
        }
        StringBuilder out = new StringBuilder(value.length * 2);
        for (byte b : value) {
            out.append(Character.forDigit((b >> 4) & 15, 16));
            out.append(Character.forDigit(b & 15, 16));
        }
        return out.toString();
    }

    static boolean validPreKeyId(String value) {
        if (value == null || value.length() < 8 || value.length() > 48
                || value.charAt(0) != 'p') {
            return false;
        }
        for (int i = 1; i < value.length(); i++) {
            char c = value.charAt(i);
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z'))) {
                return false;
            }
        }
        return true;
    }

    static boolean validFingerprint(String value) {
        if (value == null || value.length() != 64) {
            return false;
        }
        for (int i = 0; i < value.length(); i++) {
            char c = value.charAt(i);
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) {
                return false;
            }
        }
        return true;
    }
}
