package lav.visaroom.alav;

import android.app.Activity;
import android.graphics.Color;
import android.graphics.Typeface;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.inputmethod.InputMethodManager;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class ChatActivity extends Activity {

    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final List<ProfileClient.Announcement> messages = new ArrayList<>();

    private LinearLayout messagesContainer;
    private ScrollView messagesScroll;
    private View searchBar;
    private View searchDivider;
    private EditText searchInput;
    private TextView searchCount;
    private boolean loading = true;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        AuthClient.init(this);
        setContentView(R.layout.activity_chat);

        messagesContainer = findViewById(R.id/messages_container);
        messagesScroll = findViewById(R.id.messages_scroll);
        searchBar = findViewById(R.id.message_search_bar);
        searchDivider = findViewById(R.id.search_divider);
        searchInput = findViewById(R.id.message_search);
        searchCount = findViewById(R.id.search_count);

        applyBackground();
        renderMessages();

        findViewById(R.id.back).setOnClickListener(v -> finish());
        findViewById(R.id.search_toggle).setOnClickListener(v -> setSearchOpen(true));
        findViewById(R.id.search_close).setOnClickListener(v -> {
            searchInput.setText("");
            setSearchOpen(false);
        });
        searchInput.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {
            }

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                renderMessages();
            }

            @Override
            public void afterTextChanged(Editable s) {
            }
        });

        loadAnnouncements();
    }

    @Override
    protected void onDestroy() {
        executor.shutdownNow();
        super.onDestroy();
    }

    private void setSearchOpen(boolean open) {
        searchBar.setVisibility(open ? View.VISIBLE : View.GONE);
        searchDivider.setVisibility(open ? View.VISIBLE : View.GONE);
        if (open) {
            searchInput.requestFocus();
            InputMethodManager imm = (InputMethodManager) getSystemService(INPUT_METHOD_SERVICE);
            if (imm != null) {
                imm.showSoftInput(searchInput, InputMethodManager.SHOW_IMPLICIT);
            }
            renderMessages();
        } else {
            searchInput.clearFocus();
        }
    }

    private boolean isDark() {
        return AppSettings.chatBackground(this) == AppSettings.BG_DARK;
    }

    private void applyBackground() {
        int bg = AppSettings.chatBackground(this);
        int color = bg == AppSettings.BG_LIGHT ? Color.rgb(245, 245, 245)
                : bg == AppSettings.BG_DARK ? Color.rgb(17, 17, 17)
                : Color.WHITE;
        messagesScroll.setBackgroundColor(color);
        messagesContainer.setBackgroundColor(color);
    }

    private void loadAnnouncements() {
        executor.execute(() -> {
            String refreshToken = TokenStore.refreshToken(this);
            List<ProfileClient.Announcement> loaded = new ArrayList<>();
            if (refreshToken != null && refreshToken.length() > 0) {
                AuthClient.Result session = AuthClient.refresh(refreshToken);
                if (session.ok) {
                    TokenStore.save(this, null, session.refreshToken);
                    TokenStore.saveUid(this, session.uid);
                    loaded = ProfileClient.listAnnouncements(session.idToken);
                }
            }
            List<ProfileClient.Announcement> result = loaded;
            runOnUiThread(() -> {
                messages.clear();
                messages.addAll(result);
                loading = false;
                renderMessages();
            });
        });
    }

    private void renderMessages() {
        String query = searchInput == null ? ""
                : searchInput.getText().toString().trim().toLowerCase(Locale.ROOT);
        messagesContainer.removeAllViews();

        List<ProfileClient.Announcement> visible = new ArrayList<>();
        for (ProfileClient.Announcement m : messages) {
            if (query.length() == 0 || m.text.toLowerCase(Locale.ROOT).contains(query)) {
                visible.add(m);
            }
        }

        if (searchBar.getVisibility() == View.VISIBLE) {
            searchCount.setText(visible.size() == 1
                    ? "1 message" : visible.size() + " messages");
        }

        if (visible.isEmpty()) {
            renderEmpty(query);
            return;
        }
        for (ProfileClient.Announcement m : visible) {
            messagesContainer.addView(makeMessageCard(m));
        }
    }

    private void renderEmpty(String query) {
        LinearLayout empty = new LinearLayout(this);
        empty.setOrientation(LinearLayout.VERTICAL);
        empty.setGravity(Gravity.CENTER_HORIZONTAL);
        empty.setPadding(dp(8), dp(72), dp(8), dp(24));
        empty.setLayoutParams(new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

        ImageView icon = new ImageView(this);
        icon.setImageResource(R.drawable.ic_bubble);
        empty.addView(icon, new LinearLayout.LayoutParams(dp(44), dp(44)));

        TextView title = new TextView(this);
        title.setText(query.length() > 0 ? "No messages found"
                : loading ? "Loading..." : "No messages yet");
        title.setTextColor(isDark() ? Color.WHITE : getColor(R.color.text_main));
        title.setTextSize(16);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams titleParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        titleParams.topMargin = dp(14);
        empty.addView(title, titleParams);

        TextView text = new TextView(this);
        text.setText(query.length() > 0
                ? "Try a different search."
                : "Official announcements will appear here.");
        text.setTextColor(getColor(R.color.text_secondary));
        text.setTextSize(14);
        text.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams textParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        textParams.topMargin = dp(6);
        empty.addView(text, textParams);

        messagesContainer.addView(empty);
    }

    private View makeMessageCard(ProfileClient.Announcement message) {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(14), dp(12), dp(14), dp(12));
        if (isDark()) {
            card.setBackgroundColor(Color.rgb(34, 34, 34));
        } else {
            card.setBackgroundResource(R.drawable.bg_message);
        }
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        params.bottomMargin = dp(10);
        card.setLayoutParams(params);

        TextView sender = new TextView(this);
        sender.setText("visaroom");
        sender.setTextColor(isDark() ? Color.WHITE : getColor(R.color.text_main));
        sender.setTextSize(13);
        sender.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        card.addView(sender);

        TextView text = new TextView(this);
        text.setText(message.text);
        text.setTextColor(isDark() ? Color.WHITE : getColor(R.color.text_main));
        text.setTextSize(AppSettings.textSizeSp(this));
        LinearLayout.LayoutParams textParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        textParams.topMargin = dp(6);
        card.addView(text, textParams);

        if (AppSettings.showTimestamps(this) && message.time.length() > 0) {
            TextView time = new TextView(this);
            time.setText(message.time);
            time.setTextColor(getColor(R.color.text_secondary));
            time.setTextSize(11);
            time.setGravity(Gravity.END);
            LinearLayout.LayoutParams timeParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT);
            timeParams.topMargin = dp(8);
            card.addView(time, timeParams);
        }
        return card;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
