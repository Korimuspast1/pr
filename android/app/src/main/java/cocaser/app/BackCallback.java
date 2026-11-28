package cocaser.app;

import android.app.Activity;
import android.webkit.ValueCallback;

public class BackCallback implements ValueCallback<String> {
    private final Activity activity;

    public BackCallback(Activity activity) {
        this.activity = activity;
    }

    @Override
    public void onReceiveValue(String value) {
        if (!"true".equals(value)) {
            activity.finish();
        }
    }
}
