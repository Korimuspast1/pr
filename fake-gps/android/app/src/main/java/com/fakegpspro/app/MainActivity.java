package com.fakegpspro.app;

import android.Manifest;
import android.app.AppOpsManager;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Process;
import android.provider.Settings;
import android.webkit.JavascriptInterface;
import android.webkit.WebSettings;
import android.webkit.WebView;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import com.getcapacitor.BridgeActivity;
import org.json.JSONObject;

public class MainActivity extends BridgeActivity {
    private static final int REQ_LOCATION_PERM = 5501;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        WebView webView = getBridge().getWebView();
        if (webView != null) {
            WebSettings settings = webView.getSettings();
            settings.setDomStorageEnabled(true);
            webView.addJavascriptInterface(new FakeGpsBridge(), "FakeGpsAndroid");
        }
    }

    private boolean hasLocationPerm() {
        return ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)
            == PackageManager.PERMISSION_GRANTED;
    }

    private boolean hasMockPerm() {
        try {
            AppOpsManager ops = (AppOpsManager) getSystemService(Context.APP_OPS_SERVICE);
            if (ops == null) return false;
            int mode = ops.checkOpNoThrow(
                AppOpsManager.OPSTR_MOCK_LOCATION,
                Process.myUid(),
                getPackageName()
            );
            return mode == AppOpsManager.MODE_ALLOWED;
        } catch (Exception e) {
            return false;
        }
    }

    private void requestPermissionsIfNeeded() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            ActivityCompat.requestPermissions(
                this,
                new String[] {
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION,
                    Manifest.permission.POST_NOTIFICATIONS
                },
                REQ_LOCATION_PERM
            );
        } else {
            ActivityCompat.requestPermissions(
                this,
                new String[] {
                    Manifest.permission.ACCESS_FINE_LOCATION,
                    Manifest.permission.ACCESS_COARSE_LOCATION
                },
                REQ_LOCATION_PERM
            );
        }
    }

    private class FakeGpsBridge {
        @JavascriptInterface
        public boolean startSpoofing(
            double lat,
            double lng,
            float accuracyMeters,
            double altitudeMeters,
            double jitterMeters,
            long intervalMs
        ) {
            if (!hasLocationPerm()) {
                requestPermissionsIfNeeded();
                MockLocationService.lastError = "Сначала разрешите доступ к геолокации.";
                return false;
            }
            if (!hasMockPerm()) {
                MockLocationService.lastError =
                    "Выберите FakeGpsPro в «Параметры разработчика → Приложение для фиктивных местоположений».";
                return false;
            }

            Intent i = new Intent(MainActivity.this, MockLocationService.class);
            i.setAction(MockLocationService.ACTION_START);
            i.putExtra("lat", lat);
            i.putExtra("lng", lng);
            i.putExtra("accuracy", accuracyMeters);
            i.putExtra("altitude", altitudeMeters);
            i.putExtra("jitter", jitterMeters);
            i.putExtra("intervalMs", intervalMs);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                startForegroundService(i);
            } else {
                startService(i);
            }
            return true;
        }

        @JavascriptInterface
        public void stopSpoofing() {
            Intent i = new Intent(MainActivity.this, MockLocationService.class);
            i.setAction(MockLocationService.ACTION_STOP);
            startService(i);
        }

        @JavascriptInterface
        public void updateLocation(double lat, double lng, float speedKmh, float bearing) {
            if (!MockLocationService.isRunning) return;
            Intent i = new Intent(MainActivity.this, MockLocationService.class);
            i.setAction(MockLocationService.ACTION_UPDATE);
            i.putExtra("lat", lat);
            i.putExtra("lng", lng);
            i.putExtra("speedKmh", speedKmh);
            i.putExtra("bearing", bearing);
            startService(i);
        }

        @JavascriptInterface
        public boolean startRoute(String pointsJson, float speedKmh, boolean loop) {
            Intent i = new Intent(MainActivity.this, MockLocationService.class);
            i.setAction(MockLocationService.ACTION_START_ROUTE);
            i.putExtra("pointsJson", pointsJson);
            i.putExtra("speedKmh", speedKmh);
            i.putExtra("loop", loop);
            startService(i);
            return true;
        }

        @JavascriptInterface
        public void stopRoute() {
            Intent i = new Intent(MainActivity.this, MockLocationService.class);
            i.setAction(MockLocationService.ACTION_STOP_ROUTE);
            startService(i);
        }

        @JavascriptInterface
        public String getStatusJson() {
            try {
                JSONObject obj = new JSONObject();
                obj.put("isAndroid", true);
                obj.put("hasLocationPermission", hasLocationPerm());
                obj.put("hasMockPermission", hasMockPerm());
                obj.put("isSpoofing", MockLocationService.isRunning);
                obj.put("isRoutePlaying", MockLocationService.isRouteRunning);
                obj.put("currentLat", MockLocationService.liveLat);
                obj.put("currentLng", MockLocationService.liveLng);
                obj.put("bearing", MockLocationService.liveBearing);
                obj.put("errorMessage", MockLocationService.lastError);
                return obj.toString();
            } catch (Exception e) {
                return "{}";
            }
        }

        @JavascriptInterface
        public void requestLocationPermission() {
            requestPermissionsIfNeeded();
        }

        @JavascriptInterface
        public void openDeveloperSettings() {
            runOnUiThread(() -> {
                try {
                    Intent intent = new Intent(Settings.ACTION_APPLICATION_DEVELOPMENT_SETTINGS);
                    intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    startActivity(intent);
                } catch (Exception e) {
                    try {
                        Intent fallback = new Intent(Settings.ACTION_SETTINGS);
                        fallback.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                        startActivity(fallback);
                    } catch (Exception ignored) {
                    }
                }
            });
        }

        @JavascriptInterface
        public void openExternalUrl(String url) {
            runOnUiThread(() -> {
                try {
                    Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
                    intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    startActivity(intent);
                } catch (Exception ignored) {
                }
            });
        }
    }
}
