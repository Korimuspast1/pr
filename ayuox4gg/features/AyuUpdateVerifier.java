/*
 * This is the source code of AyuGram for Android.
 *
 * We do not and cannot prevent the use of our code,
 * but be respectful and credit the original author.
 *
 * Copyright @Radolyn, 2023
 */

package com.radolyn.ayugram.security;

import android.content.Context;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.Signature;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.security.MessageDigest;
import java.util.Arrays;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;

/**
 * Verifies a downloaded APK before the in-app updater hands it to the package
 * installer.
 *
 * <p>Three independent checks, cheapest first:
 * <ol>
 *   <li><b>SHA-256</b> against a digest published out of band (release notes /
 *       CI artefact). Detects truncation, corrupted downloads and most
 *       man-in-the-middle substitution.</li>
 *   <li><b>Package name</b> must match this app, so an update can never replace
 *       the client with something else.</li>
 *   <li><b>Signer certificate</b> must be the same certificate this build was
 *       signed with. This is the check that actually matters: Android itself
 *       refuses the install if it fails, but failing early gives a clear error
 *       instead of a cryptic installer message.</li>
 * </ol>
 *
 * <p>Usage at the point where the updater currently validates MD5:
 * <pre>
 * AyuUpdateVerifier.Result result = AyuUpdateVerifier.verify(context, apkFile, expectedSha256);
 * if (!result.isOk()) { showError(result.getMessage()); return; }
 * </pre>
 *
 * <p><b>Not yet wired in.</b> The updater lives in the exteraGram part of the
 * tree, which this repository does not contain. This class is self-contained so
 * it can be dropped in without other changes.
 */
public final class AyuUpdateVerifier {

    private static final String TAG = "AyuUpdateVerifier";
    private static final int BUFFER = 64 * 1024;

    private AyuUpdateVerifier() {
    }

    /** Outcome of a verification run. */
    public static final class Result {
        private final boolean ok;
        private final String message;
        private final String sha256;

        private Result(boolean ok, String message, String sha256) {
            this.ok = ok;
            this.message = message;
            this.sha256 = sha256;
        }

        public boolean isOk() {
            return ok;
        }

        public String getMessage() {
            return message;
        }

        public String getSha256() {
            return sha256;
        }

        @Override
        public String toString() {
            return ok ? "OK " + sha256 : "FAILED: " + message;
        }
    }

    /**
     * @param context        application context
     * @param apk            downloaded file
     * @param expectedSha256 lowercase hex digest of the official build, or null
     *                       to skip the hash check (signature check still runs)
     */
    public static Result verify(Context context, File apk, String expectedSha256) {
        if (apk == null || !apk.isFile() || apk.length() == 0L) {
            return new Result(false, "file is missing or empty", null);
        }

        String actual;
        try {
            actual = sha256(apk);
        } catch (Exception e) {
            return new Result(false, "could not read the file: " + e.getMessage(), null);
        }

        if (expectedSha256 != null && !expectedSha256.isEmpty()) {
            String expected = expectedSha256.replace(":", "").trim().toLowerCase(Locale.ROOT);
            if (!expected.equals(actual)) {
                return new Result(false,
                        "SHA-256 mismatch: expected " + expected + ", got " + actual, actual);
            }
        }

        // Package name must be ours.
        PackageManager packageManager = context.getPackageManager();
        PackageInfo archive = packageManager.getPackageArchiveInfo(
                apk.getAbsolutePath(), PackageManager.GET_SIGNING_CERTIFICATES);
        if (archive == null) {
            return new Result(false, "not a valid APK", actual);
        }
        String ownPackage = context.getPackageName();
        if (!ownPackage.equals(archive.packageName)) {
            return new Result(false,
                    "package mismatch: " + archive.packageName + " != " + ownPackage, actual);
        }

        // Signer certificates must match the installed build.
        Set<String> expected_signer = signerDigests(
                packageManager.getPackageInfo(ownPackage, PackageManager.GET_SIGNING_CERTIFICATES));
        Set<String> actual_signer = signerDigests(archive);
        if (expected_signer.isEmpty() || !expected_signer.equals(actual_signer)) {
            return new Result(false, "signing certificate mismatch", actual);
        }

        return new Result(true, "verified", actual);
    }

    /** SHA-256 of every signing certificate, hex encoded. */
    private static Set<String> signerDigests(PackageInfo info) {
        Set<String> digests = new HashSet<>();
        if (info == null) {
            return digests;
        }
        Signature[] signatures;
        if (info.signingInfo != null) {
            signatures = info.signingInfo.hasMultipleSigners()
                    ? info.signingInfo.getApkContentsSigners()
                    : info.signingInfo.getSigningCertificateHistory();
        } else {
            //noinspection deprecation
            signatures = info.signatures;
        }
        if (signatures == null) {
            return digests;
        }
        for (Signature signature : signatures) {
            if (signature == null) {
                continue;
            }
            try {
                MessageDigest digest = MessageDigest.getInstance("SHA-256");
                digests.add(hex(digest.digest(signature.toByteArray())));
            } catch (Exception ignored) {
                // A signature we cannot hash is treated as absent.
            }
        }
        return digests;
    }

    private static String sha256(File file) throws Exception {
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        try (InputStream input = new FileInputStream(file)) {
            byte[] buffer = new byte[BUFFER];
            int read;
            while ((read = input.read(buffer)) > 0) {
                digest.update(buffer, 0, read);
            }
        }
        return hex(digest.digest());
    }

    private static String hex(byte[] bytes) {
        StringBuilder builder = new StringBuilder(bytes.length * 2);
        for (byte value : bytes) {
            builder.append(String.format(Locale.ROOT, "%02x", value));
        }
        return builder.toString();
    }

    /**
     * Convenience for the updater UI: a short, human-comparable fingerprint.
     */
    public static String fingerprint(File apk) {
        try {
            return sha256(apk);
        } catch (Exception e) {
            LogHolder.warn("could not hash " + apk + ": " + e);
            return null;
        }
    }

    /** Keeps android.util.Log out of unit-testable code paths. */
    private static final class LogHolder {
        private LogHolder() {
        }

        static void warn(String message) {
            android.util.Log.w(TAG, message);
        }
    }
}
