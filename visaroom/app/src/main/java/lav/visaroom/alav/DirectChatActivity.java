package lav.visaroom.alav;

import android.Manifest;
import android.animation.Animator;
import android.animation.AnimatorListenerAdapter;
import android.animation.ValueAnimator;
import android.app.Activity;
import android.app.AlertDialog;
import android.app.Dialog;
import android.content.Context;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Path;
import android.graphics.PorterDuff;
import android.graphics.Typeface;
import android.graphics.drawable.BitmapDrawable;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.GradientDrawable;
import android.media.MediaDataSource;
import android.media.MediaPlayer;
import android.media.MediaRecorder;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.provider.OpenableColumns;
import android.text.Editable;
import android.text.InputType;
import android.text.Spannable;
import android.text.TextPaint;
import android.text.TextWatcher;
import android.text.style.MetricAffectingSpan;
import android.view.Gravity;
import android.view.HapticFeedbackConstants;
import android.view.MotionEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.view.animation.DecelerateInterpolator;
import android.view.animation.OvershootInterpolator;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Collections;
import java.util.Date;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Locale;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * One-to-one VRProto chat. Message history and drafts are kept only in
 * memory while this screen exists; the permanent Firestore copy of a
 * protocol 2 message is end-to-end encrypted ciphertext.
 */
public class DirectChatActivity extends Activity {

    public static final String EXTRA_UID = "uid";
    public static final String EXTRA_USERNAME = "username";

    private static final long POLL_MS = 700L;
    private static final int REQ_PICK_FILE = 701;
    private static final int REQ_PICK_WALLPAPER = 702;
    private static final int REQ_SAVE_FILE = 703;
    private static final int REQ_RECORD_AUDIO = 704;

    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    // Sending must never queue behind a full history/profile sync.
    private final ExecutorService sendExecutor = Executors.newSingleThreadExecutor();
    private final ExecutorService voiceExecutor = Executors.newSingleThreadExecutor();
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
    private EditText messageInput;
    private View composerBar;
    private View replyBar;
    private TextView replyTitle;
    private TextView replyPreview;
    private View recordingBar;
    private TextView recordingTime;
    private View recordDot;
    private ImageButton sendButton;
    private ImageButton attachButton;
    private ImageButton wallpaperButton;

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
    private boolean chatKnown;
    private volatile E2eeCrypto.Session e2eeSession;
    private volatile E2eeCrypto.PublicBundle pendingPeerBundle;
    private volatile int e2eeOwnState = E2eeManager.STATE_UNAVAILABLE;
    private volatile int e2eePeerState = E2eeManager.PEER_UNAVAILABLE;
    private boolean e2eeKeyChangePromptShown;
    private volatile boolean legacyMigrationAttempted;
    private final Map<String, byte[]> wallpaperKeys = new HashMap<>();
    private String lastRenderSignature = "";
    private String replyTargetId = "";
    private String evaporatingMessageId = "";
    private int pendingInsertStart = -1;
    private int pendingInsertCount;
    private final Set<ValueAnimator> typingAnimators = new HashSet<>();
    private MediaRecorder voiceRecorder;
    private File voiceRecordFile;
    private boolean recordingVoice;
    private long recordingStartedElapsed;
    private long recordingStartedAt;
    private Runnable recordingTicker;
    private MediaPlayer voicePlayer;
    private MediaDataSource voicePlayerSource;
    private String activeVoiceId = "";
    private Runnable voiceTicker;
    private long peerReadAt;
    private VrProto.Message pendingDownload;
    private String activeWallpaperId = "";
    private String activeWallpaperOwnerUid = "";
    private Bitmap activeWallpaperBitmap;
    private final Map<String, Bitmap> wallpaperPreviews = new HashMap<>();
    private final Set<String> loadingWallpapers = new HashSet<>();
    private final Set<String> failedWallpapers = new HashSet<>();
    private final Set<String> loadingFileStatuses = new HashSet<>();
    private final Set<String> failedFileStatuses = new HashSet<>();

    @Override
    protected void attachBaseContext(Context newBase) {
        super.attachBaseContext(ThemeFx.wrap(newBase));
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        AuthClient.init(this);
        setContentView(R.layout.activity_direct_chat);
        PressFx.install(this);

        peerUid = getIntent().getStringExtra(EXTRA_UID);
        if (peerUid == null) {
            peerUid = "";
        }
        fallbackUsername = getIntent().getStringExtra(EXTRA_USERNAME);
        if (fallbackUsername == null) {
            fallbackUsername = "";
        }
        if (!AuthClient.validUid(peerUid)) {
            peerUid = "";
        }
        if (!fallbackUsername.matches("[A-Za-z0-9_]{3,24}")) {
            fallbackUsername = "";
        }
        String uid = TokenStore.uid(this);
        myUid = AuthClient.validUid(uid) ? uid : "";

        messagesScroll = findViewById(R.id.messages_scroll);
        messagesContainer = findViewById(R.id.messages_container);
        searchBar = findViewById(R.id.message_search_bar);
        searchDivider = findViewById(R.id.search_divider);
        searchInput = findViewById(R.id.message_search);
        searchCount = findViewById(R.id.search_count);
        peerAvatar = findViewById(R.id.peer_avatar);
        peerName = findViewById(R.id.peer_name);
        peerVerified = findViewById(R.id.peer_verified);
        messageInput = findViewById(R.id.message_input);
        composerBar = findViewById(R.id.composer_bar);
        replyBar = findViewById(R.id.reply_bar);
        replyTitle = findViewById(R.id.reply_title);
        replyPreview = findViewById(R.id.reply_preview);
        recordingBar = findViewById(R.id.recording_bar);
        recordingTime = findViewById(R.id.Recording_TIME);
        recordDot = findViewById(R.id.record_dot);
        sendButton = findViewById(R.id.send_button);
        attachButton = findViewById(R.id.attach_button);
        wallpaperButton = findViewById(R.id.wallpaper_button);

        peerAvatar.setColorFilter(getColor(R.color.text_main), PorterDuff.Mode.SRC_IN);
        peerName.setText(displayName());
        cleanupStaleVoiceRecordings();
        NotificationFx.startForegroundChecks(this);
        applyBackground();
        updateComposer();
        renderMessages(true);

        findViewById(R.id.back).setOnClickListener(v -> finishWithAnimation());
        findViewById(R.id.chat_header).setOnClickListener(v -> openPeerProfile());
        findViewById(R.id.search_toggle).setOnClickListener(v -> setSearchOpen(true));
        wallpaperButton.setOnClickListener(v -> showWallpaperMenu());
        attachButton.setOnClickListener(v -> pickFile());
        findViewById(R.id.reply_close).setOnClickListener(v -> clearReplyTarget());
        findViewById(R.id.record_cancel).setOnClickListener(v -> cancelVoiceRecording());
        findViewById(R.id.record_send).setOnClickListener(v -> finishVoiceRecording(true));
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

        messageInput.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {
            }

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                pendingInsertStart = count > 0 ? start : -1;
                pendingInsertCount = Math.max(0, count);
                updateSendButtonIcon();
            }

            @Override
            public void afterTextChanged(Editable s) {
                if (pendingInsertStart >= 0 && pendingInsertCount > 0) {
                    applyTypingReveal(s, pendingInsertStart, pendingInsertCount);
                }
                pendingInsertStart = -1;
                pendingInsertCount = 0;
            }
        });
        sendButton.setOnClickListener(v -> {
            if (messageInput.getText().toString().trim().length() == 0) {
                startVoiceFlow();
            } else {
                sendCurrent();
            }
        });
        messageInput.setOnEditorActionListener((v, actionId, event) -> {
            if (AppSettings.enterToSend(this) && actionId == EditorInfo.IME_ACTION_SEND) {
                sendCurrent();
                return true;
            }
            return false;
        });

        updateSendButtonIcon();
        syncNow(true);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode,
                                           String[] permissions,
                                           int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == REQ_RECORD_AUDIO) {
            if (grantResults.length > 0
                    && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                beginVoiceRecording();
            } else {
                Toast.makeText(this,
                        "Microphone access is needed to record voice messages.",
                        Toast.LENGTH_LONG).show();
            }
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        resumed = true;
        NotificationFx.startForegroundChecks(this);
        NotificationFx.setActiveDirectPeer(peerUid);
        NotificationFx.cancelForPeer(this, myUid, peerUid);
        handler.removeCallbacks(pollRunnable);
        handler.postDelayed(pollRunnable, POLL_MS);
        syncNow(peerProfile == null);
    }

    @Override
    protected void onPause() {
        resumed = false;
        NotificationFx.setActiveDirectPeer("");
        pauseVoicePlayback();
        if (recordingVoice) {
            cancelVoiceRecording();
        }
        handler.removeCallbacks(pollRunnable);
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        resumed = false;
        NotificationFx.setActiveDirectPeer("");
        stopVoicePlayback();
        if (recordingVoice) {
            cancelVoiceRecording();
        }
        for (ValueAnimator animator : new ArrayList<>(typingAnimators)) {
            animator.cancel();
        }
        typingAnimators.clear();
        handler.removeCallbacksAndMessages(null);
        executor.shutdownNow();
        sendExecutor.shutdownNow();
        voiceExecutor.shutdownNow();
        super.onDestroy();
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (resultCode != RESULT_OK || data == null || data.getData() == null) {
            if (requestCode == REQ_SAVE_FILE) {
                pendingDownload = null;
            }
            return;
        }
        Uri uri = data.getData();
        if (requestCode == REQ_PICK_FILE) {
            handlePickedFile(uri);
        } else if (requestCode == REQ_PICK_WALLPAPER) {
            prepareWallpaper(uri);
        } else if (requestCode == REQ_SAVE_FILE && pendingDownload != null) {
            VrProto.Message message = pendingDownload;
            pendingDownload = null;
            downloadToUri(message, uri);
        }
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
        String expectedUid = myUid == null || myUid.length() == 0
                ? TokenStore.uid(this) : myUid;
        String token = SessionTokens.token(this, expectedUid);
        if (token != null) {
            String storedUid = TokenStore.uid(this);
            if (AuthClient.validUid(storedUid)) {
                myUid = storedUid;
            }
            latestIdToken = token;
            latestIdTokenAt = System.currentTimeMillis();
        }
        return token;
    }


    /** Background-thread E2EE preparation; safe to call before each sync/send. */
    private E2eeCrypto.Session prepareE2ee(String token) {
        if (token == null || myUid == null || myUid.length() == 0
                || peerUid == null || peerUid.length() == 0) {
            e2eePeerState = E2eeManager.PEER_UNAVAILABLE;
            return null;
        }
        E2eeManager.Preparation own = E2eeManager.prepareOwn(this, token, myUid);
        e2eeOwnState = own.state;
        e2eeSession = own.session;
        if (own.session == null) {
            e2eePeerState = E2eeManager.PEER_UNAVAILABLE;
            pendingPeerBundle = null;
            return null;
        }
        E2eeManager.PeerResult peer = E2eeManager.preparePeer(
                this, token, own.session, peerUid);
        e2eePeerState = peer.state;
        pendingPeerBundle = peer.state == E2eeManager.PEER_KEY_CHANGED
                ? peer.bundle : null;
        if (peer.state == E2eeManager.PEER_KEY_CHANGED) {
            runOnUiThread(this::showKeyChangedDialog);
        }
        return own.session;
    }

    private boolean e2eeReadyForSending() {
        return e2eeSession != null && e2eeSession.usable()
                && e2eePeerState == E2eeManager.PEER_READY;
    }

    private String e2eeBlockedMessage() {
        if (e2eeOwnState == E2eeManager.STATE_NEEDS_RESTORE
                || e2eeOwnState == E2eeManager.STATE_CONFLICT) {
            return getString(R.string.e2ee_locked_message);
        }
        if (e2eePeerState == E2eeManager.PEER_NO_KEYS
                || e2eePeerState == E2eeManager.PEER_INVALID) {
            return getString(R.string.e2ee_peer_needs_update);
        }
        if (e2eePeerState == E2eeManager.PEER_KEY_CHANGED) {
            return getString(R.string.e2ee_key_changed_title);
        }
        return getString(R.string.e2ee_not_ready);
    }

    private boolean requireE2eeReady() {
        if (e2eeReadyForSending()) {
            return true;
        }
        Toast.makeText(this, e2eeBlockedMessage(), Toast.LENGTH_LONG).show();
        syncNow(false);
        return false;
    }

    private void showKeyChangedDialog() {
        E2eeCrypto.PublicBundle bundle = pendingPeerBundle;
        E2eeCrypto.Session session = e2eeSession;
        if (isFinishing() || e2eeKeyChangePromptShown || bundle == null
                || session == null) {
            return;
        }
        e2eeKeyChangePromptShown = true;
        LinearLayout sheet = createGlassSheet();
        Dialog dialog = createGlassDialog(sheet);
        sheet.addView(sheetTitle(getString(R.string.e2ee_key_changed_title)));
        TextView body = new TextView(this);
        body.setText(getString(R.string.e2ee_key_changed_body));
        body.setTextColor(getColor(R.color.text_secondary));
        body.setTextSize(14);
        body.setLineSpacing(0f, 1.25f);
        body.setPadding(dp(6), 0, dp(6), dp(12));
        sheet.addView(body);
        TextView codeTitle = new TextView(this);
        codeTitle.setText(getString(R.string.e2ee_safety_code));
        codeTitle.setTextColor(getColor(R.color.text_secondary));
        codeTitle.setTextSize(12);
        codeTitle.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        codeTitle.setPadding(dp(6), 0, dp(6), dp(5));
        sheet.addView(codeTitle);
        TextView code = new TextView(this);
        code.setText(E2eeManager.safetyCode(session, bundle));
        code.setTextColor(getColor(R.color.text_main));
        code.setTextSize(14);
        code.setTypeface(Typeface.MONOSPACE);
        code.setTextIsSelectable(true);
        code.setPadding(dp(6), 0, dp(6), dp(14));
        sheet.addView(code);
        LinearLayout buttons = new LinearLayout(this);
        buttons.setOrientation(LinearLayout.HORIZONTAL);
        Button cancel = glassButton(getString(R.string.cancel), false);
        Button trust = glassButton(getString(R.string.e2ee_trust_new_key), true);
        cancel.setOnClickListener(v -> {
            e2eeKeyChangePromptShown = false;
            dialog.dismiss();
        });
        trust.setOnClickListener(v -> {
            dialog.dismiss();
            executor.execute(() -> {
                boolean ok = E2eeManager.acceptPeer(this, session, bundle);
                runOnUiThread(() -> {
                    e2eeKeyChangePromptShown = false;
                    if (ok) {
                        pendingPeerBundle = null;
                        e2eePeerState = E2eeManager.PEER_READY;
                        lastRenderSignature = "";
                        syncNow(false);
                    } else {
                        Toast.makeText(this, R.string.e2ee_not_ready,
                                Toast.LENGTH_SHORT).show();
                    }
                });
            });
        });
        LinearLayout.LayoutParams cancelParams = new LinearLayout.LayoutParams(
                0, dp(48), 1f);
        LinearLayout.LayoutParams trustParams = new LinearLayout.LayoutParams(
                0, dp(48), 1f);
        trustParams.leftMargin = dp(10);
        buttons.addView(cancel, cancelParams);
        buttons.addView(trust, trustParams);
        sheet.addView(buttons, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
        dialog.setOnDismissListener(d -> e2eeKeyChangePromptShown = false);
        showGlassDialog(dialog, sheet);
    }

    private void rememberWallpaperKeys(List<VrProto.Message> values) {
        if (values == null) {
            return;
        }
        for (VrProto.Message message : values) {
            if (message != null && VrProto.TYPE_WALLPAPER.equals(message.type)
                    && message.wallpaperKey != null
                    && message.wallpaperKey.length == E2eeCrypto.KEY_BYTES) {
                wallpaperKeys.put(wallpaperKey(message.wallpaperOwnerUid,
                        message.wallpaperId), message.wallpaperKey);
            }
        }
    }

    private void maybeMigrateLegacyTexts(String token,
                                         List<VrProto.Message> serverMessages) {
        if (legacyMigrationAttempted || token == null || serverMessages == null
                || !e2eeReadyForSending()) {
            return;
        }
        boolean hasOwnLegacy = false;
        for (VrProto.Message message : serverMessages) {
            if (message != null && !message.encrypted
                    && message.protocolVersion == VrProto.VERSION
                    && VrProto.TYPE_TEXT.equals(message.type)
                    && myUid.equals(message.senderUid)) {
                hasOwnLegacy = true;
                break;
            }
        }
        if (!hasOwnLegacy) {
            return;
        }
        legacyMigrationAttempted = true;
        VrProto.migrateOwnLegacyTexts(token, myUid, peerUid, serverMessages,
                e2eeSession, System.currentTimeMillis());
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
            // Messages first. The old order loaded the peer profile and
            // avatar before history, which made opening a chat feel
            // slow even though the Activity itself had already opened.
            E2eeCrypto.Session session = token == null ? null : prepareE2ee(token);
            VrProto.SyncResult result = token == null
                    ? new VrProto.SyncResult()
                    : VrProto.syncDirect(token, myUid, peerUid, session);
            if (result.ok) {
                maybeMigrateLegacyTexts(token, result.messages);
            }
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
            runOnUiThread(() -> {
                syncing = false;
                if (isFinishing()) {
                    return;
                }
                loading = false;
                if (result.ok) {
                    syncFailed = false;
                    rememberWallpaperKeys(result.messages);
                    chatKnown = result.exists || chatKnown;
                    peerReadAt = result.peerReadAt;
                    boolean wallpaperChanged =
                            !result.ownWallpaperId.equals(activeWallpaperId)
                                    || !result.ownWallpaperOwnerUid.equals(
                                            activeWallpaperOwnerUid);
                    activeWallpaperId = result.ownWallpaperId;
                    activeWallpaperOwnerUid = result.ownWallpaperOwnerUid;
                    if (wallpaperChanged) {
                        if (activeWallpaperId.length() == 0) {
                            activeWallpaperBitmap = null;
                            applyBackground();
                        } else {
                            activeWallpaperBitmap = wallpaperBitmapFor(
                                    activeWallpaperOwnerUid, activeWallpaperId);
                            applyBackground();
                        }
                    }
                    mergeMessages(result.messages);
                    refreshReplyBar();
                    NotificationFx.cancelForPeer(this, myUid, peerUid);
                    for (VrProto.Message fileMessage : messages) {
                        if (VrProto.TYPE_FILE.equals(fileMessage.type)) {
                            ensureFileStatus(fileMessage);
                        }
                    }
                } else {
                    syncFailed = true;
                }
                updateComposer();
                renderMessages(false);
            });

            if (token != null && (loadPeer || peerProfile == null)) {
                ProfileClient.PublicProfile loadedProfile =
                        ProfileClient.loadPublicProfile(token, peerUid);
                byte[] loadedAvatar = null;
                if (loadedProfile != null && loadedProfile.showAvatar
                        && loadedProfile.hasAvatar) {
                    loadedAvatar = ProfileClient.downloadAvatar(token, peerUid);
                }
                final byte[] finalAvatar = loadedAvatar;
                runOnUiThread(() -> {
                    if (!isFinishing() && loadedProfile != null) {
                        peerProfile = loadedProfile;
                        peerBlocked = loadedProfile.blocked;
                        renderPeer(finalAvatar);
                    }
                });
            }
        });
    }

    private void mergeMessages(List<VrProto.Message> serverMessages) {
        Set<String> serverIds = new HashSet<>();
        List<VrProto.Message> merged = new ArrayList<>();
        Map<String, VrProto.Message> oldById = new HashMap<>();
        for (VrProto.Message old : messages) {
            oldById.put(old.id, old);
        }
        for (VrProto.Message m : serverMessages) {
            serverIds.add(m.id);
            VrProto.Message old = oldById.get(m.id);
            if (old != null) {
                m.localUri = old.localUri;
                m.progress = old.progress;
                m.downloading = old.downloading;
                m.downloadCount = old.downloadCount;
                m.downloadLimit = old.downloadLimit;
                m.senderDownloaded = old.senderDownloaded;
                m.recipientDownloaded = old.recipientDownloaded;
                m.cloudDeleted = old.cloudDeleted;
                m.downloadStatusLoaded = old.downloadStatusLoaded;
                m.downloadStatusCheckedAt = old.downloadStatusCheckedAt;
                m.receiptPending = old.receiptPending;
                m.operationPending = old.operationPending;
                m.deleting = old.deleting;
                m.voiceData = old.voiceData;
                m.voiceLoading = old.voiceLoading;
                m.voicePlaying = old.voicePlaying;
                m.voicePositionMs = old.voicePositionMs;
                if (old.operationPending
                        && VrProto.TYPE_TEXT.equals(old.type)
                        && old.editedAt > m.editedAt) {
                    m.text = old.text;
                    m.editedAt = old.editedAt;
                }
            }
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
                : ImageDecode.decodeBytes(avatarBytes, 256, 256 * 256);
        if (bmp != null) {
            peerAvatar.clearColorFilter();
            peerAvatar.setImageBitmap(circular(bmp));
        } else if (avatarBytes != null || peerProfile == null || !peerProfile.hasAvatar) {
            peerAvatar.setImageResource(R.drawable.ic_nav_profile);
            peerAvatar.setColorFilter(getColor(R.color.text_main), PorterDuff.Mode.SRC_IN);
        }
        updateComposer();
    }

    private void updateComposer() {
        boolean canWrite = !peerBlocked && peerUid.length() > 0
                && !peerUid.equals(myUid);
        messageInput.setEnabled(canWrite);
        sendButton.setEnabled(canWrite);
        attachButton.setEnabled(canWrite);
        wallpaperButton.setEnabled(peerUid.length() > 0 && !peerUid.equals(myUid));
        messageInput.setHint(peerBlocked ? getString(R.string.account_blocked)
                : e2eeOwnState == E2eeManager.STATE_NEEDS_RESTORE
                        || e2eeOwnState == E2eeManager.STATE_CONFLICT
                        ? getString(R.string.e2ee_restore_title)
                : e2eePeerState == E2eeManager.PEER_NO_KEYS
                        ? getString(R.string.e2ee_peer_needs_update)
                : getString(R.string.message));
        updateSendButtonIcon();
    }

    private void updateSendButtonIcon() {
        if (sendButton == null || messageInput == null) {
            return;
        }
        boolean hasText = messageInput.getText().toString().trim().length() > 0;
        sendButton.setImageResource(hasText ? R.drawable.ic_send : R.drawable.ic_mic);
        sendButton.setContentDescription(hasText ? "Send" : "Record voice message");
    }

    /** New glyphs fade/grow in over ~190 ms without changing the text. */
    private void applyTypingReveal(Editable editable, int start, int count) {
        if (editable == null || start < 0 || count <= 0
                || start >= editable.length()) {
            return;
        }
        int safeCount = Math.min(count, editable.length() - start);
        int animated = Math.min(safeCount, 24);
        for (int i = 0; i < animated; i++) {
            TypingRevealSpan span = new TypingRevealSpan();
            int from = start + i;
            editable.setSpan(span, from, from + 1,
                    Spannable.SPAN_EXCLUSIVE_EXCLUSIVE);
            ValueAnimator animator = ValueAnimator.ofFloat(0f, 1f);
            animator.setStartDelay(Math.min(i * 12L, 180L));
            animator.setDuration(190L);
            animator.setInterpolator(new DecelerateInterpolator());
            animator.addUpdateListener(animation -> {
                span.progress = (Float) animation.getAnimatedValue();
                if (messageInput != null) {
                    messageInput.invalidate();
                }
            });
            animator.addListener(new AnimatorListenerAdapter() {
                @Override
                public void onAnimationEnd(Animator animation) {
                    typingAnimators.remove(animator);
                    editable.removeSpan(span);
                    if (messageInput != null) {
                        messageInput.invalidate();
                    }
                }

                @Override
                public void onAnimationCancel(Animator animation) {
                    typingAnimators.remove(animator);
                    editable.removeSpan(span);
                }
            });
            typingAnimators.add(animator);
            animator.start();
        }
    }

    private static final class TypingRevealSpan extends MetricAffectingSpan {
        float progress;

        @Override
        public void updateDrawState(TextPaint paint) {
            float p = Math.max(0f, Math.min(1f, progress));
            paint.setAlpha((int) (70 + 185 * p));
            paint.setTextSize(paint.getTextSize() * (0.90f + 0.10f * p));
        }

        @Override
        public void updateMeasureState(TextPaint paint) {
            // Keep layout stable while only the drawn glyph settles in.
        }
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
        if (!requireE2eeReady()) {
            return;
        }
        long now = System.currentTimeMillis();
        VrProto.Message message = new VrProto.Message();
        message.id = VrProto.newClientId(now);
        message.type = VrProto.TYPE_TEXT;
        message.senderUid = myUid;
        message.text = text;
        message.createdAt = now;
        if (!applyCurrentReply(message, true)) {
            return;
        }
        message.pending = true;
        messages.add(message);
        clearReplyTarget();
        messageInput.setText("");
        lastRenderSignature = "";
        renderMessages(true);
        submitMessage(message);
    }

    private void submitMessage(VrProto.Message message) {
        if (VrProto.TYPE_FILE.equals(message.type)
                || VrProto.TYPE_VOICE.equals(message.type)) {
            submitFileMessage(message);
            return;
        }
        sendExecutor.execute(() -> {
            String token = ensureToken();
            E2eeCrypto.Session session = token == null ? null : prepareE2ee(token);
            boolean ok = token != null && session != null && e2eeReadyForSending()
                    && VrProto.sendText(token, myUid, peerUid, message,
                            chatKnown, session,
                            () -> runOnUiThread(() -> markDelivered(message.id)));
            runOnUiThread(() -> {
                for (VrProto.Message m : messages) {
                    if (m.id.equals(message.id)) {
                        m.pending = false;
                        m.failed = !ok;
                        break;
                    }
                }
                if (ok) {
                    chatKnown = true;
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

    private void markDelivered(String messageId) {
        VrProto.Message current = messageById(messageId);
        if (current != null) {
            current.pending = false;
            current.failed = false;
            current.progress = 100;
            chatKnown = true;
            lastRenderSignature = "";
            renderMessages(true);
        }
    }

    private String senderLabelForUid(String uid) {
        return myUid.equals(uid) ? "You" : displayName();
    }

    private String replySummaryText(String type, String text) {
        String clean = text == null ? "" : text.replace('\n', ' ').trim();
        if (VrProto.TYPE_VOICE.equals(type)) {
            clean = "Voice message";
        } else if (VrProto.TYPE_WALLPAPER.equals(type)) {
            clean = "Wallpaper suggestion";
        } else if (VrProto.TYPE_FILE.equals(type) && clean.length() == 0) {
            clean = "File";
        }
        return clean.length() <= 140 ? clean : clean.substring(0, 137) + "...";
    }

    private void setReplyTarget(VrProto.Message message) {
        if (message == null || message.deleting) {
            return;
        }
        if (message.decryptionFailed) {
            Toast.makeText(this, R.string.e2ee_locked_message,
                    Toast.LENGTH_SHORT).show();
            return;
        }
        if (message.pending || message.failed) {
            Toast.makeText(this,
                    "You can reply after this message has been sent.",
                    Toast.LENGTH_SHORT).show();
            return;
        }
        replyTargetId = message.id;
        refreshReplyBar();
        messageInput.requestFocus();
        InputMethodManager imm =
                (InputMethodManager) getSystemService(INPUT_METHOD_SERVICE);
        if (imm != null) {
            imm.showSoftInput(messageInput, InputMethodManager.SHOW_IMPLICIT);
        }
    }

    private void clearReplyTarget() {
        replyTargetId = "";
        refreshReplyBar();
    }

    private void refreshReplyBar() {
        if (replyBar == null) {
            return;
        }
        VrProto.Message target = replyTargetId.length() == 0
                ? null : messageById(replyTargetId);
        if (target == null) {
            replyTargetId = "";
            replyBar.setVisibility(View.GONE);
            return;
        }
        replyTitle.setText("Reply to " + senderLabelForUid(target.senderUid));
        replyPreview.setText(replySummaryText(target.type, target.text));
        replyBar.setVisibility(recordingVoice ? View.GONE : View.VISIBLE);
    }

    /**
     * Applies the latest server-known snapshot of the selected
     * original. Text returns false when it vanished so the draft is
     * preserved; an already-picked file/recorded voice may proceed
     * as a plain message instead of silently losing the media.
     */
    private boolean applyCurrentReply(VrProto.Message outgoing,
                                      boolean failIfMissing) {
        if (replyTargetId.length() == 0) {
            return true;
        }
        VrProto.Message target = messageById(replyTargetId);
        if (target == null || target.pending || target.failed || target.deleting
                || target.decryptionFailed) {
            clearReplyTarget();
            Toast.makeText(this, failIfMissing
                    ? "The original message is no longer available."
                    : "The original message is unavailable, so this will be sent without a reply.",
                    Toast.LENGTH_LONG).show();
            return !failIfMissing;
        }
        outgoing.createdAt = Math.max(outgoing.createdAt,
                target.createdAt + 1L);
        VrProto.applyReply(outgoing, target);
        return true;
    }

    private void scrollToMessage(String messageId) {
        View target = findViewWithTagRecursive(messagesContainer, messageId);
        if (target == null) {
            Toast.makeText(this, "The original message is not loaded.",
                    Toast.LENGTH_SHORT).show();
            return;
        }
        messagesScroll.post(() -> {
            int y = 0;
            View cursor = target;
            while (cursor != null && cursor != messagesContainer) {
                y += cursor.getTop();
                Object parent = cursor.getParent();
                cursor = parent instanceof View ? (View) parent : null;
            }
            messagesScroll.smoothScrollTo(0, Math.max(0, y - dp(96)));
            target.animate().cancel();
            target.setAlpha(0.55f);
            target.animate().alpha(1f).setDuration(650L)
                    .setInterpolator(new DecelerateInterpolator()).start();
        });
    }

    private View findViewWithTagRecursive(View root, Object tag) {
        if (root == null || tag == null) {
            return null;
        }
        if (tag.equals(root.getTag())) {
            return root;
        }
        if (root instanceof android.view.ViewGroup) {
            android.view.ViewGroup group = (android.view.ViewGroup) root;
            for (int i = 0; i < group.getChildCount(); i++) {
                View found = findViewWithTagRecursive(group.getChildAt(i), tag);
                if (found != null) {
                    return found;
                }
            }
        }
        return null;
    }

    private void retryMessage(VrProto.Message message) {
        if (message.pending || peerBlocked) {
            return;
        }
        if (VrProto.hasReply(message)) {
            VrProto.Message target = messageById(message.replyToId);
            if (target == null || target.pending || target.failed
                    || target.decryptionFailed) {
                Toast.makeText(this,
                        "The original message is no longer available.",
                        Toast.LENGTH_SHORT).show();
                return;
            }
            VrProto.applyReply(message, target);
            message.createdAt = Math.max(message.createdAt,
                    target.createdAt + 1L);
        }
        message.failed = false;
        message.pending = true;
        lastRenderSignature = "";
        renderMessages(true);
        submitMessage(message);
    }

    private void copyMessageText(VrProto.Message message) {
        ClipboardManager clipboard =
                (ClipboardManager) getSystemService(CLIPBOARD_SERVICE);
        if (clipboard != null) {
            String copied = VrProto.TYPE_FILE.equals(message.type)
                    ? message.fileName : message.text;
            if (VrProto.TYPE_VOICE.equals(message.type)) {
                copied = "Voice message";
            }
            clipboard.setPrimaryClip(ClipData.newPlainText("Message", copied));
            Toast.makeText(this, "Copied", Toast.LENGTH_SHORT).show();
        }
    }

    private Dialog createGlassDialog(View content) {
        Dialog dialog = new Dialog(this);
        dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
        dialog.setContentView(content);
        Window window = dialog.getWindow();
        if (window != null) {
            window.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
            window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
            WindowManager.LayoutParams params = window.getAttributes();
            params.dimAmount = 0.28f;
            params.gravity = Gravity.BOTTOM;
            params.y = dp(12);
            window.setAttributes(params);
            int width = Math.max(dp(280),
                    getResources().getDisplayMetrics().widthPixels - dp(24));
            window.setLayout(width, WindowManager.LayoutParams.WRAP_CONTENT);
        }
        return dialog;
    }

    private LinearLayout createGlassSheet() {
        LinearLayout sheet = new LinearLayout(this);
        sheet.setOrientation(LinearLayout.VERTICAL);
        sheet.setBackgroundResource(R.drawable.bg_glass_card);
        sheet.setElevation(dp(14));
        sheet.setPadding(dp(14), dp(14), dp(14), dp(14));
        return sheet;
    }

    private TextView sheetTitle(String text) {
        TextView title = new TextView(this);
        title.setText(text);
        title.setTextColor(getColor(R.color.text_main));
        title.setTextSize(17);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setPadding(dp(6), dp(2), dp(6), dp(10));
        return title;
    }

    private View glassActionRow(int iconRes, String label, Runnable action) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setMinimumHeight(dp(52));
        row.setPadding(dp(12), dp(8), dp(12), dp(8));
        row.setBackgroundResource(R.drawable.bg_chat_row);
        row.setClickable(true);
        row.setFocusable(true);
        ImageView icon = new ImageView(this);
        icon.setImageResource(iconRes);
        icon.setColorFilter(getColor(R.color.text_main), PorterDuff.Mode.SRC_IN);
        row.addView(icon, new LinearLayout.LayoutParams(dp(23), dp(23)));
        TextView text = new TextView(this);
        text.setText(label);
        text.setTextColor(getColor(R.color.text_main));
        text.setTextSize(15);
        LinearLayout.LayoutParams textParams = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
        textParams.leftMargin = dp(13);
        row.addView(text, textParams);
        row.setOnClickListener(v -> action.run());
        PressFx.attach(row);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        params.topMargin = dp(4);
        row.setLayoutParams(params);
        return row;
    }

    private Button glassButton(String label, boolean primary) {
        Button button = new Button(this);
        button.setText(label);
        button.setAllCaps(false);
        button.setTextSize(14);
        button.setTextColor(primary ? getColor(R.color.on_primary)
                : getColor(R.color.text_main));
        button.setBackgroundResource(primary ? R.drawable.bg_button_primary
                : R.drawable.bg_glass_pill);
        PressFx.attach(button);
        return button;
    }

    private void showGlassDialog(Dialog dialog, View content) {
        content.setAlpha(0f);
        content.setTranslationY(dp(24));
        content.setScaleX(0.985f);
        content.setScaleY(0.985f);
        dialog.show();
        PressFx.applyTree(content);
        content.animate().alpha(1f).translationY(0f).scaleX(1f).scaleY(1f)
                .setDuration(240L)
                .setInterpolator(new DecelerateInterpolator())
                .start();
    }

    private void showMessageActions(VrProto.Message message, boolean mine) {
        if (message.deleting || message.id.equals(evaporatingMessageId)) {
            return;
        }
        LinearLayout sheet = createGlassSheet();
        Dialog dialog = createGlassDialog(sheet);
        sheet.addView(sheetTitle("Message"));
        boolean locked = message.decryptionFailed;
        if (!locked) {
            sheet.addView(glassActionRow(R.drawable.ic_reply, "Reply", () -> {
                dialog.dismiss();
                setReplyTarget(message);
            }));
        }
        if (VrProto.TYPE_VOICE.equals(message.type)) {
            boolean playing = message.id.equals(activeVoiceId)
                    && voicePlayer != null && voicePlayer.isPlaying();
            sheet.addView(glassActionRow(playing ? R.drawable.ic_pause
                    : R.drawable.ic_play, playing ? "Pause" : "Play", () -> {
                dialog.dismiss();
                toggleVoicePlayback(message);
            }));
        } else if (!locked) {
            sheet.addView(glassActionRow(R.drawable.ic_copy, "Copy", () -> {
                dialog.dismiss();
                copyMessageText(message);
            }));
        }
        if (VrProto.TYPE_FILE.equals(message.type) && !message.pending
                && !message.failed && !message.downloading) {
            sheet.addView(glassActionRow(R.drawable.ic_download, "Download", () -> {
                dialog.dismiss();
                requestDownload(message);
            }));
        }
        boolean canEdit = mine && VrProto.TYPE_TEXT.equals(message.type)
                && !locked && !message.pending && !message.operationPending
                && !message.deleting;
        boolean canDelete = mine && !message.pending && !message.downloading
                && !message.operationPending && !message.deleting;
        if (canEdit) {
            sheet.addView(glassActionRow(R.drawable.ic_edit, "Edit", () -> {
                dialog.dismiss();
                showEditMessageDialog(message);
            }));
        }
        if (canDelete) {
            sheet.addView(glassActionRow(R.drawable.ic_delete, "Delete", () -> {
                dialog.dismiss();
                confirmDeleteMessage(message);
            }));
        }
        View spacer = new View(this);
        sheet.addView(spacer, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(8)));
        sheet.addView(glassActionRow(R.drawable.ic_close, "Cancel", dialog::dismiss));
        showGlassDialog(dialog, sheet);
    }

    private void showEditMessageDialog(VrProto.Message message) {
        VrProto.Message current = messageById(message.id);
        if (current == null) {
            return;
        }
        LinearLayout sheet = createGlassSheet();
        Dialog dialog = createGlassDialog(sheet);
        sheet.addView(sheetTitle("Edit message"));
        EditText input = new EditText(this);
        input.setText(current.text);
        input.setSelection(input.getText().length());
        input.setMinLines(3);
        input.setMaxLines(6);
        input.setInputType(InputType.TYPE_CLASS_TEXT
                | InputType.TYPE_TEXT_FLAG_MULTI_LINE
                | InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);
        input.setBackgroundResource(R.drawable.bg_input);
        input.setTextColor(getColor(R.color.text_main));
        input.setTextColorHint(getColor(R.color.text_hint));
        input.setPadding(dp(14), dp(10), dp(14), dp(10));
        sheet.addView(input, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
        LinearLayout buttons = new LinearLayout(this);
        buttons.setOrientation(LinearLayout.HORIZONTAL);
        LinearLayout.LayoutParams buttonsParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(48));
        buttonsParams.topMargin = dp(12);
        Button cancel = glassButton("Cancel", false);
        Button save = glassButton("Save", true);
        cancel.setOnClickListener(v -> dialog.dismiss());
        save.setOnClickListener(v -> {
            String newText = input.getText().toString().trim();
            if (newText.length() == 0) {
                Toast.makeText(this, "Message cannot be empty.",
                        Toast.LENGTH_SHORT).show();
                return;
            }
            if (newText.length() > VrProto.MAX_TEXT) {
                Toast.makeText(this, "Message is too long.",
                        Toast.LENGTH_SHORT).show();
                return;
            }
            dialog.dismiss();
            if (!newText.equals(current.text)) {
                submitEditMessage(current.id, newText);
            }
        });
        LinearLayout.LayoutParams cancelParams = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.MATCH_PARENT, 1f);
        LinearLayout.LayoutParams saveParams = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.MATCH_PARENT, 1f);
        saveParams.leftMargin = dp(10);
        buttons.addView(cancel, cancelParams);
        buttons.addView(save, saveParams);
        sheet.addView(buttons, buttonsParams);
        showGlassDialog(dialog, sheet);
        input.postDelayed(() -> {
            input.requestFocus();
            InputMethodManager imm = (InputMethodManager)
                    getSystemService(INPUT_METHOD_SERVICE);
            if (imm != null) {
                imm.showSoftInput(input, InputMethodManager.SHOW_IMPLICIT);
            }
        }, 80L);
    }

    private void submitEditMessage(String messageId, String newText) {
        VrProto.Message current = messageById(messageId);
        if (current == null || current.operationPending) {
            return;
        }
        if (current.failed) {
            current.text = newText;
            current.editedAt = 0;
            lastRenderSignature = "";
            renderMessages(true);
            return;
        }
        String originalText = current.text;
        long originalEditedAt = current.editedAt;
        long editedAt = System.currentTimeMillis();
        current.text = newText;
        current.editedAt = editedAt;
        current.operationPending = true;
        lastRenderSignature = "";
        renderMessages(true);
        VrProto.Message operationMessage = current;
        sendExecutor.execute(() -> {
            String token = ensureToken();
            E2eeCrypto.Session session = token == null ? null : prepareE2ee(token);
            boolean ok = token != null && session != null && e2eeReadyForSending()
                    && VrProto.editText(token, myUid, peerUid, operationMessage,
                            newText, editedAt, session);
            runOnUiThread(() -> {
                VrProto.Message latest = messageById(messageId);
                if (latest != null) {
                    latest.operationPending = false;
                    if (ok) {
                        latest.text = newText;
                        latest.editedAt = editedAt;
                    } else {
                        latest.text = originalText;
                        latest.editedAt = originalEditedAt;
                    }
                }
                lastRenderSignature = "";
                renderMessages(true);
                if (ok) {
                    syncNow(false);
                } else {
                    Toast.makeText(this,
                            "Message was not edited. Check the connection and Firebase rules.",
                            Toast.LENGTH_LONG).show();
                }
            });
        });
    }

    private void confirmDeleteMessage(VrProto.Message message) {
        String text = message.failed
                ? "Remove this unsent message from this screen?"
                : VrProto.TYPE_VOICE.equals(message.type)
                        ? "Delete this voice message for both participants? Its cloud copy will also be deleted."
                : VrProto.TYPE_FILE.equals(message.type) || VrProto.hasAttachment(message)
                        ? "Delete this file for both participants? Its cloud copy will also be deleted."
                        : "Delete this message for both participants?";
        LinearLayout sheet = createGlassSheet();
        Dialog dialog = createGlassDialog(sheet);
        sheet.addView(sheetTitle("Delete message?"));
        TextView body = new TextView(this);
        body.setText(text);
        body.setTextColor(getColor(R.color.text_secondary));
        body.setTextSize(14);
        body.setLineSpacing(0f, 1.25f);
        body.setPadding(dp(6), 0, dp(6), dp(14));
        sheet.addView(body);
        LinearLayout buttons = new LinearLayout(this);
        buttons.setOrientation(LinearLayout.HORIZONTAL);
        Button cancel = glassButton("Cancel", false);
        Button delete = glassButton("Delete", true);
        cancel.setOnClickListener(v -> dialog.dismiss());
        delete.setOnClickListener(v -> {
            dialog.dismiss();
            submitDeleteMessage(message.id);
        });
        LinearLayout.LayoutParams cancelParams = new LinearLayout.LayoutParams(
                0, dp(48), 1f);
        LinearLayout.LayoutParams deleteParams = new LinearLayout.LayoutParams(
                0, dp(48), 1f);
        deleteParams.leftMargin = dp(10);
        buttons.addView(cancel, cancelParams);
        buttons.addView(delete, deleteParams);
        sheet.addView(buttons, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
        showGlassDialog(dialog, sheet);
    }

    private void submitDeleteMessage(String messageId) {
        VrProto.Message current = messageById(messageId);
        if (current == null || current.operationPending
                || evaporatingMessageId.length() > 0) {
            return;
        }
        evaporatingMessageId = messageId;
        animateEvaporation(current, () -> {
            evaporatingMessageId = "";
            VrProto.Message latest = messageById(messageId);
            if (latest == null) {
                lastRenderSignature = "";
                renderMessages(true);
                return;
            }
            if (latest.failed) {
                messages.remove(latest);
                lastRenderSignature = "";
                renderMessages(true);
            } else {
                submitCloudDeleteMessage(latest);
            }
        });
    }

    private void submitCloudDeleteMessage(VrProto.Message current) {
        String messageId = current.id;
        current.operationPending = true;
        current.deleting = true;
        lastRenderSignature = "";
        renderMessages(true);
        long operationAt = System.currentTimeMillis();
        VrProto.Message operationMessage = current;
        sendExecutor.execute(() -> {
            String token = ensureToken();
            E2eeCrypto.Session session = token == null ? null : prepareE2ee(token);
            boolean ok = token != null && VrProto.deleteMessage(
                    token, myUid, peerUid, operationMessage, operationAt, session);
            runOnUiThread(() -> {
                VrProto.Message latest = messageById(messageId);
                if (ok) {
                    if (latest != null) {
                        messages.remove(latest);
                    }
                    if (messageId.equals(replyTargetId)) {
                        clearReplyTarget();
                    }
                } else if (latest != null) {
                    latest.operationPending = false;
                    latest.deleting = false;
                }
                lastRenderSignature = "";
                renderMessages(true);
                if (ok) {
                    syncNow(false);
                } else {
                    Toast.makeText(this,
                            "Message was not deleted. Check the connection and Firebase rules.",
                            Toast.LENGTH_LONG).show();
                }
            });
        });
    }

    /**
     * Evaporation deletion: the bubble drifts upward, dissolves and
     * releases a short trail of small grayscale particles. The cloud
     * deletion starts only after the local effect completes; if the
     * network delete fails, the normal render restores the bubble.
     */
    private void animateEvaporation(VrProto.Message message, Runnable done) {
        View bubble = findViewWithTagRecursive(messagesContainer, message.id);
        if (bubble == null || bubble.getWidth() <= 0 || bubble.getHeight() <= 0) {
            done.run();
            return;
        }
        bubble.animate().cancel();
        bubble.setLayerType(View.LAYER_TYPE_HARDWARE, null);
        List<View> particles = new ArrayList<>();
        try {
            android.view.ViewGroup decor = (android.view.ViewGroup)
                    getWindow().getDecorView();
            int[] bubbleLocation = new int[2];
            int[] decorLocation = new int[2];
            bubble.getLocationInWindow(bubbleLocation);
            decor.getLocationInWindow(decorLocation);
            int left = bubbleLocation[0] - decorLocation[0];
            int top = bubbleLocation[1] - decorLocation[1];
            boolean mine = myUid.equals(message.senderUid);
            int baseColor = getColor(R.color.text_main);
            for (int i = 0; i < 18; i++) {
                View particle = new View(this);
                int size = dp(2 + (int) (Math.random() * 4.0));
                GradientDrawable shape = new GradientDrawable();
                shape.setShape(GradientDrawable.OVAL);
                int alpha = 95 + (int) (Math.random() * 110.0);
                shape.setColor(Color.argb(alpha, Color.red(baseColor),
                        Color.green(baseColor), Color.blue(baseColor)));
                particle.setBackground(shape);
                FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(
                        size, size);
                params.leftMargin = left + dp(6)
                        + (int) (Math.random() * Math.max(1,
                                bubble.getWidth() - dp(12)));
                params.topMargin = top + dp(5)
                        + (int) (Math.random() * Math.max(1,
                                bubble.getHeight() - dp(10)));
                decor.addView(particle, params);
                particles.add(particle);
                float driftX = (float) ((Math.random() - 0.5) * dp(44))
                        + (mine ? -dp(7) : dp(7));
                particle.animate()
                        .translationX(driftX)
                        .translationY(-dp(30 + (int) (Math.random() * 58.0)))
                        .alpha(0f)
                        .scaleX(0.25f)
                        .scaleY(0.25f)
                        .setStartDelay((long) (Math.random() * 170.0))
                        .setDuration(520L + (long) (Math.random() * 330.0))
                        .setInterpolator(new DecelerateInterpolator())
                        .start();
            }
        } catch (Exception ignored) {
            // The bubble fade below remains a graceful fallback.
        }
        boolean[] finished = new boolean[]{false};
        Runnable finish = () -> {
            if (finished[0]) {
                return;
            }
            finished[0] = true;
            for (View particle : particles) {
                Object parent = particle.getParent();
                if (parent instanceof android.view.ViewGroup) {
                    ((android.view.ViewGroup) parent).removeView(particle);
                }
            }
            done.run();
        };
        bubble.animate()
                .alpha(0f)
                .translationY(-dp(30))
                .translationX(myUid.equals(message.senderUid) ? dp(5) : -dp(5))
                .scaleX(0.94f)
                .scaleY(0.88f)
                .rotation(myUid.equals(message.senderUid) ? 0.45f : -0.45f)
                .setDuration(560L)
                .setInterpolator(new DecelerateInterpolator(1.6f))
                .withEndAction(finish)
                .start();
        handler.postDelayed(finish, 760L);
    }

    private void pickFile() {
        if (peerBlocked || !requireE2eeReady()) {
            return;
        }
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");
        startActivityForResult(intent, REQ_PICK_FILE);
    }

    private void handlePickedFile(Uri uri) {
        String cleanName = VrProto.safeFileName(queryFileValue(uri, true));
        String name = cleanName.length() > 0 ? cleanName : "file";
        String cleanMime = VrProto.safeMimeType(getContentResolver().getType(uri));
        String mime = cleanMime.length() > 0
                ? cleanMime : "application/octet-stream";
        long queriedSize = queryFileSize(uri);
        String finalMime = mime;
        executor.execute(() -> {
            long size = queriedSize >= 0 ? queriedSize : countStream(uri);
            runOnUiThread(() -> {
                if (size < 0) {
                    Toast.makeText(this, "Could not read this file.",
                            Toast.LENGTH_SHORT).show();
                    return;
                }
                if (size > VrProto.MAX_FILE_BYTES) {
                    Toast.makeText(this, "File is too large. Maximum is 25 MB.",
                            Toast.LENGTH_LONG).show();
                    return;
                }
                long now = System.currentTimeMillis();
                VrProto.Message message = new VrProto.Message();
                message.id = VrProto.newClientId(now);
                message.type = VrProto.TYPE_FILE;
                message.senderUid = myUid;
                message.fileId = message.id;
                message.fileName = name;
                message.text = name;
                message.mimeType = finalMime;
                message.fileSize = size;
                message.chunkSize = VrProto.FILE_CHUNK_BYTES;
                message.chunkCount = VrProto.chunkCount(size, message.chunkSize);
                message.localUri = uri.toString();
                message.downloadCount = 0;
                message.downloadLimit = VrProto.FILE_DOWNLOAD_LIMIT;
                message.downloadStatusLoaded = true;
                message.downloadStatusCheckedAt = now;
                message.createdAt = now;
                applyCurrentReply(message, false);
                message.pending = true;
                messages.add(message);
                clearReplyTarget();
                lastRenderSignature = "";
                renderMessages(true);
                submitFileMessage(message);
            });
        });
    }

    private String queryFileValue(Uri uri, boolean displayName) {
        Cursor cursor = null;
        try {
            cursor = getContentResolver().query(uri,
                    new String[]{OpenableColumns.DISPLAY_NAME}, null, null, null);
            if (cursor != null && cursor.moveToFirst()) {
                int index = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (index >= 0 && displayName) {
                    return cursor.getString(index);
                }
            }
        } catch (Exception ignored) {
        } finally {
            if (cursor != null) {
                cursor.close();
            }
        }
        String last = uri.getLastPathSegment();
        return last == null ? "file" : last;
    }

    private long queryFileSize(Uri uri) {
        Cursor cursor = null;
        try {
            cursor = getContentResolver().query(uri,
                    new String[]{OpenableColumns.SIZE}, null, null, null);
            if (cursor != null && cursor.moveToFirst()) {
                int index = cursor.getColumnIndex(OpenableColumns.SIZE);
                if (index >= 0 && !cursor.isNull(index)) {
                    return cursor.getLong(index);
                }
            }
        } catch (Exception ignored) {
        } finally {
            if (cursor != null) {
                cursor.close();
            }
        }
        return -1;
    }

    private long countStream(Uri uri) {
        InputStream input = null;
        try {
            input = getContentResolver().openInputStream(uri);
            if (input == null) {
                return -1;
            }
            byte[] buffer = new byte[16384];
            long total = 0;
            int read;
            while ((read = input.read(buffer)) != -1) {
                total += read;
                if (total > VrProto.MAX_FILE_BYTES) {
                    return total;
                }
            }
            return total;
        } catch (Exception ignored) {
            return -1;
        } finally {
            if (input != null) {
                try {
                    input.close();
                } catch (Exception ignored) {
                }
            }
        }
    }

    private void updateProgressById(String id, int percent, boolean downloading) {
        for (VrProto.Message m : messages) {
            if (m.id.equals(id)) {
                m.progress = percent;
                m.downloading = downloading;
                break;
            }
        }
        lastRenderSignature = "";
        renderMessages(true);
    }

    private void submitFileMessage(VrProto.Message message) {
        if (VrProto.TYPE_VOICE.equals(message.type)) {
            if (message.voiceData == null || message.voiceData.length == 0) {
                message.pending = false;
                message.failed = true;
                renderMessages(true);
                return;
            }
            byte[] bytes = message.voiceData;
            sendExecutor.execute(() -> {
                String token = ensureToken();
                E2eeCrypto.Session session = token == null ? null : prepareE2ee(token);
                boolean ok = token != null && session != null && e2eeReadyForSending()
                        && VrProto.sendVoice(
                        token, myUid, peerUid, message,
                        () -> new ByteArrayInputStream(bytes),
                        percent -> runOnUiThread(
                                () -> updateProgressById(message.id, percent, false)),
                        chatKnown, session,
                        () -> runOnUiThread(() -> markDelivered(message.id)));
                runOnUiThread(() -> {
                    VrProto.Message current = messageById(message.id);
                    if (current != null) {
                        current.pending = false;
                        current.failed = !ok;
                        current.progress = ok ? 100 : current.progress;
                    }
                    if (ok) {
                        chatKnown = true;
                    }
                    lastRenderSignature = "";
                    renderMessages(true);
                    if (ok) {
                        syncNow(false);
                    } else {
                        Toast.makeText(this,
                                "Voice message was not sent. Check the connection and Firebase rules.",
                                Toast.LENGTH_LONG).show();
                    }
                });
            });
            return;
        }
        if (message.localUri.length() == 0) {
            message.pending = false;
            message.failed = true;
            renderMessages(true);
            return;
        }
        Uri uri = Uri.parse(message.localUri);
        sendExecutor.execute(() -> {
            String token = ensureToken();
            E2eeCrypto.Session session = token == null ? null : prepareE2ee(token);
            boolean ok = token != null && session != null && e2eeReadyForSending()
                    && VrProto.sendFile(token, myUid, peerUid,
                    message,
                    () -> getContentResolver().openInputStream(uri),
                    percent -> runOnUiThread(
                            () -> updateProgressById(message.id, percent, false)),
                    chatKnown, session,
                    () -> runOnUiThread(() -> markDelivered(message.id)));
            runOnUiThread(() -> {
                for (VrProto.Message m : messages) {
                    if (m.id.equals(message.id)) {
                        m.pending = false;
                        m.failed = !ok;
                        m.progress = ok ? 100 : m.progress;
                        break;
                    }
                }
                if (ok) {
                    chatKnown = true;
                }
                lastRenderSignature = "";
                renderMessages(true);
                if (ok) {
                    syncNow(false);
                } else {
                    Toast.makeText(this,
                            "File was not sent. Check the connection and Firebase rules.",
                            Toast.LENGTH_LONG).show();
                }
            });
        });
    }

    private void cleanupStaleVoiceRecordings() {
        try {
            File[] files = getCacheDir().listFiles();
            if (files == null) {
                return;
            }
            for (File file : files) {
                if (file.getName().startsWith("vr_voice_")
                        && file.getName().endsWith(".m4a")) {
                    file.delete();
                }
            }
        } catch (Exception ignored) {
        }
    }

    private void startVoiceFlow() {
        if (peerBlocked || recordingVoice || !requireE2eeReady()) {
            return;
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M
                && checkSelfPermission(Manifest.permission.RECORD_AUDIO)
                        != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(new String[]{Manifest.permission.RECORD_AUDIO},
                    REQ_RECORD_AUDIO);
            return;
        }
        beginVoiceRecording();
    }

    private void beginVoiceRecording() {
        if (recordingVoice || peerBlocked) {
            return;
        }
        stopVoicePlayback();
        cleanupStaleVoiceRecordings();
        long now = System.currentTimeMillis();
        voiceRecordFile = new File(getCacheDir(),
                "vr_voice_" + now + "_" + Math.abs((int) (Math.random() * 1000000.0))
                        + ".m4a");
        MediaRecorder recorder = new MediaRecorder();
        try {
            recorder.setAudioSource(MediaRecorder.AudioSource.MIC);
            recorder.setOutputFormat(MediaRecorder.OutputFormat.MPEG_4);
            recorder.setAudioEncoder(MediaRecorder.AudioEncoder.AAC);
            recorder.setAudioChannels(1);
            recorder.setAudioSamplingRate(44100);
            recorder.setAudioEncodingBitRate(64000);
            recorder.setMaxDuration(VrProto.MAX_VOICE_DURATION_MS);
            recorder.setMaxFileSize(VrProto.MAX_VOICE_BYTES);
            recorder.setOutputFile(voiceRecordFile.getAbsolutePath());
            recorder.setOnInfoListener((mr, what, extra) -> {
                if (what == MediaRecorder.MEDIA_RECORDER_INFO_MAX_DURATION_REACHED
                        || what == MediaRecorder.MEDIA_RECORDER_INFO_MAX_FILESIZE_REACHED) {
                    handler.post(() -> finishVoiceRecording(true));
                }
            });
            recorder.prepare();
            recorder.start();
        } catch (Exception ignored) {
            recorder.reset();
            recorder.release();
            deleteRecordingFile();
            Toast.makeText(this, "Could not start recording.",
                    Toast.LENGTH_SHORT).show();
            return;
        }
        voiceRecorder = recorder;
        recordingVoice = true;
        recordingStartedElapsed = SystemClock.elapsedRealtime();
        recordingStartedAt = now;
        composerBar.setVisibility(View.GONE);
        replyBar.setVisibility(View.GONE);
        recordingBar.setVisibility(View.VISIBLE);
        recordingTime.setText("0:00");
        recordingTicker = new Runnable() {
            @Override
            public void run() {
                if (!recordingVoice) {
                    return;
                }
                long elapsed = SystemClock.elapsedRealtime()
                        - recordingStartedElapsed;
                recordingTime.setText(formatVoiceDuration(
                        (int) Math.min(elapsed, VrProto.MAX_VOICE_DURATION_MS)));
                float pulse = 0.55f + 0.45f * (float) Math.abs(
                        Math.sin(elapsed / 360.0));
                recordDot.setAlpha(pulse);
                handler.postDelayed(this, 100L);
            }
        };
        handler.post(recordingTicker);
        messageInput.clearFocus();
    }

    private void cancelVoiceRecording() {
        stopRecorderQuietly();
        deleteRecordingFile();
        finishRecordingUi();
    }

    private void finishVoiceRecording(boolean send) {
        if (!recordingVoice) {
            return;
        }
        long elapsed = SystemClock.elapsedRealtime() - recordingStartedElapsed;
        File recorded = voiceRecordFile;
        stopRecorderQuietly();
        voiceRecordFile = recorded;
        finishRecordingUi();
        if (!send) {
            deleteRecordingFile();
            return;
        }
        if (elapsed < 700L) {
            deleteRecordingFile();
            Toast.makeText(this, "Recording is too short.",
                    Toast.LENGTH_SHORT).show();
            return;
        }
        int durationMs = (int) Math.min(elapsed,
                VrProto.MAX_VOICE_DURATION_MS);
        voiceExecutor.execute(() -> {
            byte[] bytes = readAndDeleteRecording(recorded);
            runOnUiThread(() -> {
                if (bytes == null || bytes.length == 0
                        || bytes.length > VrProto.MAX_VOICE_BYTES) {
                    Toast.makeText(this, bytes != null
                            && bytes.length > VrProto.MAX_VOICE_BYTES
                            ? "Voice message is too large."
                            : "Could not read this recording.",
                            Toast.LENGTH_LONG).show();
                    return;
                }
                long createdAt = Math.max(recordingStartedAt,
                        System.currentTimeMillis());
                VrProto.Message message = new VrProto.Message();
                message.id = VrProto.newClientId(createdAt);
                message.type = VrProto.TYPE_VOICE;
                message.senderUid = myUid;
                message.text = VrProto.VOICE_TEXT;
                message.fileId = message.id;
                message.fileName = VrProto.VOICE_FILE_NAME;
                message.mimeType = VrProto.VOICE_MIME_TYPE;
                message.fileSize = bytes.length;
                message.chunkSize = VrProto.FILE_CHUNK_BYTES;
                message.chunkCount = VrProto.chunkCount(
                        bytes.length, message.chunkSize);
                message.durationMs = durationMs;
                message.voiceData = bytes;
                message.createdAt = createdAt;
                applyCurrentReply(message, false);
                message.pending = true;
                messages.add(message);
                clearReplyTarget();
                lastRenderSignature = "";
                renderMessages(true);
                submitFileMessage(message);
            });
        });
    }

    private void stopRecorderQuietly() {
        if (recordingTicker != null) {
            handler.removeCallbacks(recordingTicker);
            recordingTicker = null;
        }
        recordingVoice = false;
        if (voiceRecorder != null) {
            try {
                voiceRecorder.stop();
            } catch (Exception ignored) {
            }
            try {
                voiceRecorder.reset();
            } catch (Exception ignored) {
            }
            voiceRecorder.release();
            voiceRecorder = null;
        }
    }

    private void finishRecordingUi() {
        recordingBar.setVisibility(View.GONE);
        composerBar.setVisibility(View.VISIBLE);
        recordDot.setAlpha(1f);
        refreshReplyBar();
        updateSendButtonIcon();
    }

    private void deleteRecordingFile() {
        if (voiceRecordFile != null) {
            voiceRecordFile.delete();
            voiceRecordFile = null;
        }
    }

    private byte[] readAndDeleteRecording(File recorded) {
        InputStream input = null;
        try {
            if (recorded == null || !recorded.isFile()) {
                return null;
            }
            input = new FileInputStream(recorded);
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buffer = new byte[16384];
            int read;
            while ((read = input.read(buffer)) != -1) {
                out.write(buffer, 0, read);
                if (out.size() > VrProto.MAX_VOICE_BYTES) {
                    break;
                }
            }
            return out.toByteArray();
        } catch (Exception ignored) {
            return null;
        } finally {
            if (input != null) {
                try {
                    input.close();
                } catch (Exception ignored) {
                }
            }
            if (recorded != null) {
                recorded.delete();
            }
            if (recorded != null && recorded.equals(voiceRecordFile)) {
                voiceRecordFile = null;
            }
        }
    }

    private String formatVoiceDuration(int durationMs) {
        int totalSeconds = Math.max(0, durationMs) / 1000;
        return String.format(Locale.ROOT, "%d:%02d",
                totalSeconds / 60, totalSeconds % 60);
    }

    private void toggleVoicePlayback(VrProto.Message message) {
        if (message == null || message.deleting || message.voiceLoading) {
            return;
        }
        if (message.id.equals(activeVoiceId) && voicePlayer != null) {
            if (voicePlayer.isPlaying()) {
                pauseVoicePlayback();
            } else {
                try {
                    voicePlayer.start();
                    message.voicePlaying = true;
                    updateVoiceViews(message);
                    startVoiceTicker();
                } catch (Exception ignored) {
                    stopVoicePlayback();
                }
            }
            return;
        }
        stopVoicePlayback();
        activeVoiceId = message.id;
        if (message.voiceData != null && message.voiceData.length > 0) {
            prepareVoicePlayer(message, message.voiceData);
            return;
        }
        message.voiceLoading = true;
        updateProgressById(message.id, 0, false);
        voiceExecutor.execute(() -> {
            String token = ensureToken();
            byte[] bytes = token == null ? null : VrProto.downloadVoice(
                    token, myUid, peerUid, message,
                    percent -> runOnUiThread(() -> {
                        VrProto.Message current = messageById(message.id);
                        if (current != null
                                && current.id.equals(activeVoiceId)) {
                            current.progress = percent;
                            updateVoiceViews(current);
                        }
                    }));
            runOnUiThread(() -> {
                VrProto.Message current = messageById(message.id);
                if (current == null) {
                    return;
                }
                current.voiceLoading = false;
                if (bytes == null || bytes.length == 0) {
                    if (current.id.equals(activeVoiceId)) {
                        activeVoiceId = "";
                    }
                    lastRenderSignature = "";
                    renderMessages(true);
                    Toast.makeText(this,
                            "Could not load this voice message.",
                            Toast.LENGTH_SHORT).show();
                    return;
                }
                current.voiceData = bytes;
                current.progress = 100;
                if (current.id.equals(activeVoiceId)) {
                    prepareVoicePlayer(current, bytes);
                } else {
                    updateVoiceViews(current);
                }
            });
        });
    }

    private void prepareVoicePlayer(VrProto.Message message, byte[] bytes) {
        releaseVoicePlayerOnly();
        try {
            voicePlayerSource = new ByteArrayMediaDataSource(bytes);
            voicePlayer = new MediaPlayer();
            voicePlayer.setDataSource(voicePlayerSource);
            voicePlayer.setOnPreparedListener(player -> {
                VrProto.Message current = messageById(message.id);
                if (current == null || !current.id.equals(activeVoiceId)) {
                    return;
                }
                int duration = Math.max(1, player.getDuration());
                if (current.voicePositionMs > 0
                        && current.voicePositionMs < duration - 250) {
                    player.seekTo(current.voicePositionMs);
                }
                player.start();
                current.voicePlaying = true;
                updateVoiceViews(current);
                startVoiceTicker();
            });
            voicePlayer.setOnCompletionListener(player -> {
                VrProto.Message current = messageById(message.id);
                if (current != null) {
                    current.voicePositionMs = 0;
                    current.voicePlaying = false;
                    updateVoiceViews(current);
                }
                activeVoiceId = "";
                releaseVoicePlayerOnly();
            });
            voicePlayer.setOnErrorListener((player, what, extra) -> {
                Toast.makeText(this, "Could not play this voice message.",
                        Toast.LENGTH_SHORT).show();
                stopVoicePlayback();
                return true;
            });
            voicePlayer.prepareAsync();
            updateVoiceViews(message);
        } catch (Exception ignored) {
            stopVoicePlayback();
            Toast.makeText(this, "Could not play this voice message.",
                    Toast.LENGTH_SHORT).show();
        }
    }

    private void startVoiceTicker() {
        if (voiceTicker != null) {
            handler.removeCallbacks(voiceTicker);
        }
        voiceTicker = new Runnable() {
            @Override
            public void run() {
                if (voicePlayer == null || activeVoiceId.length() == 0) {
                    return;
                }
                VrProto.Message current = messageById(activeVoiceId);
                if (current != null) {
                    try {
                        current.voicePositionMs = voicePlayer.getCurrentPosition();
                        current.voicePlaying = voicePlayer.isPlaying();
                    } catch (Exception ignored) {
                        current.voicePlaying = false;
                    }
                    updateVoiceViews(current);
                }
                handler.postDelayed(this, 100L);
            }
        };
        handler.post(voiceTicker);
    }

    private void pauseVoicePlayback() {
        if (voicePlayer == null) {
            return;
        }
        VrProto.Message current = messageById(activeVoiceId);
        try {
            if (voicePlayer.isPlaying()) {
                if (current != null) {
                    current.voicePositionMs = voicePlayer.getCurrentPosition();
                }
                voicePlayer.pause();
            }
        } catch (Exception ignored) {
        }
        if (current != null) {
            current.voicePlaying = false;
            updateVoiceViews(current);
        }
        if (voiceTicker != null) {
            handler.removeCallbacks(voiceTicker);
        }
    }

    private void stopVoicePlayback() {
        VrProto.Message current = messageById(activeVoiceId);
        if (current != null) {
            current.voicePlaying = false;
            updateVoiceViews(current);
        }
        activeVoiceId = "";
        if (voiceTicker != null) {
            handler.removeCallbacks(voiceTicker);
            voiceTicker = null;
        }
        releaseVoicePlayerOnly();
    }

    private void releaseVoicePlayerOnly() {
        if (voicePlayer != null) {
            try {
                voicePlayer.reset();
            } catch (Exception ignored) {
            }
            voicePlayer.release();
            voicePlayer = null;
        }
        if (voicePlayerSource != null) {
            try {
                voicePlayerSource.close();
            } catch (Exception ignored) {
            }
            voicePlayerSource = null;
        }
    }

    private void updateVoiceViews(VrProto.Message message) {
        if (message == null || messagesContainer == null) {
            return;
        }
        ImageButton playButton = (ImageButton) findViewWithTagRecursive(
                messagesContainer, "voice_play:" + message.id);
        TextView timeView = (TextView) findViewWithTagRecursive(
                messagesContainer, "voice_time:" + message.id);
        View progressView = findViewWithTagRecursive(
                messagesContainer, "voice_progress:" + message.id);
        if (playButton != null) {
            boolean playing = message.id.equals(activeVoiceId)
                    && voicePlayer != null && voicePlayer.isPlaying();
            playButton.setImageResource(playing ? R.drawable.ic_pause
                    : R.drawable.ic_play);
            playButton.setContentDescription(playing ? "Pause" : "Play");
        }
        if (timeView != null) {
            int shown = message.id.equals(activeVoiceId)
                    ? Math.max(0, message.voicePositionMs) : 0;
            timeView.setText(message.voiceLoading
                    ? "Loading " + Math.max(0, Math.min(100, message.progress)) + "%"
                    : formatVoiceDuration(shown)
                            + " / " + formatVoiceDuration(message.durationMs));
        }
        if (progressView != null) {
            int duration = Math.max(1, message.durationMs);
            float fraction = message.voiceLoading
                    ? Math.max(0f, Math.min(1f, message.progress / 100f))
                    : Math.max(0f, Math.min(1f,
                            message.voicePositionMs / (float) duration));
            progressView.setPivotX(0f);
            progressView.setScaleX(Math.max(0.001f, fraction));
        }
    }

    private static final class ByteArrayMediaDataSource extends MediaDataSource {
        private final byte[] data;

        ByteArrayMediaDataSource(byte[] data) {
            this.data = data == null ? new byte[0] : data;
        }

        @Override
        public int readAt(long position, byte[] buffer, int offset, int size) {
            if (position < 0) {
                return -1;
            }
            if (position >= data.length) {
                return 0;
            }
            int count = (int) Math.min(size, data.length - position);
            System.arraycopy(data, (int) position, buffer, offset, count);
            return count;
        }

        @Override
        public long getSize() {
            return data.length;
        }

        @Override
        public void close() {
        }
    }

    private boolean downloadedByMe(VrProto.Message message) {
        return myUid.equals(message.senderUid)
                ? message.senderDownloaded : message.recipientDownloaded;
    }

    private VrProto.Message messageById(String id) {
        for (VrProto.Message m : messages) {
            if (m.id.equals(id)) {
                return m;
            }
        }
        return null;
    }

    private void applyFileStatus(String id, VrProto.FileStatus status,
                                 boolean cloudDeleted) {
        VrProto.Message current = messageById(id);
        if (current == null || status == null) {
            return;
        }
        current.downloadCount = status.downloadCount;
        current.downloadLimit = status.downloadLimit;
        current.senderDownloaded = status.senderDownloaded;
        current.recipientDownloaded = status.recipientDownloaded;
        current.cloudDeleted = cloudDeleted || !status.exists;
        if (current.cloudDeleted) {
            current.downloadCount = current.downloadLimit;
            current.senderDownloaded = true;
            current.recipientDownloaded = true;
        }
        current.downloadStatusLoaded = true;
        current.downloadStatusCheckedAt = System.currentTimeMillis();
        if (downloadedByMe(current)) {
            current.receiptPending = false;
        }
    }

    private void ensureFileStatus(VrProto.Message message) {
        boolean stale = message.downloadStatusLoaded && !message.cloudDeleted
                && !message.downloading
                && System.currentTimeMillis() - message.downloadStatusCheckedAt > 15000L;
        if (message.pending || message.failed
                || (message.downloadStatusLoaded && !stale)
                || loadingFileStatuses.contains(message.id)
                || failedFileStatuses.contains(message.id)) {
            return;
        }
        loadingFileStatuses.add(message.id);
        String id = message.id;
        executor.execute(() -> {
            String token = ensureToken();
            VrProto.FileStatus status = token == null ? new VrProto.FileStatus()
                    : VrProto.loadFileStatus(token, myUid, peerUid, message);
            boolean cleaned = false;
            if (status.ok && status.complete()) {
                cleaned = VrProto.cleanupDownloadedFile(
                        token, myUid, peerUid, message);
            }
            boolean finalCleaned = cleaned;
            runOnUiThread(() -> {
                loadingFileStatuses.remove(id);
                if (status.ok) {
                    applyFileStatus(id, status, finalCleaned);
                } else {
                    failedFileStatuses.add(id);
                }
                lastRenderSignature = "";
                renderMessages(true);
            });
        });
    }

    private void retryDownloadReceipt(VrProto.Message message) {
        failedFileStatuses.remove(message.id);
        executor.execute(() -> {
            String token = ensureToken();
            VrProto.FileStatus status = token == null ? new VrProto.FileStatus()
                    : VrProto.markFileDownloaded(token, myUid, peerUid, message);
            boolean cleaned = false;
            if (status.ok && status.complete()) {
                cleaned = VrProto.cleanupDownloadedFile(
                        token, myUid, peerUid, message);
            }
            boolean finalCleaned = cleaned;
            runOnUiThread(() -> {
                if (status.ok) {
                    applyFileStatus(message.id, status, finalCleaned);
                    Toast.makeText(this, finalCleaned
                            ? "Download counted. Cloud copy deleted."
                            : "Download counted.", Toast.LENGTH_SHORT).show();
                } else {
                    Toast.makeText(this,
                            "Could not update the download counter yet.",
                            Toast.LENGTH_SHORT).show();
                }
                lastRenderSignature = "";
                renderMessages(true);
            });
        });
    }

    private void requestDownload(VrProto.Message message) {
        if (message.decryptionFailed) {
            Toast.makeText(this, R.string.e2ee_locked_message,
                    Toast.LENGTH_SHORT).show();
            return;
        }
        if (message.pending || message.failed || message.downloading) {
            return;
        }
        if (message.receiptPending) {
            retryDownloadReceipt(message);
            return;
        }
        if (message.cloudDeleted) {
            Toast.makeText(this,
                    "The cloud copy was deleted after 2 downloads.",
                    Toast.LENGTH_LONG).show();
            return;
        }
        if (message.downloadStatusLoaded
                && message.downloadCount >= message.downloadLimit) {
            retryDownloadReceipt(message);
            Toast.makeText(this, "Download limit reached: "
                    + message.downloadLimit + "/" + message.downloadLimit
                    + ". Deleting the cloud copy...", Toast.LENGTH_LONG).show();
            return;
        }
        if (message.downloadStatusLoaded && downloadedByMe(message)) {
            Toast.makeText(this, "You already downloaded this file.",
                    Toast.LENGTH_SHORT).show();
            return;
        }
        pendingDownload = message;
        String safeName = VrProto.safeFileName(message.fileName);
        String safeMime = VrProto.safeMimeType(message.mimeType);
        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType(safeMime.length() == 0 ? "*/*" : safeMime);
        intent.putExtra(Intent.EXTRA_TITLE,
                safeName.length() > 0 ? safeName : "file");
        startActivityForResult(intent, REQ_SAVE_FILE);
    }

    private void downloadToUri(VrProto.Message message, Uri uri) {
        failedFileStatuses.remove(message.id);
        updateProgressById(message.id, 0, true);
        executor.execute(() -> {
            String token = ensureToken();
            VrProto.FileStatus before = token == null ? new VrProto.FileStatus()
                    : VrProto.loadFileStatus(token, myUid, peerUid, message);
            boolean eligible = before.ok && before.exists
                    && !before.downloadedBy(myUid)
                    && before.downloadCount < before.downloadLimit;
            OutputStream output = null;
            boolean downloaded = false;
            boolean closeOk = false;
            if (eligible) {
                try {
                    output = getContentResolver().openOutputStream(uri, "w");
                    downloaded = output != null
                            && VrProto.downloadFile(token, myUid, peerUid, message,
                                    output,
                                    percent -> runOnUiThread(() -> updateProgressById(
                                            message.id, percent, true)));
                } catch (Exception ignored) {
                    downloaded = false;
                } finally {
                    if (output != null) {
                        try {
                            output.close();
                            closeOk = true;
                        } catch (Exception ignored) {
                            closeOk = false;
                        }
                    }
                }
            }
            boolean saved = downloaded && closeOk;
            VrProto.FileStatus afterMark = null;
            boolean receiptOk = false;
            boolean cleaned = false;
            if (saved) {
                afterMark = VrProto.markFileDownloaded(
                        token, myUid, peerUid, message);
                receiptOk = afterMark.ok && afterMark.exists
                        && afterMark.downloadedBy(myUid);
                if (receiptOk && afterMark.complete()) {
                    cleaned = VrProto.cleanupDownloadedFile(
                            token, myUid, peerUid, message);
                }
            }
            VrProto.FileStatus finalStatus = afterMark != null && afterMark.ok
                    ? afterMark : before;
            boolean finalSaved = saved;
            boolean finalReceiptOk = receiptOk;
            boolean finalCleaned = cleaned;
            runOnUiThread(() -> {
                VrProto.Message current = messageById(message.id);
                if (current != null) {
                    current.downloading = false;
                    current.progress = finalSaved ? 100 : 0;
                    if (finalStatus != null && finalStatus.ok) {
                        applyFileStatus(message.id, finalStatus, finalCleaned);
                    }
                    if (finalSaved && !finalReceiptOk) {
                        current.receiptPending = true;
                    }
                    if (finalCleaned) {
                        current.cloudDeleted = true;
                    }
                }
                lastRenderSignature = "";
                renderMessages(true);
                if (!eligible) {
                    Toast.makeText(this, before.ok && !before.exists
                            ? "The cloud copy was already deleted."
                            : "This file is no longer available for your download slot.",
                            Toast.LENGTH_LONG).show();
                } else if (finalSaved && finalReceiptOk) {
                    Toast.makeText(this, finalCleaned
                            ? "File saved. Downloads: 2/2. Cloud copy deleted."
                            : "File saved. Download counted.",
                            Toast.LENGTH_LONG).show();
                } else if (finalSaved) {
                    Toast.makeText(this,
                            "File saved, but the download counter was not updated. Tap the file to retry the counter.",
                            Toast.LENGTH_LONG).show();
                } else {
                    Toast.makeText(this,
                            "Download failed. The counter was not used.",
                            Toast.LENGTH_LONG).show();
                }
            });
        });
    }

    private void showWallpaperMenu() {
        String[] options = activeWallpaperId.length() == 0
                ? new String[]{"Choose wallpaper"}
                : new String[]{"Choose wallpaper", "Remove my wallpaper"};
        new AlertDialog.Builder(this)
                .setTitle("Chat wallpaper")
                .setItems(options, (dialog, which) -> {
                    if (which == 0) {
                        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
                        intent.addCategory(Intent.CATEGORY_OPENABLE);
                        intent.setType("image/*");
                        startActivityForResult(intent, REQ_PICK_WALLPAPER);
                    } else {
                        removeOwnWallpaper();
                    }
                })
                .show();
    }

    private void prepareWallpaper(Uri uri) {
        executor.execute(() -> {
            byte[] image = prepareWallpaperImage(uri);
            String wallpaperId = VrProto.newWallpaperId(System.currentTimeMillis());
            runOnUiThread(() -> {
                if (image == null) {
                    Toast.makeText(this, "Could not load this image.",
                            Toast.LENGTH_SHORT).show();
                } else {
                    showWallpaperScopeDialog(image, wallpaperId);
                }
            });
        });
    }

    private byte[] prepareWallpaperImage(Uri uri) {
        try {
            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            InputStream first = getContentResolver().openInputStream(uri);
            BitmapFactory.decodeStream(first, null, bounds);
            if (first != null) {
                first.close();
            }
            int sample = ImageDecode.sampleSize(bounds.outWidth, bounds.outHeight,
                    1800, 1800 * 1800);
            if (sample <= 0) {
                return null;
            }
            BitmapFactory.Options options = new BitmapFactory.Options();
            options.inSampleSize = sample;
            InputStream second = getContentResolver().openInputStream(uri);
            Bitmap bitmap = BitmapFactory.decodeStream(second, null, options);
            if (second != null) {
                second.close();
            }
            if (bitmap == null) {
                return null;
            }
            Bitmap current = scaleBitmapToMax(bitmap, 1440);
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            int quality = 86;
            for (int attempt = 0; attempt < 9; attempt++) {
                out.reset();
                current.compress(Bitmap.CompressFormat.JPEG, quality, out);
                if (out.size() <= VrProto.MAX_WALLPAPER_BYTES) {
                    return out.toByteArray();
                }
                if (quality > 48) {
                    quality -= 10;
                } else {
                    int nextMax = Math.max(640,
                            Math.round(Math.max(current.getWidth(),
                                    current.getHeight()) * 0.78f));
                    current = scaleBitmapToMax(current, nextMax);
                }
            }
            return null;
        } catch (Exception ignored) {
            return null;
        }
    }

    private Bitmap scaleBitmapToMax(Bitmap source, int maxDimension) {
        int largest = Math.max(source.getWidth(), source.getHeight());
        if (largest <= maxDimension) {
            return source;
        }
        float scale = maxDimension / (float) largest;
        return Bitmap.createScaledBitmap(source,
                Math.max(1, Math.round(source.getWidth() * scale)),
                Math.max(1, Math.round(source.getHeight() * scale)), true);
    }

    private void showWallpaperScopeDialog(byte[] image, String wallpaperId) {
        String peer = displayName();
        String[] options = {"Set for me", "Suggest to " + peer,
                "Set for me and suggest"};
        new AlertDialog.Builder(this)
                .setTitle("Use this wallpaper?")
                .setItems(options, (dialog, which) -> applyWallpaperChoice(
                        image, wallpaperId, which != 1, which != 0))
                .show();
    }

    private void applyWallpaperChoice(byte[] image, String wallpaperId,
                                      boolean setMine, boolean suggest) {
        long now = System.currentTimeMillis();
        executor.execute(() -> {
            String token = ensureToken();
            E2eeCrypto.Session session = token == null ? null : prepareE2ee(token);
            boolean uploaded = token != null && session != null && VrProto.uploadWallpaper(
                    token, myUid, peerUid, wallpaperId, image, suggest, now, session);
            boolean ownSet = uploaded && setMine && VrProto.setOwnWallpaper(
                    token, myUid, peerUid, wallpaperId, myUid, now);
            boolean sent = false;
            if (uploaded && suggest) {
                VrProto.Message message = new VrProto.Message();
                message.id = VrProto.newClientId(now);
                message.type = VrProto.TYPE_WALLPAPER;
                message.senderUid = myUid;
                message.text = "Wallpaper suggestion";
                message.wallpaperId = wallpaperId;
                message.wallpaperOwnerUid = myUid;
                message.createdAt = now;
                sent = e2eeReadyForSending()
                        && VrProto.sendWallpaper(token, myUid, peerUid, message, session);
            }
            boolean ok = uploaded && (!setMine || ownSet) && (!suggest || sent);
            Bitmap decoded = ownSet ? decodeWallpaper(image) : null;
            runOnUiThread(() -> {
                if (ownSet) {
                    activeWallpaperId = wallpaperId;
                    activeWallpaperOwnerUid = myUid;
                    activeWallpaperBitmap = decoded;
                    wallpaperPreviews.put(wallpaperKey(myUid, wallpaperId), decoded);
                    applyBackground();
                }
                Toast.makeText(this, ok
                        ? (setMine && suggest ? "Wallpaper set and suggested."
                                : setMine ? "Wallpaper set." : "Wallpaper suggested.")
                        : "Could not update the wallpaper. Check Firebase rules.",
                        Toast.LENGTH_LONG).show();
                if (ok) {
                    syncNow(false);
                }
            });
        });
    }

    private void removeOwnWallpaper() {
        long now = System.currentTimeMillis();
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = token != null && VrProto.setOwnWallpaper(
                    token, myUid, peerUid, "", "", now);
            runOnUiThread(() -> {
                if (ok) {
                    activeWallpaperId = "";
                    activeWallpaperOwnerUid = "";
                    activeWallpaperBitmap = null;
                    applyBackground();
                    renderMessages(true);
                }
                Toast.makeText(this, ok ? "Wallpaper removed."
                        : "Could not remove the wallpaper.", Toast.LENGTH_SHORT).show();
            });
        });
    }

    private void acceptWallpaper(VrProto.Message message) {
        long now = System.currentTimeMillis();
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = token != null && VrProto.setOwnWallpaper(token, myUid,
                    peerUid, message.wallpaperId, message.wallpaperOwnerUid, now);
            E2eeCrypto.Session session = token == null ? null : prepareE2ee(token);
            byte[] image = ok ? VrProto.loadWallpaper(token, myUid, peerUid,
                    message.wallpaperOwnerUid, message.wallpaperId, session,
                    message.wallpaperKey) : null;
            Bitmap decoded = decodeWallpaper(image);
            runOnUiThread(() -> {
                if (ok) {
                    activeWallpaperId = message.wallpaperId;
                    activeWallpaperOwnerUid = message.wallpaperOwnerUid;
                    activeWallpaperBitmap = decoded;
                    if (decoded != null) {
                        wallpaperPreviews.put(wallpaperKey(
                                message.wallpaperOwnerUid, message.wallpaperId), decoded);
                    }
                    applyBackground();
                    renderMessages(true);
                }
                Toast.makeText(this, ok ? "Wallpaper set."
                        : "Could not set this wallpaper.", Toast.LENGTH_SHORT).show();
            });
        });
    }

    private String wallpaperKey(String ownerUid, String wallpaperId) {
        return ownerUid + "/" + wallpaperId;
    }

    private Bitmap wallpaperBitmapFor(String ownerUid, String wallpaperId) {
        if (ownerUid.length() == 0 || wallpaperId.length() == 0) {
            return null;
        }
        String key = wallpaperKey(ownerUid, wallpaperId);
        Bitmap cached = wallpaperPreviews.get(key);
        if (cached != null || loadingWallpapers.contains(key)
                || failedWallpapers.contains(key)) {
            return cached;
        }
        loadingWallpapers.add(key);
        executor.execute(() -> {
            String token = ensureToken();
            E2eeCrypto.Session session = token == null ? null : prepareE2ee(token);
            byte[] suggestedKey = wallpaperKeys.get(key);
            byte[] image = token == null ? null
                    : VrProto.loadWallpaper(token, myUid, peerUid, ownerUid,
                            wallpaperId, session, suggestedKey);
            Bitmap decoded = decodeWallpaper(image);
            runOnUiThread(() -> {
                loadingWallpapers.remove(key);
                if (decoded == null) {
                    failedWallpapers.add(key);
                } else {
                    wallpaperPreviews.put(key, decoded);
                    if (key.equals(wallpaperKey(activeWallpaperOwnerUid,
                            activeWallpaperId))) {
                        activeWallpaperBitmap = decoded;
                        applyBackground();
                    }
                }
                lastRenderSignature = "";
                renderMessages(true);
            });
        });
        return null;
    }

    private Bitmap decodeWallpaper(byte[] image) {
        if (image == null || image.length == 0
                || image.length > VrProto.MAX_WALLPAPER_BYTES + 28) {
            return null;
        }
        return ImageDecode.decodeBytes(image, 1200, 1200 * 1200);
    }

    private String formatBytes(long bytes) {
        if (bytes < 1024) {
            return bytes + " B";
        }
        if (bytes < 1024 * 1024) {
            return String.format(Locale.ROOT, "%.1f KB", bytes / 1024.0);
        }
        return String.format(Locale.ROOT, "%.1f MB", bytes / (1024.0 * 1024.0));
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
        return ThemeFx.isDark(this)
                || AppSettings.chatBackground(this) == AppSettings.BG_DARK;
    }

    private boolean customDarkBackground() {
        return AppSettings.chatBackground(this) == AppSettings.BG_DARK;
    }

    private void applyBackground() {
        if (messagesScroll == null || messagesContainer == null) {
            return;
        }
        if (activeWallpaperBitmap != null) {
            BitmapDrawable wallpaper = new BitmapDrawable(
                    getResources(), activeWallpaperBitmap);
            wallpaper.setGravity(Gravity.FILL);
            messagesScroll.setBackground(wallpaper);
            messagesContainer.setBackgroundColor(isDark()
                    ? Color.argb(54, 0, 0, 0)
                    : Color.argb(24, 255, 255, 255));
            return;
        }
        int bg = AppSettings.chatBackground(this);
        int color = ThemeFx.isDark(this) || bg == AppSettings.BG_DARK
                ? Color.rgb(17, 17, 17)
                : bg == AppSettings.BG_LIGHT ? Color.rgb(245, 245, 245)
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
                .append(e2eeOwnState).append('/').append(e2eePeerState).append('|')
                .append(query).append('|').append(isDark()).append('|')
                .append(activeWallpaperOwnerUid).append('/').append(activeWallpaperId)
                .append('|').append(wallpaperPreviews.size())
                .append('|').append(messages.size());
        for (VrProto.Message m : messages) {
            b.append('|').append(m.id).append(':').append(m.type)
                    .append(':').append(m.text.hashCode())
                    .append(':').append(m.editedAt)
                    .append(m.operationPending ? 'u' : 'v')
                    .append(m.deleting ? 'g' : 'h')
                    .append(':').append(m.pending ? 'p' : 's')
                    .append(m.failed ? 'f' : 'o')
                    .append(':').append(m.progress)
                    .append(m.downloading ? 'd' : 'n')
                    .append(':').append(m.downloadCount)
                    .append('/').append(m.downloadLimit)
                    .append(m.senderDownloaded ? 's' : 'x')
                    .append(m.recipientDownloaded ? 'r' : 'y')
                    .append(m.cloudDeleted ? 'c' : 'k')
                    .append(m.downloadStatusLoaded ? 'l' : 'u')
                    .append(m.receiptPending ? 'q' : 'z')
                    .append(':').append(m.replyToId)
                    .append(':').append(m.replyToText.hashCode())
                    .append(':').append(m.durationMs)
                    .append(m.voiceLoading ? 'l' : 'n')
                    .append(m.voiceData != null ? 'a' : 'b')
                    .append(':').append(m.protocolVersion)
                    .append(m.encrypted ? 'e' : 'p')
                    .append(m.decryptionFailed ? 'x' : 'd');
        }
        return b.toString();
    }

    private void renderMessages(boolean force) {
        if (messagesContainer == null || evaporatingMessageId.length() > 0) {
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
            if (!m.deleting && (query.length() == 0
                    || m.text.toLowerCase(Locale.ROOT).contains(query))) {
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
        PressFx.applyTree(messagesContainer);
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
        FrameLayout row = new FrameLayout(this);
        LinearLayout.LayoutParams rowParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        rowParams.bottomMargin = dp(8);
        row.setLayoutParams(rowParams);

        ImageView replyIndicator = new ImageView(this);
        replyIndicator.setImageResource(R.drawable.ic_reply);
        replyIndicator.setColorFilter(getColor(R.color.text_main),
                PorterDuff.Mode.SRC_IN);
        replyIndicator.setAlpha(0f);
        replyIndicator.setScaleX(0.75f);
        replyIndicator.setScaleY(0.75f);
        FrameLayout.LayoutParams replyIndicatorParams =
                new FrameLayout.LayoutParams(dp(29), dp(29),
                        Gravity.END | Gravity.CENTER_VERTICAL);
        replyIndicatorParams.rightMargin = dp(7);
        row.addView(replyIndicator, replyIndicatorParams);

        LinearLayout bubble = new LinearLayout(this);
        bubble.setTag(message.id);
        bubble.setOrientation(LinearLayout.VERTICAL);
        bubble.setPadding(dp(12), dp(9), dp(12), dp(7));
        boolean themeDark = ThemeFx.isDark(this);
        boolean customDark = customDarkBackground();
        boolean mineLightBubble = mine && (themeDark || customDark);
        bubble.setBackgroundResource(mine
                ? (!themeDark && customDark
                        ? R.drawable.bg_glass_card : R.drawable.bg_message_mine)
                : R.drawable.bg_message_peer);
        boolean downloadTap = VrProto.TYPE_FILE.equals(message.type)
                && !message.pending && !message.failed;
        bubble.setClickable((message.failed && mine) || downloadTap);
        bubble.setFocusable((message.failed && mine) || downloadTap);
        if (message.failed && mine) {
            bubble.setOnClickListener(v -> retryMessage(message));
        } else if (downloadTap) {
            bubble.setOnClickListener(v -> requestDownload(message));
        }
        bubble.setOnLongClickListener(v -> {
            PressFx.release(v);
            showMessageActions(message, mine);
            return true;
        });

        int maxBubbleWidth = Math.min(
                Math.round(getResources().getDisplayMetrics().widthPixels * 0.80f),
                dp(540));
        int mainColor = mine
                ? (mineLightBubble ? Color.rgb(17, 17, 17) : Color.WHITE)
                : getColor(R.color.text_main);
        int secondaryColor = mine
                ? (mineLightBubble ? Color.rgb(90, 90, 90)
                        : Color.rgb(215, 215, 215))
                : getColor(R.color.text_secondary);
        if (VrProto.hasReply(message)) {
            addReplyQuote(bubble, message, mine, maxBubbleWidth,
                    mainColor, secondaryColor);
        }
        if (VrProto.TYPE_FILE.equals(message.type)) {
            addFileContent(bubble, message, maxBubbleWidth, mainColor, secondaryColor);
        } else if (VrProto.TYPE_VOICE.equals(message.type)) {
            addVoiceContent(bubble, message, maxBubbleWidth,
                    mainColor, secondaryColor);
        } else if (VrProto.TYPE_WALLPAPER.equals(message.type)) {
            addWallpaperContent(bubble, message, mine, maxBubbleWidth,
                    mainColor, secondaryColor);
        } else {
            TextView text = new TextView(this);
            text.setText(message.decryptionFailed
                    ? getString(R.string.e2ee_locked_message) : message.text);
            text.setMaxWidth(Math.max(dp(80), maxBubbleWidth - dp(28)));
            text.setTextColor(mainColor);
            text.setTextSize(AppSettings.textSizeSp(this));
            bubble.addView(text, new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT));
        }

        String meta = metaText(message, mine);
        if (meta.length() > 0) {
            TextView metaView = new TextView(this);
            metaView.setText(meta);
            metaView.setTextColor(mine
                    ? (mineLightBubble ? Color.rgb(95, 95, 95)
                            : Color.rgb(210, 210, 210))
                    : getColor(R.color.text_secondary));
            metaView.setTextSize(11);
            metaView.setGravity(Gravity.END);
            LinearLayout.LayoutParams metaParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT);
            metaParams.topMargin = dp(4);
            metaParams.gravity = Gravity.END;
            bubble.addView(metaView, metaParams);
        }

        attachSwipeReply(bubble, replyIndicator, message);
        FrameLayout.LayoutParams bubbleParams = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.WRAP_CONTENT,
                FrameLayout.LayoutParams.WRAP_CONTENT,
                (mine ? Gravity.END : Gravity.START) | Gravity.CENTER_VERTICAL);
        row.addView(bubble, bubbleParams);
        return row;
    }

    private GradientDrawable roundedBackground(int color, int radiusDp) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setShape(GradientDrawable.RECTANGLE);
        drawable.setColor(color);
        drawable.setCornerRadius(dp(radiusDp));
        return drawable;
    }

    private int withAlpha(int color, int alpha) {
        return Color.argb(Math.max(0, Math.min(255, alpha)),
                Color.red(color), Color.green(color), Color.blue(color));
    }

    private void addReplyQuote(LinearLayout bubble, VrProto.Message message,
                               boolean mine, int maxBubbleWidth,
                               int mainColor, int secondaryColor) {
        LinearLayout quote = new LinearLayout(this);
        quote.setOrientation(LinearLayout.VERTICAL);
        quote.setPadding(dp(10), dp(7), dp(10), dp(7));
        quote.setBackground(roundedBackground(
                withAlpha(mainColor, mine ? 42 : 22), 14));
        quote.setClickable(true);
        quote.setFocusable(true);
        quote.setOnClickListener(v -> scrollToMessage(message.replyToId));
        quote.setOnLongClickListener(v -> {
            PressFx.release(bubble);
            showMessageActions(message, mine);
            return true;
        });

        TextView title = new TextView(this);
        title.setText(senderLabelForUid(message.replyToSenderUid));
        title.setTextColor(secondaryColor);
        title.setTextSize(12);
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        title.setSingleLine(true);
        title.setEllipsize(android.text.TextUtils.TruncateAt.END);
        title.setMaxWidth(Math.max(dp(90), maxBubbleWidth - dp(48)));
        quote.addView(title);

        TextView preview = new TextView(this);
        preview.setText(replySummaryText(
                message.replyToType, message.replyToText));
        preview.setTextColor(mainColor);
        preview.setTextSize(13);
        preview.setSingleLine(true);
        preview.setEllipsize(android.text.TextUtils.TruncateAt.END);
        preview.setMaxWidth(Math.max(dp(90), maxBubbleWidth - dp(48)));
        LinearLayout.LayoutParams previewParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        previewParams.topMargin = dp(2);
        quote.addView(preview, previewParams);

        LinearLayout.LayoutParams quoteParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        quoteParams.bottomMargin = dp(7);
        bubble.addView(quote, quoteParams);
        PressFx.attach(quote);
    }

    private void addVoiceContent(LinearLayout bubble, VrProto.Message message,
                                 int maxBubbleWidth, int mainColor,
                                 int secondaryColor) {
        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.HORIZONTAL);
        content.setGravity(Gravity.CENTER_VERTICAL);
        int contentWidth = Math.max(dp(205), maxBubbleWidth - dp(28));
        content.setLayoutParams(new LinearLayout.LayoutParams(
                contentWidth, LinearLayout.LayoutParams.WRAP_CONTENT));

        ImageButton playButton = new ImageButton(this);
        playButton.setTag("voice_play:" + message.id);
        boolean playing = message.id.equals(activeVoiceId)
                && voicePlayer != null && voicePlayer.isPlaying();
        playButton.setImageResource(playing ? R.drawable.ic_pause
                : R.drawable.ic_play);
        playButton.setColorFilter(mainColor, PorterDuff.Mode.SRC_IN);
        playButton.setBackgroundResource(R.drawable.bg_glass_pill);
        playButton.setContentDescription(playing ? "Pause" : "Play");
        playButton.setOnClickListener(v -> toggleVoicePlayback(message));
        PressFx.attach(playButton);
        content.addView(playButton, new LinearLayout.LayoutParams(dp(44), dp(44)));

        LinearLayout details = new LinearLayout(this);
        details.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams detailsParams = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
        detailsParams.leftMargin = dp(11);

        TextView title = new TextView(this);
        title.setText("Voice message");
        title.setTextColor(mainColor);
        title.setTextSize(Math.max(14, AppSettings.textSizeSp(this) - 1));
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        details.addView(title);

        FrameLayout track = new FrameLayout(this);
        track.setBackground(roundedBackground(withAlpha(secondaryColor, 80), 4));
        LinearLayout.LayoutParams trackParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(5));
        trackParams.topMargin = dp(8);
        View fill = new View(this);
        fill.setTag("voice_progress:" + message.id);
        fill.setBackground(roundedBackground(mainColor, 4));
        track.addView(fill, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT));
        details.addView(track, trackParams);

        TextView time = new TextView(this);
        time.setTag("voice_time:" + message.id);
        time.setTextColor(secondaryColor);
        time.setTextSize(12);
        String status = message.failed ? "Failed. Tap to retry"
                : message.voiceLoading ? "Loading " + message.progress + "%"
                : message.pending ? "Sending " + message.progress + "%"
                : formatVoiceDuration(message.id.equals(activeVoiceId)
                        ? message.voicePositionMs : 0)
                        + " / " + formatVoiceDuration(message.durationMs);
        time.setText(status);
        LinearLayout.LayoutParams timeParams = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        timeParams.topMargin = dp(5);
        details.addView(time, timeParams);

        content.addView(details, detailsParams);
        bubble.addView(content);
        bubble.post(() -> updateVoiceViews(message));
    }

    /** Swipe either participant's bubble left to quote-reply to it. */
    private void attachSwipeReply(LinearLayout bubble, ImageView indicator,
                                  VrProto.Message message) {
        float[] down = new float[2];
        boolean[] state = new boolean[2]; // dragging, threshold reached
        PressFx.attachWithTouch(bubble, (view, event) -> {
            int action = event.getActionMasked();
            if (action == MotionEvent.ACTION_DOWN) {
                down[0] = event.getRawX();
                down[1] = event.getRawY();
                state[0] = false;
                state[1] = false;
                return false;
            }
            if (action == MotionEvent.ACTION_MOVE) {
                float dx = event.getRawX() - down[0];
                float dy = event.getRawY() - down[1];
                if (!state[0] && dx < -dp(9)
                        && Math.abs(dx) > Math.abs(dy) * 1.15f) {
                    state[0] = true;
                    messagesScroll.requestDisallowInterceptTouchEvent(true);
                }
                if (!state[0]) {
                    return false;
                }
                float translation = Math.max(-dp(72), Math.min(0f, dx * 0.82f));
                view.setTranslationX(translation);
                float progress = Math.min(1f, Math.abs(translation) / dp(52));
                indicator.setAlpha(progress);
                float scale = 0.75f + 0.25f * progress;
                indicator.setScaleX(scale);
                indicator.setScaleY(scale);
                if (progress >= 1f && !state[1]) {
                    state[1] = true;
                    view.performHapticFeedback(
                            HapticFeedbackConstants.KEYBOARD_TAP);
                }
                return true;
            }
            if (action == MotionEvent.ACTION_UP
                    || action == MotionEvent.ACTION_CANCEL) {
                boolean shouldReply = state[1]
                        && action == MotionEvent.ACTION_UP;
                boolean wasDragging = state[0];
                state[0] = false;
                state[1] = false;
                messagesScroll.requestDisallowInterceptTouchEvent(false);
                view.animate().translationX(0f).setDuration(240L)
                        .setInterpolator(new OvershootInterpolator(1.1f))
                        .start();
                indicator.animate().alpha(0f).setDuration(180L).start();
                if (shouldReply) {
                    view.postDelayed(() -> setReplyTarget(message), 70L);
                }
                return wasDragging;
            }
            return false;
        });
    }

    private void addFileContent(LinearLayout bubble, VrProto.Message message,
                                int maxBubbleWidth, int mainColor,
                                int secondaryColor) {
        ensureFileStatus(message);
        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.HORIZONTAL);
        content.setGravity(Gravity.CENTER_VERTICAL);
        int contentWidth = Math.max(dp(190), maxBubbleWidth - dp(28));
        content.setLayoutParams(new LinearLayout.LayoutParams(
                contentWidth, LinearLayout.LayoutParams.WRAP_CONTENT));

        ImageView icon = new ImageView(this);
        icon.setImageResource(R.drawable.ic_file);
        icon.setColorFilter(mainColor, PorterDuff.Mode.SRC_IN);
        content.addView(icon, new LinearLayout.LayoutParams(dp(34), dp(34)));

        LinearLayout texts = new LinearLayout(this);
        texts.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams textParams = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f);
        textParams.leftMargin = dp(10);

        TextView name = new TextView(this);
        name.setText(message.fileName.length() == 0 ? "File" : message.fileName);
        name.setTextColor(mainColor);
        name.setTextSize(AppSettings.textSizeSp(this));
        name.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        name.setMaxLines(2);
        texts.addView(name);

        TextView detail = new TextView(this);
        String counter = "Downloads: " + message.downloadCount
                + "/" + message.downloadLimit;
        String status = message.failed ? "Failed. Tap to retry"
                : message.receiptPending ? "Saved. Tap to update counter"
                : message.cloudDeleted ? "Cloud copy deleted"
                : message.downloading ? "Downloading " + message.progress + "%"
                : message.pending ? "Uploading " + message.progress + "%"
                : downloadedByMe(message) ? "You downloaded this file"
                : message.downloadCount >= message.downloadLimit
                        ? "Deleting cloud copy..."
                : "Tap to download";
        detail.setText(formatBytes(message.fileSize) + "  -  " + counter
                + "  -  " + status);
        detail.setMaxLines(2);
        detail.setTextColor(secondaryColor);
        detail.setTextSize(12);
        texts.addView(detail);
        content.addView(texts, textParams);
        bubble.addView(content);
    }

    private void addWallpaperContent(LinearLayout bubble, VrProto.Message message,
                                     boolean mine, int maxBubbleWidth,
                                     int mainColor, int secondaryColor) {
        TextView title = new TextView(this);
        title.setText(mine ? "Wallpaper suggested" : "Wallpaper suggestion");
        title.setTextColor(mainColor);
        title.setTextSize(AppSettings.textSizeSp(this));
        title.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        bubble.addView(title);

        Bitmap preview = wallpaperBitmapFor(
                message.wallpaperOwnerUid, message.wallpaperId);
        if (preview != null) {
            ImageView image = new ImageView(this);
            image.setImageBitmap(preview);
            image.setScaleType(ImageView.ScaleType.CENTER_CROP);
            image.setAdjustViewBounds(false);
            LinearLayout.LayoutParams imageParams = new LinearLayout.LayoutParams(
                    Math.max(dp(190), maxBubbleWidth - dp(28)), dp(150));
            imageParams.topMargin = dp(8);
            bubble.addView(image, imageParams);
        } else {
            TextView loadingView = new TextView(this);
            loadingView.setText("Loading preview...");
            loadingView.setTextColor(secondaryColor);
            loadingView.setTextSize(13);
            LinearLayout.LayoutParams loadingParams = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.WRAP_CONTENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT);
            loadingParams.topMargin = dp(6);
            bubble.addView(loadingView, loadingParams);
        }

        if (!mine) {
            boolean current = message.wallpaperId.equals(activeWallpaperId)
                    && message.wallpaperOwnerUid.equals(activeWallpaperOwnerUid);
            if (current) {
                TextView currentView = new TextView(this);
                currentView.setText("Current wallpaper");
                currentView.setTextColor(secondaryColor);
                currentView.setTextSize(13);
                LinearLayout.LayoutParams currentParams = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.WRAP_CONTENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT);
                currentParams.topMargin = dp(8);
                bubble.addView(currentView, currentParams);
            } else {
                Button setButton = new Button(this);
                setButton.setText("Set wallpaper");
                setButton.setAllCaps(false);
                setButton.setTextColor(getColor(R.color.text_main));
                setButton.setBackgroundResource(R.drawable.bg_glass_pill);
                setButton.setOnClickListener(v -> acceptWallpaper(message));
                PressFx.attach(setButton);
                LinearLayout.LayoutParams buttonParams = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.MATCH_PARENT, dp(44));
                buttonParams.topMargin = dp(10);
                bubble.addView(setButton, buttonParams);
            }
        }
    }

    private String metaText(VrProto.Message message, boolean mine) {
        StringBuilder out = new StringBuilder();
        if (AppSettings.showTimestamps(this) && message.createdAt > 0) {
            out.append(formatTime(message.createdAt));
        }
        if (!message.encrypted && message.protocolVersion == VrProto.VERSION
                && !message.pending && !message.failed) {
            if (out.length() > 0) {
                out.append("  ");
            }
            out.append(getString(R.string.e2ee_legacy_unencrypted));
        }
        if (VrProto.TYPE_TEXT.equals(message.type) && message.editedAt > 0) {
            if (out.length() > 0) {
                out.append("  ");
            }
            out.append("Edited");
        }
        if (mine) {
            String status = message.operationPending ? "Updating..."
                    : message.failed ? "Failed. Tap to retry"
                    : message.pending && VrProto.TYPE_FILE.equals(message.type)
                            ? "Uploading " + message.progress + "%"
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
