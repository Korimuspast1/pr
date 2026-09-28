/*
 * CustomProfileMobile — a local-only Android port inspired by the Nightcord/Vencord CustomProfile plugin.
 * Target loaders: Revenge / Bunny / Vendetta-compatible Discord Android mods.
 * This plugin never asks for, stores, or sends your Discord token.
 */

import { findByName, findByProps, findByStoreName } from "@vendetta/metro";
import { React, ReactNative } from "@vendetta/metro/common";
import { after } from "@vendetta/patcher";
import { storage } from "@vendetta/plugin";
import { useProxy } from "@vendetta/storage";
import { Forms } from "@vendetta/ui/components";
import { getAssetIDByName } from "@vendetta/ui/assets";
import { showToast } from "@vendetta/ui/toasts";

const { ScrollView, Text } = ReactNative;
const { FormSection, FormRow, FormSwitchRow, FormInput } = Forms;

const UserStore = findByStoreName("UserStore");
const UserProfileStore = findByStoreName("UserProfileStore");
const AvatarUtils = findByProps("getUserAvatarURL", "getUserAvatarSource");
const BannerUtils = findByProps("getUserBannerURL") ?? findByProps("default", "getUserBannerURL");
const profileBadges = findByName("useBadges", false);

let patches: Array<() => void> = [];
let currentUserId: string | undefined;

const BADGES = [
    { key: "badgeStaff", label: "Discord Staff", id: "staff", icon: "5e74e9b61934fc1f67c65515d1f7e60d", description: "Discord Staff" },
    { key: "badgePartner", label: "Partner", id: "partner", icon: "3f9748e53446a137a052f3454e2de41e", description: "Partnered Server Owner" },
    { key: "badgeEarly", label: "Early Supporter", id: "early_supporter", icon: "7060786766c9c840eb3019e725d2b358", description: "Early Supporter" },
    { key: "badgeBugHunter", label: "Bug Hunter", id: "bug_hunter_level_1", icon: "2717692c7dca7289b35297368a940dd0", description: "Bug Hunter Level 1" },
    { key: "badgeHypeSquad", label: "HypeSquad Events", id: "hypesquad", icon: "bf01d1073931f921909045f3a39fd264", description: "HypeSquad Events" },
    { key: "badgeActiveDev", label: "Active Developer", id: "active_developer", icon: "6bdc42827a38498929a4920da12695d9", description: "Active Developer" },
];

function initDefaults() {
    storage.enabled ??= true;
    storage.username ??= "";
    storage.displayName ??= "";
    storage.bio ??= "";
    storage.pronouns ??= "";
    storage.avatarUrl ??= "";
    storage.bannerUrl ??= "";
    storage.accentColor ??= "";
    storage.accentColor2 ??= "";
    storage.fakeNitro ??= false;
    for (const badge of BADGES) storage[badge.key] ??= false;
}

initDefaults();

function hexToInt(value: unknown): number | undefined {
    const hex = String(value ?? "").trim().replace(/^#/, "");
    if (!/^[0-9a-f]{6}$/i.test(hex)) return undefined;
    return parseInt(hex, 16);
}

function hasAnyOverride() {
    return !!(
        storage.username ||
        storage.displayName ||
        storage.bio ||
        storage.pronouns ||
        storage.avatarUrl ||
        storage.bannerUrl ||
        storage.fakeNitro ||
        hexToInt(storage.accentColor) != null ||
        hexToInt(storage.accentColor2) != null ||
        BADGES.some(b => storage[b.key])
    );
}

function isMe(id?: string | null) {
    if (!id) return false;
    try {
        currentUserId ||= UserStore?.getCurrentUser?.()?.id;
    } catch { /* ignore */ }
    return !!currentUserId && id === currentUserId;
}

function cleanUrl(value: unknown) {
    const url = String(value ?? "").trim();
    if (!url) return "";
    if (!/^https?:\/\//i.test(url)) return "";
    return url;
}

function copyWithDescriptors(original: any, overrides: Record<string, any>) {
    if (!original || typeof original !== "object") return original;
    const clone = Object.create(Object.getPrototypeOf(original));
    try {
        for (const key of Reflect.ownKeys(original)) {
            const desc = Object.getOwnPropertyDescriptor(original, key);
            if (desc) Object.defineProperty(clone, key, desc);
        }
    } catch {
        Object.assign(clone, original);
    }
    for (const [key, value] of Object.entries(overrides)) {
        try {
            Object.defineProperty(clone, key, { configurable: true, enumerable: true, writable: true, value });
        } catch {
            clone[key] = value;
        }
    }
    return clone;
}

function getCustomBadges() {
    if (!storage.enabled) return [];
    return BADGES
        .filter(b => !!storage[b.key])
        .map(b => ({
            id: `custom-profile-mobile-${b.id}`,
            icon: b.icon,
            description: b.description,
        }));
}

function patchUser(user: any) {
    if (!storage.enabled || !user || !isMe(user.id) || !hasAnyOverride()) return user;

    const displayName = String(storage.displayName || "").trim();
    const username = String(storage.username || "").trim();
    const avatar = cleanUrl(storage.avatarUrl);

    const overrides: Record<string, any> = {};
    if (username) {
        overrides.username = username;
        overrides.legacyUsername = username;
    }
    if (displayName) {
        overrides.globalName = displayName;
        overrides.displayName = displayName;
    }
    if (avatar) overrides.avatar = avatar;
    if (storage.fakeNitro) overrides.premiumType = 2;

    if (!Object.keys(overrides).length) return user;

    const patched = copyWithDescriptors(user, overrides);

    try {
        const originalGetTag = user.getTag?.bind(user);
        patched.getTag = () => {
            const name = username || user.username;
            return user.discriminator === "0" ? name : `${name}#${user.discriminator}`;
        };
        patched.getGlobalName = () => displayName || user.globalName;
        patched.toString = () => displayName || originalGetTag?.() || user.username;
    } catch { /* ignore */ }

    return patched;
}

function patchProfile(profile: any) {
    if (!storage.enabled || !profile || !isMe(profile.userId) || !hasAnyOverride()) return profile;

    const primary = hexToInt(storage.accentColor);
    const accent = hexToInt(storage.accentColor2) ?? primary;
    const banner = cleanUrl(storage.bannerUrl);

    const overrides: Record<string, any> = {};
    if (storage.bio) overrides.bio = String(storage.bio);
    if (storage.pronouns) overrides.pronouns = String(storage.pronouns);
    if (primary != null) {
        overrides.accentColor = primary;
        overrides.themeColors = [primary, accent ?? primary];
    }
    if (banner) overrides.banner = banner;
    if (storage.fakeNitro || primary != null || banner) {
        overrides.premiumType = 2;
        overrides.premiumSince ??= profile.premiumSince ?? new Date(Date.now() - 1000 * 60 * 60 * 24 * 30);
    }

    // Do not write custom badges into profile.badges here.
    // Mobile Discord also renders badges through useBadges; injecting in both
    // places causes every selected badge to appear twice on the profile.

    return Object.keys(overrides).length ? copyWithDescriptors(profile, overrides) : profile;
}

function badgeKey(badge: any): string {
    return String(badge?.id ?? badge?.badge_id ?? badge?.key ?? badge?.icon ?? badge?.description ?? "");
}

function mergeBadges(customBadges: any[], existingBadges: any[]) {
    const seen = new Set<string>();
    const merged: any[] = [];
    for (const badge of [...customBadges, ...existingBadges]) {
        const key = badgeKey(badge);
        if (key && seen.has(key)) continue;
        if (key) seen.add(key);
        merged.push(badge);
    }
    return merged;
}

function installPatches() {
    currentUserId = UserStore?.getCurrentUser?.()?.id;

    if (UserStore?.getCurrentUser) {
        patches.push(after("getCurrentUser", UserStore, (_args: any[], user: any) => {
            if (user?.id) currentUserId = user.id;
            return patchUser(user);
        }));
    }

    if (UserStore?.getUser) {
        patches.push(after("getUser", UserStore, (_args: any[], user: any) => patchUser(user)));
    }

    if (UserProfileStore?.getUserProfile) {
        patches.push(after("getUserProfile", UserProfileStore, (_args: any[], profile: any) => patchProfile(profile)));
    }

    if (UserProfileStore?.getGuildMemberProfile) {
        patches.push(after("getGuildMemberProfile", UserProfileStore, (_args: any[], profile: any) => patchProfile(profile)));
    }

    if (AvatarUtils?.getUserAvatarURL) {
        patches.push(after("getUserAvatarURL", AvatarUtils, ([user]: any[], ret: any) => {
            const url = cleanUrl(storage.avatarUrl);
            return storage.enabled && url && isMe(user?.id) ? url : ret;
        }));
    }

    if (AvatarUtils?.getUserAvatarSource) {
        patches.push(after("getUserAvatarSource", AvatarUtils, ([user]: any[], ret: any) => {
            const url = cleanUrl(storage.avatarUrl);
            return storage.enabled && url && isMe(user?.id) ? { uri: url } : ret;
        }));
    }

    if (BannerUtils?.getUserBannerURL) {
        patches.push(after("getUserBannerURL", BannerUtils, ([user]: any[], ret: any) => {
            const url = cleanUrl(storage.bannerUrl);
            return storage.enabled && url && isMe(user?.id) ? url : ret;
        }));
    }

    if (profileBadges?.default) {
        patches.push(after("default", profileBadges, ([user]: any[], ret: any) => {
            const userId = user?.userId ?? user?.id;
            if (!storage.enabled || !isMe(userId)) return ret;
            const customBadges = getCustomBadges();
            if (!customBadges.length) return ret;
            return mergeBadges(customBadges, Array.isArray(ret) ? ret : []);
        }));
    }
}

function resetProfile() {
    storage.username = "";
    storage.displayName = "";
    storage.bio = "";
    storage.pronouns = "";
    storage.avatarUrl = "";
    storage.bannerUrl = "";
    storage.accentColor = "";
    storage.accentColor2 = "";
    storage.fakeNitro = false;
    for (const badge of BADGES) storage[badge.key] = false;
    showToast("CustomProfileMobile reset. Reopen the profile to refresh.", getAssetIDByName("ic_message_retry"));
}

function Settings() {
    useProxy(storage);

    const hintStyle = { opacity: 0.7, marginHorizontal: 12, marginBottom: 4, marginTop: 8 } as any;

    return (
        <ScrollView>
            <FormSection title="CustomProfileMobile">
                <FormSwitchRow
                    label="Enabled"
                    subLabel="Local-only: changes are visible only on this phone"
                    value={!!storage.enabled}
                    onValueChange={(v: boolean) => { storage.enabled = v; }}
                    leading={<FormRow.Icon source={getAssetIDByName("settings")} />}
                />
                <FormRow
                    label="Status"
                    subLabel="If changes do not appear immediately, close and reopen your profile or restart Discord. Never paste your Discord token anywhere."
                />
            </FormSection>

            <FormSection title="Names & bio">
                <Text style={hintStyle}>Username</Text>
                <FormInput title="" placeholder="my_username" value={String(storage.username ?? "")} onChange={(v: string) => { storage.username = v; }} />
                <Text style={hintStyle}>Display name</Text>
                <FormInput title="" placeholder="My Name" value={String(storage.displayName ?? "")} onChange={(v: string) => { storage.displayName = v; }} />
                <Text style={hintStyle}>Bio</Text>
                <FormInput title="" placeholder="My description..." value={String(storage.bio ?? "")} onChange={(v: string) => { storage.bio = v; }} />
                <Text style={hintStyle}>Pronouns</Text>
                <FormInput title="" placeholder="he/him" value={String(storage.pronouns ?? "")} onChange={(v: string) => { storage.pronouns = v; }} />
            </FormSection>

            <FormSection title="Images & colors">
                <Text style={hintStyle}>Avatar image URL</Text>
                <FormInput title="" placeholder="https://.../avatar.png" value={String(storage.avatarUrl ?? "")} onChange={(v: string) => { storage.avatarUrl = v; }} />
                <Text style={hintStyle}>Banner image URL</Text>
                <FormInput title="" placeholder="https://.../banner.png" value={String(storage.bannerUrl ?? "")} onChange={(v: string) => { storage.bannerUrl = v; }} />
                <Text style={hintStyle}>Profile color 1 (#rrggbb)</Text>
                <FormInput title="" placeholder="#5865f2" value={String(storage.accentColor ?? "")} onChange={(v: string) => { storage.accentColor = v; }} />
                <Text style={hintStyle}>Profile color 2 (#rrggbb, optional)</Text>
                <FormInput title="" placeholder="#eb459e" value={String(storage.accentColor2 ?? "")} onChange={(v: string) => { storage.accentColor2 = v; }} />
            </FormSection>

            <FormSection title="Nitro & badges">
                <FormSwitchRow
                    label="Simulate Nitro locally"
                    subLabel="Unlocks local premiumType/profile theme display where mobile Discord reads it"
                    value={!!storage.fakeNitro}
                    onValueChange={(v: boolean) => { storage.fakeNitro = v; }}
                    leading={<FormRow.Icon source={getAssetIDByName("ic_badge_staff")} />}
                />
                {BADGES.map(badge => (
                    <FormSwitchRow
                        key={badge.key}
                        label={badge.label}
                        subLabel="Adds a local fake badge to your profile"
                        value={!!storage[badge.key]}
                        onValueChange={(v: boolean) => { storage[badge.key] = v; }}
                        leading={<FormRow.Icon source={getAssetIDByName("ic_badge_staff")} />}
                    />
                ))}
            </FormSection>

            <FormSection title="Actions">
                <FormRow
                    label="Reset custom profile"
                    subLabel="Clears all local CustomProfileMobile fields"
                    onPress={resetProfile}
                    trailing={FormRow.Arrow}
                />
            </FormSection>
        </ScrollView>
    );
}

export default {
    onLoad: () => {
        initDefaults();
        installPatches();
        showToast("CustomProfileMobile loaded", getAssetIDByName("check"));
    },
    onUnload: () => {
        for (const unpatch of patches) {
            try { unpatch(); } catch { /* ignore */ }
        }
        patches = [];
    },
    settings: Settings,
};
