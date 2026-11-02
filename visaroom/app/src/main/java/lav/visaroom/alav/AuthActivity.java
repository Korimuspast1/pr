package lav.visaroom.alav;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.text.InputType;
import android.text.method.HideReturnsTransformationMethod;
import android.text.method.PasswordTransformationMethod;
import android.util.Patterns;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.ProgressBar;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class AuthActivity extends Activity {

    public static final String EXTRA_ADD_ACCOUNT = "add_account";

    private final ExecutorService executor = Executors.newSingleThreadExecutor();

    private EditText emailInput;
    private EditText passwordInput;
    private EditText confirmInput;
    private TextView titleView;
    private TextView subtitleView;
    private TextView errorView;
    private TextView switchText;
    private TextView switchAction;
    private Button submitButton;
    private ProgressBar progress;
    private View confirmBlock;

    private boolean registering;
    private boolean passwordVisible;
    private boolean confirmVisible;
    private boolean addingAccount;
    private int restoreAttempts;

    @Override
    protected void attachBaseContext(Context newBase) {
        super.attachBaseContext(ThemeFx.wrap(newBase));
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        AuthClient.init(this);
        setContentView(R.layout.activity_auth);
        PressFx.install(this);

        emailInput = findViewById(R.id.email);
        passwordInput = findViewById(R.id.password);
        confirmInput = findViewById(R.id.confirm);
        titleView = findViewById(R.id.auth_title);
        subtitleView = findViewById(R.id.auth_subtitle);
        errorView = findViewById(R.id.error);
        switchText = findViewById(R.id.switch_text);
        switchAction = findViewById(R.id.switch_mode);
        submitButton = findViewById(R.id.submit);
        progress = findViewById(R.id.progress);
        confirmBlock = findViewById(R.id.confirm_block);

        ImageButton togglePassword = findViewById(R.id.toggle_password);
        ImageButton toggleConfirm = findViewById(R.id.toggle_confirm);

        togglePassword.setOnClickListener(v -> {
            passwordVisible = !passwordVisible;
            applyVisibility(passwordInput, passwordVisible, togglePassword);
        });
        toggleConfirm.setOnClickListener(v -> {
            confirmVisible = !confirmVisible;
            applyVisibility(confirmInput, confirmVisible, toggleConfirm);
        });
        switchAction.setOnClickListener(v -> setMode(!registering));
        submitButton.setOnClickListener(v -> submit());

        addingAccount = getIntent() != null
                && getIntent().getBooleanExtra(EXTRA_ADD_ACCOUNT, false);
        if (addingAccount) {
            subtitleView.setText("Sign in or create another account. Up to 3 accounts can stay on this device.");
        } else {
            restoreSession();
        }
    }

    @Override
    protected void onDestroy() {
        executor.shutdownNow();
        super.onDestroy();
    }

    private String localTwoFaVerifier() {
        return localTwoFaVerifier(TokenStore.uid(this));
    }

    private String localTwoFaVerifier(String uid) {
        boolean enabled = TokenStore.profilePrefs(this, uid)
                .getBoolean("two_fa", false);
        String verifier = TokenStore.twoFaVerifierForUid(this, uid);
        if (verifier == null && uid != null && uid.equals(TokenStore.uid(this))) {
            verifier = TokenStore.twoFaVerifier(this);
        }
        return enabled && TwoFa.isValidVerifier(verifier) ? verifier : "";
    }

    private boolean localTwoFaEnabled(String uid) {
        return TokenStore.profilePrefs(this, uid).getBoolean("two_fa", false);
    }

    private ProfileData localProfileData(String uid, String email) {
        ProfileData data = new ProfileData();
        data.email = email == null ? "" : email;
        android.content.SharedPreferences prefs = TokenStore.profilePrefs(this, uid);
        data.username = prefs.getString("username", "");
        data.bio = prefs.getString("bio", "");
        data.discoverable = prefs.getBoolean("discoverable", true);
        data.hasAvatar = prefs.getBoolean("has_avatar", false);
        data.avatarUpdatedAt = prefs.getLong("avatar_at", 0);
        data.publicId = prefs.getLong("public_id", 0);
        data.twoFaEnabled = prefs.getBoolean("two_fa", false);
        String verifier = TokenStore.twoFaVerifierForUid(this, uid);
        data.twoFaVerifier = TwoFa.isValidVerifier(verifier) ? verifier : "";
        if (!data.twoFaEnabled) {
            data.twoFaVerifier = "";
        }
        data.showBio = prefs.getBoolean("show_bio", true);
        data.showAvatar = prefs.getBoolean("show_avatar", true);
        data.showId = prefs.getBoolean("show_id", true);
        data.verified = prefs.getBoolean("verified", false);
        return data;
    }

    private boolean markerMatches(String uid, String verifier) {
        String marker = TokenStore.twoFaMarkerForUid(this, uid);
        return marker != null && marker.length() > 0
                && marker.equals(TwoFa.markerFor(verifier));
    }

    private void proceedWithGate(String uid, String verifier, boolean forcePrompt) {
        if (!TwoFa.isValidVerifier(verifier)
                || !forcePrompt && markerMatches(uid, verifier)) {
            openChats();
            return;
        }
        showTwoFaPrompt(uid, verifier);
    }

    private EditText gatePasswordField(String hint) {
        EditText input = new EditText(this);
        input.setHint(hint);
        input.setInputType(InputType.TYPE_CLASS_TEXT
                | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        input.setBackgroundResource(R.drawable.bg_input);
        input.setPadding(dp(14), 0, dp(14), 0);
        return input;
    }

    private LinearLayout gateBox(EditText... fields) {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(24), dp(8), dp(24), dp(4));
        for (int i = 0; i < fields.length; i++) {
            LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, dp(52));
            if (i > 0) {
                params.topMargin = dp(12);
            }
            box.addView(fields[i], params);
        }
        return box;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private void showTwoFaPrompt(String uid, String verifier) {
        EditText password = gatePasswordField("Additional password");
        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle(getString(R.string.two_step_verification))
                .setMessage("Enter your additional password.")
                .setView(gateBox(password))
                .setCancelable(false)
                .setNegativeButton(getString(R.string.cancel), (d, w) -> {
                    TokenStore.clear(AuthActivity.this);
                    setLoading(false);
                })
                .setPositiveButton("Continue", null)
                .create();
        dialog.show();
        dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            if (TwoFa.verify(password.getText().toString(), verifier)) {
                TokenStore.saveTwoFaForUid(this, uid,
                        TwoFa.markerFor(verifier), verifier);
                dialog.dismiss();
                openChats();
            } else {
                Toast.makeText(this, "Incorrect password.", Toast.LENGTH_SHORT).show();
            }
        });
    }

    private void showRegistrationTwoFaPrompt(AuthClient.Result result, long publicId) {
        new AlertDialog.Builder(this)
                .setTitle(getString(R.string.two_step_verification))
                .setMessage("Add a second password? It will be asked at sign-in, in addition to your account password. You can turn it on later in Settings.")
                .setCancelable(false)
                .setNegativeButton("Not now", (d, w) -> openChats())
                .setPositiveButton("Set password", (d, w) -> {
                    EditText password = gatePasswordField("Additional password");
                    EditText confirm = gatePasswordField(getString(R.string.repeat_password));
                    AlertDialog setup = new AlertDialog.Builder(this)
                            .setTitle("Set additional password")
                            .setView(gateBox(password, confirm))
                            .setCancelable(false)
                            .setNegativeButton("Skip", (dd, ww) -> openChats())
                            .setPositiveButton(getString(R.string.save), null)
                            .create();
                    setup.show();
                    setup.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
                        String pw = password.getText().toString();
                        if (pw.length() < 4 || pw.length() > 1024
                                || !pw.equals(confirm.getText().toString())) {
                            Toast.makeText(this, "Check the password.", Toast.LENGTH_SHORT).show();
                            return;
                        }
                        setup.dismiss();
                        setLoading(true);
                        executor.execute(() -> {
                            String verifier = TwoFa.makeVerifier(pw);
                            ProfileData data = new ProfileData();
                            data.email = result.email;
                            data.publicId = publicId;
                            data.twoFaEnabled = true;
                            data.twoFaVerifier = verifier;
                            boolean saved = TwoFa.isValidVerifier(verifier)
                                    && ProfileClient.saveMyProfile(
                                            result.idToken, result.uid, data);
                            if (saved) {
                                TokenStore.saveTwoFaForUid(this, result.uid,
                                        TwoFa.markerFor(verifier), verifier);
                                TokenStore.profilePrefs(this, result.uid)
                                        .edit().putBoolean("two_fa", true).apply();
                            } else {
                                TokenStore.profilePrefs(this, result.uid)
                                        .edit().putBoolean("two_fa", false).apply();
                            }
                            runOnUiThread(() -> {
                                if (!saved) {
                                    Toast.makeText(this,
                                            "Could not enable two-step verification.",
                                            Toast.LENGTH_SHORT).show();
                                }
                                openChats();
                            });
                        });
                    });
                })
                .show();
    }

    private void restoreSession() {
        String token = TokenStore.refreshToken(this);
        if (token == null || token.length() == 0) {
            return;
        }
        setLoading(true);
        executor.execute(() -> {
            String storedUid = TokenStore.uid(this);
            AuthClient.Result result = AuthClient.refresh(token);
            String verifier = "";
            boolean blockedAccount = false;
            boolean securityCheckFailed = false;
            if (result.ok && storedUid != null && storedUid.length() > 0
                    && !storedUid.equals(result.uid)) {
                securityCheckFailed = true;
            } else if (result.ok) {
                String accountEmail = result.email == null || result.email.length() == 0
                        ? AuthClient.emailFromIdToken(result.idToken) : result.email;
                if (accountEmail == null || accountEmail.length() == 0) {
                    accountEmail = TokenStore.email(this);
                }
                boolean sessionSaved = TokenStore.saveRefreshedSession(
                        this, result.uid, accountEmail, result.refreshToken);
                if (!sessionSaved) {
                    securityCheckFailed = true;
                } else {
                    ProfileData data = localProfileData(result.uid, accountEmail);
                    ProfileClient.loadMyProfile(result.idToken, result.uid, data);
                    boolean cloudKnownAtLoad = data.cloudStateKnown;
                    boolean cloudEnabledAtLoad = data.twoFaEnabled;
                    String cloudVerifierAtLoad = data.twoFaVerifier;
                    boolean synced = ProfileClient.ensureUserDocuments(
                            result.idToken, result.uid, data);
                    blockedAccount = data.blocked;
                    if (!blockedAccount && synced) {
                        if (data.twoFaEnabled) {
                            if (TwoFa.isValidVerifier(data.twoFaVerifier)) {
                                verifier = data.twoFaVerifier;
                                TokenStore.saveTwoFaForUid(this, result.uid,
                                        TokenStore.twoFaMarkerForUid(this, result.uid),
                                        verifier);
                            } else {
                                securityCheckFailed = true;
                            }
                        } else {
                            TokenStore.saveTwoFaForUid(this, result.uid, "", "");
                        }
                        TokenStore.profilePrefs(this, result.uid).edit()
                                .putLong("public_id", data.publicId)
                                .putBoolean("two_fa", data.twoFaEnabled)
                                .apply();
                    } else if (!blockedAccount && cloudKnownAtLoad) {
                        if (cloudEnabledAtLoad) {
                            if (TwoFa.isValidVerifier(cloudVerifierAtLoad)) {
                                verifier = cloudVerifierAtLoad;
                            } else {
                                securityCheckFailed = true;
                            }
                        }
                    } else if (!blockedAccount) {
                        // Existing trusted session, temporarily offline:
                        // the locally saved verifier/marker still gate it.
                        if (localTwoFaEnabled(result.uid)
                                && localTwoFaVerifier(result.uid).length() == 0) {
                            securityCheckFailed = true;
                        } else {
                            verifier = localTwoFaVerifier(result.uid);
                        }
                    }
                }
            }
            final String gateVerifier = verifier;
            final boolean finalBlockedAccount = blockedAccount;
            final boolean finalSecurityCheckFailed = securityCheckFailed;
            runOnUiThread(() -> {
                if (result.ok && finalSecurityCheckFailed) {
                    TokenStore.clear(AuthActivity.this);
                    setLoading(false);
                    showError("Could not verify account security. Sign in again.");
                } else if (result.ok && finalBlockedAccount) {
                    TokenStore.clear(AuthActivity.this);
                    if (TokenStore.refreshToken(AuthActivity.this) != null
                            && restoreAttempts < TokenStore.MAX_ACCOUNTS) {
                        restoreAttempts++;
                        restoreSession();
                    } else {
                        setLoading(false);
                        showError("This account has been blocked.");
                    }
                } else if (result.ok) {
                    proceedWithGate(result.uid, gateVerifier, false);
                } else if (result.sessionExpired) {
                    TokenStore.clear(AuthActivity.this);
                    if (TokenStore.refreshToken(AuthActivity.this) != null
                            && restoreAttempts < TokenStore.MAX_ACCOUNTS) {
                        restoreAttempts++;
                        restoreSession();
                    } else {
                        setLoading(false);
                    }
                } else {
                    // Offline: keep the session, still honor a locally known 2FA.
                    String activeUid = TokenStore.uid(AuthActivity.this);
                    proceedWithGate(activeUid, localTwoFaVerifier(activeUid), false);
                }
            });
        });
    }

    private void setMode(boolean register) {
        registering = register;
        errorView.setVisibility(View.GONE);
        confirmBlock.setVisibility(register ? View.VISIBLE : View.GONE);
        confirmInput.setText("");
        if (register) {
            titleView.setText(getString(R.string.create_account));
            subtitleView.setText("Enter an email and set a password.");
            submitButton.setText(getString(R.string.create_account));
            switchText.setText("Already have an account?");
            switchAction.setText(getString(R.string.sign_in));
            passwordInput.setHint("At least 6 characters");
        } else {
            titleView.setText(getString(R.string.sign_in));
            subtitleView.setText(getString(R.string.enter_email_password));
            submitButton.setText(getString(R.string.sign_in));
            switchText.setText(getString(R.string.no_account));
            switchAction.setText(getString(R.string.create_account));
            passwordInput.setHint(getString(R.string.password));
        }
    }

    private void submit() {
        String email = emailInput.getText().toString().trim();
        String password = passwordInput.getText().toString();

        if (!Patterns.EMAIL_ADDRESS.matcher(email).matches()) {
            showError("Enter a valid email.");
            return;
        }
        if (password.length() < 6) {
            showError("Use a password of at least 6 characters.");
            return;
        }
        if (registering && !password.equals(confirmInput.getText().toString())) {
            showError(getString(R.string.e2ee_password_mismatch));
            return;
        }

        errorView.setVisibility(View.GONE);
        setLoading(true);
        boolean register = registering;
        executor.execute(() -> {
            AuthClient.Result result = register
                    ? AuthClient.signUp(email, password)
                    : AuthClient.signIn(email, password);
            long assignedId = 0;
            String gateVerifier = "";
            boolean blockedAccount = false;
            boolean securityCheckFailed = false;
            boolean accountStored = !result.ok || TokenStore.activateAccount(
                    AuthActivity.this, result.email, result.refreshToken, result.uid);
            if (result.ok && accountStored && register) {
                ProfileData data = new ProfileData();
                data.email = result.email;
                ProfileClient.ensureUserDocuments(result.idToken, result.uid, data);
                assignedId = data.publicId;
                if (assignedId == 0) {
                    assignedId = ProfileClient.randomPublicId();
                }
                TokenStore.profilePrefs(this, result.uid).edit()
                        .putLong("public_id", assignedId).apply();
            } else if (result.ok && accountStored) {
                ProfileData data = localProfileData(result.uid, result.email);
                ProfileClient.loadMyProfile(result.idToken, result.uid, data);
                blockedAccount = data.blocked && data.cloudProfileLoaded;
                if (!blockedAccount && !data.cloudStateKnown) {
                    // A fresh password sign-in must not fail open when
                    // the cloud 2FA state could not be loaded (offline,
                    // unpublished/broken rules, malformed document...).
                    securityCheckFailed = true;
                } else if (!blockedAccount) {
                    boolean synced = ProfileClient.ensureUserDocuments(
                            result.idToken, result.uid, data);
                    blockedAccount = data.blocked;
                    if (!blockedAccount && data.twoFaEnabled
                            && !TwoFa.isValidVerifier(data.twoFaVerifier)) {
                        securityCheckFailed = true;
                    } else if (!blockedAccount) {
                        if (data.twoFaEnabled) {
                            gateVerifier = data.twoFaVerifier;
                            TokenStore.saveTwoFaForUid(this, result.uid,
                                    TokenStore.twoFaMarkerForUid(this, result.uid),
                                    gateVerifier);
                        } else if (synced) {
                            TokenStore.saveTwoFaForUid(this, result.uid, "", "");
                        }
                        if (synced) {
                            TokenStore.profilePrefs(this, result.uid).edit()
                                    .putLong("public_id", data.publicId)
                                    .putBoolean("two_fa", data.twoFaEnabled)
                                    .apply();
                        }
                    }
                }
            }
            final long finalAssignedId = assignedId;
            final String finalGateVerifier = gateVerifier;
            final boolean finalAccountStored = accountStored;
            final boolean finalBlockedAccount = blockedAccount;
            final boolean finalSecurityCheckFailed = securityCheckFailed;
            runOnUiThread(() -> {
                if (result.ok && finalSecurityCheckFailed) {
                    TokenStore.clear(AuthActivity.this);
                    setLoading(false);
                    showError("Could not verify account security. Check your connection and try again.");
                } else if (result.ok && !finalAccountStored) {
                    setLoading(false);
                    showError("Three accounts are already saved on this device. Remove one first.");
                } else if (result.ok && finalBlockedAccount) {
                    TokenStore.clear(AuthActivity.this);
                    setLoading(false);
                    showError("This account has been blocked.");
                } else if (result.ok) {
                    if (register) {
                        showRegistrationTwoFaPrompt(result, finalAssignedId);
                    } else {
                        proceedWithGate(result.uid, finalGateVerifier, true);
                    }
                } else {
                    setLoading(false);
                    showError(messageFor(result));
                }
            });
        });
    }

    private String messageFor(AuthClient.Result result) {
        if (result.networkError) {
            return "No connection. Check your internet and try again.";
        }
        String code = result.errorCode;
        if (code.contains("EMAIL_EXISTS")) {
            return "An account with this email already exists.";
        }
        if (code.contains("INVALID_LOGIN_CREDENTIALS")
                || code.contains("INVALID_PASSWORD")
                || code.contains("EMAIL_NOT_FOUND")) {
            return "Incorrect email or password.";
        }
        if (code.contains("WEAK_PASSWORD")) {
            return "Use a password of at least 6 characters.";
        }
        if (code.contains("INVALID_EMAIL")) {
            return "Enter a valid email.";
        }
        if (code.contains("USER_DISABLED")) {
            return "This account has been disabled.";
        }
        if (code.contains("TOO_MANY_ATTEMPTS")) {
            return "Too many attempts. Try again later.";
        }
        if (code.contains("OPERATION_NOT_ALLOWED")
                || code.contains("CONFIGURATION_NOT_FOUND")
                || code.contains("SERVICE_DISABLED")) {
            return "Email sign-in is not enabled for this app yet.";
        }
        if (code.contains("API_KEY")
                || code.contains("API key not valid")
                || code.toLowerCase(java.util.Locale.ROOT).contains("are blocked")) {
            return "This app is not authorized in Firebase yet. Its SHA-1 fingerprint must be added in the Firebase console.";
        }
        if (code.length() > 0) {
            String first = code.split("\\|")[0].trim();
            if (first.length() > 90) {
                first = first.substring(0, 90);
            }
            return "Something went wrong. " + first;
        }
        return "Something went wrong. Try again.";
    }

    private void applyVisibility(EditText input, boolean visible, ImageButton button) {
        int selection = input.getSelectionStart();
        input.setTransformationMethod(visible
                ? HideReturnsTransformationMethod.getInstance()
                : PasswordTransformationMethod.getInstance());
        button.setImageResource(visible ? R.drawable.ic_eye_off : R.drawable.ic_eye);
        if (selection >= 0) {
            input.setSelection(selection);
        }
    }

    private void showError(String message) {
        errorView.setText(message);
        errorView.setVisibility(View.VISIBLE);
    }

    private void setLoading(boolean loading) {
        progress.setVisibility(loading ? View.VISIBLE : View.GONE);
        submitButton.setEnabled(!loading);
        emailInput.setEnabled(!loading);
        passwordInput.setEnabled(!loading);
        confirmInput.setEnabled(!loading);
        switchAction.setEnabled(!loading);
    }

    private void openChats() {
        Intent intent = new Intent(this, ChatsActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        startActivity(intent);
        finish();
        overridePendingTransition(R.anim.slide_in_right, R.anim.slide_out_left);
    }
}
