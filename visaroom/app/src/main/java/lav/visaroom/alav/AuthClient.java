package lav.visaroom.alav;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;

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

    private AuthClient() {
    }

    static Result signIn(String email, String password) {
        return passwordAuth(Vault.signInUrl(), email, password);
    }

    static Result signUp(String email, String password) {
        return passwordAuth(Vault.signUpUrl(), email, password);
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

    private static String parseErrorCode(JSONObject json) {
        try {
            return json.getJSONObject("error").optString("message");
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
