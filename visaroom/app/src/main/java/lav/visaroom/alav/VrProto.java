package lav.visaroom.alav;

import android.util.Base64;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

/**
 * VRProto cloud-chat protocol layer.
 *
 * Direct-chat history is not persisted locally.  Protocol 2 message
 * documents in Firestore contain only authenticated E2EE ciphertext;
 * plaintext exists only in memory on a participant's device after
 * {@link E2eeCrypto} verifies and decrypts an envelope.  Protocol 1
 * documents are accepted only when reading legacy history.  The
 * deterministic chat ID and client message IDs keep synchronization
 * idempotent when the same request is retried.
 */
final class VrProto {

    static final int VERSION = 1;
    static final int E2EE_MESSAGE_VERSION = 2;
    static final int MAX_TEXT = 4000;

    static final String TYPE_TEXT = "text";
    static final String TYPE_FILE = "file";
    static final String TYPE_WALLPAPER = "wallpaper";
    static final String TYPE_VOICE = "voice";
    static final String TYPE_ENCRYPTED = "encrypted";
    static final String ENCRYPTED_TEXT = "Encrypted message";
    private static final String ENCRYPTED_FILE_NAME = "encrypted-file.bin";
    private static final String ENCRYPTED_MIME_TYPE = "application/octet-stream";

    static final String VOICE_TEXT = "Voice message";
    static final String VOICE_FILE_NAME = "voice-message.m4a";
    static final String VOICE_MIME_TYPE = "audio/mp4";
    static final long MAX_VOICE_BYTES = 2L * 1024L * 1024L;
    static final int MAX_VOICE_DURATION_MS = 120000;
    static final int MIN_VOICE_DURATION_MS = 300;

    // Files of any MIME type are split into Firestore byte chunks. This
    // keeps visaroom on the free Firestore-only backend (no Storage/Blaze)
    // while still allowing useful attachments and explicit downloads.
    static final long MAX_FILE_BYTES = 25L * 1024L * 1024L;
    static final int FILE_CHUNK_BYTES = 600 * 1024;
    private static final int FILE_PLAIN_CHUNK_BYTES = FILE_CHUNK_BYTES - 16;
    private static final long MAX_FILE_STORED_BYTES =
            MAX_FILE_BYTES + 16L * 43L;
    private static final long MAX_VOICE_STORED_BYTES =
            MAX_VOICE_BYTES + 16L * 4L;
    static final int MAX_WALLPAPER_BYTES = 700 * 1024 - 28;
    private static final int MAX_WALLPAPER_STORED_BYTES = 700 * 1024;
    // Storage economy: each of the two direct-chat participants may
    // complete one full download. At 2/2 the chunks and metadata are
    // deleted automatically; interrupted downloads do not use a slot.
    static final int FILE_DOWNLOAD_LIMIT = 2;
    static final int MAX_FILE_NAME_CHARS = 128;
    static final int MAX_MIME_CHARS = 96;
    private static final int MAX_FIRESTORE_RESPONSE_BYTES = 4 * 1024 * 1024;
    private static final long MIN_CLIENT_TIME = 1577836800000L; // 2020-01-01
    private static final long MAX_CLIENT_TIME = 4102444800000L; // 2100-01-01
    private static final long MAX_FUTURE_SKEW_MS = 24L * 60L * 60L * 1000L;

    interface ProgressListener {
        void onProgress(int percent);
    }

    interface InputStreamFactory {
        InputStream open() throws Exception;
    }

    static final class Message {
        String id = "";
        String type = TYPE_TEXT;
        String senderUid = "";
        String text = "";
        long createdAt;
        long editedAt;
        boolean pending;
        boolean failed;

        // File envelope (the bytes live in chunk documents, not here).
        String fileId = "";
        String fileName = "";
        String mimeType = "";
        long fileSize;
        int chunkCount;
        int chunkSize;
        // SHA-256 of the complete file for envelopes sent by 1.10+.
        // Empty only for legacy 1.7-1.9 file messages.  For E2EE
        // attachments this remains the plaintext digest after the
        // payload is decrypted; the ciphertext geometry/digest used
        // by Firestore is kept separately below.
        String fileSha256 = "";
        long plainFileSize;
        String plainFileSha256 = "";
        long storedFileSize;
        String storedFileSha256 = "";

        // Protocol 2 E2EE envelope and memory-only decrypted state.
        int protocolVersion = VERSION;
        int e2eeVersion;
        boolean encrypted;
        boolean decryptionFailed;
        E2eeCrypto.Envelope e2eeEnvelope;
        byte[] contentKey;
        byte[] fileNoncePrefix;
        byte[] wallpaperKey;

        // Wallpaper suggestion envelope.
        String wallpaperId = "";
        String wallpaperOwnerUid = "";

        // Voice envelope. The encoded bytes use the same chunk store
        // as files, while durationMs makes the bubble playable without
        // turning a voice note into a one-download file attachment.
        int durationMs;

        // Optional reply snapshot.  In protocol 2 these values exist
        // only inside the encrypted payload; legacy protocol 1 stored
        // them as Firestore fields verified by Rules.
        String replyToId = "";
        String replyToSenderUid = "";
        String replyToType = "";
        String replyToText = "";
        long replyToCreatedAt;

        // Memory-only UI state. These values are never persisted locally.
        String localUri = "";
        int progress;
        boolean downloading;
        int downloadCount;
        int downloadLimit = FILE_DOWNLOAD_LIMIT;
        boolean senderDownloaded;
        boolean recipientDownloaded;
        boolean cloudDeleted;
        boolean downloadStatusLoaded;
        long downloadStatusCheckedAt;
        boolean receiptPending;
        boolean operationPending;
        boolean deleting;
        byte[] voiceData;
        boolean voiceLoading;
        boolean voicePlaying;
        int voicePositionMs;
    }

    static final class FileStatus {
        boolean ok;
        boolean exists;
        int protocolVersion = VERSION;
        String senderUid = "";
        String recipientUid = "";
        String name = "";
        String mimeType = "";
        long size;
        int chunkSize;
        int chunkCount;
        String sha256 = "";
        int downloadCount;
        int downloadLimit = FILE_DOWNLOAD_LIMIT;
        boolean senderDownloaded;
        boolean recipientDownloaded;

        boolean downloadedBy(String uid) {
            return uid != null && ((uid.equals(senderUid) && senderDownloaded)
                    || (uid.equals(recipientUid) && recipientDownloaded));
        }

        boolean complete() {
            return exists && senderDownloaded && recipientDownloaded
                    && downloadCount >= downloadLimit;
        }
    }

    static final class ChatSummary {
        String chatId = "";
        String peerUid = "";
        String username = "";
        long publicId;
        boolean verified;
        boolean blocked;
        String lastText = "";
        String lastSenderUid = "";
        String lastMessageId = "";
        long lastMessageAt;
        long updatedAt;
        long lastReadAt;
    }

    static final class SyncResult {
        boolean ok;
        boolean exists;
        final List<Message> messages = new ArrayList<>();
        long ownReadAt;
        long peerReadAt;
        String ownWallpaperId = "";
        String ownWallpaperOwnerUid = "";
        long ownWallpaperUpdatedAt;
    }

    static final class E2eeBundleResult {
        boolean ok;
        boolean exists;
        E2eeCrypto.PublicBundle bundle;
    }

    static final class E2eeBackupResult {
        boolean ok;
        boolean exists;
        E2eeCrypto.BackupData backup;
    }

    private static final class FileDigests {
        String plaintextSha256 = "";
        String ciphertextSha256 = "";
    }

    private static final class Index {
        String chatId = "";
        String peerUid = "";
        String lastText = "";
        String lastSenderUid = "";
        String lastMessageId = "";
        long lastMessageAt;
        long updatedAt;
        long lastReadAt;
        String wallpaperId = "";
        String wallpaperOwnerUid = "";
        long wallpaperUpdatedAt;
    }

    private static final class Response {
        int code;
        String text = "";
    }

    private VrProto() {
    }

    static String directChatId(String firstUid, String secondUid) {
        if (!validUid(firstUid) || !validUid(secondUid)
                || firstUid.equals(secondUid)) {
            return "";
        }
        String a = firstUid;
        String b = secondUid;
        if (a.compareTo(b) > 0) {
            String t = a;
            a = b;
            b = t;
        }
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256")
                    .digest(("VRProto/1/direct\n" + a + "\n" + b)
                            .getBytes(StandardCharsets.UTF_8));
            StringBuilder out = new StringBuilder("vrp1_");
            for (byte value : digest) {
                out.append(String.format("%02x", value & 0xff));
            }
            return out.toString();
        } catch (Exception ignored) {
            return "";
        }
    }

    static String newWallpaperId(long now) {
        return "w" + newClientId(now).substring(1);
    }

    static int chunkCount(long fileSize, int chunkSize) {
        if (fileSize <= 0 || chunkSize <= 0) {
            return 0;
        }
        return (int) ((fileSize + chunkSize - 1L) / chunkSize);
    }

    static String newClientId(long now) {
        byte[] random = new byte[10];
        new SecureRandom().nextBytes(random);
        StringBuilder out = new StringBuilder("m");
        out.append(Long.toString(now, 36));
        for (byte value : random) {
            out.append(String.format("%02x", value & 0xff));
        }
        return out.toString();
    }

    private static boolean validUid(String uid) {
        return AuthClient.validUid(uid);
    }

    private static boolean validPair(String firstUid, String secondUid) {
        return validUid(firstUid) && validUid(secondUid)
                && !firstUid.equals(secondUid)
                && validChatId(directChatId(firstUid, secondUid));
    }

    private static boolean validChatId(String chatId) {
        return chatId != null && chatId.matches("^vrp1_[0-9a-f]{64}$");
    }

    private static boolean validMessageId(String messageId) {
        return messageId != null && messageId.matches("^m[0-9a-z]{1,20}[0-9a-f]{20}$");
    }

    private static boolean validFileId(String fileId) {
        // In VRProto 1 the metadata document deliberately uses the
        // same client ID as its message envelope.
        return validMessageId(fileId);
    }

    private static boolean validWallpaperId(String wallpaperId) {
        return wallpaperId != null
                && wallpaperId.matches("^w[0-9a-z]{1,20}[0-9a-f]{20}$");
    }

    private static boolean validSha256(String value) {
        return value != null && value.matches("^[0-9a-f]{64}$");
    }

    private static boolean validTime(long value) {
        return value >= MIN_CLIENT_TIME && value <= MAX_CLIENT_TIME;
    }

    private static long saneTime(long value) {
        return validTime(value) ? value : 0;
    }

    private static String bounded(String value, int maxChars) {
        return value == null || value.length() > maxChars ? "" : value;
    }

    /** Removes path separators/control characters from a display name. */
    static String safeFileName(String value) {
        if (value == null) {
            return "";
        }
        String name = value.replace('\\', '/');
        int slash = name.lastIndexOf('/');
        if (slash >= 0) {
            name = name.substring(slash + 1);
        }
        StringBuilder out = new StringBuilder(Math.min(name.length(), MAX_FILE_NAME_CHARS));
        for (int i = 0; i < name.length() && out.length() < MAX_FILE_NAME_CHARS; i++) {
            char c = name.charAt(i);
            if (c < 0x20 || c == 0x7f || c == '/' || c == '\\'
                    || c == ':' || c == '*' || c == '?' || c == '"'
                    || c == '<' || c == '>' || c == '|') {
                out.append('_');
            } else {
                out.append(c);
            }
        }
        String clean = out.toString().trim();
        while (clean.startsWith(".")) {
            clean = clean.substring(1).trim();
        }
        return clean;
    }

    static String safeMimeType(String value) {
        if (value == null) {
            return "";
        }
        String mime = value.trim().toLowerCase(java.util.Locale.ROOT);
        return mime.length() <= MAX_MIME_CHARS
                && mime.matches("^[a-z0-9!#$&^_.+-]+/[a-z0-9!#$&^_.+-]+$")
                ? mime : "";
    }

    private static boolean validFileGeometry(long size, int chunkSize, int count) {
        if (chunkSize != FILE_CHUNK_BYTES || size < 0 || size > MAX_FILE_BYTES) {
            return false;
        }
        if (size == 0) {
            return count == 0;
        }
        return count > 0
                && count == chunkCount(size, chunkSize)
                && count <= chunkCount(MAX_FILE_BYTES, FILE_CHUNK_BYTES);
    }

    private static boolean validStoredFileGeometry(long storedSize, int chunkSize,
                                                     int count, long maxPlainSize) {
        if (chunkSize != FILE_CHUNK_BYTES || storedSize < 0
                || maxPlainSize <= 0 || maxPlainSize > MAX_FILE_BYTES) {
            return false;
        }
        long maxStored = maxPlainSize
                + 16L * chunkCount(maxPlainSize, FILE_PLAIN_CHUNK_BYTES);
        if (storedSize > maxStored) {
            return false;
        }
        if (storedSize == 0) {
            return count == 0;
        }
        return count > 0
                && count == chunkCount(storedSize, chunkSize)
                && count <= chunkCount(maxPlainSize, FILE_PLAIN_CHUNK_BYTES)
                && storedSize > 16L * count;
    }

    private static boolean validEncryptedFileGeometry(long plainSize,
                                                        long storedSize,
                                                        int chunkSize,
                                                        int chunkCount) {
        if (plainSize < 0 || plainSize > MAX_FILE_BYTES
                || chunkSize != FILE_CHUNK_BYTES) {
            return false;
        }
        int expectedCount = chunkCount(plainSize, FILE_PLAIN_CHUNK_BYTES);
        long expectedStored = plainSize == 0 ? 0 : plainSize + 16L * expectedCount;
        return chunkCount == expectedCount && storedSize == expectedStored
                && validStoredFileGeometry(storedSize, chunkSize, chunkCount,
                        MAX_FILE_BYTES);
    }

    private static int expectedChunkLength(long size, int chunkSize, int count, int index) {
        if ((!validFileGeometry(size, chunkSize, count)
                && !validStoredFileGeometry(size, chunkSize, count,
                        MAX_FILE_BYTES))
                || index < 0 || index >= count) {
            return -1;
        }
        long remaining = size - (long) index * chunkSize;
        return (int) Math.min(chunkSize, remaining);
    }

    private static String cloudPreviewText(Message message) {
        if (message == null) {
            return "";
        }
        return message.encrypted
                || message.protocolVersion == E2EE_MESSAGE_VERSION
                || TYPE_ENCRYPTED.equals(message.type)
                ? ENCRYPTED_TEXT
                : message.text == null ? "" : message.text;
    }

    static boolean hasAttachment(Message message) {
        return message != null && message.fileId != null
                && message.fileId.length() > 0
                && message.fileId.equals(message.id);
    }

    private static long expectedStoredFileSize(Message message) {
        return message != null && message.encrypted
                ? message.storedFileSize
                : message == null ? 0 : message.fileSize;
    }

    private static String expectedStoredFileSha256(Message message) {
        if (message == null) {
            return "";
        }
        return message.encrypted
                ? message.storedFileSha256 == null ? "" : message.storedFileSha256
                : message.fileSha256 == null ? "" : message.fileSha256;
    }

    private static String expectedStoredFileName(Message message) {
        return message != null && message.encrypted
                ? ENCRYPTED_FILE_NAME
                : safeFileName(message == null ? "" : message.fileName);
    }

    private static String expectedStoredMimeType(Message message) {
        if (message != null && message.encrypted) {
            return ENCRYPTED_MIME_TYPE;
        }
        String mime = safeMimeType(message == null ? "" : message.mimeType);
        return mime.length() > 0 ? mime : ENCRYPTED_MIME_TYPE;
    }

    private static String sha256Hex(MessageDigest digest) {
        byte[] value = digest.digest();
        StringBuilder out = new StringBuilder(value.length * 2);
        for (byte b : value) {
            out.append(String.format("%02x", b & 0xff));
        }
        return out.toString();
    }


    static E2eeBundleResult loadE2eeBundle(String idToken, String uid) {
        E2eeBundleResult result = new E2eeBundleResult();
        if (idToken == null || !validUid(uid)) {
            return result;
        }
        try {
            Response r = request("GET", Vault.firestoreBaseUrl()
                    + "e2eeKeys/" + uid, idToken, null);
            if (r.code == 404) {
                result.ok = true;
                result.exists = false;
                return result;
            }
            if (r.code != 200) {
                return result;
            }
            result.exists = true;
            JSONObject doc = new JSONObject(r.text);
            JSONObject fields = doc.optJSONObject("fields");
            if (fields == null || fields.length() != 9
                    || !uid.equals(documentId(doc))
                    || num(fields, "version", -1) != E2eeCrypto.VERSION
                    || !uid.equals(str(fields, "uid", ""))) {
                return result;
            }
            String signingPublic = str(fields, "signingPublicKey", "");
            String preKeyId = str(fields, "signedPreKeyId", "");
            String preKeyPublic = str(fields, "signedPreKeyPublicKey", "");
            String preKeySignature = str(fields, "signedPreKeySignature", "");
            long preKeyCreatedAt = num(fields, "signedPreKeyCreatedAt", 0);
            long preKeyExpiresAt = num(fields, "signedPreKeyExpiresAt", 0);
            long updatedAt = num(fields, "updatedAt", 0);
            if (signingPublic.length() > 512 || preKeyPublic.length() > 512
                    || preKeySignature.length() > 512
                    || !validTime(preKeyCreatedAt) || !validTime(preKeyExpiresAt)
                    || !validTime(updatedAt)) {
                return result;
            }
            E2eeCrypto.PublicBundle bundle =
                    E2eeCrypto.PublicBundle.fromEncoded(uid, signingPublic,
                            preKeyId, preKeyPublic, preKeySignature,
                            preKeyCreatedAt, preKeyExpiresAt, updatedAt);
            if (bundle == null || !bundle.verifySignedPreKey()) {
                return result;
            }
            result.bundle = bundle;
            result.ok = true;
            return result;
        } catch (Exception ignored) {
            return result;
        }
    }

    static boolean publishE2eeBundle(String idToken, String uid,
                                     E2eeCrypto.PublicBundle bundle) {
        if (idToken == null || !validUid(uid) || bundle == null
                || !uid.equals(bundle.uid) || !validTime(bundle.updatedAt)
                || !bundle.verifySignedPreKey()) {
            return false;
        }
        try {
            JSONObject fields = new JSONObject();
            putNum(fields, "version", E2eeCrypto.VERSION);
            putStr(fields, "uid", uid);
            putStr(fields, "signingPublicKey", bundle.signingPublicEncoded());
            putStr(fields, "signedPreKeyId", bundle.preKeyId);
            putStr(fields, "signedPreKeyPublicKey", bundle.preKeyPublicEncoded());
            putStr(fields, "signedPreKeySignature", bundle.preKeySignatureEncoded());
            putNum(fields, "signedPreKeyCreatedAt", bundle.preKeyCreatedAt);
            putNum(fields, "signedPreKeyExpiresAt", bundle.preKeyExpiresAt);
            putNum(fields, "updatedAt", bundle.updatedAt);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            StringBuilder url = new StringBuilder(Vault.firestoreBaseUrl())
                    .append("e2eeKeys/").append(uid).append("?");
            String[] paths = {"version", "uid", "signingPublicKey",
                    "signedPreKeyId", "signedPreKeyPublicKey",
                    "signedPreKeySignature", "signedPreKeyCreatedAt",
                    "signedPreKeyExpiresAt", "updatedAt"};
            for (int i = 0; i < paths.length; i++) {
                if (i > 0) {
                    url.append('&');
                }
                url.append("updateMask.fieldPaths=").append(paths[i]);
            }
            Response r = request("PATCH", url.toString(), idToken, body);
            return r.code >= 200 && r.code < 300;
        } catch (Exception ignored) {
            return false;
        }
    }

    static E2eeBackupResult loadE2eeBackup(String idToken, String uid) {
        E2eeBackupResult result = new E2eeBackupResult();
        if (idToken == null || !validUid(uid)) {
            return result;
        }
        try {
            Response r = request("GET", Vault.firestoreBaseUrl() + "users/"
                    + uid + "/security/e2eeBackup", idToken, null);
            if (r.code == 404) {
                result.ok = true;
                result.exists = false;
                return result;
            }
            if (r.code != 200) {
                return result;
            }
            result.exists = true;
            JSONObject doc = new JSONObject(r.text);
            JSONObject fields = doc.optJSONObject("fields");
            long iterations = fields == null ? -1 : num(fields, "iterations", -1);
            long updatedAt = fields == null ? 0 : num(fields, "updatedAt", 0);
            byte[] salt = fields == null ? null : bytes(fields, "salt", 32);
            byte[] ciphertext = fields == null ? null
                    : bytes(fields, "ciphertext", 131072);
            if (fields == null || fields.length() != 5
                    || !"e2eeBackup".equals(documentId(doc))
                    || num(fields, "version", -1) != E2eeCrypto.VERSION
                    || iterations < E2eeCrypto.MIN_BACKUP_ITERATIONS
                    || iterations > E2eeCrypto.MAX_BACKUP_ITERATIONS
                    || !validTime(updatedAt) || salt == null || salt.length < 16
                    || ciphertext == null
                    || ciphertext.length < E2eeCrypto.GCM_IV_BYTES + 17) {
                return result;
            }
            E2eeCrypto.BackupData backup = new E2eeCrypto.BackupData();
            backup.iterations = (int) iterations;
            backup.salt = salt;
            backup.ciphertext = ciphertext;
            backup.updatedAt = updatedAt;
            result.backup = backup;
            result.ok = true;
            return result;
        } catch (Exception ignored) {
            return result;
        }
    }

    static boolean saveE2eeBackup(String idToken, String uid,
                                  E2eeCrypto.BackupData backup) {
        if (idToken == null || !validUid(uid) || backup == null
                || backup.iterations < E2eeCrypto.MIN_BACKUP_ITERATIONS
                || backup.iterations > E2eeCrypto.MAX_BACKUP_ITERATIONS
                || backup.salt == null || backup.salt.length < 16
                || backup.salt.length > 32 || backup.ciphertext == null
                || backup.ciphertext.length < E2eeCrypto.GCM_IV_BYTES + 17
                || backup.ciphertext.length > 131072
                || !validTime(backup.updatedAt)) {
            return false;
        }
        try {
            JSONObject fields = new JSONObject();
            putNum(fields, "version", E2eeCrypto.VERSION);
            putNum(fields, "iterations", backup.iterations);
            putBytes(fields, "salt", backup.salt);
            putBytes(fields, "ciphertext", backup.ciphertext);
            putNum(fields, "updatedAt", backup.updatedAt);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("PATCH", Vault.firestoreBaseUrl() + "users/"
                    + uid + "/security/e2eeBackup"
                    + "?updateMask.fieldPaths=version"
                    + "&updateMask.fieldPaths=iterations"
                    + "&updateMask.fieldPaths=salt"
                    + "&updateMask.fieldPaths=ciphertext"
                    + "&updateMask.fieldPaths=updatedAt", idToken, body);
            return r.code >= 200 && r.code < 300;
        } catch (Exception ignored) {
            return false;
        }
    }

    static boolean deleteE2eeBackup(String idToken, String uid) {
        if (idToken == null || !validUid(uid)) {
            return false;
        }
        try {
            Response r = request("DELETE", Vault.firestoreBaseUrl() + "users/"
                    + uid + "/security/e2eeBackup", idToken, null);
            return (r.code >= 200 && r.code < 300) || r.code == 404;
        } catch (Exception ignored) {
            return false;
        }
    }

    static List<ChatSummary> listChats(String idToken, String myUid) {
        return listChatsInternal(idToken, myUid, true);
    }

    /**
     * Notification checks need only the cloud chat indexes. Loading a
     * public profile for every conversation on every background poll
     * was slow and wasted Firestore reads, so this variant deliberately
     * performs the single bounded index-list request only.
     */
    static List<ChatSummary> listChatPreviews(String idToken, String myUid) {
        return listChatsInternal(idToken, myUid, false);
    }

    private static List<ChatSummary> listChatsInternal(String idToken,
                                                       String myUid,
                                                       boolean includeProfiles) {
        if (idToken == null || !validUid(myUid)) {
            return null;
        }
        try {
            String url = Vault.firestoreBaseUrl() + "users/" + myUid
                    + "/chats?pageSize=100&orderBy=updatedAt%20desc";
            Response r = request("GET", url, idToken, null);
            if (r.code != 200) {
                return null;
            }
            List<ChatSummary> out = new ArrayList<>();
            JSONArray docs = new JSONObject(r.text).optJSONArray("documents");
            if (docs == null) {
                return out;
            }
            for (int i = 0; i < docs.length(); i++) {
                JSONObject doc = docs.optJSONObject(i);
                Index index = parseIndex(doc);
                String indexedChatId = documentId(doc);
                if (index == null || !validUid(index.peerUid)
                        || index.peerUid.equals(myUid) || index.lastMessageAt == 0
                        || !indexedChatId.equals(index.chatId)
                        || !indexedChatId.equals(directChatId(myUid, index.peerUid))
                        || !(myUid.equals(index.lastSenderUid)
                                || index.peerUid.equals(index.lastSenderUid))) {
                    continue;
                }
                ChatSummary summary = new ChatSummary();
                summary.chatId = indexedChatId;
                summary.peerUid = index.peerUid;
                summary.lastText = index.lastText;
                summary.lastSenderUid = index.lastSenderUid;
                summary.lastMessageId = index.lastMessageId;
                summary.lastMessageAt = index.lastMessageAt;
                summary.updatedAt = index.updatedAt;
                summary.lastReadAt = index.lastReadAt;
                if (includeProfiles) {
                    ProfileClient.PublicProfile profile =
                            ProfileClient.loadPublicProfile(idToken, index.peerUid);
                    summary.username = profile.username;
                    summary.publicId = profile.publicId;
                    summary.verified = profile.verified;
                    summary.blocked = profile.blocked;
                }
                out.add(summary);
            }
            Collections.sort(out, (a, b) -> Long.compare(b.updatedAt, a.updatedAt));
            return out;
        } catch (Exception ignored) {
            return null;
        }
    }

    static SyncResult syncDirect(String idToken, String myUid, String peerUid) {
        return syncDirect(idToken, myUid, peerUid, null);
    }

    static SyncResult syncDirect(String idToken, String myUid, String peerUid,
                                 E2eeCrypto.Session session) {
        SyncResult result = new SyncResult();
        if (idToken == null || !validPair(myUid, peerUid)) {
            return result;
        }
        String chatId = directChatId(myUid, peerUid);
        Index own = loadIndex(idToken, myUid, chatId);
        List<Message> messages = listMessages(idToken, myUid, peerUid, chatId, session);
        if (own == null && messages == null) {
            result.ok = true;
            result.exists = false;
            return result;
        }
        if (messages == null) {
            return result;
        }
        Index peer = loadIndex(idToken, peerUid, chatId);
        result.ok = true;
        result.exists = true;
        result.messages.addAll(messages);
        result.ownReadAt = own == null ? 0 : own.lastReadAt;
        result.peerReadAt = peer == null ? 0 : peer.lastReadAt;
        result.ownWallpaperId = own == null ? "" : own.wallpaperId;
        result.ownWallpaperOwnerUid = own == null ? "" : own.wallpaperOwnerUid;
        result.ownWallpaperUpdatedAt = own == null ? 0 : own.wallpaperUpdatedAt;
        return result;
    }

    static boolean hasReply(Message message) {
        return message != null && message.replyToId != null
                && message.replyToId.length() > 0;
    }

    private static boolean validReply(Message message, String myUid, String peerUid) {
        if (message == null) {
            return false;
        }
        boolean any = (message.replyToId != null && message.replyToId.length() > 0)
                || (message.replyToSenderUid != null
                        && message.replyToSenderUid.length() > 0)
                || (message.replyToType != null && message.replyToType.length() > 0)
                || (message.replyToText != null && message.replyToText.length() > 0)
                || message.replyToCreatedAt != 0;
        if (!any) {
            return message.replyToId == null || message.replyToId.length() == 0;
        }
        return validMessageId(message.replyToId)
                && !message.replyToId.equals(message.id)
                && (myUid.equals(message.replyToSenderUid)
                        || peerUid.equals(message.replyToSenderUid))
                && (TYPE_TEXT.equals(message.replyToType)
                        || TYPE_FILE.equals(message.replyToType)
                        || TYPE_WALLPAPER.equals(message.replyToType)
                        || TYPE_VOICE.equals(message.replyToType))
                && message.replyToText != null
                && message.replyToText.length() > 0
                && message.replyToText.length() <= MAX_TEXT
                && validTime(message.replyToCreatedAt)
                && message.replyToCreatedAt <= message.createdAt;
    }

    static void applyReply(Message target, Message original) {
        if (target == null) {
            return;
        }
        if (original == null) {
            target.replyToId = "";
            target.replyToSenderUid = "";
            target.replyToType = "";
            target.replyToText = "";
            target.replyToCreatedAt = 0;
            return;
        }
        target.replyToId = original.id == null ? "" : original.id;
        target.replyToSenderUid = original.senderUid == null ? "" : original.senderUid;
        target.replyToType = original.type == null ? "" : original.type;
        target.replyToText = original.text == null ? "" : original.text;
        target.replyToCreatedAt = original.createdAt;
    }

    private static boolean validVoiceGeometry(long size, int chunkSize,
                                              int chunkCount, int durationMs) {
        return durationMs >= MIN_VOICE_DURATION_MS
                && durationMs <= MAX_VOICE_DURATION_MS
                && size > 0 && size <= MAX_VOICE_BYTES
                && chunkSize == FILE_CHUNK_BYTES
                && chunkCount == chunkCount(size, chunkSize)
                && chunkCount > 0
                && chunkCount <= chunkCount(MAX_VOICE_BYTES, FILE_CHUNK_BYTES);
    }

    static boolean sendText(String idToken, String myUid, String peerUid, Message message) {
        return sendText(idToken, myUid, peerUid, message, false, null);
    }

    /**
     * Fast path used after a successful sync: the message document is
     * created first and {@code delivered} runs immediately after that
     * write. The three preview/index patches still happen, but they no
     * longer make the sender stare at a spinner or delay delivery.
     */
    static boolean sendText(String idToken, String myUid, String peerUid,
                            Message message, boolean chatKnown,
                            Runnable delivered) {
        return sendText(idToken, myUid, peerUid, message, chatKnown,
                null, delivered);
    }

    static boolean sendText(String idToken, String myUid, String peerUid,
                            Message message, boolean chatKnown,
                            E2eeCrypto.Session session, Runnable delivered) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || session == null || !session.usable()
                || !myUid.equals(session.myUid)
                || !validMessageId(message.id)
                || !myUid.equals(message.senderUid)
                || !validTime(message.createdAt)
                || message.text == null || message.text.trim().length() == 0
                || message.text.length() > MAX_TEXT) {
            return false;
        }
        message.type = TYPE_TEXT;
        String chatId = directChatId(myUid, peerUid);
        if (chatKnown) {
            if (createMessageAndPatch(idToken, myUid, peerUid, chatId,
                    message, session, delivered)) {
                return true;
            }
            // The cached "known" state can be stale after a failed
            // first send or a rules/index repair. Rebuild the cloud
            // chat and retry once with the same idempotent client ID.
        }
        if (!ensureDirectChat(idToken, myUid, peerUid, chatId, message.createdAt)) {
            return false;
        }
        return createMessageAndPatch(idToken, myUid, peerUid, chatId,
                message, session, delivered);
    }

    static boolean editText(String idToken, String myUid, String peerUid,
                            Message message, String newText, long editedAt) {
        return editText(idToken, myUid, peerUid, message, newText, editedAt, null);
    }

    static boolean editText(String idToken, String myUid, String peerUid,
                            Message message, String newText, long editedAt,
                            E2eeCrypto.Session session) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || session == null || !session.usable()
                || !myUid.equals(session.myUid)
                || newText == null || newText.trim().length() == 0
                || newText.length() > MAX_TEXT || !validTime(editedAt)
                || !TYPE_TEXT.equals(message.type)
                || !myUid.equals(message.senderUid)
                || !validMessageId(message.id)) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        List<Message> before = listMessages(idToken, myUid, peerUid, chatId, session);
        Message current = messageById(before, message.id);
        if (current == null || !TYPE_TEXT.equals(current.type)
                || !myUid.equals(current.senderUid)
                || current.decryptionFailed) {
            return false;
        }
        boolean wasLatest = isLatest(before, current.id);
        boolean legacy = current.protocolVersion == VERSION && !current.encrypted;
        current.text = newText;
        current.editedAt = editedAt;
        current.e2eeEnvelope = null;
        current.contentKey = null;
        if (!prepareEncryptedEnvelope(session, chatId, myUid, peerUid, current)) {
            return false;
        }
        try {
            JSONObject body = new JSONObject();
            body.put("fields", encryptedMessageFields(current));
            StringBuilder url = new StringBuilder(Vault.firestoreBaseUrl())
                    .append("chats/").append(chatId).append("/messages/")
                    .append(message.id).append('?');
            String[] paths = {"protocolVersion", "type", "senderUid", "text",
                    "createdAt", "clientId", "e2eeVersion",
                    "senderIdentityFingerprint", "recipientPreKeyId",
                    "selfPreKeyId", "ephemeralKey", "recipientWrappedKey",
                    "selfWrappedKey", "ciphertext", "signature", "editedAt",
                    "replyToId", "replyToSenderUid", "replyToType",
                    "replyToText", "replyToCreatedAt"};
            int pathCount = legacy ? paths.length : 16;
            for (int i = 0; i < pathCount; i++) {
                if (i > 0) {
                    url.append('&');
                }
                url.append("updateMask.fieldPaths=").append(paths[i]);
            }
            Response r = request("PATCH", url.toString(), idToken, body);
            boolean ok = r.code >= 200 && r.code < 300;
            if (!ok) {
                Message reloaded = messageById(listMessages(
                        idToken, myUid, peerUid, chatId, session), message.id);
                ok = reloaded != null && reloaded.encrypted
                        && !reloaded.decryptionFailed
                        && newText.equals(reloaded.text)
                        && reloaded.editedAt == editedAt;
                if (!ok) {
                    return false;
                }
                current = reloaded;
            }
            message.text = newText;
            message.editedAt = editedAt;
            message.protocolVersion = E2EE_MESSAGE_VERSION;
            message.e2eeVersion = E2eeCrypto.VERSION;
            message.encrypted = true;
            message.decryptionFailed = false;
            message.e2eeEnvelope = current.e2eeEnvelope;
            if (wasLatest) {
                patchChatAfterEdit(idToken, chatId, myUid, current, editedAt);
                patchIndexAfterEdit(idToken, myUid, peerUid, chatId, current);
                patchIndexAfterEdit(idToken, peerUid, myUid, chatId, current);
            }
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    /**
     * Best-effort in-place migration of this account's own legacy text
     * messages.  Only the sender may rewrite a message, so an old
     * conversation becomes fully ciphertext in Firestore after both
     * participants have opened it with E2EE keys available.  Legacy
     * file chunks are intentionally not rewritten here because a
     * participant's used download slot may make the original bytes
     * unavailable to the sender.
     */
    static int migrateOwnLegacyTexts(String idToken, String myUid,
                                     String peerUid, List<Message> messages,
                                     E2eeCrypto.Session session,
                                     long operationAt) {
        if (idToken == null || !validPair(myUid, peerUid) || messages == null
                || session == null || !session.usable()
                || !myUid.equals(session.myUid) || !validTime(operationAt)) {
            return 0;
        }
        String chatId = directChatId(myUid, peerUid);
        Message latest = latestExcluding(messages, null);
        List<String> migratedIds = new ArrayList<>();
        int migrated = 0;
        for (Message message : messages) {
            if (message == null || message.pending || message.failed
                    || message.encrypted
                    || message.protocolVersion != VERSION
                    || !TYPE_TEXT.equals(message.type)
                    || !myUid.equals(message.senderUid)
                    || !validMessageId(message.id)
                    || message.text == null || message.text.trim().length() == 0
                    || message.text.length() > MAX_TEXT) {
                continue;
            }
            message.e2eeEnvelope = null;
            message.contentKey = null;
            if (!prepareEncryptedEnvelope(session, chatId, myUid, peerUid, message)) {
                break;
            }
            try {
                JSONObject body = new JSONObject();
                body.put("fields", encryptedMessageFields(message));
                StringBuilder url = new StringBuilder(Vault.firestoreBaseUrl())
                        .append("chats/").append(chatId).append("/messages/")
                        .append(message.id).append('?');
                String[] paths = {"protocolVersion", "type", "senderUid", "text",
                        "createdAt", "clientId", "e2eeVersion",
                        "senderIdentityFingerprint", "recipientPreKeyId",
                        "selfPreKeyId", "ephemeralKey", "recipientWrappedKey",
                        "selfWrappedKey", "ciphertext", "signature", "editedAt",
                        "replyToId", "replyToSenderUid", "replyToType",
                        "replyToText", "replyToCreatedAt"};
                for (int i = 0; i < paths.length; i++) {
                    if (i > 0) {
                        url.append('&');
                    }
                    url.append("updateMask.fieldPaths=").append(paths[i]);
                }
                Response r = request("PATCH", url.toString(), idToken, body);
                if (!(r.code >= 200 && r.code < 300)) {
                    break;
                }
                migratedIds.add(message.id);
                migrated++;
            } catch (Exception ignored) {
                break;
            }
        }
        if (latest != null && migratedIds.contains(latest.id)) {
            patchChatAfterEdit(idToken, chatId, latest.senderUid, latest, operationAt);
            patchIndexAfterEdit(idToken, myUid, peerUid, chatId, latest);
            patchIndexAfterEdit(idToken, peerUid, myUid, chatId, latest);
        }
        return migrated;
    }

    static boolean deleteMessage(String idToken, String myUid, String peerUid,
                                 Message message, long operationAt) {
        return deleteMessage(idToken, myUid, peerUid, message, operationAt, null);
    }

    static boolean deleteMessage(String idToken, String myUid, String peerUid,
                                 Message message, long operationAt,
                                 E2eeCrypto.Session session) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || !validMessageId(message.id) || !validTime(operationAt)
                || !myUid.equals(message.senderUid)) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        List<Message> before = listMessages(idToken, myUid, peerUid, chatId, session);
        Message current = messageById(before, message.id);
        if (current == null || !myUid.equals(current.senderUid)) {
            return false;
        }
        boolean wasLatest = isLatest(before, current.id);
        if (hasAttachment(current)
                && !deleteOwnFileChunks(idToken, myUid, peerUid, current)) {
            return false;
        }
        try {
            Response r = request("DELETE", Vault.firestoreBaseUrl()
                    + "chats/" + chatId + "/messages/" + message.id,
                    idToken, null);
            boolean deleted = (r.code >= 200 && r.code < 300) || r.code == 404;
            if (!deleted) {
                Message reloaded = messageById(listMessages(
                        idToken, myUid, peerUid, chatId, session), message.id);
                deleted = reloaded == null;
            }
            if (!deleted) {
                return false;
            }
        } catch (Exception ignored) {
            return false;
        }
        if (hasAttachment(current)) {
            deleteFileMetadataQuietly(idToken, chatId, current.fileId);
        }
        if (wasLatest) {
            List<Message> after = listMessages(idToken, myUid, peerUid, chatId, session);
            Message latest = latestExcluding(after, message.id);
            if (after == null) {
                latest = latestExcluding(before, message.id);
            }
            patchChatAfterDelete(idToken, chatId, latest, operationAt);
            patchIndexAfterDelete(idToken, myUid, peerUid, chatId, latest);
            patchIndexAfterDelete(idToken, peerUid, myUid, chatId, latest);
        }
        return true;
    }

    private static Message messageById(List<Message> list, String id) {
        if (list == null || id == null) {
            return null;
        }
        for (Message item : list) {
            if (id.equals(item.id)) {
                return item;
            }
        }
        return null;
    }

    private static boolean isLatest(List<Message> list, String id) {
        Message latest = latestExcluding(list, null);
        return latest != null && latest.id.equals(id);
    }

    private static Message latestExcluding(List<Message> list, String excludedId) {
        if (list == null) {
            return null;
        }
        for (int i = list.size() - 1; i >= 0; i--) {
            Message item = list.get(i);
            if (excludedId == null || !excludedId.equals(item.id)) {
                return item;
            }
        }
        return null;
    }

    private static boolean deleteOwnFileChunks(String idToken, String myUid,
                                               String peerUid, Message message) {
        FileStatus status = loadFileStatus(idToken, myUid, peerUid, message);
        if (!status.ok) {
            return false;
        }
        if (!status.exists) {
            return true;
        }
        if (!myUid.equals(status.senderUid)) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        int count = status.chunkCount;
        if (!validChatId(chatId) || !validFileId(message.fileId) || count < 0
                || (count == 0 && status.size != 0)) {
            return false;
        }
        try {
            for (int index = 0; index < count; index++) {
                Response r = request("DELETE", Vault.firestoreBaseUrl()
                        + "chats/" + chatId + "/files/" + message.fileId
                        + "/chunks/" + index, idToken, null);
                if (!(r.code >= 200 && r.code < 300) && r.code != 404) {
                    return false;
                }
            }
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static void deleteFileMetadataQuietly(String idToken, String chatId,
                                                  String fileId) {
        try {
            if (idToken == null || !validChatId(chatId) || !validFileId(fileId)) {
                return;
            }
            request("DELETE", fileMetadataUrl(chatId, fileId), idToken, null);
        } catch (Exception ignored) {
        }
    }

    static boolean sendWallpaper(String idToken, String myUid, String peerUid,
                                 Message message) {
        return sendWallpaper(idToken, myUid, peerUid, message, null);
    }

    static boolean sendWallpaper(String idToken, String myUid, String peerUid,
                                 Message message, E2eeCrypto.Session session) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || session == null || !session.usable()
                || !myUid.equals(session.myUid)
                || !validMessageId(message.id)
                || !myUid.equals(message.senderUid)
                || !validTime(message.createdAt)
                || !validWallpaperId(message.wallpaperId)
                || !myUid.equals(message.wallpaperOwnerUid)) {
            return false;
        }
        message.type = TYPE_WALLPAPER;
        message.text = "Wallpaper suggestion";
        String chatId = directChatId(myUid, peerUid);
        if (!ensureDirectChat(idToken, myUid, peerUid, chatId, message.createdAt)) {
            return false;
        }
        message.wallpaperKey = session.identity.wallpaperKey(
                chatId, message.wallpaperId);
        return message.wallpaperKey != null
                && createMessageAndPatch(idToken, myUid, peerUid, chatId,
                        message, session, null);
    }

    static boolean sendFile(String idToken, String myUid, String peerUid,
                            Message message, InputStreamFactory factory,
                            ProgressListener progress) {
        return sendFile(idToken, myUid, peerUid, message, factory,
                progress, false, null, null);
    }

    static boolean sendFile(String idToken, String myUid, String peerUid,
                            Message message, InputStreamFactory factory,
                            ProgressListener progress, boolean chatKnown,
                            Runnable delivered) {
        return sendFile(idToken, myUid, peerUid, message, factory,
                progress, chatKnown, null, delivered);
    }

    static boolean sendFile(String idToken, String myUid, String peerUid,
                            Message message, InputStreamFactory factory,
                            ProgressListener progress, boolean chatKnown,
                            E2eeCrypto.Session session, Runnable delivered) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || factory == null || session == null || !session.usable()
                || !myUid.equals(session.myUid)
                || session.bundleFor(peerUid) == null
                || !validMessageId(message.id)
                || !myUid.equals(message.senderUid)
                || !validTime(message.createdAt)
                || message.fileSize < 0 || message.fileSize > MAX_FILE_BYTES) {
            return false;
        }
        String cleanName = safeFileName(message.fileName);
        if (cleanName.length() == 0) {
            return false;
        }
        boolean voice = TYPE_VOICE.equals(message.type);
        message.fileId = message.id;
        message.fileName = cleanName;
        message.text = voice ? VOICE_TEXT : cleanName;
        if (!voice) {
            message.type = TYPE_FILE;
        }
        String cleanMime = safeMimeType(message.mimeType);
        message.mimeType = cleanMime.length() > 0
                ? cleanMime : "application/octet-stream";
        message.chunkSize = FILE_CHUNK_BYTES;
        message.plainFileSize = message.fileSize;
        message.chunkCount = chunkCount(message.plainFileSize,
                FILE_PLAIN_CHUNK_BYTES);
        message.storedFileSize = message.plainFileSize == 0 ? 0
                : message.plainFileSize + 16L * message.chunkCount;
        message.protocolVersion = E2EE_MESSAGE_VERSION;
        message.e2eeVersion = E2eeCrypto.VERSION;
        message.encrypted = true;
        if (voice ? message.plainFileSize <= 0
                        || message.plainFileSize > MAX_VOICE_BYTES
                        || message.storedFileSize > MAX_VOICE_STORED_BYTES
                        || message.durationMs < MIN_VOICE_DURATION_MS
                        || message.durationMs > MAX_VOICE_DURATION_MS
                        || !validEncryptedFileGeometry(message.plainFileSize,
                                message.storedFileSize, message.chunkSize,
                                message.chunkCount)
                        || !VOICE_FILE_NAME.equals(message.fileName)
                        || !VOICE_MIME_TYPE.equals(message.mimeType)
                : !validEncryptedFileGeometry(message.plainFileSize,
                        message.storedFileSize, message.chunkSize,
                        message.chunkCount)) {
            return false;
        }
        if (message.contentKey == null
                || message.contentKey.length != E2eeCrypto.KEY_BYTES) {
            message.contentKey = E2eeCrypto.randomBytes(E2eeCrypto.KEY_BYTES);
        }
        if (message.fileNoncePrefix == null
                || message.fileNoncePrefix.length
                        != E2eeCrypto.FILE_NONCE_PREFIX_BYTES) {
            message.fileNoncePrefix = E2eeCrypto.randomBytes(
                    E2eeCrypto.FILE_NONCE_PREFIX_BYTES);
        }
        String chatId = directChatId(myUid, peerUid);
        byte[] context = fileContext(chatId, myUid, peerUid, message);
        byte[] fileKey = E2eeCrypto.fileKey(message.contentKey, context);
        if (fileKey == null) {
            return false;
        }
        // Hash and encrypt the complete source stream before metadata
        // is published.  The upload pass repeats both hashes, so a
        // provider stream that changes between passes cannot publish.
        FileDigests digests = hashEncryptedInput(factory, message.plainFileSize,
                fileKey, message.fileNoncePrefix, context);
        if (digests == null || !validSha256(digests.plaintextSha256)
                || !validSha256(digests.ciphertextSha256)) {
            return false;
        }
        message.plainFileSha256 = digests.plaintextSha256;
        message.fileSha256 = digests.plaintextSha256;
        message.storedFileSha256 = digests.ciphertextSha256;
        if (!chatKnown
                && !ensureDirectChat(idToken, myUid, peerUid, chatId,
                        message.createdAt)) {
            return false;
        }
        if (!ensureFileMetadata(idToken, chatId, myUid, peerUid, message)) {
            if (chatKnown
                    && (!ensureDirectChat(idToken, myUid, peerUid, chatId,
                            message.createdAt)
                        || !ensureFileMetadata(idToken, chatId, myUid,
                                peerUid, message))) {
                return false;
            }
        }
        InputStream input = null;
        try {
            input = factory.open();
            if (input == null) {
                return false;
            }
            MessageDigest uploadDigest = MessageDigest.getInstance("SHA-256");
            MessageDigest cipherDigest = MessageDigest.getInstance("SHA-256");
            byte[] buffer = new byte[FILE_PLAIN_CHUNK_BYTES];
            long sent = 0;
            for (int index = 0; index < message.chunkCount; index++) {
                int expected = (int) Math.min(FILE_PLAIN_CHUNK_BYTES,
                        message.plainFileSize - sent);
                int read = readExactly(input, buffer, expected);
                if (read != expected) {
                    return false;
                }
                uploadDigest.update(buffer, 0, expected);
                byte[] chunk = E2eeCrypto.encryptFileChunk(fileKey,
                        message.fileNoncePrefix, index, buffer, 0, expected,
                        context);
                if (chunk == null || chunk.length != expected + 16
                        || chunk.length > FILE_CHUNK_BYTES) {
                    return false;
                }
                cipherDigest.update(chunk);
                if (!ensureFileChunk(idToken, chatId, message.fileId, index, chunk)) {
                    return false;
                }
                sent += expected;
                if (progress != null) {
                    progress.onProgress(message.plainFileSize == 0 ? 100
                            : (int) Math.min(100,
                                    sent * 100L / message.plainFileSize));
                }
            }
            if (input.read() != -1
                    || !sha256Hex(uploadDigest).equals(message.plainFileSha256)
                    || !sha256Hex(cipherDigest).equals(message.storedFileSha256)) {
                return false;
            }
            if (progress != null) {
                progress.onProgress(100);
            }
        } catch (Exception ignored) {
            return false;
        } finally {
            if (input != null) {
                try {
                    input.close();
                } catch (Exception ignored) {
                }
            }
        }
        return createMessageAndPatch(idToken, myUid, peerUid, chatId,
                message, session, delivered);
    }

    static boolean sendVoice(String idToken, String myUid, String peerUid,
                             Message message, InputStreamFactory factory,
                             ProgressListener progress, boolean chatKnown,
                             Runnable delivered) {
        return sendVoice(idToken, myUid, peerUid, message, factory,
                progress, chatKnown, null, delivered);
    }

    static boolean sendVoice(String idToken, String myUid, String peerUid,
                             Message message, InputStreamFactory factory,
                             ProgressListener progress, boolean chatKnown,
                             E2eeCrypto.Session session, Runnable delivered) {
        if (message == null) {
            return false;
        }
        message.type = TYPE_VOICE;
        message.fileName = VOICE_FILE_NAME;
        message.mimeType = VOICE_MIME_TYPE;
        message.text = VOICE_TEXT;
        return sendFile(idToken, myUid, peerUid, message, factory,
                progress, chatKnown, session, delivered);
    }

    private static String hashInput(InputStreamFactory factory, long expectedSize) {
        InputStream input = null;
        try {
            input = factory.open();
            if (input == null || expectedSize < 0 || expectedSize > MAX_FILE_BYTES) {
                return "";
            }
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] buffer = new byte[32 * 1024];
            long total = 0;
            int read;
            while ((read = input.read(buffer)) != -1) {
                if (read == 0) {
                    continue;
                }
                total += read;
                if (total > expectedSize || total > MAX_FILE_BYTES) {
                    return "";
                }
                digest.update(buffer, 0, read);
            }
            return total == expectedSize ? sha256Hex(digest) : "";
        } catch (Exception ignored) {
            return "";
        } finally {
            if (input != null) {
                try {
                    input.close();
                } catch (Exception ignored) {
                }
            }
        }
    }


    private static FileDigests hashEncryptedInput(InputStreamFactory factory,
                                                  long expectedPlainSize,
                                                  byte[] fileKey,
                                                  byte[] noncePrefix,
                                                  byte[] context) {
        InputStream input = null;
        try {
            if (factory == null || expectedPlainSize < 0
                    || expectedPlainSize > MAX_FILE_BYTES || fileKey == null
                    || noncePrefix == null || context == null) {
                return null;
            }
            input = factory.open();
            if (input == null) {
                return null;
            }
            MessageDigest plainDigest = MessageDigest.getInstance("SHA-256");
            MessageDigest cipherDigest = MessageDigest.getInstance("SHA-256");
            byte[] buffer = new byte[FILE_PLAIN_CHUNK_BYTES];
            long total = 0;
            int index = 0;
            while (total < expectedPlainSize) {
                int expected = (int) Math.min(FILE_PLAIN_CHUNK_BYTES,
                        expectedPlainSize - total);
                int read = readExactly(input, buffer, expected);
                if (read != expected) {
                    return null;
                }
                plainDigest.update(buffer, 0, expected);
                byte[] encrypted = E2eeCrypto.encryptFileChunk(fileKey,
                        noncePrefix, index++, buffer, 0, expected, context);
                if (encrypted == null || encrypted.length != expected + 16) {
                    return null;
                }
                cipherDigest.update(encrypted);
                total += expected;
            }
            if (input.read() != -1) {
                return null;
            }
            FileDigests out = new FileDigests();
            out.plaintextSha256 = sha256Hex(plainDigest);
            out.ciphertextSha256 = sha256Hex(cipherDigest);
            return out;
        } catch (Exception ignored) {
            return null;
        } finally {
            if (input != null) {
                try {
                    input.close();
                } catch (Exception ignored) {
                }
            }
        }
    }

    static boolean downloadFile(String idToken, String myUid, String peerUid,
                                Message message, OutputStream output,
                                ProgressListener progress) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || output == null || !TYPE_FILE.equals(message.type)
                || !validMessageId(message.id)
                || !message.id.equals(message.fileId)
                || !(myUid.equals(message.senderUid)
                        || peerUid.equals(message.senderUid))
                || safeFileName(message.fileName).length() == 0) {
            return false;
        }
        boolean encrypted = message.encrypted;
        long plainSize = encrypted ? message.plainFileSize : message.fileSize;
        long storedSize = expectedStoredFileSize(message);
        if (encrypted ? message.protocolVersion != E2EE_MESSAGE_VERSION
                        || message.decryptionFailed
                        || message.fileSize != plainSize
                        || !validSha256(message.plainFileSha256)
                        || !validSha256(message.storedFileSha256)
                        || message.contentKey == null
                        || message.contentKey.length != E2eeCrypto.KEY_BYTES
                        || message.fileNoncePrefix == null
                        || message.fileNoncePrefix.length
                                != E2eeCrypto.FILE_NONCE_PREFIX_BYTES
                        || !validEncryptedFileGeometry(plainSize, storedSize,
                                message.chunkSize, message.chunkCount)
                : !validFileGeometry(message.fileSize, message.chunkSize,
                                message.chunkCount)
                        || !(message.fileSha256 == null
                                || message.fileSha256.length() == 0
                                || validSha256(message.fileSha256))) {
            return false;
        }
        FileStatus status = loadFileStatus(idToken, myUid, peerUid, message);
        String expectedRecipient = myUid.equals(message.senderUid)
                ? peerUid : myUid;
        if (!status.ok || !status.exists || status.downloadedBy(myUid)
                || !fileStatusMatchesMessage(status, message.senderUid,
                        expectedRecipient, message)) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        int chunkSize = message.chunkSize;
        int count = message.chunkCount;
        try {
            List<byte[]> chunks = new ArrayList<>(count);
            MessageDigest storedDigest = MessageDigest.getInstance("SHA-256");
            long received = 0;
            for (int index = 0; index < count; index++) {
                Response r = request("GET", Vault.firestoreBaseUrl()
                        + "chats/" + chatId + "/files/" + message.fileId
                        + "/chunks/" + index, idToken, null);
                if (r.code != 200) {
                    return false;
                }
                JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
                if (fields == null || fields.length() != 2
                        || num(fields, "index", -1) != index) {
                    return false;
                }
                int expected = expectedChunkLength(storedSize, chunkSize, count, index);
                byte[] chunk = bytes(fields, "data", expected);
                if (chunk == null || chunk.length != expected) {
                    return false;
                }
                chunks.add(chunk);
                storedDigest.update(chunk);
                received += chunk.length;
                if (progress != null) {
                    progress.onProgress(storedSize == 0 ? 100
                            : (int) Math.min(100, received * 100L / storedSize));
                }
            }
            String actualStoredSha256 = sha256Hex(storedDigest);
            if (received != storedSize
                    || (status.sha256.length() > 0
                            && !status.sha256.equals(actualStoredSha256))) {
                return false;
            }
            if (!encrypted) {
                for (byte[] chunk : chunks) {
                    output.write(chunk);
                }
            } else {
                byte[] context = fileContext(chatId, myUid, peerUid, message);
                byte[] fileKey = E2eeCrypto.fileKey(message.contentKey, context);
                if (fileKey == null) {
                    return false;
                }
                List<byte[]> plainChunks = new ArrayList<>(count);
                MessageDigest plainDigest = MessageDigest.getInstance("SHA-256");
                long plainReceived = 0;
                for (int index = 0; index < chunks.size(); index++) {
                    byte[] plain = E2eeCrypto.decryptFileChunk(fileKey,
                            message.fileNoncePrefix, index, chunks.get(index), context);
                    int expectedPlain = (int) Math.min(FILE_PLAIN_CHUNK_BYTES,
                            plainSize - plainReceived);
                    if (plain == null || plain.length != expectedPlain) {
                        return false;
                    }
                    plainChunks.add(plain);
                    plainDigest.update(plain);
                    plainReceived += plain.length;
                }
                if (plainReceived != plainSize
                        || !sha256Hex(plainDigest).equals(message.plainFileSha256)) {
                    return false;
                }
                for (byte[] chunk : plainChunks) {
                    output.write(chunk);
                }
            }
            output.flush();
            if (progress != null) {
                progress.onProgress(100);
            }
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    /**
     * Downloads a complete voice payload into memory for playback.
     * Voice chunks intentionally do not consume either participant's
     * one-download file slot, so a voice note remains replayable while
     * its message exists.  Protocol 2 chunks are AES-GCM ciphertext;
     * the returned bytes are the verified plaintext audio.
     */
    static byte[] downloadVoice(String idToken, String myUid, String peerUid,
                                Message message, ProgressListener progress) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || !TYPE_VOICE.equals(message.type)
                || !validMessageId(message.id)
                || !message.id.equals(message.fileId)
                || !(myUid.equals(message.senderUid)
                        || peerUid.equals(message.senderUid))
                || !VOICE_TEXT.equals(message.text)
                || !VOICE_FILE_NAME.equals(message.fileName)
                || !VOICE_MIME_TYPE.equals(message.mimeType)) {
            return null;
        }
        boolean encrypted = message.encrypted;
        long plainSize = encrypted ? message.plainFileSize : message.fileSize;
        long storedSize = expectedStoredFileSize(message);
        if (encrypted ? message.protocolVersion != E2EE_MESSAGE_VERSION
                        || message.decryptionFailed
                        || message.fileSize != plainSize
                        || !validSha256(message.plainFileSha256)
                        || !validSha256(message.storedFileSha256)
                        || message.contentKey == null
                        || message.contentKey.length != E2eeCrypto.KEY_BYTES
                        || message.fileNoncePrefix == null
                        || message.fileNoncePrefix.length
                                != E2eeCrypto.FILE_NONCE_PREFIX_BYTES
                        || plainSize <= 0 || plainSize > MAX_VOICE_BYTES
                        || storedSize > MAX_VOICE_STORED_BYTES
                        || message.durationMs < MIN_VOICE_DURATION_MS
                        || message.durationMs > MAX_VOICE_DURATION_MS
                        || !validEncryptedFileGeometry(plainSize, storedSize,
                                message.chunkSize, message.chunkCount)
                : !validSha256(message.fileSha256)
                        || !validVoiceGeometry(message.fileSize,
                                message.chunkSize, message.chunkCount,
                                message.durationMs)) {
            return null;
        }
        FileStatus status = loadFileStatus(idToken, myUid, peerUid, message);
        String expectedRecipient = myUid.equals(message.senderUid)
                ? peerUid : myUid;
        if (!status.ok || !status.exists
                || !fileStatusMatchesMessage(status, message.senderUid,
                        expectedRecipient, message)) {
            return null;
        }
        String chatId = directChatId(myUid, peerUid);
        try {
            List<byte[]> chunks = new ArrayList<>(message.chunkCount);
            MessageDigest storedDigest = MessageDigest.getInstance("SHA-256");
            long received = 0;
            for (int index = 0; index < message.chunkCount; index++) {
                Response r = request("GET", Vault.firestoreBaseUrl()
                        + "chats/" + chatId + "/files/" + message.fileId
                        + "/chunks/" + index, idToken, null);
                if (r.code != 200) {
                    return null;
                }
                JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
                if (fields == null || fields.length() != 2
                        || num(fields, "index", -1) != index) {
                    return null;
                }
                int expected = expectedChunkLength(storedSize,
                        message.chunkSize, message.chunkCount, index);
                byte[] chunk = bytes(fields, "data", expected);
                if (chunk == null || chunk.length != expected) {
                    return null;
                }
                chunks.add(chunk);
                storedDigest.update(chunk);
                received += chunk.length;
                if (progress != null) {
                    progress.onProgress(storedSize == 0 ? 100
                            : (int) Math.min(100, received * 100L / storedSize));
                }
            }
            if (received != storedSize || plainSize > Integer.MAX_VALUE
                    || !sha256Hex(storedDigest).equals(status.sha256)) {
                return null;
            }
            byte[] out = new byte[(int) plainSize];
            int offset = 0;
            if (!encrypted) {
                for (byte[] chunk : chunks) {
                    System.arraycopy(chunk, 0, out, offset, chunk.length);
                    offset += chunk.length;
                }
                return offset == plainSize ? out : null;
            }
            byte[] context = fileContext(chatId, myUid, peerUid, message);
            byte[] fileKey = E2eeCrypto.fileKey(message.contentKey, context);
            if (fileKey == null) {
                return null;
            }
            MessageDigest plainDigest = MessageDigest.getInstance("SHA-256");
            for (int index = 0; index < chunks.size(); index++) {
                byte[] plain = E2eeCrypto.decryptFileChunk(fileKey,
                        message.fileNoncePrefix, index, chunks.get(index), context);
                int expectedPlain = (int) Math.min(FILE_PLAIN_CHUNK_BYTES,
                        plainSize - offset);
                if (plain == null || plain.length != expectedPlain) {
                    return null;
                }
                plainDigest.update(plain);
                System.arraycopy(plain, 0, out, offset, plain.length);
                offset += plain.length;
            }
            return offset == plainSize
                    && sha256Hex(plainDigest).equals(message.plainFileSha256)
                    ? out : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    static FileStatus loadFileStatus(String idToken, String myUid,
                                     String peerUid, Message message) {
        FileStatus status = new FileStatus();
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || !(TYPE_FILE.equals(message.type)
                        || TYPE_VOICE.equals(message.type)
                        || (TYPE_ENCRYPTED.equals(message.type)
                                && hasAttachment(message)))
                || !validMessageId(message.id)
                || !message.id.equals(message.fileId)
                || !(myUid.equals(message.senderUid)
                        || peerUid.equals(message.senderUid))) {
            return status;
        }
        String chatId = directChatId(myUid, peerUid);
        try {
            Response r = request("GET", fileMetadataUrl(chatId, message.fileId),
                    idToken, null);
            if (r.code == 404) {
                status.ok = true;
                status.exists = false;
                status.downloadCount = FILE_DOWNLOAD_LIMIT;
                return status;
            }
            if (r.code != 200) {
                return status;
            }
            return parseFileStatus(new JSONObject(r.text), message);
        } catch (Exception ignored) {
            return status;
        }
    }

    static FileStatus markFileDownloaded(String idToken, String myUid,
                                         String peerUid, Message message) {
        FileStatus current = loadFileStatus(idToken, myUid, peerUid, message);
        if (!current.ok || !current.exists) {
            return current;
        }
        boolean sender = myUid.equals(current.senderUid);
        boolean recipient = myUid.equals(current.recipientUid);
        if (!sender && !recipient) {
            current.ok = false;
            return current;
        }
        if (current.downloadedBy(myUid)) {
            return current;
        }
        String expectedRecipient = message.senderUid.equals(myUid)
                ? peerUid : myUid;
        if (!message.senderUid.equals(current.senderUid)
                || !expectedRecipient.equals(current.recipientUid)) {
            current.ok = false;
            return current;
        }
        if (current.downloadCount >= current.downloadLimit) {
            current.ok = false;
            return current;
        }
        String flagField = sender ? "senderDownloaded" : "recipientDownloaded";
        try {
            JSONObject fields = new JSONObject();
            putBool(fields, flagField, true);
            putNum(fields, "downloadCount", current.downloadCount + 1);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            String chatId = directChatId(myUid, peerUid);
            Response r = request("PATCH", fileMetadataUrl(chatId, message.fileId)
                    + "?updateMask.fieldPaths=" + flagField
                    + "&updateMask.fieldPaths=downloadCount", idToken, body);
            if (r.code >= 200 && r.code < 300) {
                FileStatus updated = parseFileStatus(new JSONObject(r.text), message);
                return updated.ok ? updated
                        : loadFileStatus(idToken, myUid, peerUid, message);
            }
            FileStatus reloaded = loadFileStatus(idToken, myUid, peerUid, message);
            if (reloaded.ok && reloaded.downloadedBy(myUid)) {
                return reloaded;
            }
            current.ok = false;
            return current;
        } catch (Exception ignored) {
            current.ok = false;
            return current;
        }
    }

    static boolean cleanupDownloadedFile(String idToken, String myUid,
                                         String peerUid, Message message) {
        FileStatus status = loadFileStatus(idToken, myUid, peerUid, message);
        if (!status.ok) {
            return false;
        }
        if (!status.exists) {
            return true;
        }
        if (!status.complete()) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        int count = status.chunkCount;
        if (!validChatId(chatId) || !validFileId(message.fileId) || count < 0) {
            return false;
        }
        try {
            for (int index = 0; index < count; index++) {
                Response r = request("DELETE", Vault.firestoreBaseUrl()
                        + "chats/" + chatId + "/files/" + message.fileId
                        + "/chunks/" + index, idToken, null);
                if (!(r.code >= 200 && r.code < 300) && r.code != 404) {
                    return false;
                }
            }
            Response metadata = request("DELETE",
                    fileMetadataUrl(chatId, message.fileId), idToken, null);
            if (!(metadata.code >= 200 && metadata.code < 300)
                    && metadata.code != 404) {
                return false;
            }
            FileStatus after = loadFileStatus(idToken, myUid, peerUid, message);
            return after.ok && !after.exists;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static String fileMetadataUrl(String chatId, String fileId) {
        return Vault.firestoreBaseUrl() + "chats/" + chatId + "/files/" + fileId;
    }

    private static FileStatus parseFileStatus(JSONObject doc, Message message) {
        FileStatus status = new FileStatus();
        JSONObject fields = doc == null ? null : doc.optJSONObject("fields");
        long statusProtocol = fields == null ? -1
                : num(fields, "protocolVersion", -1);
        if (fields == null || message == null
                || (fields.length() != 14 && fields.length() != 15)
                || !message.fileId.equals(documentId(doc))
                || (statusProtocol != VERSION
                        && statusProtocol != E2EE_MESSAGE_VERSION)) {
            return status;
        }
        String senderUid = bounded(str(fields, "senderUid", ""), 128);
        String recipientUid = bounded(str(fields, "recipientUid", ""), 128);
        String clientId = bounded(str(fields, "clientId", ""), 64);
        String rawName = bounded(str(fields, "fileName", ""), 255);
        String cleanName = safeFileName(rawName);
        long size = num(fields, "fileSize", -1);
        long chunkSizeValue = num(fields, "chunkSize", -1);
        long chunkCountValue = num(fields, "chunkCount", -1);
        long createdAt = num(fields, "createdAt", 0);
        long limitValue = num(fields, "downloadLimit", -1);
        long countValue = num(fields, "downloadCount", -1);
        String sha256 = fields.has("fileSha256")
                ? bounded(str(fields, "fileSha256", ""), 64) : "";
        if (!validUid(senderUid) || !validUid(recipientUid)
                || senderUid.equals(recipientUid)
                || !message.fileId.equals(clientId)
                || cleanName.length() == 0
                || chunkSizeValue < 0 || chunkSizeValue > Integer.MAX_VALUE
                || chunkCountValue < 0 || chunkCountValue > Integer.MAX_VALUE
                || !(statusProtocol == E2EE_MESSAGE_VERSION
                        ? validStoredFileGeometry(size,
                                (int) chunkSizeValue, (int) chunkCountValue,
                                MAX_FILE_BYTES)
                        : validFileGeometry(size, (int) chunkSizeValue,
                                (int) chunkCountValue))
                || !validTime(createdAt) || createdAt != message.createdAt
                || limitValue != FILE_DOWNLOAD_LIMIT
                || countValue < 0 || countValue > FILE_DOWNLOAD_LIMIT
                || (fields.has("fileSha256") && !validSha256(sha256))) {
            return status;
        }
        boolean senderDownloaded = bool(fields, "senderDownloaded", false);
        boolean recipientDownloaded = bool(fields, "recipientDownloaded", false);
        int flagCount = (senderDownloaded ? 1 : 0) + (recipientDownloaded ? 1 : 0);
        if (countValue != flagCount) {
            return status;
        }
        status.protocolVersion = (int) statusProtocol;
        status.senderUid = senderUid;
        status.recipientUid = recipientUid;
        status.name = cleanName;
        String mime = safeMimeType(bounded(str(fields, "mimeType", ""), MAX_MIME_CHARS));
        status.mimeType = mime.length() > 0 ? mime : "application/octet-stream";
        status.size = size;
        status.chunkSize = (int) chunkSizeValue;
        status.chunkCount = (int) chunkCountValue;
        status.sha256 = sha256;
        status.downloadCount = (int) countValue;
        status.downloadLimit = FILE_DOWNLOAD_LIMIT;
        status.senderDownloaded = senderDownloaded;
        status.recipientDownloaded = recipientDownloaded;
        status.ok = true;
        status.exists = true;
        return status;
    }

    static boolean uploadWallpaper(String idToken, String myUid, String peerUid,
                                   String wallpaperId, byte[] image,
                                   boolean shared, long createdAt) {
        return uploadWallpaper(idToken, myUid, peerUid, wallpaperId, image,
                shared, createdAt, null);
    }

    static boolean uploadWallpaper(String idToken, String myUid, String peerUid,
                                   String wallpaperId, byte[] image,
                                   boolean shared, long createdAt,
                                   E2eeCrypto.Session session) {
        if (idToken == null || !validPair(myUid, peerUid)
                || session == null || !session.usable()
                || !myUid.equals(session.myUid)
                || !validWallpaperId(wallpaperId)
                || !validTime(createdAt)
                || image == null || image.length == 0
                || image.length > MAX_WALLPAPER_BYTES) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        byte[] key = session.identity.wallpaperKey(chatId, wallpaperId);
        byte[] stored = key == null ? null : E2eeCrypto.encryptWallpaper(
                key, image, wallpaperAad(chatId, myUid, peerUid, wallpaperId,
                        shared, createdAt));
        if (stored == null || stored.length > MAX_WALLPAPER_STORED_BYTES) {
            return false;
        }
        if (!ensureDirectChat(idToken, myUid, peerUid, chatId, createdAt)) {
            return false;
        }
        try {
            String docUrl = Vault.firestoreBaseUrl() + "users/" + myUid
                    + "/wallpapers/" + wallpaperId;
            Response existing = request("GET", docUrl, idToken, null);
            if (existing.code == 200) {
                return wallpaperDocumentMatches(new JSONObject(existing.text),
                        wallpaperId, chatId, myUid, shared, stored);
            }
            JSONObject fields = new JSONObject();
            putStr(fields, "chatId", chatId);
            putStr(fields, "senderUid", myUid);
            putBool(fields, "shared", shared);
            putBytes(fields, "image", stored);
            putNum(fields, "createdAt", createdAt);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("POST", Vault.firestoreBaseUrl() + "users/"
                    + myUid + "/wallpapers?documentId=" + wallpaperId,
                    idToken, body);
            if (r.code >= 200 && r.code < 300) {
                return true;
            }
            if (r.code == 409 || r.code == 400) {
                Response reloaded = request("GET", docUrl, idToken, null);
                return reloaded.code == 200
                        && wallpaperDocumentMatches(new JSONObject(reloaded.text),
                                wallpaperId, chatId, myUid, shared, stored);
            }
            return false;
        } catch (Exception ignored) {
            return false;
        }
    }

    static byte[] loadWallpaper(String idToken, String ownerUid, String wallpaperId) {
        if (idToken == null || !validUid(ownerUid)
                || !validWallpaperId(wallpaperId)) {
            return null;
        }
        try {
            Response r = request("GET", Vault.firestoreBaseUrl() + "users/"
                    + ownerUid + "/wallpapers/" + wallpaperId, idToken, null);
            if (r.code != 200) {
                return null;
            }
            JSONObject doc = new JSONObject(r.text);
            JSONObject fields = validWallpaperFields(doc, wallpaperId, ownerUid);
            byte[] stored = fields == null ? null
                    : bytes(fields, "image", MAX_WALLPAPER_STORED_BYTES);
            return looksLikeImage(stored) ? stored : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    static byte[] loadWallpaper(String idToken, String myUid, String peerUid,
                                String ownerUid, String wallpaperId,
                                E2eeCrypto.Session session, byte[] suggestedKey) {
        if (idToken == null || !validPair(myUid, peerUid)
                || !(myUid.equals(ownerUid) || peerUid.equals(ownerUid))
                || !validWallpaperId(wallpaperId)) {
            return null;
        }
        try {
            Response r = request("GET", Vault.firestoreBaseUrl() + "users/"
                    + ownerUid + "/wallpapers/" + wallpaperId, idToken, null);
            if (r.code != 200) {
                return null;
            }
            JSONObject doc = new JSONObject(r.text);
            JSONObject fields = validWallpaperFields(doc, wallpaperId, ownerUid);
            if (fields == null) {
                return null;
            }
            String chatId = directChatId(myUid, peerUid);
            if (!chatId.equals(str(fields, "chatId", ""))) {
                return null;
            }
            byte[] stored = bytes(fields, "image", MAX_WALLPAPER_STORED_BYTES);
            if (stored == null || stored.length == 0) {
                return null;
            }
            byte[] key = null;
            if (myUid.equals(ownerUid) && session != null && session.usable()
                    && myUid.equals(session.myUid)) {
                key = session.identity.wallpaperKey(chatId, wallpaperId);
            } else if (suggestedKey != null
                    && suggestedKey.length == E2eeCrypto.KEY_BYTES) {
                key = suggestedKey;
            }
            if (key != null) {
                byte[] plain = E2eeCrypto.decryptWallpaper(key, stored,
                        wallpaperAad(chatId, myUid, peerUid, wallpaperId,
                                bool(fields, "shared", false),
                                num(fields, "createdAt", 0)));
                if (plain != null && plain.length > 0
                        && plain.length <= MAX_WALLPAPER_STORED_BYTES) {
                    return plain;
                }
            }
            // Wallpapers uploaded before protocol 2 are plaintext image
            // bytes.  Keep reading those legacy objects, but never use
            // this fallback for data that is not recognizably an image.
            return looksLikeImage(stored) ? stored : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    private static boolean looksLikeImage(byte[] value) {
        if (value == null || value.length < 3) {
            return false;
        }
        if ((value[0] & 0xff) == 0xff && (value[1] & 0xff) == 0xd8
                && (value[2] & 0xff) == 0xff) {
            return true;
        }
        if (value.length >= 8 && (value[0] & 0xff) == 0x89
                && value[1] == 'P' && value[2] == 'N' && value[3] == 'G') {
            return true;
        }
        if (value.length >= 6 && value[0] == 'G' && value[1] == 'I'
                && value[2] == 'F') {
            return true;
        }
        return value.length >= 12 && value[0] == 'R' && value[1] == 'I'
                && value[2] == 'F' && value[3] == 'F' && value[8] == 'W'
                && value[9] == 'E' && value[10] == 'B' && value[11] == 'P';
    }

    private static JSONObject validWallpaperFields(JSONObject doc, String wallpaperId,
                                                   String ownerUid) {
        JSONObject fields = doc == null ? null : doc.optJSONObject("fields");
        if (fields == null || fields.length() != 5
                || !wallpaperId.equals(documentId(doc))
                || !validChatId(bounded(str(fields, "chatId", ""), 80))
                || !ownerUid.equals(bounded(str(fields, "senderUid", ""), 128))
                || !validTime(num(fields, "createdAt", 0))) {
            return null;
        }
        JSONObject sharedValue = fields.optJSONObject("shared");
        if (sharedValue == null
                || !(sharedValue.opt("booleanValue") instanceof Boolean)) {
            return null;
        }
        byte[] image = bytes(fields, "image", MAX_WALLPAPER_STORED_BYTES);
        return image != null && image.length > 0 ? fields : null;
    }

    private static boolean wallpaperDocumentMatches(JSONObject doc, String wallpaperId,
                                                    String chatId, String ownerUid,
                                                    boolean shared, byte[] image) {
        JSONObject fields = validWallpaperFields(doc, wallpaperId, ownerUid);
        if (fields == null || image == null) {
            return false;
        }
        byte[] existing = bytes(fields, "image", MAX_WALLPAPER_STORED_BYTES);
        return chatId.equals(str(fields, "chatId", ""))
                && bool(fields, "shared", false) == shared
                && existing != null && Arrays.equals(existing, image);
    }

    static boolean setOwnWallpaper(String idToken, String myUid, String peerUid,
                                   String wallpaperId, String ownerUid,
                                   long updatedAt) {
        String cleanWallpaperId = wallpaperId == null ? "" : wallpaperId;
        String cleanOwnerUid = ownerUid == null ? "" : ownerUid;
        boolean clearing = cleanWallpaperId.length() == 0
                && cleanOwnerUid.length() == 0;
        if (idToken == null || !validPair(myUid, peerUid)
                || !validTime(updatedAt)
                || !(clearing || (validWallpaperId(cleanWallpaperId)
                        && (myUid.equals(cleanOwnerUid)
                                || peerUid.equals(cleanOwnerUid))))) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        if (!ensureDirectChat(idToken, myUid, peerUid, chatId, updatedAt)) {
            return false;
        }
        try {
            JSONObject fields = new JSONObject();
            putStr(fields, "wallpaperId", wallpaperId == null ? "" : wallpaperId);
            putStr(fields, "wallpaperOwnerUid", ownerUid == null ? "" : ownerUid);
            putNum(fields, "wallpaperUpdatedAt", updatedAt);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("PATCH", Vault.firestoreBaseUrl()
                    + "users/" + myUid + "/chats/" + chatId
                    + "?updateMask.fieldPaths=wallpaperId"
                    + "&updateMask.fieldPaths=wallpaperOwnerUid"
                    + "&updateMask.fieldPaths=wallpaperUpdatedAt", idToken, body);
            return r.code >= 200 && r.code < 300;
        } catch (Exception ignored) {
            return false;
        }
    }


    private static String recipientFor(Message message, String myUid,
                                       String peerUid) {
        return message != null && myUid.equals(message.senderUid) ? peerUid : myUid;
    }

    private static byte[] messageAad(String chatId, String myUid,
                                     String peerUid, Message message) {
        boolean attachment = hasAttachment(message);
        boolean wallpaper = message != null
                && validWallpaperId(message.wallpaperId)
                && validUid(message.wallpaperOwnerUid);
        return E2eeCrypto.canonical("VISAROOM-E2EE-MESSAGE-v1", chatId,
                message.id, message.senderUid,
                recipientFor(message, myUid, peerUid), message.createdAt,
                message.editedAt, (long) E2EE_MESSAGE_VERSION,
                attachment ? "attachment" : "none",
                attachment ? message.fileId : "",
                attachment ? ENCRYPTED_FILE_NAME : "",
                attachment ? ENCRYPTED_MIME_TYPE : "",
                attachment ? expectedStoredFileSize(message) : 0L,
                attachment ? (long) message.chunkSize : 0L,
                attachment ? (long) message.chunkCount : 0L,
                attachment ? expectedStoredFileSha256(message) : "",
                wallpaper ? "wallpaper" : "none",
                wallpaper ? message.wallpaperId : "",
                wallpaper ? message.wallpaperOwnerUid : "");
    }

    private static byte[] fileContext(String chatId, String myUid,
                                      String peerUid, Message message) {
        return E2eeCrypto.canonical("VISAROOM-E2EE-FILE-CONTEXT-v1", chatId,
                message.id, message.senderUid,
                recipientFor(message, myUid, peerUid), message.createdAt,
                message.fileId);
    }

    private static byte[] wallpaperAad(String chatId, String firstUid,
                                       String secondUid, String wallpaperId,
                                       boolean shared, long createdAt) {
        String a = firstUid;
        String b = secondUid;
        if (a != null && b != null && a.compareTo(b) > 0) {
            String t = a;
            a = b;
            b = t;
        }
        return E2eeCrypto.canonical("VISAROOM-E2EE-WALLPAPER-DOC-v1",
                chatId, a, b, wallpaperId, shared ? 1L : 0L, createdAt);
    }

    private static byte[] buildEncryptedPayload(Message message) {
        try {
            JSONObject payload = new JSONObject();
            payload.put("v", E2eeCrypto.VERSION);
            payload.put("kind", message.type);
            payload.put("text", TYPE_TEXT.equals(message.type)
                    ? message.text == null ? "" : message.text : "");
            if (hasReply(message)) {
                JSONObject reply = new JSONObject();
                reply.put("id", message.replyToId);
                reply.put("senderUid", message.replyToSenderUid);
                reply.put("type", message.replyToType);
                reply.put("text", message.replyToText);
                reply.put("createdAt", message.replyToCreatedAt);
                payload.put("reply", reply);
            }
            if (TYPE_FILE.equals(message.type) || TYPE_VOICE.equals(message.type)) {
                long plainSize = message.plainFileSize > 0 || message.encrypted
                        ? message.plainFileSize : message.fileSize;
                String plainSha = validSha256(message.plainFileSha256)
                        ? message.plainFileSha256 : message.fileSha256;
                payload.put("fileName", message.fileName);
                payload.put("mimeType", message.mimeType);
                payload.put("plainSize", plainSize);
                payload.put("plainSha256", plainSha);
                payload.put("durationMs", message.durationMs);
                payload.put("fileNoncePrefix",
                        E2eeCrypto.encode(message.fileNoncePrefix));
            } else if (TYPE_WALLPAPER.equals(message.type)) {
                payload.put("wallpaperId", message.wallpaperId);
                payload.put("wallpaperOwnerUid", message.wallpaperOwnerUid);
                payload.put("wallpaperKey", E2eeCrypto.encode(message.wallpaperKey));
            }
            byte[] out = payload.toString().getBytes(StandardCharsets.UTF_8);
            return out.length > 0
                    && out.length <= E2eeCrypto.MAX_CIPHERTEXT_BYTES - 28
                    ? out : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    private static boolean prepareEncryptedEnvelope(
            E2eeCrypto.Session session, String chatId, String myUid,
            String peerUid, Message message) {
        if (session == null || !session.usable() || !myUid.equals(session.myUid)
                || message == null || !myUid.equals(message.senderUid)
                || !validReply(message, myUid, peerUid)) {
            return false;
        }
        if (message.e2eeEnvelope != null && message.encrypted
                && E2eeCrypto.validEnvelope(message.e2eeEnvelope)) {
            return true;
        }
        E2eeCrypto.PublicBundle recipient = session.bundleFor(peerUid);
        if (recipient == null || !peerUid.equals(recipient.uid)) {
            return false;
        }
        if (TYPE_WALLPAPER.equals(message.type)
                && (message.wallpaperKey == null
                        || message.wallpaperKey.length != E2eeCrypto.KEY_BYTES)) {
            message.wallpaperKey = session.identity.wallpaperKey(
                    chatId, message.wallpaperId);
            if (message.wallpaperKey == null) {
                return false;
            }
        }
        boolean attachment = TYPE_FILE.equals(message.type)
                || TYPE_VOICE.equals(message.type);
        byte[] contentKey = attachment ? message.contentKey
                : E2eeCrypto.randomBytes(E2eeCrypto.KEY_BYTES);
        if (contentKey == null || contentKey.length != E2eeCrypto.KEY_BYTES) {
            return false;
        }
        // The AAD must see the ciphertext file geometry.  Send-file sets
        // this flag before metadata/chunks are created; setting it here
        // as well keeps text/wallpaper preparation consistent.
        message.encrypted = true;
        byte[] payload = buildEncryptedPayload(message);
        byte[] aad = messageAad(chatId, myUid, peerUid, message);
        E2eeCrypto.Envelope envelope = payload == null ? null
                : E2eeCrypto.encryptPayload(session, recipient, aad, payload,
                        contentKey);
        if (envelope == null) {
            return false;
        }
        message.protocolVersion = E2EE_MESSAGE_VERSION;
        message.e2eeVersion = E2eeCrypto.VERSION;
        message.encrypted = true;
        message.decryptionFailed = false;
        message.e2eeEnvelope = envelope;
        if (attachment) {
            message.contentKey = contentKey;
        }
        return true;
    }

    private static boolean applyDecryptedPayload(Message message,
                                                 E2eeCrypto.Decrypted decrypted,
                                                 String myUid, String peerUid) {
        try {
            if (message == null || decrypted == null || decrypted.plaintext == null
                    || decrypted.plaintext.length == 0
                    || decrypted.plaintext.length > E2eeCrypto.MAX_CIPHERTEXT_BYTES
                    || decrypted.contentKey == null
                    || decrypted.contentKey.length != E2eeCrypto.KEY_BYTES) {
                return false;
            }
            JSONObject payload = new JSONObject(new String(
                    decrypted.plaintext, StandardCharsets.UTF_8));
            if (payload.optInt("v", 0) != E2eeCrypto.VERSION) {
                return false;
            }
            Object kindRaw = payload.opt("kind");
            Object textRaw = payload.opt("text");
            if (!(kindRaw instanceof String) || !(textRaw instanceof String)) {
                return false;
            }
            String kind = (String) kindRaw;
            String text = (String) textRaw;
            boolean outerAttachment = hasAttachment(message);
            boolean outerWallpaper = validWallpaperId(message.wallpaperId)
                    && validUid(message.wallpaperOwnerUid);
            if (payload.has("reply")) {
                JSONObject reply = payload.optJSONObject("reply");
                if (reply == null) {
                    return false;
                }
                message.replyToId = reply.optString("id", "");
                message.replyToSenderUid = reply.optString("senderUid", "");
                message.replyToType = reply.optString("type", "");
                message.replyToText = reply.optString("text", "");
                message.replyToCreatedAt = reply.optLong("createdAt", 0);
            }
            message.contentKey = decrypted.contentKey;
            if (TYPE_TEXT.equals(kind)) {
                if (outerAttachment || outerWallpaper || text.length() == 0
                        || text.trim().length() == 0 || text.length() > MAX_TEXT) {
                    return false;
                }
                message.type = TYPE_TEXT;
                message.text = text;
            } else if (TYPE_FILE.equals(kind) || TYPE_VOICE.equals(kind)) {
                if (!outerAttachment || outerWallpaper || text.length() != 0) {
                    return false;
                }
                String cleanName = safeFileName(payload.optString("fileName", ""));
                String mime = safeMimeType(payload.optString("mimeType", ""));
                long plainSize = payload.optLong("plainSize", -1);
                String plainSha = payload.optString("plainSha256", "");
                long duration = payload.optLong("durationMs", -1);
                byte[] nonce = E2eeCrypto.decode(
                        payload.optString("fileNoncePrefix", ""), 16);
                if (cleanName.length() == 0 || mime.length() == 0
                        || !validSha256(plainSha) || nonce == null
                        || nonce.length != E2eeCrypto.FILE_NONCE_PREFIX_BYTES
                        || !validEncryptedFileGeometry(plainSize,
                                message.storedFileSize, message.chunkSize,
                                message.chunkCount)) {
                    return false;
                }
                if (TYPE_VOICE.equals(kind)
                        && (!VOICE_FILE_NAME.equals(cleanName)
                                || !VOICE_MIME_TYPE.equals(mime)
                                || plainSize <= 0 || plainSize > MAX_VOICE_BYTES
                                || message.storedFileSize > MAX_VOICE_STORED_BYTES
                                || duration < MIN_VOICE_DURATION_MS
                                || duration > MAX_VOICE_DURATION_MS)) {
                    return false;
                }
                if (TYPE_FILE.equals(kind) && duration != 0) {
                    return false;
                }
                message.type = kind;
                message.fileName = cleanName;
                message.mimeType = mime;
                message.fileSize = plainSize;
                message.plainFileSize = plainSize;
                message.fileSha256 = plainSha;
                message.plainFileSha256 = plainSha;
                message.durationMs = TYPE_VOICE.equals(kind) ? (int) duration : 0;
                message.fileNoncePrefix = nonce;
                message.text = TYPE_VOICE.equals(kind) ? VOICE_TEXT : cleanName;
            } else if (TYPE_WALLPAPER.equals(kind)) {
                if (outerAttachment || !outerWallpaper || text.length() != 0
                        || !message.wallpaperId.equals(
                                payload.optString("wallpaperId", ""))
                        || !message.wallpaperOwnerUid.equals(
                                payload.optString("wallpaperOwnerUid", ""))
                        || !message.senderUid.equals(message.wallpaperOwnerUid)) {
                    return false;
                }
                byte[] key = E2eeCrypto.decode(
                        payload.optString("wallpaperKey", ""), 64);
                if (key == null || key.length != E2eeCrypto.KEY_BYTES) {
                    return false;
                }
                message.type = TYPE_WALLPAPER;
                message.text = "Wallpaper suggestion";
                message.wallpaperKey = key;
            } else {
                return false;
            }
            if (!validReply(message, myUid, peerUid)) {
                return false;
            }
            message.decryptionFailed = false;
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean decryptMessageInPlace(Message message, String myUid,
                                                 String peerUid,
                                                 E2eeCrypto.Session session) {
        if (message == null || !message.encrypted) {
            return message != null;
        }
        message.decryptionFailed = true;
        if (session == null || !session.usable() || !myUid.equals(session.myUid)
                || message.e2eeEnvelope == null) {
            return false;
        }
        E2eeCrypto.PublicBundle senderBundle = session.bundleFor(message.senderUid);
        String chatId = directChatId(myUid, peerUid);
        byte[] aad = messageAad(chatId, myUid, peerUid, message);
        E2eeCrypto.Decrypted decrypted = senderBundle == null ? null
                : E2eeCrypto.decryptPayload(session, senderBundle,
                        message.e2eeEnvelope, aad,
                        myUid.equals(message.senderUid));
        return decrypted != null
                && applyDecryptedPayload(message, decrypted, myUid, peerUid);
    }

    private static JSONObject encryptedMessageFields(Message message)
            throws Exception {
        E2eeCrypto.Envelope envelope = message.e2eeEnvelope;
        JSONObject fields = new JSONObject();
        putNum(fields, "protocolVersion", E2EE_MESSAGE_VERSION);
        putStr(fields, "type", TYPE_ENCRYPTED);
        putStr(fields, "senderUid", message.senderUid);
        putStr(fields, "text", ENCRYPTED_TEXT);
        putNum(fields, "createdAt", message.createdAt);
        putStr(fields, "clientId", message.id);
        putNum(fields, "e2eeVersion", E2eeCrypto.VERSION);
        putStr(fields, "senderIdentityFingerprint", envelope.senderFingerprint);
        putStr(fields, "recipientPreKeyId", envelope.recipientPreKeyId);
        putStr(fields, "selfPreKeyId", envelope.selfPreKeyId);
        putBytes(fields, "ephemeralKey", envelope.ephemeralPublic);
        putBytes(fields, "recipientWrappedKey", envelope.recipientWrappedKey);
        putBytes(fields, "selfWrappedKey", envelope.selfWrappedKey);
        putBytes(fields, "ciphertext", envelope.ciphertext);
        putBytes(fields, "signature", envelope.signature);
        boolean attachment = TYPE_FILE.equals(message.type)
                || TYPE_VOICE.equals(message.type)
                || (TYPE_ENCRYPTED.equals(message.type) && hasAttachment(message));
        boolean wallpaper = TYPE_WALLPAPER.equals(message.type)
                || (TYPE_ENCRYPTED.equals(message.type)
                        && validWallpaperId(message.wallpaperId));
        if (attachment) {
            putStr(fields, "fileId", message.fileId);
            putStr(fields, "fileName", ENCRYPTED_FILE_NAME);
            putStr(fields, "mimeType", ENCRYPTED_MIME_TYPE);
            putNum(fields, "fileSize", expectedStoredFileSize(message));
            putNum(fields, "chunkCount", message.chunkCount);
            putNum(fields, "chunkSize", message.chunkSize);
            putStr(fields, "fileSha256", expectedStoredFileSha256(message));
        } else if (wallpaper) {
            putStr(fields, "wallpaperId", message.wallpaperId);
            putStr(fields, "wallpaperOwnerUid", message.wallpaperOwnerUid);
        }
        if (message.editedAt > 0) {
            putNum(fields, "editedAt", message.editedAt);
        }
        return fields;
    }

    private static boolean sameEnvelope(E2eeCrypto.Envelope a,
                                        E2eeCrypto.Envelope b) {
        return a != null && b != null
                && Arrays.equals(a.ephemeralPublic, b.ephemeralPublic)
                && Arrays.equals(a.recipientWrappedKey, b.recipientWrappedKey)
                && Arrays.equals(a.selfWrappedKey, b.selfWrappedKey)
                && Arrays.equals(a.ciphertext, b.ciphertext)
                && Arrays.equals(a.signature, b.signature)
                && a.senderFingerprint.equals(b.senderFingerprint)
                && a.recipientPreKeyId.equals(b.recipientPreKeyId)
                && a.selfPreKeyId.equals(b.selfPreKeyId);
    }

    private static boolean createMessageAndPatch(String idToken, String myUid,
                                                 String peerUid, String chatId,
                                                 Message message) {
        return createMessageAndPatch(idToken, myUid, peerUid, chatId,
                message, null, null);
    }

    private static boolean createMessageAndPatch(String idToken, String myUid,
                                                 String peerUid, String chatId,
                                                 Message message,
                                                 E2eeCrypto.Session session,
                                                 Runnable delivered) {
        // Validate again at the single Firestore write boundary.  The
        // public send* methods validate their inputs too, but this
        // private method must not trust a future caller that forgot to.
        if (idToken == null || !validPair(myUid, peerUid)
                || chatId == null
                || !chatId.equals(directChatId(myUid, peerUid))
                || message == null || session == null || !session.usable()
                || !myUid.equals(session.myUid)
                || !validMessageId(message.id)
                || !myUid.equals(message.senderUid)
                || !validTime(message.createdAt)
                || message.text == null
                || message.text.length() == 0
                || message.text.length() > MAX_TEXT
                || !validReply(message, myUid, peerUid)) {
            return false;
        }
        if (TYPE_TEXT.equals(message.type)) {
            if (message.text.trim().length() == 0) {
                return false;
            }
        } else if (TYPE_FILE.equals(message.type)) {
            String cleanName = safeFileName(message.fileName);
            if (!message.id.equals(message.fileId)
                    || cleanName.length() == 0
                    || !cleanName.equals(message.fileName)
                    || !cleanName.equals(message.text)
                    || !safeMimeType(message.mimeType).equals(message.mimeType)
                    || !validSha256(message.plainFileSha256)
                    || !validSha256(message.storedFileSha256)
                    || !validEncryptedFileGeometry(message.plainFileSize,
                            message.storedFileSize, message.chunkSize,
                            message.chunkCount)) {
                return false;
            }
        } else if (TYPE_VOICE.equals(message.type)) {
            if (!VOICE_TEXT.equals(message.text)
                    || !message.id.equals(message.fileId)
                    || !VOICE_FILE_NAME.equals(message.fileName)
                    || !VOICE_MIME_TYPE.equals(message.mimeType)
                    || !validSha256(message.plainFileSha256)
                    || !validSha256(message.storedFileSha256)
                    || message.plainFileSize <= 0
                    || message.plainFileSize > MAX_VOICE_BYTES
                    || message.storedFileSize > MAX_VOICE_STORED_BYTES
                    || message.durationMs < MIN_VOICE_DURATION_MS
                    || message.durationMs > MAX_VOICE_DURATION_MS
                    || !validEncryptedFileGeometry(message.plainFileSize,
                            message.storedFileSize, message.chunkSize,
                            message.chunkCount)) {
                return false;
            }
        } else if (TYPE_WALLPAPER.equals(message.type)) {
            if (!"Wallpaper suggestion".equals(message.text)
                    || !validWallpaperId(message.wallpaperId)
                    || !myUid.equals(message.wallpaperOwnerUid)) {
                return false;
            }
        } else {
            return false;
        }
        if (!prepareEncryptedEnvelope(session, chatId, myUid, peerUid, message)) {
            return false;
        }
        try {
            JSONObject fields = encryptedMessageFields(message);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("POST", Vault.firestoreBaseUrl()
                    + "chats/" + chatId + "/messages?documentId=" + message.id,
                    idToken, body);
            boolean created = r.code >= 200 && r.code < 300;
            if (!created && (r.code == 409 || r.code == 400)) {
                Response existing = request("GET", Vault.firestoreBaseUrl()
                        + "chats/" + chatId + "/messages/" + message.id, idToken, null);
                created = existing.code == 200
                        && messageDocumentMatches(new JSONObject(existing.text),
                                myUid, peerUid, message);
            }
            if (!created) {
                return false;
            }
            if (delivered != null) {
                try {
                    delivered.run();
                } catch (Exception ignored) {
                    // Delivery already succeeded; a UI callback must not
                    // turn that successful write into a false failure.
                }
            }
            patchChatAfterSend(idToken, chatId, myUid, message);
            patchIndexAfterSend(idToken, myUid, peerUid, chatId, myUid, message);
            patchIndexAfterSend(idToken, peerUid, myUid, chatId, myUid, message);
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean ensureFileMetadata(String idToken, String chatId,
                                              String senderUid, String recipientUid,
                                              Message message) {
        try {
            if (idToken == null || !validPair(senderUid, recipientUid)
                    || !chatId.equals(directChatId(senderUid, recipientUid))
                    || message == null || !validMessageId(message.id)
                    || !message.id.equals(message.fileId)
                    || !senderUid.equals(message.senderUid)
                    || !validSha256(expectedStoredFileSha256(message))
                    || !(message.encrypted
                            ? validStoredFileGeometry(
                                    expectedStoredFileSize(message),
                                    message.chunkSize, message.chunkCount,
                                    MAX_FILE_BYTES)
                            : validFileGeometry(message.fileSize,
                                    message.chunkSize, message.chunkCount))) {
                return false;
            }
            String docUrl = Vault.firestoreBaseUrl() + "chats/" + chatId
                    + "/files/" + message.fileId;
            Response existing = request("GET", docUrl, idToken, null);
            if (existing.code == 200) {
                FileStatus status = parseFileStatus(
                        new JSONObject(existing.text), message);
                return fileStatusMatchesMessage(status, senderUid,
                        recipientUid, message);
            }
            JSONObject fields = new JSONObject();
            putNum(fields, "protocolVersion", message.encrypted
                    ? E2EE_MESSAGE_VERSION : VERSION);
            putStr(fields, "senderUid", senderUid);
            putStr(fields, "recipientUid", recipientUid);
            putStr(fields, "fileName", expectedStoredFileName(message));
            putStr(fields, "mimeType", expectedStoredMimeType(message));
            putNum(fields, "fileSize", expectedStoredFileSize(message));
            putNum(fields, "chunkSize", message.chunkSize);
            putNum(fields, "chunkCount", message.chunkCount);
            putStr(fields, "fileSha256", expectedStoredFileSha256(message));
            putNum(fields, "createdAt", message.createdAt);
            putStr(fields, "clientId", message.fileId);
            putNum(fields, "downloadLimit", FILE_DOWNLOAD_LIMIT);
            putNum(fields, "downloadCount", 0);
            putBool(fields, "senderDownloaded", false);
            putBool(fields, "recipientDownloaded", false);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("POST", Vault.firestoreBaseUrl() + "chats/"
                    + chatId + "/files?documentId=" + message.fileId,
                    idToken, body);
            if (r.code >= 200 && r.code < 300) {
                return true;
            }
            if (r.code == 409 || r.code == 400) {
                Response reloaded = request("GET", docUrl, idToken, null);
                if (reloaded.code == 200) {
                    FileStatus status = parseFileStatus(
                            new JSONObject(reloaded.text), message);
                    return fileStatusMatchesMessage(status, senderUid,
                            recipientUid, message);
                }
            }
            return false;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean fileStatusMatchesMessage(FileStatus status,
                                                    String senderUid,
                                                    String recipientUid,
                                                    Message message) {
        if (status == null || !status.ok || !status.exists || message == null) {
            return false;
        }
        return status.protocolVersion == (message.encrypted
                        ? E2EE_MESSAGE_VERSION : VERSION)
                && senderUid.equals(status.senderUid)
                && recipientUid.equals(status.recipientUid)
                && expectedStoredFileName(message).equals(status.name)
                && expectedStoredMimeType(message).equals(status.mimeType)
                && expectedStoredFileSize(message) == status.size
                && message.chunkSize == status.chunkSize
                && message.chunkCount == status.chunkCount
                && expectedStoredFileSha256(message).equals(status.sha256);
    }

    private static boolean ensureFileChunk(String idToken, String chatId,
                                           String fileId, int index,
                                           byte[] chunk) {
        try {
            if (idToken == null || !validChatId(chatId) || !validFileId(fileId)
                    || index < 0 || index > chunkCount(MAX_FILE_BYTES, FILE_CHUNK_BYTES)
                    || chunk == null || chunk.length == 0
                    || chunk.length > FILE_CHUNK_BYTES) {
                return false;
            }
            String docUrl = Vault.firestoreBaseUrl() + "chats/" + chatId
                    + "/files/" + fileId + "/chunks/" + index;
            Response existing = request("GET", docUrl, idToken, null);
            if (existing.code == 200) {
                return chunkDocumentMatches(new JSONObject(existing.text),
                        index, chunk);
            }
            JSONObject fields = new JSONObject();
            putNum(fields, "index", index);
            putBytes(fields, "data", chunk);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("POST", Vault.firestoreBaseUrl() + "chats/"
                    + chatId + "/files/" + fileId
                    + "/chunks?documentId=" + index, idToken, body);
            if (r.code >= 200 && r.code < 300) {
                return true;
            }
            if (r.code == 409 || r.code == 400) {
                Response reloaded = request("GET", docUrl, idToken, null);
                return reloaded.code == 200
                        && chunkDocumentMatches(new JSONObject(reloaded.text),
                                index, chunk);
            }
            return false;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean chunkDocumentMatches(JSONObject doc, int index,
                                                byte[] expected) {
        JSONObject fields = doc == null ? null : doc.optJSONObject("fields");
        if (fields == null || fields.length() != 2
                || !String.valueOf(index).equals(documentId(doc))
                || num(fields, "index", -1) != index) {
            return false;
        }
        byte[] existing = bytes(fields, "data", FILE_CHUNK_BYTES);
        return existing != null && Arrays.equals(existing, expected);
    }

    private static int readExactly(InputStream input, byte[] buffer,
                                   int expected) throws Exception {
        int total = 0;
        while (total < expected) {
            int read = input.read(buffer, total, expected - total);
            if (read < 0) {
                break;
            }
            total += read;
        }
        return total;
    }

    static boolean markRead(String idToken, String myUid, String peerUid, long readAt) {
        if (idToken == null || !validPair(myUid, peerUid) || !validTime(readAt)) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        Index own = loadIndex(idToken, myUid, chatId);
        if (own == null || !peerUid.equals(own.peerUid)) {
            return false;
        }
        if (own.lastReadAt >= readAt) {
            return true;
        }
        try {
            JSONObject fields = new JSONObject();
            putNum(fields, "lastReadAt", readAt);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("PATCH", Vault.firestoreBaseUrl()
                    + "users/" + myUid + "/chats/" + chatId
                    + "?updateMask.fieldPaths=lastReadAt", idToken, body);
            return r.code >= 200 && r.code < 300;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static List<Message> listMessages(String idToken, String myUid,
                                              String peerUid, String chatId) {
        return listMessages(idToken, myUid, peerUid, chatId, null);
    }

    private static List<Message> listMessages(String idToken, String myUid,
                                              String peerUid, String chatId,
                                              E2eeCrypto.Session session) {
        try {
            if (idToken == null || !validPair(myUid, peerUid)
                    || !chatId.equals(directChatId(myUid, peerUid))) {
                return null;
            }
            String url = Vault.firestoreBaseUrl() + "chats/" + chatId
                    + "/messages?pageSize=100&orderBy=createdAt%20desc";
            Response r = request("GET", url, idToken, null);
            if (r.code != 200) {
                return null;
            }
            List<Message> out = new ArrayList<>();
            JSONArray docs = new JSONObject(r.text).optJSONArray("documents");
            if (docs != null) {
                if (docs.length() > 100) {
                    return null;
                }
                for (int i = 0; i < docs.length(); i++) {
                    Message message = parseMessage(docs.optJSONObject(i), myUid,
                            peerUid, session);
                    if (message != null) {
                        out.add(message);
                    }
                }
            }
            Collections.sort(out, (a, b) -> {
                int byTime = Long.compare(a.createdAt, b.createdAt);
                return byTime != 0 ? byTime : a.id.compareTo(b.id);
            });
            return out;
        } catch (Exception ignored) {
            return null;
        }
    }

    /**
     * Strict VRProto envelope parser.  Firestore rules are the first
     * boundary, but the client does not trust a returned document just
     * because it came from Firestore: IDs, participant, field count,
     * text bounds, timestamps and file geometry all have to agree.
     */
    private static boolean parseReply(JSONObject fields, Message message,
                                      String myUid, String peerUid) {
        boolean present = fields.has("replyToId")
                || fields.has("replyToSenderUid")
                || fields.has("replyToType")
                || fields.has("replyToText")
                || fields.has("replyToCreatedAt");
        if (!present) {
            return true;
        }
        if (!(fields.has("replyToId") && fields.has("replyToSenderUid")
                && fields.has("replyToType") && fields.has("replyToText")
                && fields.has("replyToCreatedAt"))) {
            return false;
        }
        message.replyToId = bounded(str(fields, "replyToId", ""), 64);
        message.replyToSenderUid = bounded(
                str(fields, "replyToSenderUid", ""), 128);
        message.replyToType = bounded(str(fields, "replyToType", ""), 16);
        message.replyToText = str(fields, "replyToText", "");
        message.replyToCreatedAt = num(fields, "replyToCreatedAt", 0);
        return validReply(message, myUid, peerUid);
    }

    private static int replyFieldCount(JSONObject fields) {
        return fields.has("replyToId") ? 5 : 0;
    }

    private static Message parseMessage(JSONObject doc, String myUid, String peerUid) {
        return parseMessage(doc, myUid, peerUid, null);
    }

    private static Message parseMessage(JSONObject doc, String myUid,
                                        String peerUid,
                                        E2eeCrypto.Session session) {
        JSONObject fields = doc == null ? null : doc.optJSONObject("fields");
        if (fields == null || !validPair(myUid, peerUid)) {
            return null;
        }
        long protocolVersion = num(fields, "protocolVersion", -1);
        if (protocolVersion == E2EE_MESSAGE_VERSION) {
            return parseEncryptedMessage(doc, fields, myUid, peerUid, session);
        }
        if (protocolVersion != VERSION) {
            return null;
        }
        String id = documentId(doc);
        String clientId = bounded(str(fields, "clientId", ""), 64);
        String type = bounded(str(fields, "type", ""), 16);
        String senderUid = bounded(str(fields, "senderUid", ""), 128);
        String text = str(fields, "text", "");
        long createdAt = num(fields, "createdAt", 0);
        long editedAt = fields.has("editedAt") ? num(fields, "editedAt", 0) : 0;
        boolean participant = myUid.equals(senderUid) || peerUid.equals(senderUid);
        if (!validMessageId(id) || !id.equals(clientId) || !participant
                || text == null || text.length() == 0 || text.length() > MAX_TEXT
                || !validTime(createdAt)
                || (editedAt != 0 && (!validTime(editedAt) || editedAt < createdAt))) {
            return null;
        }
        Message message = new Message();
        message.protocolVersion = VERSION;
        message.id = id;
        message.type = type;
        message.senderUid = senderUid;
        message.text = text;
        message.createdAt = createdAt;
        message.editedAt = editedAt;
        if (TYPE_TEXT.equals(type)) {
            int expectedFields = (fields.has("editedAt") ? 7 : 6)
                    + replyFieldCount(fields);
            return fields.length() == expectedFields
                    && parseReply(fields, message, myUid, peerUid)
                    ? message : null;
        }
        if (TYPE_FILE.equals(type)) {
            int replyFields = replyFieldCount(fields);
            if (editedAt != 0
                    || (fields.length() != 12 + replyFields
                        && fields.length() != 13 + replyFields)) {
                return null;
            }
            String fileId = bounded(str(fields, "fileId", ""), 64);
            String rawName = bounded(str(fields, "fileName", ""), 255);
            String cleanName = safeFileName(rawName);
            long fileSize = num(fields, "fileSize", -1);
            long chunkSizeValue = num(fields, "chunkSize", -1);
            long chunkCountValue = num(fields, "chunkCount", -1);
            String sha256 = fields.has("fileSha256")
                    ? bounded(str(fields, "fileSha256", ""), 64) : "";
            // Files written by 1.7-1.9 used the name as the envelope
            // text but allowed names up to 255 chars.  The safe name is
            // the display/save name now; metadata is compared again
            // before any chunk is downloaded.
            if (!id.equals(fileId) || cleanName.length() == 0
                    || chunkSizeValue < 0 || chunkSizeValue > Integer.MAX_VALUE
                    || chunkCountValue < 0 || chunkCountValue > Integer.MAX_VALUE
                    || !validFileGeometry(fileSize, (int) chunkSizeValue,
                            (int) chunkCountValue)
                    || (fields.has("fileSha256") && !validSha256(sha256))) {
                return null;
            }
            message.fileId = fileId;
            message.fileName = cleanName;
            String mime = safeMimeType(bounded(
                    str(fields, "mimeType", ""), MAX_MIME_CHARS));
            message.mimeType = mime.length() > 0
                    ? mime : "application/octet-stream";
            message.fileSize = fileSize;
            message.chunkSize = (int) chunkSizeValue;
            message.chunkCount = (int) chunkCountValue;
            message.fileSha256 = sha256;
            message.plainFileSize = fileSize;
            message.plainFileSha256 = sha256;
            message.storedFileSize = fileSize;
            message.storedFileSha256 = sha256;
            return parseReply(fields, message, myUid, peerUid)
                    ? message : null;
        }
        if (TYPE_WALLPAPER.equals(type)) {
            if (editedAt != 0
                    || fields.length() != 8 + replyFieldCount(fields)
                    || !"Wallpaper suggestion".equals(text)) {
                return null;
            }
            String wallpaperId = bounded(str(fields, "wallpaperId", ""), 64);
            String ownerUid = bounded(str(fields, "wallpaperOwnerUid", ""), 128);
            if (!validWallpaperId(wallpaperId)
                    || !senderUid.equals(ownerUid)) {
                return null;
            }
            message.wallpaperId = wallpaperId;
            message.wallpaperOwnerUid = ownerUid;
            return parseReply(fields, message, myUid, peerUid)
                    ? message : null;
        }
        if (TYPE_VOICE.equals(type)) {
            int replyFields = replyFieldCount(fields);
            long durationValue = num(fields, "durationMs", -1);
            long fileSize = num(fields, "fileSize", -1);
            long chunkSizeValue = num(fields, "chunkSize", -1);
            long chunkCountValue = num(fields, "chunkCount", -1);
            String fileId = bounded(str(fields, "fileId", ""), 64);
            String rawName = bounded(str(fields, "fileName", ""), 255);
            String mime = bounded(str(fields, "mimeType", ""), MAX_MIME_CHARS);
            String sha256 = bounded(str(fields, "fileSha256", ""), 64);
            if (editedAt != 0 || fields.length() != 14 + replyFields
                    || !VOICE_TEXT.equals(text)
                    || !id.equals(fileId)
                    || !VOICE_FILE_NAME.equals(rawName)
                    || !VOICE_MIME_TYPE.equals(mime)
                    || durationValue < 0 || durationValue > Integer.MAX_VALUE
                    || chunkSizeValue < 0 || chunkSizeValue > Integer.MAX_VALUE
                    || chunkCountValue < 0 || chunkCountValue > Integer.MAX_VALUE
                    || !validSha256(sha256)
                    || !validVoiceGeometry(fileSize, (int) chunkSizeValue,
                            (int) chunkCountValue, (int) durationValue)) {
                return null;
            }
            message.fileId = fileId;
            message.fileName = VOICE_FILE_NAME;
            message.mimeType = VOICE_MIME_TYPE;
            message.fileSize = fileSize;
            message.chunkSize = (int) chunkSizeValue;
            message.chunkCount = (int) chunkCountValue;
            message.fileSha256 = sha256;
            message.plainFileSize = fileSize;
            message.plainFileSha256 = sha256;
            message.storedFileSize = fileSize;
            message.storedFileSha256 = sha256;
            message.durationMs = (int) durationValue;
            return parseReply(fields, message, myUid, peerUid)
                    ? message : null;
        }
        return null;
    }


    private static Message parseEncryptedMessage(JSONObject doc, JSONObject fields,
                                                 String myUid, String peerUid,
                                                 E2eeCrypto.Session session) {
        String id = documentId(doc);
        String clientId = str(fields, "clientId", "");
        String type = str(fields, "type", "");
        String senderUid = str(fields, "senderUid", "");
        String text = str(fields, "text", "");
        long createdAt = num(fields, "createdAt", 0);
        long editedAt = fields.has("editedAt") ? num(fields, "editedAt", 0) : 0;
        boolean hasFile = fields.has("fileId");
        boolean hasWallpaper = fields.has("wallpaperId");
        if (hasFile && !(fields.has("fileName") && fields.has("mimeType")
                && fields.has("fileSize") && fields.has("chunkCount")
                && fields.has("chunkSize") && fields.has("fileSha256"))) {
            return null;
        }
        if (hasWallpaper && !fields.has("wallpaperOwnerUid")) {
            return null;
        }
        int expectedFields = 15 + (fields.has("editedAt") ? 1 : 0)
                + (hasFile ? 7 : 0) + (hasWallpaper ? 2 : 0);
        boolean participant = myUid.equals(senderUid) || peerUid.equals(senderUid);
        if (!validMessageId(id) || !id.equals(clientId) || !participant
                || !TYPE_ENCRYPTED.equals(type) || !ENCRYPTED_TEXT.equals(text)
                || num(fields, "e2eeVersion", -1) != E2eeCrypto.VERSION
                || !validTime(createdAt)
                || (editedAt != 0 && (!validTime(editedAt) || editedAt < createdAt
                        || hasFile || hasWallpaper))
                || (hasFile && hasWallpaper)
                || fields.length() != expectedFields) {
            return null;
        }
        E2eeCrypto.Envelope envelope = new E2eeCrypto.Envelope();
        envelope.senderFingerprint = str(fields, "senderIdentityFingerprint", "");
        envelope.recipientPreKeyId = str(fields, "recipientPreKeyId", "");
        envelope.selfPreKeyId = str(fields, "selfPreKeyId", "");
        envelope.ephemeralPublic = bytes(fields, "ephemeralKey", 256);
        envelope.recipientWrappedKey = bytes(fields, "recipientWrappedKey",
                E2eeCrypto.WRAPPED_KEY_BYTES);
        envelope.selfWrappedKey = bytes(fields, "selfWrappedKey",
                E2eeCrypto.WRAPPED_KEY_BYTES);
        envelope.ciphertext = bytes(fields, "ciphertext",
                E2eeCrypto.MAX_CIPHERTEXT_BYTES);
        envelope.signature = bytes(fields, "signature", 128);
        if (!E2eeCrypto.validEnvelope(envelope)) {
            return null;
        }
        Message message = new Message();
        message.protocolVersion = E2EE_MESSAGE_VERSION;
        message.e2eeVersion = E2eeCrypto.VERSION;
        message.encrypted = true;
        message.decryptionFailed = true;
        message.e2eeEnvelope = envelope;
        message.id = id;
        message.type = TYPE_ENCRYPTED;
        message.senderUid = senderUid;
        message.text = ENCRYPTED_TEXT;
        message.createdAt = createdAt;
        message.editedAt = editedAt;
        if (hasFile) {
            String fileId = str(fields, "fileId", "");
            String fileName = str(fields, "fileName", "");
            String mimeType = str(fields, "mimeType", "");
            long storedSize = num(fields, "fileSize", -1);
            long chunkSizeValue = num(fields, "chunkSize", -1);
            long chunkCountValue = num(fields, "chunkCount", -1);
            String storedSha = str(fields, "fileSha256", "");
            if (!id.equals(fileId) || !ENCRYPTED_FILE_NAME.equals(fileName)
                    || !ENCRYPTED_MIME_TYPE.equals(mimeType)
                    || chunkSizeValue < 0 || chunkSizeValue > Integer.MAX_VALUE
                    || chunkCountValue < 0 || chunkCountValue > Integer.MAX_VALUE
                    || !validSha256(storedSha)
                    || !validStoredFileGeometry(storedSize,
                            (int) chunkSizeValue, (int) chunkCountValue,
                            MAX_FILE_BYTES)) {
                return null;
            }
            message.fileId = fileId;
            message.fileName = ENCRYPTED_FILE_NAME;
            message.mimeType = ENCRYPTED_MIME_TYPE;
            message.fileSize = storedSize;
            message.storedFileSize = storedSize;
            message.chunkSize = (int) chunkSizeValue;
            message.chunkCount = (int) chunkCountValue;
            message.fileSha256 = storedSha;
            message.storedFileSha256 = storedSha;
        }
        if (hasWallpaper) {
            String wallpaperId = str(fields, "wallpaperId", "");
            String ownerUid = str(fields, "wallpaperOwnerUid", "");
            if (!validWallpaperId(wallpaperId) || !senderUid.equals(ownerUid)) {
                return null;
            }
            message.wallpaperId = wallpaperId;
            message.wallpaperOwnerUid = ownerUid;
        }
        decryptMessageInPlace(message, myUid, peerUid, session);
        return message;
    }

    private static boolean messageDocumentMatches(JSONObject doc, String myUid,
                                                  String peerUid, Message expected) {
        Message actual = parseMessage(doc, myUid, peerUid);
        if (actual == null || expected == null
                || !expected.id.equals(actual.id)
                || !myUid.equals(actual.senderUid)
                || expected.createdAt != actual.createdAt) {
            return false;
        }
        if (expected.encrypted) {
            boolean expectedAttachment = TYPE_FILE.equals(expected.type)
                    || TYPE_VOICE.equals(expected.type);
            boolean expectedWallpaper = TYPE_WALLPAPER.equals(expected.type);
            boolean outerMatches = actual.encrypted
                    && expectedAttachment == hasAttachment(actual)
                    && expectedWallpaper == validWallpaperId(actual.wallpaperId)
                    && (!expectedAttachment
                            || (expected.fileId.equals(actual.fileId)
                                    && expectedStoredFileSize(expected)
                                            == actual.storedFileSize
                                    && expected.chunkSize == actual.chunkSize
                                    && expected.chunkCount == actual.chunkCount
                                    && expectedStoredFileSha256(expected)
                                            .equals(actual.storedFileSha256)))
                    && (!expectedWallpaper
                            || (expected.wallpaperId.equals(actual.wallpaperId)
                                    && expected.wallpaperOwnerUid.equals(
                                            actual.wallpaperOwnerUid)));
            // An edited text envelope intentionally has fresh keys and
            // ciphertext.  It is still the same idempotent message ID.
            return outerMatches && (actual.editedAt > 0
                    || sameEnvelope(expected.e2eeEnvelope, actual.e2eeEnvelope));
        }
        if (!expected.type.equals(actual.type)) {
            return false;
        }
        if (TYPE_TEXT.equals(expected.type)) {
            // A retry may race with an edit made on another device; an
            // edited envelope is still the same message.  Unedited text
            // must match exactly.
            return (actual.editedAt > 0 || expected.text.equals(actual.text))
                    && sameReply(expected, actual);
        }
        if (TYPE_FILE.equals(expected.type)) {
            String expectedMime = safeMimeType(expected.mimeType);
            if (expectedMime.length() == 0) {
                expectedMime = "application/octet-stream";
            }
            return expected.fileId.equals(actual.fileId)
                    && safeFileName(expected.fileName).equals(actual.fileName)
                    && expectedMime.equals(actual.mimeType)
                    && expected.fileSize == actual.fileSize
                    && expected.chunkSize == actual.chunkSize
                    && expected.chunkCount == actual.chunkCount
                    && (expected.fileSha256 == null ? "" : expected.fileSha256)
                            .equals(actual.fileSha256)
                    && sameReply(expected, actual);
        }
        if (TYPE_VOICE.equals(expected.type)) {
            return expected.fileId.equals(actual.fileId)
                    && VOICE_FILE_NAME.equals(actual.fileName)
                    && VOICE_MIME_TYPE.equals(actual.mimeType)
                    && expected.fileSize == actual.fileSize
                    && expected.chunkSize == actual.chunkSize
                    && expected.chunkCount == actual.chunkCount
                    && expected.durationMs == actual.durationMs
                    && (expected.fileSha256 == null ? "" : expected.fileSha256)
                            .equals(actual.fileSha256)
                    && sameReply(expected, actual);
        }
        return TYPE_WALLPAPER.equals(expected.type)
                && expected.wallpaperId.equals(actual.wallpaperId)
                && expected.wallpaperOwnerUid.equals(actual.wallpaperOwnerUid)
                && actual.text.equals("Wallpaper suggestion")
                && sameReply(expected, actual);
    }

    private static boolean sameReply(Message expected, Message actual) {
        return (expected.replyToId == null ? "" : expected.replyToId)
                        .equals(actual.replyToId == null ? "" : actual.replyToId)
                && (expected.replyToSenderUid == null
                        ? "" : expected.replyToSenderUid).equals(
                                actual.replyToSenderUid == null
                                        ? "" : actual.replyToSenderUid)
                && (expected.replyToType == null ? "" : expected.replyToType)
                        .equals(actual.replyToType == null
                                ? "" : actual.replyToType)
                && (expected.replyToText == null ? "" : expected.replyToText)
                        .equals(actual.replyToText == null
                                ? "" : actual.replyToText)
                && expected.replyToCreatedAt == actual.replyToCreatedAt;
    }

    private static boolean ensureDirectChat(String idToken, String myUid,
                                            String peerUid, String chatId, long now) {
        try {
            if (idToken == null || !validPair(myUid, peerUid)
                    || !chatId.equals(directChatId(myUid, peerUid))
                    || !validTime(now)) {
                return false;
            }
            Response existing = request("GET",
                    Vault.firestoreBaseUrl() + "chats/" + chatId, idToken, null);
            if (existing.code == 200 && !validDirectChatDocument(
                    new JSONObject(existing.text), chatId, myUid, peerUid)) {
                return false;
            }
            if (existing.code != 200) {
                List<String> participants = new ArrayList<>();
                participants.add(myUid);
                participants.add(peerUid);
                Collections.sort(participants);
                JSONObject fields = new JSONObject();
                putStringArray(fields, "participants", participants);
                putStr(fields, "type", "direct");
                putStr(fields, "protocol", "VRProto/1");
                putNum(fields, "createdAt", now);
                putNum(fields, "updatedAt", now);
                putStr(fields, "lastMessageText", "");
                putNum(fields, "lastMessageAt", 0);
                putStr(fields, "lastSenderUid", "");
                putStr(fields, "lastMessageId", "");
                JSONObject body = new JSONObject();
                body.put("fields", fields);
                Response created = request("POST", Vault.firestoreBaseUrl()
                        + "chats?documentId=" + chatId, idToken, body);
                boolean ok = created.code >= 200 && created.code < 300;
                if (!ok && (created.code == 409 || created.code == 400)) {
                    Response check = request("GET",
                            Vault.firestoreBaseUrl() + "chats/" + chatId, idToken, null);
                    ok = check.code == 200 && validDirectChatDocument(
                            new JSONObject(check.text), chatId, myUid, peerUid);
                }
                if (!ok) {
                    return false;
                }
            }
            return ensureIndex(idToken, myUid, peerUid, chatId, now)
                    && ensureIndex(idToken, peerUid, myUid, chatId, now);
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean validDirectChatDocument(JSONObject doc, String chatId,
                                                   String myUid, String peerUid) {
        JSONObject fields = doc == null ? null : doc.optJSONObject("fields");
        if (fields == null || fields.length() != 9
                || !chatId.equals(documentId(doc))
                || !"direct".equals(bounded(str(fields, "type", ""), 16))
                || !"VRProto/1".equals(bounded(str(fields, "protocol", ""), 32))) {
            return false;
        }
        JSONObject participantsValue = fields.optJSONObject("participants");
        JSONObject arrayValue = participantsValue == null ? null
                : participantsValue.optJSONObject("arrayValue");
        JSONArray values = arrayValue == null ? null : arrayValue.optJSONArray("values");
        if (values == null || values.length() != 2) {
            return false;
        }
        List<String> expected = new ArrayList<>();
        expected.add(myUid);
        expected.add(peerUid);
        Collections.sort(expected);
        for (int i = 0; i < values.length(); i++) {
            JSONObject item = values.optJSONObject(i);
            Object raw = item == null ? null : item.opt("stringValue");
            if (!(raw instanceof String) || !expected.get(i).equals(raw)) {
                return false;
            }
        }
        long createdAt = num(fields, "createdAt", 0);
        long updatedAt = num(fields, "updatedAt", 0);
        long lastMessageAt = num(fields, "lastMessageAt", -1);
        String lastText = str(fields, "lastMessageText", "");
        String lastSenderUid = bounded(str(fields, "lastSenderUid", ""), 128);
        String lastMessageId = bounded(str(fields, "lastMessageId", ""), 64);
        if (!validTime(createdAt) || !validTime(updatedAt)
                || updatedAt < createdAt
                || lastText == null || lastText.length() > MAX_TEXT
                || (lastMessageAt != 0 && !validTime(lastMessageAt))) {
            return false;
        }
        if (lastMessageAt == 0) {
            return lastText.length() == 0 && lastSenderUid.length() == 0
                    && lastMessageId.length() == 0;
        }
        return lastMessageAt >= createdAt && lastText.length() > 0
                && (myUid.equals(lastSenderUid) || peerUid.equals(lastSenderUid))
                && validMessageId(lastMessageId);
    }

    private static boolean ensureIndex(String idToken, String ownerUid,
                                       String peerUid, String chatId, long now) {
        Index existingIndex = loadIndex(idToken, ownerUid, chatId);
        if (existingIndex != null) {
            return peerUid.equals(existingIndex.peerUid);
        }
        if (idToken == null || !validPair(ownerUid, peerUid)
                || !chatId.equals(directChatId(ownerUid, peerUid))
                || !validTime(now)) {
            return false;
        }
        try {
            JSONObject fields = indexFields(chatId, peerUid, now, "", 0, "", "", 0);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("POST", Vault.firestoreBaseUrl()
                    + "users/" + ownerUid + "/chats?documentId=" + chatId,
                    idToken, body);
            if (r.code >= 200 && r.code < 300) {
                return true;
            }
            if (r.code == 409 || r.code == 400) {
                Index reloaded = loadIndex(idToken, ownerUid, chatId);
                return reloaded != null && peerUid.equals(reloaded.peerUid);
            }
            return false;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static Index loadIndex(String idToken, String ownerUid, String chatId) {
        try {
            if (idToken == null || !validUid(ownerUid) || !validChatId(chatId)) {
                return null;
            }
            Response r = request("GET", Vault.firestoreBaseUrl()
                    + "users/" + ownerUid + "/chats/" + chatId, idToken, null);
            if (r.code != 200) {
                return null;
            }
            Index index = parseIndex(new JSONObject(r.text));
            if (index == null || !chatId.equals(index.chatId)
                    || !chatId.equals(directChatId(ownerUid, index.peerUid))
                    || !(index.lastSenderUid.length() == 0
                            || ownerUid.equals(index.lastSenderUid)
                            || index.peerUid.equals(index.lastSenderUid))
                    || !(index.wallpaperOwnerUid.length() == 0
                            || ownerUid.equals(index.wallpaperOwnerUid)
                            || index.peerUid.equals(index.wallpaperOwnerUid))) {
                return null;
            }
            return index;
        } catch (Exception ignored) {
            return null;
        }
    }

    private static Index parseIndex(JSONObject doc) {
        if (doc == null) {
            return null;
        }
        JSONObject fields = doc.optJSONObject("fields");
        if (fields == null || (fields.length() != 8 && fields.length() != 11)) {
            return null;
        }
        Index index = new Index();
        index.chatId = bounded(str(fields, "chatId", ""), 80);
        index.peerUid = bounded(str(fields, "peerUid", ""), 128);
        index.lastText = str(fields, "lastMessageText", "");
        index.lastSenderUid = bounded(str(fields, "lastSenderUid", ""), 128);
        index.lastMessageId = bounded(str(fields, "lastMessageId", ""), 64);
        index.lastMessageAt = num(fields, "lastMessageAt", -1);
        index.updatedAt = num(fields, "updatedAt", -1);
        index.lastReadAt = num(fields, "lastReadAt", -1);
        index.wallpaperId = bounded(str(fields, "wallpaperId", ""), 64);
        index.wallpaperOwnerUid = bounded(
                str(fields, "wallpaperOwnerUid", ""), 128);
        index.wallpaperUpdatedAt = num(fields, "wallpaperUpdatedAt", 0);
        boolean noMessage = index.lastMessageAt == 0;
        if (!validChatId(index.chatId)
                || !index.chatId.equals(documentId(doc))
                || !validUid(index.peerUid)
                || index.lastText == null || index.lastText.length() > MAX_TEXT
                || !(index.lastSenderUid.length() == 0
                        || validUid(index.lastSenderUid))
                || !(index.lastMessageId.length() == 0
                        || validMessageId(index.lastMessageId))
                || (index.lastMessageAt != 0 && !validTime(index.lastMessageAt))
                || (index.updatedAt != 0 && !validTime(index.updatedAt))
                || index.updatedAt < 0
                || (index.lastReadAt != 0 && !validTime(index.lastReadAt))
                || index.lastReadAt < 0
                || (index.wallpaperUpdatedAt != 0
                        && !validTime(index.wallpaperUpdatedAt))
                || index.wallpaperUpdatedAt < 0) {
            return null;
        }
        if (noMessage) {
            if (index.lastText.length() > 0 || index.lastSenderUid.length() > 0
                    || index.lastMessageId.length() > 0) {
                return null;
            }
        } else if (index.lastText.length() == 0
                || index.lastSenderUid.length() == 0
                || index.lastMessageId.length() == 0
                || index.updatedAt == 0) {
            return null;
        }
        boolean noWallpaper = index.wallpaperId.length() == 0
                && index.wallpaperOwnerUid.length() == 0;
        if (!noWallpaper && (!validWallpaperId(index.wallpaperId)
                || !validUid(index.wallpaperOwnerUid)
                || index.wallpaperUpdatedAt == 0)) {
            return null;
        }
        return index;
    }

    private static JSONObject indexFields(String chatId, String peerUid,
                                          long updatedAt, String text,
                                          long messageAt, String senderUid,
                                          String messageId,
                                          long readAt) throws Exception {
        JSONObject fields = new JSONObject();
        putStr(fields, "chatId", chatId);
        putStr(fields, "peerUid", peerUid);
        putNum(fields, "updatedAt", updatedAt);
        putStr(fields, "lastMessageText", text);
        putNum(fields, "lastMessageAt", messageAt);
        putStr(fields, "lastSenderUid", senderUid);
        putStr(fields, "lastMessageId", messageId);
        putNum(fields, "lastReadAt", readAt);
        return fields;
    }

    private static void patchChatAfterSend(String idToken, String chatId,
                                           String senderUid, Message message) {
        try {
            if (idToken == null || !validChatId(chatId) || !validUid(senderUid)
                    || message == null || !validMessageId(message.id)
                    || !senderUid.equals(message.senderUid)
                    || message.text == null || message.text.length() > MAX_TEXT
                    || !validTime(message.createdAt)) {
                return;
            }
            JSONObject fields = new JSONObject();
            putNum(fields, "updatedAt", message.createdAt);
            putStr(fields, "lastMessageText", cloudPreviewText(message));
            putNum(fields, "lastMessageAt", message.createdAt);
            putStr(fields, "lastSenderUid", senderUid);
            putStr(fields, "lastMessageId", message.id);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            String url = Vault.firestoreBaseUrl() + "chats/" + chatId
                    + "?updateMask.fieldPaths=updatedAt"
                    + "&updateMask.fieldPaths=lastMessageText"
                    + "&updateMask.fieldPaths=lastMessageAt"
                    + "&updateMask.fieldPaths=lastSenderUid"
                    + "&updateMask.fieldPaths=lastMessageId";
            request("PATCH", url, idToken, body);
        } catch (Exception ignored) {
        }
    }

    private static void patchIndexAfterSend(String idToken, String ownerUid,
                                            String peerUid, String chatId,
                                            String senderUid, Message message) {
        try {
            if (idToken == null || !validPair(ownerUid, peerUid)
                    || !chatId.equals(directChatId(ownerUid, peerUid))
                    || message == null || !validMessageId(message.id)
                    || !senderUid.equals(message.senderUid)
                    || !(ownerUid.equals(senderUid) || peerUid.equals(senderUid))
                    || message.text == null || message.text.length() > MAX_TEXT
                    || !validTime(message.createdAt)) {
                return;
            }
            Index existing = loadIndex(idToken, ownerUid, chatId);
            long readAt = ownerUid.equals(senderUid) ? message.createdAt
                    : existing == null ? 0 : existing.lastReadAt;
            JSONObject fields = indexFields(chatId, peerUid, message.createdAt,
                    cloudPreviewText(message), message.createdAt, senderUid,
                    message.id, readAt);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            String url = Vault.firestoreBaseUrl() + "users/" + ownerUid
                    + "/chats/" + chatId
                    + "?updateMask.fieldPaths=chatId"
                    + "&updateMask.fieldPaths=peerUid"
                    + "&updateMask.fieldPaths=updatedAt"
                    + "&updateMask.fieldPaths=lastMessageText"
                    + "&updateMask.fieldPaths=lastMessageAt"
                    + "&updateMask.fieldPaths=lastSenderUid"
                    + "&updateMask.fieldPaths=lastMessageId"
                    + "&updateMask.fieldPaths=lastReadAt";
            request("PATCH", url, idToken, body);
        } catch (Exception ignored) {
        }
    }

    private static void patchChatAfterEdit(String idToken, String chatId,
                                             String senderUid, Message message,
                                             long editedAt) {
        try {
            if (idToken == null || !validChatId(chatId) || !validUid(senderUid)
                    || message == null || !validMessageId(message.id)
                    || !senderUid.equals(message.senderUid)
                    || message.text == null || message.text.length() > MAX_TEXT
                    || !validTime(message.createdAt) || !validTime(editedAt)) {
                return;
            }
            JSONObject fields = new JSONObject();
            putNum(fields, "updatedAt", editedAt);
            putStr(fields, "lastMessageText", cloudPreviewText(message));
            putNum(fields, "lastMessageAt", message.createdAt);
            putStr(fields, "lastSenderUid", senderUid);
            putStr(fields, "lastMessageId", message.id);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            String url = Vault.firestoreBaseUrl() + "chats/" + chatId
                    + "?updateMask.fieldPaths=updatedAt"
                    + "&updateMask.fieldPaths=lastMessageText"
                    + "&updateMask.fieldPaths=lastMessageAt"
                    + "&updateMask.fieldPaths=lastSenderUid"
                    + "&updateMask.fieldPaths=lastMessageId";
            request("PATCH", url, idToken, body);
        } catch (Exception ignored) {
        }
    }

    private static void patchIndexAfterEdit(String idToken, String ownerUid,
                                              String peerUid, String chatId,
                                              Message message) {
        try {
            if (idToken == null || !validPair(ownerUid, peerUid)
                    || !chatId.equals(directChatId(ownerUid, peerUid))
                    || message == null || !validMessageId(message.id)
                    || !(ownerUid.equals(message.senderUid)
                            || peerUid.equals(message.senderUid))
                    || message.text == null || message.text.length() > MAX_TEXT
                    || !validTime(message.createdAt)) {
                return;
            }
            Index existing = loadIndex(idToken, ownerUid, chatId);
            if (existing == null) {
                return;
            }
            JSONObject fields = indexFields(chatId, peerUid, message.createdAt,
                    cloudPreviewText(message), message.createdAt,
                    message.senderUid, message.id, existing.lastReadAt);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            String url = Vault.firestoreBaseUrl() + "users/" + ownerUid
                    + "/chats/" + chatId
                    + "?updateMask.fieldPaths=chatId"
                    + "&updateMask.fieldPaths=peerUid"
                    + "&updateMask.fieldPaths=updatedAt"
                    + "&updateMask.fieldPaths=lastMessageText"
                    + "&updateMask.fieldPaths=lastMessageAt"
                    + "&updateMask.fieldPaths=lastSenderUid"
                    + "&updateMask.fieldPaths=lastMessageId"
                    + "&updateMask.fieldPaths=lastReadAt";
            request("PATCH", url, idToken, body);
        } catch (Exception ignored) {
        }
    }

    private static void patchChatAfterDelete(String idToken, String chatId,
                                               Message latest, long operationAt) {
        try {
            if (idToken == null || !validChatId(chatId) || !validTime(operationAt)
                    || (latest != null && (!validMessageId(latest.id)
                            || !validUid(latest.senderUid)
                            || latest.text == null
                            || latest.text.length() > MAX_TEXT
                            || !validTime(latest.createdAt)))) {
                return;
            }
            JSONObject fields = new JSONObject();
            putNum(fields, "updatedAt", operationAt);
            putStr(fields, "lastMessageText", latest == null ? "" : cloudPreviewText(latest));
            putNum(fields, "lastMessageAt", latest == null ? 0 : latest.createdAt);
            putStr(fields, "lastSenderUid", latest == null ? "" : latest.senderUid);
            putStr(fields, "lastMessageId", latest == null ? "" : latest.id);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            String url = Vault.firestoreBaseUrl() + "chats/" + chatId
                    + "?updateMask.fieldPaths=updatedAt"
                    + "&updateMask.fieldPaths=lastMessageText"
                    + "&updateMask.fieldPaths=lastMessageAt"
                    + "&updateMask.fieldPaths=lastSenderUid"
                    + "&updateMask.fieldPaths=lastMessageId";
            request("PATCH", url, idToken, body);
        } catch (Exception ignored) {
        }
    }

    private static void patchIndexAfterDelete(String idToken, String ownerUid,
                                                String peerUid, String chatId,
                                                Message latest) {
        try {
            if (idToken == null || !validPair(ownerUid, peerUid)
                    || !chatId.equals(directChatId(ownerUid, peerUid))
                    || (latest != null && (!validMessageId(latest.id)
                            || !(ownerUid.equals(latest.senderUid)
                                    || peerUid.equals(latest.senderUid))
                            || latest.text == null
                            || latest.text.length() > MAX_TEXT
                            || !validTime(latest.createdAt)))) {
                return;
            }
            Index existing = loadIndex(idToken, ownerUid, chatId);
            if (existing == null) {
                return;
            }
            long at = latest == null ? 0 : latest.createdAt;
            JSONObject fields = indexFields(chatId, peerUid, at,
                    latest == null ? "" : cloudPreviewText(latest), at,
                    latest == null ? "" : latest.senderUid,
                    latest == null ? "" : latest.id, existing.lastReadAt);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            String url = Vault.firestoreBaseUrl() + "users/" + ownerUid
                    + "/chats/" + chatId
                    + "?updateMask.fieldPaths=chatId"
                    + "&updateMask.fieldPaths=peerUid"
                    + "&updateMask.fieldPaths=updatedAt"
                    + "&updateMask.fieldPaths=lastMessageText"
                    + "&updateMask.fieldPaths=lastMessageAt"
                    + "&updateMask.fieldPaths=lastSenderUid"
                    + "&updateMask.fieldPaths=lastMessageId"
                    + "&updateMask.fieldPaths=lastReadAt";
            request("PATCH", url, idToken, body);
        } catch (Exception ignored) {
        }
    }

    private static String documentId(JSONObject doc) {
        if (doc == null) {
            return "";
        }
        Object raw = doc.opt("name");
        if (!(raw instanceof String)) {
            return "";
        }
        String name = (String) raw;
        if (name.length() > 1024) {
            return "";
        }
        int slash = name.lastIndexOf('/');
        return slash >= 0 ? name.substring(slash + 1) : "";
    }

    private static void putStr(JSONObject fields, String name, String value)
            throws Exception {
        JSONObject v = new JSONObject();
        v.put("stringValue", value == null ? "" : value);
        fields.put(name, v);
    }

    private static void putBool(JSONObject fields, String name, boolean value)
            throws Exception {
        JSONObject v = new JSONObject();
        v.put("booleanValue", value);
        fields.put(name, v);
    }

    private static void putBytes(JSONObject fields, String name, byte[] value)
            throws Exception {
        JSONObject v = new JSONObject();
        v.put("bytesValue", Base64.encodeToString(value, Base64.NO_WRAP));
        fields.put(name, v);
    }

    private static byte[] bytes(JSONObject fields, String name, int maxBytes) {
        JSONObject v = fields.optJSONObject(name);
        if (v == null || maxBytes < 0) {
            return null;
        }
        Object raw = v.opt("bytesValue");
        if (!(raw instanceof String)) {
            return null;
        }
        String encoded = (String) raw;
        int maxEncoded = ((maxBytes + 2) / 3) * 4 + 4;
        if (encoded.length() == 0 || encoded.length() > maxEncoded) {
            return null;
        }
        try {
            byte[] decoded = Base64.decode(encoded, Base64.DEFAULT);
            return decoded.length <= maxBytes ? decoded : null;
        } catch (Exception ignored) {
            return null;
        }
    }

    private static void putNum(JSONObject fields, String name, long value)
            throws Exception {
        JSONObject v = new JSONObject();
        v.put("integerValue", String.valueOf(value));
        fields.put(name, v);
    }

    private static void putStringArray(JSONObject fields, String name,
                                       List<String> values) throws Exception {
        JSONArray array = new JSONArray();
        for (String value : values) {
            JSONObject item = new JSONObject();
            item.put("stringValue", value);
            array.put(item);
        }
        JSONObject arrayValue = new JSONObject();
        arrayValue.put("values", array);
        JSONObject v = new JSONObject();
        v.put("arrayValue", arrayValue);
        fields.put(name, v);
    }

    private static String str(JSONObject fields, String name, String fallback) {
        JSONObject v = fields.optJSONObject(name);
        if (v == null) {
            return fallback;
        }
        Object raw = v.opt("stringValue");
        return raw instanceof String ? (String) raw : fallback;
    }

    private static boolean bool(JSONObject fields, String name, boolean fallback) {
        JSONObject v = fields.optJSONObject(name);
        if (v == null) {
            return fallback;
        }
        Object raw = v.opt("booleanValue");
        return raw instanceof Boolean ? (Boolean) raw : fallback;
    }

    private static long num(JSONObject fields, String name, long fallback) {
        JSONObject v = fields.optJSONObject(name);
        if (v == null) {
            return fallback;
        }
        try {
            Object raw = v.opt("integerValue");
            if (raw instanceof String) {
                return Long.parseLong((String) raw);
            }
            return raw instanceof Number ? ((Number) raw).longValue() : fallback;
        } catch (Exception ignored) {
            return fallback;
        }
    }

    private static Response request(String method, String url, String idToken,
                                    JSONObject body) throws Exception {
        HttpURLConnection c = SafeHttp.open(url);
        try {
            c.setRequestMethod(method);
            c.setRequestProperty("Content-Type", "application/json");
            AuthClient.applyAndroidHeaders(c);
            if (idToken != null) {
                c.setRequestProperty("Authorization", "Bearer " + idToken);
            }
            if (body != null) {
                c.setDoOutput(true);
                OutputStream out = c.getOutputStream();
                out.write(body.toString().getBytes(StandardCharsets.UTF_8));
                out.close();
            }
            Response r = new Response();
            r.code = c.getResponseCode();
            InputStream stream = r.code >= 200 && r.code < 300
                    ? c.getInputStream() : c.getErrorStream();
            r.text = SafeHttp.readText(stream, MAX_FIRESTORE_RESPONSE_BYTES);
            return r;
        } finally {
            c.disconnect();
        }
    }
}
