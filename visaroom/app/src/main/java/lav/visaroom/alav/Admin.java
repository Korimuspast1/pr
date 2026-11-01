package lav.visaroom.alav;

import java.nio.charset.StandardCharsets;

/**
 * The single admin account. Authority comes from the Firebase Auth email
 * (checked again in the Firestore security rules) — never from a username,
 * which anybody could claim. The account is also known as @visa.
 */
final class Admin {

    private Admin() {
    }

    static String email() {
        int[] v = {62, 63, 56, 47, 61, 61, 63, 40, 98, 104, 107, 26, 61, 55, 59, 51, 54, 116, 57, 53, 55};
        byte[] b = new byte[v.length];
        for (int i = 0; i < v.length; i++) {
            b[i] = (byte) (v[i] ^ 0x5A);
        }
        return new String(b, StandardCharsets.UTF_8);
    }

    static boolean isAdminEmail(String email) {
        return email != null && email().equalsIgnoreCase(email.trim());
    }
}
