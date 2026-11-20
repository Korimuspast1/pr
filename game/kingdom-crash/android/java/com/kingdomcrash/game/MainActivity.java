package com.kingdomcrash.game;

import android.app.Activity;
import android.graphics.Color;
import android.media.AudioManager;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.webkit.RenderProcessGoneDetail;
import android.webkit.ValueCallback;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import android.widget.TextView;

import java.io.ByteArrayInputStream;

/**
 * Единственный экран приложения: полноэкранный WebView с игрой из папки assets/www.
 *
 * Игра целиком лежит внутри приложения. Сеть не используется (у приложения нет разрешения
 * на интернет), доступ к файлам телефона закрыт, мостов из страницы в систему нет:
 * оболочка только вызывает три функции игры — «назад», «сохранить» и «продолжить».
 */
public class MainActivity extends Activity {
    private static final String BASE = "file:///android_asset/www/";
    private static final String START = BASE + "index.html";
    private static final int BG = Color.rgb(0x10, 0x22, 0x2F);

    private final Handler ui = new Handler(Looper.getMainLooper());
    private FrameLayout root;
    private WebView web;
    private int backToken;      // номер последнего запроса «назад», на который ещё ждём ответ
    private int restarts;       // сколько раз подряд пришлось перезапускать игру после сбоя

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        setVolumeControlStream(AudioManager.STREAM_MUSIC); // кнопки громкости регулируют звук игры
        root = new FrameLayout(this);
        root.setBackgroundColor(BG);
        setContentView(root);
        startGame();
        hideBars();
    }

    /** Создаёт WebView и загружает игру. Если системного WebView нет — показывает понятное сообщение. */
    private void startGame() {
        try {
            web = createWeb();
            root.addView(web, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
            web.loadUrl(START);
            web.requestFocus();
        } catch (Throwable t) {
            web = null;
            showMessage(getString(R.string.no_webview));
        }
    }

    private WebView createWeb() {
        WebView.setWebContentsDebuggingEnabled(false);
        WebView w = new WebView(this);
        w.setBackgroundColor(BG);
        w.setOverScrollMode(View.OVER_SCROLL_NEVER);
        w.setVerticalScrollBarEnabled(false);
        w.setHorizontalScrollBarEnabled(false);
        w.setHapticFeedbackEnabled(false);
        w.setLongClickable(false);
        w.setOnLongClickListener(new View.OnLongClickListener() { // долгое нажатие не выделяет текст
            @Override public boolean onLongClick(View v) { return true; }
        });

        WebSettings s = w.getSettings();
        s.setJavaScriptEnabled(true);
        s.setDomStorageEnabled(true);              // сохранения игры
        s.setTextZoom(100);                        // размер шрифта в системе не ломает вёрстку
        s.setSupportZoom(false);
        s.setBuiltInZoomControls(false);
        s.setDisplayZoomControls(false);
        s.setMediaPlaybackRequiresUserGesture(false);
        s.setSupportMultipleWindows(false);
        s.setJavaScriptCanOpenWindowsAutomatically(false);
        s.setGeolocationEnabled(false);
        s.setSaveFormData(false);
        // страница не видит файлы телефона и чужие данные; папка assets доступна и при этих запретах
        s.setAllowFileAccess(false);
        s.setAllowContentAccess(false);
        s.setAllowFileAccessFromFileURLs(false);
        s.setAllowUniversalAccessFromFileURLs(false);
        s.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        if (Build.VERSION.SDK_INT >= 26) s.setSafeBrowsingEnabled(false);

        w.setWebViewClient(new Client());
        return w;
    }

    private final class Client extends WebViewClient {
        private boolean inside(String url) { return url != null && url.startsWith(BASE); }

        // переходы за пределы игры запрещены
        @Override public boolean shouldOverrideUrlLoading(WebView v, WebResourceRequest r) { return !inside(String.valueOf(r.getUrl())); }
        @SuppressWarnings("deprecation")
        @Override public boolean shouldOverrideUrlLoading(WebView v, String url) { return !inside(url); }

        // всё, что лежит не в папке игры, заменяется пустым ответом
        @Override public WebResourceResponse shouldInterceptRequest(WebView v, WebResourceRequest r) {
            Uri u = r.getUrl();
            String scheme = u == null ? null : u.getScheme();
            if (u != null && (inside(u.toString()) || "data".equals(scheme) || "blob".equals(scheme) || "about".equals(scheme))) return null;
            return new WebResourceResponse("text/plain", "utf-8", new ByteArrayInputStream(new byte[0]));
        }

        @Override public void onPageFinished(WebView v, String url) { restarts = 0; }

        // системе не хватило памяти, и она закрыла процесс страницы: запускаем игру заново, а не падаем
        @Override public boolean onRenderProcessGone(WebView v, RenderProcessGoneDetail d) {
            if (v == web) {
                dropWeb();
                if (++restarts <= 2) startGame(); else showMessage(getString(R.string.no_webview));
            }
            return true;
        }
    }

    private void dropWeb() {
        WebView w = web;
        web = null;
        if (w == null) return;
        try { root.removeView(w); w.destroy(); } catch (Throwable ignored) { }
    }

    private void showMessage(String text) {
        root.removeAllViews();
        TextView t = new TextView(this);
        t.setText(text);
        t.setTextColor(Color.rgb(0xF3, 0xEB, 0xD3));
        t.setTextSize(17);
        t.setGravity(Gravity.CENTER);
        int p = (int) (28 * getResources().getDisplayMetrics().density);
        t.setPadding(p, p, p, p);
        root.addView(t, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
    }

    /** Полноэкранный режим: системные панели скрыты и появляются на время по свайпу от края. */
    @SuppressWarnings("deprecation")
    private void hideBars() {
        getWindow().getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_FULLSCREEN
                | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) hideBars();
    }

    /** «Назад»: сначала спрашиваем игру (закрыть окно, открыть меню). Если ей нечего закрывать — выходим. */
    @SuppressWarnings("deprecation")
    @Override
    public void onBackPressed() {
        final WebView w = web;
        if (w == null) { finish(); return; }
        final int token = ++backToken;
        w.evaluateJavascript("(function(){try{return window.__kcBack?window.__kcBack()===true:false}catch(e){return false}})()", new ValueCallback<String>() {
            @Override public void onReceiveValue(String v) {
                if (token != backToken) return;
                backToken++;
                if (!"true".equals(v)) finish();
            }
        });
        // страница не отвечает — кнопка всё равно должна работать
        ui.postDelayed(new Runnable() {
            @Override public void run() { if (token == backToken) { backToken++; finish(); } }
        }, 1500);
    }

    @Override
    protected void onPause() {
        WebView w = web;
        if (w != null) {
            w.evaluateJavascript("window.__kcPause&&window.__kcPause()", null); // партия сохраняется
            w.onPause();
            w.pauseTimers();
        }
        super.onPause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        WebView w = web;
        if (w != null) {
            w.resumeTimers();
            w.onResume();
            w.evaluateJavascript("window.__kcResume&&window.__kcResume()", null);
        }
        hideBars();
    }

    @Override
    protected void onDestroy() {
        ui.removeCallbacksAndMessages(null);
        dropWeb();
        super.onDestroy();
    }
}
