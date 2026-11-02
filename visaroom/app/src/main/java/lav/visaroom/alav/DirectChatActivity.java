package lav.visaroom.alav;

import android.app.Activity;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Path;
import android.graphics.PorterDuff;
import android.graphics.Typeface;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Collections;
import java.util.Date;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * One-to-one VRProto chat. Message history and drafts are kept only in
 * memory while this screen exists; the permanent copy is in Firestore.
 */
public class DirectChatActivity extends Activity {

    public static final String EXTRA_UID = "uid";
    public static final String EXTRA_USERNAME = "username";

    private static final long POLL_MS = 1200L;

    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final List<VrProto.Message> messages = new ArrayList<>();

    private final Runnable pollRunnable = new Runnable() {
        @Override
        public void run() {
            if (!resumed) {
                return;
            }
            syncNow(false);
            handler.postDelayed(this, POLL_MS);
        }
    };

    private ScrollView messagesScroll;
    private LinearLayout messagesContainer;
    private View searchBar;
    private View searchDivider;
    private EditText searchInput;
    private TextView searchCount;
    private ImageView peerAvatar;
    private TextView peerName;
    private ImageView peerVerified;
    private TextView peerSubtitle;
    private EditText messageInput;
    private ImageButton sendButton;

    private String peerUid = "";
    private String fallbackUsername = "";
    private String myUid = "";
    private String latestIdToken;
    private long latestIdTokenAt;
    private ProfileClient.PublicProfile peerProfile;
    private boolean peerBlocked;
    private boolean loading = true;
    private boolean syncFailed;
    private boolean syncing;
    private boolean resumed;
    private boolean firstRender = true;
    private String lastRenderSignature = "";
    private long peerReadAt;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        AuthClient.init(this);
        setContentView(R.layout.activity_direct_chat);

        peerUid = getIntent().getStringExtra(EXTRA_UID);
        if (peerUid == null) {
            peerUid = "";
        }
        fallbackUsername = getIntent().getStringExtra(EXTRA_USERNAME);
        if (fallbackUsername == null) {
            fallbackUsername = "";
        }
        String uid = TokenStore.uid(this);
        myUid = uid == null ? "" : uid;

        messagesScroll = findViewById(R.id.messages_scroll);
        messagesContainer = findViewById(R.id.messages_container);
        searchBar = findViewById(R.id.message_search_bar);
        searchDivider = findViewById(R.id.search_divider);
        searchInput = findViewById(R.id.message_search);
        searchCount = findViewById(R.id.search_count);
        peerAvatar = findViewById(R.id.peer_avatar);
        peerName = findViewById(R.id.peer_name);
        peerVerified = findViewById(R.id.peer_verified);
        peerSubtitle = findViewById(R.id.peer_subtitle);
        messageInput = findViewById(R.id.message_input);
        sendButton = findViewById(R.id.send_button);

        peerAvatar.setColorFilter(Color.BLACK, PorterDuff.Mode.SRC_IN);
        peerName.setText(displayName());
        applyBackground();
        updateComposer();
        renderMessages(true);

        findViewById(R.id.back).setOnClickListener(v -> finishWithAnimation());
        findViewById(R.id.chat_header).setOnClickListener(v -> openPeerProfile());
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
                renderMessages(false);
            }

            @Override
            public void afterTextChanged(Editable s) {
            }
        });

        sendButton.setOnClickListener(v -> sendCurrent());
        messageInput.setOnEditorActionListener((v, actionId, event) -> {
            if (AppSettings.enterToSend(this) && actionId == EditorInfo.IME_ACTION_SEND) {
                sendCurrent();
                return true;
            }
            return false;
        });

        syncNow(true);
    }

    @Override
    protected void onResume() {
        super.onResume();
        resumed = true;
        handler.removeCallbacks(pollRunnable);
        handler.postDelayed(pollRunnable, POLL_MS);
        syncNow(peerProfile == null);
    }

    @Override
    protected void onPause() {
        resumed = false;
        handler.removeCallbacks(pollRunnable);
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        resumed = false;
        handler.removeCallbacksAndMessages(null);
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

    private String displayName() {
        String username = peerProfile != null && peerProfile.username.length() > 0
                ? peerProfile.username : fallbackUsername;
        if (username.length() > 0) {
            return "@" + username;
        }
        if (peerProfile != null && peerProfile.publicId > 0) {
            return "ID " + peerProfile.publicId;
        }
        return "User";
    }

    private String usernameForIntent() {
        if (peerProfile != null && peerProfile.username.length() > 0) {
            return peerProfile.username;
        }
        return fallbackUsername;
    }

    private void openPeerProfile() {
        if (peerUid.length() == 0) {
            return;
        }
        Intent intent = new Intent(this, UserProfileActivity.class);
        intent.putExtra(UserProfileActivity.EXTRA_UID, peerUid);
        intent.putExtra(UserProfileActivity.EXTRA_USERNAME, usernameForIntent());
        startActivity(intent);
        overridePendingTransition(R.anim.slide_in_right, R.anim.slide_out_left);
    }

    private String ensureToken() {
        if (latestIdToken != null
                && System.currentTimeMillis() - latestIdTokenAt < 40 * 60 * 1000L) {
            return latestIdToken;
        }
        String refreshToken = TokenStore.refreshToken(this);
        if (refreshToken == null || refreshToken.length() == 0) {
            return null;
        }
        AuthClient.Result session = AuthClient.refresh(refreshToken);
        if (!session.ok) {
            return null;
        }
        latestIdToken = session.idToken;
        latestIdTokenAt = System.currentTimeMillis();
        String tokenEmail = AuthClient.emailFromIdToken(session.idToken);
        if (tokenEmail.length() > 0) {
            TokenStore.save(this, tokenEmail, session.refreshToken);
        } else {
            TokenStore.save(this, null, session.refreshToken);
        }
        if (session.uid.length() > 0) {
            myUid = session.uid;
            TokenStore.saveUid(this, session.uid);
        }
        return latestIdToken;
    }

    private void syncNow(boolean loadPeer) {
        if (syncing || peerUid.length() == 0 || peerUid.equals(myUid)) {
            return;
        }
        syncing = true;
        final boolean shouldMarkRead = resumed
                && searchBar.getVisibility() != View.VISIBLE
                && AppSettings.readReceipts(this);
        executor.execute(() -> {
            String token = ensureToken();
            ProfileClient.PublicProfile loadedProfile = null;
            byte[] loadedAvatar = null;
            if (token != null && (loadPeer || peerProfile == null)) {
                loadedProfile = ProfileClient.loadPublicProfile(token, peerUid);
                if (loadedProfile.showAvatar && loadedProfile.hasAvatar) {
                    loadedAvatar = ProfileClient.downloadAvatar(token, peerUid);
                }
            }
            VrProto.SyncResult result = token == null
                    ? new VrProto.SyncResult()
                    : VrProto.syncDirect(token, myUid, peerUid);
            if (result.ok && shouldMarkRead && token != null) {
                long latestIncoming = result.ownReadAt;
                for (VrProto.Message m : result.messages) {
                    if (!myUid.equals(m.senderUid)) {
                        latestIncoming = Math.max(latestIncoming, m.createdAt);
                    }
                }
                if (latestIncoming > result.ownReadAt
                        && VrProto.markRead(token, myUid, peerUid, latestIncoming)) {
                    result.ownReadAt = latestIncoming;
                }
            }
            final ProfileClient.PublicProfile finalProfile = loadedProfile;
            final byte[] finalAvatar = loadedAvatar;
            runOnUiThread(() -> {
                syncing = false;
                if (isFinishing()) {
                    return;
                }
                if (finalProfile != null) {
                    peerProfile = finalProfile;
                    peerBlocked = finalProfile.blocked;
                    renderPeer(finalAvatar);
                }
                loading = false;
                if (result.ok) {
                    syncFailed = false;
                    peerReadAt = result.peerReadAt;
                    mergeMessages(result.messages);
                } else {
                    syncFailed = true;
                }
                updateComposer();
                renderMessages(false);
            });
        });
    }

    private void mergeMessages(List<VrProto.Message> serverMessages) {
        Set<String> serverIds = new HashSet<>();
        List<VrProto.Message> merged = new ArrayList<>();
        for (VrProto.Message m : serverMessages) {
            serverIds.add(m.id);
            merged.add(m);
        }
        for (VrProto.Message old : messages) {
            if ((old.pending || old.failed) && !serverIds.contains(old.id)) {
                merged.add(old);
            }
        }
        Collections.sort(merged, (a, b) -> {
            int byTime = Long.compare(a.createdAt, b.createdAt);
            return byTime != 0 ? byTime : a.id.compareTo(b.id);
        });
        messages.clear();
        messages.addAll(merged);
    }

    private void renderPeer(byte[] avatarBytes) {
        peerName.setText(displayName());
        peerVerified.setVisibility(peerProfile != null && peerProfile.verified
                ? View.VISIBLE : View.GONE);
        Bitmap bmp = avatarBytes == null ? null
                : BitmapFactory.decodeByteArray(avatarBytes, 0, avatarBytes.length);
        if (bmp != null) {
            peerAvatar.clearColorFilter();
            peerAvatar.setImageBitmap(circular(bmp));
        } else if (avatarBytes != null || peerProfile == null || !peerProfile.hasAvatar) {
            peerAvatar.setImageResource(R.drawable.ic_nav_profile);
            peerAvatar.setColorFilter(Color.BLACK, PorterDuff.Mode.SRC_IN);
        }
        updateComposer();
    }

    private void updateComposer() {
        boolean canWrite = !peerBlocked && peerUid.length() > 0
                && !peerUid.equals(myUid);
        messageInput.setEnabled(canWrite);
        sendButton.setEnabled(canWrite);
        messageInput.setHint(peerBlocked ? "Account blocked" : "Message");
        peerSubtitle.setText(peerBlocked ? "Account blocked"
                : syncFailed ? "Waiting for cloud sync" : "VRProto cloud chat");
    }

    private void sendCurrent() {
        String text = messageInput.getText().toString().trim();
        if (text.length() == 0 || peerBlocked) {
            return;
        }
        if (text.length() > VrProto.MAX_TEXT) {
            Toast.makeText(this, "Message is too long.", Toast.LENGTH_SHORT).show();
            return;
        }
        long now = System.currentTimeMillis();
        VrProto.Message message = new VrProto.Message();
        message.id = VrProto.newClientId(now);
        message.senderUid = myUid;
        message.text = text;
        message.createdAt = now;
        message.pending = true;
        messages.add(message);
        messageInput.setText("");
        lastRenderSignature = "";
        renderMessages(true);
        submitMessage(message);
    }

    private void submitMessage(VrProto.Message message) {
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = token != null
                    && VrProto.sendText(token, myUid, peerUid, message);
            runOnUiThread(() -> {
                for (VrProto.Message m : messages) {
                    if (m.id.equals(message.id)) {
                        m.pending = false;
                        m.failed = !ok;
                        break;
                    }
                }
                lastRenderSignature = "";
                renderMessages(true);
                if (ok) {
                    syncNow(false);
                } else {
                    Toast.makeText(this,
                            "Message was not sent. Check the connection and Firebase rules.",
                            Toast.LENGTH_SHORT).show();
                }
            });
        });
    }

    private void retryMessage(VrProto.Message message) {
        if (message.pending || peerBlocked) {
            return;
        }
        message.failed = false;
        message.pending = true;
        lastRenderSignature = "";
        renderMessages(true);
        submitMessage(message);
    }

    private void setSearchOpen(boolean open) {
        searchBar.setVisibility(open ? View.VISIBLE : View.GONE);
        searchDivider.setVisibility(open ? View.VISIBLE : View.GONE);
        if (open) {
            searchInput.requestFocus();
            InputMethodManager imm =
                    (InputMethodManager) getSystemService(INPUT_METHOD_SERVICE);
            if (imm != null) {
                imm.showSoftInput(searchInput, InputMethodManager.SHOW_IMPLICIT);
            }
        } else {
            searchInput.clearFocus();
        }
        lastRenderSignature = "";
        renderMessages(true);
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

    private boolean isNearBottom() {
        int max = Math.max(0, messagesContainer.getHeight() - messagesScroll.getHeight());
        return max - messagesScroll.getScrollY() < dp(120);
    }

    private String renderSignature(String query) {
        StringBuilder b = new StringBuilder();
        b.append(loading).append('|').append(syncFailed).append('|')
                .append(peerBlocked).append('|').append(peerReadAt).append('|')
                .append(query).append('|').append(isDark()).append('|').append(messages.size());
        for (VrProto.Message m : messages) {
            b.append('|').append(m.id).append(':').append(m.pending ? 'p' : 's')
                    .append(m.failed ? 'f' : 'o');
        }
        return b.toString();
    }

    private void renderMessages(boolean force) {
        if (messagesContainer == null) {
            return;
        }
        String query = searchInput == null ? ""
                : searchInput.getText().toString().trim().toLowerCase(Locale.ROOT);
        String signature = renderSignature(query);
        if (!force && signature.equals(lastRenderSignature)) {
            return;
        }
        boolean stickToBottom = force || firstRender || isNearBottom();
        lastRenderSignature = signature;
        firstRender = false;
        messagesContainer.removeAllViews();

        List<VrProto.Message> visible = new ArrayList<>();
        for (VrProto.Message m : messages) {
            if (query.length() == 0
                    || m.text.toLowerCase(Locale.ROOT).contains(query)) {
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
        for (VrProto.Message m : visible) {
            messagesContainer.addView(makeMessageRow(m));
        }
        if (stickToBottom && query.length() == 0) {
            messagesScroll.post(() -> messagesScroll.fullScroll(View.FOCUS_DOWN));
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
        if (isDark()) {
            icon.setColorFilter(Color.WHITE, PorterDuff.Mode.SRC_IN);
        }
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
        text.setText(query.length() > 0 ? "Try a different search."
                : peerBlocked ? "This account is blocked."
                : syncFailed ? "Messages are stored in the cloud. Check the connection and Firebase rules."
                : "Write the first message. It will be stored in the cloud, not on this device.");
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

    private View makeMessageRow(VrProto.Message message) {
        boolean mine = myUid.equals(message.senderUid);
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(mine ? Gravity.END : Gravity.START);
        LinearLayout.LayoutParams rowParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        rowParams.bottomMargin = dp(8);
        row.setLayoutParams(rowParams);

        LinearLayout bubble = new LinearLayout(this);
        bubble.setOrientation(LinearLayout.VERTICAL);
        bubble.setPadding(dp(12), dp(9), dp(12), dp(7));
        bubble.setBackgroundResource(mine
                ? (isDark() ? R.drawable.bg_input_normal : R.drawable.bg_button_primary)
                : R.drawable.bg_message);
        bubble.setClickable(message.failed && mine);
        bubble.setFocusable(message.failed && mine);
        if (message.failed && mine) {
            bubble.setOnClickListener(v -> retryMessage(message));
        }
        bubble.setOnLongClickListener(v -> {
            ClipboardManager clipboard =
                    (ClipboardManager) getSystemService(CLIPBOARD_SERVICE);
            if (clipboard != null) {
                clipboard.setPrimaryClip(ClipData.newPlainText("Message", message.text));
                Toast.makeText(this, "Message copied", Toast.LENGTH_SHORT).show();
            }
            return true;
        });

        int maxBubbleWidth = Math.min(
                Math.round(getResources().getDisplayMetrics().widthPixels * 0.78f),
                dp(520));
        TextView text = new TextView(this);
        text.setText(message.text);
        text.setMaxWidth(Math.max(dp(80), maxBubbleWidth - dp(24)));
        text.setTextColor(mine && !isDark() ? Color.WHITE : getColor(R.color.text_main));
        text.setTextSize(AppSettings.textSizeSp(this));
        bubble.addView(text, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

        String meta = metaText(message, mine);
        if (meta.length() > 0) {
            TextView metaView = new TextView(this);
            metaView.setText(meta);
            metaView.setTextColor(mine && !isDark()
                    ? Color.rgb(210, 210, 210) : getColor(R.color.text_secondary));
            metaView.setTextSize(11);
            metaView.setGravity(Gravity.END);
            LinearLayout.LayoutParams metaParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT);
            metaParams.topMargin = dp(4);
            metaParams.gravity = Gravity.END;
            bubble.addView(metaView, metaParams);
        }

        row.addView(bubble, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
        return row;
    }

    private String metaText(VrProto.Message message, boolean mine) {
        StringBuilder out = new StringBuilder();
        if (AppSettings.showTimestamps(this) && message.createdAt > 0) {
            out.append(formatTime(message.createdAt));
        }
        if (mine) {
            String status = message.failed ? "Failed. Tap to retry"
                    : message.pending ? "Sending..."
                    : message.createdAt <= peerReadAt && peerReadAt > 0 ? "Read" : "Sent";
            if (out.length() > 0) {
                out.append("  ");
            }
            out.append(status);
        }
        return out.toString();
    }

    private String formatTime(long millis) {
        Calendar then = Calendar.getInstance();
        then.setTimeInMillis(millis);
        Calendar now = Calendar.getInstance();
        boolean sameDay = then.get(Calendar.YEAR) == now.get(Calendar.YEAR)
                && then.get(Calendar.DAY_OF_YEAR) == now.get(Calendar.DAY_OF_YEAR);
        String pattern = sameDay ? "HH:mm" : "dd.MM HH:mm";
        return new SimpleDateFormat(pattern, Locale.ROOT).format(new Date(millis));
    }

    private Bitmap circular(Bitmap source) {
        int size = Math.min(source.getWidth(), source.getHeight());
        Bitmap square = Bitmap.createBitmap(source,
                (source.getWidth() - size) / 2,
                (source.getHeight() - size) / 2, size, size);
        Bitmap out = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(out);
        Path path = new Path();
        path.addCircle(size / 2f, size / 2f, size / 2f, Path.Direction.CW);
        canvas.clipPath(path);
        canvas.drawBitmap(square, 0, 0, null);
        return out;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
