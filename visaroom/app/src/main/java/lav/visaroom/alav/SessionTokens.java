package lav.visaroom.alav;

import android.content.Context;

/**
 * Process-only cache for the active Firebase ID token.
 *
 * The durable refresh token remains encrypted in TokenStore. Keeping
 * the short-lived ID token only in RAM lets a chat opened from the
 * chat list start synchronizing immediately instead of paying for a
 * second refresh request in every Activity, and lets notification
 * checks share the same refresh with the foreground UI.
 */
final class SessionTokens {

    private static final long CACHE_MS = 40L * 60L * 1000L;

    private static String cachedUid = "";
    private static String cachedToken;
    private static long cachedAt;

    private SessionTokens() {
    }

    static synchronized String cached(String uid) {
        if (uid == null || uid.length() == 0 || cachedToken == null
                || !uid.equals(cachedUid)
                || System.currentTimeMillis() - cachedAt >= CACHE_MS) {
            return null;
        }
        return cachedToken;
    }

    static synchronized void put(String uid, String idToken) {
        if (uid == null || uid.length() == 0
                || idToken == null || idToken.length() == 0) {
            return;
        }
        cachedUid = uid;
        cachedToken = idToken;
        cachedAt = System.currentTimeMillis();
    }

    static synchronized void clear() {
        cachedUid = "";
        cachedToken = null;
        cachedAt = 0;
    }

    /**
     * Returns an ID token for the expected account. This method does
     * network I/O when the RAM cache is cold and must be called from a
     * background thread. A refresh result for another UID is never
     * accepted or written over the expected account's slot.
     */
    static synchronized String token(Context context, String expectedUid) {
        if (context == null) {
            return null;
        }
        Context app = context.getApplicationContext();
        AuthClient.init(app);
        String storedUid = TokenStore.uid(app);
        String uid = expectedUid == null || expectedUid.length() == 0
                ? storedUid : expectedUid;
        if (uid == null) {
            uid = "";
        }
        String hit = cached(uid);
        if (hit != null) {
            return hit;
        }
        String refreshToken = TokenStore.refreshToken(app);
        if (refreshToken == null || refreshToken.length() == 0) {
            return null;
        }
        AuthClient.Result result = AuthClient.refresh(refreshToken);
        if (!result.ok || result.uid.length() == 0
                || (uid.length() > 0 && !uid.equals(result.uid))) {
            return null;
        }
        String tokenEmail = AuthClient.emailFromIdToken(result.idToken);
        String accountEmail = tokenEmail.length() > 0
                ? tokenEmail : TokenStore.email(app);
        if (!TokenStore.saveRefreshedSession(app, result.uid,
                accountEmail, result.refreshToken)) {
            return null;
        }
        put(result.uid, result.idToken);
        return result.idToken;
    }
}
