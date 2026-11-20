package lav.visaroom.alav;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;

/**
 * Debug-build-only smoke-test entry point.
 *
 * The production session store intentionally encrypts refresh tokens with
 * AndroidKeyStore, so an emulator script cannot create a realistic
 * saved-session state by writing SharedPreferences directly. This activity
 * uses the app's own TokenStore API to save a deliberately invalid test
 * refresh token, then opens ChatsActivity through the same task-clearing
 * navigation used after a real sign-in. The invalid token makes all network
 * refreshes fail safely, while still exercising the local signed-in startup
 * path (including notification-job scheduling).
 */
public class SmokeSeedActivity extends Activity {

    private static final String TAG = "VisaroomSmoke";
    private static final String EMAIL = "seed@example.invalid";
    private static final String REFRESH_TOKEN = "debug-smoke-invalid-refresh-token";
    private static final String UID = "SmokeSeedUser0000000000001";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        TokenStore.clearAll(this);
        boolean saved = TokenStore.activateAccount(
                this, EMAIL, REFRESH_TOKEN, UID);
        String storedUid = TokenStore.uid(this);
        String storedToken = TokenStore.refreshToken(this);
        Log.i(TAG, "seed_saved=" + saved
                + " uid_matches=" + UID.equals(storedUid)
                + " token_present=" + (storedToken != null
                && storedToken.length() > 0));

        Intent intent = new Intent(this, ChatsActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK
                | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        startActivity(intent);
        finish();
    }
}
