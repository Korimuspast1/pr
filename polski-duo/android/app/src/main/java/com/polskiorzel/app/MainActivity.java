package com.polskiorzel.app;

import android.content.Intent;
import android.os.Bundle;
import android.speech.tts.TextToSpeech;
import android.webkit.JavascriptInterface;
import android.webkit.WebSettings;
import android.webkit.WebView;
import com.getcapacitor.BridgeActivity;
import java.util.Locale;

public class MainActivity extends BridgeActivity {
    private TextToSpeech tts;
    private volatile boolean ttsReady = false;
    private volatile boolean plSupported = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        tts = new TextToSpeech(this, status -> {
            if (status == TextToSpeech.SUCCESS && tts != null) {
                Locale pl = new Locale("pl", "PL");
                int res = tts.setLanguage(pl);
                if (res == TextToSpeech.LANG_MISSING_DATA || res == TextToSpeech.LANG_NOT_SUPPORTED) {
                    int resShort = tts.setLanguage(new Locale("pl"));
                    plSupported = resShort != TextToSpeech.LANG_MISSING_DATA
                        && resShort != TextToSpeech.LANG_NOT_SUPPORTED;
                } else {
                    plSupported = true;
                }
                ttsReady = true;
            }
        });

        WebView webView = getBridge().getWebView();
        if (webView != null) {
            WebSettings settings = webView.getSettings();
            settings.setMediaPlaybackRequiresUserGesture(false);
            settings.setDomStorageEnabled(true);
            webView.addJavascriptInterface(new PolskiTtsBridge(), "PolskiAndroidTTS");
        }
    }

    @Override
    public void onDestroy() {
        if (tts != null) {
            tts.stop();
            tts.shutdown();
            tts = null;
        }
        super.onDestroy();
    }

    private class PolskiTtsBridge {
        @JavascriptInterface
        public boolean speak(String text, float rate) {
            if (!ttsReady || tts == null || text == null || text.trim().isEmpty()) {
                return false;
            }
            runOnUiThread(() -> {
                if (tts != null) {
                    if (plSupported) {
                        tts.setLanguage(new Locale("pl", "PL"));
                    }
                    tts.setSpeechRate(Math.max(0.5f, Math.min(1.5f, rate)));
                    tts.speak(text, TextToSpeech.QUEUE_FLUSH, null, "polski_" + System.currentTimeMillis());
                }
            });
            return true;
        }

        @JavascriptInterface
        public void stop() {
            runOnUiThread(() -> {
                if (tts != null) {
                    tts.stop();
                }
            });
        }

        @JavascriptInterface
        public boolean isReady() {
            return ttsReady;
        }

        @JavascriptInterface
        public boolean isPolishSupported() {
            return plSupported;
        }

        @JavascriptInterface
        public void openTtsInstall() {
            runOnUiThread(() -> {
                try {
                    Intent intent = new Intent(TextToSpeech.Engine.ACTION_INSTALL_TTS_DATA);
                    intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    startActivity(intent);
                } catch (Exception ignored) {
                }
            });
        }
    }
}
