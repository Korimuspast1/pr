package lav.visaroom.alav;

import android.util.Base64;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
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
 * the account email is not copied into Firestore at all, the PBKDF2
 * 2FA verifier lives in an owner-only security document, and the
 * username directory only ever contains a username and its owner's ID.
 */
final class ProfileClient {

    static final int USERNAME_OK = 0;
    static final int USERNAME_TAKEN = 1;
    static final int USERNAME_ERROR = -1;
    static final int MAX_AVATAR_BYTES = 120000;
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

    /**
     * Owner profile load.  The moderation fields live in users/{uid};
     * the 2FA verifier is loaded separately from the owner-only
     * users/{uid}/security/twoFa document.  A legacy verifier still
     * present in users/{uid} is accepted here once and is migrated by
     * the next successful saveMyProfile() call.
     */
    static boolean loadMyProfile(String idToken, String uid, ProfileData out) {
        if (out == null) {
            return false;
        }
        out.cloudProfileLoaded = false;
        out.cloudStateKnown = false;
        out.twoFaStateKnown = false;
        if (idToken == null || !AuthClient.validUid(uid)) {
            return false;
        }
        try {
            Response r = request("GET", Vault.firestoreBaseUrl() + "users/" + uid, idToken, null);
            if (r.code == 404) {
                int security = probeTwoFaDocument(idToken, uid, out);
                out.twoFaStateKnown = security >= 0;
                out.cloudStateKnown = security >= 0;
                return false;
            }
            if (r.code != 200) {
                return false;
            }
            out.cloudProfileLoaded = true;
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            if (fields == null) {
                int security = probeTwoFaDocument(idToken, uid, out);
                out.twoFaStateKnown = security >= 0;
                out.cloudStateKnown = security >= 0;
                return security >= 0;
            }
            parsePrivateProfile(fields, out, true);
            // Legacy values are only a fallback until the dedicated
            // security document has been checked.  When that document
            // exists it is authoritative; when it is definitively absent
            // the legacy values above remain available for migration.
            int security = probeTwoFaDocument(idToken, uid, out);
            if (security < 0) {
                return false;
            }
            out.twoFaStateKnown = true;
            out.cloudStateKnown = true;
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    /**
     * Moderation load for the administrator.  A Firestore document mask
     * requests only moderation fields, so a legacy email/2FA verifier
     * in an unmigrated users/{uid} document is not even transmitted to
     * the admin client, and migrated accounts keep those values in a
     * document the admin cannot read at all.
     */
    static boolean loadProfileForAdmin(String idToken, String uid, ProfileData out) {
        if (out == null || idToken == null || !AuthClient.validUid(uid)) {
            return false;
        }
        out.cloudProfileLoaded = false;
        out.cloudStateKnown = false;
        out.twoFaStateKnown = false;
        try {
            String url = Vault.firestoreBaseUrl() + "users/" + uid
                    + "?mask.fieldPaths=username"
                    + "&mask.fieldPaths=bio"
                    + "&mask.fieldPaths=hasAvatar"
                    + "&mask.fieldPaths=avatarUpdatedAt"
                    + "&mask.fieldPaths=discoverable"
                    + "&mask.fieldPaths=publicId"
                    + "&mask.fieldPaths=showBio"
                    + "&mask.fieldPaths=showAvatar"
                    + "&mask.fieldPaths=showId"
                    + "&mask.fieldPaths=blocked"
                    + "&mask.fieldPaths=blockedAt"
                    + "&mask.fieldPaths=verified";
            Response r = request("GET", url, idToken, null);
            if (r.code != 200) {
                return false;
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            if (fields != null) {
                parsePrivateProfile(fields, out, false);
            }
            out.email = "";
            out.twoFaEnabled = false;
            out.twoFaVerifier = "";
            out.cloudProfileLoaded = true;
            out.cloudStateKnown = true;
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    private static void parsePrivateProfile(JSONObject fields, ProfileData out,
                                            boolean includeLegacySecrets) {
        if (includeLegacySecrets) {
            out.email = bounded(str(fields, "email", out.email), 320);
            out.twoFaEnabled = bool(fields, "twoFaEnabled", out.twoFaEnabled);
            out.twoFaVerifier = bounded(
                    str(fields, "twoFaVerifier", out.twoFaVerifier), 256);
        }
        String username = bounded(str(fields, "username", out.username), 24);
        out.username = validUsername(username) ? username : "";
        out.bio = bounded(str(fields, "bio", out.bio), 160);
        out.hasAvatar = bool(fields, "hasAvatar", out.hasAvatar);
        out.avatarUpdatedAt = Math.max(0,
                num(fields, "avatarUpdatedAt", out.avatarUpdatedAt));
        out.discoverable = bool(fields, "discoverable", out.discoverable);
        long publicId = num(fields, "publicId", out.publicId);
        out.publicId = validPublicId(publicId) ? publicId : 0;
        out.showBio = bool(fields, "showBio", out.showBio);
        out.showAvatar = bool(fields, "showAvatar", out.showAvatar);
        out.showId = bool(fields, "showId", out.showId);
        out.blocked = bool(fields, "blocked", out.blocked);
        out.blockedAt = Math.max(0, num(fields, "blockedAt", out.blockedAt));
        out.verified = bool(fields, "verified", out.verified);
    }

    /** 1 = security document exists, 0 = definitively absent, -1 = error. */
    private static int probeTwoFaDocument(String idToken, String uid,
                                          ProfileData out) {
        try {
            Response r = request("GET", twoFaUrl(uid), idToken, null);
            if (r.code == 404) {
                return 0;
            }
            if (r.code != 200) {
                return -1;
            }
            if (out != null) {
                JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
                if (fields == null) {
                    out.twoFaEnabled = false;
                    out.twoFaVerifier = "";
                } else {
                    out.twoFaEnabled = bool(fields, "enabled", false);
                    out.twoFaVerifier = bounded(str(fields, "verifier", ""), 256);
                }
            }
            return 1;
        } catch (Exception ignored) {
            return -1;
        }
    }

    private static String twoFaUrl(String uid) {
        return Vault.firestoreBaseUrl() + "users/" + uid + "/security/twoFa";
    }

    /**
     * Owner save.  The 2FA state is written first to its owner-only
     * document; only then is users/{uid} patched.  The users patch uses
     * an update mask that also deletes the legacy email/twoFaEnabled/
     * twoFaVerifier fields, completing migration without ever having a
     * moment where the only copy of the verifier has been erased before
     * its replacement exists.
     */
    static boolean saveMyProfile(String idToken, String uid, ProfileData data) {
        if (idToken == null || !AuthClient.validUid(uid) || data == null) {
            return false;
        }
        try {
            if (data.publicId != 0) {
                int claim = validPublicId(data.publicId)
                        ? verifyOrClaimPublicId(idToken, uid, data.publicId) : 1;
                if (claim == 1) {
                    long assigned = assignPublicId(idToken, uid, 0, 0);
                    if (assigned <= 0) {
                        return false;
                    }
                    data.publicId = assigned;
                } else if (claim != 0) {
                    return false;
                }
            }
            if (!saveTwoFaDocument(idToken, uid, data)) {
                return false;
            }
            String mask = privateProfileMask(data.publicId > 0)
                    + "&updateMask.fieldPaths=email"
                    + "&updateMask.fieldPaths=twoFaEnabled"
                    + "&updateMask.fieldPaths=twoFaVerifier";
            JSONObject mineBody = new JSONObject();
            mineBody.put("fields", privateProfileFields(data));
            Response r1 = request("PATCH", Vault.firestoreBaseUrl() + "users/" + uid
                    + "?" + mask, idToken, mineBody);
            if (r1.code < 200 || r1.code >= 300) {
                return false;
            }
            boolean saved = savePublicProfile(idToken, uid, data);
            if (saved) {
                data.cloudProfileLoaded = true;
                data.cloudStateKnown = true;
                data.twoFaStateKnown = true;
            }
            return saved;
        } catch (Exception ignored) {
            return false;
        }
    }

    /**
     * Administrator save.  It patches only moderation/profile fields
     * and uses no deletion masks for legacy secrets, so an unmigrated
     * account's email/verifier fields are preserved untouched while a
     * migrated account's verifier is never in this document at all.
     */
    static boolean saveProfileAsAdmin(String idToken, String uid, ProfileData data) {
        if (idToken == null || !AuthClient.validUid(uid) || data == null) {
            return false;
        }
        try {
            if (data.publicId > 0
                    && verifyPublicIdClaim(idToken, uid, data.publicId) != 0) {
                return false;
            }
            JSONObject body = new JSONObject();
            body.put("fields", privateProfileFields(data));
            Response r1 = request("PATCH", Vault.firestoreBaseUrl() + "users/" + uid
                    + "?" + privateProfileMask(data.publicId > 0), idToken, body);
            return r1.code >= 200 && r1.code < 300
                    && savePublicProfile(idToken, uid, data);
        } catch (Exception ignored) {
            return false;
        }
    }

    private static boolean saveTwoFaDocument(String idToken, String uid,
                                             ProfileData data) throws Exception {
        String verifier = data.twoFaVerifier == null ? "" : data.twoFaVerifier;
        if (data.twoFaEnabled && !TwoFa.isValidVerifier(verifier)) {
            return false;
        }
        int existing = data.twoFaStateKnown
                ? 0 : probeTwoFaDocument(idToken, uid, null);
        if (!data.twoFaStateKnown && existing < 0) {
            return false;
        }
        // A brand-new disabled account does not need an extra document.
        if (!data.twoFaEnabled && !data.twoFaStateKnown && existing == 0) {
            data.twoFaStateKnown = true;
            return true;
        }
        JSONObject fields = new JSONObject();
        putBool(fields, "enabled", data.twoFaEnabled);
        if (data.twoFaEnabled) {
            putStr(fields, "verifier", verifier);
        }
        JSONObject body = new JSONObject();
        body.put("fields", fields);
        // The mask is essential when disabling 2FA: omitting verifier
        // from the body must delete the old verifier, not preserve it.
        Response r = request("PATCH", twoFaUrl(uid)
                + "?updateMask.fieldPaths=enabled"
                + "&updateMask.fieldPaths=verifier", idToken, body);
        if (r.code >= 200 && r.code < 300) {
            data.twoFaStateKnown = true;
            return true;
        }
        return false;
    }

    private static JSONObject privateProfileFields(ProfileData data) throws Exception {
        JSONObject mine = new JSONObject();
        putStr(mine, "username", data.username == null ? "" : data.username);
        putStr(mine, "bio", data.bio == null ? "" : data.bio);
        putBool(mine, "hasAvatar", data.hasAvatar);
        putNum(mine, "avatarUpdatedAt", Math.max(0, data.avatarUpdatedAt));
        putBool(mine, "discoverable", data.discoverable);
        if (data.publicId > 0) {
            putNum(mine, "publicId", data.publicId);
        }
        putBool(mine, "showBio", data.showBio);
        putBool(mine, "showAvatar", data.showAvatar);
        putBool(mine, "showId", data.showId);
        putBool(mine, "blocked", data.blocked);
        putNum(mine, "blockedAt", Math.max(0, data.blockedAt));
        putBool(mine, "verified", data.verified);
        return mine;
    }

    private static String privateProfileMask(boolean includePublicId) {
        String mask = "updateMask.fieldPaths=username"
                + "&updateMask.fieldPaths=bio"
                + "&updateMask.fieldPaths=hasAvatar"
                + "&updateMask.fieldPaths=avatarUpdatedAt"
                + "&updateMask.fieldPaths=discoverable";
        if (includePublicId) {
            mask += "&updateMask.fieldPaths=publicId";
        }
        return mask
                + "&updateMask.fieldPaths=showBio"
                + "&updateMask.fieldPaths=showAvatar"
                + "&updateMask.fieldPaths=showId"
                + "&updateMask.fieldPaths=blocked"
                + "&updateMask.fieldPaths=blockedAt"
                + "&updateMask.fieldPaths=verified";
    }

    private static boolean savePublicProfile(String idToken, String uid,
                                             ProfileData data) throws Exception {
        // Public copy: privacy settings are applied here, so a hidden
        // bio/photo/ID is not present in the public document at all.
        JSONObject pub = new JSONObject();
        putStr(pub, "username", data.username == null ? "" : data.username);
        putStr(pub, "bio", data.showBio && data.bio != null ? data.bio : "");
        putBool(pub, "hasAvatar", data.showAvatar && data.hasAvatar);
        putNum(pub, "avatarUpdatedAt",
                data.showAvatar ? Math.max(0, data.avatarUpdatedAt) : 0);
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
        // Include publicId in the mask even when it is omitted above,
        // so turning "Show my ID" off deletes a previously public ID.
        String mask = "updateMask.fieldPaths=username"
                + "&updateMask.fieldPaths=bio"
                + "&updateMask.fieldPaths=hasAvatar"
                + "&updateMask.fieldPaths=avatarUpdatedAt"
                + "&updateMask.fieldPaths=publicId"
                + "&updateMask.fieldPaths=showBio"
                + "&updateMask.fieldPaths=showAvatar"
                + "&updateMask.fieldPaths=showId"
                + "&updateMask.fieldPaths=blocked"
                + "&updateMask.fieldPaths=verified";
        Response r2 = request("PATCH", Vault.firestoreBaseUrl() + "profiles/" + uid
                        + "?" + mask,
                idToken, pubBody);
        return r2.code >= 200 && r2.code < 300;
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
        if (idToken == null || !AuthClient.validUid(uid) || data == null) {
            return false;
        }
        loadMyProfile(idToken, uid, data);
        if (data.blocked) {
            // Callers check data.blocked and stop before opening chats.
            // A blocked owner must not recreate search/avatar documents.
            return false;
        }
        if (validPublicId(data.publicId)) {
            int claim = verifyOrClaimPublicId(idToken, uid, data.publicId);
            if (claim == 1) {
                // The stored/local ID belongs to another account.  Do
                // not copy it into this profile; claim a free auto ID.
                long assigned = assignPublicId(idToken, uid, 0, 0);
                if (assigned <= 0) {
                    return false;
                }
                data.publicId = assigned;
            } else if (claim != 0) {
                // Includes an admin-range ID whose claim document is
                // missing.  Preserve the account state and fail closed
                // instead of silently renumbering the user.
                return false;
            }
        } else {
            long assigned = assignPublicId(idToken, uid, data.publicId, 0);
            if (assigned > 0) {
                data.publicId = assigned;
            }
        }
        boolean saved = saveMyProfile(idToken, uid, data);
        if (saved && !data.blocked && data.username != null
                && data.username.trim().length() > 0 && data.discoverable) {
            claimUsername(idToken, uid, data.username,
                    data.usernameLower(), true,
                    data.showId ? data.publicId : 0, data.verified);
        }
        return saved;
    }

    static int claimUsername(String idToken, String uid, String username,
                             String oldLower, boolean discoverable, long publicId,
                             boolean verified) {
        String cleanUsername = username == null ? "" : username.trim();
        String lower = cleanUsername.toLowerCase(java.util.Locale.ROOT);
        String cleanOldLower = oldLower == null ? ""
                : oldLower.trim().toLowerCase(java.util.Locale.ROOT);
        try {
            if (idToken == null || !AuthClient.validUid(uid)) {
                return USERNAME_ERROR;
            }
            if (lower.length() == 0 || !discoverable) {
                if (validUsernameLower(cleanOldLower)) {
                    deleteUsername(idToken, cleanOldLower);
                }
                return USERNAME_OK;
            }
            if (!validClaimUsername(cleanUsername)
                    || !lower.equals(cleanUsername.toLowerCase(java.util.Locale.ROOT))) {
                return USERNAME_ERROR;
            }
            if (publicId > 0 && (!validPublicId(publicId)
                    || verifyPublicIdClaim(idToken, uid, publicId) != 0)) {
                return USERNAME_ERROR;
            }
            if (!lower.equals(cleanOldLower)) {
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
            putStr(fields, "username", cleanUsername);
            putStr(fields, "usernameLower", lower);
            if (publicId > 0) {
                putNum(fields, "publicId", publicId);
            }
            putBool(fields, "verified", verified);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            // The publicId mask deletes a directory ID that has since
            // been hidden; without a mask an omitted field can remain.
            String mask = "updateMask.fieldPaths=uid"
                    + "&updateMask.fieldPaths=username"
                    + "&updateMask.fieldPaths=usernameLower"
                    + "&updateMask.fieldPaths=publicId"
                    + "&updateMask.fieldPaths=verified";
            Response r = request("PATCH", Vault.firestoreBaseUrl() + "usernames/" + lower
                            + "?" + mask,
                    idToken, body);
            if (r.code < 200 || r.code >= 300) {
                return USERNAME_ERROR;
            }
            if (validUsernameLower(cleanOldLower) && !cleanOldLower.equals(lower)) {
                deleteUsername(idToken, cleanOldLower);
            }
            return USERNAME_OK;
        } catch (Exception ignored) {
            return USERNAME_ERROR;
        }
    }

    static void deleteUsername(String idToken, String lower) {
        try {
            String clean = lower == null ? ""
                    : lower.trim().toLowerCase(java.util.Locale.ROOT);
            if (idToken == null || !validUsernameLower(clean)) {
                return;
            }
            request("DELETE", Vault.firestoreBaseUrl() + "usernames/" + clean, idToken, null);
        } catch (Exception ignored) {
        }
    }

    // ---------- search & public profiles ----------

    static List<SearchResult> searchUsernames(String idToken, String queryLower) {
        List<SearchResult> results = new ArrayList<>();
        try {
            if (idToken == null || queryLower == null
                    || !queryLower.matches("[a-z0-9_]{1,24}")) {
                return results;
            }
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
                s.uid = bounded(str(fields, "uid", ""), 128);
                s.username = bounded(str(fields, "username", ""), 24);
                String directoryLower = bounded(
                        str(fields, "usernameLower", ""), 24);
                long directoryPublicId = num(fields, "publicId", 0);
                if (!AuthClient.validUid(s.uid)
                        || !validClaimUsername(s.username)
                        || !directoryLower.equals(
                                s.username.toLowerCase(java.util.Locale.ROOT))) {
                    continue;
                }
                // The directory is an index, not the authority for a
                // verified badge or a visible numeric ID.  Confirm both
                // against the public profile so stale/squatted index
                // rows cannot resurrect a hidden ID or removed badge.
                PublicProfile profile = loadPublicProfile(idToken, s.uid);
                if (profile.username.length() > 0
                        && !profile.username.equals(s.username)) {
                    continue;
                }
                if (profile.blocked) {
                    continue;
                }
                s.publicId = profile.showId
                        && profile.publicId == directoryPublicId
                        ? directoryPublicId : 0;
                s.verified = profile.verified;
                results.add(s);
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
            String uid = fields == null ? "" : bounded(str(fields, "uid", ""), 128);
            return AuthClient.validUid(uid) ? uid : "";
        } catch (Exception ignored) {
            return "";
        }
    }

    static String loadUidByUsername(String idToken, String usernameLower) {
        try {
            if (idToken == null || !validUsernameLower(usernameLower)) {
                return "";
            }
            Response r = request("GET", Vault.firestoreBaseUrl() + "usernames/" + usernameLower,
                    idToken, null);
            if (r.code != 200) {
                return "";
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            String uid = fields == null ? "" : bounded(str(fields, "uid", ""), 128);
            return AuthClient.validUid(uid) ? uid : "";
        } catch (Exception ignored) {
            return "";
        }
    }

    static PublicProfile loadPublicProfile(String idToken, String uid) {
        PublicProfile p = new PublicProfile();
        try {
            if (idToken == null || !AuthClient.validUid(uid)) {
                return p;
            }
            Response r = request("GET", Vault.firestoreBaseUrl() + "profiles/" + uid,
                    idToken, null);
            if (r.code != 200) {
                return p;
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            if (fields != null) {
                String username = bounded(str(fields, "username", ""), 24);
                p.username = validUsername(username) ? username : "";
                p.bio = bounded(str(fields, "bio", ""), 160);
                p.hasAvatar = bool(fields, "hasAvatar", false);
                p.avatarUpdatedAt = Math.max(0, num(fields, "avatarUpdatedAt", 0));
                long publicId = num(fields, "publicId", 0);
                p.publicId = validPublicId(publicId) ? publicId : 0;
                p.showBio = bool(fields, "showBio", true);
                p.showAvatar = bool(fields, "showAvatar", true);
                p.showId = bool(fields, "showId", true);
                p.blocked = bool(fields, "blocked", false);
                p.verified = bool(fields, "verified", false);
                // Enforce privacy again in the client.  The writer and
                // rules should already have omitted hidden values, but
                // a stale or hand-edited public document must not make
                // the UI display them anyway.
                if (!p.showBio) {
                    p.bio = "";
                }
                if (!p.showAvatar) {
                    p.hasAvatar = false;
                }
                if (!p.showId) {
                    p.publicId = 0;
                }
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
                JSONObject document = docs.getJSONObject(i);
                JSONObject fields = document.optJSONObject("fields");
                if (fields == null) {
                    continue;
                }
                String name = bounded(document.optString("name", ""), 1024);
                int slash = name.lastIndexOf('/');
                String announcementId = slash >= 0 ? name.substring(slash + 1) : "";
                String authorUid = bounded(str(fields, "authorUid", ""), 128);
                if (announcementId.length() > 0
                        && !announcementId.matches("^a[0-9]+_[0-9a-f]{16}$")) {
                    continue;
                }
                if (authorUid.length() > 0 && !AuthClient.validUid(authorUid)) {
                    continue;
                }
                String title = bounded(str(fields, "title", ""), 200);
                String text = bounded(
                        str(fields, "text", str(fields, "body", "")), 4000);
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

    /**
     * Verifies that publicIds/{id} names this UID.
     * 0=owned by uid, 1=owned by someone else, 2=claim missing, -1=error.
     */
    static int verifyPublicIdClaim(String idToken, String uid, long publicId) {
        try {
            if (idToken == null || !AuthClient.validUid(uid)
                    || !validPublicId(publicId)) {
                return -1;
            }
            Response r = request("GET", Vault.firestoreBaseUrl()
                    + "publicIds/" + publicId, idToken, null);
            if (r.code == 404) {
                return 2;
            }
            if (r.code != 200) {
                return -1;
            }
            JSONObject fields = new JSONObject(r.text).optJSONObject("fields");
            String owner = fields == null ? "" : bounded(str(fields, "uid", ""), 128);
            if (!AuthClient.validUid(owner)) {
                return 1;
            }
            return owner.equals(uid) ? 0 : 1;
        } catch (Exception ignored) {
            return -1;
        }
    }

    /**
     * Auto-range IDs may be claimed by their owner when the claim
     * document is missing (older accounts).  An administrator-range ID
     * is never self-claimed here: a missing claim is an error state to
     * repair through the admin flow, not permission to take the number.
     */
    static int verifyOrClaimPublicId(String idToken, String uid, long publicId) {
        int verified = verifyPublicIdClaim(idToken, uid, publicId);
        if (verified != 2) {
            return verified;
        }
        if (!validAutoPublicId(publicId)) {
            return 2;
        }
        return tryClaimPublicId(idToken, uid, publicId);
    }

    /** Claims publicIds/{id} for uid. 0=ok, 1=taken by someone else, -1=error. */
    static int tryClaimPublicId(String idToken, String uid, long candidate) {
        try {
            if (idToken == null || !AuthClient.validUid(uid)
                    || !validPublicId(candidate)) {
                return -1;
            }
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
                    String owner = f == null ? "" : bounded(str(f, "uid", ""), 128);
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
            if (idToken != null && validPublicId(id)) {
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
        if (validAutoPublicId(preferred)) {
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
            if (idToken == null || !AuthClient.validUid(uid)) {
                return false;
            }
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
            if (idToken == null || !AuthClient.validUid(uid)
                    || jpeg == null || jpeg.length == 0 || jpeg.length > MAX_AVATAR_BYTES) {
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
            if (idToken == null || !AuthClient.validUid(uid)) {
                return null;
            }
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

    private static String bounded(String value, int maxChars) {
        return value == null || value.length() > maxChars ? "" : value;
    }

    private static boolean validUsername(String username) {
        return username != null
                && (username.length() == 0 || ProfileData.isValidUsername(username));
    }

    private static boolean validClaimUsername(String username) {
        return username != null && username.matches("[A-Za-z0-9_]{3,24}");
    }

    private static boolean validUsernameLower(String usernameLower) {
        return usernameLower != null && usernameLower.matches("[a-z0-9_]{3,24}");
    }

    private static byte[] bytes(JSONObject fields, String name) {
        JSONObject v = fields.optJSONObject(name);
        if (v == null) {
            return null;
        }
        Object raw = v.opt("bytesValue");
        if (!(raw instanceof String)) {
            return null;
        }
        String encoded = (String) raw;
        int maxEncoded = ((MAX_AVATAR_BYTES + 2) / 3) * 4 + 4;
        if (encoded.length() == 0 || encoded.length() > maxEncoded) {
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

    private static String timestamp(JSONObject fields, String name) {
        JSONObject v = fields.optJSONObject(name);
        if (v == null) {
            return "";
        }
        Object raw = v.opt("timestampValue");
        if (!(raw instanceof String)) {
            return "";
        }
        String t = (String) raw;
        if (t.length() < 19 || t.length() > 40
                || !t.substring(0, 19).matches(
                        "[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}")) {
            return "";
        }
        try {
            SimpleDateFormat parser = new SimpleDateFormat(
                    "yyyy-MM-dd'T'HH:mm:ss", Locale.ROOT);
            parser.setLenient(false);
            parser.setTimeZone(TimeZone.getTimeZone("UTC"));
            Date parsed = parser.parse(t.substring(0, 19));
            if (parsed == null || parsed.getTime() < 1577836800000L
                    || parsed.getTime() > 4102444800000L) {
                return "";
            }
            SimpleDateFormat output = new SimpleDateFormat(
                    "yyyy-MM-dd HH:mm", Locale.ROOT);
            output.setTimeZone(TimeZone.getTimeZone("UTC"));
            return output.format(parsed);
        } catch (Exception ignored) {
            return "";
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
            // The largest legitimate response here is a 716800-byte
            // avatar in base64 plus Firestore JSON overhead.
            r.text = SafeHttp.readText(stream, 2 * 1024 * 1024);
            return r;
        } finally {
            c.disconnect();
        }
    }
}
