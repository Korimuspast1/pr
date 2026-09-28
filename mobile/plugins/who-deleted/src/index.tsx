/*
 * WhoDeleted — local delete attribution helper for Discord Android mods.
 * Discord does not normally send the real deleter, so this plugin labels it as
 * exact when it can (your manual deletes / explicit event fields) and otherwise
 * as a best-effort guess.
 */

import { findByProps, findByStoreName } from "@vendetta/metro";
import { FluxDispatcher, React, ReactNative, clipboard } from "@vendetta/metro/common";
import { before } from "@vendetta/patcher";
import { storage } from "@vendetta/plugin";
import { useProxy } from "@vendetta/storage";
import { Forms } from "@vendetta/ui/components";
import { showToast } from "@vendetta/ui/toasts";
import { getAssetIDByName } from "@vendetta/ui/assets";

const { ScrollView, Text } = ReactNative;
const { FormSection, FormRow, FormSwitchRow, FormInput } = Forms;

const MessageStore = findByStoreName("MessageStore") || findByProps("getMessage", "getMessages");
const ChannelStore = findByStoreName("ChannelStore") || findByProps("getChannel", "getDMFromUserId");
const UserStore = findByStoreName("UserStore") || findByProps("getCurrentUser");
const AuthStore = findByStoreName("AuthenticationStore") || findByProps("getId", "getToken");
const MessageActions = findByProps("deleteMessage", "startEditMessage") || findByProps("deleteMessage");

const cleanups: Array<() => void> = [];
const manualDeletes = new Set<string>();

const CACHE_CAP = 1200;
const LOG_CAP = 300;

type Snap = {
    id: string;
    channelId: string;
    guildId: string;
    authorId: string;
    authorName: string;
    content: string;
    bot: boolean;
    at: number;
};

type DeleteLog = Snap & {
    actor: string;
    certainty: "exact" | "likely" | "unknown";
    bulk: boolean;
    loggedAt: number;
};

function initDefaults() {
    storage.enabled ??= true;
    storage.toastDeletes ??= true;
    storage.logDMs ??= true;
    storage.logServers ??= true;
    storage.ignoreBots ??= false;
    storage.ignoreSelf ??= false;
    storage.ignoreManualDeletes ??= false;
    storage.cache ??= {};
    storage.log ??= [];
    storage.search ??= "";
}

initDefaults();

function notify(message: string, icon = "ic_message_delete") {
    try { showToast(message, getAssetIDByName(icon)); }
    catch { showToast(message); }
}

function myId() {
    try { return UserStore?.getCurrentUser?.()?.id || AuthStore?.getId?.() || ""; }
    catch { return ""; }
}

function channelOf(channelId: string) {
    try { return ChannelStore?.getChannel?.(channelId); }
    catch { return undefined; }
}

function guildOf(channelId: string) {
    const ch = channelOf(channelId);
    return String(ch?.guild_id || ch?.guildId || "");
}

function isDM(channelId: string) {
    return !guildOf(channelId);
}

function readCache(): Record<string, Snap> {
    return storage.cache && typeof storage.cache === "object" ? storage.cache : {};
}

function writeCache(cache: Record<string, Snap>) {
    const keys = Object.keys(cache);
    if (keys.length > CACHE_CAP) {
        for (const key of keys.slice(0, keys.length - CACHE_CAP)) delete cache[key];
    }
    storage.cache = cache;
}

function readLog(): DeleteLog[] {
    return Array.isArray(storage.log) ? storage.log : [];
}

function writeLog(log: DeleteLog[]) {
    storage.log = log.slice(0, LOG_CAP);
}

function shouldSkip(snap: Partial<Snap>) {
    const dm = isDM(String(snap.channelId || ""));
    if (dm && !storage.logDMs) return true;
    if (!dm && !storage.logServers) return true;
    if (storage.ignoreBots && snap.bot) return true;
    if (storage.ignoreSelf && snap.authorId && snap.authorId === myId()) return true;
    return false;
}

function snapFromMessage(message: any): Snap | null {
    try {
        if (!message?.id || !(message.channel_id || message.channelId)) return null;
        const channelId = String(message.channel_id || message.channelId);
        return {
            id: String(message.id),
            channelId,
            guildId: String(message.guild_id || message.guildId || guildOf(channelId)),
            authorId: String(message.author?.id || ""),
            authorName: String(message.author?.global_name || message.author?.globalName || message.author?.username || "unknown"),
            content: String(message.content || ""),
            bot: !!message.author?.bot,
            at: Date.now(),
        };
    } catch {
        return null;
    }
}

function getMessage(channelId: string, messageId: string) {
    try {
        const direct = MessageStore?.getMessage?.(channelId, messageId);
        if (direct) return direct;
    } catch { }
    try {
        const messages = MessageStore?.getMessages?.(channelId);
        if (messages?.get) return messages.get(messageId);
        if (Array.isArray(messages)) return messages.find((m: any) => m?.id === messageId);
    } catch { }
    return undefined;
}

function cacheMessage(message: any) {
    const snap = snapFromMessage(message);
    if (!snap) return;
    if (shouldSkip(snap)) return;
    const cache = readCache();
    cache[snap.id] = snap;
    writeCache(cache);
}

function explicitActor(ev: any) {
    const id = ev?.deletedBy || ev?.deleted_by || ev?.deleterId || ev?.deleter_id || ev?.userId || ev?.user_id;
    if (!id) return null;
    if (String(id) === myId()) return { actor: "you", certainty: "exact" as const };
    return { actor: `user ${id}`, certainty: "exact" as const };
}

function guessActor(snap: Snap | Partial<Snap>, manual: boolean, bulk: boolean) {
    const explicit = explicitActor(snap as any);
    if (explicit) return explicit;
    if (manual) return { actor: "you", certainty: "exact" as const };
    if (bulk) return { actor: "moderator / bot bulk delete", certainty: "likely" as const };
    if (isDM(String(snap.channelId || ""))) return { actor: "message author", certainty: "likely" as const };
    if (snap.authorId && snap.authorId === myId()) return { actor: "you or a moderator", certainty: "likely" as const };
    return { actor: "author, moderator, or bot", certainty: "unknown" as const };
}

function logDelete(channelId: string, messageId: string, bulk = false, eventObj?: any) {
    if (!storage.enabled || !messageId || !channelId) return;

    const manual = manualDeletes.has(messageId);
    manualDeletes.delete(messageId);
    if (manual && storage.ignoreManualDeletes) return;

    const live = getMessage(channelId, messageId);
    const cache = readCache();
    const snap = snapFromMessage(live) || cache[messageId] || {
        id: messageId,
        channelId,
        guildId: guildOf(channelId),
        authorId: "",
        authorName: "unknown",
        content: "",
        bot: false,
        at: Date.now(),
    };
    if (shouldSkip(snap)) return;

    delete cache[messageId];
    writeCache(cache);

    const actorInfo = explicitActor(eventObj) || guessActor(snap, manual, bulk);
    const entry: DeleteLog = {
        ...snap,
        channelId,
        guildId: snap.guildId || guildOf(channelId),
        actor: actorInfo.actor,
        certainty: actorInfo.certainty,
        bulk,
        loggedAt: Date.now(),
    };
    writeLog([entry, ...readLog()]);

    if (storage.toastDeletes) {
        notify(`Deleted by ${entry.actor}: ${entry.authorName}${entry.content ? ` — ${entry.content.slice(0, 60)}` : ""}`);
    }
}

function fmtTime(ms: number) {
    try { return new Date(ms).toLocaleString(); }
    catch { return ""; }
}

function copyLog(entry: DeleteLog) {
    const text = [
        `Who deleted: ${entry.actor} (${entry.certainty})`,
        `Author: ${entry.authorName} (${entry.authorId})`,
        `Channel: ${entry.channelId}`,
        `Server: ${entry.guildId || "DM"}`,
        `At: ${fmtTime(entry.loggedAt)}`,
        "",
        entry.content || "(no text cached)",
    ].join("\n");
    try {
        clipboard.setString(text);
        notify("Delete log copied", "copy");
    } catch {
        notify("Copy failed", "small");
    }
}

function Settings() {
    useProxy(storage);
    const search = String(storage.search || "").toLowerCase();
    const log = readLog().filter(e => !search || `${e.authorName} ${e.actor} ${e.content} ${e.channelId} ${e.guildId}`.toLowerCase().includes(search));
    const hint = { opacity: 0.7, marginHorizontal: 12, marginVertical: 6 } as any;

    return (
        <ScrollView style={{ flex: 1 }}>
            <FormSection title="WhoDeleted">
                <FormSwitchRow label="Enabled" value={!!storage.enabled} onValueChange={(v: boolean) => { storage.enabled = v; }} />
                <FormSwitchRow label="Toast when a message is deleted" value={!!storage.toastDeletes} onValueChange={(v: boolean) => { storage.toastDeletes = v; }} />
                <FormSwitchRow label="Log DMs" value={!!storage.logDMs} onValueChange={(v: boolean) => { storage.logDMs = v; }} />
                <FormSwitchRow label="Log servers" value={!!storage.logServers} onValueChange={(v: boolean) => { storage.logServers = v; }} />
                <Text style={hint}>Discord rarely exposes the exact deleter. Exact = your own manual delete or explicit event field; otherwise this is best-effort.</Text>
            </FormSection>

            <FormSection title="Ignore">
                <FormSwitchRow label="Ignore bots" value={!!storage.ignoreBots} onValueChange={(v: boolean) => { storage.ignoreBots = v; }} />
                <FormSwitchRow label="Ignore my messages" value={!!storage.ignoreSelf} onValueChange={(v: boolean) => { storage.ignoreSelf = v; }} />
                <FormSwitchRow label="Ignore messages I delete manually" value={!!storage.ignoreManualDeletes} onValueChange={(v: boolean) => { storage.ignoreManualDeletes = v; }} />
            </FormSection>

            <FormSection title={`Log (${log.length})`}>
                <FormInput title="" placeholder="Search author/text/channel" value={String(storage.search || "")} onChange={(v: string) => { storage.search = v; }} />
                <FormRow label="Clear log" subLabel={`${readLog().length} saved`} onPress={() => { storage.log = []; notify("WhoDeleted log cleared", "ic_message_retry"); }} trailing={FormRow.Arrow} />
                {log.slice(0, 80).map(e => (
                    <FormRow
                        key={`${e.id}-${e.loggedAt}`}
                        label={`${e.actor} (${e.certainty})`}
                        subLabel={`${e.authorName}: ${(e.content || "no text").slice(0, 100)} — ${fmtTime(e.loggedAt)}`}
                        onPress={() => copyLog(e)}
                        trailing={FormRow.Arrow}
                    />
                ))}
            </FormSection>
        </ScrollView>
    );
}

export default {
    onLoad: () => {
        initDefaults();

        if (MessageActions?.deleteMessage) {
            cleanups.push(before("deleteMessage", MessageActions, (args: any[]) => {
                const id = String(args?.[1] || "");
                if (id) manualDeletes.add(id);
                return args;
            }));
        }

        cleanups.push(before("dispatch", FluxDispatcher as any, (args: any[]) => {
            const ev = args?.[0];
            if (!ev?.type) return args;
            try {
                if (ev.type === "MESSAGE_CREATE" && ev.message) cacheMessage(ev.message);
                if (ev.type === "MESSAGE_UPDATE" && ev.message) cacheMessage(ev.message);
                if (ev.type === "MESSAGE_DELETE") {
                    const id = String(ev.id || ev.messageId || ev.message?.id || "");
                    const ch = String(ev.channelId || ev.channel_id || ev.message?.channel_id || ev.message?.channelId || "");
                    logDelete(ch, id, false, ev);
                }
                if (ev.type === "MESSAGE_DELETE_BULK") {
                    const ch = String(ev.channelId || ev.channel_id || "");
                    for (const id of ev.ids || []) logDelete(ch, String(id), true, ev);
                }
            } catch (e) {
                console.error("[WhoDeleted] dispatch error", e);
            }
            return args;
        }));

        notify("WhoDeleted loaded");
    },
    onUnload: () => {
        while (cleanups.length) {
            try { cleanups.pop()?.(); } catch { }
        }
        manualDeletes.clear();
    },
    settings: Settings,
};
