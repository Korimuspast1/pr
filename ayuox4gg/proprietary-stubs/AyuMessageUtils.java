/*
 * This is the source code of AyuGram for Android.
 *
 * We do not and cannot prevent the use of our code,
 * but be respectful and credit the original author.
 *
 * Copyright @Radolyn, 2023
 */

package com.radolyn.ayugram.proprietary;

import com.radolyn.ayugram.database.entities.AyuMessageBase;
import com.radolyn.ayugram.database.entities.DeletedMessage;
import com.radolyn.ayugram.database.entities.EditedMessage;
import com.radolyn.ayugram.messages.AyuSavePreferences;
import org.telegram.messenger.FileLog;
import org.telegram.tgnet.SerializedData;
import org.telegram.tgnet.TLRPC;

/**
 * ayuox4gg — public stand-in for the private {@code AyuMessageUtils} that ships
 * in the closed-source {@code AyuGram4A_proprietary} submodule.
 *
 * <p><b>Why this file exists.</b> The upstream repository declares
 * {@code TMessagesProj/src/main/java/com/radolyn/ayugram/proprietary} as a git
 * submodule pointing at a private repository. Three public files call 8 methods
 * from it, so the project cannot compile without it. This implementation makes
 * the tree buildable using only public Telegram classes.
 *
 * <p><b>What works:</b> text, links, reply/forward metadata, bot/instant/silent
 * flags, topic ids, dates, and the reference to the original message inside the
 * client cache. Deleted and edited messages are therefore saved, listed and
 * rendered correctly for text content.
 *
 * <p><b>What is deliberately reduced:</b> media (documents, photos, thumbs,
 * stickers, voice) is <em>not</em> re-materialised. The original code serialises
 * {@code TLRPC.Document}/{@code Photo} graphs and copies files into the AyuGram
 * attachments folder; doing that from public code would mean re-implementing
 * Telegram's TL serialisation graph. Instead, media messages are still recorded
 * (so they show up in history/lists with their flags and captions), but the
 * attachment itself is not preserved. Calls are no-ops rather than throws, so a
 * media message never breaks the history screen.
 *
 * <p>Replacing this file with the real proprietary implementation instantly
 * restores full media support — the signatures below are the ones the public
 * code expects, verified against every call site in the tree.
 */
public final class AyuMessageUtils {

    private AyuMessageUtils() {
    }

    // ------------------------------------------------------------------
    // Inbound: client objects -> database entities
    // ------------------------------------------------------------------

    /**
     * Copy message metadata from the client into a database row.
     *
     * @param prefs    captured message preferences (account, user, dialog, id…)
     * @param message  target row ({@link DeletedMessage} or {@link EditedMessage})
     */
    public static void map(AyuSavePreferences prefs, AyuMessageBase message) {
        if (prefs == null || message == null) {
            return;
        }
        try {
            final TLRPC.Message original = prefs.getMessage();

            message.userId = prefs.getUserId();
            message.dialogId = prefs.getDialogId();
            message.messageId = prefs.getMessageId();
            message.topicId = prefs.getTopicId();
            message.entityCreateDate = prefs.getRequestCatchTime();

            if (!(message instanceof DeletedMessage)) {
                // Revisions are stamped with the edit time; deletions keep the
                // moment the client noticed them (already set above).
                message.entityCreateDate = prefs.getRequestCatchTime();
            }

            if (original == null) {
                return;
            }

            message.date = original.date;
            message.editDate = original.edit_date;
            message.views = original.views;
            message.groupedId = original.grouped_id;
            message.flags = original.flags;

            // Author: from_id for channels/groups, peer_id for private chats.
            message.fromId = original.from_id != null ? getPeerId(original.from_id) : 0;
            message.peerId = original.peer_id != null ? getPeerId(original.peer_id) : 0;

            // Forward header.
            if (original.fwd_from != null) {
                message.fwdFlags = original.flags;
                message.fwdDate = original.fwd_from.date;
                message.fwdName = original.fwd_from.from_name;
                message.fwdPostAuthor = original.fwd_from.post_author;
                message.fwdFromId = original.fwd_from.from_id != null
                        ? getPeerId(original.fwd_from.from_id) : 0;
            }

            // Reply header.
            if (original.reply_to != null) {
                message.replyFlags = original.reply_to.flags;
                message.replyMessageId = original.reply_to.reply_to_msg_id;
                message.replyTopId = original.reply_to.reply_to_top_id;
                message.replyForumTopic = original.reply_to.forum_topic;
                message.replyPeerId = original.reply_to.reply_to_peer_id != null
                        ? getPeerId(original.reply_to.reply_to_peer_id) : 0;
            }

            // Text + entities, in the form the history screen renders.
            message.text = original.message;
            message.textEntities = serialize(original.entities);
        } catch (Throwable t) {
            // History capture must never break message processing.
            FileLog.e("ayuox4gg: AyuMessageUtils.map failed", t);
        }
    }

    /**
     * Copy media metadata.
     *
     * <p>Reduced implementation: records the document type / mime type when a
     * document is present so lists can show a sensible icon, and intentionally
     * skips file copying and thumbnail serialisation.
     *
     * @param prefs        captured message preferences
     * @param message      target row
     * @param copyFiles    the caller's hint that attachment files changed;
     *                     ignored here, see the class javadoc
     */
    public static void map(AyuSavePreferences prefs, AyuMessageBase message, boolean copyFiles) {
        map(prefs, message);
        mapMedia(prefs, message, copyFiles);
    }

    /** Media variant for the {@code AyuSavePreferences} source. */
    public static void mapMedia(AyuSavePreferences prefs, AyuMessageBase message, boolean copyFiles) {
        if (prefs == null || message == null) {
            return;
        }
        try {
            final TLRPC.Message original = prefs.getMessage();
            if (original == null || original.media == null) {
                return;
            }
            if (original.media.document != null) {
                message.mimeType = original.media.document.mime_type;
                message.documentType = classify(original.media.document.mime_type);
            } else if (original.media.photo != null) {
                message.documentType = 0; // photo
            }
        } catch (Throwable t) {
            FileLog.e("ayuox4gg: AyuMessageUtils.mapMedia failed", t);
        }
    }

    // ------------------------------------------------------------------
    // Outbound: database entities -> client objects
    // ------------------------------------------------------------------

    /**
     * Rebuild a {@code TLRPC.Message} from a stored row so the history screen
     * can render it. Signature used by {@code AyuMessageHistory}.
     */
    public static void map(EditedMessage editedMessage, TLRPC.Message target, int currentAccount) {
        if (editedMessage == null || target == null) {
            return;
        }
        try {
            target.id = editedMessage.messageId;
            target.dialog_id = editedMessage.dialogId;
            target.date = editedMessage.entityCreateDate;
            target.edit_date = editedMessage.editDate;
            target.views = editedMessage.views;
            target.grouped_id = editedMessage.groupedId;
            target.flags = editedMessage.flags;
            target.message = editedMessage.text;
            target.entities = deserializeEntities(editedMessage.textEntities);

            if (editedMessage.fromId != 0) {
                target.from_id = new TLRPC.TL_peerUser();
                target.from_id.user_id = editedMessage.fromId;
            }
            if (editedMessage.peerId != 0) {
                target.peer_id = new TLRPC.TL_peerUser();
                target.peer_id.user_id = editedMessage.peerId;
            }

            if (editedMessage.replyMessageId != 0 || editedMessage.replyTopId != 0) {
                target.reply_to = new TLRPC.TL_messageReplyHeader();
                target.reply_to.reply_to_msg_id = editedMessage.replyMessageId;
                target.reply_to.reply_to_top_id = editedMessage.replyTopId;
                target.reply_to.forum_topic = editedMessage.replyForumTopic;
                target.reply_to.flags = editedMessage.replyFlags;
            }

            if (editedMessage.fwdDate != 0 || editedMessage.fwdFromId != 0
                    || editedMessage.fwdName != null) {
                target.fwd_from = new TLRPC.TL_messageFwdHeader();
                target.fwd_from.date = editedMessage.fwdDate;
                target.fwd_from.from_name = editedMessage.fwdName;
                target.fwd_from.post_author = editedMessage.fwdPostAuthor;
                if (editedMessage.fwdFromId != 0) {
                    target.fwd_from.from_id = new TLRPC.TL_peerUser();
                    target.fwd_from.from_id.user_id = editedMessage.fwdFromId;
                }
            }
        } catch (Throwable t) {
            FileLog.e("ayuox4gg: AyuMessageUtils.map(entity) failed", t);
        }
    }

    /** Signature used by {@code AyuMessageHistory} right after {@link #map}. */
    public static void mapMedia(EditedMessage editedMessage, TLRPC.Message target) {
        if (editedMessage == null || target == null) {
            return;
        }
        // Reduced implementation: the media graph is not re-materialised, so
        // the message renders as text/caption only. See the class javadoc.
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private static long getPeerId(TLRPC.Peer peer) {
        if (peer == null) {
            return 0;
        }
        if (peer.user_id != 0) {
            return peer.user_id;
        }
        if (peer.chat_id != 0) {
            return -peer.chat_id;
        }
        if (peer.channel_id != 0) {
            return -peer.channel_id;
        }
        return 0;
    }

    private static byte[] serialize(java.util.ArrayList<TLRPC.MessageEntity> entities) {
        if (entities == null || entities.isEmpty()) {
            return null;
        }
        SerializedData data = new SerializedData();
        try {
            data.writeInt32(entities.size());
            for (TLRPC.MessageEntity entity : entities) {
                entity.serializeToStream(data);
            }
            return data.toByteArray();
        } finally {
            data.cleanup();
        }
    }

    private static java.util.ArrayList<TLRPC.MessageEntity> deserializeEntities(byte[] bytes) {
        if (bytes == null || bytes.length == 0) {
            return null;
        }
        SerializedData data = new SerializedData(bytes);
        try {
            int count = data.readInt32(false);
            if (count <= 0 || count > 10_000) {
                return null;
            }
            java.util.ArrayList<TLRPC.MessageEntity> entities = new java.util.ArrayList<>(count);
            for (int i = 0; i < count; i++) {
                TLRPC.MessageEntity entity = TLRPC.MessageEntity.TLdeserialize(data, data.readInt32(false), false);
                if (entity == null) {
                    return null;
                }
                entities.add(entity);
            }
            return entities;
        } catch (Throwable t) {
            FileLog.e("ayuox4gg: could not deserialize text entities", t);
            return null;
        } finally {
            data.cleanup();
        }
    }

    /** Very small mime -> AyuGram document type mapping; 0 = generic/photo. */
    private static int classify(String mimeType) {
        if (mimeType == null) {
            return 0;
        }
        if (mimeType.startsWith("image/")) {
            return 1;
        }
        if (mimeType.startsWith("video/")) {
            return 2;
        }
        if (mimeType.startsWith("audio/")) {
            return 3;
        }
        return 4;
    }
}
