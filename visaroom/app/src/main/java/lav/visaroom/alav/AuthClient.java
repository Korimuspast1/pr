package lav.visaroom.alav;

import android.content.Context;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.os.Build;
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

/**
 * Firebase Authentication through its official REST API.
 * Accounts are the same Firebase Auth (Email/Password) accounts that the
 * Firebase console shows. Using REST keeps third-party SDK code out of
 * the APK entirely.
 */
final class AuthClient {

    static final class Result {
        boolean ok;
        boolean networkError;
        boolean sessionExpired;
        String errorCode = "";
        String email = "";
        String uid = "";
        String idToken = "";
        String refreshToken = "";
    }

    private static volatile String androidPackage;
    private static volatile String androidCertSha1;

    private AuthClient() {
    }

    /**
     * The Android API key in Firebase projects is normally restricted to the
     * registered app. The Firebase SDK labels its requests with the package
     * name and the SHA-1 of the signing certificate in these headers; a
     * REST client must do the same or Google rejects the call with
     * "Requests from this Android client application are blocked".
     */
    static void init(Context context) {
        if (androidPackage != null) {
            return;
        }
        try {
            Context app = context.getApplicationContext();
            androidPackage = app.getPackageName();
            androidCertSha1 = signingSha1(app);
        } catch (Exception ignored) {
            // Headers are simply omitted if the signature cannot be read.
        }
    }

    private static String signingSha1(Context context) throws Exception {
        PackageManager pm = context.getPackageManager();
        String pkg = context.getPackageName();
        byte[] signatureBytes = null;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            PackageInfo info = pm.getPackageInfo(pkg, PackageManager.GET_SIGNING_CERTIFICATES);
            if (info.signingInfo != null) {
                Signature[] signers = info.signingInfo.getApkContentsSigners();
                if (signers != null && signers.length > 0) {
                    signatureBytes = signers[0].toByteArray();
                }
            }
        } else {
            PackageInfo info = pm.getPackageInfo(pkg, PackageManager.GET_SIGNATURES);
            if (info.signatures != null && info.signatures.length > 0) {
                signatureBytes = info.signatures[0].toByteArray();
            }
        }
        if (signatureBytes == null) {
            return null;
        }
        byte[] digest = MessageDigest.getInstance("SHA-1").digest(signatureBytes);
        StringBuilder hex = new StringBuilder(digest.length * 2);
        for (byte b : digest) {
            hex.append(String.format("%02X", b & 0xff));
        }
        return hex.toString();
    }

    static void applyAndroidHeaders(HttpURLConnection connection) {
        if (androidPackage != null) {
            connection.setRequestProperty("X-Android-Package", androidPackage);
        }
        if (androidCertSha1 != null) {
            connection.setRequestProperty("X-Android-Cert", androidCertSha1);
        }
    }

    static Result signIn(String email, String password) {
        return passwordAuth(Vault.signInUrl(), email, password);
    }

    static Result signUp(String email, String password) {
        return passwordAuth(Vault.signUpUrl(), email, password);
    }

    static Result updateEmail(String idToken, String newEmail) {
        Result result = new Result();
        try {
            JSONObject body = new JSONObject();
            body.put("idToken", idToken);
            body.put("email", newEmail);
            body.put("returnSecureToken", true);
            JSONObject json = postJson(Vault.updateUrl(), body, null, result);
            if (json == null) {
                return result;
            }
            result.ok = true;
            result.email = json.optString("email");
            result.uid = json.optString("localId");
            result.idToken = json.optString("idToken");
            result.refreshToken = json.optString("refreshToken");
            return result;
        } catch (Exception ignored) {
            result.networkError = true;
            return result;
        }
    }

    static Result refresh(String refreshToken) {
        Result result = new Result();
        try {
            JSONObject body = new JSONObject();
            body.put("grant_type", "refresh_token");
            body.put("refresh_token", refreshToken);
            JSONObject json = postJson(Vault.refreshUrl(), body, null, result);
            if (json == null) {
                return result;
            }
            result.ok = true;
            result.idToken = json.optString("id_token");
            result.refreshToken = json.optString("refresh_token");
            result.uid = json.optString("user_id");
            return result;
        } catch (Exception ignored) {
            result.networkError = true;
            return result;
        }
    }

    /** Creates users/{uid} in Firestore. Allowed only for the owner by the security rules. */
    static void createUserDocument(String idToken, String uid, String email) {
        try {
            JSONObject value = new JSONObject();
            value.put("stringValue", email);
            JSONObject fields = new JSONObject();
            fields.put("email", value);
            JSONObject body = new JSONObject();
            body.put("fields", fields);
            HttpURLConnection connection =
                    (HttpURLConnection) new URL(Vault.usersUrl() + uid).openConnection();
            connection.setRequestMethod("PATCH");
            connection.setConnectTimeout(15000);
            connection.setReadTimeout(15000);
            connection.setDoOutput(true);
            connection.setRequestProperty("Content-Type", "application/json");
            applyAndroidHeaders(connection);
            connection.setRequestProperty("Authorization", "Bearer " + idToken);
            OutputStream out = connection.getOutputStream();
            out.write(body.toString().getBytes(StandardCharsets.UTF_8));
            out.close();
            connection.getResponseCode();
            connection.disconnect();
        } catch (Exception ignored) {
            // Non-blocking: the account exists in Auth even if Firestore is not created yet.
        }
    }

    private static Result passwordAuth(String url, String email, String password) {
        Result result = new Result();
        try {
            JSONObject body = new JSONObject();
            body.put("email", email);
            body.put("password", password);
            body.put("returnSecureToken", true);
            JSONObject json = postJson(url, body, null, result);
            if (json == null) {
                return result;
            }
            result.ok = true;
            result.email = json.optString("email");
            result.uid = json.optString("localId");
            result.idToken = json.optString("idToken");
            result.refreshToken = json.optString("refreshToken");
            return result;
        } catch (Exception ignored) {
            result.networkError = true;
            return result;
        }
    }

    private static JSONObject postJson(String url, JSONObject body, String bearer, Result result)
            throws Exception {
        HttpURLConnection connection = (HttpURLConnection) new URL(url).openConnection();
        connection.setRequestMethod("POST");
        connection.setConnectTimeout(15000);
        connection.setReadTimeout(15000);
        connection.setDoOutput(true);
        connection.setRequestProperty("Content-Type", "application/json");
        applyAndroidHeaders(connection);
        if (bearer != null) {
            connection.setRequestProperty("Authorization", "Bearer " + bearer);
        }
        OutputStream out = connection.getOutputStream();
        out.write(body.toString().getBytes(StandardCharsets.UTF_8));
        out.close();

        int code = connection.getResponseCode();
        InputStream stream = code >= 200 && code < 300
                ? connection.getInputStream() : connection.getErrorStream();
        String text = readAll(stream);
        connection.disconnect();

        JSONObject json = text.length() == 0 ? new JSONObject() : new JSONObject(text);
        if (code >= 200 && code < 300) {
            return json;
        }
        result.errorCode = parseErrorCode(json);
        result.sessionExpired = result.errorCode.contains("INVALID_REFRESH_TOKEN")
                || result.errorCode.contains("TOKEN_EXPIRED")
                || result.errorCode.contains("USER_DISABLED")
                || result.errorCode.contains("USER_NOT_FOUND");
        return null;
    }

    /**
     * Reads the email claim from a Firebase ID token. Used only for UI
     * (whether to show moderation controls); real authority is enforced
     * by the Firestore rules against the verified token.
     */
    static String emailFromIdToken(String idToken) {
        try {
            if (idToken == null) {
                return "";
            }
            String[] parts = idToken.split("\\.");
            if (parts.length < 2) {
                return "";
            }
            String part = parts[1];
            int rem = part.length() % 4;
            if (rem > 0) {
                StringBuilder padded = new StringBuilder(part);
                for (int i = 0; i < 4 - rem; i++) {
                    padded.append('=');
                }
                part = padded.toString();
            }
            byte[] payload = Base64.decode(part, Base64.URL_SAFE | Base64.NO_WRAP);
            return new JSONObject(new String(payload, StandardCharsets.UTF_8))
                    .optString("email", "");
        } catch (Exception ignored) {
            return "";
        }
    }

    private static String parseErrorCode(JSONObject json) {
        try {
            JSONObject error = json.getJSONObject("error");
            StringBuilder code = new StringBuilder(error.optString("message"));
            String status = error.optString("status");
            if (status.length() > 0) {
                code.append('|').append(status);
            }
            JSONArray details = error.optJSONArray("errors");
            if (details != null && details.length() > 0) {
                String reason = details.getJSONObject(0).optString("reason");
                if (reason.length() > 0) {
                    code.append('|').append(reason);
                }
            }
            return code.toString();
        } catch (Exception ignored) {
            return "";
        }
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
