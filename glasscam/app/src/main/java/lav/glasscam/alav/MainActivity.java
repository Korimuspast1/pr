package lav.glasscam.alav;

import android.Manifest;
import android.app.Activity;
import android.content.Context;
import android.content.pm.PackageManager;
import android.graphics.Matrix;
import android.graphics.SurfaceTexture;
import android.media.MediaActionSound;
import android.os.Bundle;
import android.os.CountDownTimer;
import android.util.Size;
import android.view.MotionEvent;
import android.view.ScaleGestureDetector;
import android.view.TextureView;
import android.view.View;
import android.view.WindowManager;
import android.view.animation.Animation;
import android.view.animation.ScaleAnimation;
import android.widget.ImageView;
import android.widget.TextView;
import android.widget.Toast;

/**
 * Камера в стиле GCam: превью на весь экран, большая кнопка съёмки,
 * режимы Фото/Видео, вспышка, сетка, таймер, зум щипком, тап-фокус.
 */
public class MainActivity extends Activity implements CameraController.Events {

    private static final int REQ_PERMISSIONS = 42;

    private CameraController cam;
    private TextureView textureView;
    private GridView grid;
    private View focusRing;
    private TextView countdownView;
    private TextView recordTimer;
    private TextView zoomLabel;
    private ImageView btnFlash;
    private ImageView btnGrid;
    private ImageView btnTimer;
    private ImageView btnShutter;
    private TextView modePhoto;
    private TextView modeVideo;
    private TextView mpLabel;
    private TextView btnQuality;

    private boolean videoMode;
    private boolean gridOn;
    private int timerSeconds;
    private boolean cameraReady;
    private boolean permissionAsked;
    private long recordStart;

    private final MediaActionSound actionSound = new MediaActionSound();
    private ScaleGestureDetector scaleDetector;

    private final android.os.Handler uiHandler =
            new android.os.Handler(android.os.Looper.getMainLooper());
    private final Runnable timerTick = new Runnable() {
        @Override
        public void run() {
            if (cam != null && cam.isRecording()) {
                long s = (System.currentTimeMillis() - recordStart) / 1000;
                recordTimer.setText(String.format(java.util.Locale.US,
                        "%02d:%02d", s / 60, s % 60));
                uiHandler.postDelayed(this, 500);
            }
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        getWindow().getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                        | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN);

        textureView = findViewById(R.id.texture);
        grid = findViewById(R.id.grid);
        focusRing = findViewById(R.id.focus_ring);
        countdownView = findViewById(R.id.countdown);
        recordTimer = findViewById(R.id.record_timer);
        zoomLabel = findViewById(R.id.zoom_label);
        btnFlash = findViewById(R.id.btn_flash);
        btnGrid = findViewById(R.id.btn_grid);
        btnTimer = findViewById(R.id.btn_timer);
        btnShutter = findViewById(R.id.btn_shutter);
        modePhoto = findViewById(R.id.mode_photo);
        modeVideo = findViewById(R.id.mode_video);
        mpLabel = findViewById(R.id.mp_label);
        btnQuality = findViewById(R.id.btn_quality);

        actionSound.load(MediaActionSound.SHUTTER_CLICK);
        actionSound.load(MediaActionSound.START_VIDEO_RECORDING);
        actionSound.load(MediaActionSound.STOP_VIDEO_RECORDING);

        cam = new CameraController(this, this);

        scaleDetector = new ScaleGestureDetector(this,
                new ScaleGestureDetector.SimpleOnScaleGestureListener() {
                    @Override
                    public boolean onScale(ScaleGestureDetector d) {
                        cam.setZoom(cam.getZoom() * d.getScaleFactor());
                        showZoom();
                        return true;
                    }
                });

        textureView.setOnTouchListener((v, event) -> {
            scaleDetector.onTouchEvent(event);
            if (event.getAction() == MotionEvent.ACTION_DOWN
                    && event.getPointerCount() == 1) {
                showFocusRing(event.getX(), event.getY());
                cam.tapToFocus(event.getX(), event.getY(),
                        textureView.getWidth(), textureView.getHeight());
            }
            return true;
        });

        textureView.setSurfaceTextureListener(
                new TextureView.SurfaceTextureListener() {
                    @Override
                    public void onSurfaceTextureAvailable(SurfaceTexture st,
                                                          int w, int h) {
                        openCameraIfPermitted();
                    }

                    @Override
                    public void onSurfaceTextureSizeChanged(SurfaceTexture st,
                                                            int w, int h) {
                        if (cameraReady) {
                            applyTextureTransform();
                        }
                    }

                    @Override
                    public boolean onSurfaceTextureDestroyed(SurfaceTexture st) {
                        return true;
                    }

                    @Override
                    public void onSurfaceTextureUpdated(SurfaceTexture st) {
                    }
                });

        btnShutter.setOnClickListener(v -> onShutter());
        findViewById(R.id.btn_switch).setOnClickListener(v -> switchCamera());
        btnFlash.setOnClickListener(v -> cycleFlash());
        btnGrid.setOnClickListener(v -> toggleGrid());
        btnTimer.setOnClickListener(v -> cycleTimer());
        btnQuality.setOnClickListener(v -> cycleQuality());
        modePhoto.setOnClickListener(v -> setVideoMode(false));
        modeVideo.setOnClickListener(v -> setVideoMode(true));
        updateFlashIcon();
    }

    // ── разрешения ──────────────────────────────────────────────

    private void openCameraIfPermitted() {
        if (checkSelfPermission(Manifest.permission.CAMERA)
                != PackageManager.PERMISSION_GRANTED
                || checkSelfPermission(Manifest.permission.RECORD_AUDIO)
                != PackageManager.PERMISSION_GRANTED) {
            if (!permissionAsked) {
                permissionAsked = true;
                requestPermissions(new String[]{Manifest.permission.CAMERA,
                        Manifest.permission.RECORD_AUDIO}, REQ_PERMISSIONS);
            }
            return;
        }
        if (!cameraReady && textureView.getSurfaceTexture() != null) {
            cam.open(false, textureView.getSurfaceTexture(),
                    textureView.getWidth(), textureView.getHeight());
        }
    }

    @Override
    public void onRequestPermissionsResult(int requestCode,
                                           String[] permissions,
                                           int[] grantResults) {
        if (requestCode == REQ_PERMISSIONS) {
            boolean ok = true;
            for (int r : grantResults) {
                ok &= r == PackageManager.PERMISSION_GRANTED;
            }
            if (ok) {
                openCameraIfPermitted();
            } else {
                Toast.makeText(this, R.string.need_permissions,
                        Toast.LENGTH_LONG).show();
            }
        }
    }

    // ── события камеры ──────────────────────────────────────────

    @Override
    public void onOpened() {
        runOnUiThread(() -> {
            cameraReady = true;
            cam.startPreview(textureView.getSurfaceTexture());
            applyTextureTransform();
            updateCameraInfo();
        });
    }

    @Override
    public void onError(String message) {
        runOnUiThread(() -> Toast.makeText(this,
                getString(R.string.camera_error) + ": " + message,
                Toast.LENGTH_LONG).show());
    }

    @Override
    public void onPhotoSaved(String name) {
        runOnUiThread(() -> Toast.makeText(this,
                getString(R.string.photo_saved), Toast.LENGTH_SHORT).show());
    }

    @Override
    public void onRecordingStarted() {
        runOnUiThread(() -> {
            recordStart = System.currentTimeMillis();
            recordTimer.setVisibility(View.VISIBLE);
            recordTimer.setText("00:00");
            btnShutter.setImageResource(R.drawable.ic_stop);
            uiHandler.post(timerTick);
        });
    }

    @Override
    public void onRecordingSaved(String name) {
        runOnUiThread(() -> Toast.makeText(this,
                getString(R.string.video_saved), Toast.LENGTH_SHORT).show());
    }

    // ── UI ──────────────────────────────────────────────────────

    private void onShutter() {
        if (!cameraReady) {
            return;
        }
        if (videoMode) {
            if (cam.isRecording()) {
                actionSound.play(MediaActionSound.STOP_VIDEO_RECORDING);
                recordTimer.setVisibility(View.GONE);
                btnShutter.setImageResource(R.drawable.ic_shutter);
                cam.stopVideo(textureView.getSurfaceTexture());
            } else {
                actionSound.play(MediaActionSound.START_VIDEO_RECORDING);
                cam.startVideo(textureView.getSurfaceTexture());
            }
            return;
        }
        if (timerSeconds > 0) {
            startCountdown();
        } else {
            shoot();
        }
    }

    private void shoot() {
        actionSound.play(MediaActionSound.SHUTTER_CLICK);
        int rot = getWindowManager().getDefaultDisplay().getRotation();
        cam.takePicture(rot);
    }

    private void startCountdown() {
        btnShutter.setEnabled(false);
        countdownView.setVisibility(View.VISIBLE);
        new CountDownTimer(timerSeconds * 1000L, 1000L) {
            @Override
            public void onTick(long millisUntilFinished) {
                countdownView.setText(
                        String.valueOf(millisUntilFinished / 1000 + 1));
            }

            @Override
            public void onFinish() {
                countdownView.setVisibility(View.GONE);
                btnShutter.setEnabled(true);
                shoot();
            }
        }.start();
    }

    private void switchCamera() {
        if (!cameraReady) {
            return;
        }
        cameraReady = false;
        cam.open(!cam.isFront(), textureView.getSurfaceTexture(),
                textureView.getWidth(), textureView.getHeight());
    }

    private void cycleFlash() {
        int next = (cam.getFlashMode() + 1) % 4;
        cam.setFlashMode(next);
        updateFlashIcon();
    }

    private void updateFlashIcon() {
        int icon;
        int label;
        switch (cam.getFlashMode()) {
            case CameraController.FLASH_AUTO:
                icon = R.drawable.ic_flash_auto;
                label = R.string.flash_auto;
                break;
            case CameraController.FLASH_ON:
                icon = R.drawable.ic_flash_on;
                label = R.string.flash_on;
                break;
            case CameraController.FLASH_TORCH:
                icon = R.drawable.ic_torch;
                label = R.string.flash_torch;
                break;
            default:
                icon = R.drawable.ic_flash_off;
                label = R.string.flash_off;
        }
        btnFlash.setImageResource(icon);
        btnFlash.setContentDescription(getString(label));
    }

    private void toggleGrid() {
        gridOn = !gridOn;
        grid.setVisibility(gridOn ? View.VISIBLE : View.GONE);
        btnGrid.setAlpha(gridOn ? 1f : 0.55f);
    }

    private void cycleTimer() {
        timerSeconds = timerSeconds == 0 ? 3 : timerSeconds == 3 ? 10 : 0;
        btnTimer.setImageResource(timerSeconds == 0
                ? R.drawable.ic_timer_off : R.drawable.ic_timer);
        Toast.makeText(this, timerSeconds == 0
                ? getString(R.string.timer_off)
                : getString(R.string.timer_fmt, timerSeconds),
                Toast.LENGTH_SHORT).show();
    }

    private void setVideoMode(boolean video) {
        if (video == videoMode || cam.isRecording()) {
            return;
        }
        videoMode = video;
        modePhoto.setTextColor(getColor(video
                ? R.color.hud_dim : R.color.hud_active));
        modeVideo.setTextColor(getColor(video
                ? R.color.hud_active : R.color.hud_dim));
    }

    private void showZoom() {
        zoomLabel.setText(String.format(java.util.Locale.US, "%.1fx / %.0fx",
                cam.getZoom(), cam.getMaxZoom()));
        zoomLabel.setVisibility(View.VISIBLE);
        zoomLabel.removeCallbacks(hideZoom);
        zoomLabel.postDelayed(hideZoom, 1200);
    }

    private final Runnable hideZoom =
            () -> zoomLabel.setVisibility(View.GONE);

    private void showFocusRing(float x, float y) {
        focusRing.setTranslationX(x - focusRing.getWidth() / 2f);
        focusRing.setTranslationY(y - focusRing.getHeight() / 2f);
        focusRing.setVisibility(View.VISIBLE);
        ScaleAnimation anim = new ScaleAnimation(1.5f, 1f, 1.5f, 1f,
                Animation.RELATIVE_TO_SELF, 0.5f,
                Animation.RELATIVE_TO_SELF, 0.5f);
        anim.setDuration(280);
        focusRing.startAnimation(anim);
        focusRing.removeCallbacks(hideRing);
        focusRing.postDelayed(hideRing, 900);
    }

    private final Runnable hideRing =
            () -> focusRing.setVisibility(View.GONE);

    /**
     * Превью без искажений: center-crop матрицей на весь экран.
     * Система отдаёт буфер уже повёрнутым в портрет, поэтому ширина буфера —
     * это высота превью-размера. Масштабируем так, чтобы вью было заполнено
     * полностью, а выступающие полосы обрезались.
     */
    private void applyTextureTransform() {
        int vw = textureView.getWidth();
        int vh = textureView.getHeight();
        if (vw == 0 || vh == 0) {
            return;
        }
        float bufW = cam.getPreviewSize().getHeight();
        float bufH = cam.getPreviewSize().getWidth();
        float sx = vw / bufW;
        float sy = vh / bufH;
        float scale = Math.max(sx, sy);
        Matrix m = new Matrix();
        m.setScale(scale / sx, scale / sy, vw / 2f, vh / 2f);
        textureView.setTransform(m);
    }

    /** Реальные мегапиксели фото + текущее качество видео. */
    private void updateCameraInfo() {
        Size s = cam.getStillSize();
        float mp = s.getWidth() * (float) s.getHeight() / 1_000_000f;
        String mpText = mp >= 10f
                ? String.valueOf(Math.round(mp))
                : String.format(java.util.Locale.US, "%.1f", mp);
        mpLabel.setText(getString(R.string.mp_fmt, mpText));
        btnQuality.setText(qualityLabel(
                cam.getVideoSizes().get(cam.getVideoSizeIndex())));
    }

    private void cycleQuality() {
        if (!cameraReady || cam.isRecording()) {
            return;
        }
        int n = cam.getVideoSizes().size();
        int idx = (cam.getVideoSizeIndex() + 1) % n;
        cam.setVideoSizeIndex(idx);
        String label = qualityLabel(cam.getVideoSizes().get(idx));
        btnQuality.setText(label);
        Toast.makeText(this, getString(R.string.video_quality_fmt, label),
                Toast.LENGTH_SHORT).show();
    }

    private String qualityLabel(Size s) {
        int h = Math.min(s.getWidth(), s.getHeight());
        if (h >= 2160) {
            return "4K";
        }
        if (h >= 1440) {
            return "2K";
        }
        return h + "p";
    }

    @Override
    protected void onPause() {
        cam.close();
        cameraReady = false;
        super.onPause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (textureView.isAvailable()) {
            openCameraIfPermitted();
        }
    }

    @Override
    protected void onDestroy() {
        cam.shutdown();
        actionSound.release();
        super.onDestroy();
    }
}
