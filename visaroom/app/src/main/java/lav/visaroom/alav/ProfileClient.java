package lav.visaroom.alav;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * Profiles, username search and official announcements on top of the
 * Firestore / Firebase Storage REST APIs. Every call carries the user's
 * Firebase ID token, so the security rules decide what is readable:
 * private profile data stays owner-only; the username directory only
 * ever contains a username and its owner's ID (no emails).
 */
final class ProfileClient {

    static final int USERNAME_OK = 0;
    static final int USERNAME_TAKEN = 1;
    static final int USERNAME_ERROR = -1;

    static final class SearchResult {
        String uid = "";
        String username = "";
    }

    static final class PublicProfile {
        String username = "";
        String bio = "";
        boolean hasAvatar;
        long avatarUpdatedAt;
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
            JSONObject mineBody = new JSONObject();
            mineBody.put("fields", mine);
            Response r1 = request("PATCH", Vault.firestoreBaseUrl() + "users/" + uid,
                    idToken, mineBody);
            if (r1.code < 200 || r1.code >= 300) {
                return false;
            }

            JSONObject pub = new JSONObject();
            putStr(pub, "username", data.username);
            putStr(pub, "bio", data.bio);
            putBool(pub, "hasAvatar", data.hasAvatar);
            putNum(pub, "avatarUpdatedAt", data.avatarUpdatedAt);
            JSONObject pubBody = new JSONObject();
            pubBody.put("fields", pub);
            Response r2 = request("PATCH", Vault.firestoreBaseUrl() + "profiles/" + uid,
                    idToken, pubBody);
            return r2.code >= 200 && r2.code < 300;
        } catch (Exception ignored) {
            return false;
        }
    }

    static int claimUsername(String idToken, String uid, String username,
                             String oldLower, boolean discoverable) {
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

    private static void deleteUsername(String idToken, String lower) {
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
                if (s.uid.length() > 0 && s.username.length() > 0) {
                    results.add(s);
                }
            }
            return results;
        } catch (Exception ignored) {
            return results;
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

    // ---------- avatar (Firebase Storage) ----------

    static boolean uploadAvatar(String idToken, String uid, byte[] jpeg) {
        try {
            String object = URLEncoder.encode("users/" + uid + "/avatar.jpg", "UTF-8");
            String url = Vault.storageBaseUrl() + "?uploadType=media&name=" + object;
            HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
            c.setRequestMethod("POST");
            c.setConnectTimeout(20000);
            c.setReadTimeout(20000);
            c.setDoOutput(true);
            c.setRequestProperty("Content-Type", "image/jpeg");
            AuthClient.applyAndroidHeaders(c);
            c.setRequestProperty("Authorization", "Bearer " + idToken);
            OutputStream out = c.getOutputStream();
            out.write(jpeg);
            out.close();
            int code = c.getResponseCode();
            c.disconnect();
            return code >= 200 && code < 300;
        } catch (Exception ignored) {
            return false;
        }
    }

    static byte[] downloadAvatar(String idToken, String uid) {
        try {
            String object = URLEncoder.encode("users/" + uid + "/avatar.jpg", "UTF-8");
            String url = Vault.storageBaseUrl() + "/" + object + "?alt=media";
            HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
            c.setRequestMethod("GET");
            c.setConnectTimeout(15000);
            c.setReadTimeout(15000);
            AuthClient.applyAndroidHeaders(c);
            c.setRequestProperty("Authorization", "Bearer " + idToken);
            if (c.getResponseCode() != 200) {
                c.disconnect();
                return null;
            }
            InputStream in = c.getInputStream();
            ByteArrayOutputStream buffer = new ByteArrayOutputStream();
            byte[] chunk = new byte[8192];
            int n;
            int total = 0;
            while ((n = in.read(chunk)) != -1) {
                total += n;
                if (total > 5 * 1024 * 1024) {
                    in.close();
                    c.disconnect();
                    return null;
                }
                buffer.write(chunk, 0, n);
            }
            in.close();
            c.disconnect();
            return buffer.toByteArray();
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
