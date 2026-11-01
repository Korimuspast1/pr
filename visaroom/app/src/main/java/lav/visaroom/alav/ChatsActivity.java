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
import android.text.TextWatcher;
import android.view.Gravity;
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

        TextView accountEmail = findViewById(R.id.account_email);
        accountEmail.setText(userEmail);
        emailView.setText(userEmail);
        idView.setText(userId.length() == 0 ? "Loading..." : userId);

        loadLocalProfile();
        displayAvatar();
        applyChatTextSize();
        loadProfileFromCloud();
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
    }

    private void setupNavigation() {
        findViewById(R.id.nav_chats).setOnClickListener(v -> showPage(0));
        findViewById(R.id.nav_profile).setOnClickListener(v -> showPage(1));
        findViewById(R.id.nav_settings).setOnClickListener(v -> showPage(2));
        showPage(0);
    }

    private void showPage(int index) {
        for (int i = 0; i < pages.length; i++) {
            boolean selected = i == index;
            pages[i].setVisibility(selected ? View.VISIBLE : View.GONE);
            navIndicators[i].setBackgroundColor(
                    selected ? Color.BLACK : Color.TRANSPARENT);
            int color = getColor(selected ? R.color.near_black : R.color.text_secondary);
            navIcons[i].setColorFilter(color, PorterDuff.Mode.SRC_IN);
            navLabels[i].setTextColor(color);
            navLabels[i].setTypeface(Typeface.DEFAULT,
                    selected ? Typeface.BOLD : Typeface.NORMAL);
        }
    }

    private void setupChats() {
        officialChat.setOnClickListener(v ->
                startActivity(new Intent(this, ChatActivity.class)));
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
            if (userId.length() == 0) {
                return;
            }
            ClipboardManager clipboard = (ClipboardManager) getSystemService(CLIPBOARD_SERVICE);
            clipboard.setPrimaryClip(ClipData.newPlainText("visaroom ID", userId));
            Toast.makeText(this, "ID copied", Toast.LENGTH_SHORT).show();
        });
        findViewById(R.id.change_email).setOnClickListener(v -> showChangeEmailDialog());
        findViewById(R.id.profile_sign_out).setOnClickListener(v -> signOut());
    }

    private void setupSettings() {
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
        findViewById(R.id.setting_sign_out).setOnClickListener(v -> signOut());
    }

    private void applyChatTextSize() {
        int sp = AppSettings.textSizeSp(this);
        officialName.setTextSize(sp);
        officialPreview.setTextSize(Math.max(12, sp - 2));
    }

    private void signOut() {
        TokenStore.clear(this);
        profileCache().edit().clear().apply();
        File localAvatar = avatarFile();
        if (localAvatar.exists()) {
            localAvatar.delete();
        }
        Intent intent = new Intent(this, AuthActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        startActivity(intent);
        finish();
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
            noChats.setVisibility(officialMatch ? View.GONE : View.VISIBLE);
            return;
        }
        searchHint.setVisibility(View.GONE);
        pendingGlobalQuery = q;
        globalSearchRunnable = () -> runGlobalSearch(q, officialMatch);
        handler.postDelayed(globalSearchRunnable, 350);
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
                    LinearLayout.LayoutParams.MATCH_PARENT, dp(1)));
            divider.setBackgroundColor(getColor(R.color.border));
            globalResults.addView(divider);
            shown++;
        }
        peopleLabel.setVisibility(shown > 0 ? View.VISIBLE : View.GONE);
        noChats.setVisibility(shown == 0 && !officialMatch ? View.VISIBLE : View.GONE);
    }

    private View makeUserRow(ProfileClient.SearchResult result) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(dp(16), dp(10), dp(16), dp(10));
        row.setBackgroundResource(R.drawable.bg_chat_row);
        row.setClickable(true);
        row.setFocusable(true);
        row.setLayoutParams(new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

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

        TextView name = new TextView(this);
        name.setText("@" + result.username);
        name.setTextColor(getColor(R.color.text_main));
        name.setTextSize(15);
        name.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        texts.addView(name);

        TextView sub = new TextView(this);
        sub.setText("User");
        sub.setTextColor(getColor(R.color.text_secondary));
        sub.setTextSize(13);
        texts.addView(sub);

        row.addView(texts, textParams);
        row.setOnClickListener(v -> showPublicProfile(result.uid, result.username));
        return row;
    }

    private void showPublicProfile(String uid, String username) {
        executor.execute(() -> {
            String token = ensureIdToken();
            ProfileClient.PublicProfile profile = token == null
                    ? new ProfileClient.PublicProfile()
                    : ProfileClient.loadPublicProfile(token, uid);
            byte[] avatar = token != null && profile.hasAvatar
                    ? ProfileClient.downloadAvatar(token, uid) : null;
            runOnUiThread(() -> {
                LinearLayout box = new LinearLayout(this);
                box.setOrientation(LinearLayout.VERTICAL);
                box.setGravity(Gravity.CENTER_HORIZONTAL);
                box.setPadding(dp(24), dp(8), dp(24), dp(8));

                ImageView photo = new ImageView(this);
                Bitmap bmp = avatar == null ? null
                        : BitmapFactory.decodeByteArray(avatar, 0, avatar.length);
                if (bmp != null) {
                    photo.setImageBitmap(circularBitmap(bmp));
                } else {
                    photo.setImageResource(R.drawable.ic_logo);
                }
                box.addView(photo, new LinearLayout.LayoutParams(dp(80), dp(80)));

                TextView name = new TextView(this);
                name.setText("@" + (profile.username.length() > 0
                        ? profile.username : username));
                name.setTextColor(getColor(R.color.text_main));
                name.setTextSize(18);
                name.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
                name.setGravity(Gravity.CENTER);
                LinearLayout.LayoutParams nameParams = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.WRAP_CONTENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT);
                nameParams.topMargin = dp(12);
                box.addView(name, nameParams);

                if (profile.bio.length() > 0) {
                    TextView bio = new TextView(this);
                    bio.setText(profile.bio);
                    bio.setTextColor(getColor(R.color.text_secondary));
                    bio.setTextSize(14);
                    bio.setGravity(Gravity.CENTER);
                    LinearLayout.LayoutParams bioParams = new LinearLayout.LayoutParams(
                            LinearLayout.LayoutParams.WRAP_CONTENT,
                            LinearLayout.LayoutParams.WRAP_CONTENT);
                    bioParams.topMargin = dp(6);
                    box.addView(bio, bioParams);
                }

                TextView id = new TextView(this);
                id.setText("ID: " + uid);
                id.setTextColor(getColor(R.color.text_hint));
                id.setTextSize(11);
                id.setTypeface(Typeface.MONOSPACE);
                id.setGravity(Gravity.CENTER);
                LinearLayout.LayoutParams idParams = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.WRAP_CONTENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT);
                idParams.topMargin = dp(10);
                box.addView(id, idParams);

                TextView note = new TextView(this);
                note.setText("Direct chats are not available yet.");
                note.setTextColor(getColor(R.color.text_secondary));
                note.setTextSize(13);
                note.setGravity(Gravity.CENTER);
                LinearLayout.LayoutParams noteParams = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.WRAP_CONTENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT);
                noteParams.topMargin = dp(14);
                box.addView(note, noteParams);

                new AlertDialog.Builder(this)
                        .setView(box)
                        .setPositiveButton("Close", null)
                        .show();
            });
        });
    }

    // ---------- profile ----------

    private SharedPreferences profileCache() {
        return getSharedPreferences("profile_cache", Context.MODE_PRIVATE);
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
    }

    private void cacheProfile(String username, String bio, boolean discoverable) {
        profileCache().edit()
                .putString("username", username)
                .putString("bio", bio)
                .putBoolean("discoverable", discoverable)
                .putBoolean("has_avatar", hasAvatar)
                .putLong("avatar_at", avatarUpdatedAt)
                .apply();
    }

    private File avatarFile() {
        return new File(getFilesDir(), "avatar.jpg");
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
            Bitmap scaled = Bitmap.createScaledBitmap(square, 512, 512, true);
            FileOutputStream out = new FileOutputStream(avatarFile());
            scaled.compress(Bitmap.CompressFormat.JPEG, 82, out);
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
        TokenStore.save(this, null, result.refreshToken);
        if (result.uid.length() > 0) {
            userId = result.uid;
            TokenStore.saveUid(this, result.uid);
        }
        return latestIdToken;
    }

    private void loadProfileFromCloud() {
        executor.execute(() -> {
            String token = ensureIdToken();
            if (token == null || userId.length() == 0) {
                return;
            }
            ProfileData data = new ProfileData();
            data.email = userEmail;
            boolean loaded = ProfileClient.loadMyProfile(token, userId, data);
            if (loaded && data.hasAvatar
                    && (!avatarFile().exists() || data.avatarUpdatedAt != avatarUpdatedAt)) {
                byte[] bytes = ProfileClient.downloadAvatar(token, userId);
                if (bytes != null) {
                    try {
                        FileOutputStream out = new FileOutputStream(avatarFile());
                        out.write(bytes);
                        out.close();
                    } catch (Exception ignored) {
                    }
                }
            }
            runOnUiThread(() -> {
                idView.setText(userId);
                if (loaded) {
                    usernameInput.setText(data.username);
                    bioInput.setText(data.bio);
                    discoverableSwitch.setChecked(data.discoverable);
                    originalUsernameLower = data.discoverable ? data.usernameLower() : "";
                    hasAvatar = data.hasAvatar || avatarFile().exists();
                    avatarUpdatedAt = data.avatarUpdatedAt;
                    cacheProfile(data.username, data.bio, data.discoverable);
                    displayAvatar();
                }
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
                        originalUsernameLower, discoverable);
                if (claim == ProfileClient.USERNAME_OK) {
                    ProfileData data = new ProfileData();
                    data.email = userEmail;
                    data.username = finalUsername;
                    data.bio = bio;
                    data.discoverable = discoverable;
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
            data.hasAvatar = hasAvatar;
            data.avatarUpdatedAt = avatarUpdatedAt;
            ProfileClient.saveMyProfile(updated.idToken, userId, data);

            runOnUiThread(() -> {
                setProfileLoading(false);
                emailView.setText(userEmail);
                TextView accountEmail = findViewById(R.id.account_email);
                accountEmail.setText(userEmail);
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
