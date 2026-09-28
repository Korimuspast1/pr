/*
 * FakeOfflinePlus — best-effort privacy tools for Discord Android mods.
 * Main reliable feature: blocks outgoing typing events. Status spoofing is local-only unless Discord exposes a compatible status action.
 */

import { findByProps, findByStoreName } from "@vendetta/metro";
import { React, ReactNative } from "@vendetta/metro/common";
import { instead, after } from "@vendetta/patcher";
import { storage } from "@vendetta/plugin";
import { useProxy } from "@vendetta/storage";
import { Forms } from "@vendetta/ui/components";
import { showToast } from "@vendetta/ui/toasts";
import { getAssetIDByName } from "@vendetta/ui/assets";

const { ScrollView, Text } = ReactNative;
const { FormSection, FormRow, FormSwitchRow, FormInput } = Forms;

const TypingModule = findByProps("startTyping") || findByProps("sendTyping") || findByProps("startTyping", "stopTyping");
const ChannelStore = findByStoreName("ChannelStore") || findByProps("getChannel", "getDMFromUserId");
const UserStore = findByStoreName("UserStore") || findByProps("getCurrentUser");
const AuthStore = findByStoreName("AuthenticationStore") || findByProps("getId", "getToken");
const PresenceStore = findByStoreName("PresenceStore") || findByProps("getStatus", "getState");
const StatusActions = findByProps("updateStatus") || findByProps("setStatus") || findByProps("updateRemoteSettings") || findByProps("saveAccountChanges");
const RestAPI = findByProps("get", "post", "patch") || findByProps("patch", "put", "del");

const cleanups: Array<() => void> = [];
let statusLockTimer: any = null;

function initDefaults() {
    storage.enabled ??= true;
    storage.hideTyping ??= true;
    storage.hideInDMs ??= true;
    storage.hideInServers ??= true;
    storage.panicMode ??= false;
    storage.localStatusSpoof ??= false;
    storage.localStatus ??= "invisible";
    storage.realStatusLock ??= false;
    storage.realStatus ??= "invisible";
    storage.lockInterval ??= "5";
    storage.lastRealStatusResult ??= "Not applied yet";
    storage.toastBlockedTyping ??= false;
    storage.blockedTypingCount ??= 0;
    storage.quietHours ??= false;
    storage.quietStart ??= "22";
    storage.quietEnd ??= "8";
    storage.whitelistChannels ??= "";
}

initDefaults();

function notify(message: string, icon = "check") {
    try { showToast(message, getAssetIDByName(icon)); }
    catch { showToast(message); }
}

function currentUserId() {
    try { return UserStore?.getCurrentUser?.()?.id || AuthStore?.getId?.() || ""; }
    catch { return ""; }
}

function getChannelId(args: any[]) {
    const first = args?.[0];
    if (typeof first === "string") return first;
    if (first?.channelId) return String(first.channelId);
    if (first?.channel_id) return String(first.channel_id);
    if (args?.[1]?.channelId) return String(args[1].channelId);
    return "";
}

function channelOf(channelId: string) {
    try { return ChannelStore?.getChannel?.(channelId); }
    catch { return undefined; }
}

function isDM(channelId: string) {
    const ch = channelOf(channelId);
    return !ch?.guild_id && !ch?.guildId;
}

function listIncludes(csv: unknown, value: string) {
    if (!value || typeof csv !== "string") return false;
    return csv.split(",").map(s => s.trim()).filter(Boolean).includes(value);
}

function inQuietHours() {
    if (!storage.quietHours) return false;
    const hour = new Date().getHours();
    const start = Math.max(0, Math.min(23, parseInt(String(storage.quietStart || "22"), 10) || 22));
    const end = Math.max(0, Math.min(23, parseInt(String(storage.quietEnd || "8"), 10) || 8));
    if (start === end) return true;
    if (start < end) return hour >= start && hour < end;
    return hour >= start || hour < end;
}

function shouldBlockTyping(channelId: string) {
    if (!storage.enabled) return false;
    if (!(storage.hideTyping || storage.panicMode || inQuietHours())) return false;
    if (listIncludes(storage.whitelistChannels, channelId)) return false;
    const dm = isDM(channelId);
    if (dm && !storage.hideInDMs) return false;
    if (!dm && !storage.hideInServers) return false;
    return true;
}

function blockTyping(args: any[]) {
    const channelId = getChannelId(args);
    if (!shouldBlockTyping(channelId)) return false;
    storage.blockedTypingCount = (Number(storage.blockedTypingCount) || 0) + 1;
    if (storage.toastBlockedTyping && Number(storage.blockedTypingCount) % 10 === 1) notify("Typing hidden");
    return true;
}

function normalizeStatus(value: unknown) {
    const raw = String(value || "invisible").trim().toLowerCase();
    if (["invisible", "offline", "idle", "dnd", "online"].includes(raw)) return raw === "offline" ? "invisible" : raw;
    return "invisible";
}

function wantedRealStatus() {
    return normalizeStatus(storage.realStatus || storage.localStatus || "invisible");
}

async function trySetRealStatus(showToastResult = true) {
    const status = wantedRealStatus();
    const attempts = [
        () => StatusActions?.updateStatus?.(status),
        () => StatusActions?.updateStatus?.({ status }),
        () => StatusActions?.setStatus?.(status),
        () => StatusActions?.setStatus?.({ status }),
        () => StatusActions?.updateRemoteSettings?.({ status }),
        () => StatusActions?.saveAccountChanges?.({ status }),
        () => RestAPI?.patch?.({ url: "/users/@me/settings", body: { status } }),
        () => RestAPI?.put?.({ url: "/users/@me/settings", body: { status } }),
    ];

    let attempted = false;
    let lastError = "";
    for (const fn of attempts) {
        try {
            const ret = fn();
            if (ret === undefined) continue;
            attempted = true;
            if (ret && typeof ret.then === "function") await ret;
            storage.lastRealStatusResult = `Applied ${status} at ${new Date().toLocaleTimeString()}`;
            if (showToastResult) notify(`Tried server-side status: ${status}`);
            return true;
        } catch (e: any) {
            lastError = e?.message || String(e);
        }
    }

    storage.lastRealStatusResult = attempted ? `Attempted ${status}, no confirmation` : `No compatible status action${lastError ? `: ${lastError}` : ""}`;
    if (showToastResult) notify(storage.lastRealStatusResult, attempted ? "check" : "small");
    return attempted;
}

function stopStatusLock() {
    if (statusLockTimer) {
        clearInterval(statusLockTimer);
        statusLockTimer = null;
    }
}

function startStatusLock() {
    stopStatusLock();
    if (!storage.enabled || !storage.realStatusLock) return;
    trySetRealStatus(false).catch(() => {});
    const mins = Math.max(1, Math.min(60, parseInt(String(storage.lockInterval || "5"), 10) || 5));
    statusLockTimer = setInterval(() => {
        trySetRealStatus(false).catch(() => {});
    }, mins * 60 * 1000);
}

function spoofStatusResult(args: any[], ret: any) {
    if (!storage.enabled || !storage.localStatusSpoof) return ret;
    const wanted = String(storage.localStatus || "invisible");
    const id = String(args?.[0] || args?.[0]?.id || "");
    if (!id || id === currentUserId()) return wanted;
    return ret;
}

function Settings() {
    useProxy(storage);
    const hint = { opacity: 0.7, marginHorizontal: 12, marginVertical: 6 } as any;

    return (
        <ScrollView style={{ flex: 1 }}>
            <FormSection title="FakeOfflinePlus">
                <FormSwitchRow label="Enabled" value={!!storage.enabled} onValueChange={(v: boolean) => { storage.enabled = v; }} />
                <FormSwitchRow label="Panic mode" subLabel="Forces privacy features on" value={!!storage.panicMode} onValueChange={(v: boolean) => { storage.panicMode = v; }} />
                <Text style={hint}>Non-local: hiding typing blocks outgoing typing events. Server-side status below tries Discord's real status APIs without using or storing your token.</Text>
            </FormSection>

            <FormSection title="Typing privacy">
                <FormSwitchRow label="Hide typing" subLabel="Blocks startTyping/sendTyping calls" value={!!storage.hideTyping} onValueChange={(v: boolean) => { storage.hideTyping = v; }} />
                <FormSwitchRow label="Hide typing in DMs" value={!!storage.hideInDMs} onValueChange={(v: boolean) => { storage.hideInDMs = v; }} />
                <FormSwitchRow label="Hide typing in servers" value={!!storage.hideInServers} onValueChange={(v: boolean) => { storage.hideInServers = v; }} />
                <FormSwitchRow label="Toast when typing is hidden" value={!!storage.toastBlockedTyping} onValueChange={(v: boolean) => { storage.toastBlockedTyping = v; }} />
                <FormRow label="Blocked typing events" subLabel={String(storage.blockedTypingCount || 0)} />
                <Text style={hint}>Whitelist channel IDs, comma-separated</Text>
                <FormInput title="" placeholder="123, 456" value={String(storage.whitelistChannels || "")} onChange={(v: string) => { storage.whitelistChannels = v; }} />
            </FormSection>

            <FormSection title="Quiet hours">
                <FormSwitchRow label="Enable quiet hours" subLabel="Hide typing only during the selected hours (or with normal Hide Typing on)" value={!!storage.quietHours} onValueChange={(v: boolean) => { storage.quietHours = v; }} />
                <Text style={hint}>Start hour (0-23)</Text>
                <FormInput title="" placeholder="22" value={String(storage.quietStart || "")} onChange={(v: string) => { storage.quietStart = v.replace(/[^0-9]/g, ""); }} />
                <Text style={hint}>End hour (0-23)</Text>
                <FormInput title="" placeholder="8" value={String(storage.quietEnd || "")} onChange={(v: string) => { storage.quietEnd = v.replace(/[^0-9]/g, ""); }} />
            </FormSection>

            <FormSection title="Server-side status lock">
                <FormSwitchRow label="Lock real Discord status" subLabel="Best-effort: reapplies status every N minutes" value={!!storage.realStatusLock} onValueChange={(v: boolean) => { storage.realStatusLock = v; v ? startStatusLock() : stopStatusLock(); }} />
                <Text style={hint}>Real status: invisible / idle / dnd / online</Text>
                <FormInput title="" placeholder="invisible" value={String(storage.realStatus || "")} onChange={(v: string) => { storage.realStatus = v.trim(); }} />
                <Text style={hint}>Reapply interval minutes</Text>
                <FormInput title="" placeholder="5" value={String(storage.lockInterval || "")} onChange={(v: string) => { storage.lockInterval = v.replace(/[^0-9]/g, ""); }} />
                <FormRow label="Apply real status now" subLabel={String(storage.lastRealStatusResult || "Not applied yet")} onPress={() => trySetRealStatus(true)} trailing={FormRow.Arrow} />
            </FormSection>

            <FormSection title="Local status fallback">
                <FormSwitchRow label="Spoof my status locally" subLabel="Only changes how your status looks on your device" value={!!storage.localStatusSpoof} onValueChange={(v: boolean) => { storage.localStatusSpoof = v; }} />
                <Text style={hint}>Local status text: invisible / offline / idle / dnd / online</Text>
                <FormInput title="" placeholder="invisible" value={String(storage.localStatus || "")} onChange={(v: string) => { storage.localStatus = v.trim(); }} />
            </FormSection>
        </ScrollView>
    );
}

export default {
    onLoad: () => {
        initDefaults();

        if (TypingModule?.startTyping) {
            cleanups.push(instead("startTyping", TypingModule, (args: any[], orig: (...args: any[]) => any) => {
                if (blockTyping(args)) return undefined;
                return orig(...args);
            }));
        }
        if (TypingModule?.sendTyping) {
            cleanups.push(instead("sendTyping", TypingModule, (args: any[], orig: (...args: any[]) => any) => {
                if (blockTyping(args)) return undefined;
                return orig(...args);
            }));
        }
        if (PresenceStore?.getStatus) {
            cleanups.push(after("getStatus", PresenceStore, (args: any[], ret: any) => spoofStatusResult(args, ret)));
        }
        if (PresenceStore?.getState) {
            cleanups.push(after("getState", PresenceStore, (args: any[], ret: any) => {
                if (!storage.enabled || !storage.localStatusSpoof || !ret || typeof ret !== "object") return ret;
                const id = currentUserId();
                if (!id) return ret;
                try {
                    return { ...ret, [id]: { ...(ret[id] ?? {}), status: String(storage.localStatus || "invisible") } };
                } catch { return ret; }
            }));
        }
        startStatusLock();
        notify("FakeOfflinePlus loaded");
    },
    onUnload: () => {
        stopStatusLock();
        while (cleanups.length) {
            try { cleanups.pop()?.(); } catch { }
        }
    },
    settings: Settings,
};
