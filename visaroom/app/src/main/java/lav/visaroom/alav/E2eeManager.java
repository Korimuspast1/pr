package lav.visaroom.alav;

import android.content.Context;

import java.util.Arrays;

/**
 * Coordinates device-local E2EE identities, public-key publication,
 * password-encrypted key backup/restore and peer identity pinning.
 * All methods that touch Firestore must be called off the UI thread.
 */
final class E2eeManager {
    static final int STATE_READY = 0;
    static final int STATE_NEEDS_RESTORE = 1;
    static final int STATE_CONFLICT = 2;
    static final int STATE_UNAVAILABLE = 3;

    static final int PEER_READY = 0;
    static final int PEER_NO_KEYS = 1;
    static final int PEER_KEY_CHANGED = 2;
    static final int PEER_INVALID = 3;
    static final int PEER_UNAVAILABLE = 4;

    private static final long PREPARATION_CACHE_MS = 5L * 60L * 1000L;
    private static final long PEER_BUNDLE_CACHE_MS = 5L * 60L * 1000L;
    private static final Object LOCK = new Object();

    private static E2eeCrypto.Session cachedSession;
    private static Preparation cachedPreparation;
    private static String cachedUid = "";
    private static long cachedPreparedAt;

    private E2eeManager() {
    }

    static final class Preparation {
        int state = STATE_UNAVAILABLE;
        E2eeCrypto.Session session;
        E2eeCrypto.PublicBundle cloudBundle;
        boolean newlyCreated;
        boolean backupKnown;
        boolean backupExists;
        boolean published;
    }

    static final class PeerResult {
        int state = PEER_UNAVAILABLE;
        E2eeCrypto.PublicBundle bundle;
    }

    static E2eeCrypto.Session localSession(Context context, String uid) {
        if (context == null || uid == null || uid.length() == 0) {
            return null;
        }
        synchronized (LOCK) {
            if (cachedSession != null && uid.equals(cachedUid)
                    && cachedSession.usable()) {
                return cachedSession;
            }
        }
        E2eeCrypto.LocalIdentity identity = E2eeKeyStore.loadIdentity(context, uid);
        if (identity == null) {
            return null;
        }
        E2eeCrypto.Session session = new E2eeCrypto.Session(uid, identity);
        if (!session.usable()) {
            return null;
        }
        synchronized (LOCK) {
            cachedSession = session;
            cachedUid = uid;
        }
        return session;
    }

    static Preparation prepareOwn(Context context, String idToken, String uid) {
        return prepareOwn(context, idToken, uid, false);
    }

    static synchronized Preparation prepareOwn(Context context, String idToken, String uid,
                                  boolean force) {
        Preparation out = new Preparation();
        if (context == null || idToken == null || uid == null || uid.length() == 0) {
            return out;
        }
        long now = System.currentTimeMillis();
        synchronized (LOCK) {
            if (!force && uid.equals(cachedUid) && cachedPreparation != null
                    && cachedSession != null && cachedSession.usable()
                    && now - cachedPreparedAt >= 0
                    && now - cachedPreparedAt < PREPARATION_CACHE_MS) {
                return cachedPreparation;
            }
        }

        E2eeCrypto.LocalIdentity identity = E2eeKeyStore.loadIdentity(context, uid);
        VrProto.E2eeBundleResult cloud = VrProto.loadE2eeBundle(idToken, uid);
        VrProto.E2eeBackupResult backup = VrProto.loadE2eeBackup(idToken, uid);
        out.backupKnown = backup.ok;
        out.backupExists = backup.ok && backup.exists;
        if (cloud.ok && cloud.exists) {
            out.cloudBundle = cloud.bundle;
        }

        if (identity != null) {
            if (identity.rotatePreKeyIfNeeded(now)) {
                E2eeKeyStore.saveIdentity(context, uid, identity);
                clearCache(uid);
            }
            E2eeCrypto.Session session = newSession(uid, identity);
            out.session = session;
            if (session == null) {
                return out;
            }
            if (!cloud.ok) {
                out.state = STATE_READY;
                cachePreparation(uid, out, now);
                return out;
            }
            if (!cloud.exists) {
                out.published = publishOwn(idToken, session);
                out.state = STATE_READY;
                cachePreparation(uid, out, now);
                return out;
            }
            if (!Arrays.equals(cloud.bundle.signingPublic,
                    session.ownBundle.signingPublic)) {
                out.state = STATE_CONFLICT;
                cachePreparation(uid, out, now);
                return out;
            }
            if (!cloud.bundle.preKeyId.equals(session.ownBundle.preKeyId)) {
                if (identity.findPreKey(cloud.bundle.preKeyId) != null) {
                    out.published = publishOwn(idToken, session);
                    out.state = STATE_READY;
                } else {
                    out.state = STATE_CONFLICT;
                }
                cachePreparation(uid, out, now);
                return out;
            }
            out.state = STATE_READY;
            out.published = true;
            cachePreparation(uid, out, now);
            return out;
        }

        if (!cloud.ok) {
            out.state = STATE_UNAVAILABLE;
            return out;
        }
        if (cloud.exists) {
            out.state = STATE_NEEDS_RESTORE;
            synchronized (LOCK) {
                cachedUid = uid;
                cachedPreparation = out;
                cachedPreparedAt = now;
                cachedSession = null;
            }
            return out;
        }

        identity = E2eeCrypto.generateIdentity(now);
        if (identity == null || !E2eeKeyStore.saveIdentity(context, uid, identity)) {
            out.state = STATE_UNAVAILABLE;
            return out;
        }
        E2eeCrypto.Session session = newSession(uid, identity);
        out.session = session;
        out.newlyCreated = session != null;
        out.state = session == null ? STATE_UNAVAILABLE : STATE_READY;
        out.published = session != null && publishOwn(idToken, session);
        cachePreparation(uid, out, now);
        return out;
    }

    static boolean publishOwn(String idToken, E2eeCrypto.Session session) {
        if (idToken == null || session == null || !session.usable()) {
            return false;
        }
        E2eeCrypto.PublicBundle fresh = session.identity.publicBundle(
                session.myUid, System.currentTimeMillis());
        return fresh != null && VrProto.publishE2eeBundle(
                idToken, session.myUid, fresh);
    }

    static boolean saveBackup(Context context, String idToken, String uid,
                              char[] password) {
        if (context == null || idToken == null || uid == null || password == null) {
            return false;
        }
        E2eeCrypto.LocalIdentity identity = E2eeKeyStore.loadIdentity(context, uid);
        E2eeCrypto.BackupData backup = identity == null ? null
                : E2eeCrypto.encryptBackup(uid, identity, password,
                        System.currentTimeMillis());
        boolean ok = backup != null && VrProto.saveE2eeBackup(idToken, uid, backup);
        if (ok) {
            synchronized (LOCK) {
                if (uid.equals(cachedUid) && cachedPreparation != null) {
                    cachedPreparation.backupKnown = true;
                    cachedPreparation.backupExists = true;
                }
            }
        }
        return ok;
    }

    static boolean restoreBackup(Context context, String idToken, String uid,
                                 char[] password) {
        if (context == null || idToken == null || uid == null || password == null) {
            return false;
        }
        VrProto.E2eeBackupResult backupResult = VrProto.loadE2eeBackup(idToken, uid);
        if (!backupResult.ok || !backupResult.exists || backupResult.backup == null) {
            return false;
        }
        E2eeCrypto.LocalIdentity identity = E2eeCrypto.decryptBackup(
                uid, backupResult.backup, password);
        if (identity == null) {
            return false;
        }
        VrProto.E2eeBundleResult cloud = VrProto.loadE2eeBundle(idToken, uid);
        E2eeCrypto.PublicBundle restoredBundle = identity.publicBundle(
                uid, System.currentTimeMillis());
        if (restoredBundle == null) {
            return false;
        }
        if (cloud.ok && cloud.exists && !Arrays.equals(
                cloud.bundle.signingPublic, restoredBundle.signingPublic)) {
            return false;
        }
        if (!E2eeKeyStore.saveIdentity(context, uid, identity)) {
            return false;
        }
        E2eeCrypto.Session session = newSession(uid, identity);
        if (session == null) {
            return false;
        }
        synchronized (LOCK) {
            cachedPreparation = null;
            cachedPreparedAt = 0;
        }
        if (cloud.ok && !cloud.exists) {
            publishOwn(idToken, session);
        }
        return true;
    }

    static boolean resetIdentity(Context context, String idToken, String uid) {
        if (context == null || idToken == null || uid == null) {
            return false;
        }
        E2eeCrypto.LocalIdentity identity = E2eeCrypto.generateIdentity(
                System.currentTimeMillis());
        if (identity == null || !E2eeKeyStore.saveIdentity(context, uid, identity)) {
            return false;
        }
        E2eeCrypto.Session session = newSession(uid, identity);
        if (session == null) {
            return false;
        }
        synchronized (LOCK) {
            cachedPreparation = null;
            cachedPreparedAt = 0;
        }
        boolean published = publishOwn(idToken, session);
        if (published) {
            // The previous backup encrypts the abandoned identity.  Do
            // not leave it available as a misleading restore source.
            VrProto.deleteE2eeBackup(idToken, uid);
        }
        return published;
    }

    static synchronized PeerResult preparePeer(Context context, String idToken,
                                  E2eeCrypto.Session session, String peerUid) {
        PeerResult out = new PeerResult();
        if (context == null || idToken == null || session == null
                || !session.usable() || peerUid == null
                || peerUid.equals(session.myUid)) {
            return out;
        }
        E2eeCrypto.PublicBundle cached = session.bundleFor(peerUid);
        long now = System.currentTimeMillis();
        Long checkedAt = session.peerBundleCheckedAt.get(peerUid);
        if (cached != null && checkedAt != null
                && now - checkedAt >= 0
                && now - checkedAt < PEER_BUNDLE_CACHE_MS
                && cached.verifyForEncryption(now)) {
            out.state = PEER_READY;
            out.bundle = cached;
            return out;
        }
        VrProto.E2eeBundleResult loaded = VrProto.loadE2eeBundle(idToken, peerUid);
        if (!loaded.ok) {
            out.state = loaded.exists ? PEER_INVALID : PEER_UNAVAILABLE;
            return out;
        }
        if (!loaded.exists || loaded.bundle == null) {
            out.state = PEER_NO_KEYS;
            return out;
        }
        out.bundle = loaded.bundle;
        if (!loaded.bundle.verifyForEncryption(System.currentTimeMillis())) {
            out.state = PEER_INVALID;
            return out;
        }
        int pin = E2eeKeyStore.pinStatus(context, session.myUid, loaded.bundle);
        if (pin == E2eeKeyStore.PIN_CHANGED) {
            session.peerBundles.remove(peerUid);
            session.peerBundleCheckedAt.remove(peerUid);
            out.state = PEER_KEY_CHANGED;
            return out;
        }
        if (pin == E2eeKeyStore.PIN_NEW
                && !E2eeKeyStore.savePin(context, session.myUid, loaded.bundle)) {
            out.state = PEER_UNAVAILABLE;
            return out;
        }
        if (pin != E2eeKeyStore.PIN_NEW && pin != E2eeKeyStore.PIN_MATCH) {
            out.state = PEER_UNAVAILABLE;
            return out;
        }
        session.peerBundles.put(peerUid, loaded.bundle);
        session.peerBundleCheckedAt.put(peerUid, System.currentTimeMillis());
        out.state = PEER_READY;
        return out;
    }

    static boolean acceptPeer(Context context, E2eeCrypto.Session session,
                              E2eeCrypto.PublicBundle bundle) {
        if (context == null || session == null || !session.usable() || bundle == null
                || bundle.uid == null || bundle.uid.equals(session.myUid)
                || !bundle.verifyForEncryption(System.currentTimeMillis())
                || !E2eeKeyStore.savePin(context, session.myUid, bundle)) {
            return false;
        }
        session.peerBundles.put(bundle.uid, bundle);
        session.peerBundleCheckedAt.put(bundle.uid, System.currentTimeMillis());
        return true;
    }

    static String safetyCode(E2eeCrypto.Session session,
                             E2eeCrypto.PublicBundle peerBundle) {
        if (session == null || !session.usable() || peerBundle == null) {
            return "";
        }
        return E2eeCrypto.safetyCode(session.myUid,
                session.ownBundle.signingPublic, peerBundle.uid,
                peerBundle.signingPublic);
    }

    static String ownFingerprint(E2eeCrypto.Session session) {
        return session == null || !session.usable() ? ""
                : session.ownBundle.fingerprint();
    }

    static void clearCache(String uid) {
        synchronized (LOCK) {
            if (uid == null || uid.equals(cachedUid)) {
                cachedSession = null;
                cachedPreparation = null;
                cachedPreparedAt = 0;
                if (uid == null) {
                    cachedUid = "";
                }
            }
        }
    }

    private static E2eeCrypto.Session newSession(String uid,
                                                  E2eeCrypto.LocalIdentity identity) {
        E2eeCrypto.Session session = new E2eeCrypto.Session(uid, identity);
        if (!session.usable()) {
            return null;
        }
        synchronized (LOCK) {
            // Preserve already trusted/pinned peer bundles when our own
            // session object is recreated for the same account.
            if (cachedSession != null && uid.equals(cachedUid)) {
                session.peerBundles.putAll(cachedSession.peerBundles);
                session.peerBundleCheckedAt.putAll(
                        cachedSession.peerBundleCheckedAt);
            }
            cachedSession = session;
            cachedUid = uid;
        }
        return session;
    }

    private static void cachePreparation(String uid, Preparation preparation, long now) {
        synchronized (LOCK) {
            cachedUid = uid;
            cachedPreparation = preparation;
            cachedPreparedAt = now;
            if (preparation.session != null) {
                cachedSession = preparation.session;
            }
        }
    }
}
