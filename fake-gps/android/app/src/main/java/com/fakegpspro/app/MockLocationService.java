package com.fakegpspro.app;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.location.Location;
import android.location.LocationManager;
import android.location.provider.ProviderProperties;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.os.SystemClock;
import androidx.core.app.NotificationCompat;
import com.google.android.gms.location.FusedLocationProviderClient;
import com.google.android.gms.location.LocationServices;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Random;
import org.json.JSONArray;
import org.json.JSONObject;

public class MockLocationService extends Service {
    public static final String ACTION_START = "com.fakegpspro.ACTION_START";
    public static final String ACTION_UPDATE = "com.fakegpspro.ACTION_UPDATE";
    public static final String ACTION_START_ROUTE = "com.fakegpspro.ACTION_START_ROUTE";
    public static final String ACTION_STOP_ROUTE = "com.fakegpspro.ACTION_STOP_ROUTE";
    public static final String ACTION_STOP = "com.fakegpspro.ACTION_STOP";

    public static final String CHANNEL_ID = "fakegps_spoof_channel";
    public static final int NOTIF_ID = 9012;

    public static volatile boolean isRunning = false;
    public static volatile boolean isRouteRunning = false;
    public static volatile double liveLat = 52.2297;
    public static volatile double liveLng = 21.0122;
    public static volatile float liveBearing = 0f;
    public static volatile float liveSpeedKmh = 0f;
    public static volatile String lastError = "";

    private LocationManager locationManager;
    private FusedLocationProviderClient fusedClient;
    private Handler handler;
    private final Random random = new Random();

    private float accuracyMeters = 4.0f;
    private double altitudeMeters = 110.0;
    private double jitterMeters = 1.5;
    private long intervalMs = 600L;

    private final List<double[]> routeWaypoints = new ArrayList<>();
    private int routeSegIndex = 0;
    private double routeProgressMeters = 0.0;
    private boolean routeLoop = true;

    private final Runnable tickRunnable = new Runnable() {
        @Override
        public void run() {
            if (!isRunning) return;
            if (isRouteRunning && routeWaypoints.size() >= 2) {
                advanceRoute(intervalMs / 1000.0);
            }
            pushMockLocation();
            handler.postDelayed(this, Math.max(250L, intervalMs));
        }
    };

    @Override
    public void onCreate() {
        super.onCreate();
        locationManager = (LocationManager) getSystemService(Context.LOCATION_SERVICE);
        fusedClient = LocationServices.getFusedLocationProviderClient(this);
        handler = new Handler(Looper.getMainLooper());
        createNotificationChannel();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent == null) {
            stopSelf();
            return START_NOT_STICKY;
        }
        String action = intent.getAction();
        if (ACTION_STOP.equals(action)) {
            stopSpoofingInternal();
            stopSelf();
            return START_NOT_STICKY;
        }

        if (ACTION_START.equals(action)) {
            liveLat = intent.getDoubleExtra("lat", liveLat);
            liveLng = intent.getDoubleExtra("lng", liveLng);
            accuracyMeters = intent.getFloatExtra("accuracy", 4.0f);
            altitudeMeters = intent.getDoubleExtra("altitude", 110.0);
            jitterMeters = intent.getDoubleExtra("jitter", 1.5);
            intervalMs = intent.getLongExtra("intervalMs", 600L);
            liveSpeedKmh = 0f;
            isRouteRunning = false;

            if (!setupTestProviders()) {
                isRunning = false;
                stopSelf();
                return START_NOT_STICKY;
            }
            isRunning = true;
            startForegroundWithNotif();
            handler.removeCallbacks(tickRunnable);
            handler.post(tickRunnable);
        } else if (ACTION_UPDATE.equals(action)) {
            liveLat = intent.getDoubleExtra("lat", liveLat);
            liveLng = intent.getDoubleExtra("lng", liveLng);
            liveSpeedKmh = intent.getFloatExtra("speedKmh", 0f);
            liveBearing = intent.getFloatExtra("bearing", 0f);
            if (isRunning) {
                pushMockLocation();
                updateNotification();
            }
        } else if (ACTION_START_ROUTE.equals(action)) {
            String pointsJson = intent.getStringExtra("pointsJson");
            liveSpeedKmh = intent.getFloatExtra("speedKmh", 18f);
            routeLoop = intent.getBooleanExtra("loop", true);
            parseRoutePoints(pointsJson);
            if (routeWaypoints.size() >= 2) {
                routeSegIndex = 0;
                routeProgressMeters = 0.0;
                liveLat = routeWaypoints.get(0)[0];
                liveLng = routeWaypoints.get(0)[1];
                isRouteRunning = true;
            }
        } else if (ACTION_STOP_ROUTE.equals(action)) {
            isRouteRunning = false;
            liveSpeedKmh = 0f;
        }

        return START_STICKY;
    }

    private void parseRoutePoints(String json) {
        routeWaypoints.clear();
        if (json == null || json.isEmpty()) return;
        try {
            JSONArray arr = new JSONArray(json);
            for (int i = 0; i < arr.length(); i++) {
                JSONObject obj = arr.getJSONObject(i);
                routeWaypoints.add(new double[] { obj.getDouble("lat"), obj.getDouble("lng") });
            }
        } catch (Exception ignored) {
        }
    }

    private void advanceRoute(double deltaSec) {
        double speedMps = (liveSpeedKmh * 1000.0) / 3600.0;
        double step = speedMps * deltaSec;
        while (step > 0 && routeWaypoints.size() >= 2) {
            if (routeSegIndex >= routeWaypoints.size() - 1) {
                if (routeLoop) {
                    routeSegIndex = 0;
                    routeProgressMeters = 0.0;
                } else {
                    isRouteRunning = false;
                    liveSpeedKmh = 0f;
                    return;
                }
            }
            double[] a = routeWaypoints.get(routeSegIndex);
            double[] b = routeWaypoints.get(routeSegIndex + 1);
            float[] res = new float[2];
            Location.distanceBetween(a[0], a[1], b[0], b[1], res);
            double segLen = Math.max(0.5, res[0]);
            double rem = segLen - routeProgressMeters;
            if (step < rem) {
                routeProgressMeters += step;
                step = 0;
            } else {
                step -= rem;
                routeSegIndex += 1;
                routeProgressMeters = 0.0;
            }
        }
        int idxA = Math.min(routeSegIndex, routeWaypoints.size() - 2);
        int idxB = Math.min(routeSegIndex + 1, routeWaypoints.size() - 1);
        double[] a = routeWaypoints.get(idxA);
        double[] b = routeWaypoints.get(idxB);
        float[] res = new float[2];
        Location.distanceBetween(a[0], a[1], b[0], b[1], res);
        double segLen = Math.max(0.5, res[0]);
        double frac = Math.min(1.0, routeProgressMeters / segLen);
        liveLat = a[0] + (b[0] - a[0]) * frac;
        liveLng = a[1] + (b[1] - a[1]) * frac;
        liveBearing = (res[1] + 360f) % 360f;
    }

    @SuppressWarnings("deprecation")
    private boolean setupTestProviders() {
        lastError = "";
        String[] providers = new String[] {
            LocationManager.GPS_PROVIDER,
            LocationManager.NETWORK_PROVIDER
        };
        boolean anyOk = false;
        for (String provider : providers) {
            try {
                try {
                    locationManager.removeTestProvider(provider);
                } catch (Exception ignored) {
                }
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                    ProviderProperties props = new ProviderProperties.Builder()
                        .setHasAltitudeSupport(true)
                        .setHasSpeedSupport(true)
                        .setHasBearingSupport(true)
                        .setPowerUsage(ProviderProperties.POWER_USAGE_LOW)
                        .setAccuracy(ProviderProperties.ACCURACY_FINE)
                        .build();
                    locationManager.addTestProvider(provider, props);
                } else {
                    locationManager.addTestProvider(
                        provider,
                        false,
                        false,
                        false,
                        false,
                        true,
                        true,
                        true,
                        1,
                        1
                    );
                }
                locationManager.setTestProviderEnabled(provider, true);
                anyOk = true;
            } catch (SecurityException se) {
                lastError = "Выберите FakeGpsPro в «Параметры разработчика → Приложение для фиктивных местоположений»";
                return false;
            } catch (Exception ignored) {
            }
        }
        try {
            fusedClient.setMockMode(true);
        } catch (Exception ignored) {
        }
        return anyOk;
    }

    private void pushMockLocation() {
        double dLat = 0.0;
        double dLng = 0.0;
        if (jitterMeters > 0.05) {
            double r = jitterMeters * random.nextDouble();
            double ang = random.nextDouble() * 2.0 * Math.PI;
            dLat = (r * Math.cos(ang)) / 111320.0;
            dLng = (r * Math.sin(ang)) / (111320.0 * Math.max(0.2, Math.cos(Math.toRadians(liveLat))));
        }

        double lat = liveLat + dLat;
        double lng = liveLng + dLng;
        float speedMps = (liveSpeedKmh * 1000f) / 3600f;
        long now = System.currentTimeMillis();
        long elapsed = SystemClock.elapsedRealtimeNanos();

        String[] providers = new String[] {
            LocationManager.GPS_PROVIDER,
            LocationManager.NETWORK_PROVIDER
        };
        for (String p : providers) {
            try {
                Location loc = new Location(p);
                loc.setLatitude(lat);
                loc.setLongitude(lng);
                loc.setAltitude(altitudeMeters);
                loc.setAccuracy(accuracyMeters);
                loc.setSpeed(speedMps);
                loc.setBearing(liveBearing);
                loc.setTime(now);
                loc.setElapsedRealtimeNanos(elapsed);
                locationManager.setTestProviderLocation(p, loc);
            } catch (Exception ignored) {
            }
        }

        try {
            Location fusedLoc = new Location(LocationManager.GPS_PROVIDER);
            fusedLoc.setLatitude(lat);
            fusedLoc.setLongitude(lng);
            fusedLoc.setAltitude(altitudeMeters);
            fusedLoc.setAccuracy(accuracyMeters);
            fusedLoc.setSpeed(speedMps);
            fusedLoc.setBearing(liveBearing);
            fusedLoc.setTime(now);
            fusedLoc.setElapsedRealtimeNanos(elapsed);
            fusedClient.setMockLocation(fusedLoc);
        } catch (Exception ignored) {
        }
    }

    private void stopSpoofingInternal() {
        isRunning = false;
        isRouteRunning = false;
        handler.removeCallbacks(tickRunnable);
        String[] providers = new String[] {
            LocationManager.GPS_PROVIDER,
            LocationManager.NETWORK_PROVIDER
        };
        for (String p : providers) {
            try {
                locationManager.setTestProviderEnabled(p, false);
                locationManager.removeTestProvider(p);
            } catch (Exception ignored) {
            }
        }
        try {
            fusedClient.setMockMode(false);
        } catch (Exception ignored) {
        }
        stopForeground(true);
    }

    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel ch = new NotificationChannel(
                CHANNEL_ID,
                "FakeGpsPro GPS Spoofing",
                NotificationManager.IMPORTANCE_LOW
            );
            ch.setDescription("Постоянное уведомление активной подмены GPS");
            NotificationManager nm = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
            if (nm != null) {
                nm.createNotificationChannel(ch);
            }
        }
    }

    private Notification buildNotification() {
        Intent openIntent = new Intent(this, MainActivity.class);
        openIntent.setFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP);
        PendingIntent contentPi = PendingIntent.getActivity(
            this,
            0,
            openIntent,
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
        );

        Intent stopIntent = new Intent(this, MockLocationService.class);
        stopIntent.setAction(ACTION_STOP);
        PendingIntent stopPi = PendingIntent.getService(
            this,
            1,
            stopIntent,
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
        );

        String coords = String.format(Locale.US, "%.5f, %.5f", liveLat, liveLng);
        return new NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.mipmap.ic_launcher)
            .setContentTitle("FakeGpsPro активен")
            .setContentText("Локация: " + coords)
            .setOngoing(true)
            .setContentIntent(contentPi)
            .addAction(android.R.drawable.ic_media_pause, "Остановить", stopPi)
            .build();
    }

    private void startForegroundWithNotif() {
        Notification notif = buildNotification();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NOTIF_ID, notif, ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION);
        } else {
            startForeground(NOTIF_ID, notif);
        }
    }

    private void updateNotification() {
        NotificationManager nm = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm != null) {
            nm.notify(NOTIF_ID, buildNotification());
        }
    }

    @Override
    public void onDestroy() {
        stopSpoofingInternal();
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }
}
