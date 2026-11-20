package lav.udalenka.alav;

import android.app.Activity;
import android.app.AlertDialog;
import android.text.TextUtils;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.Switch;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

/** Настройки: доступ, хранение, какие приложения ловить, очистка. */
public class SettingsActivity extends Activity {

    private LinearLayout appsBox;
    private TextView accessValue;
    private TextView retentionValue;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_settings);
        appsBox = findViewById(R.id.settings_apps);
        accessValue = findViewById(R.id.setting_access_value);
        retentionValue = findViewById(R.id.setting_retention_value);

        findViewById(R.id.btn_back).setOnClickListener(v -> finish());
        findViewById(R.id.setting_access).setOnClickListener(
                v -> Ui.openListenerSettings(this));
        findViewById(R.id.setting_retention).setOnClickListener(
                v -> pickRetention());
        findViewById(R.id.setting_clear_all).setOnClickListener(
                v -> confirmClear());
    }

    @Override
    protected void onResume() {
        super.onResume();
        renderStatus();
        renderApps();
    }

    private void renderStatus() {
        boolean enabled = Ui.listenerEnabled(this);
        accessValue.setText(enabled
                ? R.string.access_granted : R.string.access_needed);
        accessValue.setTextColor(getColor(enabled
                ? R.color.m3_tertiary : R.color.m3_error));
        int days = Db.get(this).retentionDays();
        retentionValue.setText(days <= 0
                ? getString(R.string.retention_forever)
                : getResources().getQuantityString(
                        R.plurals.retention_days, days, days));
    }

    private void pickRetention() {
        String[] labels = {
                getResources().getQuantityString(R.plurals.retention_days, 3, 3),
                getResources().getQuantityString(R.plurals.retention_days, 7, 7),
                getResources().getQuantityString(R.plurals.retention_days, 30, 30),
                getString(R.string.retention_forever)};
        int[] values = {3, 7, 30, 0};
        new AlertDialog.Builder(this)
                .setTitle(R.string.retention_title)
                .setItems(labels, (d, which) -> {
                    Db.get(this).setRetentionDays(values[which]);
                    Db.get(this).purgeOld();
                    renderStatus();
                })
                .show();
    }

    private void confirmClear() {
        new AlertDialog.Builder(this)
                .setTitle(R.string.clear_all_title)
                .setMessage(R.string.clear_all_body)
                .setPositiveButton(R.string.clear, (d, w) -> {
                    Db.get(this).clearAll();
                })
                .setNegativeButton(R.string.cancel, null)
                .show();
    }

    /** Все установленные приложения с лаунчером + уже пойманные пакеты. */
    private void renderApps() {
        appsBox.removeAllViews();
        Set<String> pkgs = new LinkedHashSet<>();
        for (Db.AppRow r : Db.get(this).listApps()) {
            pkgs.add(r.pkg);
        }
        PackageManager pm = getPackageManager();
        Intent launcher = new Intent(Intent.ACTION_MAIN);
        launcher.addCategory(Intent.CATEGORY_LAUNCHER);
        List<ResolveInfo> infos = pm.queryIntentActivities(launcher, 0);
        List<String> installed = new ArrayList<>();
        for (ResolveInfo info : infos) {
            String pkg = info.activityInfo.packageName;
            if (!pkg.equals(getPackageName())) {
                installed.add(pkg);
            }
        }
        Collections.sort(installed, (a, b) -> Ui.appLabel(this, a)
                .compareToIgnoreCase(Ui.appLabel(this, b)));
        // Сначала те, что уже что-то поймали.
        List<String> ordered = new ArrayList<>(new LinkedHashSet<>(pkgs));
        for (String p : installed) {
            if (!ordered.contains(p)) {
                ordered.add(p);
            }
        }
        for (String pkg : ordered) {
            appsBox.addView(appToggle(pkg));
        }
    }

    private View appToggle(String pkg) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(Ui.dp(this, 16), Ui.dp(this, 6),
                Ui.dp(this, 16), Ui.dp(this, 6));
        row.setBackgroundResource(R.drawable.bg_row);

        ImageView icon = new ImageView(this);
        icon.setImageDrawable(Ui.appIcon(this, pkg));
        row.addView(icon, new LinearLayout.LayoutParams(
                Ui.dp(this, 36), Ui.dp(this, 36)));

        TextView name = new TextView(this);
        name.setText(Ui.appLabel(this, pkg));
        name.setTextColor(getColor(R.color.text_main));
        name.setTextSize(15);
        name.setSingleLine(true);
        name.setEllipsize(TextUtils.TruncateAt.END);
        LinearLayout.LayoutParams np = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
        np.leftMargin = Ui.dp(this, 12);
        row.addView(name, np);

        Switch sw = new Switch(this);
        sw.setChecked(Db.get(this).appEnabled(pkg));
        sw.setOnCheckedChangeListener((b, checked) ->
                Db.get(this).setAppEnabled(pkg, checked));
        row.addView(sw);
        return row;
    }
}
