package lav.visaroom.alav;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Intent;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Path;
import android.graphics.PorterDuff;
import android.graphics.Typeface;
import android.graphics.drawable.BitmapDrawable;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.OpenableColumns;
import android.text.Editable;
import android.text.InputType;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import java.io.ByteArrayOutputStream;
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
 * memory while this screen exists; the permanent copy is in Firestore.
 */
public class DirectChatActivity extends Activity {

    public static final String EXTRA_UID = "uid";
    public static final String EXTRA_USERNAME = "username";

    private static final long POLL_MS = 1200L;
    private static final int REQ_PICK_FILE = 701;
    private static final int REQ_PICK_WALLPAPER = 702;
    private static final int REQ_SAVE_FILE = 703;

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
    private String lastRenderSignature = "";
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
        attachButton = findViewById(R.id.attach_button);
        wallpaperButton = findViewById(R.id.wallpaper_button);

        peerAvatar.setColorFilter(Color.BLACK, PorterDuff.Mode.SRC_IN);
        peerName.setText(displayName());
        applyBackground();
        updateComposer();
        renderMessages(true);

        findViewById(R.id.back).setOnClickListener(v -> finishWithAnimation());
        findViewById(R.id.chat_header).setOnClickListener(v -> openPeerProfile());
        findViewById(R.id.search_toggle).setOnClickListener(v -> setSearchOpen(true));
        wallpaperButton.setOnClickListener(v -> showWallpaperMenu());
        attachButton.setOnClickListener(v -> pickFile());
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
        attachButton.setEnabled(canWrite);
        wallpaperButton.setEnabled(peerUid.length() > 0 && !peerUid.equals(myUid));
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
        message.type = VrProto.TYPE_TEXT;
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
        if (VrProto.TYPE_FILE.equals(message.type)) {
            submitFileMessage(message);
            return;
        }
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

    private void copyMessageText(VrProto.Message message) {
        ClipboardManager clipboard =
                (ClipboardManager) getSystemService(CLIPBOARD_SERVICE);
        if (clipboard != null) {
            String copied = VrProto.TYPE_FILE.equals(message.type)
                    ? message.fileName : message.text;
            clipboard.setPrimaryClip(ClipData.newPlainText("Message", copied));
            Toast.makeText(this, "Copied", Toast.LENGTH_SHORT).show();
        }
    }

    private void showMessageActions(VrProto.Message message, boolean mine) {
        List<String> labels = new ArrayList<>();
        List<Integer> actions = new ArrayList<>();
        labels.add("Copy");
        actions.add(0);
        boolean canEdit = mine && VrProto.TYPE_TEXT.equals(message.type)
                && !message.pending && !message.operationPending
                && !message.deleting;
        boolean canDelete = mine && !message.pending && !message.downloading
                && !message.operationPending && !message.deleting;
        if (canEdit) {
            labels.add("Edit");
            actions.add(1);
        }
        if (canDelete) {
            labels.add("Delete");
            actions.add(2);
        }
        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle("Message")
                .setItems(labels.toArray(new String[0]), (d, which) -> {
                    int action = actions.get(which);
                    if (action == 0) {
                        copyMessageText(message);
                    } else if (action == 1) {
                        showEditMessageDialog(message);
                    } else if (action == 2) {
                        confirmDeleteMessage(message);
                    }
                })
                .create();
        dialog.show();
        PressFx.install(dialog);
    }

    private void showEditMessageDialog(VrProto.Message message) {
        VrProto.Message current = messageById(message.id);
        if (current == null) {
            return;
        }
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(24), dp(8), dp(24), dp(8));

        EditText input = new EditText(this);
        input.setText(current.text);
        input.setSelection(input.getText().length());
        input.setMinLines(3);
        input.setMaxLines(6);
        input.setInputType(InputType.TYPE_CLASS_TEXT
                | InputType.TYPE_TEXT_FLAG_MULTI_LINE
                | InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);
        box.addView(input, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));

        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle("Edit message")
                .setView(box)
                .setNegativeButton("Cancel", null)
                .setPositiveButton("Save", null)
                .create();
        dialog.show();
        PressFx.install(dialog);
        dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
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
            if (newText.equals(current.text)) {
                dialog.dismiss();
                return;
            }
            dialog.dismiss();
            submitEditMessage(current.id, newText);
        });
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
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = token != null && VrProto.editText(
                    token, myUid, peerUid, operationMessage, newText, editedAt);
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
                : VrProto.TYPE_FILE.equals(message.type)
                        ? "Delete this file for both participants? Its cloud copy will also be deleted."
                        : "Delete this message for both participants?";
        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle("Delete message?")
                .setMessage(text)
                .setNegativeButton("Cancel", null)
                .setPositiveButton("Delete", (d, which) ->
                        submitDeleteMessage(message.id))
                .create();
        dialog.show();
        PressFx.install(dialog);
    }

    private void submitDeleteMessage(String messageId) {
        VrProto.Message current = messageById(messageId);
        if (current == null || current.operationPending) {
            return;
        }
        if (current.failed) {
            messages.remove(current);
            lastRenderSignature = "";
            renderMessages(true);
            return;
        }
        current.operationPending = true;
        current.deleting = true;
        lastRenderSignature = "";
        renderMessages(true);
        long operationAt = System.currentTimeMillis();
        VrProto.Message operationMessage = current;
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = token != null && VrProto.deleteMessage(
                    token, myUid, peerUid, operationMessage, operationAt);
            runOnUiThread(() -> {
                VrProto.Message latest = messageById(messageId);
                if (ok) {
                    if (latest != null) {
                        messages.remove(latest);
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

    private void pickFile() {
        if (peerBlocked) {
            return;
        }
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");
        startActivityForResult(intent, REQ_PICK_FILE);
    }

    private void handlePickedFile(Uri uri) {
        String name = sanitizeFileName(queryFileValue(uri, true));
        String mime = getContentResolver().getType(uri);
        if (mime == null || mime.length() == 0) {
            mime = "application/octet-stream";
        }
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
                message.pending = true;
                messages.add(message);
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

    private String sanitizeFileName(String raw) {
        String name = raw == null ? "file" : raw.trim();
        name = name.replace('/', '_').replace((char) 92, '_');
        StringBuilder clean = new StringBuilder();
        for (int i = 0; i < name.length() && clean.length() < 255; i++) {
            char c = name.charAt(i);
            if (c >= 32 && c != 127) {
                clean.append(c);
            }
        }
        return clean.length() == 0 ? "file" : clean.toString();
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
        if (message.localUri.length() == 0) {
            message.pending = false;
            message.failed = true;
            renderMessages(true);
            return;
        }
        Uri uri = Uri.parse(message.localUri);
        executor.execute(() -> {
            String token = ensureToken();
            boolean ok = token != null && VrProto.sendFile(token, myUid, peerUid,
                    message,
                    () -> getContentResolver().openInputStream(uri),
                    percent -> runOnUiThread(
                            () -> updateProgressById(message.id, percent, false)));
            runOnUiThread(() -> {
                for (VrProto.Message m : messages) {
                    if (m.id.equals(message.id)) {
                        m.pending = false;
                        m.failed = !ok;
                        m.progress = ok ? 100 : m.progress;
                        break;
                    }
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
        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType(message.mimeType.length() == 0
                ? "*/*" : message.mimeType);
        intent.putExtra(Intent.EXTRA_TITLE, message.fileName);
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
            int sample = 1;
            while (bounds.outWidth / sample > 1800
                    || bounds.outHeight / sample > 1800) {
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
            boolean uploaded = token != null && VrProto.uploadWallpaper(
                    token, myUid, peerUid, wallpaperId, image, suggest, now);
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
                sent = VrProto.sendWallpaper(token, myUid, peerUid, message);
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
            byte[] image = ok ? VrProto.loadWallpaper(token,
                    message.wallpaperOwnerUid, message.wallpaperId) : null;
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
            byte[] image = token == null ? null
                    : VrProto.loadWallpaper(token, ownerUid, wallpaperId);
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
        if (image == null || image.length == 0) {
            return null;
        }
        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        BitmapFactory.decodeByteArray(image, 0, image.length, bounds);
        int sample = 1;
        while (bounds.outWidth / sample > 1200
                || bounds.outHeight / sample > 1200) {
            sample *= 2;
        }
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inSampleSize = sample;
        return BitmapFactory.decodeByteArray(image, 0, image.length, options);
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
                    .append(m.receiptPending ? 'q' : 'z');
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
                ? (isDark() ? R.drawable.bg_glass_card : R.drawable.bg_message_mine)
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
        int mainColor = mine && !isDark() ? Color.WHITE : getColor(R.color.text_main);
        int secondaryColor = mine && !isDark()
                ? Color.rgb(215, 215, 215) : getColor(R.color.text_secondary);
        if (VrProto.TYPE_FILE.equals(message.type)) {
            addFileContent(bubble, message, maxBubbleWidth, mainColor, secondaryColor);
        } else if (VrProto.TYPE_WALLPAPER.equals(message.type)) {
            addWallpaperContent(bubble, message, mine, maxBubbleWidth,
                    mainColor, secondaryColor);
        } else {
            TextView text = new TextView(this);
            text.setText(message.text);
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

        PressFx.attach(bubble);
        row.addView(bubble, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT));
        return row;
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
