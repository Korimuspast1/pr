package lav.visaroom.alav;

final class ProfileData {
    String email = "";
    String username = "";
    String bio = "";
    boolean hasAvatar;
    long avatarUpdatedAt;
    boolean discoverable = true;

    /**
     * Public numeric ID. Registration auto-generates 100000-5000000;
     * an admin may later assign any positive ID. 0 = none yet.
     */
    long publicId;

    /** Admin account block. Only an admin may change these fields. */
    boolean blocked;
    long blockedAt;

    /** Verified badge. Only an admin may grant or remove it. */
    boolean verified;

    boolean twoFaEnabled;
    String twoFaVerifier = "";

    boolean showBio = true;
    boolean showAvatar = true;
    boolean showId = true;

    String usernameLower() {
        return username == null ? "" : username.trim().toLowerCase(java.util.Locale.ROOT);
    }
}
