package lav.glasscam.alav;

import android.content.ContentValues;
import android.content.Context;
import android.graphics.ImageFormat;
import android.graphics.Rect;
import android.graphics.SurfaceTexture;
import android.hardware.camera2.CameraCaptureSession;
import android.hardware.camera2.CameraCharacteristics;
import android.hardware.camera2.CameraDevice;
import android.hardware.camera2.CameraManager;
import android.hardware.camera2.CameraMetadata;
import android.hardware.camera2.CaptureRequest;
import android.hardware.camera2.params.MeteringRectangle;
import android.hardware.camera2.params.StreamConfigurationMap;
import android.media.Image;
import android.media.ImageReader;
import android.media.MediaRecorder;
import android.os.Build;
import android.os.Environment;
import android.os.Handler;
import android.os.HandlerThread;
import android.provider.MediaStore;
import android.util.Size;
import android.view.Surface;

import java.io.OutputStream;
import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/**
 * Обёртка Camera2. Вся работа с камерой — на фоновом HandlerThread,
 * JPEG пишется асинхронно: превью никогда не подтормаживает.
 */
final class CameraController {

    interface Events {
        void onOpened();
        void onError(String message);
        void onPhotoSaved(String name);
        void onRecordingStarted();
        void onRecordingSaved(String name);
    }

    static final int FLASH_OFF = 0;
    static final int FLASH_AUTO = 1;
    static final int FLASH_ON = 2;
    static final int FLASH_TORCH = 3;

    private final Context context;
    private final Events events;
    private final HandlerThread thread;
    private final Handler handler;
    private final CameraManager manager;

    private CameraDevice camera;
    private CameraCaptureSession session;
    private ImageReader imageReader;
    private MediaRecorder recorder;
    private Surface previewSurface;
    private Surface recorderSurface;

    private String cameraId;
    private boolean front;
    private int sensorOrientation = 90;
    private int flashMode = FLASH_OFF;
    private float zoom = 1f;
    private float maxZoom = 4f;
    private Rect sensorRect;
    private boolean hasFlash;
    private boolean recording;
    private Size previewSize = new Size(1280, 720);
    private Size videoSize = new Size(1280, 720);
    private String videoTmpPath;

    CameraController(Context context, Events events) {
        this.context = context.getApplicationContext();
        this.events = events;
        this.manager = (CameraManager) context.getSystemService(Context.CAMERA_SERVICE);
        thread = new HandlerThread("camera");
        thread.start();
        handler = new Handler(thread.getLooper());
    }

    boolean isFront() {
        return front;
    }

    boolean hasFlash() {
        return hasFlash;
    }

    boolean isRecording() {
        return recording;
    }

    float getMaxZoom() {
        return maxZoom;
    }

    Size getPreviewSize() {
        return previewSize;
    }

    int getSensorOrientation() {
        return sensorOrientation;
    }

    // ── открытие ────────────────────────────────────────────────

    void open(boolean frontCamera, SurfaceTexture texture, int viewW, int viewH) {
        handler.post(() -> doOpen(frontCamera, texture, viewW, viewH));
    }

    private void doOpen(boolean frontCamera, SurfaceTexture texture,
                        int viewW, int viewH) {
        closeLocked();
        try {
            front = frontCamera;
            cameraId = findCamera(frontCamera);
            if (cameraId == null) {
                postError("no camera");
                return;
            }
            CameraCharacteristics ch = manager.getCameraCharacteristics(cameraId);
            Integer so = ch.get(CameraCharacteristics.SENSOR_ORIENTATION);
            sensorOrientation = so(ch);
            hasFlash = Boolean.TRUE.equals(
                    ch.get(CameraCharacteristics.FLASH_INFO_AVAILABLE));
            sensorRect = ch.get(
                    CameraCharacteristics.SENSOR_INFO_ACTIVE_ARRAY_SIZE);
            Float maxZ = ch.get(
                    CameraCharacteristics.SCALER_AVAILABLE_MAX_DIGITAL_ZOOM);
            maxZoom = maxZ == null ? 4f : Math.max(1f, Math.min(maxZ, 8f));
            StreamConfigurationMap map = ch.get(
                    CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP);
            if (map != null) {
                Size[] prev = map.getOutputSizes(SurfaceTexture.class);
                if (prev != null && prev.length > 0) {
                    previewSize = pickSize(prev, viewW, viewH, 1920);
                }
                Size[] vid = map.getOutputSizes(MediaRecorder.class);
                if (vid != null && vid.length > 0) {
                    videoSize = pickSize(vid, 1280, 720, 1920);
                }
            }
            imageReader = ImageReader.newInstance(
                    previewSize.getWidth(), previewSize.getHeight(),
                    ImageFormat.JPEG, 2);
            manager.openCamera(cameraId, stateCallback, handler);
        } catch (Exception e) {
            postError(String.valueOf(e.getMessage()));
        }
    }

    private int so(CameraCharacteristics ch) {
        Integer so = ch.get(CameraCharacteristics.SENSOR_ORIENTATION);
        return so == null ? 90 : so;
    }

    private String findCamera(boolean frontCamera) {
        try {
            int facing = frontCamera
                    ? CameraCharacteristics.LENS_FACING_FRONT
                    : CameraCharacteristics.LENS_FACING_BACK;
            String fallback = null;
            for (String id : manager.getCameraIdList()) {
                CameraCharacteristics ch = manager.getCameraCharacteristics(id);
                Integer f = ch.get(CameraCharacteristics.LENS_FACING);
                if (fallback == null) {
                    fallback = id;
                }
                if (f != null && f == facing) {
                    return id;
                }
            }
            return fallback;
        } catch (Exception e) {
            return null;
        }
    }

    /** Оптимальный размер: ближайший по пропорциям к вью, не больше cap. */
    private static Size pickSize(Size[] sizes, int viewW, int viewH, int cap) {
        double target = (double) Math.max(viewW, viewH) / Math.max(1, Math.min(viewW, viewH));
        Size best = sizes[0];
        double bestScore = Double.MAX_VALUE;
        for (Size s : sizes) {
            if (s.getWidth() > cap || s.getHeight() > cap) {
                continue;
            }
            double aspect = (double) s.getWidth() / s.getHeight();
            double score = Math.abs(aspect - target)
                    + Math.abs(s.getWidth() - 1280) / 4000.0;
            if (score < bestScore) {
                bestScore = score;
                best = s;
            }
        }
        return best;
    }

    private final CameraDevice.StateCallback stateCallback =
            new CameraDevice.StateCallback() {
                @Override
                public void onOpened(CameraDevice device) {
                    camera = device;
                    events.onOpened();
                }

                @Override
                public void onDisconnected(CameraDevice device) {
                    device.close();
                    camera = null;
                    postError("camera disconnected");
                }

                @Override
                public void onError(CameraDevice device, int error) {
                    device.close();
                    camera = null;
                    postError("camera error " + error);
                }
            };

    private boolean currentRecorderOn;

    void startPreview(SurfaceTexture texture) {
        handler.post(() -> doStartPreview(texture));
    }

    private void doStartPreview(SurfaceTexture texture) {
        if (camera == null) {
            return;
        }
        try {
            if (session != null) {
                session.close();
                session = null;
            }
            currentRecorderOn = false;
            texture.setDefaultBufferSize(
                    previewSize.getWidth(), previewSize.getHeight());
            if (previewSurface != null) {
                previewSurface.release();
            }
            previewSurface = new Surface(texture);
            startSession();
        } catch (Exception e) {
            postError(String.valueOf(e.getMessage()));
        }
    }

    private void startSession() {
        try {
            List<Surface> targets = new ArrayList<>();
            targets.add(previewSurface);
            if (currentRecorderOn && recorderSurface != null) {
                targets.add(recorderSurface);
            } else if (imageReader != null) {
                targets.add(imageReader.getSurface());
            }
            camera.createCaptureSession(targets,
                    new CameraCaptureSession.StateCallback() {
                        @Override
                        public void onConfigured(CameraCaptureSession s) {
                            session = s;
                            startRepeating();
                        }

                        @Override
                        public void onConfigureFailed(CameraCaptureSession s) {
                            postError("session config failed");
                        }
                    }, handler);
        } catch (Exception e) {
            postError(String.valueOf(e.getMessage()));
        }
    }

    private void startRepeating() {
        try {
            int template = currentRecorderOn
                    ? CameraDevice.TEMPLATE_RECORD
                    : CameraDevice.TEMPLATE_PREVIEW;
            CaptureRequest.Builder b = camera.createCaptureRequest(template);
            b.addTarget(previewSurface);
            if (currentRecorderOn && recorderSurface != null) {
                b.addTarget(recorderSurface);
            } else if (imageReader != null) {
                b.addTarget(imageReader.getSurface());
            }
            applyCommon(b);
            session.setRepeatingRequest(b.build(), null, handler);
        } catch (Exception e) {
            postError(String.valueOf(e.getMessage()));
        }
    }

    private void applyCommon(CaptureRequest.Builder b) {
        b.set(CaptureRequest.CONTROL_MODE, CameraMetadata.CONTROL_MODE_AUTO);
        b.set(CaptureRequest.CONTROL_AF_MODE,
                CaptureRequest.CONTROL_AF_MODE_CONTINUOUS_PICTURE);
        applyFlash(b);
        applyZoom(b);
    }

    private void applyFlash(CaptureRequest.Builder b) {
        switch (flashMode) {
            case FLASH_AUTO:
                b.set(CaptureRequest.CONTROL_AE_MODE,
                        CaptureRequest.CONTROL_AE_MODE_ON_AUTO_FLASH);
                break;
            case FLASH_ON:
                b.set(CaptureRequest.CONTROL_AE_MODE,
                        CaptureRequest.CONTROL_AE_MODE_ON_ALWAYS_FLASH);
                break;
            case FLASH_TORCH:
                b.set(CaptureRequest.CONTROL_AE_MODE,
                        CaptureRequest.CONTROL_AE_MODE_ON);
                b.set(CaptureRequest.FLASH_MODE,
                        CaptureRequest.FLASH_MODE_TORCH);
                break;
            default:
                b.set(CaptureRequest.CONTROL_AE_MODE,
                        CaptureRequest.CONTROL_AE_MODE_ON);
                b.set(CaptureRequest.FLASH_MODE,
                        CaptureRequest.FLASH_MODE_OFF);
        }
    }

    private void applyZoom(CaptureRequest.Builder b) {
        if (sensorRect == null) {
            return;
        }
        float z = Math.max(1f, Math.min(zoom, maxZoom));
        int w = sensorRect.width();
        int h = sensorRect.height();
        int cw = (int) (w / z);
        int ch = (int) (h / z);
        int left = (w - cw) / 2;
        int top = (h - ch) / 2;
        b.set(CaptureRequest.SCALER_CROP_REGION,
                new Rect(left, top, left + cw, top + ch));
    }

    // ── зум / вспышка / фокус ───────────────────────────────────

    void setZoom(float z) {
        zoom = Math.max(1f, Math.min(z, maxZoom));
        refreshRepeating();
    }

    float getZoom() {
        return zoom;
    }

    void setFlashMode(int mode) {
        flashMode = mode;
        refreshRepeating();
    }

    int getFlashMode() {
        return flashMode;
    }

    void tapToFocus(float viewX, float viewY, int viewW, int viewH) {
        handler.post(() -> {
            if (camera == null || session == null || sensorRect == null) {
                return;
            }
            try {
                // превью подогнано под вью точно по пропорциям (aspect-fit)
                float cropX = viewX / Math.max(1f, viewW);
                float cropY = viewY / Math.max(1f, viewH);
                // портрет: сенсор повёрнут на 90°
                int sx = (int) (sensorRect.left + cropY * sensorRect.width());
                int sy = (int) (sensorRect.top + (1f - cropX) * sensorRect.height());
                if (front) {
                    sx = sensorRect.left + sensorRect.width() - (sx - sensorRect.left);
                }
                int half = Math.min(sensorRect.width(), sensorRect.height()) / 8;
                Rect area = new Rect(sx - half, sy - half, sx + half, sy + half);
                MeteringRectangle mr = new MeteringRectangle(area,
                        MeteringRectangle.METERING_WEIGHT_MAX);
                CaptureRequest.Builder b = camera.createCaptureRequest(
                        CameraDevice.TEMPLATE_PREVIEW);
                b.addTarget(previewSurface);
                if (imageReader != null && !currentRecorderOn) {
                    b.addTarget(imageReader.getSurface());
                }
                applyCommon(b);
                b.set(CaptureRequest.CONTROL_AF_REGIONS,
                        new MeteringRectangle[]{mr});
                b.set(CaptureRequest.CONTROL_AE_REGIONS,
                        new MeteringRectangle[]{mr});
                b.set(CaptureRequest.CONTROL_AF_MODE,
                        CaptureRequest.CONTROL_AF_MODE_AUTO);
                b.set(CaptureRequest.CONTROL_AF_TRIGGER,
                        CaptureRequest.CONTROL_AF_TRIGGER_START);
                session.capture(b.build(), null, handler);
            } catch (Exception ignored) {
            }
        });
    }

    private void refreshRepeating() {
        handler.post(() -> {
            if (camera == null || session == null) {
                return;
            }
            try {
                int template = currentRecorderOn
                        ? CameraDevice.TEMPLATE_RECORD
                        : CameraDevice.TEMPLATE_PREVIEW;
                CaptureRequest.Builder b = camera.createCaptureRequest(template);
                b.addTarget(previewSurface);
                if (currentRecorderOn && recorderSurface != null) {
                    b.addTarget(recorderSurface);
                } else if (imageReader != null) {
                    b.addTarget(imageReader.getSurface());
                }
                applyCommon(b);
                session.setRepeatingRequest(b.build(), null, handler);
            } catch (Exception ignored) {
            }
        });
    }

    // ── фото ────────────────────────────────────────────────────

    void takePicture(int displayRotation) {
        handler.post(() -> {
            if (camera == null || session == null || imageReader == null) {
                return;
            }
            try {
                imageReader.setOnImageAvailableListener(reader -> {
                    Image img = reader.acquireLatestImage();
                    if (img == null) {
                        return;
                    }
                    ByteBuffer buf = img.getPlanes()[0].getBuffer();
                    byte[] data = new byte[buf.remaining()];
                    buf.get(data);
                    img.close();
                    saveJpeg(data);
                    startRepeating();
                }, handler);
                CaptureRequest.Builder b = camera.createCaptureRequest(
                        CameraDevice.TEMPLATE_STILL_CAPTURE);
                b.addTarget(imageReader.getSurface());
                applyCommon(b);
                b.set(CaptureRequest.CONTROL_AF_MODE,
                        CaptureRequest.CONTROL_AF_MODE_CONTINUOUS_PICTURE);
                b.set(CaptureRequest.JPEG_ORIENTATION,
                        jpegOrientation(displayRotation));
                session.stopRepeating();
                session.capture(b.build(), null, handler);
            } catch (Exception e) {
                postError(String.valueOf(e.getMessage()));
            }
        });
    }

    private int jpegOrientation(int displayRotation) {
        int[] deg = {0, 90, 180, 270};
        int rot = deg[displayRotation & 3];
        if (front) {
            return (sensorOrientation + rot) % 360;
        }
        return (sensorOrientation - rot + 360) % 360;
    }

    private void saveJpeg(byte[] data) {
        try {
            String name = "GlassCam_" + System.currentTimeMillis() + ".jpg";
            ContentValues v = new ContentValues();
            v.put(MediaStore.Images.Media.DISPLAY_NAME, name);
            v.put(MediaStore.Images.Media.MIME_TYPE, "image/jpeg");
            if (Build.VERSION.SDK_INT >= 29) {
                v.put(MediaStore.Images.Media.RELATIVE_PATH,
                        Environment.DIRECTORY_PICTURES + "/GlassCam");
                v.put(MediaStore.Images.Media.IS_PENDING, 1);
            }
            android.net.Uri uri = context.getContentResolver().insert(
                    MediaStore.Images.Media.EXTERNAL_CONTENT_URI, v);
            if (uri == null) {
                postError("save failed");
                return;
            }
            OutputStream out = context.getContentResolver().openOutputStream(uri);
            out.write(data);
            out.close();
            if (Build.VERSION.SDK_INT >= 29) {
                ContentValues done = new ContentValues();
                done.put(MediaStore.Images.Media.IS_PENDING, 0);
                context.getContentResolver().update(uri, done, null, null);
            }
            events.onPhotoSaved(name);
        } catch (Exception e) {
            postError(String.valueOf(e.getMessage()));
        }
    }

    // ── видео ───────────────────────────────────────────────────

    void startVideo(SurfaceTexture texture) {
        handler.post(() -> {
            if (camera == null || recording) {
                return;
            }
            try {
                videoTmpPath = context.getCacheDir() + "/rec_tmp.mp4";
                recorder = new MediaRecorder();
                recorder.setAudioSource(MediaRecorder.AudioSource.MIC);
                recorder.setVideoSource(MediaRecorder.VideoSource.SURFACE);
                recorder.setOutputFormat(MediaRecorder.OutputFormat.MPEG_4);
                recorder.setOutputFile(videoTmpPath);
                recorder.setVideoEncoder(MediaRecorder.VideoEncoder.H264);
                recorder.setAudioEncoder(MediaRecorder.AudioEncoder.AAC);
                recorder.setVideoSize(videoSize.getWidth(), videoSize.getHeight());
                recorder.setVideoFrameRate(30);
                recorder.setVideoEncodingBitRate(10_000_000);
                recorder.setOrientationHint(videoOrientation());
                recorder.prepare();
                recorderSurface = recorder.getSurface();
                if (session != null) {
                    session.close();
                    session = null;
                }
                currentRecorderOn = true;
                texture.setDefaultBufferSize(previewSize.getWidth(),
                        previewSize.getHeight());
                previewSurface = new Surface(texture);
                startSessionWithRecorderWait();
            } catch (Exception e) {
                postError(String.valueOf(e.getMessage()));
            }
        });
    }

    /** Сессия с рекордером; MediaRecorder.start только после конфигурации. */
    private void startSessionWithRecorderWait() {
        try {
            List<Surface> targets = Arrays.asList(previewSurface, recorderSurface);
            camera.createCaptureSession(targets,
                    new CameraCaptureSession.StateCallback() {
                        @Override
                        public void onConfigured(CameraCaptureSession s) {
                            session = s;
                            startRepeating();
                            try {
                                recorder.start();
                                recording = true;
                                events.onRecordingStarted();
                            } catch (Exception e) {
                                postError(String.valueOf(e.getMessage()));
                            }
                        }

                        @Override
                        public void onConfigureFailed(CameraCaptureSession s) {
                            postError("video session failed");
                        }
                    }, handler);
        } catch (Exception e) {
            postError(String.valueOf(e.getMessage()));
        }
    }

    private int videoOrientation() {
        int result = sensorOrientation;
        if (front) {
            result = (360 - sensorOrientation) % 360;
        }
        return result;
    }

    void stopVideo(SurfaceTexture texture) {
        handler.post(() -> {
            if (!recording) {
                return;
            }
            recording = false;
            try {
                session.stopRepeating();
            } catch (Exception ignored) {
            }
            try {
                recorder.stop();
            } catch (Exception ignored) {
            }
            try {
                recorder.reset();
                recorder.release();
            } catch (Exception ignored) {
            }
            recorder = null;
            recorderSurface = null;
            currentRecorderOn = false;
            publishVideo();
            // пересоздаём превью-сессию
            try {
                texture.setDefaultBufferSize(previewSize.getWidth(),
                        previewSize.getHeight());
                previewSurface = new Surface(texture);
                startSession();
            } catch (Exception e) {
                postError(String.valueOf(e.getMessage()));
            }
        });
    }

    private void publishVideo() {
        try {
            java.io.File tmp = new java.io.File(videoTmpPath);
            String name = "GlassCam_" + System.currentTimeMillis() + ".mp4";
            ContentValues v = new ContentValues();
            v.put(MediaStore.Video.Media.DISPLAY_NAME, name);
            v.put(MediaStore.Video.Media.MIME_TYPE, "video/mp4");
            if (Build.VERSION.SDK_INT >= 29) {
                v.put(MediaStore.Video.Media.RELATIVE_PATH,
                        Environment.DIRECTORY_MOVIES + "/GlassCam");
                v.put(MediaStore.Video.Media.IS_PENDING, 1);
            }
            android.net.Uri uri = context.getContentResolver().insert(
                    MediaStore.Video.Media.EXTERNAL_CONTENT_URI, v);
            if (uri == null) {
                postError("save failed");
                return;
            }
            OutputStream out = context.getContentResolver().openOutputStream(uri);
            java.io.InputStream in = new java.io.FileInputStream(tmp);
            byte[] buf = new byte[64 * 1024];
            int r;
            while ((r = in.read(buf)) > 0) {
                out.write(buf, 0, r);
            }
            in.close();
            out.close();
            if (Build.VERSION.SDK_INT >= 29) {
                ContentValues done = new ContentValues();
                done.put(MediaStore.Video.Media.IS_PENDING, 0);
                context.getContentResolver().update(uri, done, null, null);
            }
            tmp.delete();
            events.onRecordingSaved(name);
        } catch (Exception e) {
            postError(String.valueOf(e.getMessage()));
        }
    }

    // ── жизненный цикл ──────────────────────────────────────────

    void close() {
        handler.post(this::closeLocked);
    }

    private void closeLocked() {
        try {
            if (session != null) {
                session.close();
                session = null;
            }
        } catch (Exception ignored) {
        }
        try {
            if (camera != null) {
                camera.close();
                camera = null;
            }
        } catch (Exception ignored) {
        }
        if (previewSurface != null) {
            previewSurface.release();
            previewSurface = null;
        }
    }

    void shutdown() {
        close();
        handler.post(() -> {
            if (imageReader != null) {
                imageReader.close();
                imageReader = null;
            }
        });
        thread.quitSafely();
    }

    private void postError(String message) {
        events.onError(message);
    }
}
