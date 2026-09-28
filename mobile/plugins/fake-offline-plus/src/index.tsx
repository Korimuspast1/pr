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

const cleanups: Array<() => void> = [];

function initDefaults() {
    storage.enabled ??= true;
    storage.hideTyping ??= true;
    storage.hideInDMs ??= true;
    storage.hideInServers ??= true;
    storage.panicMode ??= false;
    storage.localStatusSpoof ??= true;
    storage.localStatus ??= "invisible";
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

function trySetInvisible() {
    const attempts = [
        () => StatusActions?.updateStatus?.("invisible"),
        () => StatusActions?.setStatus?.("invisible"),
        () => StatusActions?.updateRemoteSettings?.({ status: "invisible" }),
        () => StatusActions?.saveAccountChanges?.({ status: "invisible" }),
    ];
    let ok = false;
    for (const fn of attempts) {
        try {
            const ret = fn();
            if (ret !== undefined) ok = true;
        } catch { /* try next */ }
    }
    notify(ok ? "Tried to set invisible" : "No compatible status action found", ok ? "check" : "small");
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
                <FormRow label="Try set real Discord status to Invisible" subLabel="Best-effort; depends on Discord version" onPress={trySetInvisible} trailing={FormRow.Arrow} />
                <Text style={hint}>Reliable feature: blocking outgoing typing events. Status spoof below is local-only unless the action above works.</Text>
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

            <FormSection title="Local status spoof">
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
        notify("FakeOfflinePlus loaded");
    },
    onUnload: () => {
        while (cleanups.length) {
            try { cleanups.pop()?.(); } catch { }
        }
    },
    settings: Settings,
};
