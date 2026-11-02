package lav.visaroom.alav;

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

    static final class Message {
        String id = "";
        String senderUid = "";
        String text = "";
        long createdAt;
        boolean pending;
        boolean failed;
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
    }

    private static final class Index {
        String peerUid = "";
        String lastText = "";
        String lastSenderUid = "";
        long lastMessageAt;
        long updatedAt;
        long lastReadAt;
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
        String chatId = directChatId(myUid, peerUid);
        if (!ensureDirectChat(idToken, myUid, peerUid, chatId, message.createdAt)) {
            return false;
        }
        try {
            JSONObject fields = new JSONObject();
            putNum(fields, "protocolVersion", VERSION);
            putStr(fields, "type", "text");
            putStr(fields, "senderUid", myUid);
            putStr(fields, "text", message.text);
            putNum(fields, "createdAt", message.createdAt);
            putStr(fields, "clientId", message.id);
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
                    if (fields == null || !"text".equals(str(fields, "type", ""))) {
                        continue;
                    }
                    Message m = new Message();
                    m.id = str(fields, "clientId", documentId(doc));
                    if (m.id.length() == 0) {
                        m.id = documentId(doc);
                    }
                    m.senderUid = str(fields, "senderUid", "");
                    m.text = str(fields, "text", "");
                    m.createdAt = num(fields, "createdAt", 0);
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
        index.lastMessageAt = num(fields, "lastMessageAt", 0);
        index.updatedAt = num(fields, "updatedAt", index.lastMessageAt);
        index.lastReadAt = num(fields, "lastReadAt", 0);
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
