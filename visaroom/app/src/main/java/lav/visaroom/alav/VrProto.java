package lav.visaroom.alav;

import android.util.Base64;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
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
        int chunkCount;
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
        String a = firstUid == null ? "" : firstUid;
        String b = secondUid == null ? "" : secondUid;
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
            return "vrp1_" + Math.abs((a + "_" + b).hashCode());
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

    static List<ChatSummary> listChats(String idToken, String myUid) {
        if (idToken == null || myUid == null || myUid.length() == 0) {
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
                if (index == null || index.peerUid.length() == 0
                        || index.peerUid.equals(myUid) || index.lastMessageAt == 0) {
                    continue;
                }
                ChatSummary summary = new ChatSummary();
                summary.chatId = documentId(doc);
                summary.peerUid = index.peerUid;
                summary.lastText = index.lastText;
                summary.lastSenderUid = index.lastSenderUid;
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
        if (idToken == null || myUid == null || peerUid == null
                || myUid.length() == 0 || peerUid.length() == 0
                || myUid.equals(peerUid)) {
            return result;
        }
        String chatId = directChatId(myUid, peerUid);
        Index own = loadIndex(idToken, myUid, chatId);
        List<Message> messages = listMessages(idToken, chatId);
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
        if (idToken == null || myUid == null || peerUid == null || message == null
                || myUid.length() == 0 || peerUid.length() == 0
                || myUid.equals(peerUid) || message.id.length() == 0
                || message.text.trim().length() == 0
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
        if (idToken == null || myUid == null || peerUid == null || message == null
                || newText == null || newText.trim().length() == 0
                || newText.length() > MAX_TEXT || editedAt <= 0
                || !TYPE_TEXT.equals(message.type)
                || !myUid.equals(message.senderUid)
                || message.id.length() == 0) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        List<Message> before = listMessages(idToken, chatId);
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
                        listMessages(idToken, chatId), message.id);
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
        if (idToken == null || myUid == null || peerUid == null || message == null
                || message.id.length() == 0 || operationAt <= 0
                || !myUid.equals(message.senderUid)) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        List<Message> before = listMessages(idToken, chatId);
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
                        listMessages(idToken, chatId), message.id);
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
            List<Message> after = listMessages(idToken, chatId);
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
        int count = status.chunkCount > 0 ? status.chunkCount : message.chunkCount;
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
            request("DELETE", fileMetadataUrl(chatId, fileId), idToken, null);
        } catch (Exception ignored) {
        }
    }

    static boolean sendWallpaper(String idToken, String myUid, String peerUid,
                                 Message message) {
        if (idToken == null || myUid == null || peerUid == null || message == null
                || myUid.length() == 0 || peerUid.length() == 0
                || myUid.equals(peerUid) || message.id.length() == 0
                || message.wallpaperId.length() == 0
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
        if (idToken == null || myUid == null || peerUid == null || message == null
                || factory == null || myUid.length() == 0 || peerUid.length() == 0
                || myUid.equals(peerUid) || message.id.length() == 0
                || message.fileName.trim().length() == 0
                || message.fileName.length() > 255
                || message.fileSize < 0 || message.fileSize > MAX_FILE_BYTES) {
            return false;
        }
        message.type = TYPE_FILE;
        message.fileId = message.id;
        message.text = message.fileName;
        if (message.mimeType == null || message.mimeType.length() == 0) {
            message.mimeType = "application/octet-stream";
        }
        if (message.mimeType.length() > 128) {
            message.mimeType = message.mimeType.substring(0, 128);
        }
        message.chunkSize = FILE_CHUNK_BYTES;
        message.chunkCount = chunkCount(message.fileSize, message.chunkSize);
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
            byte[] buffer = new byte[FILE_CHUNK_BYTES];
            long sent = 0;
            for (int index = 0; index < message.chunkCount; index++) {
                int expected = (int) Math.min(FILE_CHUNK_BYTES,
                        message.fileSize - sent);
                int read = readExactly(input, buffer, expected);
                if (read != expected) {
                    return false;
                }
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
            if (input.read() != -1) {
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

    static boolean downloadFile(String idToken, String myUid, String peerUid,
                                Message message, OutputStream output,
                                ProgressListener progress) {
        if (idToken == null || myUid == null || peerUid == null || message == null
                || output == null || !TYPE_FILE.equals(message.type)
                || message.fileId.length() == 0 || message.fileSize < 0
                || message.fileSize > MAX_FILE_BYTES) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        int chunkSize = message.chunkSize > 0 ? message.chunkSize : FILE_CHUNK_BYTES;
        int count = message.chunkCount > 0
                ? message.chunkCount : chunkCount(message.fileSize, chunkSize);
        try {
            long written = 0;
            for (int index = 0; index < count; index++) {
                Response r = request("GET", Vault.firestoreBaseUrl()
                        + "chats/" + chatId + "/files/" + message.fileId
                        + "/chunks/" + index, idToken, null);
                if (r.code != 200) {
                    return false;
                }
                JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
                byte[] chunk = fields == null ? null : bytes(fields, "data");
                long expected = Math.min(chunkSize, message.fileSize - written);
                if (chunk == null || chunk.length != expected) {
                    return false;
                }
                output.write(chunk);
                written += chunk.length;
                if (progress != null) {
                    progress.onProgress(message.fileSize == 0 ? 100
                            : (int) Math.min(100, written * 100L / message.fileSize));
                }
            }
            output.flush();
            if (progress != null) {
                progress.onProgress(100);
            }
            return written == message.fileSize;
        } catch (Exception ignored) {
            return false;
        }
    }

    static FileStatus loadFileStatus(String idToken, String myUid,
                                     String peerUid, Message message) {
        FileStatus status = new FileStatus();
        if (idToken == null || myUid == null || peerUid == null || message == null
                || !TYPE_FILE.equals(message.type) || message.fileId.length() == 0) {
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
        int count = status.chunkCount > 0 ? status.chunkCount : message.chunkCount;
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
        if (fields == null) {
            return status;
        }
        status.ok = true;
        status.exists = true;
        status.senderUid = str(fields, "senderUid",
                message == null ? "" : message.senderUid);
        status.recipientUid = str(fields, "recipientUid", "");
        status.chunkCount = (int) num(fields, "chunkCount",
                message == null ? 0 : message.chunkCount);
        status.downloadCount = (int) num(fields, "downloadCount", 0);
        status.downloadLimit = (int) num(fields, "downloadLimit",
                FILE_DOWNLOAD_LIMIT);
        if (status.downloadLimit <= 0) {
            status.downloadLimit = FILE_DOWNLOAD_LIMIT;
        }
        status.senderDownloaded = bool(fields, "senderDownloaded", false);
        status.recipientDownloaded = bool(fields, "recipientDownloaded", false);
        return status;
    }

    static boolean uploadWallpaper(String idToken, String myUid, String peerUid,
                                   String wallpaperId, byte[] image,
                                   boolean shared, long createdAt) {
        if (idToken == null || myUid == null || peerUid == null
                || wallpaperId == null || wallpaperId.length() == 0
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
                return true;
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
            return (r.code == 409 || r.code == 400)
                    && request("GET", docUrl, idToken, null).code == 200;
        } catch (Exception ignored) {
            return false;
        }
    }

    static byte[] loadWallpaper(String idToken, String ownerUid, String wallpaperId) {
        if (idToken == null || ownerUid == null || wallpaperId == null
                || ownerUid.length() == 0 || wallpaperId.length() == 0) {
            return null;
        }
        try {
            Response r = request("GET", Vault.firestoreBaseUrl() + "users/"
                    + ownerUid + "/wallpapers/" + wallpaperId, idToken, null);
            if (r.code != 200) {
                return null;
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            return fields == null ? null : bytes(fields, "image");
        } catch (Exception ignored) {
            return null;
        }
    }

    static boolean setOwnWallpaper(String idToken, String myUid, String peerUid,
                                   String wallpaperId, String ownerUid,
                                   long updatedAt) {
        if (idToken == null || myUid == null || peerUid == null
                || myUid.length() == 0 || peerUid.length() == 0) {
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
                created = existing.code == 200;
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
            String docUrl = Vault.firestoreBaseUrl() + "chats/" + chatId
                    + "/files/" + message.fileId;
            if (request("GET", docUrl, idToken, null).code == 200) {
                return true;
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
            return (r.code == 409 || r.code == 400)
                    && request("GET", docUrl, idToken, null).code == 200;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean ensureFileChunk(String idToken, String chatId,
                                           String fileId, int index,
                                           byte[] chunk) {
        try {
            String docUrl = Vault.firestoreBaseUrl() + "chats/" + chatId
                    + "/files/" + fileId + "/chunks/" + index;
            if (request("GET", docUrl, idToken, null).code == 200) {
                return true;
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
            return (r.code == 409 || r.code == 400)
                    && request("GET", docUrl, idToken, null).code == 200;
        } catch (Exception ignored) {
            return false;
        }
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
        if (idToken == null || myUid == null || peerUid == null || readAt <= 0) {
            return false;
        }
        String chatId = directChatId(myUid, peerUid);
        Index own = loadIndex(idToken, myUid, chatId);
        if (own == null) {
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

    private static List<Message> listMessages(String idToken, String chatId) {
        try {
            String url = Vault.firestoreBaseUrl() + "chats/" + chatId
                    + "/messages?pageSize=100&orderBy=createdAt%20desc";
            Response r = request("GET", url, idToken, null);
            if (r.code != 200) {
                return null;
            }
            List<Message> out = new ArrayList<>();
            JSONArray docs = new JSONObject(r.text).optJSONArray("documents");
            if (docs != null) {
                for (int i = 0; i < docs.length(); i++) {
                    JSONObject doc = docs.optJSONObject(i);
                    if (doc == null) {
                        continue;
                    }
                    JSONObject fields = doc.optJSONObject("fields");
                    if (fields == null) {
                        continue;
                    }
                    String type = str(fields, "type", TYPE_TEXT);
                    if (!TYPE_TEXT.equals(type) && !TYPE_FILE.equals(type)
                            && !TYPE_WALLPAPER.equals(type)) {
                        continue;
                    }
                    Message m = new Message();
                    m.id = str(fields, "clientId", documentId(doc));
                    if (m.id.length() == 0) {
                        m.id = documentId(doc);
                    }
                    m.type = type;
                    m.senderUid = str(fields, "senderUid", "");
                    m.text = str(fields, "text", "");
                    m.createdAt = num(fields, "createdAt", 0);
                    m.editedAt = num(fields, "editedAt", 0);
                    m.fileId = str(fields, "fileId", "");
                    m.fileName = str(fields, "fileName", m.text);
                    m.mimeType = str(fields, "mimeType", "application/octet-stream");
                    m.fileSize = num(fields, "fileSize", 0);
                    m.chunkCount = (int) num(fields, "chunkCount", 0);
                    m.chunkSize = (int) num(fields, "chunkSize", FILE_CHUNK_BYTES);
                    m.wallpaperId = str(fields, "wallpaperId", "");
                    m.wallpaperOwnerUid = str(fields, "wallpaperOwnerUid", "");
                    if (m.senderUid.length() > 0 && m.text.length() > 0) {
                        out.add(m);
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

    private static boolean ensureDirectChat(String idToken, String myUid,
                                            String peerUid, String chatId, long now) {
        try {
            Response existing = request("GET",
                    Vault.firestoreBaseUrl() + "chats/" + chatId, idToken, null);
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
                    ok = check.code == 200;
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

    private static boolean ensureIndex(String idToken, String ownerUid,
                                       String peerUid, String chatId, long now) {
        if (loadIndex(idToken, ownerUid, chatId) != null) {
            return true;
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
            return (r.code == 409 || r.code == 400)
                    && loadIndex(idToken, ownerUid, chatId) != null;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static Index loadIndex(String idToken, String ownerUid, String chatId) {
        try {
            Response r = request("GET", Vault.firestoreBaseUrl()
                    + "users/" + ownerUid + "/chats/" + chatId, idToken, null);
            if (r.code != 200) {
                return null;
            }
            return parseIndex(new JSONObject(r.text));
        } catch (Exception ignored) {
            return null;
        }
    }

    private static Index parseIndex(JSONObject doc) {
        if (doc == null) {
            return null;
        }
        JSONObject fields = doc.optJSONObject("fields");
        if (fields == null) {
            return null;
        }
        Index index = new Index();
        index.peerUid = str(fields, "peerUid", "");
        index.lastText = str(fields, "lastMessageText", "");
        index.lastSenderUid = str(fields, "lastSenderUid", "");
        index.lastMessageId = str(fields, "lastMessageId", "");
        index.lastMessageAt = num(fields, "lastMessageAt", 0);
        index.updatedAt = num(fields, "updatedAt", index.lastMessageAt);
        index.lastReadAt = num(fields, "lastReadAt", 0);
        index.wallpaperId = str(fields, "wallpaperId", "");
        index.wallpaperOwnerUid = str(fields, "wallpaperOwnerUid", "");
        index.wallpaperUpdatedAt = num(fields, "wallpaperUpdatedAt", 0);
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
        String name = doc.optString("name", "");
        int slash = name.lastIndexOf('/');
        return slash >= 0 ? name.substring(slash + 1) : name;
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

    private static byte[] bytes(JSONObject fields, String name) {
        JSONObject v = fields.optJSONObject(name);
        if (v == null) {
            return null;
        }
        String encoded = v.optString("bytesValue");
        if (encoded.length() == 0) {
            return null;
        }
        try {
            return Base64.decode(encoded, Base64.DEFAULT);
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
        String s = v.optString("stringValue");
        return s.length() == 0 && !v.has("stringValue") ? fallback : s;
    }

    private static boolean bool(JSONObject fields, String name, boolean fallback) {
        JSONObject v = fields.optJSONObject(name);
        return v == null ? fallback : v.optBoolean("booleanValue", fallback);
    }

    private static long num(JSONObject fields, String name, long fallback) {
        JSONObject v = fields.optJSONObject(name);
        if (v == null) {
            return fallback;
        }
        try {
            return Long.parseLong(v.optString("integerValue"));
        } catch (Exception ignored) {
            return fallback;
        }
    }

    private static Response request(String method, String url, String idToken,
                                    JSONObject body) throws Exception {
        HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
        c.setRequestMethod(method);
        c.setConnectTimeout(15000);
        c.setReadTimeout(15000);
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
        r.text = readAll(stream);
        c.disconnect();
        return r;
    }

    private static String readAll(InputStream stream) throws Exception {
        if (stream == null) {
            return "";
        }
        StringBuilder builder = new StringBuilder();
        BufferedReader reader = new BufferedReader(
                new InputStreamReader(stream, StandardCharsets.UTF_8));
        String line;
        while ((line = reader.readLine()) != null) {
            builder.append(line);
        }
        reader.close();
        return builder.toString();
    }
}
