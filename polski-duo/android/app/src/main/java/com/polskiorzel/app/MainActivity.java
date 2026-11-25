package com.polskiorzel.app;

import android.Manifest;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.media.AudioAttributes;
import android.os.Bundle;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;
import android.speech.tts.TextToSpeech;
import android.webkit.JavascriptInterface;
import android.webkit.WebSettings;
import android.webkit.WebView;
import androidx.annotation.NonNull;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import com.getcapacitor.BridgeActivity;
import java.util.ArrayList;
import java.util.Locale;
import org.json.JSONArray;
import org.json.JSONObject;

public class MainActivity extends BridgeActivity {
    private static final int REQ_RECORD_AUDIO = 4401;

    private TextToSpeech tts;
    private volatile boolean ttsReady = false;
    private volatile boolean ttsInitializing = false;
    private volatile boolean plSupported = false;
    private volatile String pendingText = null;
    private volatile float pendingRate = 0.92f;

    private SpeechRecognizer speechRecognizer;
    private volatile boolean sttListening = false;
    private volatile boolean pendingStartAfterPermission = false;

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
            webView.addJavascriptInterface(new PolskiSttBridge(), "PolskiAndroidSTT");
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

    private void dispatchSttEvent(JSONObject payload) {
        final String json = payload.toString();
        runOnUiThread(() -> {
            WebView webView = getBridge().getWebView();
            if (webView != null) {
                webView.evaluateJavascript(
                    "window.__polskiSttEvent && window.__polskiSttEvent(" + json + ");",
                    null
                );
            }
        });
    }

    private void emitSttError(String code, String message) {
        try {
            JSONObject obj = new JSONObject();
            obj.put("type", "error");
            obj.put("code", code);
            obj.put("message", message);
            dispatchSttEvent(obj);
        } catch (Exception ignored) {
        }
    }

    private void startNativeListening() {
        runOnUiThread(() -> {
            try {
                if (!SpeechRecognizer.isRecognitionAvailable(getApplicationContext())) {
                    emitSttError("unavailable", "Распознавание речи недоступно на устройстве (установите приложение Google).");
                    return;
                }

                // Pause TTS so microphone doesn't pick up playback
                if (tts != null) {
                    try {
                        tts.stop();
                    } catch (Exception ignored) {
                    }
                }

                if (speechRecognizer != null) {
                    try {
                        speechRecognizer.destroy();
                    } catch (Exception ignored) {
                    }
                    speechRecognizer = null;
                }

                speechRecognizer = SpeechRecognizer.createSpeechRecognizer(getApplicationContext());
                speechRecognizer.setRecognitionListener(new RecognitionListener() {
                    @Override
                    public void onReadyForSpeech(Bundle params) {
                        sttListening = true;
                        try {
                            JSONObject obj = new JSONObject();
                            obj.put("type", "ready");
                            dispatchSttEvent(obj);
                        } catch (Exception ignored) {
                        }
                    }

                    @Override
                    public void onBeginningOfSpeech() {
                        try {
                            JSONObject obj = new JSONObject();
                            obj.put("type", "speech_start");
                            dispatchSttEvent(obj);
                        } catch (Exception ignored) {
                        }
                    }

                    @Override
                    public void onRmsChanged(float rmsdB) {
                        try {
                            JSONObject obj = new JSONObject();
                            obj.put("type", "rms");
                            obj.put("value", rmsdB);
                            dispatchSttEvent(obj);
                        } catch (Exception ignored) {
                        }
                    }

                    @Override
                    public void onBufferReceived(byte[] buffer) {}

                    @Override
                    public void onEndOfSpeech() {
                        try {
                            JSONObject obj = new JSONObject();
                            obj.put("type", "speech_end");
                            dispatchSttEvent(obj);
                        } catch (Exception ignored) {
                        }
                    }

                    @Override
                    public void onError(int error) {
                        sttListening = false;
                        String code = "unknown";
                        String msg = "Не удалось распознать речь. Попробуйте ещё раз.";
                        switch (error) {
                            case SpeechRecognizer.ERROR_AUDIO:
                                code = "audio";
                                msg = "Ошибка записи с микрофона.";
                                break;
                            case SpeechRecognizer.ERROR_NETWORK:
                            case SpeechRecognizer.ERROR_NETWORK_TIMEOUT:
                                code = "network";
                                msg = "Для распознавания речи проверьте подключение к интернету или офлайн-пакет pl-PL.";
                                break;
                            case SpeechRecognizer.ERROR_NO_MATCH:
                                code = "no_match";
                                msg = "Речь не распознана. Говорите чуть громче и ближе к микрофону.";
                                break;
                            case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
                                code = "timeout";
                                msg = "Микрофон не услышал голос. Нажмите и сразу произнесите фразу.";
                                break;
                            case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                                code = "busy";
                                msg = "Микрофон занят, подождите секунду и попробуйте снова.";
                                break;
                            case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                                code = "permission_denied";
                                msg = "Нет доступа к микрофону. Разрешите запись звука в настройках.";
                                break;
                            case SpeechRecognizer.ERROR_LANGUAGE_NOT_SUPPORTED:
                            case SpeechRecognizer.ERROR_LANGUAGE_UNAVAILABLE:
                                code = "language_unavailable";
                                msg = "Польский язык распознавания (pl-PL) не найден в сервисе Google.";
                                break;
                            default:
                                break;
                        }
                        emitSttError(code, msg);
                    }

                    @Override
                    public void onResults(Bundle results) {
                        sttListening = false;
                        ArrayList<String> matches =
                            results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
                        float[] scores =
                            results.getFloatArray(SpeechRecognizer.CONFIDENCE_SCORES);

                        if (matches == null || matches.isEmpty()) {
                            emitSttError("no_match", "Речь не распознана. Попробуйте сказать чуть чётче.");
                            return;
                        }

                        try {
                            JSONObject obj = new JSONObject();
                            obj.put("type", "result");
                            JSONArray hyps = new JSONArray();
                            for (String m : matches) {
                                hyps.put(m);
                            }
                            obj.put("hypotheses", hyps);

                            JSONArray confs = new JSONArray();
                            if (scores != null) {
                                for (float s : scores) {
                                    confs.put(Math.max(0.0, Math.min(1.0, s)));
                                }
                            }
                            obj.put("confidences", confs);
                            dispatchSttEvent(obj);
                        } catch (Exception e) {
                            emitSttError("parse", "Ошибка обработки результата речи.");
                        }
                    }

                    @Override
                    public void onPartialResults(Bundle partialResults) {
                        ArrayList<String> partial =
                            partialResults.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
                        if (partial != null && !partial.isEmpty()) {
                            try {
                                JSONObject obj = new JSONObject();
                                obj.put("type", "partial");
                                obj.put("text", partial.get(0));
                                dispatchSttEvent(obj);
                            } catch (Exception ignored) {
                            }
                        }
                    }

                    @Override
                    public void onEvent(int eventType, Bundle params) {}
                });

                Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
                intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
                intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, "pl-PL");
                intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_PREFERENCE, "pl-PL");
                intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true);
                intent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 5);
                intent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_COMPLETE_SILENCE_LENGTH_MILLIS, 1800L);
                intent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_POSSIBLY_COMPLETE_SILENCE_LENGTH_MILLIS, 1500L);
                intent.putExtra(RecognizerIntent.EXTRA_CALLING_PACKAGE, getPackageName());

                speechRecognizer.startListening(intent);
            } catch (Exception e) {
                sttListening = false;
                emitSttError("exception", "Не удалось запустить микрофон: " + e.getMessage());
            }
        });
    }

    @Override
    public void onRequestPermissionsResult(
        int requestCode,
        @NonNull String[] permissions,
        @NonNull int[] grantResults
    ) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == REQ_RECORD_AUDIO) {
            boolean granted =
                grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED;
            if (granted && pendingStartAfterPermission) {
                pendingStartAfterPermission = false;
                startNativeListening();
            } else if (!granted) {
                pendingStartAfterPermission = false;
                emitSttError("permission_denied", "Доступ к микрофону отклонён. Разрешите микрофон для разговорных уровней.");
            }
        }
    }

    @Override
    public void onDestroy() {
        if (speechRecognizer != null) {
            try {
                speechRecognizer.destroy();
            } catch (Exception ignored) {
            }
            speechRecognizer = null;
        }
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

    private class PolskiSttBridge {
        @JavascriptInterface
        public boolean isAvailable() {
            try {
                return SpeechRecognizer.isRecognitionAvailable(getApplicationContext());
            } catch (Exception e) {
                return false;
            }
        }

        @JavascriptInterface
        public boolean isListening() {
            return sttListening;
        }

        @JavascriptInterface
        public void startListening() {
            if (ContextCompat.checkSelfPermission(MainActivity.this, Manifest.permission.RECORD_AUDIO)
                != PackageManager.PERMISSION_GRANTED) {
                pendingStartAfterPermission = true;
                ActivityCompat.requestPermissions(
                    MainActivity.this,
                    new String[] { Manifest.permission.RECORD_AUDIO },
                    REQ_RECORD_AUDIO
                );
                return;
            }
            startNativeListening();
        }

        @JavascriptInterface
        public void stopListening() {
            runOnUiThread(() -> {
                if (speechRecognizer != null && sttListening) {
                    try {
                        speechRecognizer.stopListening();
                    } catch (Exception ignored) {
                    }
                }
            });
        }

        @JavascriptInterface
        public void cancelListening() {
            sttListening = false;
            runOnUiThread(() -> {
                if (speechRecognizer != null) {
                    try {
                        speechRecognizer.cancel();
                    } catch (Exception ignored) {
                    }
                }
            });
        }
    }
}
