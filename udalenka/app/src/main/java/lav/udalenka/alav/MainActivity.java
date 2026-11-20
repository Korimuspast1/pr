package lav.udalenka.alav;

import android.app.Activity;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.graphics.Typeface;
import android.os.Build;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import java.util.List;

/** Главный экран: сохранённые уведомления, сгруппированные по приложениям. */
public class MainActivity extends Activity {

    private LinearLayout list;
    private TextView banner;
    private EditText search;
    private String query = "";

    private final BroadcastReceiver refreshReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            render();
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        banner = findViewById(R.id.access_banner);
        list = findViewById(R.id.apps_list);
        search = findViewById(R.id.search_field);

        banner.setOnClickListener(v -> Ui.openListenerSettings(this));
        findViewById(R.id.btn_settings).setOnClickListener(
                v -> startActivity(new Intent(this, SettingsActivity.class)));
        search.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int a, int b, int c) { }
            @Override public void onTextChanged(CharSequence s, int a, int b, int c) {
                query = s == null ? "" : s.toString().trim();
                render();
            }
            @Override public void afterTextChanged(Editable s) { }
        });
    }

    @Override
    protected void onResume() {
        super.onResume();
        IntentFilter filter = new IntentFilter(NotifSaverService.ACTION_UPDATED);
        if (Build.VERSION.SDK_INT >= 33) {
            registerReceiver(refreshReceiver, filter, Context.RECEIVER_NOT_EXPORTED);
        } else {
            registerReceiver(refreshReceiver, filter);
        }
        render();
    }

    @Override
    protected void onPause() {
        super.onPause();
        try {
            unregisterReceiver(refreshReceiver);
        } catch (RuntimeException ignored) {
        }
    }

    private void render() {
        banner.setVisibility(Ui.listenerEnabled(this) ? View.GONE : View.VISIBLE);
        list.removeAllViews();
        if (query.length() > 0) {
            renderSearch();
            return;
        }
        List<Db.AppRow> apps = Db.get(this).listApps();
        if (apps.isEmpty()) {
            list.addView(emptyState());
            return;
        }
        for (Db.AppRow row : apps) {
            list.addView(appRow(row));
        }
    }

    private View emptyState() {
        TextView tv = new TextView(this);
        tv.setText(R.string.empty_apps);
        tv.setTextColor(getColor(R.color.text_secondary));
        tv.setTextSize(15);
        tv.setGravity(Gravity.CENTER);
        tv.setPadding(Ui.dp(this, 32), Ui.dp(this, 80),
                Ui.dp(this, 32), Ui.dp(this, 32));
        return tv;
    }

    private View appRow(Db.AppRow row) {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.HORIZONTAL);
        root.setGravity(Gravity.CENTER_VERTICAL);
        root.setPadding(Ui.dp(this, 16), Ui.dp(this, 10),
                Ui.dp(this, 16), Ui.dp(this, 10));
        root.setBackgroundResource(R.drawable.bg_row);
        root.setClickable(true);
        root.setFocusable(true);

        ImageView icon = new ImageView(this);
        icon.setImageDrawable(Ui.appIcon(this, row.pkg));
        root.addView(icon, new LinearLayout.LayoutParams(
                Ui.dp(this, 46), Ui.dp(this, 46)));

        LinearLayout texts = new LinearLayout(this);
        texts.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams tp = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
        tp.leftMargin = Ui.dp(this, 12);

        TextView name = new TextView(this);
        name.setText(Ui.appLabel(this, row.pkg));
        name.setTextColor(getColor(R.color.text_main));
        name.setTextSize(16);
        name.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        name.setSingleLine(true);
        name.setEllipsize(TextUtils.TruncateAt.END);
        texts.addView(name);

        TextView stats = new TextView(this);
        String s = getResources().getQuantityString(
                R.plurals.saved_count, row.total, row.total)
                + " · " + getResources().getQuantityString(
                R.plurals.chats_count, row.convos, row.convos);
        if (row.deletedCount > 0) {
            s += " · 🗑 " + row.deletedCount;
        }
        stats.setText(s);
        stats.setTextColor(getColor(R.color.text_secondary));
        stats.setTextSize(13);
        stats.setSingleLine(true);
        texts.addView(stats);
        root.addView(texts, tp);

        TextView time = new TextView(this);
        time.setText(Ui.relTime(row.last));
        time.setTextColor(getColor(R.color.text_secondary));
        time.setTextSize(12);
        root.addView(time);

        root.setOnClickListener(v -> {
            Intent i = new Intent(this, ConvosActivity.class);
            i.putExtra("pkg", row.pkg);
            startActivity(i);
        });
        return root;
    }

    /** Глобальный поиск по всем сохранённым текстам. */
    private void renderSearch() {
        List<Db.Msg> found = Db.get(this).globalSearch(query);
        if (found.isEmpty()) {
            list.addView(emptyState());
            return;
        }
        for (Db.Msg m : found) {
            LinearLayout rowRoot = new LinearLayout(this);
            rowRoot.setOrientation(LinearLayout.VERTICAL);
            rowRoot.setPadding(Ui.dp(this, 16), Ui.dp(this, 10),
                    Ui.dp(this, 16), Ui.dp(this, 10));
            rowRoot.setBackgroundResource(R.drawable.bg_row);
            rowRoot.setClickable(true);
            rowRoot.setFocusable(true);

            TextView head = new TextView(this);
            head.setText(Ui.appLabel(this, m.convoPkg) + " · " + m.convoName
                    + " · " + Ui.relTime(m.ts));
            head.setTextColor(getColor(R.color.m3_primary));
            head.setTextSize(13);
            head.setSingleLine(true);
            head.setEllipsize(TextUtils.TruncateAt.END);
            rowRoot.addView(head);

            TextView body = new TextView(this);
            body.setText((m.deleted ? "🗑 " : "") + m.text);
            body.setTextColor(getColor(R.color.text_main));
            body.setTextSize(15);
            body.setMaxLines(3);
            body.setEllipsize(TextUtils.TruncateAt.END);
            rowRoot.addView(body);

            rowRoot.setOnClickListener(v -> {
                Intent i = new Intent(this, MessagesActivity.class);
                i.putExtra("pkg", m.convoPkg);
                i.putExtra("convo", m.convoName);
                startActivity(i);
            });
            list.addView(rowRoot);
        }
    }
}
