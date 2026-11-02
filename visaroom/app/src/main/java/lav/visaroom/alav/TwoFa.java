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
 *
 * The verifier comes back from Firestore/local storage and is therefore
 * parsed defensively.  In particular, its iteration count and decoded
 * salt/hash lengths are bounded before any key derivation starts; a
 * malformed value must not be able to turn one sign-in attempt into a
 * multi-minute CPU denial of service.
 */
final class TwoFa {

    private static final int ITERATIONS = 120000;
    private static final int MIN_ITERATIONS = 10000;
    private static final int MAX_ITERATIONS = 600000;
    private static final int KEY_BITS = 256;
    private static final int SALT_BYTES = 16;
    private static final int MAX_PASSWORD_CHARS = 1024;
    private static final int MAX_VERIFIER_CHARS = 256;

    private TwoFa() {
    }

    static String makeVerifier(String password) {
        try {
            if (!validPassword(password)) {
                return "";
            }
            byte[] salt = new byte[SALT_BYTES];
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
            Parsed parsed = parse(verifier);
            if (!validPassword(password) || parsed == null) {
                return false;
            }
            byte[] actual = derive(password, parsed.salt, parsed.iterations);
            return MessageDigest.isEqual(parsed.expected, actual);
        } catch (Exception ignored) {
            return false;
        }
    }

    static boolean isValidVerifier(String verifier) {
        return parse(verifier) != null;
    }

    private static boolean validPassword(String password) {
        return password != null
                && password.length() > 0
                && password.length() <= MAX_PASSWORD_CHARS;
    }

    private static Parsed parse(String verifier) {
        try {
            if (verifier == null
                    || verifier.length() == 0
                    || verifier.length() > MAX_VERIFIER_CHARS) {
                return null;
            }
            String[] parts = verifier.split(":", -1);
            if (parts.length != 3
                    || parts[0].length() == 0 || parts[0].length() > 9
                    || parts[1].length() == 0 || parts[1].length() > 96
                    || parts[2].length() == 0 || parts[2].length() > 96) {
                return null;
            }
            int iterations = Integer.parseInt(parts[0]);
            if (iterations < MIN_ITERATIONS || iterations > MAX_ITERATIONS) {
                return null;
            }
            byte[] salt = Base64.decode(parts[1], Base64.NO_WRAP);
            byte[] expected = Base64.decode(parts[2], Base64.NO_WRAP);
            if (salt.length < SALT_BYTES || salt.length > 64
                    || expected.length != KEY_BITS / 8) {
                return null;
            }
            return new Parsed(iterations, salt, expected);
        } catch (Exception ignored) {
            return null;
        }
    }

    /** Stable marker proving this device passed 2FA for the current verifier. */
    static String markerFor(String verifier) {
        try {
            if (!isValidVerifier(verifier)) {
                return "";
            }
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
        try {
            return SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256")
                    .generateSecret(spec).getEncoded();
        } finally {
            spec.clearPassword();
        }
    }

    private static final class Parsed {
        final int iterations;
        final byte[] salt;
        final byte[] expected;

        Parsed(int iterations, byte[] salt, byte[] expected) {
            this.iterations = iterations;
            this.salt = salt;
            this.expected = expected;
        }
    }
}
