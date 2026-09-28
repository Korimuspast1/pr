/*
 * VisualFakeUsers — local-only fake/test users for Discord Android mods.
 * It does NOT create real Discord accounts and other people will not see them.
 */

import { findByName, findByProps, findByStoreName } from "@vendetta/metro";
import { React, ReactNative, clipboard } from "@vendetta/metro/common";
import { after } from "@vendetta/patcher";
import { storage } from "@vendetta/plugin";
import { useProxy } from "@vendetta/storage";
import { Forms } from "@vendetta/ui/components";
import { showToast } from "@vendetta/ui/toasts";
import { getAssetIDByName } from "@vendetta/ui/assets";

const { ScrollView, Text, View, Image } = ReactNative;
const { FormSection, FormRow, FormSwitchRow, FormInput } = Forms;

const UserStore = findByStoreName("UserStore") || findByProps("getCurrentUser", "getUser");
const GuildMemberStore = findByStoreName("GuildMemberStore") || findByProps("getMember", "getMembers", "getMemberIds");
const UserProfileStore = findByStoreName("UserProfileStore") || findByProps("getUserProfile", "getGuildMemberProfile");
const PresenceStore = findByStoreName("PresenceStore") || findByProps("getStatus", "getState");
const AvatarUtils = findByProps("getUserAvatarURL", "getUserAvatarSource");
const BannerUtils = findByProps("getUserBannerURL") || findByProps("default", "getUserBannerURL");
const SelectedGuildStore = findByStoreName("SelectedGuildStore") || findByProps("getGuildId", "getLastSelectedGuildId");
const profileBadges = findByName("useBadges", false);

const DISCORD_EPOCH = 1420070400000n;
const SLOT_COUNT = 5;
const cleanups: Array<() => void> = [];

const BADGE_DEFS = [
    { key: "staff", label: "Staff", icon: "5e74e9b61934fc1f67c65515d1f7e60d", description: "Discord Staff" },
    { key: "partner", label: "Partner", icon: "3f9748e53446a137a052f3454e2de41e", description: "Partnered Server Owner" },
    { key: "early", label: "Early Supporter", icon: "7060786766c9c840eb3019e725d2b358", description: "Early Supporter" },
    { key: "activeDev", label: "Active Dev", icon: "6bdc42827a38498929a4920da12695d9", description: "Active Developer" },
    { key: "bugHunter", label: "Bug Hunter", icon: "2717692c7dca7289b35297368a940dd0", description: "Bug Hunter Level 1" },
    { key: "nitro", label: "Nitro", icon: "2ba85e8026a8614b640c2837bcdfe21b", description: "Nitro Subscriber" },
];

type FakeUserConfig = Record<string, any> & {
    slot: number;
    enabled: boolean;
    id: string;
    username: string;
    displayName: string;
    avatarUrl: string;
    bannerUrl: string;
    bio: string;
    pronouns: string;
    status: string;
    bot: boolean;
    roleIds: string;
    nick: string;
    accentColor: string;
    guildId: string;
};

function key(slot: number, name: string) {
    return `fu${slot}_${name}`;
}

function initDefaults() {
    storage.enabled ??= true;
    storage.onlyCurrentGuild ??= true;
    storage.showInMemberList ??= true;
    storage.patchProfiles ??= true;
    storage.patchPresence ??= true;
    storage.activeSlot ??= 1;
    storage.importJson ??= "";
    for (let i = 1; i <= SLOT_COUNT; i++) {
        storage[key(i, "enabled")] ??= false;
        storage[key(i, "id")] ??= "";
        storage[key(i, "username")] ??= `fake_user_${i}`;
        storage[key(i, "displayName")] ??= `Fake User ${i}`;
        storage[key(i, "avatarUrl")] ??= "";
        storage[key(i, "bannerUrl")] ??= "";
        storage[key(i, "bio")] ??= "Local visual test account";
        storage[key(i, "pronouns")] ??= "";
        storage[key(i, "status")] ??= "online";
        storage[key(i, "bot")] ??= false;
        storage[key(i, "roleIds")] ??= "";
        storage[key(i, "nick")] ??= "";
        storage[key(i, "accentColor")] ??= "#5865f2";
        storage[key(i, "guildId")] ??= "";
        for (const badge of BADGE_DEFS) storage[key(i, `badge_${badge.key}`)] ??= false;
    }
}

initDefaults();

function notify(message: string, icon = "check") {
    try { showToast(message, getAssetIDByName(icon)); }
    catch { showToast(message); }
}

function selectedGuildId() {
    try { return SelectedGuildStore?.getGuildId?.() || SelectedGuildStore?.getLastSelectedGuildId?.() || ""; }
    catch { return ""; }
}

function cleanUrl(value: unknown) {
    const url = String(value || "").trim();
    return /^https?:\/\//i.test(url) ? url : "";
}

function csv(value: unknown) {
    return String(value || "").split(/[ ,;\n]+/).map(x => x.trim()).filter(Boolean);
}

function hexToInt(value: unknown) {
    const clean = String(value || "").trim().replace(/^#/, "");
    if (!/^[0-9a-f]{6}$/i.test(clean)) return 0x5865f2;
    return parseInt(clean, 16);
}

function generateSnowflake() {
    const time = BigInt(Date.now()) - DISCORD_EPOCH;
    const random = BigInt(Math.floor(Math.random() * 4194303));
    return ((time << 22n) + random).toString();
}

function copyWithDescriptors(original: any, overrides: Record<string, any>) {
    const base = original && typeof original === "object" ? original : {};
    const clone = Object.create(Object.getPrototypeOf(base));
    try {
        for (const prop of Reflect.ownKeys(base)) {
            const desc = Object.getOwnPropertyDescriptor(base, prop);
            if (desc) Object.defineProperty(clone, prop, desc);
        }
    } catch { Object.assign(clone, base); }
    for (const [prop, value] of Object.entries(overrides)) {
        try { Object.defineProperty(clone, prop, { configurable: true, enumerable: true, writable: true, value }); }
        catch { clone[prop] = value; }
    }
    return clone;
}

function slotConfig(slot: number): FakeUserConfig {
    return {
        slot,
        enabled: !!storage[key(slot, "enabled")],
        id: String(storage[key(slot, "id")] || "").trim(),
        username: String(storage[key(slot, "username")] || `fake_user_${slot}`).trim(),
        displayName: String(storage[key(slot, "displayName")] || `Fake User ${slot}`).trim(),
        avatarUrl: cleanUrl(storage[key(slot, "avatarUrl")]),
        bannerUrl: cleanUrl(storage[key(slot, "bannerUrl")]),
        bio: String(storage[key(slot, "bio")] || ""),
        pronouns: String(storage[key(slot, "pronouns")] || ""),
        status: String(storage[key(slot, "status")] || "online").trim().toLowerCase(),
        bot: !!storage[key(slot, "bot")],
        roleIds: String(storage[key(slot, "roleIds")] || ""),
        nick: String(storage[key(slot, "nick")] || ""),
        accentColor: String(storage[key(slot, "accentColor")] || "#5865f2"),
        guildId: String(storage[key(slot, "guildId")] || "").trim(),
    };
}

function allConfigs() {
    return Array.from({ length: SLOT_COUNT }, (_, i) => slotConfig(i + 1)).filter(c => c.enabled && /^\d{5,}$/.test(c.id));
}

function configById(id?: string | null) {
    if (!storage.enabled || !id) return null;
    return allConfigs().find(c => c.id === String(id)) || null;
}

function configAllowedInGuild(config: FakeUserConfig, guildId?: string | null) {
    if (!config) return false;
    const gid = String(guildId || "");
    if (config.guildId) return gid === config.guildId;
    if (storage.onlyCurrentGuild) return !gid || gid === selectedGuildId();
    return true;
}

function configsForGuild(guildId?: string | null) {
    return allConfigs().filter(c => configAllowedInGuild(c, guildId));
}

function currentUserPrototypeBase() {
    try { return UserStore?.getCurrentUser?.() || {}; }
    catch { return {}; }
}

function fakeUser(config: FakeUserConfig) {
    const base = currentUserPrototypeBase();
    const displayName = config.displayName || config.username;
    const overrides: Record<string, any> = {
        id: config.id,
        username: config.username || `fake_user_${config.slot}`,
        globalName: displayName,
        global_name: displayName,
        displayName,
        discriminator: "0",
        bot: !!config.bot,
        system: false,
        verified: !!config.bot,
        avatar: config.avatarUrl || null,
        banner: config.bannerUrl || null,
        publicFlags: 0,
        flags: 0,
        premiumType: 2,
        __visualFakeUser: true,
    };
    const user = copyWithDescriptors(base, overrides);
    user.getTag = () => overrides.username;
    user.getGlobalName = () => displayName;
    user.toString = () => displayName;
    return user;
}

function fakeMember(config: FakeUserConfig, guildId: string) {
    const roles = csv(config.roleIds);
    return {
        userId: config.id,
        user_id: config.id,
        guildId,
        guild_id: guildId,
        nick: config.nick || config.displayName || config.username,
        roles,
        premiumSince: null,
        joinedAt: new Date().toISOString(),
        communicationDisabledUntil: null,
        flags: 0,
        pending: false,
        avatar: null,
        colorRoleId: roles[0] || null,
        hoistRoleId: roles[0] || null,
        __visualFakeMember: true,
    };
}

function customBadges(config: FakeUserConfig | null) {
    if (!config) return [];
    return BADGE_DEFS
        .filter(b => !!storage[key(config.slot, `badge_${b.key}`)])
        .map(b => ({
            id: `visual-fake-user-${config.id}-${b.key}`,
            icon: b.icon,
            description: b.description,
        }));
}

function fakeProfile(config: FakeUserConfig) {
    const color = hexToInt(config.accentColor);
    return {
        userId: config.id,
        bio: config.bio || "Local visual fake user",
        pronouns: config.pronouns || "",
        banner: config.bannerUrl || undefined,
        accentColor: color,
        themeColors: [color, color],
        premiumType: 2,
        badges: customBadges(config),
        connectedAccounts: [],
        connected_accounts: [],
        profileFetchFailed: false,
        lastFetched: Date.now(),
    };
}

function mergeIds(original: any, ids: string[]) {
    if (!storage.showInMemberList || !ids.length) return original;
    if (Array.isArray(original)) return Array.from(new Set([...original.map(String), ...ids]));
    if (original instanceof Set) return new Set([...Array.from(original).map(String), ...ids]);
    return original;
}

function mergeMembers(original: any, members: any[]) {
    if (!storage.showInMemberList || !members.length) return original;
    if (Array.isArray(original)) {
        const seen = new Set(original.map((m: any) => String(m?.userId || m?.user_id || "")));
        const add = members.filter(m => !seen.has(String(m.userId)));
        return [...original, ...add];
    }
    if (original && typeof original === "object") {
        const clone = { ...original };
        for (const member of members) clone[member.userId] = member;
        return clone;
    }
    return original;
}

function exportConfig() {
    const out: any[] = [];
    for (let slot = 1; slot <= SLOT_COUNT; slot++) {
        const row: any = { slot };
        for (const name of ["enabled", "id", "username", "displayName", "avatarUrl", "bannerUrl", "bio", "pronouns", "status", "bot", "roleIds", "nick", "accentColor", "guildId"]) row[name] = storage[key(slot, name)];
        for (const badge of BADGE_DEFS) row[`badge_${badge.key}`] = storage[key(slot, `badge_${badge.key}`)];
        out.push(row);
    }
    return out;
}

function importConfig() {
    try {
        const parsed = JSON.parse(String(storage.importJson || ""));
        const list = Array.isArray(parsed) ? parsed : parsed.users;
        if (!Array.isArray(list)) throw new Error("expected array");
        for (const item of list.slice(0, SLOT_COUNT)) {
            const slot = Math.max(1, Math.min(SLOT_COUNT, Number(item.slot) || (list.indexOf(item) + 1)));
            for (const [name, value] of Object.entries(item)) {
                if (name === "slot") continue;
                storage[key(slot, name)] = value;
            }
        }
        storage.importJson = "";
        notify("Imported fake users");
    } catch (e: any) {
        notify(`Import failed: ${e?.message || e}`, "small");
    }
}

function copyExport() {
    try {
        clipboard.setString(JSON.stringify(exportConfig(), null, 2));
        notify("Fake users JSON copied", "copy");
    } catch { notify("Copy failed", "small"); }
}

function generateIdForSlot(slot: number) {
    storage[key(slot, "id")] = generateSnowflake();
    notify(`Generated ID for slot ${slot}`);
}

function Settings() {
    useProxy(storage);
    const active = Math.max(1, Math.min(SLOT_COUNT, Number(storage.activeSlot) || 1));
    const cfg = slotConfig(active);
    const hint = { opacity: 0.7, marginHorizontal: 12, marginVertical: 6 } as any;
    const avatar = cfg.avatarUrl || "https://cdn.discordapp.com/embed/avatars/0.png";

    return (
        <ScrollView style={{ flex: 1 }}>
            <FormSection title="VisualFakeUsers">
                <FormSwitchRow label="Enabled" subLabel="Local-only fake/test accounts" value={!!storage.enabled} onValueChange={(v: boolean) => { storage.enabled = v; }} />
                <FormSwitchRow label="Show in member list" value={!!storage.showInMemberList} onValueChange={(v: boolean) => { storage.showInMemberList = v; }} />
                <FormSwitchRow label="Patch fake profiles" value={!!storage.patchProfiles} onValueChange={(v: boolean) => { storage.patchProfiles = v; }} />
                <FormSwitchRow label="Patch presence/status" value={!!storage.patchPresence} onValueChange={(v: boolean) => { storage.patchPresence = v; }} />
                <FormSwitchRow label="Only selected guild if slot guild ID is empty" value={!!storage.onlyCurrentGuild} onValueChange={(v: boolean) => { storage.onlyCurrentGuild = v; }} />
                <Text style={hint}>Other people will not see these users. They do not exist on Discord servers.</Text>
            </FormSection>

            <FormSection title="Select fake user slot">
                {Array.from({ length: SLOT_COUNT }, (_, i) => i + 1).map(slot => {
                    const c = slotConfig(slot);
                    return <FormRow key={slot} label={`${active === slot ? "✓ " : ""}Slot ${slot}: ${c.displayName || c.username}`} subLabel={c.enabled ? c.id || "enabled, no id" : "disabled"} onPress={() => { storage.activeSlot = slot; }} trailing={FormRow.Arrow} />;
                })}
            </FormSection>

            <View style={{ marginHorizontal: 12, marginVertical: 8, padding: 12, borderRadius: 12, backgroundColor: "#1f2b30", flexDirection: "row", alignItems: "center" }}>
                <Image source={{ uri: avatar }} style={{ width: 54, height: 54, borderRadius: 27, marginRight: 12 }} />
                <View style={{ flex: 1 }}>
                    <Text style={{ color: "white", fontSize: 18, fontWeight: "700" }}>{cfg.displayName || cfg.username}</Text>
                    <Text style={{ color: "#c9d1d9" }}>{cfg.username} · {cfg.id || "no id"}</Text>
                    <Text style={{ color: "#c9d1d9" }}>Status: {cfg.status} {cfg.bot ? "· BOT" : ""}</Text>
                </View>
            </View>

            <FormSection title={`Edit slot ${active}`}>
                <FormSwitchRow label="Enabled" value={cfg.enabled} onValueChange={(v: boolean) => { storage[key(active, "enabled")] = v; }} />
                <Text style={hint}>Fake user ID</Text>
                <FormInput title="" placeholder="auto-generate or paste id" value={cfg.id} onChange={(v: string) => { storage[key(active, "id")] = v.replace(/[^0-9]/g, ""); }} />
                <FormRow label="Generate valid fake ID" onPress={() => generateIdForSlot(active)} trailing={FormRow.Arrow} />
                <Text style={hint}>Username</Text>
                <FormInput title="" placeholder="fake_user" value={cfg.username} onChange={(v: string) => { storage[key(active, "username")] = v; }} />
                <Text style={hint}>Display name</Text>
                <FormInput title="" placeholder="Fake User" value={cfg.displayName} onChange={(v: string) => { storage[key(active, "displayName")] = v; }} />
                <FormSwitchRow label="Bot account visual" value={cfg.bot} onValueChange={(v: boolean) => { storage[key(active, "bot")] = v; }} />
                <Text style={hint}>Status: online / idle / dnd / offline</Text>
                <FormInput title="" placeholder="online" value={cfg.status} onChange={(v: string) => { storage[key(active, "status")] = v.trim().toLowerCase(); }} />
                <Text style={hint}>Guild ID for this fake user (empty = selected server/global setting)</Text>
                <FormInput title="" placeholder={selectedGuildId() || "server id"} value={cfg.guildId} onChange={(v: string) => { storage[key(active, "guildId")] = v.replace(/[^0-9]/g, ""); }} />
            </FormSection>

            <FormSection title="Profile data">
                <Text style={hint}>Avatar URL</Text>
                <FormInput title="" placeholder="https://.../avatar.png" value={cfg.avatarUrl} onChange={(v: string) => { storage[key(active, "avatarUrl")] = v; }} />
                <Text style={hint}>Banner URL</Text>
                <FormInput title="" placeholder="https://.../banner.png" value={cfg.bannerUrl} onChange={(v: string) => { storage[key(active, "bannerUrl")] = v; }} />
                <Text style={hint}>Bio</Text>
                <FormInput title="" placeholder="bio" value={cfg.bio} onChange={(v: string) => { storage[key(active, "bio")] = v; }} />
                <Text style={hint}>Pronouns</Text>
                <FormInput title="" placeholder="they/them" value={cfg.pronouns} onChange={(v: string) => { storage[key(active, "pronouns")] = v; }} />
                <Text style={hint}>Profile color #rrggbb</Text>
                <FormInput title="" placeholder="#5865f2" value={cfg.accentColor} onChange={(v: string) => { storage[key(active, "accentColor")] = v; }} />
            </FormSection>

            <FormSection title="Server member data">
                <Text style={hint}>Nickname in server</Text>
                <FormInput title="" placeholder="local nick" value={cfg.nick} onChange={(v: string) => { storage[key(active, "nick")] = v; }} />
                <Text style={hint}>Role IDs, comma-separated</Text>
                <FormInput title="" placeholder="roleId1, roleId2" value={cfg.roleIds} onChange={(v: string) => { storage[key(active, "roleIds")] = v; }} />
            </FormSection>

            <FormSection title="Fake badges">
                {BADGE_DEFS.map(badge => (
                    <FormSwitchRow key={badge.key} label={badge.label} subLabel={badge.description} value={!!storage[key(active, `badge_${badge.key}`)]} onValueChange={(v: boolean) => { storage[key(active, `badge_${badge.key}`)] = v; }} />
                ))}
            </FormSection>

            <FormSection title="Import / export">
                <FormRow label="Copy all fake users JSON" onPress={copyExport} trailing={FormRow.Arrow} />
                <Text style={hint}>Paste JSON to import</Text>
                <FormInput title="" placeholder="[{...}]" value={String(storage.importJson || "")} onChange={(v: string) => { storage.importJson = v; }} />
                <FormRow label="Import JSON" onPress={importConfig} trailing={FormRow.Arrow} />
            </FormSection>
        </ScrollView>
    );
}

export default {
    onLoad: () => {
        initDefaults();

        if (UserStore?.getUser) {
            cleanups.push(after("getUser", UserStore, (args: any[], user: any) => {
                const config = configById(String(args?.[0] || user?.id || ""));
                return config ? fakeUser(config) : user;
            }));
        }

        if (UserStore?.getUsers) {
            cleanups.push(after("getUsers", UserStore, (_args: any[], users: any) => {
                if (!users || typeof users !== "object") return users;
                const clone = Array.isArray(users) ? [...users] : { ...users };
                for (const config of allConfigs()) {
                    if (Array.isArray(clone)) clone.push(fakeUser(config));
                    else clone[config.id] = fakeUser(config);
                }
                return clone;
            }));
        }

        if (GuildMemberStore?.getMember) {
            cleanups.push(after("getMember", GuildMemberStore, (args: any[], member: any) => {
                const guildId = String(args?.[0] || "");
                const userId = String(args?.[1] || "");
                const config = configById(userId);
                if (config && configAllowedInGuild(config, guildId)) return fakeMember(config, guildId);
                return member;
            }));
        }

        if (GuildMemberStore?.getMemberIds) {
            cleanups.push(after("getMemberIds", GuildMemberStore, (args: any[], ids: any) => {
                const guildId = String(args?.[0] || "");
                return mergeIds(ids, configsForGuild(guildId).map(c => c.id));
            }));
        }

        if (GuildMemberStore?.getMembers) {
            cleanups.push(after("getMembers", GuildMemberStore, (args: any[], members: any) => {
                const guildId = String(args?.[0] || "");
                return mergeMembers(members, configsForGuild(guildId).map(c => fakeMember(c, guildId)));
            }));
        }

        if (UserProfileStore?.getUserProfile) {
            cleanups.push(after("getUserProfile", UserProfileStore, (args: any[], profile: any) => {
                if (!storage.patchProfiles) return profile;
                const config = configById(String(args?.[0] || profile?.userId || ""));
                return config ? fakeProfile(config) : profile;
            }));
        }

        if (UserProfileStore?.getGuildMemberProfile) {
            cleanups.push(after("getGuildMemberProfile", UserProfileStore, (args: any[], profile: any) => {
                if (!storage.patchProfiles) return profile;
                const config = configById(String(args?.[0] || profile?.userId || ""));
                return config ? fakeProfile(config) : profile;
            }));
        }

        if (AvatarUtils?.getUserAvatarURL) {
            cleanups.push(after("getUserAvatarURL", AvatarUtils, ([user]: any[], ret: any) => {
                const config = configById(user?.id);
                return config?.avatarUrl || ret;
            }));
        }

        if (AvatarUtils?.getUserAvatarSource) {
            cleanups.push(after("getUserAvatarSource", AvatarUtils, ([user]: any[], ret: any) => {
                const config = configById(user?.id);
                return config?.avatarUrl ? { uri: config.avatarUrl } : ret;
            }));
        }

        if (BannerUtils?.getUserBannerURL) {
            cleanups.push(after("getUserBannerURL", BannerUtils, ([user]: any[], ret: any) => {
                const config = configById(user?.id);
                return config?.bannerUrl || ret;
            }));
        }

        if (PresenceStore?.getStatus) {
            cleanups.push(after("getStatus", PresenceStore, (args: any[], ret: any) => {
                if (!storage.patchPresence) return ret;
                const config = configById(String(args?.[0] || ""));
                return config ? config.status : ret;
            }));
        }

        if (PresenceStore?.getState) {
            cleanups.push(after("getState", PresenceStore, (_args: any[], ret: any) => {
                if (!storage.patchPresence || !ret || typeof ret !== "object") return ret;
                const clone = { ...ret };
                for (const config of allConfigs()) clone[config.id] = { ...(clone[config.id] || {}), status: config.status };
                return clone;
            }));
        }

        if (profileBadges?.default) {
            cleanups.push(after("default", profileBadges, ([user]: any[], ret: any) => {
                const config = configById(user?.id || user?.userId);
                const badges = customBadges(config);
                return badges.length ? [...badges, ...(Array.isArray(ret) ? ret : [])] : ret;
            }));
        }

        notify("VisualFakeUsers loaded");
    },
    onUnload: () => {
        while (cleanups.length) {
            try { cleanups.pop()?.(); } catch { }
        }
    },
    settings: Settings,
};
