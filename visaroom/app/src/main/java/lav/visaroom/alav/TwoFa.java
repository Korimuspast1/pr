package lav.visaroom.alav;

import android.util.Base64;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;

import javax.crypto.SecretKeyFactory;
import javax.crypto.spec.PBEKeySpec;

/**
 * Two-step verification password. Only a PBKDF2 verifier
 * ("iterations:salt:hash", base64) is ever stored — never the password.
 */
final class TwoFa {

    private static final int ITERATIONS = 120000;
    private static final int KEY_BITS = 256;

    private TwoFa() {
    }

    static String makeVerifier(String password) {
        try {
            byte[] salt = new byte[16];
            new SecureRandom().nextBytes(salt);
            byte[] hash = derive(password, salt, ITERATIONS);
            return ITERATIONS + ":"
                    + Base64.encodeToString(salt, Base64.NO_WRAP) + ":"
                    + Base64.encodeToString(hash, Base64.NO_WRAP);
        } catch (Exception ignored) {
            return "";
        }
    }

    static boolean verify(String password, String verifier) {
        try {
            if (password == null || verifier == null || verifier.length() == 0) {
                return false;
            }
            String[] parts = verifier.split(":");
            if (parts.length != 3) {
                return false;
            }
            int iterations = Integer.parseInt(parts[0]);
            byte[] salt = Base64.decode(parts[1], Base64.NO_WRAP);
            byte[] expected = Base64.decode(parts[2], Base64.NO_WRAP);
            byte[] actual = derive(password, salt, iterations);
            return MessageDigest.isEqual(expected, actual);
        } catch (Exception ignored) {
            return false;
        }
    }

    /** Stable marker proving this device passed 2FA for the current verifier. */
    static String markerFor(String verifier) {
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256")
                    .digest(("vr2fa|" + verifier).getBytes(StandardCharsets.UTF_8));
            StringBuilder hex = new StringBuilder(digest.length * 2);
            for (byte b : digest) {
                hex.append(String.format("%02x", b & 0xff));
            }
            return hex.toString();
        } catch (Exception ignored) {
            return "";
        }
    }

    private static byte[] derive(String password, byte[] salt, int iterations) throws Exception {
        PBEKeySpec spec = new PBEKeySpec(password.toCharArray(), salt, iterations, KEY_BITS);
        return SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256")
                .generateSecret(spec).getEncoded();
    }
}
