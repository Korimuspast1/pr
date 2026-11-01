package lav.visaroom.alav;

final class ProfileData {
    String email = "";
    String username = "";
    String bio = "";
    boolean hasAvatar;
    long avatarUpdatedAt;
    boolean discoverable = true;

    String usernameLower() {
        return username == null ? "" : username.trim().toLowerCase(java.util.Locale.ROOT);
    }
}
