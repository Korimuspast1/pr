package com.polskiorzel.app;

import android.content.Intent;
import android.media.AudioAttributes;
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
    private volatile boolean ttsInitializing = false;
    private volatile boolean plSupported = false;
    private volatile String pendingText = null;
    private volatile float pendingRate = 0.92f;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        initTts(null);

        WebView webView = getBridge().getWebView();
        if (webView != null) {
            WebSettings settings = webView.getSettings();
            settings.setMediaPlaybackRequiresUserGesture(false);
            settings.setDomStorageEnabled(true);
            webView.addJavascriptInterface(new PolskiTtsBridge(), "PolskiAndroidTTS");
        }
    }

    @Override
    public void onResume() {
        super.onResume();
        if (!ttsReady && !ttsInitializing) {
            initTts(null);
        }
    }

    private synchronized void initTts(String enginePackage) {
        if (ttsInitializing) return;
        ttsInitializing = true;
        ttsReady = false;

        if (tts != null) {
            try {
                tts.stop();
                tts.shutdown();
            } catch (Exception ignored) {
            }
            tts = null;
        }

        TextToSpeech.OnInitListener listener = status -> {
            ttsInitializing = false;
            if (status == TextToSpeech.SUCCESS && tts != null) {
                configureAudioAttributes(tts);
                Locale pl = new Locale("pl", "PL");
                int res = tts.setLanguage(pl);
                if (res == TextToSpeech.LANG_MISSING_DATA || res == TextToSpeech.LANG_NOT_SUPPORTED) {
                    int resShort = tts.setLanguage(new Locale("pl"));
                    plSupported = resShort != TextToSpeech.LANG_MISSING_DATA
                        && resShort != TextToSpeech.LANG_NOT_SUPPORTED;
                    if (!plSupported) {
                        // Fall back to default locale only if Polish is strictly unavailable,
                        // keeping TTS operational rather than silent.
                        tts.setLanguage(Locale.getDefault());
                    }
                } else {
                    plSupported = true;
                }
                ttsReady = true;

                final String queued = pendingText;
                final float queuedRate = pendingRate;
                pendingText = null;
                if (queued != null && !queued.trim().isEmpty()) {
                    speakInternal(queued, queuedRate, false);
                }
            } else if (enginePackage == null) {
                // Retry explicitly with Google Speech Services if default engine failed
                initTts("com.google.android.tts");
            }
        };

        try {
            if (enginePackage != null) {
                tts = new TextToSpeech(getApplicationContext(), listener, enginePackage);
            } else {
                tts = new TextToSpeech(getApplicationContext(), listener);
            }
        } catch (Exception e) {
            ttsInitializing = false;
            ttsReady = false;
        }
    }

    private void configureAudioAttributes(TextToSpeech engine) {
        try {
            AudioAttributes attrs = new AudioAttributes.Builder()
                .setUsage(AudioAttributes.USAGE_MEDIA)
                .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
                .build();
            engine.setAudioAttributes(attrs);
        } catch (Exception ignored) {
        }
    }

    private void speakInternal(String text, float rate, boolean allowReinit) {
        runOnUiThread(() -> {
            if (tts == null || !ttsReady) {
                pendingText = text;
                pendingRate = rate;
                if (!ttsInitializing) {
                    initTts(null);
                }
                return;
            }
            try {
                if (plSupported) {
                    tts.setLanguage(new Locale("pl", "PL"));
                }
                tts.setSpeechRate(Math.max(0.5f, Math.min(1.5f, rate)));
                int result = tts.speak(
                    text,
                    TextToSpeech.QUEUE_FLUSH,
                    null,
                    "polski_" + System.currentTimeMillis()
                );
                if (result == TextToSpeech.ERROR && allowReinit) {
                    // Binder likely died in background; re-init and replay queued text
                    pendingText = text;
                    pendingRate = rate;
                    ttsReady = false;
                    initTts(null);
                }
            } catch (Exception e) {
                if (allowReinit) {
                    pendingText = text;
                    pendingRate = rate;
                    ttsReady = false;
                    initTts(null);
                }
            }
        });
    }

    @Override
    public void onDestroy() {
        if (tts != null) {
            try {
                tts.stop();
                tts.shutdown();
            } catch (Exception ignored) {
            }
            tts = null;
        }
        super.onDestroy();
    }

    private class PolskiTtsBridge {
        @JavascriptInterface
        public boolean speak(String text, float rate) {
            if (text == null || text.trim().isEmpty()) {
                return false;
            }
            if (!ttsReady) {
                pendingText = text;
                pendingRate = rate;
                if (!ttsInitializing) {
                    runOnUiThread(() -> initTts(null));
                }
                // Queued for immediate playback once engine finishes binding
                return true;
            }
            speakInternal(text, rate, true);
            return true;
        }

        @JavascriptInterface
        public void stop() {
            pendingText = null;
            runOnUiThread(() -> {
                if (tts != null) {
                    try {
                        tts.stop();
                    } catch (Exception ignored) {
                    }
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
