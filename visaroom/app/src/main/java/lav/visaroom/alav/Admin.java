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

    private static String decode(int[] v) {
        byte[] b = new byte[v.length];
        for (int i = 0; i < v.length; i++) {
            b[i] = (byte) (v[i] ^ 0x5A);
        }
        return new String(b, StandardCharsets.UTF_8);
    }

    static String email() {
        return decode(new int[]{62, 63, 56, 47, 61, 61, 63, 40, 98, 104, 107, 26, 61, 55, 59, 51, 54, 116, 57, 53, 55});
    }

    /** Второй администратор: nikiilrus22@gmail.com */
    static String email2() {
        return decode(new int[]{52, 51, 49, 51, 51, 54, 40, 47, 41, 104, 104, 26, 61, 55, 59, 51, 54, 116, 57, 53, 55});
    }

    static boolean isAdminEmail(String email) {
        if (email == null) {
            return false;
        }
        String e = email.trim();
        return email().equalsIgnoreCase(e) || email2().equalsIgnoreCase(e);
    }
}
