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

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        AuthClient.init(this);
        setContentView(R.layout.activity_auth);

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

        restoreSession();
    }

    @Override
    protected void onDestroy() {
        executor.shutdownNow();
        super.onDestroy();
    }

    private String localTwoFaVerifier() {
        boolean enabled = getSharedPreferences("profile_cache", Context.MODE_PRIVATE)
                .getBoolean("two_fa", false);
        String verifier = TokenStore.twoFaVerifier(this);
        return enabled && verifier != null ? verifier : "";
    }

    private boolean markerMatches(String verifier) {
        String marker = TokenStore.twoFaMarker(this);
        return marker != null && marker.equals(TwoFa.markerFor(verifier));
    }

    private void proceedWithGate(String verifier, boolean forcePrompt) {
        if (verifier == null || verifier.length() == 0
                || !forcePrompt && markerMatches(verifier)) {
            openChats();
            return;
        }
        showTwoFaPrompt(verifier);
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

    private void showTwoFaPrompt(String verifier) {
        EditText password = gatePasswordField("Additional password");
        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle("Two-step verification")
                .setMessage("Enter your additional password.")
                .setView(gateBox(password))
                .setCancelable(false)
                .setNegativeButton("Cancel", (d, w) -> {
                    TokenStore.clear(AuthActivity.this);
                    setLoading(false);
                })
                .setPositiveButton("Continue", null)
                .create();
        dialog.show();
        dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            if (TwoFa.verify(password.getText().toString(), verifier)) {
                TokenStore.saveTwoFa(this, TwoFa.markerFor(verifier), verifier);
                dialog.dismiss();
                openChats();
            } else {
                Toast.makeText(this, "Incorrect password.", Toast.LENGTH_SHORT).show();
            }
        });
    }

    private void showRegistrationTwoFaPrompt(AuthClient.Result result, long publicId) {
        new AlertDialog.Builder(this)
                .setTitle("Two-step verification")
                .setMessage("Add a second password? It will be asked at sign-in, in addition to your account password. You can turn it on later in Settings.")
                .setCancelable(false)
                .setNegativeButton("Not now", (d, w) -> openChats())
                .setPositiveButton("Set password", (d, w) -> {
                    EditText password = gatePasswordField("Additional password");
                    EditText confirm = gatePasswordField("Repeat password");
                    AlertDialog setup = new AlertDialog.Builder(this)
                            .setTitle("Set additional password")
                            .setView(gateBox(password, confirm))
                            .setCancelable(false)
                            .setNegativeButton("Skip", (dd, ww) -> openChats())
                            .setPositiveButton("Save", null)
                            .create();
                    setup.show();
                    setup.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
                        String pw = password.getText().toString();
                        if (pw.length() < 4 || !pw.equals(confirm.getText().toString())) {
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
                            ProfileClient.saveMyProfile(result.idToken, result.uid, data);
                            TokenStore.saveTwoFa(this, TwoFa.markerFor(verifier), verifier);
                            getSharedPreferences("profile_cache", Context.MODE_PRIVATE)
                                    .edit().putBoolean("two_fa", true).apply();
                            runOnUiThread(this::openChats);
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
            AuthClient.Result result = AuthClient.refresh(token);
            String verifier = "";
            if (result.ok) {
                ProfileData data = new ProfileData();
                if (ProfileClient.loadMyProfile(result.idToken, result.uid, data)
                        && data.twoFaEnabled) {
                    verifier = data.twoFaVerifier;
                    TokenStore.saveTwoFa(this, TokenStore.twoFaMarker(this), verifier);
                } else {
                    verifier = localTwoFaVerifier();
                }
            }
            final String gateVerifier = verifier;
            runOnUiThread(() -> {
                if (result.ok) {
                    TokenStore.save(AuthActivity.this, null, result.refreshToken);
                    TokenStore.saveUid(AuthActivity.this, result.uid);
                    proceedWithGate(gateVerifier, false);
                } else if (result.sessionExpired) {
                    TokenStore.clear(AuthActivity.this);
                    setLoading(false);
                } else {
                    // Offline: keep the session, still honor a locally known 2FA.
                    proceedWithGate(localTwoFaVerifier(), false);
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
            titleView.setText("Create account");
            subtitleView.setText("Enter an email and set a password.");
            submitButton.setText("Create account");
            switchText.setText("Already have an account?");
            switchAction.setText("Sign in");
            passwordInput.setHint("At least 6 characters");
        } else {
            titleView.setText("Sign in");
            subtitleView.setText("Enter your email and password.");
            submitButton.setText("Sign in");
            switchText.setText("No account?");
            switchAction.setText("Create account");
            passwordInput.setHint("Password");
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
            showError("Passwords do not match.");
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
            if (result.ok && register) {
                assignedId = ProfileClient.assignPublicId(result.idToken, result.uid, 0, 0);
                ProfileData data = new ProfileData();
                data.email = result.email;
                data.publicId = assignedId;
                if (!ProfileClient.saveMyProfile(result.idToken, result.uid, data)) {
                    AuthClient.createUserDocument(result.idToken, result.uid, result.email);
                }
                if (assignedId == 0) {
                    assignedId = ProfileClient.randomPublicId();
                }
                getSharedPreferences("profile_cache", Context.MODE_PRIVATE).edit()
                        .putLong("public_id", assignedId).apply();
            } else if (result.ok) {
                ProfileData data = new ProfileData();
                data.email = result.email;
                if (ProfileClient.loadMyProfile(result.idToken, result.uid, data)) {
                    if (!ProfileClient.validPublicId(data.publicId)) {
                        long preferred = getSharedPreferences("profile_cache",
                                Context.MODE_PRIVATE).getLong("public_id", 0);
                        long assigned = ProfileClient.assignPublicId(
                                result.idToken, result.uid, preferred, 0);
                        if (assigned > 0) {
                            data.publicId = assigned;
                            ProfileClient.saveMyProfile(result.idToken, result.uid, data);
                            getSharedPreferences("profile_cache", Context.MODE_PRIVATE).edit()
                                    .putLong("public_id", assigned).apply();
                        }
                    }
                    if (data.twoFaEnabled) {
                        gateVerifier = data.twoFaVerifier;
                        TokenStore.saveTwoFa(this, TokenStore.twoFaMarker(this), gateVerifier);
                    }
                } else {
                    gateVerifier = localTwoFaVerifier();
                }
            }
            final long finalAssignedId = assignedId;
            final String finalGateVerifier = gateVerifier;
            runOnUiThread(() -> {
                if (result.ok) {
                    TokenStore.save(AuthActivity.this, result.email, result.refreshToken);
                    TokenStore.saveUid(AuthActivity.this, result.uid);
                    if (register) {
                        showRegistrationTwoFaPrompt(result, finalAssignedId);
                    } else {
                        proceedWithGate(finalGateVerifier, true);
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
