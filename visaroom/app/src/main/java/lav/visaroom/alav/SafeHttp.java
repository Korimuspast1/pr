package lav.visaroom.alav;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.Locale;

import javax.net.ssl.HttpsURLConnection;

/**
 * Small network guard used by every Firebase REST client in the app.
 *
 * The application only ever talks to three Google HTTPS hosts.  Keeping
 * that allow-list here prevents an accidental future URL change (or a
 * malformed server value being reused as a URL) from sending a Firebase
 * bearer token somewhere else.  Redirects are disabled for the same
 * reason: a 3xx response must never silently move an authenticated
 * request to another endpoint.  Responses are also read with a hard byte
 * limit before they reach a JSON parser or an image decoder.
 */
final class SafeHttp {

    private SafeHttp() {
    }

    static HttpURLConnection open(String urlText) throws Exception {
        if (urlText == null || urlText.length() == 0 || urlText.length() > 8192) {
            throw new IOException("Invalid URL");
        }
        URL url = new URL(urlText);
        if (!"https".equalsIgnoreCase(url.getProtocol())
                || url.getUserInfo() != null
                || url.getRef() != null) {
            throw new IOException("Blocked URL");
        }
        String host = url.getHost() == null
                ? "" : url.getHost().toLowerCase(Locale.ROOT);
        if (!"firestore.googleapis.com".equals(host)
                && !"identitytoolkit.googleapis.com".equals(host)
                && !"securetoken.googleapis.com".equals(host)) {
            throw new IOException("Blocked host");
        }
        HttpsURLConnection connection = (HttpsURLConnection) url.openConnection();
        connection.setInstanceFollowRedirects(false);
        connection.setConnectTimeout(15000);
        connection.setReadTimeout(15000);
        return connection;
    }

    static String readText(InputStream stream, int maxBytes) throws Exception {
        if (stream == null) {
            return "";
        }
        if (maxBytes <= 0) {
            throw new IOException("Invalid response limit");
        }
        try {
            ByteArrayOutputStream out = new ByteArrayOutputStream(
                    Math.min(maxBytes, 8192));
            byte[] buffer = new byte[8192];
            int total = 0;
            int read;
            while ((read = stream.read(buffer)) != -1) {
                if (read == 0) {
                    throw new IOException("Stalled response stream");
                }
                total += read;
                if (total > maxBytes) {
                    throw new IOException("Response too large");
                }
                out.write(buffer, 0, read);
            }
            return new String(out.toByteArray(), StandardCharsets.UTF_8);
        } finally {
            try {
                stream.close();
            } catch (Exception ignored) {
            }
        }
    }
}
