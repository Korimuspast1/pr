package lav.visaroom.alav;

import android.app.Activity;
import android.content.Context;
import android.os.Bundle;
import android.widget.TextView;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Standalone, scrollable User Agreement opened from Settings. */
public class AgreementActivity extends Activity {

    private final ExecutorService executor = Executors.newSingleThreadExecutor();

    @Override
    protected void attachBaseContext(Context newBase) {
        super.attachBaseContext(ThemeFx.wrap(newBase));
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_agreement);
        PressFx.install(this);
        findViewById(R.id.agreement_back).setOnClickListener(v -> finishWithAnimation());
        TextView textView = findViewById(R.id.agreement_text);
        executor.execute(() -> {
            String text = loadAgreement();
            runOnUiThread(() -> {
                if (!isFinishing()) {
                    textView.setText(text.length() > 0
                            ? text : getString(R.string.agreement_unavailable));
                }
            });
        });
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

    private String loadAgreement() {
        InputStream input = null;
        try {
            input = getResources().openRawResource(R.raw.user_agreement);
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = input.read(buffer)) != -1) {
                out.write(buffer, 0, read);
                if (out.size() > 512 * 1024) {
                    return "";
                }
            }
            return new String(out.toByteArray(), StandardCharsets.UTF_8).trim();
        } catch (Exception ignored) {
            return "";
        } finally {
            if (input != null) {
                try {
                    input.close();
                } catch (Exception ignored) {
                }
            }
        }
    }
}
