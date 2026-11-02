package lav.visaroom.alav;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Path;
import android.graphics.PorterDuff;
import android.graphics.Typeface;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.Editable;
import android.text.InputType;
import android.text.SpannableStringBuilder;
import android.text.Spanned;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.text.style.ForegroundColorSpan;
import android.text.style.RelativeSizeSpan;
import android.text.style.StyleSpan;
import android.view.Gravity;
import android.view.animation.DecelerateInterpolator;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.Switch;
import android.widget.TextView;
import android.widget.Toast;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.regex.Pattern;

public class ChatsActivity extends Activity {

    private static final int PICK_IMAGE = 701;
    private static final Pattern USERNAME = Pattern.compile("[A-Za-z0-9_]{3,24}");

    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final Handler handler = new Handler(Looper.getMainLooper());

    private View[] pages;
    private View[] navIndicators;
    private ImageView[] navIcons;
    private TextView[] navLabels;

    private EditText searchInput;
    private View officialChat;
    private TextView officialName;
    private TextView officialPreview;
    private View clearSearch;
    private TextView noChats;
    private TextView searchHint;
    private TextView peopleLabel;
    private LinearLayout globalResults;
    private TextView directLabel;
    private LinearLayout directChats;
    private TextView directEmpty;
    private final List<VrProto.ChatSummary> directChatList = new ArrayList<>();
    private boolean directChatsLoading;
    private boolean directChatsLoaded;
    private Runnable directChatsPoll;

    private ImageView profileAvatar;
    private EditText usernameInput;
    private EditText bioInput;
    private Switch discoverableSwitch;
    private TextView emailView;
    private TextView idView;
    private TextView profileStatus;
    private ProgressBar profileProgress;
    private Button saveButton;

    private TextView textSizeValue;
    private TextView backgroundValue;
    private TextView twoFaValue;
    private TextView accountsValue;
    private TextView[] settingsFolderHeaders;
    private View[] settingsFolderContents;
    private String[] settingsFolderTitles;
    private String[] settingsFolderDescriptions;
    private Switch showBioSwitch;
    private Switch showAvatarSwitch;
    private Switch showIdSwitch;
    private View moderationSection;
    private int currentPage;
    private long publicId;
    private boolean twoFaEnabledState;
    private String twoFaVerifierState = "";
    private boolean showBioState = true;
    private boolean showAvatarState = true;
    private boolean showIdState = true;
    private boolean verifiedState;
    private boolean applyingSettings;

    private String userId = "";
    private String userEmail = "";
    private String originalUsernameLower = "";
    private String latestIdToken;
    private long latestIdTokenAt;
    private boolean hasAvatar;
    private long avatarUpdatedAt;
    private boolean avatarChanged;
    private String pendingGlobalQuery = "";
    private Runnable globalSearchRunnable;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        AuthClient.init(this);
        setContentView(R.layout.activity_chats);

        bindViews();
        setupNavigation();
        setupChats();
        setupProfile();
        setupSettings();

        String email = TokenStore.email(this);
        userEmail = email == null ? "" : email;
        String uid = TokenStore.uid(this);
        userId = uid == null ? "" : uid;

        emailView.setText(userEmail);
        updateAdminUi();
        idView.setText(userId.length() == 0 ? "Loading..." : userId);

        loadLocalProfile();
        displayAvatar();
        applyChatTextSize();
        loadProfileFromCloud();
        loadDirectChats();
    }

    @Override
    protected void onResume() {
        super.onResume();
        updateAdminUi();
        updateAccountsUi();
        loadDirectChats();
        startDirectChatsPolling();
    }

    @Override
    protected void onPause() {
        stopDirectChatsPolling();
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        handler.removeCallbacksAndMessages(null);
        executor.shutdownNow();
        super.onDestroy();
    }

    // ---------- setup ----------

    private void bindViews() {
        pages = new View[]{
                findViewById(R.id.page_chats),
                findViewById(R.id.page_profile),
                findViewById(R.id.page_settings)};
        navIndicators = new View[]{
                findViewById(R.id.nav_chats_indicator),
                findViewById(R.id.nav_profile_indicator),
                findViewById(R.id.nav_settings_indicator)};
        navIcons = new ImageView[]{
                findViewById(R.id.nav_chats_icon),
                findViewById(R.id.nav_profile_icon),
                findViewById(R.id.nav_settings_icon)};
        navLabels = new TextView[]{
                findViewById(R.id.nav_chats_label),
                findViewById(R.id.nav_profile_label),
                findViewById(R.id.nav_settings_label)};

        searchInput = findViewById(R.id.search);
        officialChat = findViewById(R.id.official_chat);
        officialName = findViewById(R.id.official_name);
        officialPreview = findViewById(R.id.official_preview);
        clearSearch = findViewById(R.id.clear_search);
        noChats = findViewById(R.id.no_chats);
        searchHint = findViewById(R.id.search_hint);
        peopleLabel = findViewById(R.id.people_label);
        globalResults = findViewById(R.id.global_results);
        directLabel = findViewById(R.id.direct_label);
        directChats = findViewById(R.id.direct_chats);
        directEmpty = findViewById(R.id.direct_empty);

        profileAvatar = findViewById(R.id.profile_avatar);
        usernameInput = findViewById(R.id.profile_username);
        bioInput = findViewById(R.id.profile_bio);
        discoverableSwitch = findViewById(R.id.profile_discoverable);
        emailView = findViewById(R.id.profile_email);
        idView = findViewById(R.id.profile_id);
        profileStatus = findViewById(R.id.profile_status);
        profileProgress = findViewById(R.id.profile_progress);
        saveButton = findViewById(R.id.save_profile);

        textSizeValue = findViewById(R.id.setting_text_size_value);
        backgroundValue = findViewById(R.id.setting_background_value);
        twoFaValue = findViewById(R.id.setting_2fa_value);
        accountsValue = findViewById(R.id.setting_accounts_value);
        moderationSection = findViewById(R.id.moderation_section);
    }

    private void setupNavigation() {
        findViewById(R.id.nav_chats).setOnClickListener(v -> showPage(0));
        findViewById(R.id.nav_profile).setOnClickListener(v -> showPage(1));
        findViewById(R.id.nav_settings).setOnClickListener(v -> showPage(2));
        findViewById(R.id.nav_settings).setOnLongClickListener(v -> {
            showAccountsDialog();
            return true;
        });
        showPage(0);
    }

    private void updateNav(int index) {
        for (int i = 0; i < pages.length; i++) {
            boolean selected = i == index;
            if (selected) {
                navIndicators[i].setBackgroundResource(R.drawable.bg_nav_indicator);
            } else {
                navIndicators[i].setBackgroundColor(Color.TRANSPARENT);
            }
            int color = getColor(selected ? R.color.near_black : R.color.text_secondary);
            navIcons[i].setColorFilter(color, PorterDuff.Mode.SRC_IN);
            navLabels[i].setTextColor(color);
            navLabels[i].setTypeface(Typeface.DEFAULT,
                    selected ? Typeface.BOLD : Typeface.NORMAL);
        }
    }

    private void showPage(int index) {
        if (index == currentPage && pages[index].getVisibility() == View.VISIBLE) {
            updateNav(index);
            return;
        }
        int previous = currentPage;
        currentPage = index;
        updateNav(index);

        View incoming = pages[index];
        View outgoing = pages[previous];
        incoming.animate().cancel();
        outgoing.animate().cancel();

        float direction = index > previous ? 1f : -1f;
        float distance = dp(28);
        incoming.setVisibility(View.VISIBLE);
        incoming.bringToFront();
        incoming.setAlpha(0f);
        incoming.setTranslationX(direction * distance);
        incoming.animate()
                .alpha(1f)
                .translationX(0f)
                .setDuration(260)
                .setInterpolator(new DecelerateInterpolator())
                .start();

        if (previous != index) {
            outgoing.animate()
                    .alpha(0f)
                    .translationX(-direction * distance * 0.55f)
                    .setDuration(190)
                    .setInterpolator(new DecelerateInterpolator())
                    .withEndAction(() -> {
                        outgoing.setVisibility(View.GONE);
                        outgoing.setAlpha(1f);
                        outgoing.setTranslationX(0f);
                    })
                    .start();
        }
    }

    private void setupChats() {
        officialChat.setOnClickListener(v -> {
            startActivity(new Intent(this, ChatActivity.class));
            overridePendingTransition(R.anim.slide_in_right, R.anim.slide_out_left);
        });
        clearSearch.setOnClickListener(v -> searchInput.setText(""));
        searchInput.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {
            }

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                applySearch(s.toString());
            }

            @Override
            public void afterTextChanged(Editable s) {
            }
        });
    }

    private void setupProfile() {
        findViewById(R.id.change_photo).setOnClickListener(v -> {
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.setType("image/*");
            startActivityForResult(intent, PICK_IMAGE);
        });
        saveButton.setOnClickListener(v -> saveProfile());
        findViewById(R.id.copy_id).setOnClickListener(v -> {
            String copyValue = publicId > 0 ? String.valueOf(publicId) : userId;
            if (copyValue.length() == 0) {
                return;
            }
            ClipboardManager clipboard = (ClipboardManager) getSystemService(CLIPBOARD_SERVICE);
            clipboard.setPrimaryClip(ClipData.newPlainText("visaroom ID", copyValue));
            Toast.makeText(this, "ID copied", Toast.LENGTH_SHORT).show();
        });
        findViewById(R.id.change_email).setOnClickListener(v -> showChangeEmailDialog());
        findViewById(R.id.profile_sign_out).setOnClickListener(v -> signOut());
    }

    private void setupSettings() {
        setupSettingsFolders();
        textSizeValue.setText(AppSettings.textSizeLabel(this));
        backgroundValue.setText(AppSettings.chatBackgroundLabel(this));

        Switch timestamps = findViewById(R.id.setting_timestamps);
        Switch enterSend = findViewById(R.id.setting_enter_send);
        Switch notifications = findViewById(R.id.setting_notifications);
        Switch sounds = findViewById(R.id.setting_sounds);
        Switch receipts = findViewById(R.id.setting_read_receipts);

        timestamps.setChecked(AppSettings.showTimestamps(this));
        enterSend.setChecked(AppSettings.enterToSend(this));
        notifications.setChecked(AppSettings.notifications(this));
        sounds.setChecked(AppSettings.sounds(this));
        receipts.setChecked(AppSettings.readReceipts(this));

        timestamps.setOnCheckedChangeListener((b, checked) ->
                AppSettings.setShowTimestamps(this, checked));
        enterSend.setOnCheckedChangeListener((b, checked) ->
                AppSettings.setEnterToSend(this, checked));
        notifications.setOnCheckedChangeListener((b, checked) ->
                AppSettings.setNotifications(this, checked));
        sounds.setOnCheckedChangeListener((b, checked) ->
                AppSettings.setSounds(this, checked));
        receipts.setOnCheckedChangeListener((b, checked) ->
                AppSettings.setReadReceipts(this, checked));

        showBioSwitch = findViewById(R.id.setting_show_bio);
        showAvatarSwitch = findViewById(R.id.setting_show_avatar);
        showIdSwitch = findViewById(R.id.setting_show_id);
        showBioSwitch.setChecked(true);
        showAvatarSwitch.setChecked(true);
        showIdSwitch.setChecked(true);
        showBioSwitch.setOnCheckedChangeListener((b, checked) -> {
            showBioState = checked;
            savePrivacySoon();
        });
        showAvatarSwitch.setOnCheckedChangeListener((b, checked) -> {
            showAvatarState = checked;
            savePrivacySoon();
        });
        showIdSwitch.setOnCheckedChangeListener((b, checked) -> {
            showIdState = checked;
            savePrivacySoon();
        });
        findViewById(R.id.setting_2fa).setOnClickListener(v -> showTwoFaDialog());
        updateTwoFaUi();
        findViewById(R.id.setting_admin_find).setOnClickListener(v -> showAdminFindDialog());
        updateAdminUi();

        findViewById(R.id.setting_text_size).setOnClickListener(v -> {
            int next = (AppSettings.textSizeIndex(this) + 1) % 3;
            AppSettings.setTextSizeIndex(this, next);
            textSizeValue.setText(AppSettings.textSizeLabel(this));
            applyChatTextSize();
        });
        findViewById(R.id.setting_background).setOnClickListener(v -> {
            int next = (AppSettings.chatBackground(this) + 1) % 3;
            AppSettings.setChatBackground(this, next);
            backgroundValue.setText(AppSettings.chatBackgroundLabel(this));
        });
        findViewById(R.id.setting_clear_search).setOnClickListener(v -> {
            searchInput.setText("");
            showPage(0);
        });
        findViewById(R.id.setting_accounts).setOnClickListener(v -> showAccountsDialog());
        updateAccountsUi();
        findViewById(R.id.setting_sign_out).setOnClickListener(v -> signOut());
    }

    private void setupSettingsFolders() {
        settingsFolderHeaders = new TextView[]{
                findViewById(R.id.folder_chats_header),
                findViewById(R.id.folder_notifications_header),
                findViewById(R.id.folder_security_header),
                findViewById(R.id.folder_privacy_header),
                findViewById(R.id.folder_moderation_header),
                findViewById(R.id.folder_account_header)};
        settingsFolderContents = new View[]{
                findViewById(R.id.folder_chats_content),
                findViewById(R.id.folder_notifications_content),
                findViewById(R.id.folder_security_content),
                findViewById(R.id.folder_privacy_content),
                findViewById(R.id.folder_moderation_content),
                findViewById(R.id.folder_account_content)};
        settingsFolderTitles = new String[]{
                "Chats", "Notifications", "Security", "Privacy", "Moderation", "Account"};
        settingsFolderDescriptions = new String[]{
                "Text size, background, timestamps, Enter to send, clear search",
                "Message notifications and sounds",
                "Two-step verification",
                "Read receipts, bio, photo and ID visibility",
                "Admin tools: find users, block accounts, set any ID",
                "Saved accounts and sign out"};

        for (int i = 0; i < settingsFolderHeaders.length; i++) {
            final int index = i;
            TextView header = settingsFolderHeaders[i];
            View content = settingsFolderContents[i];
            if (header == null || content == null) {
                continue;
            }
            header.setTextSize(16);
            header.setTextColor(getColor(R.color.text_main));
            header.setPadding(dp(24), dp(12), dp(24), dp(12));
            header.setLineSpacing(0f, 1.08f);
            setFolderOpen(index, index == 0, false);
            header.setOnClickListener(v -> {
                boolean openNext = content.getVisibility() != View.VISIBLE;
                if (openNext) {
                    closeOtherSettingsFolders(index);
                }
                setFolderOpen(index, openNext, true);
            });
        }
    }

    private void closeOtherSettingsFolders(int exceptIndex) {
        if (settingsFolderContents == null) {
            return;
        }
        for (int i = 0; i < settingsFolderContents.length; i++) {
            if (i != exceptIndex && settingsFolderContents[i] != null
                    && settingsFolderContents[i].getVisibility() == View.VISIBLE) {
                setFolderOpen(i, false, true);
            }
        }
    }

    private void setFolderOpen(int index, boolean open, boolean animate) {
        TextView header = settingsFolderHeaders[index];
        View content = settingsFolderContents[index];
        if (header == null || content == null) {
            return;
        }
        SpannableStringBuilder text = new SpannableStringBuilder();
        text.append(settingsFolderTitles[index]);
        text.setSpan(new StyleSpan(Typeface.BOLD), 0, text.length(),
                Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
        text.append(open ? "  -" : "  +");
        int descriptionStart = text.length() + 1;
        text.append("\n").append(settingsFolderDescriptions[index]);
        text.setSpan(new RelativeSizeSpan(0.78f), descriptionStart, text.length(),
                Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
        text.setSpan(new ForegroundColorSpan(getColor(R.color.text_secondary)),
                descriptionStart, text.length(), Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
        header.setText(text);

        content.animate().cancel();
        if (open) {
            content.setVisibility(View.VISIBLE);
            if (animate) {
                content.setAlpha(0f);
                content.setTranslationY(-dp(6));
                content.animate().alpha(1f).translationY(0f)
                        .setDuration(170)
                        .setInterpolator(new DecelerateInterpolator())
                        .start();
            } else {
                content.setAlpha(1f);
                content.setTranslationY(0f);
            }
        } else if (animate && content.getVisibility() == View.VISIBLE) {
            content.animate().alpha(0f).translationY(-dp(6))
                    .setDuration(140)
                    .setInterpolator(new DecelerateInterpolator())
                    .withEndAction(() -> {
                        content.setVisibility(View.GONE);
                        content.setAlpha(1f);
                        content.setTranslationY(0f);
                    })
                    .start();
        } else {
            content.setVisibility(View.GONE);
            content.setAlpha(1f);
            content.setTranslationY(0f);
        }
    }

    private void updateAccountsUi() {
        if (accountsValue != null) {
            accountsValue.setText(TokenStore.accountCount(this)
                    + "/" + TokenStore.MAX_ACCOUNTS);
        }
    }

    private String accountLabel(TokenStore.Account account, int index) {
        SharedPreferences prefs = TokenStore.profilePrefs(this, account.uid);
        String username = prefs.getString("username", "");
        if (username != null && username.trim().length() > 0) {
            return "@" + username.trim();
        }
        long storedPublicId = prefs.getLong("public_id", 0);
        if (storedPublicId > 0) {
            return "ID " + storedPublicId;
        }
        return "Account " + (index + 1);
    }

    private void restartChatsForAccount() {
        Intent intent = new Intent(this, ChatsActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        startActivity(intent);
        finish();
        overridePendingTransition(R.anim.slide_in_right, R.anim.slide_out_left);
    }

    private void showAccountsDialog() {
        java.util.List<TokenStore.Account> accountsList = TokenStore.accounts(this);
        int activeSlot = TokenStore.activeSlot(this);

        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(24), dp(8), dp(24), dp(8));

        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle("Accounts")
                .setView(box)
                .setNegativeButton("Close", null)
                .create();

        TextView hint = new TextView(this);
        hint.setText("Up to 3 accounts can stay signed in on this device.");
        hint.setTextColor(getColor(R.color.text_secondary));
        hint.setTextSize(13);
        box.addView(hint, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

        for (int i = 0; i < accountsList.size(); i++) {
            final TokenStore.Account account = accountsList.get(i);
            final boolean current = account.slot == activeSlot;

            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            row.setBackgroundResource(R.drawable.bg_chat_row);
            row.setMinimumHeight(dp(64));
            row.setPadding(dp(14), dp(8), dp(14), dp(8));
            row.setClickable(true);
            row.setFocusable(true);

            LinearLayout labels = new LinearLayout(this);
            labels.setOrientation(LinearLayout.VERTICAL);
            TextView title = new TextView(this);
            title.setText(accountLabel(account, i));
            title.setTextColor(getColor(R.color.text_main));
            title.setTextSize(15);
            title.setTypeface(Typeface.DEFAULT, current ? Typeface.BOLD : Typeface.NORMAL);
            labels.addView(title);

            TextView detail = new TextView(this);
            detail.setText(current ? "Current account"
                    : "Tap to switch. Long-press to remove from this device.");
            detail.setTextColor(getColor(R.color.text_secondary));
            detail.setTextSize(12);
            labels.addView(detail);
            row.addView(labels, new LinearLayout.LayoutParams(
                    0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f));

            TextView state = new TextView(this);
            state.setText(current ? "Current" : "Switch");
            state.setTextColor(getColor(R.color.text_secondary));
            state.setTextSize(13);
            row.addView(state);

            LinearLayout.LayoutParams rowParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT);
            rowParams.topMargin = dp(12);
            box.addView(row, rowParams);

            row.setOnClickListener(v -> {
                if (current) {
                    dialog.dismiss();
                } else if (TokenStore.switchAccount(this, account.slot)) {
                    dialog.dismiss();
                    restartChatsForAccount();
                } else {
                    Toast.makeText(this, "This saved session is no longer available.",
                            Toast.LENGTH_SHORT).show();
                }
            });
            if (!current) {
                row.setOnLongClickListener(v -> {
                    confirmRemoveAccount(account, dialog);
                    return true;
                });
            }
        }

        if (accountsList.size() < TokenStore.MAX_ACCOUNTS) {
            TextView add = new TextView(this);
            add.setText("Add account");
            add.setTextColor(getColor(R.color.text_main));
            add.setTextSize(15);
            add.setGravity(Gravity.CENTER_VERTICAL);
            add.setBackgroundResource(R.drawable.bg_chat_row);
            add.setMinimumHeight(dp(56));
            add.setPadding(dp(14), 0, dp(14), 0);
            add.setClickable(true);
            add.setFocusable(true);
            LinearLayout.LayoutParams addParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT);
            addParams.topMargin = dp(12);
            box.addView(add, addParams);
            add.setOnClickListener(v -> {
                dialog.dismiss();
                Intent intent = new Intent(this, AuthActivity.class);
                intent.putExtra(AuthActivity.EXTRA_ADD_ACCOUNT, true);
                startActivity(intent);
                overridePendingTransition(R.anim.slide_in_right, R.anim.slide_out_left);
            });
        } else {
            TextView limit = new TextView(this);
            limit.setText("Maximum of 3 accounts on this device.");
            limit.setTextColor(getColor(R.color.text_secondary));
            limit.setTextSize(13);
            LinearLayout.LayoutParams limitParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT);
            limitParams.topMargin = dp(14);
            box.addView(limit, limitParams);
        }

        dialog.show();
        box.setAlpha(0f);
        box.setTranslationY(dp(14));
        box.animate().alpha(1f).translationY(0f)
                .setDuration(190)
                .setInterpolator(new DecelerateInterpolator())
                .start();
    }

    private void confirmRemoveAccount(TokenStore.Account account, AlertDialog parent) {
        new AlertDialog.Builder(this)
                .setTitle("Remove account?")
                .setMessage("This removes the saved session from this device. "
                        + "The visaroom account itself is not deleted.")
                .setNegativeButton("Cancel", null)
                .setPositiveButton("Remove", (d, w) -> {
                    TokenStore.profilePrefs(this, account.uid).edit().clear().apply();
                    File file = avatarFileFor(account.uid);
                    if (file.exists()) {
                        file.delete();
                    }
                    TokenStore.removeAccount(this, account.slot);
                    updateAccountsUi();
                    parent.dismiss();
                    Toast.makeText(this, "Account removed from this device.",
                            Toast.LENGTH_SHORT).show();
                    showAccountsDialog();
                })
                .show();
    }

    private void applyChatTextSize() {
        int sp = AppSettings.textSizeSp(this);
        officialName.setTextSize(sp);
        officialPreview.setTextSize(Math.max(12, sp - 2));
    }

    private void signOut() {
        SharedPreferences cache = profileCache();
        File localAvatar = avatarFile();
        int slot = TokenStore.activeSlot(this);
        if (slot >= 0) {
            TokenStore.removeAccount(this, slot);
        } else {
            TokenStore.clear(this);
        }
        cache.edit().clear().apply();
        if (localAvatar.exists()) {
            localAvatar.delete();
        }
        boolean hasOtherAccount = TokenStore.accountCount(this) > 0;
        if (hasOtherAccount) {
            Toast.makeText(this, "Signed out. Switched to another saved account.",
                    Toast.LENGTH_SHORT).show();
        }
        Intent intent = new Intent(this,
                hasOtherAccount ? ChatsActivity.class : AuthActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        startActivity(intent);
        finish();
        overridePendingTransition(R.anim.slide_in_left, R.anim.slide_out_right);
    }

    // ---------- chats search ----------

    private String cleanQuery(String raw) {
        String q = raw == null ? "" : raw.trim().toLowerCase(Locale.ROOT);
        return q.startsWith("@") ? q.substring(1) : q;
    }

    private void applySearch(String rawText) {
        String q = cleanQuery(rawText);
        clearSearch.setVisibility(rawText.trim().length() == 0 ? View.GONE : View.VISIBLE);

        boolean officialMatch = q.length() == 0
                || "visaroom".contains(q)
                || "official channel".contains(q)
                || "channel".contains(q);
        officialChat.setVisibility(officialMatch ? View.VISIBLE : View.GONE);
        findViewById(R.id.chats_label).setVisibility(
                officialMatch ? View.VISIBLE : View.GONE);
        int directShown = renderDirectChats();

        if (globalSearchRunnable != null) {
            handler.removeCallbacks(globalSearchRunnable);
        }
        if (q.length() == 0) {
            pendingGlobalQuery = "";
            globalResults.removeAllViews();
            peopleLabel.setVisibility(View.GONE);
            searchHint.setVisibility(View.GONE);
            noChats.setVisibility(View.GONE);
            return;
        }
        if (q.length() == 1) {
            globalResults.removeAllViews();
            peopleLabel.setVisibility(View.GONE);
            searchHint.setVisibility(View.VISIBLE);
            noChats.setVisibility(officialMatch || directShown > 0
                    ? View.GONE : View.VISIBLE);
            return;
        }
        searchHint.setVisibility(View.GONE);
        pendingGlobalQuery = q;
        globalSearchRunnable = () -> runGlobalSearch(q, officialMatch);
        handler.postDelayed(globalSearchRunnable, 350);
    }

    private void startDirectChatsPolling() {
        stopDirectChatsPolling();
        directChatsPoll = new Runnable() {
            @Override
            public void run() {
                loadDirectChats();
                handler.postDelayed(this, 3000);
            }
        };
        handler.postDelayed(directChatsPoll, 3000);
    }

    private void stopDirectChatsPolling() {
        if (directChatsPoll != null) {
            handler.removeCallbacks(directChatsPoll);
            directChatsPoll = null;
        }
    }

    private void loadDirectChats() {
        if (directChatsLoading || userId.length() == 0) {
            return;
        }
        directChatsLoading = true;
        executor.execute(() -> {
            String token = ensureIdToken();
            List<VrProto.ChatSummary> loaded = token == null ? null
                    : VrProto.listChats(token, userId);
            runOnUiThread(() -> {
                directChatsLoading = false;
                if (loaded != null) {
                    directChatList.clear();
                    directChatList.addAll(loaded);
                    directChatsLoaded = true;
                    renderDirectChats();
                }
            });
        });
    }

    private boolean directMatches(VrProto.ChatSummary summary, String q) {
        if (q.length() == 0) {
            return true;
        }
        return summary.username.toLowerCase(Locale.ROOT).contains(q)
                || summary.lastText.toLowerCase(Locale.ROOT).contains(q)
                || String.valueOf(summary.publicId).equals(q);
    }

    private int directMatchCount() {
        String q = cleanQuery(searchInput == null ? "" : searchInput.getText().toString());
        int count = 0;
        for (VrProto.ChatSummary summary : directChatList) {
            if (directMatches(summary, q)) {
                count++;
            }
        }
        return count;
    }

    private int renderDirectChats() {
        if (directChats == null) {
            return 0;
        }
        String q = cleanQuery(searchInput == null ? "" : searchInput.getText().toString());
        directChats.removeAllViews();
        int shown = 0;
        for (VrProto.ChatSummary summary : directChatList) {
            if (!directMatches(summary, q)) {
                continue;
            }
            directChats.addView(makeDirectChatRow(summary));
            View divider = new View(this);
            divider.setLayoutParams(new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, dp(8)));
            divider.setBackgroundColor(Color.TRANSPARENT);
            directChats.addView(divider);
            shown++;
        }
        directLabel.setVisibility(shown > 0 ? View.VISIBLE : View.GONE);
        directEmpty.setVisibility(q.length() == 0 && directChatsLoaded
                && directChatList.isEmpty() ? View.VISIBLE : View.GONE);
        if (q.length() > 0 && shown > 0) {
            noChats.setVisibility(View.GONE);
        }
        return shown;
    }

    private ImageView verifiedIcon() {
        ImageView icon = new ImageView(this);
        icon.setImageResource(R.drawable.ic_verified);
        icon.setContentDescription("Verified");
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(dp(16), dp(16));
        params.leftMargin = dp(5);
        icon.setLayoutParams(params);
        return icon;
    }

    private View makeDirectChatRow(VrProto.ChatSummary summary) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(dp(16), dp(12), dp(16), dp(12));
        row.setBackgroundResource(R.drawable.bg_glass_card);
        row.setElevation(dp(3));
        row.setClickable(true);
        row.setFocusable(true);
        LinearLayout.LayoutParams rowParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        rowParams.leftMargin = dp(12);
        rowParams.rightMargin = dp(12);
        rowParams.topMargin = dp(4);
        row.setLayoutParams(rowParams);

        ImageView icon = new ImageView(this);
        icon.setImageResource(R.drawable.ic_nav_profile);
        icon.setColorFilter(Color.BLACK, PorterDuff.Mode.SRC_IN);
        icon.setClickable(true);
        icon.setFocusable(true);
        row.addView(icon, new LinearLayout.LayoutParams(dp(42), dp(42)));

        LinearLayout texts = new LinearLayout(this);
        texts.setOrientation(LinearLayout.VERTICAL);
        texts.setClickable(true);
        texts.setFocusable(true);
        LinearLayout.LayoutParams textParams = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
        textParams.leftMargin = dp(12);

        LinearLayout nameRow = new LinearLayout(this);
        nameRow.setOrientation(LinearLayout.HORIZONTAL);
        nameRow.setGravity(Gravity.CENTER_VERTICAL);
        TextView name = new TextView(this);
        name.setText(summary.username.length() > 0
                ? "@" + summary.username
                : summary.publicId > 0 ? "ID " + summary.publicId : "User");
        name.setTextColor(getColor(R.color.text_main));
        name.setTextSize(AppSettings.textSizeSp(this));
        name.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        name.setSingleLine(true);
        name.setEllipsize(TextUtils.TruncateAt.END);
        nameRow.addView(name);
        if (summary.verified) {
            nameRow.addView(verifiedIcon());
        }
        texts.addView(nameRow);

        TextView preview = new TextView(this);
        String last = summary.lastText.replace('\n', ' ').trim();
        if (summary.blocked) {
            preview.setText("Account blocked");
        } else {
            preview.setText(summary.lastSenderUid.equals(userId) && last.length() > 0
                    ? "You: " + last : last);
        }
        preview.setTextColor(getColor(R.color.text_secondary));
        preview.setTextSize(Math.max(12, AppSettings.textSizeSp(this) - 3));
        preview.setSingleLine(true);
        preview.setEllipsize(TextUtils.TruncateAt.END);
        texts.addView(preview);
        row.addView(texts, textParams);

        LinearLayout side = new LinearLayout(this);
        side.setOrientation(LinearLayout.VERTICAL);
        side.setGravity(Gravity.END);
        LinearLayout.LayoutParams sideParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        sideParams.leftMargin = dp(10);
        if (AppSettings.showTimestamps(this) && summary.lastMessageAt > 0) {
            TextView time = new TextView(this);
            time.setText(formatChatTime(summary.lastMessageAt));
            time.setTextColor(getColor(R.color.text_secondary));
            time.setTextSize(12);
            time.setGravity(Gravity.END);
            side.addView(time);
        }
        boolean unread = !summary.lastSenderUid.equals(userId)
                && summary.lastMessageAt > summary.lastReadAt;
        if (unread) {
            View dot = new View(this);
            dot.setBackgroundResource(R.drawable.bg_unread_dot);
            LinearLayout.LayoutParams dotParams = new LinearLayout.LayoutParams(dp(9), dp(9));
            dotParams.topMargin = dp(7);
            dotParams.gravity = Gravity.END;
            side.addView(dot, dotParams);
        }
        row.addView(side, sideParams);

        icon.setOnClickListener(v -> showPublicProfile(summary.peerUid, summary.username));
        texts.setOnClickListener(v -> showPublicProfile(summary.peerUid, summary.username));
        row.setOnClickListener(v -> openDirectChat(summary.peerUid, summary.username));
        return row;
    }

    private String formatChatTime(long millis) {
        Calendar then = Calendar.getInstance();
        then.setTimeInMillis(millis);
        Calendar now = Calendar.getInstance();
        boolean sameDay = then.get(Calendar.YEAR) == now.get(Calendar.YEAR)
                && then.get(Calendar.DAY_OF_YEAR) == now.get(Calendar.DAY_OF_YEAR);
        return new SimpleDateFormat(sameDay ? "HH:mm" : "dd.MM", Locale.ROOT)
                .format(new Date(millis));
    }

    private void openDirectChat(String uid, String username) {
        Intent intent = new Intent(this, DirectChatActivity.class);
        intent.putExtra(DirectChatActivity.EXTRA_UID, uid);
        intent.putExtra(DirectChatActivity.EXTRA_USERNAME, username);
        startActivity(intent);
        overridePendingTransition(R.anim.slide_in_right, R.anim.slide_out_left);
    }

    private void runGlobalSearch(String query, boolean officialMatch) {
        executor.execute(() -> {
            String token = ensureIdToken();
            List<ProfileClient.SearchResult> results = token == null
                    ? new java.util.ArrayList<>()
                    : ProfileClient.searchUsernames(token, query);
            runOnUiThread(() -> {
                if (!query.equals(cleanQuery(searchInput.getText().toString()))) {
                    return;
                }
                renderGlobalResults(results, officialMatch);
            });
        });
    }

    private void renderGlobalResults(List<ProfileClient.SearchResult> results,
                                      boolean officialMatch) {
        globalResults.removeAllViews();
        int shown = 0;
        for (ProfileClient.SearchResult r : results) {
            if (r.uid.equals(userId)) {
                continue;
            }
            globalResults.addView(makeUserRow(r));
            View divider = new View(this);
            divider.setLayoutParams(new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, dp(8)));
            divider.setBackgroundColor(Color.TRANSPARENT);
            globalResults.addView(divider);
            shown++;
        }
        peopleLabel.setVisibility(shown > 0 ? View.VISIBLE : View.GONE);
        noChats.setVisibility(shown == 0 && !officialMatch && directMatchCount() == 0
                ? View.VISIBLE : View.GONE);
    }

    private View makeUserRow(ProfileClient.SearchResult result) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(dp(16), dp(12), dp(16), dp(12));
        row.setBackgroundResource(R.drawable.bg_glass_card);
        row.setElevation(dp(3));
        row.setClickable(true);
        row.setFocusable(true);
        LinearLayout.LayoutParams rowParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        rowParams.leftMargin = dp(12);
        rowParams.rightMargin = dp(12);
        rowParams.topMargin = dp(4);
        row.setLayoutParams(rowParams);

        ImageView icon = new ImageView(this);
        icon.setImageResource(R.drawable.ic_nav_profile);
        icon.setColorFilter(Color.BLACK, PorterDuff.Mode.SRC_IN);
        LinearLayout.LayoutParams iconParams =
                new LinearLayout.LayoutParams(dp(42), dp(42));
        row.addView(icon, iconParams);

        LinearLayout texts = new LinearLayout(this);
        texts.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams textParams = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
        textParams.leftMargin = dp(12);

        LinearLayout nameRow = new LinearLayout(this);
        nameRow.setOrientation(LinearLayout.HORIZONTAL);
        nameRow.setGravity(Gravity.CENTER_VERTICAL);
        TextView name = new TextView(this);
        name.setText("@" + result.username);
        name.setTextColor(getColor(R.color.text_main));
        name.setTextSize(15);
        name.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        nameRow.addView(name);
        if (result.verified) {
            nameRow.addView(verifiedIcon());
        }
        texts.addView(nameRow);

        TextView sub = new TextView(this);
        sub.setText(result.publicId > 0 ? "ID " + result.publicId : "User");
        sub.setTextColor(getColor(R.color.text_secondary));
        sub.setTextSize(13);
        texts.addView(sub);

        row.addView(texts, textParams);
        row.setOnClickListener(v -> showPublicProfile(result.uid, result.username));
        return row;
    }

    private void showPublicProfile(String uid, String username) {
        Intent intent = new Intent(this, UserProfileActivity.class);
        intent.putExtra(UserProfileActivity.EXTRA_UID, uid);
        intent.putExtra(UserProfileActivity.EXTRA_USERNAME, username);
        startActivity(intent);
        overridePendingTransition(R.anim.slide_in_right, R.anim.slide_out_left);
    }

    // ---------- profile ----------

    private SharedPreferences profileCache() {
        return TokenStore.profilePrefs(this, userId);
    }

    private void loadLocalProfile() {
        SharedPreferences p = profileCache();
        String username = p.getString("username", "");
        usernameInput.setText(username);
        originalUsernameLower = username == null ? ""
                : username.toLowerCase(Locale.ROOT);
        bioInput.setText(p.getString("bio", ""));
        discoverableSwitch.setChecked(p.getBoolean("discoverable", true));
        hasAvatar = p.getBoolean("has_avatar", avatarFile().exists());
        avatarUpdatedAt = p.getLong("avatar_at", 0);
        publicId = p.getLong("public_id", 0);
        if (publicId == 0) {
            publicId = ProfileClient.randomPublicId();
            p.edit().putLong("public_id", publicId).apply();
        }
        twoFaEnabledState = p.getBoolean("two_fa", false);
        String localVerifier = TokenStore.twoFaVerifierForUid(this, userId);
        if (localVerifier == null) {
            localVerifier = TokenStore.twoFaVerifier(this);
        }
        if (localVerifier != null) {
            twoFaVerifierState = localVerifier;
        }
        showBioState = p.getBoolean("show_bio", true);
        showAvatarState = p.getBoolean("show_avatar", true);
        showIdState = p.getBoolean("show_id", true);
        verifiedState = p.getBoolean("verified", false);
        applyingSettings = true;
        if (showBioSwitch != null) {
            showBioSwitch.setChecked(showBioState);
            showAvatarSwitch.setChecked(showAvatarState);
            showIdSwitch.setChecked(showIdState);
        }
        applyingSettings = false;
        updateTwoFaUi();
        updateIdDisplay();
    }

    private void cacheProfile(String username, String bio, boolean discoverable) {
        profileCache().edit()
                .putString("username", username)
                .putString("bio", bio)
                .putBoolean("discoverable", discoverable)
                .putBoolean("has_avatar", hasAvatar)
                .putLong("avatar_at", avatarUpdatedAt)
                .putLong("public_id", publicId)
                .putBoolean("two_fa", twoFaEnabledState)
                .putBoolean("show_bio", showBioState)
                .putBoolean("show_avatar", showAvatarState)
                .putBoolean("show_id", showIdState)
                .putBoolean("verified", verifiedState)
                .apply();
    }

    private File avatarFile() {
        return avatarFileFor(userId);
    }

    private File avatarFileFor(String uid) {
        File legacy = new File(getFilesDir(), "avatar.jpg");
        if (uid == null || uid.length() == 0) {
            return legacy;
        }
        File perAccount = new File(getFilesDir(),
                "avatar_" + uid.replaceAll("[^A-Za-z0-9]", "_") + ".jpg");
        if (!perAccount.exists() && legacy.exists() && uid.equals(TokenStore.uid(this))) {
            legacy.renameTo(perAccount);
        }
        return perAccount;
    }

    private void displayAvatar() {
        File f = avatarFile();
        if (f.exists()) {
            Bitmap bmp = BitmapFactory.decodeFile(f.getAbsolutePath());
            if (bmp != null) {
                profileAvatar.setImageBitmap(circularBitmap(bmp));
                return;
            }
        }
        profileAvatar.setImageResource(R.drawable.ic_logo);
    }

    private Bitmap circularBitmap(Bitmap source) {
        int size = Math.min(source.getWidth(), source.getHeight());
        int x = (source.getWidth() - size) / 2;
        int y = (source.getHeight() - size) / 2;
        Bitmap square = Bitmap.createBitmap(source, x, y, size, size);
        Bitmap out = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(out);
        Path path = new Path();
        path.addCircle(size / 2f, size / 2f, size / 2f, Path.Direction.CW);
        canvas.clipPath(path);
        canvas.drawBitmap(square, 0, 0, null);
        return out;
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != PICK_IMAGE || resultCode != RESULT_OK
                || data == null || data.getData() == null) {
            return;
        }
        Uri uri = data.getData();
        executor.execute(() -> {
            boolean ok = processPickedImage(uri);
            runOnUiThread(() -> {
                if (ok) {
                    avatarChanged = true;
                    hasAvatar = true;
                    avatarUpdatedAt = System.currentTimeMillis();
                    displayAvatar();
                    showProfileStatus("Photo selected. Tap Save changes.");
                } else {
                    showProfileStatus("Could not load this photo.");
                }
            });
        });
    }

    private boolean processPickedImage(Uri uri) {
        try {
            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            InputStream first = getContentResolver().openInputStream(uri);
            BitmapFactory.decodeStream(first, null, bounds);
            if (first != null) {
                first.close();
            }
            int sample = 1;
            while (bounds.outWidth / sample > 1024 || bounds.outHeight / sample > 1024) {
                sample *= 2;
            }
            BitmapFactory.Options options = new BitmapFactory.Options();
            options.inSampleSize = sample;
            InputStream second = getContentResolver().openInputStream(uri);
            Bitmap bitmap = BitmapFactory.decodeStream(second, null, options);
            if (second != null) {
                second.close();
            }
            if (bitmap == null) {
                return false;
            }
            int size = Math.min(bitmap.getWidth(), bitmap.getHeight());
            Bitmap square = Bitmap.createBitmap(bitmap,
                    (bitmap.getWidth() - size) / 2,
                    (bitmap.getHeight() - size) / 2, size, size);
            Bitmap scaled = Bitmap.createScaledBitmap(square, 256, 256, true);
            ByteArrayOutputStream mem = new ByteArrayOutputStream();
            int quality = 82;
            scaled.compress(Bitmap.CompressFormat.JPEG, quality, mem);
            while (mem.size() > 120000 && quality > 40) {
                mem.reset();
                quality -= 14;
                scaled.compress(Bitmap.CompressFormat.JPEG, quality, mem);
            }
            FileOutputStream out = new FileOutputStream(avatarFile());
            out.write(mem.toByteArray());
            out.close();
            return true;
        } catch (Exception ignored) {
            return false;
        }
    }

    private byte[] readAvatarFile() {
        try {
            FileInputStream in = new FileInputStream(avatarFile());
            ByteArrayOutputStream buffer = new ByteArrayOutputStream();
            byte[] chunk = new byte[8192];
            int n;
            while ((n = in.read(chunk)) != -1) {
                buffer.write(chunk, 0, n);
            }
            in.close();
            return buffer.toByteArray();
        } catch (Exception ignored) {
            return null;
        }
    }

    private String ensureIdToken() {
        if (latestIdToken != null
                && System.currentTimeMillis() - latestIdTokenAt < 40 * 60 * 1000L) {
            return latestIdToken;
        }
        String refreshToken = TokenStore.refreshToken(this);
        if (refreshToken == null || refreshToken.length() == 0) {
            return null;
        }
        AuthClient.Result result = AuthClient.refresh(refreshToken);
        if (!result.ok) {
            return null;
        }
        latestIdToken = result.idToken;
        latestIdTokenAt = System.currentTimeMillis();
        String tokenEmail = AuthClient.emailFromIdToken(result.idToken);
        if (tokenEmail.length() > 0) {
            userEmail = tokenEmail;
            TokenStore.save(this, tokenEmail, result.refreshToken);
        } else {
            TokenStore.save(this, null, result.refreshToken);
        }
        if (result.uid.length() > 0) {
            userId = result.uid;
            TokenStore.saveUid(this, result.uid);
        }
        return latestIdToken;
    }

    private void updateIdDisplay() {
        idView.setText(publicId > 0 ? String.valueOf(publicId)
                : userId.length() == 0 ? "Loading..." : "Assigning...");
    }

    private void updateTwoFaUi() {
        if (twoFaValue != null) {
            twoFaValue.setText(twoFaEnabledState ? "On" : "Off");
        }
    }

    private ProfileData currentProfileData() {
        ProfileData data = new ProfileData();
        data.email = userEmail;
        String raw = usernameInput.getText().toString().trim();
        data.username = raw.startsWith("@") ? raw.substring(1) : raw;
        data.bio = bioInput.getText().toString().trim();
        data.discoverable = discoverableSwitch.isChecked();
        data.hasAvatar = hasAvatar;
        data.avatarUpdatedAt = avatarUpdatedAt;
        data.publicId = publicId;
        data.twoFaEnabled = twoFaEnabledState;
        data.twoFaVerifier = twoFaVerifierState;
        data.showBio = showBioState;
        data.showAvatar = showAvatarState;
        data.showId = showIdState;
        data.verified = verifiedState;
        return data;
    }

    private void savePrivacySoon() {
        if (applyingSettings) {
            return;
        }
        ProfileData snapshot = currentProfileData();
        cacheProfile(snapshot.username, snapshot.bio, snapshot.discoverable);
        executor.execute(() -> {
            String token = ensureIdToken();
            if (token != null && userId.length() > 0) {
                ProfileClient.saveMyProfile(token, userId, snapshot);
            }
        });
    }

    private EditText passwordField(String hint) {
        EditText input = new EditText(this);
        input.setHint(hint);
        input.setInputType(InputType.TYPE_CLASS_TEXT
                | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        input.setBackgroundResource(R.drawable.bg_input);
        input.setPadding(dp(14), 0, dp(14), 0);
        return input;
    }

    private LinearLayout dialogBox(EditText... fields) {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(24), dp(8), dp(24), dp(4));
        for (int i = 0; i < fields.length; i++) {
            LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT, dp(52));
            if (i > 0) {
                params.topMargin = dp(12);
            }
            box.addView(fields[i], params);
        }
        return box;
    }

    private void updateAdminUi() {
        if (moderationSection != null) {
            moderationSection.setVisibility(
                    Admin.isAdminEmail(userEmail) ? View.VISIBLE : View.GONE);
        }
    }

    private EditText textField(String hint) {
        EditText input = new EditText(this);
        input.setHint(hint);
        input.setInputType(InputType.TYPE_CLASS_TEXT);
        input.setSingleLine(true);
        input.setBackgroundResource(R.drawable.bg_input);
        input.setPadding(dp(14), 0, dp(14), 0);
        return input;
    }

    private void showAdminFindDialog() {
        if (!Admin.isAdminEmail(userEmail)) {
            updateAdminUi();
            return;
        }
        EditText input = textField("@username or ID");
        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle("Find user")
                .setMessage("Enter a username or any numeric ID.")
                .setView(dialogBox(input))
                .setNegativeButton("Cancel", null)
                .setPositiveButton("Find", null)
                .create();
        dialog.show();
        dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            String raw = input.getText().toString().trim();
            if (raw.length() == 0) {
                Toast.makeText(this, "Enter a username or ID.", Toast.LENGTH_SHORT).show();
                return;
            }
            dialog.dismiss();
            Toast.makeText(this, "Searching...", Toast.LENGTH_SHORT).show();
            executor.execute(() -> {
                String token = ensureIdToken();
                String foundUid = "";
                String foundName = "";
                if (token != null) {
                    if (raw.matches("\\d+")) {
                        try {
                            long wanted = Long.parseLong(raw);
                            foundUid = ProfileClient.loadUidByPublicId(token, wanted);
                            if (foundUid.length() > 0) {
                                foundName = ProfileClient
                                        .loadPublicProfile(token, foundUid).username;
                            }
                        } catch (Exception ignored) {
                        }
                    } else {
                        String lower = cleanQuery(raw);
                        if (USERNAME.matcher(lower).matches()) {
                            foundUid = ProfileClient.loadUidByUsername(token, lower);
                            foundName = raw.startsWith("@") ? raw.substring(1) : raw;
                        }
                    }
                }
                final String uidResult = foundUid;
                final String nameResult = foundName;
                runOnUiThread(() -> {
                    updateAdminUi();
                    if (uidResult.length() == 0) {
                        Toast.makeText(this,
                                "User not found. Check the username/ID and the Firebase rules.",
                                Toast.LENGTH_LONG).show();
                    } else {
                        showPublicProfile(uidResult, nameResult);
                    }
                });
            });
        });
    }

    private void showTwoFaDialog() {
        if (!twoFaEnabledState) {
            EditText password = passwordField("New additional password");
            EditText confirm = passwordField("Repeat password");
            AlertDialog dialog = new AlertDialog.Builder(this)
                    .setTitle("Two-step verification")
                    .setMessage("This password will be asked in addition to your account password when you sign in.")
                    .setView(dialogBox(password, confirm))
                    .setNegativeButton("Cancel", null)
                    .setPositiveButton("Turn on", null)
                    .create();
            dialog.show();
            dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
                String pw = password.getText().toString();
                if (pw.length() < 4) {
                    Toast.makeText(this, "Use at least 4 characters.", Toast.LENGTH_SHORT).show();
                    return;
                }
                if (!pw.equals(confirm.getText().toString())) {
                    Toast.makeText(this, "Passwords do not match.", Toast.LENGTH_SHORT).show();
                    return;
                }
                dialog.dismiss();
                saveTwoFa(true, pw);
            });
            return;
        }
        EditText current = passwordField("Current additional password");
        EditText next = passwordField("New password (optional)");
        EditText confirm = passwordField("Repeat new password");
        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle("Two-step verification")
                .setView(dialogBox(current, next, confirm))
                .setNegativeButton("Turn off", null)
                .setNeutralButton("Cancel", null)
                .setPositiveButton("Save", null)
                .create();
        dialog.show();
        dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            if (!TwoFa.verify(current.getText().toString(), twoFaVerifierState)) {
                Toast.makeText(this, "Current password is incorrect.", Toast.LENGTH_SHORT).show();
                return;
            }
            String pw = next.getText().toString();
            if (pw.length() == 0) {
                dialog.dismiss();
                return;
            }
            if (pw.length() < 4 || !pw.equals(confirm.getText().toString())) {
                Toast.makeText(this, "Check the new password.", Toast.LENGTH_SHORT).show();
                return;
            }
            dialog.dismiss();
            saveTwoFa(true, pw);
        });
        dialog.getButton(AlertDialog.BUTTON_NEGATIVE).setOnClickListener(v -> {
            if (!TwoFa.verify(current.getText().toString(), twoFaVerifierState)) {
                Toast.makeText(this, "Current password is incorrect.", Toast.LENGTH_SHORT).show();
                return;
            }
            dialog.dismiss();
            saveTwoFa(false, "");
        });
    }

    private void saveTwoFa(boolean enabled, String plainPassword) {
        ProfileData snapshot = currentProfileData();
        executor.execute(() -> {
            String verifier = enabled ? TwoFa.makeVerifier(plainPassword) : "";
            snapshot.twoFaEnabled = enabled;
            snapshot.twoFaVerifier = verifier;
            String token = ensureIdToken();
            boolean savedCloud = token != null && userId.length() > 0
                    && ProfileClient.saveMyProfile(token, userId, snapshot);
            TokenStore.saveTwoFa(this,
                    enabled ? TwoFa.markerFor(verifier) : null,
                    enabled ? verifier : null);
            runOnUiThread(() -> {
                twoFaEnabledState = enabled;
                twoFaVerifierState = verifier;
                cacheProfile(snapshot.username, snapshot.bio, snapshot.discoverable);
                updateTwoFaUi();
                Toast.makeText(this, savedCloud
                        ? (enabled ? "Two-step verification is on." : "Two-step verification is off.")
                        : "Saved on this device. Cloud sync is not available yet.",
                        Toast.LENGTH_SHORT).show();
            });
        });
    }

    private void loadProfileFromCloud() {
        final ProfileData fallback = currentProfileData();
        final boolean hadLocalAvatar = avatarFile().exists();
        executor.execute(() -> {
            String token = ensureIdToken();
            if (token == null || userId.length() == 0) {
                return;
            }
            ProfileData data = fallback;
            boolean existed = ProfileClient.loadMyProfile(token, userId, data);
            boolean synced;
            if (!existed) {
                // First open after Firestore/rules were created, or an
                // Auth account that predates the database: upload a local
                // photo first, then create users/profiles/publicIds docs.
                if (hadLocalAvatar) {
                    byte[] jpeg = readAvatarFile();
                    boolean uploaded = jpeg != null
                            && ProfileClient.uploadAvatar(token, userId, jpeg);
                    data.hasAvatar = uploaded;
                    if (uploaded && data.avatarUpdatedAt == 0) {
                        data.avatarUpdatedAt = System.currentTimeMillis();
                    }
                } else {
                    data.hasAvatar = false;
                }
                synced = ProfileClient.ensureUserDocuments(token, userId, data);
            } else {
                // Heal a missing public profile, public ID claim or
                // username directory entry for an existing account.
                synced = ProfileClient.ensureUserDocuments(token, userId, data);
            }
            boolean loaded = existed || synced;
            if (loaded && data.hasAvatar) {
                if (!avatarFile().exists() || data.avatarUpdatedAt != avatarUpdatedAt) {
                    byte[] bytes = ProfileClient.downloadAvatar(token, userId);
                    if (bytes != null) {
                        try {
                            FileOutputStream out = new FileOutputStream(avatarFile());
                            out.write(bytes);
                            out.close();
                        } catch (Exception ignored) {
                        }
                    }
                } else if (ProfileClient.downloadAvatar(token, userId) == null) {
                    // Profile says there is a photo, but its Firestore
                    // avatar document is missing (older install): reupload it.
                    byte[] jpeg = readAvatarFile();
                    if (jpeg != null) {
                        ProfileClient.uploadAvatar(token, userId, jpeg);
                    }
                }
            }
            runOnUiThread(() -> {
                if (data.blocked) {
                    new AlertDialog.Builder(this)
                            .setTitle("Account blocked")
                            .setMessage("This account has been blocked by an administrator.")
                            .setCancelable(false)
                            .setPositiveButton("Sign out", (d, w) -> signOut())
                            .show();
                    return;
                }
                if (loaded) {
                    usernameInput.setText(data.username);
                    bioInput.setText(data.bio);
                    discoverableSwitch.setChecked(data.discoverable);
                    originalUsernameLower = data.discoverable ? data.usernameLower() : "";
                    hasAvatar = data.hasAvatar || avatarFile().exists();
                    avatarUpdatedAt = data.avatarUpdatedAt;
                    if (data.publicId > 0) {
                        publicId = data.publicId;
                    }
                    twoFaEnabledState = data.twoFaEnabled;
                    twoFaVerifierState = data.twoFaVerifier;
                    TokenStore.saveTwoFa(this, TokenStore.twoFaMarker(this),
                            twoFaEnabledState ? twoFaVerifierState : null);
                    showBioState = data.showBio;
                    showAvatarState = data.showAvatar;
                    showIdState = data.showId;
                    verifiedState = data.verified;
                    applyingSettings = true;
                    if (showBioSwitch != null) {
                        showBioSwitch.setChecked(showBioState);
                        showAvatarSwitch.setChecked(showAvatarState);
                        showIdSwitch.setChecked(showIdState);
                    }
                    applyingSettings = false;
                    cacheProfile(data.username, data.bio, data.discoverable);
                    displayAvatar();
                    updateTwoFaUi();
                }
                updateIdDisplay();
                updateAdminUi();
            });
        });
    }

    private void saveProfile() {
        String username = usernameInput.getText().toString().trim();
        if (username.startsWith("@")) {
            username = username.substring(1);
        }
        String bio = bioInput.getText().toString().trim();
        boolean discoverable = discoverableSwitch.isChecked();

        if (username.length() > 0 && !USERNAME.matcher(username).matches()) {
            showProfileStatus("Username must be 3-24 letters, numbers or underscores.");
            return;
        }
        if (bio.length() > 160) {
            showProfileStatus("Bio is too long.");
            return;
        }

        setProfileLoading(true);
        final String finalUsername = username;
        executor.execute(() -> {
            String token = ensureIdToken();
            boolean uploadFailed = false;
            boolean cloudAvatar = hasAvatar && !avatarChanged;
            long cloudAvatarAt = avatarUpdatedAt;
            int claim = ProfileClient.USERNAME_ERROR;
            boolean savedCloud = false;

            if (token != null && userId.length() > 0) {
                if (avatarChanged) {
                    byte[] jpeg = readAvatarFile();
                    if (jpeg != null && ProfileClient.uploadAvatar(token, userId, jpeg)) {
                        cloudAvatar = true;
                    } else {
                        uploadFailed = true;
                        cloudAvatar = false;
                    }
                }
                claim = ProfileClient.claimUsername(token, userId, finalUsername,
                        originalUsernameLower, discoverable, publicId, verifiedState);
                if (claim == ProfileClient.USERNAME_OK) {
                    ProfileData data = new ProfileData();
                    data.email = userEmail;
                    data.username = finalUsername;
                    data.bio = bio;
                    data.discoverable = discoverable;
                    data.publicId = publicId;
                    data.twoFaEnabled = twoFaEnabledState;
                    data.twoFaVerifier = twoFaVerifierState;
                    data.showBio = showBioState;
                    data.showAvatar = showAvatarState;
                    data.showId = showIdState;
                    data.verified = verifiedState;
                    data.hasAvatar = cloudAvatar;
                    data.avatarUpdatedAt = cloudAvatarAt;
                    savedCloud = ProfileClient.saveMyProfile(token, userId, data);
                }
            }

            final boolean finalUploadFailed = uploadFailed;
            final int finalClaim = claim;
            final boolean finalSaved = savedCloud;
            final boolean finalCloudAvatar = cloudAvatar;
            runOnUiThread(() -> {
                setProfileLoading(false);
                if (finalClaim == ProfileClient.USERNAME_TAKEN) {
                    showProfileStatus("This username is already taken.");
                    return;
                }
                hasAvatar = finalCloudAvatar || avatarFile().exists();
                cacheProfile(finalUsername, bio, discoverable);
                originalUsernameLower = discoverable
                        ? finalUsername.toLowerCase(Locale.ROOT) : "";
                if (finalSaved) {
                    if (!finalUploadFailed) {
                        avatarChanged = false;
                    }
                    showProfileStatus(finalUploadFailed
                            ? "Saved. The photo is kept on this device only for now."
                            : "Saved.");
                } else {
                    showProfileStatus("Saved on this device. Cloud sync is not available yet.");
                }
            });
        });
    }

    private void showChangeEmailDialog() {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(24), dp(8), dp(24), dp(4));

        EditText newEmailInput = new EditText(this);
        newEmailInput.setHint("New email");
        newEmailInput.setInputType(InputType.TYPE_CLASS_TEXT
                | InputType.TYPE_TEXT_VARIATION_EMAIL_ADDRESS);
        newEmailInput.setBackgroundResource(R.drawable.bg_input);
        newEmailInput.setPadding(dp(14), 0, dp(14), 0);
        box.addView(newEmailInput, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(52)));

        EditText passwordInput = new EditText(this);
        passwordInput.setHint("Current password");
        passwordInput.setInputType(InputType.TYPE_CLASS_TEXT
                | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        passwordInput.setBackgroundResource(R.drawable.bg_input);
        passwordInput.setPadding(dp(14), 0, dp(14), 0);
        LinearLayout.LayoutParams pwParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(52));
        pwParams.topMargin = dp(12);
        box.addView(passwordInput, pwParams);

        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle("Change email")
                .setView(box)
                .setNegativeButton("Cancel", null)
                .setPositiveButton("Change", null)
                .create();
        dialog.show();
        dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            String newEmail = newEmailInput.getText().toString().trim();
            String currentPassword = passwordInput.getText().toString();
            if (!android.util.Patterns.EMAIL_ADDRESS.matcher(newEmail).matches()) {
                Toast.makeText(this, "Enter a valid email.", Toast.LENGTH_SHORT).show();
                return;
            }
            if (currentPassword.length() == 0) {
                Toast.makeText(this, "Enter your current password.", Toast.LENGTH_SHORT).show();
                return;
            }
            dialog.dismiss();
            changeEmail(newEmail, currentPassword);
        });
    }

    private void changeEmail(String newEmail, String currentPassword) {
        setProfileLoading(true);
        executor.execute(() -> {
            AuthClient.Result recent = AuthClient.signIn(userEmail, currentPassword);
            if (!recent.ok) {
                runOnUiThread(() -> {
                    setProfileLoading(false);
                    showProfileStatus("Current password is incorrect.");
                });
                return;
            }
            AuthClient.Result updated = AuthClient.updateEmail(recent.idToken, newEmail);
            if (!updated.ok) {
                String message = updated.errorCode.contains("EMAIL_EXISTS")
                        ? "An account with this email already exists."
                        : "Could not change email. " + firstError(updated.errorCode);
                runOnUiThread(() -> {
                    setProfileLoading(false);
                    showProfileStatus(message);
                });
                return;
            }
            TokenStore.save(this, updated.email, updated.refreshToken);
            TokenStore.saveUid(this, updated.uid);
            latestIdToken = updated.idToken;
            latestIdTokenAt = System.currentTimeMillis();
            userEmail = updated.email;
            if (updated.uid.length() > 0) {
                userId = updated.uid;
            }
            ProfileData data = new ProfileData();
            data.email = updated.email;
            data.username = usernameInput.getText().toString().trim().replaceFirst("^@", "");
            data.bio = bioInput.getText().toString().trim();
            data.discoverable = discoverableSwitch.isChecked();
            data.publicId = publicId;
            data.twoFaEnabled = twoFaEnabledState;
            data.twoFaVerifier = twoFaVerifierState;
            data.showBio = showBioState;
            data.showAvatar = showAvatarState;
            data.showId = showIdState;
            data.verified = verifiedState;
            data.hasAvatar = hasAvatar;
            data.avatarUpdatedAt = avatarUpdatedAt;
            ProfileClient.saveMyProfile(updated.idToken, userId, data);

            runOnUiThread(() -> {
                setProfileLoading(false);
                emailView.setText(userEmail);
                updateAdminUi();
                idView.setText(userId);
                showProfileStatus("Email changed.");
            });
        });
    }

    private String firstError(String code) {
        if (code == null || code.length() == 0) {
            return "Try again.";
        }
        String first = code.split("\\|")[0].trim();
        return first.length() > 90 ? first.substring(0, 90) : first;
    }

    private void showProfileStatus(String message) {
        profileStatus.setText(message);
        profileStatus.setVisibility(View.VISIBLE);
    }

    private void setProfileLoading(boolean loading) {
        profileProgress.setVisibility(loading ? View.VISIBLE : View.GONE);
        saveButton.setEnabled(!loading);
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
