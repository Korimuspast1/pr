package lav.visaroom.alav;

final class ProfileData {
    String email = "";
    String username = "";
    String bio = "";
    boolean hasAvatar;
    long avatarUpdatedAt;
    boolean discoverable = true;

    /** Public numeric ID, 100000-5000000, assigned at registration. 0 = none yet. */
    long publicId;

    boolean twoFaEnabled;
    String twoFaVerifier = "";

    boolean showBio = true;
    boolean showAvatar = true;
    boolean showId = true;

    String usernameLower() {
        return username == null ? "" : username.trim().toLowerCase(java.util.Locale.ROOT);
    }
}
