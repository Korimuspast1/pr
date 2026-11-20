package lav.udalenka.alav;

import android.app.Activity;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.graphics.PorterDuff;
import android.graphics.Typeface;
import android.os.Build;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;

import java.util.List;

/** Диалоги одного приложения (как список чатов в Telegram). */
public class ConvosActivity extends Activity {

    private String pkg;
    private LinearLayout list;

    private final BroadcastReceiver refreshReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            render();
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_convos);
        pkg = getIntent().getStringExtra("pkg");
        list = findViewById(R.id.convos_list);

        TextView title = findViewById(R.id.convos_title);
        title.setText(Ui.appLabel(this, pkg));
        ImageView icon = findViewById(R.id.convos_icon);
        icon.setImageDrawable(Ui.appIcon(this, pkg));
        findViewById(R.id.btn_back).setOnClickListener(v -> finish());
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
        list.removeAllViews();
        List<Db.ConvoRow> convos = Db.get(this).listConvos(pkg);
        if (convos.isEmpty()) {
            TextView tv = new TextView(this);
            tv.setText(R.string.empty_convos);
            tv.setTextColor(getColor(R.color.text_secondary));
            tv.setGravity(Gravity.CENTER);
            tv.setPadding(Ui.dp(this, 32), Ui.dp(this, 80),
                    Ui.dp(this, 32), Ui.dp(this, 32));
            list.addView(tv);
            return;
        }
        for (Db.ConvoRow row : convos) {
            list.addView(convoRow(row));
        }
    }

    private View convoRow(Db.ConvoRow row) {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.HORIZONTAL);
        root.setGravity(Gravity.CENTER_VERTICAL);
        root.setPadding(Ui.dp(this, 16), Ui.dp(this, 10),
                Ui.dp(this, 16), Ui.dp(this, 10));
        root.setBackgroundResource(R.drawable.bg_row);
        root.setClickable(true);
        root.setFocusable(true);

        // Аватар-инициал, цвет зависит от названия диалога.
        FrameLayout avatar = new FrameLayout(this);
        avatar.setBackgroundResource(R.drawable.bg_avatar_circle);
        avatar.getBackground().setColorFilter(
                Ui.avatarColor(row.convo), PorterDuff.Mode.SRC_IN);
        TextView initial = new TextView(this);
        String label = row.convo.length() > 0 ? row.convo : "?";
        initial.setText(String.valueOf(Character.toUpperCase(label.charAt(0))));
        initial.setTextColor(getColor(R.color.white));
        initial.setTextSize(19);
        initial.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        initial.setGravity(Gravity.CENTER);
        avatar.addView(initial, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT));
        root.addView(avatar, new LinearLayout.LayoutParams(
                Ui.dp(this, 50), Ui.dp(this, 50)));

        LinearLayout texts = new LinearLayout(this);
        texts.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams tp = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
        tp.leftMargin = Ui.dp(this, 12);

        TextView name = new TextView(this);
        name.setText(row.convo);
        name.setTextColor(getColor(R.color.text_main));
        name.setTextSize(16);
        name.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        name.setSingleLine(true);
        name.setEllipsize(TextUtils.TruncateAt.END);
        texts.addView(name);

        TextView preview = new TextView(this);
        preview.setText(row.lastDeleted
                ? "🗑 " + row.lastText : row.lastText);
        preview.setTextColor(getColor(R.color.text_secondary));
        preview.setTextSize(14);
        preview.setSingleLine(true);
        preview.setEllipsize(TextUtils.TruncateAt.END);
        texts.addView(preview);
        root.addView(texts, tp);

        LinearLayout side = new LinearLayout(this);
        side.setOrientation(LinearLayout.VERTICAL);
        side.setGravity(Gravity.END);
        TextView time = new TextView(this);
        time.setText(Ui.relTime(row.lastTs));
        time.setTextColor(getColor(R.color.text_secondary));
        time.setTextSize(12);
        side.addView(time);
        TextView count = new TextView(this);
        count.setText(String.valueOf(row.total));
        count.setTextColor(getColor(R.color.white));
        count.setTextSize(11);
        count.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        count.setGravity(Gravity.CENTER);
        count.setBackgroundResource(R.drawable.bg_unread_badge);
        LinearLayout.LayoutParams cp = new LinearLayout.LayoutParams(
                Ui.dp(this, 20), Ui.dp(this, 20));
        cp.topMargin = Ui.dp(this, 6);
        side.addView(count, cp);
        root.addView(side);

        root.setOnClickListener(v -> {
            Intent i = new Intent(this, MessagesActivity.class);
            i.putExtra("pkg", pkg);
            i.putExtra("convo", row.convo);
            startActivity(i);
        });
        return root;
    }
}
