package lav.visaroom.alav;

import android.app.Activity;
import android.app.AlertDialog;
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
        PressFx.install(this);

        messagesScroll = findViewById(R.id.messages_scroll);
        messagesContainer = (LinearLayout) messagesScroll.getChildAt(0);
        searchBar = findViewById(R.id.message_search_bar);
        searchDivider = findViewById(R.id.search_divider);
        searchInput = findViewById(R.id.message_search);
        searchCount = findViewById(R.id.search_count);

        applyBackground();
        renderMessages();

        findViewById(R.id.back).setOnClickListener(v -> finishWithAnimation());
        findViewById(R.id.chat_header).setOnClickListener(v -> showOfficialProfile());
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

    @Override
    public void onBackPressed() {
        finishWithAnimation();
    }

    private void finishWithAnimation() {
        finish();
        overridePendingTransition(R.anim.slide_in_left, R.anim.slide_out_right);
    }

    private void showOfficialProfile() {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setGravity(Gravity.CENTER_HORIZONTAL);
        box.setPadding(dp(24), dp(8), dp(24), dp(8));

        ImageView photo = new ImageView(this);
        photo.setImageResource(R.drawable.ic_logo);
        box.addView(photo, new LinearLayout.LayoutParams(dp(80), dp(80)));

        TextView name = new TextView(this);
        name.setText("visaroom");
        name.setTextColor(getColor(R.color.text_main));
        name.setTextSize(19);
        name.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        name.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams nameParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        nameParams.topMargin = dp(12);
        box.addView(name, nameParams);

        TextView sub = new TextView(this);
        sub.setText("Verified official channel");
        sub.setTextColor(getColor(R.color.text_secondary));
        sub.setTextSize(13);
        sub.setGravity(Gravity.CENTER);
        box.addView(sub);

        TextView bio = new TextView(this);
        bio.setText("Official announcements from the visaroom team.");
        bio.setTextColor(getColor(R.color.text_secondary));
        bio.setTextSize(14);
        bio.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams bioParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        bioParams.topMargin = dp(12);
        box.addView(bio, bioParams);

        new AlertDialog.Builder(this)
                .setView(box)
                .setPositiveButton("Close", null)
                .show();
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
        PressFx.applyTree(messagesContainer);
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

        LinearLayout senderRow = new LinearLayout(this);
        senderRow.setOrientation(LinearLayout.HORIZONTAL);
        senderRow.setGravity(Gravity.CENTER_VERTICAL);
        senderRow.setClickable(true);
        senderRow.setFocusable(true);
        senderRow.setOnClickListener(v -> showOfficialProfile());

        ImageView senderAvatar = new ImageView(this);
        senderAvatar.setImageResource(R.drawable.ic_logo);
        senderRow.addView(senderAvatar, new LinearLayout.LayoutParams(dp(28), dp(28)));

        TextView sender = new TextView(this);
        sender.setText("visaroom");
        sender.setTextColor(isDark() ? Color.WHITE : getColor(R.color.text_main));
        sender.setTextSize(13);
        sender.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        LinearLayout.LayoutParams senderParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        senderParams.leftMargin = dp(8);
        senderRow.addView(sender, senderParams);
        card.addView(senderRow);

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
