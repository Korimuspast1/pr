/*
 * This is the source code of AyuGram for Android.
 *
 * We do not and cannot prevent the use of our code,
 * but be respectful and credit the original author.
 *
 * Copyright @Radolyn, 2023
 */

package com.radolyn.ayugram.proprietary;

import android.util.Pair;

import org.telegram.messenger.FileLog;
import org.telegram.messenger.MessageObject;

import java.util.ArrayList;
import java.util.HashMap;

/**
 * ayuox4gg — public stand-in for the private {@code AyuHistoryHook} from the
 * closed {@code AyuGram4A_proprietary} submodule.
 *
 * <p>The real class splices AyuGram's locally saved (deleted) messages into the
 * message list Telegram is about to render in {@code ChatActivity}, which is how
 * the fork shows deleted messages inside the conversation instead of only in a
 * separate history screen.
 *
 * <p><b>This implementation is a safe no-op with a correct signature.</b> The
 * splice needs the private DAO queries and the media mapping that only the
 * proprietary code has; faking it would mean inventing message ids and risking
 * corrupting the rendered chat (the caller has already closed over
 * {@code messagesDict}, {@code startId}/{@code endId} and the load limit, so a
 * wrong insert changes scroll position and pagination).
 *
 * <p>Result: with this stub the project compiles, the deleted-message
 * <em>database</em> still fills, and the separate history screen still works;
 * only the in-chat splice is disabled. Swapping in the proprietary class
 * restores it without touching any other file.
 *
 * <p>Signatures verified against the two call sites in
 * {@code org.telegram.ui.ChatActivity} (lines ~15808 and ~15902 in the
 * {@code rewrite} branch).
 */
public final class AyuHistoryHook {

    private AyuHistoryHook() {
    }

    /**
     * Range of message ids loaded in this batch.
     *
     * @param messages the batch Telegram just loaded, never null
     * @return {@code first} = smallest id, {@code second} = largest id
     */
    public static Pair<Integer, Integer> getMinAndMaxIds(ArrayList<MessageObject> messages) {
        int min = Integer.MAX_VALUE;
        int max = Integer.MIN_VALUE;
        if (messages != null) {
            for (MessageObject object : messages) {
                if (object == null || object.messageOwner == null) {
                    continue;
                }
                int id = object.messageOwner.id;
                if (id < min) {
                    min = id;
                }
                if (id > max) {
                    max = id;
                }
            }
        }
        if (min == Integer.MAX_VALUE) {
            // Empty batch: ChatActivity treats an unchanged range as "nothing to do".
            min = 0;
            max = 0;
        }
        return new Pair<>(min, max);
    }

    /**
     * Splice locally stored messages into the loaded batch.
     *
     * <p>Reduced implementation: logs once per call at debug level and returns,
     * leaving the batch exactly as Telegram produced it.
     */
    public static void doHook(
            int currentAccount,
            ArrayList<MessageObject> messages,
            HashMap<Integer, MessageObject> messagesDict,
            int startId,
            int endId,
            long dialogId,
            int limit,
            int topicId,
            boolean isSecretChat
    ) {
        if (messages == null || messagesDict == null) {
            return;
        }
        // Secret chats must never be touched: their messages are not persisted
        // by AyuGram anyway, and mixing them would leak ids across contexts.
        if (isSecretChat) {
            return;
        }
        try {
            FileLog.d("ayuox4gg: AyuHistoryHook.doHook stub — in-chat deleted messages disabled "
                    + "(dialog=" + dialogId + ", range=" + startId + ".." + endId
                    + ", limit=" + limit + ", topic=" + topicId + ")");
        } catch (Throwable ignored) {
            // Never let logging break message loading.
        }
    }
}
