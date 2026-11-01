package lav.visaroom.alav;

import android.app.Activity;
import android.app.AlertDialog;
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
    private TextView idView;
    private TextView bioView;
    private TextView statusView;
    private View adminPanel;

    private String targetUid = "";
    private String fallbackUsername = "";
    private String myUid = "";
    private boolean iAmAdmin;
    private ProfileData targetData;
    private ProfileClient.PublicProfile publicProfile;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        AuthClient.init(this);
        setContentView(R.layout.activity_user_profile);

        targetUid = getIntent().getStringExtra(EXTRA_UID);
        if (targetUid == null) {
            targetUid = "";
        }
        fallbackUsername = getIntent().getStringExtra(EXTRA_USERNAME);
        if (fallbackUsername == null) {
            fallbackUsername = "";
        }
        String myEmail = TokenStore.email(this);
        iAmAdmin = Admin.isAdminEmail(myEmail);
        String uid = TokenStore.uid(this);
        myUid = uid == null ? "" : uid;

        avatarView = findViewById(R.id.user_avatar);
        nameView = findViewById(R.id.user_name);
        idView = findViewById(R.id.user_id);
        bioView = findViewById(R.id.user_bio);
        statusView = findViewById(R.id.profile_status);
        adminPanel = findViewById(R.id.admin_panel);

        nameView.setText(fallbackUsername.length() > 0 ? "@" + fallbackUsername : "User");
        idView.setText("Loading...");
        bioView.setText("");

        findViewById(R.id.back).setOnClickListener(v -> finishWithAnimation());
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
        String refreshToken = TokenStore.refreshToken(this);
        if (refreshToken == null || refreshToken.length() == 0) {
            return null;
        }
        AuthClient.Result session = AuthClient.refresh(refreshToken);
        if (!session.ok) {
            return null;
        }
        String tokenEmail = AuthClient.emailFromIdToken(session.idToken);
        if (tokenEmail.length() > 0) {
            iAmAdmin = Admin.isAdminEmail(tokenEmail);
            TokenStore.save(this, tokenEmail, session.refreshToken);
        } else {
            TokenStore.save(this, null, session.refreshToken);
        }
        if (session.uid.length() > 0) {
            myUid = session.uid;
            TokenStore.saveUid(this, session.uid);
        }
        return session.idToken;
    }

    private void loadProfile() {
        statusView.setText("Loading...");
        executor.execute(() -> {
            String token = ensureToken();
            ProfileClient.PublicProfile pub = token == null
                    ? new ProfileClient.PublicProfile()
                    : ProfileClient.loadPublicProfile(token, targetUid);
            ProfileData priv = null;
            if (token != null && iAmAdmin) {
                ProfileData loaded = new ProfileData();
                if (ProfileClient.loadMyProfile(token, targetUid, loaded)) {
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
        nameView.setText(username.length() > 0 ? "@" + username : "No username");

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
                : BitmapFactory.decodeByteArray(avatar, 0, avatar.length);
        if (bmp != null) {
            avatarView.setImageBitmap(circular(bmp));
        } else {
            avatarView.setImageResource(R.drawable.ic_logo);
        }

        boolean showAdmin = iAmAdmin && targetUid.length() > 0;
        adminPanel.setVisibility(showAdmin ? View.VISIBLE : View.GONE);
        statusView.setText(iAmAdmin && priv == null
                ? "Admin data could not be loaded. Publish the Firebase rules first."
                : "");
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
                .setNegativeButton("Cancel", null)
                .setPositiveButton("Confirm", (d, w) -> action.run());
        if (message != null) {
            builder.setMessage(message);
        }
        builder.show();
    }

    private boolean adminReady() {
        if (targetData == null) {
            Toast.makeText(this, "Profile data is not loaded yet.", Toast.LENGTH_SHORT).show();
            return false;
        }
        return true;
    }

    private void setAdminBusy(boolean busy, String status) {
        int[] ids = {R.id.admin_change_id, R.id.admin_remove_username,
                R.id.admin_clear_bio, R.id.admin_remove_photo};
        for (int id : ids) {
            findViewById(id).setEnabled(!busy);
        }
        statusView.setText(status == null ? "" : status);
    }

    private void showChangeIdDialog() {
        if (!adminReady()) {
            return;
        }
        EditText input = new EditText(this);
        input.setInputType(InputType.TYPE_CLASS_NUMBER);
        input.setHint("100000 - 5000000");
        if (targetData.publicId > 0) {
            input.setText(String.valueOf(targetData.publicId));
        }
        input.setBackgroundResource(R.drawable.bg_input);
        input.setPadding(dp(14), 0, dp(14), 0);
        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle("Change user ID")
                .setView(input)
                .setNegativeButton("Cancel", null)
                .setNeutralButton("Random", null)
                .setPositiveButton("Change", null)
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
                Toast.makeText(this, "ID must be between 100000 and 5000000.",
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
                if (newId == oldId) {
                    ok = true;
                } else {
                    int claim = ProfileClient.tryClaimPublicId(token, targetUid, newId);
                    if (claim == 1) {
                        error = "This ID is already taken.";
                    } else if (claim == 0) {
                        targetData.publicId = newId;
                        ok = ProfileClient.saveMyProfile(token, targetUid, targetData);
                        if (ok) {
                            if (targetData.username.length() > 0) {
                                ProfileClient.claimUsername(token, targetUid,
                                        targetData.username, targetData.usernameLower(),
                                        targetData.discoverable, newId);
                            }
                            if (oldId > 0) {
                                ProfileClient.releasePublicId(token, oldId);
                            }
                        } else {
                            targetData.publicId = oldId;
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
                ok = ProfileClient.saveMyProfile(token, targetUid, targetData);
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
                ok = ProfileClient.saveMyProfile(token, targetUid, targetData);
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
                ProfileClient.deleteAvatar(token, targetUid);
                targetData.hasAvatar = false;
                targetData.avatarUpdatedAt = System.currentTimeMillis();
                ok = ProfileClient.saveMyProfile(token, targetUid, targetData);
                if (!ok) {
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
