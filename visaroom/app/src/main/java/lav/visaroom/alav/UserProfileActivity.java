package lav.visaroom.alav;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Path;
import android.os.Bundle;
import android.text.InputType;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.TextView;
import android.widget.Toast;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Public profile of another user. Admin accounts get moderation actions. */
public class UserProfileActivity extends Activity {

    public static final String EXTRA_UID = "uid";
    public static final String EXTRA_USERNAME = "username";

    private final ExecutorService executor = Executors.newSingleThreadExecutor();

    private ImageView avatarView;
    private TextView nameView;
    private ImageView verifiedIcon;
    private TextView kindView;
    private TextView idView;
    private TextView bioView;
    private TextView statusView;
    private View adminPanel;
    private Button blockButton;
    private Button verifyButton;
    private Button messageButton;
    private String displayedUsername = "";

    private String targetUid = "";
    private String fallbackUsername = "";
    private String myUid = "";
    private boolean iAmAdmin;
    private ProfileData targetData;
    private ProfileClient.PublicProfile publicProfile;

    @Override
    protected void attachBaseContext(Context newBase) {
        super.attachBaseContext(ThemeFx.wrap(newBase));
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        AuthClient.init(this);
        setContentView(R.layout.activity_user_profile);
        PressFx.install(this);

        targetUid = getIntent().getStringExtra(EXTRA_UID);
        if (targetUid == null) {
            targetUid = "";
        }
        fallbackUsername = getIntent().getStringExtra(EXTRA_USERNAME);
        if (fallbackUsername == null) {
            fallbackUsername = "";
        }
        if (!AuthClient.validUid(targetUid)) {
            targetUid = "";
        }
        if (!fallbackUsername.matches("[A-Za-z0-9_]{3,24}")) {
            fallbackUsername = "";
        }
        String myEmail = TokenStore.email(this);
        iAmAdmin = Admin.isAdminEmail(myEmail);
        String uid = TokenStore.uid(this);
        myUid = AuthClient.validUid(uid) ? uid : "";

        avatarView = findViewById(R.id.user_avatar);
        nameView = findViewById(R.id.user_name);
        verifiedIcon = findViewById(R.id.user_verified);
        kindView = findViewById(R.id.user_kind);
        idView = findViewById(R.id.user_id);
        bioView = findViewById(R.id.user_bio);
        statusView = findViewById(R.id.profile_status);
        adminPanel = findViewById(R.id.admin_panel);
        blockButton = findViewById(R.id.admin_block_account);
        verifyButton = findViewById(R.id.admin_verified);
        messageButton = findViewById(R.id.message_button);

        nameView.setText(fallbackUsername.length() > 0 ? "@" + fallbackUsername : getString(R.string.user));
        idView.setText(getString(R.string.loading));
        bioView.setText("");

        messageButton.setEnabled(false);
        messageButton.setText(getString(R.string.loading));
        findViewById(R.id.back).setOnClickListener(v -> finishWithAnimation());
        messageButton.setOnClickListener(v -> openDirectChat());
        blockButton.setOnClickListener(v -> confirmToggleBlock());
        verifyButton.setOnClickListener(v -> confirmToggleVerified());
        findViewById(R.id.admin_change_id).setOnClickListener(v -> showChangeIdDialog());
        findViewById(R.id.admin_remove_username).setOnClickListener(v -> confirm(
                "Remove username?", "The user can choose a new one later.",
                this::adminRemoveUsername));
        findViewById(R.id.admin_clear_bio).setOnClickListener(v -> confirm(
                "Clear bio?", null, this::adminClearBio));
        findViewById(R.id.admin_remove_photo).setOnClickListener(v -> confirm(
                "Remove profile photo?", null, this::adminRemovePhoto));

        loadProfile();
    }

    @Override
    protected void onDestroy() {
        executor.shutdownNow();
        super.onDestroy();
    }

    @Override
    public void onBackPressed() {
        finishWithAnimation();
    }

    private void finishWithAnimation() {
        finish();
        overridePendingTransition(R.anim.slide_in_left, R.anim.slide_out_right);
    }

    private String ensureToken() {
        String expectedUid = myUid == null || myUid.length() == 0
                ? TokenStore.uid(this) : myUid;
        String refreshToken = TokenStore.refreshToken(this);
        if (refreshToken == null || refreshToken.length() == 0) {
            return null;
        }
        AuthClient.Result session = AuthClient.refresh(refreshToken);
        if (!session.ok || session.uid.length() == 0
                || (expectedUid != null && expectedUid.length() > 0
                        && !expectedUid.equals(session.uid))) {
            iAmAdmin = false;
            return null;
        }
        String tokenEmail = AuthClient.emailFromIdToken(session.idToken);
        iAmAdmin = Admin.isAdminEmail(tokenEmail);
        String accountEmail = tokenEmail.length() > 0
                ? tokenEmail : TokenStore.email(this);
        if (!TokenStore.saveRefreshedSession(this, session.uid,
                accountEmail, session.refreshToken)) {
            iAmAdmin = false;
            return null;
        }
        myUid = session.uid;
        return session.idToken;
    }

    private void loadProfile() {
        statusView.setText(getString(R.string.loading));
        executor.execute(() -> {
            String token = ensureToken();
            ProfileClient.PublicProfile pub = token == null
                    ? new ProfileClient.PublicProfile()
                    : ProfileClient.loadPublicProfile(token, targetUid);
            ProfileData priv = null;
            if (token != null && iAmAdmin) {
                ProfileData loaded = new ProfileData();
                if (ProfileClient.loadProfileForAdmin(token, targetUid, loaded)) {
                    priv = loaded;
                }
            }
            boolean canSeePhoto = pub.showAvatar && pub.hasAvatar || iAmAdmin
                    && priv != null && priv.hasAvatar;
            byte[] avatar = token != null && canSeePhoto
                    ? ProfileClient.downloadAvatar(token, targetUid) : null;
            ProfileData finalPriv = priv;
            runOnUiThread(() -> {
                publicProfile = pub;
                targetData = finalPriv;
                render(pub, finalPriv, avatar);
            });
        });
    }

    private void render(ProfileClient.PublicProfile pub, ProfileData priv, byte[] avatar) {
        String username = pub.username.length() > 0 ? pub.username
                : priv != null && priv.username.length() > 0 ? priv.username
                : fallbackUsername;
        displayedUsername = username;
        nameView.setText(username.length() > 0 ? "@" + username : "No username");
        boolean verified = priv != null ? priv.verified : pub.verified;
        verifiedIcon.setVisibility(verified ? View.VISIBLE : View.GONE);
        kindView.setText(verified ? "Verified user" : getString(R.string.user));

        long shownId = iAmAdmin && priv != null && priv.publicId > 0
                ? priv.publicId : pub.publicId;
        idView.setText(shownId > 0 ? String.valueOf(shownId)
                : pub.showId ? "Not assigned yet" : "Hidden");

        if (!pub.showBio && !iAmAdmin) {
            bioView.setText("Bio is hidden.");
        } else {
            String bio = pub.bio.length() > 0 ? pub.bio
                    : priv != null ? priv.bio : "";
            bioView.setText(bio.length() > 0 ? bio : "No bio.");
        }

        Bitmap bmp = avatar == null ? null
                : ImageDecode.decodeBytes(avatar, 256, 256 * 256);
        if (bmp != null) {
            avatarView.setImageBitmap(circular(bmp));
        } else {
            avatarView.setImageResource(R.drawable.ic_logo);
        }

        boolean showAdmin = iAmAdmin && targetUid.length() > 0;
        adminPanel.setVisibility(showAdmin ? View.VISIBLE : View.GONE);
        boolean blocked = priv != null ? priv.blocked : pub.blocked;
        blockButton.setText(blocked ? "Unblock account" : getString(R.string.block_account));
        verifyButton.setText(verified ? "Remove verified badge" : getString(R.string.give_verified_badge));
        if (targetUid.length() == 0 || targetUid.equals(myUid)) {
            messageButton.setText("Your profile");
            messageButton.setEnabled(false);
        } else if (blocked) {
            messageButton.setText(getString(R.string.account_blocked));
            messageButton.setEnabled(false);
        } else {
            messageButton.setText(getString(R.string.message));
            messageButton.setEnabled(true);
        }
        if (blocked) {
            statusView.setText(getString(R.string.account_blocked));
        } else if (iAmAdmin && priv == null) {
            statusView.setText(
                    "Admin data could not be loaded. Publish the Firebase rules first.");
        } else {
            statusView.setText(verified ? "Verified account" : "");
        }
    }

    private void openDirectChat() {
        if (targetUid.length() == 0 || targetUid.equals(myUid) || targetBlocked()) {
            return;
        }
        Intent intent = new Intent(this, DirectChatActivity.class);
        intent.putExtra(DirectChatActivity.EXTRA_UID, targetUid);
        intent.putExtra(DirectChatActivity.EXTRA_USERNAME, displayedUsername);
        startActivity(intent);
        overridePendingTransition(R.anim.slide_in_right, R.anim.slide_out_left);
    }

    private Bitmap circular(Bitmap source) {
        int size = Math.min(source.getWidth(), source.getHeight());
        Bitmap square = Bitmap.createBitmap(source,
                (source.getWidth() - size) / 2,
                (source.getHeight() - size) / 2, size, size);
        Bitmap out = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(out);
        Path path = new Path();
        path.addCircle(size / 2f, size / 2f, size / 2f, Path.Direction.CW);
        canvas.clipPath(path);
        canvas.drawBitmap(square, 0, 0, null);
        return out;
    }

    // ---------- admin ----------

    private interface Action {
        void run();
    }

    private void confirm(String title, String message, Action action) {
        AlertDialog.Builder builder = new AlertDialog.Builder(this)
                .setTitle(title)
                .setNegativeButton(getString(R.string.cancel), null)
                .setPositiveButton("Confirm", (d, w) -> action.run());
        if (message != null) {
            builder.setMessage(message);
        }
        builder.show();
    }

    private boolean adminReady() {
        if (!iAmAdmin || !AuthClient.validUid(targetUid) || targetData == null) {
            Toast.makeText(this, "Profile data is not loaded yet.", Toast.LENGTH_SHORT).show();
            return false;
        }
        return true;
    }

    private void setAdminBusy(boolean busy, String status) {
        int[] ids = {R.id.admin_block_account, R.id.admin_verified,
                R.id.admin_change_id, R.id.admin_remove_username,
                R.id.admin_clear_bio, R.id.admin_remove_photo};
        for (int id : ids) {
            findViewById(id).setEnabled(!busy);
        }
        statusView.setText(status == null ? "" : status);
    }

    private boolean targetBlocked() {
        if (targetData != null) {
            return targetData.blocked;
        }
        return publicProfile != null && publicProfile.blocked;
    }

    private void confirmToggleBlock() {
        if (!adminReady()) {
            return;
        }
        if (targetUid.equals(myUid) && !targetBlocked()) {
            Toast.makeText(this, "You cannot block your own admin account.",
                    Toast.LENGTH_SHORT).show();
            return;
        }
        boolean nextBlocked = !targetBlocked();
        confirm(nextBlocked ? "Block account?" : "Unblock account?",
                nextBlocked
                        ? "The user will be signed out, removed from search and will not be able to sign in until unblocked."
                        : "The user will be able to sign in and use visaroom again.",
                () -> adminSetBlocked(nextBlocked));
    }

    private void adminSetBlocked(boolean blocked) {
        if (!adminReady()) {
            return;
        }
        setAdminBusy(true, blocked ? "Blocking account..." : "Unblocking account...");
        boolean oldBlocked = targetData.blocked;
        long oldBlockedAt = targetData.blockedAt;
        String usernameLower = targetData.usernameLower();
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = false;
            if (token != null) {
                targetData.blocked = blocked;
                targetData.blockedAt = blocked ? System.currentTimeMillis() : 0;
                ok = ProfileClient.saveProfileAsAdmin(token, targetUid, targetData);
                if (ok && blocked && usernameLower.length() > 0) {
                    ProfileClient.deleteUsername(token, usernameLower);
                }
                if (ok && !blocked && targetData.username.length() > 0
                        && targetData.discoverable) {
                    ProfileClient.claimUsername(token, targetUid,
                            targetData.username, usernameLower,
                            true, targetData.showId ? targetData.publicId : 0,
                            targetData.verified);
                }
                if (!ok) {
                    targetData.blocked = oldBlocked;
                    targetData.blockedAt = oldBlockedAt;
                }
            }
            boolean finalOk = ok;
            runOnUiThread(() -> {
                setAdminBusy(false, finalOk
                        ? (blocked ? "Account blocked." : "Account unblocked.")
                        : "Could not change the block state.");
                if (finalOk) {
                    loadProfile();
                }
            });
        });
    }

    private boolean targetVerified() {
        if (targetData != null) {
            return targetData.verified;
        }
        return publicProfile != null && publicProfile.verified;
    }

    private void confirmToggleVerified() {
        if (!adminReady()) {
            return;
        }
        boolean nextVerified = !targetVerified();
        confirm(nextVerified ? "Give verified badge?" : "Remove verified badge?",
                nextVerified
                        ? "A checkmark will appear next to this user's name in profiles, search and chats."
                        : "The checkmark will be removed from this user's profile, search and chats.",
                () -> adminSetVerified(nextVerified));
    }

    private void adminSetVerified(boolean verified) {
        if (!adminReady()) {
            return;
        }
        setAdminBusy(true, verified ? "Giving verified badge..." : "Removing verified badge...");
        boolean oldVerified = targetData.verified;
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = false;
            if (token != null) {
                targetData.verified = verified;
                ok = ProfileClient.saveProfileAsAdmin(token, targetUid, targetData);
                if (ok && !targetData.blocked && targetData.username.length() > 0
                        && targetData.discoverable) {
                    ProfileClient.claimUsername(token, targetUid,
                            targetData.username, targetData.usernameLower(),
                            true, targetData.showId ? targetData.publicId : 0,
                            verified);
                }
                if (!ok) {
                    targetData.verified = oldVerified;
                }
            }
            boolean finalOk = ok;
            runOnUiThread(() -> {
                setAdminBusy(false, finalOk
                        ? (verified ? "Verified badge given." : "Verified badge removed.")
                        : "Could not change the verified badge.");
                if (finalOk) {
                    loadProfile();
                }
            });
        });
    }

    private void showChangeIdDialog() {
        if (!adminReady()) {
            return;
        }
        EditText input = new EditText(this);
        input.setInputType(InputType.TYPE_CLASS_NUMBER);
        input.setHint("Any positive ID, for example 777");
        if (targetData.publicId > 0) {
            input.setText(String.valueOf(targetData.publicId));
        }
        input.setBackgroundResource(R.drawable.bg_input);
        input.setPadding(dp(14), 0, dp(14), 0);
        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle(getString(R.string.change_user_id))
                .setView(input)
                .setNegativeButton(getString(R.string.cancel), null)
                .setNeutralButton("Random", null)
                .setPositiveButton(getString(R.string.change), null)
                .create();
        dialog.show();
        dialog.getButton(AlertDialog.BUTTON_NEUTRAL).setOnClickListener(v ->
                input.setText(String.valueOf(ProfileClient.randomPublicId())));
        dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            long newId;
            try {
                newId = Long.parseLong(input.getText().toString().trim());
            } catch (Exception ignored) {
                newId = 0;
            }
            if (!ProfileClient.validPublicId(newId)) {
                Toast.makeText(this,
                        "ID must be a positive number up to 999999999999.",
                        Toast.LENGTH_SHORT).show();
                return;
            }
            dialog.dismiss();
            adminChangeId(newId);
        });
    }

    private void adminChangeId(long newId) {
        setAdminBusy(true, "Changing ID...");
        long oldId = targetData.publicId;
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = false;
            String error = "Could not change ID.";
            if (token != null) {
                int claim = ProfileClient.tryClaimPublicId(token, targetUid, newId);
                if (claim == 1) {
                    error = "This ID is already taken.";
                } else if (claim == 0) {
                    targetData.publicId = newId;
                    ok = ProfileClient.saveProfileAsAdmin(token, targetUid, targetData);
                    if (ok) {
                        if (targetData.username.length() > 0) {
                            ProfileClient.claimUsername(token, targetUid,
                                    targetData.username, targetData.usernameLower(),
                                    targetData.discoverable,
                                    targetData.showId ? newId : 0,
                                    targetData.verified);
                        }
                        if (oldId > 0 && oldId != newId) {
                            ProfileClient.releasePublicId(token, oldId);
                        }
                    } else {
                        targetData.publicId = oldId;
                        if (oldId != newId) {
                            ProfileClient.releasePublicId(token, newId);
                        }
                    }
                }
            }
            boolean finalOk = ok;
            String finalError = error;
            runOnUiThread(() -> {
                setAdminBusy(false, finalOk ? "ID changed." : finalError);
                if (finalOk) {
                    loadProfile();
                }
            });
        });
    }

    private void adminRemoveUsername() {
        if (!adminReady()) {
            return;
        }
        setAdminBusy(true, "Removing username...");
        String oldLower = targetData.usernameLower();
        String oldName = targetData.username;
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = false;
            if (token != null) {
                targetData.username = "";
                ok = ProfileClient.saveProfileAsAdmin(token, targetUid, targetData);
                if (ok && oldLower.length() > 0) {
                    ProfileClient.deleteUsername(token, oldLower);
                }
                if (!ok) {
                    targetData.username = oldName;
                }
            }
            boolean finalOk = ok;
            runOnUiThread(() -> {
                setAdminBusy(false, finalOk ? "Username removed." : "Could not remove username.");
                if (finalOk) {
                    loadProfile();
                }
            });
        });
    }

    private void adminClearBio() {
        if (!adminReady()) {
            return;
        }
        setAdminBusy(true, "Clearing bio...");
        String oldBio = targetData.bio;
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = false;
            if (token != null) {
                targetData.bio = "";
                ok = ProfileClient.saveProfileAsAdmin(token, targetUid, targetData);
                if (!ok) {
                    targetData.bio = oldBio;
                }
            }
            boolean finalOk = ok;
            runOnUiThread(() -> {
                setAdminBusy(false, finalOk ? "Bio cleared." : "Could not clear bio.");
                if (finalOk) {
                    loadProfile();
                }
            });
        });
    }

    private void adminRemovePhoto() {
        if (!adminReady()) {
            return;
        }
        setAdminBusy(true, "Removing photo...");
        boolean oldHas = targetData.hasAvatar;
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = false;
            if (token != null) {
                targetData.hasAvatar = false;
                targetData.avatarUpdatedAt = System.currentTimeMillis();
                ok = ProfileClient.saveProfileAsAdmin(token, targetUid, targetData);
                if (ok) {
                    ProfileClient.deleteAvatar(token, targetUid);
                } else {
                    targetData.hasAvatar = oldHas;
                }
            }
            boolean finalOk = ok;
            runOnUiThread(() -> {
                setAdminBusy(false, finalOk ? "Photo removed." : "Could not remove photo.");
                if (finalOk) {
                    loadProfile();
                }
            });
        });
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
