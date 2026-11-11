package dev.arena.liquidglasslab;

import android.app.Activity;
import android.graphics.Canvas;
import android.os.Bundle;

/**
 * Single-activity app: one full-screen view that owns the backdrop, the floating
 * glass chrome (toolbar, tab bar, buttons, menu) and the tuning sheet.
 */
public class MainActivity extends Activity {

    private LabView labView;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        GlassRender.setDensity(getResources().getDisplayMetrics().density);
        labView = new LabView(this);
        setContentView(labView);
        // a relaunch with extras must reach onNewIntent, not just resume the task
        setIntent(getIntent().addFlags(
                android.content.Intent.FLAG_ACTIVITY_SINGLE_TOP
                        | android.content.Intent.FLAG_ACTIVITY_CLEAR_TOP));
        // deep link: adb shell am start -n dev.arena.liquidglasslab/.MainActivity --es tab tune
        labView.post(() -> applyIntent(getIntent()));
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, android.content.Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == 42 && resultCode == RESULT_OK && data != null && data.getData() != null) {
            try (java.io.InputStream in = getContentResolver().openInputStream(data.getData())) {
                android.graphics.Bitmap bmp = android.graphics.BitmapFactory.decodeStream(in);
                if (bmp != null && labView != null) labView.setPhoto(bmp);
            } catch (Exception ignored) {
                // the lab simply keeps the procedural backdrop
            }
        }
    }

    @Override
    protected void onNewIntent(android.content.Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        if (labView != null) labView.post(() -> applyIntent(intent));
    }

    private void applyIntent(android.content.Intent intent) {
        if (intent == null || labView == null) return;
        labView.openTab(intent.getStringExtra("tab"));
        if (intent.getBooleanExtra("menu", false)) labView.openMenu();
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (labView != null) labView.onShown();
    }
}
