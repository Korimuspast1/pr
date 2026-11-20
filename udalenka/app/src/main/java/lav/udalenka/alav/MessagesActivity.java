package lav.udalenka.alav;

import android.app.Activity;
import android.app.AlertDialog;
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
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import java.util.List;

/** История сохранённых сообщений одного диалога. */
public class MessagesActivity extends Activity {

    private String pkg;
    private String convo;
    private String query = "";
    private LinearLayout list;
    private ScrollView scroll;
    private EditText searchField;

    private final BroadcastReceiver refreshReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            render();
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_messages);
        pkg = getIntent().getStringExtra("pkg");
        convo = getIntent().getStringExtra("convo");
        list = findViewById(R.id.messages_list);
        scroll = findViewById(R.id.messages_scroll);
        searchField = findViewById(R.id.search_msgs);

        TextView title = findViewById(R.id.messages_title);
        title.setText(convo);
        findViewById(R.id.btn_back).setOnClickListener(v -> finish());
        findViewById(R.id.btn_clear).setOnClickListener(v -> confirmClear());
        searchField.addTextChangedListener(new TextWatcher() {
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

    private void confirmClear() {
        new AlertDialog.Builder(this)
                .setTitle(R.string.clear_convo_title)
                .setMessage(R.string.clear_convo_body)
                .setPositiveButton(R.string.clear, (d, w) -> {
                    Db.get(this).clearConvo(pkg, convo);
                    finish();
                })
                .setNegativeButton(R.string.cancel, null)
                .show();
    }

    private void render() {
        list.removeAllViews();
        List<Db.Msg> msgs = Db.get(this).listMessages(pkg, convo, query);
        if (msgs.isEmpty()) {
            TextView tv = new TextView(this);
            tv.setText(R.string.empty_messages);
            tv.setTextColor(getColor(R.color.text_secondary));
            tv.setGravity(Gravity.CENTER);
            tv.setPadding(Ui.dp(this, 32), Ui.dp(this, 80),
                    Ui.dp(this, 32), Ui.dp(this, 32));
            list.addView(tv);
            return;
        }
        String lastSender = null;
        for (Db.Msg m : msgs) {
            list.addView(bubble(m, !m.sender.equals(lastSender)));
            lastSender = m.sender;
        }
        scroll.post(() -> scroll.fullScroll(View.FOCUS_DOWN));
    }

    private View bubble(Db.Msg m, boolean showSender) {
        LinearLayout wrap = new LinearLayout(this);
        wrap.setOrientation(LinearLayout.VERTICAL);
        wrap.setPadding(Ui.dp(this, 12), Ui.dp(this, 3),
                Ui.dp(this, 12), Ui.dp(this, 3));

        LinearLayout bubble = new LinearLayout(this);
        bubble.setOrientation(LinearLayout.VERTICAL);
        bubble.setPadding(Ui.dp(this, 12), Ui.dp(this, 8),
                Ui.dp(this, 12), Ui.dp(this, 7));
        bubble.setBackgroundResource(m.deleted
                ? R.drawable.bg_bubble_deleted : R.drawable.bg_bubble);
        if (showSender) {
            TextView sender = new TextView(this);
            sender.setText(m.sender);
            sender.setTextColor(Ui.avatarColor(m.sender));
            sender.setTextSize(13);
            sender.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
            sender.setSingleLine(true);
            sender.setEllipsize(TextUtils.TruncateAt.END);
            bubble.addView(sender);
        }
        if (m.deleted) {
            TextView badge = new TextView(this);
            badge.setText(R.string.deleted_badge);
            badge.setTextColor(getColor(R.color.m3_error));
            badge.setTextSize(12);
            badge.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
            bubble.addView(badge);
        }
        TextView body = new TextView(this);
        body.setText(m.text);
        body.setTextColor(getColor(R.color.text_main));
        body.setTextSize(16);
        body.setTextIsSelectable(true);
        bubble.addView(body);

        TextView time = new TextView(this);
        time.setText(Ui.fullTime(m.ts));
        time.setTextColor(getColor(R.color.text_hint));
        time.setTextSize(11);
        time.setGravity(Gravity.END);
        bubble.addView(time);

        wrap.addView(bubble);
        return wrap;
    }
}
