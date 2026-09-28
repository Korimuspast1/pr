/*
 * DeletedMessageMarker — AyuGram-style inline delete/edit marker for Discord Android mods.
 * Target loaders: Revenge / Bunny / Vendetta-compatible clients.
 * Local-only: nothing is uploaded anywhere. It can only preserve messages your client already received.
 */

import { findByProps, findByStoreName, findByName } from "@vendetta/metro";
import { FluxDispatcher, React, ReactNative } from "@vendetta/metro/common";
import { before, after } from "@vendetta/patcher";
import { storage } from "@vendetta/plugin";
import { useProxy } from "@vendetta/storage";
import { Forms } from "@vendetta/ui/components";
import { showToast } from "@vendetta/ui/toasts";
import { getAssetIDByName } from "@vendetta/ui/assets";

const { ScrollView, Text } = ReactNative;
const { FormSection, FormRow, FormSwitchRow, FormInput } = Forms;

const MAX_TRACKED = 1500;

const deletedMessages = new Map<string, { channelId: string; at: number; }>();
const editedMessages = new Map<string, { channelId: string; at: number; oldContent: string; }>();
const manualDeletes = new Set<string>();
const cleanups: Array<() => void> = [];

const ChannelStore = findByStoreName("ChannelStore") || findByProps("getChannel", "getDMFromUserId");
const MessageStore = findByStoreName("MessageStore") || findByProps("getMessage", "getMessages");
const UserStore = findByStoreName("UserStore") || findByProps("getCurrentUser");
const AuthStore = findByStoreName("AuthenticationStore") || findByProps("getId", "getToken");
const MessageActions = findByProps("deleteMessage", "startEditMessage") || findByProps("deleteMessage");
const ChannelMessages = findByProps("_channelMessages") || rawFind(m => m && m._channelMessages !== undefined);
const RowManager = findByName("RowManager", false) || (findByProps("RowManager")?.RowManager);

function initDefaults() {
    storage.enabled ??= true;
    storage.logDeleted ??= true;
    storage.logEdited ??= true;
    storage.highlightRows ??= true;
    storage.preserveMedia ??= true;
    storage.ignoreBots ??= false;
    storage.ignoreSelf ??= false;
    storage.ignoreManualDeletes ??= true;
    storage.showInDMs ??= true;
    storage.showInServers ??= true;
    storage.deletedText ??= "deleted";
    storage.editedText ??= "edited";
}

initDefaults();

function rawFind(predicate: (m: any) => boolean) {
    try {
        const mods = (globalThis as any).modules ?? (window as any).modules;
        if (!mods) return undefined;
        for (const id in mods) {
            const def = mods[id];
            if (!def?.isInitialized) continue;
            const exports = def.publicModule?.exports;
            if (!exports) continue;
            try {
                if (predicate(exports)) return exports;
                if (exports.default != null && predicate(exports.default)) return exports.default;
            } catch { /* ignore module */ }
        }
    } catch { /* ignore */ }
    return undefined;
}

function notify(message: string, icon = "check") {
    try { showToast(message, getAssetIDByName(icon)); }
    catch { showToast(message); }
}

function trimMap(map: Map<any, any>) {
    while (map.size > MAX_TRACKED) {
        const first = map.keys().next().value;
        if (first === undefined) break;
        map.delete(first);
    }
}

function currentUserId() {
    try {
        return UserStore?.getCurrentUser?.()?.id || AuthStore?.getId?.() || "";
    } catch {
        return "";
    }
}

function getChannel(channelId: string) {
    try { return ChannelStore?.getChannel?.(channelId); }
    catch { return undefined; }
}

function isDMChannel(channelId: string) {
    const ch = getChannel(channelId);
    return !ch?.guild_id && !ch?.guildId;
}

function shouldSkip(channelId: string, message?: any) {
    if (!storage.enabled) return true;
    const isDM = isDMChannel(channelId);
    if (isDM && !storage.showInDMs) return true;
    if (!isDM && !storage.showInServers) return true;
    const author = message?.author;
    if (storage.ignoreBots && author?.bot) return true;
    if (storage.ignoreSelf && author?.id && author.id === currentUserId()) return true;
    return false;
}

function getOriginalMessage(channelId: string, messageId: string) {
    if (!channelId || !messageId) return undefined;

    try {
        const direct = MessageStore?.getMessage?.(channelId, messageId);
        if (direct) return direct;
    } catch { /* ignore */ }

    try {
        const messages = MessageStore?.getMessages?.(channelId);
        if (messages?.get) {
            const msg = messages.get(messageId);
            if (msg) return msg;
        }
        if (Array.isArray(messages)) {
            const msg = messages.find((m: any) => m?.id === messageId);
            if (msg) return msg;
        }
    } catch { /* ignore */ }

    try {
        const channelMessages = ChannelMessages?._channelMessages?.[channelId] ?? ChannelMessages?.get?.(channelId);
        if (channelMessages?.get) {
            const msg = channelMessages.get(messageId);
            if (msg) return msg;
        }
        if (channelMessages?._map?.[messageId]) return channelMessages._map[messageId];
        if (Array.isArray(channelMessages?._array)) return channelMessages._array.find((m: any) => m?.id === messageId);
    } catch { /* ignore */ }

    return undefined;
}

function authorToGateway(author: any) {
    if (!author || typeof author !== "object") return author;
    return {
        id: author.id,
        username: author.username || "Unknown",
        discriminator: author.discriminator && author.discriminator !== "???" ? String(author.discriminator) : "0",
        avatar: author.avatar ?? null,
        avatar_decoration_data: author.avatarDecorationData ?? author.avatar_decoration_data ?? null,
        bot: !!author.bot,
        global_name: author.globalName || author.global_name || author.username || "Unknown",
    };
}

function embedToGateway(embed: any) {
    if (!embed || typeof embed !== "object") return embed;
    return {
        ...embed,
        title: embed.rawTitle ?? embed.title,
        description: embed.rawDescription ?? embed.description,
        fields: Array.isArray(embed.fields)
            ? embed.fields.map((f: any) => ({
                name: f.rawName ?? f.name ?? "",
                value: f.rawValue ?? f.value ?? "",
                inline: !!f.inline,
            }))
            : embed.fields,
    };
}

function recordToGateway(record: any) {
    if (!record || typeof record !== "object") return record;
    return {
        ...record,
        id: record.id,
        channel_id: record.channel_id || record.channelId,
        guild_id: record.guild_id || record.guildId,
        content: record.content ?? "",
        author: authorToGateway(record.author),
        embeds: Array.isArray(record.embeds) ? record.embeds.map(embedToGateway) : record.embeds,
        attachments: Array.isArray(record.attachments) ? record.attachments : [],
        sticker_items: record.sticker_items || record.stickers || [],
    };
}

function mergeAttachments(original: any, updated: any) {
    const oldList = Array.isArray(original?.attachments) ? original.attachments : [];
    const newList = Array.isArray(updated?.attachments) ? updated.attachments : [];
    if (!storage.preserveMedia) return newList;
    if (!oldList.length) return newList;
    if (!newList.length) return oldList;
    const byId = new Map<string, any>();
    for (const a of oldList) if (a?.id) byId.set(String(a.id), a);
    for (const a of newList) if (a?.id) byId.set(String(a.id), a);
    return Array.from(byId.values());
}

function deletedLabel() {
    return String(storage.deletedText || "deleted").trim() || "deleted";
}

function editedLabel() {
    return String(storage.editedText || "edited").trim() || "edited";
}

function blockDeleteWithMark(args: any[], channelId: string, messageId: string) {
    deletedMessages.set(messageId, { channelId, at: Date.now() });
    trimMap(deletedMessages);
    args[0] = {
        type: "MESSAGE_EDIT_FAILED_AUTOMOD",
        messageData: {
            type: 1,
            message: {
                channelId,
                messageId,
            },
        },
        errorResponseBody: {
            code: 200000,
            message: `(${deletedLabel()})`,
        },
    };
    return args;
}

function dispatchDeletedMark(channelId: string, messageId: string) {
    try {
        (FluxDispatcher as any).dispatch({
            type: "MESSAGE_EDIT_FAILED_AUTOMOD",
            messageData: {
                type: 1,
                message: { channelId, messageId },
            },
            errorResponseBody: {
                code: 200000,
                message: `(${deletedLabel()})`,
            },
            __deletedMarkerBypass: true,
        });
    } catch { /* ignore */ }
}

function handleDeleteEvent(args: any[]) {
    const ev = args?.[0];
    if (!ev || ev.__deletedMarkerBypass || ev.type !== "MESSAGE_DELETE") return args;
    if (!storage.logDeleted) return args;

    const messageId = String(ev.id || ev.messageId || ev.message?.id || "");
    const channelId = String(ev.channelId || ev.channel_id || ev.message?.channel_id || ev.message?.channelId || "");
    if (!messageId || !channelId) return args;

    if (manualDeletes.has(messageId)) {
        manualDeletes.delete(messageId);
        if (storage.ignoreManualDeletes) return args;
    }

    const original = getOriginalMessage(channelId, messageId);
    if (shouldSkip(channelId, original)) return args;
    return blockDeleteWithMark(args, channelId, messageId);
}

function handleBulkDeleteEvent(args: any[]) {
    const ev = args?.[0];
    if (!ev || ev.__deletedMarkerBypass || ev.type !== "MESSAGE_DELETE_BULK") return args;
    if (!storage.logDeleted || !Array.isArray(ev.ids)) return args;

    const channelId = String(ev.channelId || ev.channel_id || "");
    if (!channelId) return args;

    let handled = false;
    for (const rawId of ev.ids) {
        const messageId = String(rawId);
        if (!messageId) continue;
        if (manualDeletes.has(messageId)) {
            manualDeletes.delete(messageId);
            if (storage.ignoreManualDeletes) continue;
        }
        const original = getOriginalMessage(channelId, messageId);
        if (shouldSkip(channelId, original)) continue;
        deletedMessages.set(messageId, { channelId, at: Date.now() });
        trimMap(deletedMessages);
        dispatchDeletedMark(channelId, messageId);
        handled = true;
    }

    if (handled) {
        args[0] = { type: "DELETED_MESSAGE_MARKER_BLOCK_BULK", __deletedMarkerBypass: true };
        return args;
    }
    return args;
}

function handleUpdateEvent(args: any[]) {
    const ev = args?.[0];
    if (!ev || ev.__deletedMarkerBypass || ev.type !== "MESSAGE_UPDATE") return args;
    if (!storage.logEdited) return args;

    const msg = ev.message;
    if (!msg?.id || !msg.edited_timestamp || msg.edited_timestamp === "invalid_timestamp") return args;

    const channelId = String(msg.channel_id || msg.channelId || ev.channelId || "");
    const messageId = String(msg.id || ev.id || "");
    if (!channelId || !messageId || deletedMessages.has(messageId)) return args;

    const original = getOriginalMessage(channelId, messageId);
    if (!original || shouldSkip(channelId, original)) return args;

    const oldContent = typeof original.content === "string" ? original.content : "";
    const newContent = typeof msg.content === "string" ? msg.content : "";
    const oldAttachments = Array.isArray(original.attachments) ? original.attachments : [];
    const newAttachments = Array.isArray(msg.attachments) ? msg.attachments : [];

    if (oldContent === newContent && newAttachments.length >= oldAttachments.length) return args;
    if (oldContent.includes(`(${editedLabel()})`) && oldContent.endsWith(newContent)) return args;

    editedMessages.set(messageId, { channelId, at: Date.now(), oldContent });
    trimMap(editedMessages);

    const gatewayOriginal = recordToGateway(original);
    ev.message = {
        ...gatewayOriginal,
        ...msg,
        content: oldContent !== newContent ? `~~${oldContent}~~ \`(${editedLabel()})\`\n${newContent}` : oldContent,
        attachments: mergeAttachments(original, msg),
        guild_id: msg.guild_id || original.guild_id || getChannel(channelId)?.guild_id,
        edited_timestamp: "invalid_timestamp",
        message_reference: msg.message_reference || original.messageReference || original.message_reference || null,
    };
    return args;
}

function mutateRow(row: any) {
    try {
        if (!row || row.type !== 1 || !row.message?.id) return;
        const messageId = String(row.message.id);
        const isDeleted = deletedMessages.has(messageId);
        const isEdited = editedMessages.has(messageId);
        if (!isDeleted && !isEdited) return;

        if (isDeleted) {
            row.message.edited = `(${deletedLabel()})`;
            if (storage.highlightRows) {
                const color = ReactNative.processColor?.("#f04747") ?? "#f04747";
                const bg = ReactNative.processColor?.("#f0474720") ?? "#f0474720";
                row.message.textColor = color;
                row.backgroundHighlight = { backgroundColor: bg, gutterColor: color };
            }
        } else if (isEdited && storage.highlightRows) {
            const color = ReactNative.processColor?.("#faa61a") ?? "#faa61a";
            const bg = ReactNative.processColor?.("#faa61a20") ?? "#faa61a20";
            row.backgroundHighlight = { backgroundColor: bg, gutterColor: color };
        }
    } catch { /* never break rows */ }
}

function hookRows() {
    const applyHook = (target: any) => {
        if (!target || typeof target.updateRows !== "function") return;
        cleanups.push(before("updateRows", target, (args: any[]) => {
            if (!deletedMessages.size && !editedMessages.size) return args;
            const rowsArg = args?.[1];
            if (!rowsArg) return args;
            try {
                if (typeof rowsArg === "string") {
                    const rows = JSON.parse(rowsArg);
                    let changed = false;
                    if (Array.isArray(rows)) {
                        for (const row of rows) {
                            const beforeRow = JSON.stringify(row);
                            mutateRow(row);
                            if (!changed && beforeRow !== JSON.stringify(row)) changed = true;
                        }
                    }
                    if (changed) args[1] = JSON.stringify(rows);
                } else if (Array.isArray(rowsArg)) {
                    for (const row of rowsArg) mutateRow(row);
                } else if (rowsArg && Array.isArray(rowsArg.rows)) {
                    for (const row of rowsArg.rows) mutateRow(row);
                }
            } catch { /* ignore */ }
            return args;
        }));
    };

    applyHook(ReactNative.NativeModules?.DCDChatManager);
    const nativeChat = rawFind(m => m && typeof m.updateRows === "function") || findByProps("updateRows", "getConstants") || findByProps("updateRows");
    applyHook(nativeChat);

    if (RowManager?.prototype?.generate) {
        cleanups.push(after("generate", RowManager.prototype, (_args: any[], rowObj: any) => {
            mutateRow(rowObj?.row ?? rowObj);
        }));
    }
}

function Settings() {
    useProxy(storage);
    const hintStyle = { opacity: 0.7, marginHorizontal: 12, marginVertical: 6 } as any;

    return (
        <ScrollView style={{ flex: 1 }}>
            <FormSection title="DeletedMessageMarker">
                <FormSwitchRow label="Enabled" subLabel="AyuGram-style inline deleted messages" value={!!storage.enabled} onValueChange={(v: boolean) => { storage.enabled = v; }} />
                <FormSwitchRow label="Show deleted messages inline" subLabel="Prevents the row from disappearing and marks it as deleted" value={!!storage.logDeleted} onValueChange={(v: boolean) => { storage.logDeleted = v; }} />
                <FormSwitchRow label="Show edited history inline" subLabel="Displays old text struck through above the new text" value={!!storage.logEdited} onValueChange={(v: boolean) => { storage.logEdited = v; }} />
                <FormSwitchRow label="Red/yellow row highlight" value={!!storage.highlightRows} onValueChange={(v: boolean) => { storage.highlightRows = v; }} />
                <Text style={hintStyle}>Deleted label</Text>
                <FormInput title="" placeholder="deleted" value={String(storage.deletedText ?? "")} onChange={(v: string) => { storage.deletedText = v; }} />
                <Text style={hintStyle}>Edited label</Text>
                <FormInput title="" placeholder="edited" value={String(storage.editedText ?? "")} onChange={(v: string) => { storage.editedText = v; }} />
            </FormSection>

            <FormSection title="Where to show">
                <FormSwitchRow label="DMs / private chats" value={!!storage.showInDMs} onValueChange={(v: boolean) => { storage.showInDMs = v; }} />
                <FormSwitchRow label="Server channels" value={!!storage.showInServers} onValueChange={(v: boolean) => { storage.showInServers = v; }} />
            </FormSection>

            <FormSection title="Ignore / privacy">
                <FormSwitchRow label="Ignore bots" value={!!storage.ignoreBots} onValueChange={(v: boolean) => { storage.ignoreBots = v; }} />
                <FormSwitchRow label="Ignore my own messages" value={!!storage.ignoreSelf} onValueChange={(v: boolean) => { storage.ignoreSelf = v; }} />
                <FormSwitchRow label="Do not preserve messages I delete manually" subLabel="Recommended to avoid your own cleanup coming back" value={!!storage.ignoreManualDeletes} onValueChange={(v: boolean) => { storage.ignoreManualDeletes = v; }} />
                <FormSwitchRow label="Preserve deleted images/media on edits" value={!!storage.preserveMedia} onValueChange={(v: boolean) => { storage.preserveMedia = v; }} />
                <Text style={hintStyle}>Messages stay local and only if your client already received them. It cannot recover old messages from before installing the plugin.</Text>
            </FormSection>

            <FormSection title="Diagnostics">
                <FormRow label="Tracked deleted messages" subLabel={String(deletedMessages.size)} />
                <FormRow label="Tracked edited messages" subLabel={String(editedMessages.size)} />
                <FormRow
                    label="Clear tracked marks"
                    subLabel="Removes highlights until messages are updated again"
                    onPress={() => {
                        const count = deletedMessages.size + editedMessages.size;
                        deletedMessages.clear();
                        editedMessages.clear();
                        notify(`Cleared ${count} mark(s)`, "ic_message_retry");
                    }}
                    trailing={FormRow.Arrow}
                />
            </FormSection>
        </ScrollView>
    );
}

export default {
    onLoad: () => {
        initDefaults();

        if (MessageActions?.deleteMessage) {
            cleanups.push(before("deleteMessage", MessageActions, (args: any[]) => {
                try {
                    const messageId = String(args?.[1] || "");
                    if (messageId) manualDeletes.add(messageId);
                } catch { /* ignore */ }
                return args;
            }));
        }

        if (MessageActions?.startEditMessage) {
            cleanups.push(before("startEditMessage", MessageActions, (args: any[]) => {
                const content = args?.[2];
                if (typeof content === "string" && content.includes(`(${editedLabel()})`)) {
                    args[2] = content.split(/`\([^`]+\)`\n/).pop() ?? content;
                }
                return args;
            }));
        }

        cleanups.push(before("dispatch", FluxDispatcher as any, (args: any[]) => {
            try {
                const ev = args?.[0];
                if (!ev?.type || ev.__deletedMarkerBypass) return args;
                if (ev.type === "MESSAGE_DELETE") return handleDeleteEvent(args);
                if (ev.type === "MESSAGE_DELETE_BULK") return handleBulkDeleteEvent(args);
                if (ev.type === "MESSAGE_UPDATE") return handleUpdateEvent(args);
            } catch (e) {
                console.error("[DeletedMessageMarker] dispatch error", e);
            }
            return args;
        }));

        hookRows();
        notify("DeletedMessageMarker loaded");
    },

    onUnload: () => {
        while (cleanups.length) {
            try { cleanups.pop()?.(); } catch { /* ignore */ }
        }
        deletedMessages.clear();
        editedMessages.clear();
        manualDeletes.clear();
    },

    settings: Settings,
};
