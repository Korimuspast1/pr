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
import java.security.SecureRandom;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.TimeZone;

/**
 * Profiles, username search and official announcements on top of the
 * Firestore REST API. Every call carries the user's
 * Firebase ID token, so the security rules decide what is readable:
 * private profile data stays owner-only; the username directory only
 * ever contains a username and its owner's ID (no emails).
 */
final class ProfileClient {

    static final int USERNAME_OK = 0;
    static final int USERNAME_TAKEN = 1;
    static final int USERNAME_ERROR = -1;
    private static final int MAX_AVATAR_BYTES = 120000;
    static final int MAX_ANNOUNCEMENT_CHARS = 4000;

    static final class SearchResult {
        String uid = "";
        String username = "";
        long publicId;
        boolean verified;
    }

    static final class PublicProfile {
        String username = "";
        String bio = "";
        boolean hasAvatar;
        long avatarUpdatedAt;
        long publicId;
        boolean showBio = true;
        boolean showAvatar = true;
        boolean showId = true;
        boolean blocked;
        boolean verified;
    }

    static final class Announcement {
        String text = "";
        String time = "";
    }

    private static final class Response {
        int code;
        String text = "";
    }

    private ProfileClient() {
    }

    // ---------- my profile ----------

    static boolean loadMyProfile(String idToken, String uid, ProfileData out) {
        try {
            Response r = request("GET", Vault.firestoreBaseUrl() + "users/" + uid, idToken, null);
            if (r.code != 200) {
                return false;
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            if (fields == null) {
                return true;
            }
            out.email = str(fields, "email", out.email);
            out.username = str(fields, "username", out.username);
            out.bio = str(fields, "bio", out.bio);
            out.hasAvatar = bool(fields, "hasAvatar", out.hasAvatar);
            out.avatarUpdatedAt = num(fields, "avatarUpdatedAt", out.avatarUpdatedAt);
            out.discoverable = bool(fields, "discoverable", out.discoverable);
            out.publicId = num(fields, "publicId", out.publicId);
            out.twoFaEnabled = bool(fields, "twoFaEnabled", out.twoFaEnabled);
            out.twoFaVerifier = str(fields, "twoFaVerifier", out.twoFaVerifier);
            out.showBio = bool(fields, "showBio", out.showBio);
            out.showAvatar = bool(fields, "showAvatar", out.showAvatar);
            out.showId = bool(fields, "showId", out.showId);
            out.blocked = bool(fields, "blocked", out.blocked);
            out.blockedAt = num(fields, "blockedAt", out.blockedAt);
            out.verified = bool(fields, "verified", out.verified);
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    static boolean saveMyProfile(String idToken, String uid, ProfileData data) {
        try {
            JSONObject mine = new JSONObject();
            putStr(mine, "email", data.email);
            putStr(mine, "username", data.username);
            putStr(mine, "bio", data.bio);
            putBool(mine, "hasAvatar", data.hasAvatar);
            putNum(mine, "avatarUpdatedAt", data.avatarUpdatedAt);
            putBool(mine, "discoverable", data.discoverable);
            if (data.publicId > 0) {
                putNum(mine, "publicId", data.publicId);
            }
            putBool(mine, "twoFaEnabled", data.twoFaEnabled);
            if (data.twoFaVerifier != null && data.twoFaVerifier.length() > 0) {
                putStr(mine, "twoFaVerifier", data.twoFaVerifier);
            }
            putBool(mine, "showBio", data.showBio);
            putBool(mine, "showAvatar", data.showAvatar);
            putBool(mine, "showId", data.showId);
            putBool(mine, "blocked", data.blocked);
            putNum(mine, "blockedAt", data.blockedAt);
            putBool(mine, "verified", data.verified);
            JSONObject mineBody = new JSONObject();
            mineBody.put("fields", mine);
            Response r1 = request("PATCH", Vault.firestoreBaseUrl() + "users/" + uid,
                    idToken, mineBody);
            if (r1.code < 200 || r1.code >= 300) {
                return false;
            }

            // Public copy: privacy settings are applied here, so a hidden
            // bio/photo/ID is not present in the public document at all.
            JSONObject pub = new JSONObject();
            putStr(pub, "username", data.username);
            putStr(pub, "bio", data.showBio ? data.bio : "");
            putBool(pub, "hasAvatar", data.showAvatar && data.hasAvatar);
            putNum(pub, "avatarUpdatedAt", data.avatarUpdatedAt);
            if (data.showId && data.publicId > 0) {
                putNum(pub, "publicId", data.publicId);
            }
            putBool(pub, "showBio", data.showBio);
            putBool(pub, "showAvatar", data.showAvatar);
            putBool(pub, "showId", data.showId);
            putBool(pub, "blocked", data.blocked);
            putBool(pub, "verified", data.verified);
            JSONObject pubBody = new JSONObject();
            pubBody.put("fields", pub);
            Response r2 = request("PATCH", Vault.firestoreBaseUrl() + "profiles/" + uid,
                    idToken, pubBody);
            return r2.code >= 200 && r2.code < 300;
        } catch (Exception ignored) {
            return false;
        }
    }

    /**
     * Makes sure Firestore contains this account's documents:
     * users/{uid}, profiles/{uid}, publicIds/{publicId} and, when a
     * discoverable username exists, usernames/{usernameLower}.  This is
     * what makes new and previously-registered users appear in the
     * Firestore console after they sign in or simply open the app.
     * The supplied data is used as a local fallback and is replaced by
     * the cloud values when a profile already exists.
     */
    static boolean ensureUserDocuments(String idToken, String uid, ProfileData data) {
        if (idToken == null || uid == null || uid.length() == 0 || data == null) {
            return false;
        }
        loadMyProfile(idToken, uid, data);
        if (data.blocked) {
            // Callers check data.blocked and stop before opening chats.
            // A blocked owner must not recreate search/avatar documents.
            return false;
        }
        if (!validPublicId(data.publicId)) {
            long assigned = assignPublicId(idToken, uid, data.publicId, 0);
            if (assigned > 0) {
                data.publicId = assigned;
            }
        }
        boolean saved = saveMyProfile(idToken, uid, data);
        if (saved && !data.blocked && data.username != null
                && data.username.trim().length() > 0 && data.discoverable) {
            claimUsername(idToken, uid, data.username,
                    data.usernameLower(), true, data.publicId, data.verified);
        }
        return saved;
    }

    static int claimUsername(String idToken, String uid, String username,
                             String oldLower, boolean discoverable, long publicId,
                             boolean verified) {
        String lower = username == null ? ""
                : username.trim().toLowerCase(java.util.Locale.ROOT);
        try {
            if (lower.length() == 0 || !discoverable) {
                if (oldLower != null && oldLower.length() > 0) {
                    deleteUsername(idToken, oldLower);
                }
                return USERNAME_OK;
            }
            if (!lower.equals(oldLower)) {
                Response existing = request("GET",
                        Vault.firestoreBaseUrl() + "usernames/" + lower, idToken, null);
                if (existing.code == 200) {
                    JSONObject fields = new JSONObject(existing.text).optJSONObject("fields");
                    String owner = fields == null ? "" : str(fields, "uid", "");
                    if (owner.length() > 0 && !owner.equals(uid)) {
                        return USERNAME_TAKEN;
                    }
                }
            }
            JSONObject fields = new JSONObject();
            putStr(fields, "uid", uid);
            putStr(fields, "username", username.trim());
            putStr(fields, "usernameLower", lower);
            if (publicId > 0) {
                putNum(fields, "publicId", publicId);
            }
            putBool(fields, "verified", verified);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("PATCH", Vault.firestoreBaseUrl() + "usernames/" + lower,
                    idToken, body);
            if (r.code < 200 || r.code >= 300) {
                return USERNAME_ERROR;
            }
            if (oldLower != null && oldLower.length() > 0 && !oldLower.equals(lower)) {
                deleteUsername(idToken, oldLower);
            }
            return USERNAME_OK;
        } catch (Exception ignored) {
            return USERNAME_ERROR;
        }
    }

    static void deleteUsername(String idToken, String lower) {
        try {
            request("DELETE", Vault.firestoreBaseUrl() + "usernames/" + lower, idToken, null);
        } catch (Exception ignored) {
        }
    }

    // ---------- search & public profiles ----------

    static List<SearchResult> searchUsernames(String idToken, String queryLower) {
        List<SearchResult> results = new ArrayList<>();
        try {
            JSONObject from = new JSONObject();
            from.put("collectionId", "usernames");
            JSONArray fromArr = new JSONArray();
            fromArr.put(from);

            JSONObject ge = fieldFilter("usernameLower", "GREATER_THAN_OR_EQUAL", queryLower);
            JSONObject lt = fieldFilter("usernameLower", "LESS_THAN", queryLower + "\uf8ff");
            JSONArray filters = new JSONArray();
            filters.put(ge);
            filters.put(lt);
            JSONObject composite = new JSONObject();
            composite.put("op", "AND");
            composite.put("filters", filters);
            JSONObject where = new JSONObject();
            where.put("compositeFilter", composite);

            JSONObject orderField = new JSONObject();
            orderField.put("fieldPath", "usernameLower");
            JSONObject order = new JSONObject();
            order.put("field", orderField);
            JSONArray orderBy = new JSONArray();
            orderBy.put(order);

            JSONObject query = new JSONObject();
            query.put("from", fromArr);
            query.put("where", where);
            query.put("orderBy", orderBy);
            query.put("limit", 10);
            JSONObject body = new JSONObject();
            body.put("structuredQuery", query);

            String base = Vault.firestoreBaseUrl();
            String url = (base.endsWith("/") ? base.substring(0, base.length() - 1) : base)
                    + ":runQuery";
            Response r = request("POST", url, idToken, body);
            if (r.code != 200) {
                return results;
            }
            JSONArray rows = new JSONArray(r.text);
            for (int i = 0; i < rows.length(); i++) {
                JSONObject doc = rows.getJSONObject(i).optJSONObject("document");
                if (doc == null) {
                    continue;
                }
                JSONObject fields = doc.optJSONObject("fields");
                if (fields == null) {
                    continue;
                }
                SearchResult s = new SearchResult();
                s.uid = str(fields, "uid", "");
                s.username = str(fields, "username", "");
                s.publicId = num(fields, "publicId", 0);
                s.verified = bool(fields, "verified", false);
                if (s.uid.length() > 0 && s.username.length() > 0) {
                    results.add(s);
                }
            }
            return results;
        } catch (Exception ignored) {
            return results;
        }
    }

    static String loadUidByPublicId(String idToken, long publicId) {
        if (!validPublicId(publicId)) {
            return "";
        }
        try {
            Response r = request("GET", Vault.firestoreBaseUrl() + "publicIds/" + publicId,
                    idToken, null);
            if (r.code != 200) {
                return "";
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            return fields == null ? "" : str(fields, "uid", "");
        } catch (Exception ignored) {
            return "";
        }
    }

    static String loadUidByUsername(String idToken, String usernameLower) {
        try {
            Response r = request("GET", Vault.firestoreBaseUrl() + "usernames/" + usernameLower,
                    idToken, null);
            if (r.code != 200) {
                return "";
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            return fields == null ? "" : str(fields, "uid", "");
        } catch (Exception ignored) {
            return "";
        }
    }

    static PublicProfile loadPublicProfile(String idToken, String uid) {
        PublicProfile p = new PublicProfile();
        try {
            Response r = request("GET", Vault.firestoreBaseUrl() + "profiles/" + uid,
                    idToken, null);
            if (r.code != 200) {
                return p;
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            if (fields != null) {
                p.username = str(fields, "username", "");
                p.bio = str(fields, "bio", "");
                p.hasAvatar = bool(fields, "hasAvatar", false);
                p.avatarUpdatedAt = num(fields, "avatarUpdatedAt", 0);
                p.publicId = num(fields, "publicId", 0);
                p.showBio = bool(fields, "showBio", true);
                p.showAvatar = bool(fields, "showAvatar", true);
                p.showId = bool(fields, "showId", true);
                p.blocked = bool(fields, "blocked", false);
                p.verified = bool(fields, "verified", false);
            }
            return p;
        } catch (Exception ignored) {
            return p;
        }
    }

    static List<Announcement> listAnnouncements(String idToken) {
        List<Announcement> out = new ArrayList<>();
        try {
            String url = Vault.firestoreBaseUrl()
                    + "announcements?pageSize=50&orderBy=createdAt%20desc";
            Response r = request("GET", url, idToken, null);
            if (r.code != 200) {
                return out;
            }
            JSONArray docs = new JSONObject(r.text).optJSONArray("documents");
            if (docs == null) {
                return out;
            }
            for (int i = 0; i < docs.length(); i++) {
                JSONObject fields = docs.getJSONObject(i).optJSONObject("fields");
                if (fields == null) {
                    continue;
                }
                String title = str(fields, "title", "");
                String text = str(fields, "text", str(fields, "body", ""));
                String message = title.length() > 0
                        ? (text.length() > 0 ? title + "\n" + text : title) : text;
                if (message.length() == 0) {
                    continue;
                }
                Announcement a = new Announcement();
                a.text = message;
                a.time = timestamp(fields, "createdAt");
                out.add(a);
            }
            java.util.Collections.reverse(out);
            return out;
        } catch (Exception ignored) {
            return out;
        }
    }



    static String newAnnouncementId(long createdAtMillis) {
        long random = new SecureRandom().nextLong();
        return "a" + Math.max(1L, createdAtMillis) + "_"
                + String.format(Locale.ROOT, "%016x", random);
    }

    /**
     * Creates one official-channel announcement.  The Firestore rules
     * independently require the administrator's Firebase token email;
     * this method being reachable in the UI is not the security boundary.
     * A caller-supplied stable ID makes a retry after a lost response
     * idempotent instead of posting the same announcement twice.
     */
    static boolean publishAnnouncement(String idToken, String uid,
                                       String announcementId, String text,
                                       long createdAtMillis) {
        try {
            String cleanText = text == null ? "" : text.trim();
            if (idToken == null || idToken.length() == 0
                    || uid == null || uid.length() == 0
                    || announcementId == null
                    || !announcementId.matches("a[0-9]+_[0-9a-f]{16}")
                    || cleanText.length() == 0
                    || cleanText.length() > MAX_ANNOUNCEMENT_CHARS
                    || createdAtMillis <= 0) {
                return false;
            }
            JSONObject fields = new JSONObject();
            putStr(fields, "text", cleanText);
            putStr(fields, "authorUid", uid);
            putTimestamp(fields, "createdAt", createdAtMillis);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("POST", Vault.firestoreBaseUrl()
                    + "announcements?documentId=" + announcementId, idToken, body);
            if (r.code >= 200 && r.code < 300) {
                return true;
            }
            if (r.code == 409 || r.code == 400) {
                Response existing = request("GET", Vault.firestoreBaseUrl()
                        + "announcements/" + announcementId, idToken, null);
                if (existing.code == 200) {
                    JSONObject existingFields = new JSONObject(existing.text)
                            .optJSONObject("fields");
                    return existingFields != null
                            && uid.equals(str(existingFields, "authorUid", ""))
                            && cleanText.equals(str(existingFields, "text", ""));
                }
            }
            return false;
        } catch (Exception ignored) {
            return false;
        }
    }

    // ---------- public numeric IDs (100000-5000000) ----------

    static final long PUBLIC_ID_MIN = 100000L;
    static final long PUBLIC_ID_MAX = 5000000L;
    static final long MAX_ANY_PUBLIC_ID = 999999999999L;

    static long randomPublicId() {
        long range = PUBLIC_ID_MAX - PUBLIC_ID_MIN + 1;
        return PUBLIC_ID_MIN + Math.floorMod(new SecureRandom().nextLong(), range);
    }

    /**
     * Valid existing ID. Auto-generated IDs stay in
     * PUBLIC_ID_MIN..PUBLIC_ID_MAX, but an admin may assign any positive
     * ID up to 999999999999.
     */
    static boolean validPublicId(long id) {
        return id >= 1 && id <= MAX_ANY_PUBLIC_ID;
    }

    static boolean validAutoPublicId(long id) {
        return id >= PUBLIC_ID_MIN && id <= PUBLIC_ID_MAX;
    }

    /** Claims publicIds/{id} for uid. 0=ok, 1=taken by someone else, -1=error. */
    static int tryClaimPublicId(String idToken, String uid, long candidate) {
        try {
            JSONObject fields = new JSONObject();
            putStr(fields, "uid", uid);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            Response r = request("POST", Vault.firestoreBaseUrl()
                    + "publicIds?documentId=" + candidate, idToken, body);
            if (r.code >= 200 && r.code < 300) {
                return 0;
            }
            if (r.code == 409 || r.code == 400) {
                Response existing = request("GET", Vault.firestoreBaseUrl()
                        + "publicIds/" + candidate, idToken, null);
                if (existing.code == 200) {
                    JSONObject f = new JSONObject(existing.text).optJSONObject("fields");
                    String owner = f == null ? "" : str(f, "uid", "");
                    return owner.equals(uid) ? 0 : 1;
                }
                return r.code == 409 ? 1 : -1;
            }
            return -1;
        } catch (Exception ignored) {
            return -1;
        }
    }

    static void releasePublicId(String idToken, long id) {
        try {
            if (id > 0) {
                request("DELETE", Vault.firestoreBaseUrl() + "publicIds/" + id, idToken, null);
            }
        } catch (Exception ignored) {
        }
    }

    /**
     * Returns the account's public ID, claiming the preferred (locally
     * generated) one first and random ones after. 0 = could not claim.
     */
    static long assignPublicId(String idToken, String uid, long preferred, long existing) {
        if (validPublicId(existing)) {
            return existing;
        }
        if (validPublicId(preferred)) {
            int r = tryClaimPublicId(idToken, uid, preferred);
            if (r == 0) {
                return preferred;
            }
            if (r < 0) {
                return 0;
            }
        }
        for (int i = 0; i < 14; i++) {
            long candidate = randomPublicId();
            int r = tryClaimPublicId(idToken, uid, candidate);
            if (r == 0) {
                return candidate;
            }
            if (r < 0) {
                return 0;
            }
        }
        return 0;
    }

    static boolean deleteAvatar(String idToken, String uid) {
        try {
            Response r = request("DELETE",
                    Vault.firestoreBaseUrl() + "avatars/" + uid, idToken, null);
            return (r.code >= 200 && r.code < 300) || r.code == 404;
        } catch (Exception ignored) {
            return false;
        }
    }

    // ---------- avatar (Firestore bytes document) ----------
    // Avatars live in avatars/{uid} as one small JPEG bytes field.
    // Firebase Storage (and the paid Blaze plan) is not required.

    static boolean uploadAvatar(String idToken, String uid, byte[] jpeg) {
        try {
            if (jpeg == null || jpeg.length == 0 || jpeg.length > MAX_AVATAR_BYTES) {
                return false;
            }
            JSONObject fields = new JSONObject();
            putBytes(fields, "image", jpeg);
            putNum(fields, "updatedAt", System.currentTimeMillis());
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            String url = Vault.firestoreBaseUrl() + "avatars/" + uid
                    + "?updateMask.fieldPaths=image"
                    + "&updateMask.fieldPaths=updatedAt";
            Response r = request("PATCH", url, idToken, body);
            return r.code >= 200 && r.code < 300;
        } catch (Exception ignored) {
            return false;
        }
    }

    static byte[] downloadAvatar(String idToken, String uid) {
        try {
            Response r = request("GET",
                    Vault.firestoreBaseUrl() + "avatars/" + uid, idToken, null);
            if (r.code != 200) {
                return null;
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            if (fields == null) {
                return null;
            }
            byte[] image = bytes(fields, "image");
            if (image == null || image.length == 0 || image.length > MAX_AVATAR_BYTES) {
                return null;
            }
            return image;
        } catch (Exception ignored) {
            return null;
        }
    }

    // ---------- helpers ----------

    private static JSONObject fieldFilter(String path, String op, String value) throws Exception {
        JSONObject field = new JSONObject();
        field.put("fieldPath", path);
        JSONObject val = new JSONObject();
        val.put("stringValue", value);
        JSONObject ff = new JSONObject();
        ff.put("field", field);
        ff.put("op", op);
        ff.put("value", val);
        JSONObject wrapper = new JSONObject();
        wrapper.put("fieldFilter", ff);
        return wrapper;
    }

    private static void putStr(JSONObject fields, String name, String value) throws Exception {
        JSONObject v = new JSONObject();
        v.put("stringValue", value == null ? "" : value);
        fields.put(name, v);
    }

    private static void putBool(JSONObject fields, String name, boolean value) throws Exception {
        JSONObject v = new JSONObject();
        v.put("booleanValue", value);
        fields.put(name, v);
    }

    private static void putNum(JSONObject fields, String name, long value) throws Exception {
        JSONObject v = new JSONObject();
        v.put("integerValue", String.valueOf(value));
        fields.put(name, v);
    }


    private static void putTimestamp(JSONObject fields, String name, long millis)
            throws Exception {
        SimpleDateFormat format = new SimpleDateFormat(
                "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.ROOT);
        format.setTimeZone(TimeZone.getTimeZone("UTC"));
        JSONObject v = new JSONObject();
        v.put("timestampValue", format.format(new Date(millis)));
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

    private static String timestamp(JSONObject fields, String name) {
        JSONObject v = fields.optJSONObject(name);
        if (v == null) {
            return "";
        }
        String t = v.optString("timestampValue");
        if (t.length() > 16) {
            return t.substring(0, 10) + " " + t.substring(11, 16);
        }
        return t;
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
