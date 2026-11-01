package lav.visaroom.alav;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.text.method.HideReturnsTransformationMethod;
import android.text.method.PasswordTransformationMethod;
import android.util.Patterns;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.ProgressBar;
import android.widget.TextView;

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

    private void restoreSession() {
        String token = TokenStore.refreshToken(this);
        if (token == null || token.length() == 0) {
            return;
        }
        setLoading(true);
        executor.execute(() -> {
            AuthClient.Result result = AuthClient.refresh(token);
            runOnUiThread(() -> {
                if (result.ok) {
                    TokenStore.save(AuthActivity.this, null, result.refreshToken);
                    openChats();
                } else if (result.sessionExpired) {
                    TokenStore.clear(AuthActivity.this);
                    setLoading(false);
                } else {
                    // Offline or temporary error: the saved session is kept.
                    openChats();
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
            if (result.ok && register) {
                AuthClient.createUserDocument(result.idToken, result.uid, result.email);
            }
            runOnUiThread(() -> {
                if (result.ok) {
                    TokenStore.save(AuthActivity.this, result.email, result.refreshToken);
                    openChats();
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
        if (code.contains("OPERATION_NOT_ALLOWED")) {
            return "Email sign-in is not enabled for this app yet.";
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
    }
}
