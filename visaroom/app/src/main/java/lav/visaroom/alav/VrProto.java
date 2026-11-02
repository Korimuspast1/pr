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
 * VRProto 1: visaroom's cloud-chat protocol layer.
 *
 * Messages are never written to local storage. They live only in
 * Firestore at chats/{chatId}/messages/{clientId}; per-account cloud
 * indexes at users/{uid}/chats/{chatId} make the same chat list and
 * read state appear after sign-in on any device. The deterministic
 * chat ID, versioned message envelope and client message IDs make
 * synchronization idempotent when the same request is retried.
 */
final class VrProto {

    static final int VERSION = 1;
    static final int MAX_TEXT = 4000;

    static final String TYPE_TEXT = "text";
    static final String TYPE_FILE = "file";
    static final String TYPE_WALLPAPER = "wallpaper";

    // Files of any MIME type are split into Firestore byte chunks. This
    // keeps visaroom on the free Firestore-only backend (no Storage/Blaze)
    // while still allowing useful attachments and explicit downloads.
    static final long MAX_FILE_BYTES = 25L * 1024L * 1024L;
    static final int FILE_CHUNK_BYTES = 600 * 1024;
    static final int MAX_WALLPAPER_BYTES = 700 * 1024;
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
        // Empty only for legacy 1.7-1.9 file messages.
        String fileSha256 = "";

        // Wallpaper suggestion envelope.
        String wallpaperId = "";
        String wallpaperOwnerUid = "";

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
    }

    static final class FileStatus {
        boolean ok;
        boolean exists;
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

    private static int expectedChunkLength(long size, int chunkSize, int count, int index) {
        if (!validFileGeometry(size, chunkSize, count)
                || index < 0 || index >= count) {
            return -1;
        }
        long remaining = size - (long) index * chunkSize;
        return (int) Math.min(chunkSize, remaining);
    }

    private static String sha256Hex(MessageDigest digest) {
        byte[] value = digest.digest();
        StringBuilder out = new StringBuilder(value.length * 2);
        for (byte b : value) {
            out.append(String.format("%02x", b & 0xff));
        }
        return out.toString();
    }

    static List<ChatSummary> listChats(String idToken, String myUid) {
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
                ProfileClient.PublicProfile profile =
                        ProfileClient.loadPublicProfile(idToken, index.peerUid);
                summary.username = profile.username;
                summary.publicId = profile.publicId;
                summary.verified = profile.verified;
                summary.blocked = profile.blocked;
                out.add(summary);
            }
            Collections.sort(out, (a, b) -> Long.compare(b.updatedAt, a.updatedAt));
            return out;
        } catch (Exception ignored) {
            return null;
        }
    }

    static SyncResult syncDirect(String idToken, String myUid, String peerUid) {
        SyncResult result = new SyncResult();
        if (idToken == null || !validPair(myUid, peerUid)) {
            return result;
        }
        String chatId = directChatId(myUid, peerUid);
        Index own = loadIndex(idToken, myUid, chatId);
        List<Message> messages = listMessages(idToken, myUid, peerUid, chatId);
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

    static boolean sendText(String idToken, String myUid, String peerUid, Message message) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || !validMessageId(message.id)
                || !myUid.equals(message.senderUid)
                || !validTime(message.createdAt)
                || message.text == null || message.text.trim().length() == 0
                || message.text.length() > MAX_TEXT) {
            return false;
        }
        message.type = TYPE_TEXT;
        String chatId = directChatId(myUid, peerUid);
        if (!ensureDirectChat(idToken, myUid, peerUid, chatId, message.createdAt)) {
            return false;
        }
        return createMessageAndPatch(idToken, myUid, peerUid, chatId, message);
    }

    static boolean editText(String idToken, String myUid, String peerUid,
                            Message message, String newText, long editedAt) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || newText == null || newText.trim().length() == 0
                || newText.length() > MAX_TEXT || !validTime(editedAt)
                || !TYPE_TEXT.equals(message.type)
                || !myUid.equals(message.senderUid)
                || !validMessageId(message.id)) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        List<Message> before = listMessages(idToken, myUid, peerUid, chatId);
        Message current = messageById(before, message.id);
        if (current == null || !TYPE_TEXT.equals(current.type)
                || !myUid.equals(current.senderUid)) {
            return false;
        }
        boolean wasLatest = isLatest(before, current.id);
        try {
            JSONObject fields = new JSONObject();
            putStr(fields, "text", newText);
            putNum(fields, "editedAt", editedAt);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("PATCH", Vault.firestoreBaseUrl()
                    + "chats/" + chatId + "/messages/" + message.id
                    + "?updateMask.fieldPaths=text"
                    + "&updateMask.fieldPaths=editedAt", idToken, body);
            boolean ok = r.code >= 200 && r.code < 300;
            if (!ok) {
                Message reloaded = messageById(
                        listMessages(idToken, myUid, peerUid, chatId), message.id);
                ok = reloaded != null && newText.equals(reloaded.text)
                        && reloaded.editedAt == editedAt;
                if (!ok) {
                    return false;
                }
                current = reloaded;
            }
            current.text = newText;
            current.editedAt = editedAt;
            message.text = newText;
            message.editedAt = editedAt;
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

    static boolean deleteMessage(String idToken, String myUid, String peerUid,
                                 Message message, long operationAt) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || !validMessageId(message.id) || !validTime(operationAt)
                || !myUid.equals(message.senderUid)) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        List<Message> before = listMessages(idToken, myUid, peerUid, chatId);
        Message current = messageById(before, message.id);
        if (current == null || !myUid.equals(current.senderUid)) {
            return false;
        }
        boolean wasLatest = isLatest(before, current.id);
        if (TYPE_FILE.equals(current.type)
                && !deleteOwnFileChunks(idToken, myUid, peerUid, current)) {
            return false;
        }
        try {
            Response r = request("DELETE", Vault.firestoreBaseUrl()
                    + "chats/" + chatId + "/messages/" + message.id,
                    idToken, null);
            boolean deleted = (r.code >= 200 && r.code < 300) || r.code == 404;
            if (!deleted) {
                Message reloaded = messageById(
                        listMessages(idToken, myUid, peerUid, chatId), message.id);
                deleted = reloaded == null;
            }
            if (!deleted) {
                return false;
            }
        } catch (Exception ignored) {
            return false;
        }
        if (TYPE_FILE.equals(current.type)) {
            deleteFileMetadataQuietly(idToken, chatId, current.fileId);
        }
        if (wasLatest) {
            List<Message> after = listMessages(idToken, myUid, peerUid, chatId);
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
        if (idToken == null || !validPair(myUid, peerUid) || message == null
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
        return createMessageAndPatch(idToken, myUid, peerUid, chatId, message);
    }

    static boolean sendFile(String idToken, String myUid, String peerUid,
                            Message message, InputStreamFactory factory,
                            ProgressListener progress) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || factory == null || !validMessageId(message.id)
                || !myUid.equals(message.senderUid)
                || !validTime(message.createdAt)
                || message.fileSize < 0 || message.fileSize > MAX_FILE_BYTES) {
            return false;
        }
        String cleanName = safeFileName(message.fileName);
        if (cleanName.length() == 0) {
            return false;
        }
        message.type = TYPE_FILE;
        message.fileId = message.id;
        message.fileName = cleanName;
        message.text = cleanName;
        String cleanMime = safeMimeType(message.mimeType);
        message.mimeType = cleanMime.length() > 0
                ? cleanMime : "application/octet-stream";
        message.chunkSize = FILE_CHUNK_BYTES;
        message.chunkCount = chunkCount(message.fileSize, message.chunkSize);
        if (!validFileGeometry(message.fileSize, message.chunkSize,
                message.chunkCount)) {
            return false;
        }
        // Hash the complete source stream before any metadata is
        // published.  The source is then opened a second time for the
        // chunk upload and hashed again there; a provider stream that
        // changes between passes cannot be published under this digest.
        String digest = hashInput(factory, message.fileSize);
        if (!validSha256(digest)) {
            return false;
        }
        message.fileSha256 = digest;
        String chatId = directChatId(myUid, peerUid);
        if (!ensureDirectChat(idToken, myUid, peerUid, chatId, message.createdAt)
                || !ensureFileMetadata(idToken, chatId, myUid, peerUid, message)) {
            return false;
        }
        InputStream input = null;
        try {
            input = factory.open();
            if (input == null) {
                return false;
            }
            MessageDigest uploadDigest = MessageDigest.getInstance("SHA-256");
            byte[] buffer = new byte[FILE_CHUNK_BYTES];
            long sent = 0;
            for (int index = 0; index < message.chunkCount; index++) {
                int expected = (int) Math.min(FILE_CHUNK_BYTES,
                        message.fileSize - sent);
                int read = readExactly(input, buffer, expected);
                if (read != expected) {
                    return false;
                }
                uploadDigest.update(buffer, 0, expected);
                byte[] chunk = expected == buffer.length
                        ? buffer : Arrays.copyOf(buffer, expected);
                if (!ensureFileChunk(idToken, chatId, message.fileId, index, chunk)) {
                    return false;
                }
                sent += expected;
                if (progress != null) {
                    progress.onProgress(message.fileSize == 0 ? 100
                            : (int) Math.min(100, sent * 100L / message.fileSize));
                }
            }
            if (input.read() != -1
                    || !sha256Hex(uploadDigest).equals(message.fileSha256)) {
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
        return createMessageAndPatch(idToken, myUid, peerUid, chatId, message);
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

    static boolean downloadFile(String idToken, String myUid, String peerUid,
                                Message message, OutputStream output,
                                ProgressListener progress) {
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || output == null || !TYPE_FILE.equals(message.type)
                || !validMessageId(message.id)
                || !message.id.equals(message.fileId)
                || !(myUid.equals(message.senderUid)
                        || peerUid.equals(message.senderUid))
                || safeFileName(message.fileName).length() == 0
                || !validFileGeometry(message.fileSize, message.chunkSize,
                        message.chunkCount)
                || !(message.fileSha256 == null
                        || message.fileSha256.length() == 0
                        || validSha256(message.fileSha256))) {
            return false;
        }
        FileStatus status = loadFileStatus(idToken, myUid, peerUid, message);
        String expectedRecipient = myUid.equals(message.senderUid)
                ? peerUid : myUid;
        if (!status.ok || !status.exists
                || !message.senderUid.equals(status.senderUid)
                || !expectedRecipient.equals(status.recipientUid)
                || !safeFileName(message.fileName).equals(status.name)
                || !(safeMimeType(message.mimeType).length() > 0
                        ? safeMimeType(message.mimeType)
                        : "application/octet-stream").equals(status.mimeType)
                || message.fileSize != status.size
                || message.chunkSize != status.chunkSize
                || message.chunkCount != status.chunkCount
                || !(message.fileSha256 == null ? "" : message.fileSha256)
                        .equals(status.sha256)
                || status.downloadedBy(myUid)) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        int chunkSize = message.chunkSize;
        int count = message.chunkCount;
        try {
            List<byte[]> chunks = new ArrayList<>(count);
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
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
                byte[] chunk = bytes(fields, "data",
                        expectedChunkLength(message.fileSize, chunkSize, count, index));
                int expected = expectedChunkLength(
                        message.fileSize, chunkSize, count, index);
                if (chunk == null || chunk.length != expected) {
                    return false;
                }
                chunks.add(chunk);
                digest.update(chunk);
                received += chunk.length;
                if (progress != null) {
                    progress.onProgress(message.fileSize == 0 ? 100
                            : (int) Math.min(100, received * 100L / message.fileSize));
                }
            }
            // Nothing reaches the user's chosen document until the
            // complete byte sequence has been verified.  Legacy files
            // have no digest and retain their exact-size verification.
            String actualSha256 = sha256Hex(digest);
            if (received != message.fileSize
                    || (status.sha256.length() > 0
                            && !status.sha256.equals(actualSha256))) {
                return false;
            }
            for (byte[] chunk : chunks) {
                output.write(chunk);
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

    static FileStatus loadFileStatus(String idToken, String myUid,
                                     String peerUid, Message message) {
        FileStatus status = new FileStatus();
        if (idToken == null || !validPair(myUid, peerUid) || message == null
                || !TYPE_FILE.equals(message.type)
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
        if (fields == null || message == null
                || (fields.length() != 14 && fields.length() != 15)
                || !message.fileId.equals(documentId(doc))
                || num(fields, "protocolVersion", -1) != VERSION) {
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
                || !validFileGeometry(size, (int) chunkSizeValue,
                        (int) chunkCountValue)
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
        if (idToken == null || !validPair(myUid, peerUid)
                || !validWallpaperId(wallpaperId)
                || !validTime(createdAt)
                || image == null || image.length == 0
                || image.length > MAX_WALLPAPER_BYTES) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        if (!ensureDirectChat(idToken, myUid, peerUid, chatId, createdAt)) {
            return false;
        }
        try {
            String docUrl = Vault.firestoreBaseUrl() + "users/" + myUid
                    + "/wallpapers/" + wallpaperId;
            Response existing = request("GET", docUrl, idToken, null);
            if (existing.code == 200) {
                return wallpaperDocumentMatches(new JSONObject(existing.text),
                        wallpaperId, chatId, myUid, shared, image);
            }
            JSONObject fields = new JSONObject();
            putStr(fields, "chatId", chatId);
            putStr(fields, "senderUid", myUid);
            putBool(fields, "shared", shared);
            putBytes(fields, "image", image);
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
                                wallpaperId, chatId, myUid, shared, image);
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
            return fields == null ? null
                    : bytes(fields, "image", MAX_WALLPAPER_BYTES);
        } catch (Exception ignored) {
            return null;
        }
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
        byte[] image = bytes(fields, "image", MAX_WALLPAPER_BYTES);
        return image != null && image.length > 0 ? fields : null;
    }

    private static boolean wallpaperDocumentMatches(JSONObject doc, String wallpaperId,
                                                    String chatId, String ownerUid,
                                                    boolean shared, byte[] image) {
        JSONObject fields = validWallpaperFields(doc, wallpaperId, ownerUid);
        if (fields == null || image == null) {
            return false;
        }
        byte[] existing = bytes(fields, "image", MAX_WALLPAPER_BYTES);
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

    private static boolean createMessageAndPatch(String idToken, String myUid,
                                                 String peerUid, String chatId,
                                                 Message message) {
        // Validate again at the single Firestore write boundary.  The
        // public send* methods validate their inputs too, but this
        // private method must not trust a future caller that forgot to.
        if (idToken == null || !validPair(myUid, peerUid)
                || chatId == null
                || !chatId.equals(directChatId(myUid, peerUid))
                || message == null
                || !validMessageId(message.id)
                || !myUid.equals(message.senderUid)
                || !validTime(message.createdAt)
                || message.text == null
                || message.text.length() == 0
                || message.text.length() > MAX_TEXT) {
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
                    || !validSha256(message.fileSha256)
                    || !validFileGeometry(message.fileSize, message.chunkSize,
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
        try {
            JSONObject fields = new JSONObject();
            putNum(fields, "protocolVersion", VERSION);
            putStr(fields, "type", message.type);
            putStr(fields, "senderUid", myUid);
            putStr(fields, "text", message.text);
            putNum(fields, "createdAt", message.createdAt);
            putStr(fields, "clientId", message.id);
            if (TYPE_FILE.equals(message.type)) {
                putStr(fields, "fileId", message.fileId);
                putStr(fields, "fileName", message.fileName);
                putStr(fields, "mimeType", message.mimeType);
                putNum(fields, "fileSize", message.fileSize);
                putNum(fields, "chunkCount", message.chunkCount);
                putNum(fields, "chunkSize", message.chunkSize);
                putStr(fields, "fileSha256", message.fileSha256);
            } else if (TYPE_WALLPAPER.equals(message.type)) {
                putStr(fields, "wallpaperId", message.wallpaperId);
                putStr(fields, "wallpaperOwnerUid", message.wallpaperOwnerUid);
            }
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
                    || !validSha256(message.fileSha256)
                    || !validFileGeometry(message.fileSize, message.chunkSize,
                            message.chunkCount)) {
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
            putNum(fields, "protocolVersion", VERSION);
            putStr(fields, "senderUid", senderUid);
            putStr(fields, "recipientUid", recipientUid);
            putStr(fields, "fileName", message.fileName);
            putStr(fields, "mimeType", message.mimeType);
            putNum(fields, "fileSize", message.fileSize);
            putNum(fields, "chunkSize", message.chunkSize);
            putNum(fields, "chunkCount", message.chunkCount);
            putStr(fields, "fileSha256", message.fileSha256);
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
        String expectedMime = safeMimeType(message.mimeType);
        if (expectedMime.length() == 0) {
            expectedMime = "application/octet-stream";
        }
        return senderUid.equals(status.senderUid)
                && recipientUid.equals(status.recipientUid)
                && safeFileName(message.fileName).equals(status.name)
                && expectedMime.equals(status.mimeType)
                && message.fileSize == status.size
                && message.chunkSize == status.chunkSize
                && message.chunkCount == status.chunkCount
                && (message.fileSha256 == null ? "" : message.fileSha256)
                        .equals(status.sha256);
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
                    Message message = parseMessage(docs.optJSONObject(i), myUid, peerUid);
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
    private static Message parseMessage(JSONObject doc, String myUid, String peerUid) {
        JSONObject fields = doc == null ? null : doc.optJSONObject("fields");
        if (fields == null || !validPair(myUid, peerUid)
                || num(fields, "protocolVersion", -1) != VERSION) {
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
        message.id = id;
        message.type = type;
        message.senderUid = senderUid;
        message.text = text;
        message.createdAt = createdAt;
        message.editedAt = editedAt;
        if (TYPE_TEXT.equals(type)) {
            int expectedFields = fields.has("editedAt") ? 7 : 6;
            return fields.length() == expectedFields ? message : null;
        }
        if (TYPE_FILE.equals(type)) {
            if (editedAt != 0 || (fields.length() != 12 && fields.length() != 13)) {
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
            return message;
        }
        if (TYPE_WALLPAPER.equals(type)) {
            if (editedAt != 0 || fields.length() != 8
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
            return message;
        }
        return null;
    }

    private static boolean messageDocumentMatches(JSONObject doc, String myUid,
                                                  String peerUid, Message expected) {
        Message actual = parseMessage(doc, myUid, peerUid);
        if (actual == null || expected == null
                || !expected.id.equals(actual.id)
                || !expected.type.equals(actual.type)
                || !myUid.equals(actual.senderUid)
                || expected.createdAt != actual.createdAt) {
            return false;
        }
        if (TYPE_TEXT.equals(expected.type)) {
            // A retry may race with an edit made on another device; an
            // edited envelope is still the same message.  Unedited text
            // must match exactly.
            return actual.editedAt > 0 || expected.text.equals(actual.text);
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
                            .equals(actual.fileSha256);
        }
        return TYPE_WALLPAPER.equals(expected.type)
                && expected.wallpaperId.equals(actual.wallpaperId)
                && expected.wallpaperOwnerUid.equals(actual.wallpaperOwnerUid)
                && actual.text.equals("Wallpaper suggestion");
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
            putStr(fields, "lastMessageText", message.text);
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
                    message.text, message.createdAt, senderUid, message.id, readAt);
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
            putStr(fields, "lastMessageText", message.text);
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
                    message.text, message.createdAt, message.senderUid,
                    message.id, existing.lastReadAt);
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
            putStr(fields, "lastMessageText", latest == null ? "" : latest.text);
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
                    latest == null ? "" : latest.text, at,
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
