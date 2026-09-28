/* ChatHeatmap — local-only chat activity analytics for Discord Android mods. */

import { findByStoreName } from "@vendetta/metro";
import { FluxDispatcher, React, ReactNative, clipboard } from "@vendetta/metro/common";
import { before } from "@vendetta/patcher";
import { storage } from "@vendetta/plugin";
import { useProxy } from "@vendetta/storage";
import { Forms } from "@vendetta/ui/components";
import { showToast } from "@vendetta/ui/toasts";
import { getAssetIDByName } from "@vendetta/ui/assets";

const { ScrollView, Text } = ReactNative;
const { FormSection, FormRow, FormSwitchRow, FormInput } = Forms;

const UserStore = findByStoreName("UserStore");
const ChannelStore = findByStoreName("ChannelStore");
const SelectedChannelStore = findByStoreName("SelectedChannelStore");

const cleanups: Array<() => void> = [];
let memStats: any = null;
let writes = 0;
let publishing = false;

function initDefaults() {
    storage.enabled ??= true;
    storage.ignoreBots ??= true;
    storage.ignoreSelf ??= false;
    storage.trackWords ??= true;
    storage.trackEmoji ??= true;
    storage.searchUser ??= "";
    storage.webhookUrl ??= "";
    storage.publishTitle ??= "Discord Chat Heatmap";
    storage.autoPublish ??= false;
    storage.publishEvery ??= "100";
    storage.lastPublishTotal ??= 0;
    storage.lastPublishStatus ??= "Not published yet";
    storage.stats ??= makeEmptyStats();
}

function makeEmptyStats() {
    return {
        startedAt: Date.now(),
        updatedAt: Date.now(),
        total: 0,
        attachments: 0,
        byHour: Array(24).fill(0),
        byDay: Array(7).fill(0),
        byUser: {},
        byChannel: {},
        byGuild: {},
        words: {},
        emoji: {},
    };
}

initDefaults();

function notify(message: string, icon = "check") {
    try { showToast(message, getAssetIDByName(icon)); }
    catch { showToast(message); }
}

function stats() {
    if (!memStats) memStats = { ...makeEmptyStats(), ...(storage.stats || {}) };
    memStats.byHour = Array.isArray(memStats.byHour) ? memStats.byHour : Array(24).fill(0);
    memStats.byDay = Array.isArray(memStats.byDay) ? memStats.byDay : Array(7).fill(0);
    memStats.byUser ??= {};
    memStats.byChannel ??= {};
    memStats.byGuild ??= {};
    memStats.words ??= {};
    memStats.emoji ??= {};
    return memStats;
}

function flush(force = false) {
    if (!memStats) return;
    writes++;
    if (!force && writes % 10 !== 0) return;
    storage.stats = memStats;
}

function currentUserId() {
    try { return UserStore?.getCurrentUser?.()?.id || ""; }
    catch { return "" ;}
}

function channelOf(channelId: string) {
    try { return ChannelStore?.getChannel?.(channelId); }
    catch { return undefined; }
}

function selectedChannelId() {
    try { return SelectedChannelStore?.getChannelId?.() || SelectedChannelStore?.getChannel?.()?.id || ""; }
    catch { return ""; }
}

function inc(obj: Record<string, number>, key: string, amount = 1) {
    if (!key) return;
    obj[key] = (obj[key] || 0) + amount;
}

function trimObject(obj: Record<string, number>, cap: number) {
    const entries = Object.entries(obj);
    if (entries.length <= cap) return;
    entries.sort((a, b) => b[1] - a[1]);
    const keep = new Set(entries.slice(0, cap).map(([k]) => k));
    for (const key of Object.keys(obj)) if (!keep.has(key)) delete obj[key];
}

function wordsOf(text: string) {
    return (text.toLowerCase().match(/[\p{L}\p{N}_]{3,}/gu) || [])
        .filter(w => !/^https?$/.test(w) && !/^www$/.test(w))
        .slice(0, 80);
}

function emojiOf(text: string) {
    const custom = text.match(/<a?:[a-z0-9_~]+:\d+>/gi) || [];
    const unicode = text.match(/[\u{1F300}-\u{1FAFF}]/gu) || [];
    return [...custom, ...unicode].slice(0, 40);
}

function shouldSkip(message: any) {
    if (!storage.enabled) return true;
    if (storage.ignoreBots && message?.author?.bot) return true;
    if (storage.ignoreSelf && message?.author?.id === currentUserId()) return true;
    return false;
}

function handleMessage(message: any) {
    try {
        if (!message?.id || !message.channel_id || shouldSkip(message)) return;
        const s = stats();
        const now = new Date();
        const channelId = String(message.channel_id);
        const guildId = String(message.guild_id || channelOf(channelId)?.guild_id || "DM");
        const authorId = String(message.author?.id || "unknown");
        const authorName = String(message.author?.global_name || message.author?.globalName || message.author?.username || authorId);
        const content = String(message.content || "");
        const attachments = Array.isArray(message.attachments) ? message.attachments.length : 0;

        s.total++;
        s.updatedAt = Date.now();
        s.attachments += attachments;
        s.byHour[now.getHours()] = (s.byHour[now.getHours()] || 0) + 1;
        s.byDay[now.getDay()] = (s.byDay[now.getDay()] || 0) + 1;
        inc(s.byChannel, channelId);
        inc(s.byGuild, guildId);
        inc(s.byUser, `${authorName} (${authorId})`);
        if (storage.trackWords) for (const word of wordsOf(content)) inc(s.words, word);
        if (storage.trackEmoji) for (const emoji of emojiOf(content)) inc(s.emoji, emoji);

        trimObject(s.byUser, 100);
        trimObject(s.byChannel, 100);
        trimObject(s.byGuild, 100);
        trimObject(s.words, 120);
        trimObject(s.emoji, 80);
        flush();

        const every = Math.max(10, parseInt(String(storage.publishEvery || "100"), 10) || 100);
        if (storage.autoPublish && String(storage.webhookUrl || "").startsWith("https://") && s.total - (Number(storage.lastPublishTotal) || 0) >= every) {
            publishHeatmap("auto").catch(e => console.error("[ChatHeatmap] auto publish failed", e));
        }
    } catch (e) {
        console.error("[ChatHeatmap] handle error", e);
    }
}

function top(obj: Record<string, number>, n = 10) {
    return Object.entries(obj || {}).sort((a, b) => b[1] - a[1]).slice(0, n);
}

function bar(value: number, max: number) {
    const len = max ? Math.max(1, Math.round((value / max) * 12)) : 0;
    return "█".repeat(len);
}

function heatRows(values: number[], labels: string[]) {
    const max = Math.max(1, ...values);
    return values.map((v, i) => `${labels[i]} ${bar(v, max)} ${v}`).join("\n");
}

function fmtDate(ms: number) {
    try { return new Date(ms).toLocaleString(); }
    catch { return ""; }
}

function copyStats() {
    const s = stats();
    const text = JSON.stringify(s, null, 2);
    try {
        clipboard.setString(text);
        notify("Heatmap stats copied");
    } catch { notify("Copy failed", "small"); }
}

function resetStats() {
    memStats = makeEmptyStats();
    storage.stats = memStats;
    notify("Chat heatmap reset", "ic_message_retry");
}

function formatTop(obj: Record<string, number>, n = 8) {
    const rows = top(obj, n);
    return rows.length ? rows.map(([k, v], i) => `${i + 1}. ${k}: ${v}`).join("\n") : "no data";
}

function heatmapSummary() {
    const s = stats();
    const days = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    const hours = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, "0"));
    const parts = [
        `**${String(storage.publishTitle || "Discord Chat Heatmap")}**`,
        `Messages: **${s.total || 0}** | Media: **${s.attachments || 0}**`,
        `Started: ${fmtDate(s.startedAt)} | Updated: ${fmtDate(s.updatedAt)}`,
        "",
        "**By day**",
        "```" + heatRows(s.byDay || [], days).slice(0, 700) + "```",
        "**By hour**",
        "```" + heatRows(s.byHour || [], hours).slice(0, 900) + "```",
        "**Top users**",
        "```" + formatTop(s.byUser, 8).slice(0, 800) + "```",
        "**Top words**",
        "```" + formatTop(s.words, 12).slice(0, 600) + "```",
        "**Top emoji**",
        "```" + formatTop(s.emoji, 10).slice(0, 500) + "```",
    ];
    return parts.join("\n").slice(0, 1900);
}

async function publishHeatmap(reason = "manual") {
    if (publishing) return;
    const webhook = String(storage.webhookUrl || "").trim();
    if (!/^https:\/\/.*discord(?:app)?\.com\/api\/webhooks\//i.test(webhook)) {
        storage.lastPublishStatus = "Webhook URL is missing or invalid";
        notify("Invalid Discord webhook URL", "small");
        return;
    }

    publishing = true;
    try {
        const body = {
            username: "ChatHeatmap",
            content: heatmapSummary(),
            allowed_mentions: { parse: [] },
        };
        const res = await fetch(webhook, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
        });
        if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
        storage.lastPublishTotal = stats().total || 0;
        storage.lastPublishStatus = `${reason} publish OK at ${fmtDate(Date.now())}`;
        notify("Heatmap published to webhook");
    } catch (e: any) {
        storage.lastPublishStatus = `Publish failed: ${e?.message || e}`;
        notify("Heatmap publish failed", "small");
    } finally {
        publishing = false;
    }
}

function Settings() {
    useProxy(storage);
    const s = stats();
    const days = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    const hours = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, "0"));
    const selected = selectedChannelId();
    const hint = { opacity: 0.7, marginHorizontal: 12, marginVertical: 6, fontFamily: "monospace" } as any;

    return (
        <ScrollView style={{ flex: 1 }}>
            <FormSection title="ChatHeatmap">
                <FormSwitchRow label="Enabled" value={!!storage.enabled} onValueChange={(v: boolean) => { storage.enabled = v; }} />
                <FormSwitchRow label="Ignore bots" value={!!storage.ignoreBots} onValueChange={(v: boolean) => { storage.ignoreBots = v; }} />
                <FormSwitchRow label="Ignore yourself" value={!!storage.ignoreSelf} onValueChange={(v: boolean) => { storage.ignoreSelf = v; }} />
                <FormSwitchRow label="Track words" value={!!storage.trackWords} onValueChange={(v: boolean) => { storage.trackWords = v; }} />
                <FormSwitchRow label="Track emoji" value={!!storage.trackEmoji} onValueChange={(v: boolean) => { storage.trackEmoji = v; }} />
                <FormRow label="Total messages tracked" subLabel={`${s.total} since ${fmtDate(s.startedAt)}`} />
                <FormRow label="Attachments/media" subLabel={String(s.attachments || 0)} />
                <FormRow label="Current channel count" subLabel={selected ? String(s.byChannel?.[selected] || 0) : "unknown"} />
                <FormRow label="Copy stats JSON" onPress={copyStats} trailing={FormRow.Arrow} />
                <FormRow label="Reset stats" onPress={resetStats} trailing={FormRow.Arrow} />
            </FormSection>

            <FormSection title="Publish / non-local sharing">
                <Text style={hint}>Discord webhook URL. This posts the heatmap to a channel through your webhook, so other people can see it. No token is used.</Text>
                <FormInput title="" placeholder="https://discord.com/api/webhooks/..." value={String(storage.webhookUrl || "")} onChange={(v: string) => { storage.webhookUrl = v.trim(); }} />
                <Text style={hint}>Publish title</Text>
                <FormInput title="" placeholder="Discord Chat Heatmap" value={String(storage.publishTitle || "")} onChange={(v: string) => { storage.publishTitle = v; }} />
                <FormSwitchRow label="Auto-publish" subLabel="Posts after every N tracked messages" value={!!storage.autoPublish} onValueChange={(v: boolean) => { storage.autoPublish = v; }} />
                <Text style={hint}>Auto-publish every N messages</Text>
                <FormInput title="" placeholder="100" value={String(storage.publishEvery || "")} onChange={(v: string) => { storage.publishEvery = v.replace(/[^0-9]/g, ""); }} />
                <FormRow label="Publish now" subLabel={String(storage.lastPublishStatus || "Not published yet")} onPress={() => publishHeatmap("manual")} trailing={FormRow.Arrow} />
            </FormSection>

            <FormSection title="By hour">
                <Text style={hint}>{heatRows(s.byHour || [], hours)}</Text>
            </FormSection>
            <FormSection title="By day">
                <Text style={hint}>{heatRows(s.byDay || [], days)}</Text>
            </FormSection>
            <FormSection title="Top users">
                {top(s.byUser, 20).map(([name, count]) => <FormRow key={name} label={name} subLabel={`${count} messages`} />)}
            </FormSection>
            <FormSection title="Top channels">
                {top(s.byChannel, 15).map(([id, count]) => <FormRow key={id} label={`#${id}`} subLabel={`${count} messages`} />)}
            </FormSection>
            <FormSection title="Top words">
                {top(s.words, 20).map(([word, count]) => <FormRow key={word} label={word} subLabel={`${count} times`} />)}
            </FormSection>
            <FormSection title="Top emoji">
                {top(s.emoji, 20).map(([emoji, count]) => <FormRow key={emoji} label={emoji} subLabel={`${count} times`} />)}
            </FormSection>
        </ScrollView>
    );
}

export default {
    onLoad: () => {
        initDefaults();
        cleanups.push(before("dispatch", FluxDispatcher as any, (args: any[]) => {
            const ev = args?.[0];
            if (ev?.type === "MESSAGE_CREATE") handleMessage(ev.message);
            return args;
        }));
        notify("ChatHeatmap loaded");
    },
    onUnload: () => {
        flush(true);
        memStats = null;
        while (cleanups.length) {
            try { cleanups.pop()?.(); } catch { }
        }
    },
    settings: Settings,
};
