/*
 * CustomProfileMobile — local-only Android profile customizer.
 * Target loaders: Revenge / Bunny / Vendetta-compatible Discord Android mods.
 * This plugin never asks for, stores, or sends your Discord token.
 */

import { findByName, findByProps, findByStoreName } from "@vendetta/metro";
import { React, ReactNative, clipboard } from "@vendetta/metro/common";
import { after, instead } from "@vendetta/patcher";
import { storage } from "@vendetta/plugin";
import { useProxy } from "@vendetta/storage";
import { Forms } from "@vendetta/ui/components";
import { getAssetIDByName } from "@vendetta/ui/assets";
import { showToast } from "@vendetta/ui/toasts";

const { ScrollView, Text, View, Image } = ReactNative;
const { FormSection, FormRow, FormSwitchRow, FormInput } = Forms;

const UserStore = findByStoreName("UserStore");
const UserProfileStore = findByStoreName("UserProfileStore");
const AvatarUtils = findByProps("getUserAvatarURL", "getUserAvatarSource");
const BannerUtils = findByProps("getUserBannerURL") ?? findByProps("default", "getUserBannerURL");
const ImageResolver = findByProps("getAvatarDecorationURL", "default");
const AvatarDecorationUtils = findByProps("isAnimatedAvatarDecoration");
const ProfileEffectStore = findByProps("getProfileEffectById");
const profileBadges = findByName("useBadges", false);

const CUSTOM_DECORATION_SKU = "custom-profile-mobile-decoration";
const CUSTOM_EFFECT_PREFIX = "custom-profile-mobile-effect";
const DEFAULT_AVATAR = "https://cdn.discordapp.com/embed/avatars/0.png";

let patches: Array<() => void> = [];
let currentUserId: string | undefined;

type AnyConfig = Record<string, any>;

type SelectItem = {
    label: string;
    value: string;
    subLabel?: string;
};

const BADGES = [
    { key: "badgeStaff", label: "Discord Staff", id: "staff", icon: "5e74e9b61934fc1f67c65515d1f7e60d", description: "Discord Staff" },
    { key: "badgePartner", label: "Partner", id: "partner", icon: "3f9748e53446a137a052f3454e2de41e", description: "Partnered Server Owner" },
    { key: "badgeHypeSquad", label: "HypeSquad Events", id: "hypesquad", icon: "bf01d1073931f921909045f3a39fd264", description: "HypeSquad Events" },
    { key: "badgeBugHunter", label: "Bug Hunter 1", id: "bug_hunter_level_1", icon: "2717692c7dca7289b35297368a940dd0", description: "Bug Hunter Level 1" },
    { key: "badgeBugHunter2", label: "Bug Hunter 2", id: "bug_hunter_level_2", icon: "848f79194d4be5ff5f81505cbd0ce1e6", description: "Bug Hunter Level 2" },
    { key: "badgeBravery", label: "HypeSquad Bravery", id: "hypesquad_house_1", icon: "8a88d63823d8a71cd5e390baa45efa02", description: "HypeSquad Bravery" },
    { key: "badgeBrilliance", label: "HypeSquad Brilliance", id: "hypesquad_house_2", icon: "011940fd013da3f7fb926e4a1cd2e618", description: "HypeSquad Brilliance" },
    { key: "badgeBalance", label: "HypeSquad Balance", id: "hypesquad_house_3", icon: "3aa41de486fa12454c3761e8e223442e", description: "HypeSquad Balance" },
    { key: "badgeEarly", label: "Early Supporter", id: "early_supporter", icon: "7060786766c9c840eb3019e725d2b358", description: "Early Supporter" },
    { key: "badgeVerifiedDev", label: "Early Verified Bot Dev", id: "verified_developer", icon: "6df5892e0f35b051f8b61eace34f4967", description: "Early Verified Bot Developer" },
    { key: "badgeModAlumni", label: "Moderator Alumni", id: "moderator_programs_alumni", icon: "fee1624003e2fee35cb398e125dc479b", description: "Moderator Programs Alumni" },
    { key: "badgeActiveDev", label: "Active Developer", id: "active_developer", icon: "6bdc42827a38498929a4920da12695d9", description: "Active Developer" },
    { key: "badgeQuest", label: "Quest", id: "quest_completed", icon: "7d9ae358c8c5e118768335dbe68b4fb8", description: "Completed a Quest" },
    { key: "badgeOrbs", label: "Orbs", id: "orb_profile_badge", icon: "83d8a1eb09a8d64e59233eec5d4d5c2d", description: "Orbs — Apprentice" },
    { key: "badgeGifting", label: "Gifting", id: "gifting_icon", icon: "64f2413c9b9803661322aaad25826b62", description: "Gifting Icon" },
];

const NITRO_LEVELS = [
    { label: "None", months: -1, icon: "" },
    { label: "Nitro 0 months", months: 0, icon: "2ba85e8026a8614b640c2837bcdfe21b" },
    { label: "Nitro 1 month", months: 1, icon: "4f33c4a9c64ce221936bd256c356f91f" },
    { label: "Nitro 3 months", months: 3, icon: "4514fab914bdbfb4ad2fa23df76121a6" },
    { label: "Nitro 6 months", months: 6, icon: "2895086c18d5531d499862e41d1155a6" },
    { label: "Nitro 12 months", months: 12, icon: "0334688279c8359120922938dcb1d6f8" },
    { label: "Nitro 24 months", months: 24, icon: "0d61871f72bb9a33a7ae568c1fb4f20a" },
    { label: "Nitro 36 months", months: 36, icon: "11e2d339068b55d3a506cff34d3780f3" },
    { label: "Nitro 60 months", months: 60, icon: "cd5e2cfd9d7f27a8cdcd3e8a8d5dc9f4" },
    { label: "Nitro 72 months", months: 72, icon: "5b154df19c53dce2af92c9b61e6be5e2" },
];

const BOOST_LEVELS = [
    { label: "None", months: -1, icon: "" },
    { label: "Boost 1 month", months: 1, level: "lvl1", icon: "51040c70d4f20a921ad6674ff86fc95c" },
    { label: "Boost 2 months", months: 2, level: "lvl2", icon: "0e4080d1d333bc7ad29ef6528b6f2fb7" },
    { label: "Boost 3 months", months: 3, level: "lvl3", icon: "72bed924410c304dbe3d00a6e593ff59" },
    { label: "Boost 6 months", months: 6, level: "lvl4", icon: "df199d2050d3ed4ebf84d64ae83989f8" },
    { label: "Boost 9 months", months: 9, level: "lvl5", icon: "996b3e870e8a22ce519b3a50e6bdd52f" },
    { label: "Boost 12 months", months: 12, level: "lvl6", icon: "991c9f39ee33d7537d9f408c3e53141e" },
    { label: "Boost 15 months", months: 15, level: "lvl7", icon: "cb3ae83c15e970e8f3d410bc62cb8b99" },
    { label: "Boost 18 months", months: 18, level: "lvl8", icon: "7142225d31238f6387d9f09efaa02759" },
    { label: "Boost 24 months", months: 24, level: "lvl9", icon: "ec92202290b48d0879b7413d2dde3bab" },
];

const DECORATIONS = [
    { label: "None", value: "" },
    { label: "Hearts", value: "1144307957425778779" },
    { label: "Lofi Cafe", value: "1212569433839636530" },
    { label: "Winter", value: "1481387347642810480" },
    { label: "Magic Orb", value: "1343751617362661526" },
    { label: "Dragon", value: "1373015260465987705" },
    { label: "Ghost", value: "1333866045303423026" },
    { label: "Sakura", value: "1144308439720394944" },
    { label: "Neon", value: "1432550258126229565" },
    { label: "Cyber City", value: "1462116613632426014" },
    { label: "Fire", value: "1144307629225672846" },
    { label: "Void", value: "1341506443718688768" },
    { label: "Celestial", value: "1447654090640330763" },
    { label: "Ice", value: "1479561706672885811" },
    { label: "Cozy", value: "1212569856189407352" },
];

const EFFECTS = [
    { label: "None", value: "" },
    { label: "Sakura Dreams", value: `${CUSTOM_EFFECT_PREFIX}-sakura` },
    { label: "Galaxy", value: `${CUSTOM_EFFECT_PREFIX}-galaxy` },
    { label: "Lightning", value: `${CUSTOM_EFFECT_PREFIX}-lightning` },
    { label: "Snow", value: `${CUSTOM_EFFECT_PREFIX}-snow` },
    { label: "Fire", value: `${CUSTOM_EFFECT_PREFIX}-fire` },
];

const CONFIG_KEYS = [
    "username", "displayName", "bio", "pronouns", "avatarUrl", "bannerUrl",
    "accentColor", "accentColor2", "fakeNitro", "nitroLevel", "boostLevel",
    "decorationAsset", "customDecorationUrl", "profileEffectId", "customEffectId",
    "clanTag", "nameplateText", "orbsBalance", "extraProfileNote",
    "conn1Type", "conn1Name", "conn1Url", "conn2Type", "conn2Name", "conn2Url", "conn3Type", "conn3Name", "conn3Url",
    ...BADGES.map(b => b.key),
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
    storage.nitroLevel ??= "0";
    storage.boostLevel ??= "0";
    storage.decorationAsset ??= "";
    storage.customDecorationUrl ??= "";
    storage.profileEffectId ??= "";
    storage.customEffectId ??= "";
    storage.clanTag ??= "";
    storage.nameplateText ??= "";
    storage.orbsBalance ??= "";
    storage.extraProfileNote ??= "";
    storage.conn1Type ??= "github";
    storage.conn1Name ??= "";
    storage.conn1Url ??= "";
    storage.conn2Type ??= "spotify";
    storage.conn2Name ??= "";
    storage.conn2Url ??= "";
    storage.conn3Type ??= "steam";
    storage.conn3Name ??= "";
    storage.conn3Url ??= "";
    storage.importJson ??= "";
    storage.overrideUserId ??= "";
    storage.overrideDisplayName ??= "";
    storage.overrideUsername ??= "";
    storage.overrideAvatarUrl ??= "";
    storage.overrideBannerUrl ??= "";
    storage.overrideBio ??= "";
    storage.userOverrides ??= {};
    storage.profileSlots ??= {};
    for (const badge of BADGES) storage[badge.key] ??= false;
}

initDefaults();

function notify(message: string, icon = "check") {
    try { showToast(message, getAssetIDByName(icon)); }
    catch { showToast(message); }
}

function cleanUrl(value: unknown) {
    const url = String(value ?? "").trim();
    if (!url) return "";
    if (!/^https?:\/\//i.test(url)) return "";
    return url;
}

function hexToInt(value: unknown): number | undefined {
    const hex = String(value ?? "").trim().replace(/^#/, "");
    if (!/^[0-9a-f]{6}$/i.test(hex)) return undefined;
    return parseInt(hex, 16);
}

function intFromInput(value: unknown, fallback = 0) {
    const parsed = parseInt(String(value ?? "").replace(/[^0-9-]/g, ""), 10);
    return Number.isFinite(parsed) ? parsed : fallback;
}

function getSelfId() {
    try {
        currentUserId ||= UserStore?.getCurrentUser?.()?.id;
    } catch { /* ignore */ }
    return currentUserId;
}

function isMe(id?: string | null) {
    return !!id && !!getSelfId() && id === getSelfId();
}

function exportConfigObject(): AnyConfig {
    const out: AnyConfig = {};
    for (const key of CONFIG_KEYS) out[key] = storage[key];
    return out;
}

function applyConfig(config: AnyConfig) {
    if (!config || typeof config !== "object") return;
    for (const key of CONFIG_KEYS) {
        if (Object.prototype.hasOwnProperty.call(config, key)) storage[key] = config[key];
    }
}

function getConfigForUser(id?: string | null): AnyConfig | null {
    if (!id) return null;
    if (isMe(id)) return exportConfigObject();
    const override = storage.userOverrides?.[id];
    return override && typeof override === "object" ? override : null;
}

function hasAnyOverride(config: AnyConfig | null) {
    if (!config) return false;
    return !!(
        config.username || config.displayName || config.bio || config.pronouns ||
        config.avatarUrl || config.bannerUrl || config.fakeNitro ||
        intFromInput(config.nitroLevel, 0) > 0 || intFromInput(config.boostLevel, 0) > 0 ||
        config.decorationAsset || config.customDecorationUrl || config.profileEffectId || config.customEffectId ||
        config.clanTag || config.nameplateText || config.orbsBalance || config.extraProfileNote ||
        getConnections(config).length ||
        hexToInt(config.accentColor) != null || hexToInt(config.accentColor2) != null ||
        BADGES.some(b => config[b.key])
    );
}

function monthsAgo(months: number) {
    return new Date(Date.now() - Math.max(0, months) * 30 * 24 * 60 * 60 * 1000);
}

function badgeIconUrl(icon: string) {
    if (!icon) return "";
    if (/^https?:\/\//i.test(icon)) return icon;
    return `https://cdn.discordapp.com/badge-icons/${icon}.png`;
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

function getDecorationAsset(config: AnyConfig | null) {
    if (!config) return "";
    return cleanUrl(config.customDecorationUrl) || String(config.decorationAsset || "").trim();
}

function getDecorationUrl(asset: string, animated = true) {
    if (!asset) return "";
    if (/^https?:\/\//i.test(asset)) return asset;
    if (asset.startsWith("preset:")) {
        return `https://cdn.discordapp.com/avatar-decoration-presets/${asset.slice(7)}.png`;
    }
    return `https://cdn.discordapp.com/media/v1/collectibles-shop/${asset}/${animated ? "animated" : "static"}`;
}

function getEffectId(config: AnyConfig | null) {
    if (!config) return "";
    return String(config.customEffectId || config.profileEffectId || "").trim();
}

function getConnections(config: AnyConfig | null) {
    if (!config) return [];
    const rows = [1, 2, 3].map(i => ({
        type: String(config[`conn${i}Type`] || "").trim().toLowerCase(),
        name: String(config[`conn${i}Name`] || "").trim(),
        url: cleanUrl(config[`conn${i}Url`]),
    }));
    return rows.filter(c => c.type && c.name).map((c, i) => ({
        id: `custom-profile-mobile-${c.type}-${i}`,
        type: c.type,
        name: c.name,
        verified: true,
        metadata: c.url ? { url: c.url, verified: "true" } : { verified: "true" },
    }));
}

function getCustomBadges(config: AnyConfig | null) {
    if (!storage.enabled || !config) return [];
    const badges: any[] = [];

    const nitroIdx = intFromInput(config.nitroLevel, 0);
    const nitro = NITRO_LEVELS[nitroIdx];
    if (config.fakeNitro || (nitro && nitro.months >= 0)) {
        const n = nitro && nitro.months >= 0 ? nitro : NITRO_LEVELS[1];
        badges.push({
            id: `premium_tenure_${n.months}_month_v2`,
            icon: n.icon,
            description: n.months <= 0 ? "Subscriber since today" : `Subscriber since ${n.months} month${n.months === 1 ? "" : "s"}`,
        });
    }

    const boostIdx = intFromInput(config.boostLevel, 0);
    const boost = BOOST_LEVELS[boostIdx];
    if (boost && boost.months >= 0) {
        badges.push({
            id: `guild_booster_${boost.level}`,
            icon: boost.icon,
            description: `Server boosting since ${boost.months} month${boost.months === 1 ? "" : "s"}`,
        });
    }

    for (const b of BADGES) {
        if (config[b.key]) {
            badges.push({
                id: `custom-profile-mobile-${b.id}`,
                icon: b.icon,
                description: b.description,
            });
        }
    }
    return badges;
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

function patchUser(user: any) {
    if (!storage.enabled || !user) return user;
    const config = getConfigForUser(user.id);
    if (!hasAnyOverride(config)) return user;

    const displayName = String(config!.displayName || "").trim();
    const username = String(config!.username || "").trim();
    const avatar = cleanUrl(config!.avatarUrl);
    const decorationAsset = getDecorationAsset(config);
    const clanTag = String(config!.clanTag || "").trim().slice(0, 8);
    const nameplateText = String(config!.nameplateText || "").trim().slice(0, 32);

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
    if (config!.fakeNitro || intFromInput(config!.nitroLevel, 0) > 0) overrides.premiumType = 2;
    if (decorationAsset) {
        overrides.avatarDecoration = { asset: decorationAsset, skuId: CUSTOM_DECORATION_SKU };
        overrides.avatarDecorationData = { asset: decorationAsset, skuId: CUSTOM_DECORATION_SKU };
    }
    if (clanTag) {
        overrides.primaryGuild = {
            ...(user.primaryGuild ?? {}),
            tag: clanTag,
            identityEnabled: true,
            identityGuildId: user.primaryGuild?.identityGuildId ?? "0",
        };
        overrides.clan = { ...(user.clan ?? {}), tag: clanTag, identityEnabled: true };
    }
    if (nameplateText) {
        overrides.nameplate = { label: nameplateText, text: nameplateText };
        overrides.nameplateText = nameplateText;
    }

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
    if (!storage.enabled || !profile) return profile;
    const config = getConfigForUser(profile.userId);
    if (!hasAnyOverride(config)) return profile;

    const primary = hexToInt(config!.accentColor);
    const accent = hexToInt(config!.accentColor2) ?? primary;
    const banner = cleanUrl(config!.bannerUrl);
    const effectId = getEffectId(config);
    const connections = getConnections(config);
    const orbsBalance = intFromInput(config!.orbsBalance, -1);
    const extraNote = String(config!.extraProfileNote || "").trim();
    const nitroIdx = intFromInput(config!.nitroLevel, 0);
    const boostIdx = intFromInput(config!.boostLevel, 0);
    const nitro = NITRO_LEVELS[nitroIdx];
    const boost = BOOST_LEVELS[boostIdx];

    const overrides: Record<string, any> = {};
    if (config!.bio || extraNote) {
        overrides.bio = [String(config!.bio || ""), extraNote ? `\n${extraNote}` : ""].join("").trim();
    }
    if (config!.pronouns) overrides.pronouns = String(config!.pronouns);
    if (primary != null) {
        overrides.accentColor = primary;
        overrides.themeColors = [primary, accent ?? primary];
    }
    if (banner) overrides.banner = banner;
    if (config!.fakeNitro || (nitro && nitro.months >= 0) || primary != null || banner || effectId) {
        overrides.premiumType = 2;
        overrides.premiumSince = monthsAgo(nitro?.months ?? 0);
    }
    if (boost && boost.months >= 0) {
        overrides.premiumGuildSince = monthsAgo(boost.months);
    }
    if (effectId) {
        overrides.profileEffectId = effectId;
        overrides.profileEffectID = effectId;
        overrides.profileEffect = { expireAt: null, skuId: effectId };
        if (!overrides.premiumType) overrides.premiumType = 2;
    }
    if (connections.length) {
        const existing = profile.connectedAccounts || profile.connected_accounts || [];
        overrides.connectedAccounts = [...existing, ...connections];
        overrides.connected_accounts = [...existing, ...connections];
    }
    if (orbsBalance >= 0) {
        overrides.orbsBalance = orbsBalance;
        overrides.orbBalance = orbsBalance;
        overrides.orbs = orbsBalance;
        overrides.currencyBalance = orbsBalance;
    }

    // Badges are injected only through useBadges below to avoid duplicates.
    return Object.keys(overrides).length ? copyWithDescriptors(profile, overrides) : profile;
}

function makeEffect(id: string) {
    const label = EFFECTS.find(e => e.value === id)?.label ?? "Custom Profile Effect";
    return {
        type: 0,
        id,
        sku_id: id,
        title: label,
        description: label,
        accessibilityLabel: label,
        animationType: 0,
        thumbnailPreviewSrc: "",
        reducedMotionSrc: "",
        effects: [],
    };
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
            const config = getConfigForUser(user?.id ?? user?.userId);
            const url = cleanUrl(config?.avatarUrl);
            return storage.enabled && url ? url : ret;
        }));
    }

    if (AvatarUtils?.getUserAvatarSource) {
        patches.push(after("getUserAvatarSource", AvatarUtils, ([user]: any[], ret: any) => {
            const config = getConfigForUser(user?.id ?? user?.userId);
            const url = cleanUrl(config?.avatarUrl);
            return storage.enabled && url ? { uri: url } : ret;
        }));
    }

    if (BannerUtils?.getUserBannerURL) {
        patches.push(after("getUserBannerURL", BannerUtils, ([user]: any[], ret: any) => {
            const config = getConfigForUser(user?.id ?? user?.userId);
            const url = cleanUrl(config?.bannerUrl);
            return storage.enabled && url ? url : ret;
        }));
    }

    if (ImageResolver?.getAvatarDecorationURL) {
        patches.push(instead("getAvatarDecorationURL", ImageResolver, (args: any[], orig: (...args: any[]) => any) => {
            const [opts] = args;
            const deco = opts?.avatarDecorationData ?? opts?.avatarDecoration;
            if (deco?.skuId === CUSTOM_DECORATION_SKU || deco?.sku_id === CUSTOM_DECORATION_SKU) {
                return getDecorationUrl(String(deco.asset || ""), opts?.canAnimate ?? opts?.animated ?? true);
            }
            return orig(...args);
        }));
    }

    if (AvatarDecorationUtils?.isAnimatedAvatarDecoration) {
        patches.push(after("isAnimatedAvatarDecoration", AvatarDecorationUtils, ([deco]: any[], ret: any) => {
            if (deco?.skuId === CUSTOM_DECORATION_SKU || deco?.sku_id === CUSTOM_DECORATION_SKU) return true;
            return ret;
        }));
    }

    if (ProfileEffectStore?.getProfileEffectById) {
        patches.push(after("getProfileEffectById", ProfileEffectStore, ([id]: any[], ret: any) => {
            if (ret) return ret;
            const effectId = String(id || "");
            if (effectId.startsWith(CUSTOM_EFFECT_PREFIX)) return makeEffect(effectId);
            return ret;
        }));
    }

    if (profileBadges?.default) {
        patches.push(after("default", profileBadges, ([user]: any[], ret: any) => {
            const userId = user?.userId ?? user?.id;
            const config = getConfigForUser(userId);
            if (!storage.enabled || !config) return ret;
            const customBadges = getCustomBadges(config);
            if (!customBadges.length) return ret;
            return mergeBadges(customBadges, Array.isArray(ret) ? ret : []);
        }));
    }
}

function resetProfile() {
    for (const key of CONFIG_KEYS) {
        if (typeof storage[key] === "boolean") storage[key] = false;
        else storage[key] = "";
    }
    storage.fakeNitro = false;
    storage.nitroLevel = "0";
    storage.boostLevel = "0";
    notify("CustomProfileMobile reset. Reopen the profile to refresh.", "ic_message_retry");
}

function copyExport() {
    try {
        clipboard.setString(JSON.stringify(exportConfigObject(), null, 2));
        notify("Profile JSON copied to clipboard");
    } catch (e) {
        notify("Failed to copy JSON", "small");
    }
}

function importFromField() {
    try {
        const parsed = JSON.parse(String(storage.importJson || ""));
        applyConfig(parsed);
        storage.importJson = "";
        notify("Imported profile config");
    } catch {
        notify("Invalid JSON", "small");
    }
}

function saveSlot(slot: string) {
    storage.profileSlots = { ...(storage.profileSlots ?? {}), [slot]: exportConfigObject() };
    notify(`Saved slot ${slot}`);
}

function loadSlot(slot: string) {
    const config = storage.profileSlots?.[slot];
    if (!config) return notify(`Slot ${slot} is empty`, "small");
    applyConfig(config);
    notify(`Loaded slot ${slot}`);
}

function deleteSlot(slot: string) {
    const next = { ...(storage.profileSlots ?? {}) };
    delete next[slot];
    storage.profileSlots = next;
    notify(`Deleted slot ${slot}`);
}

function saveUserOverride() {
    const id = String(storage.overrideUserId || "").trim();
    if (!/^\d{5,}$/.test(id)) return notify("Enter a valid user ID", "small");
    storage.userOverrides = {
        ...(storage.userOverrides ?? {}),
        [id]: {
            displayName: storage.overrideDisplayName,
            username: storage.overrideUsername,
            avatarUrl: storage.overrideAvatarUrl,
            bannerUrl: storage.overrideBannerUrl,
            bio: storage.overrideBio,
        },
    };
    notify("Saved local user override");
}

function removeUserOverride() {
    const id = String(storage.overrideUserId || "").trim();
    const next = { ...(storage.userOverrides ?? {}) };
    delete next[id];
    storage.userOverrides = next;
    notify("Removed local user override");
}

function applyPreset(name: string) {
    resetProfile();
    if (name === "nitro") {
        storage.fakeNitro = true;
        storage.nitroLevel = "5";
        storage.boostLevel = "3";
        storage.badgeActiveDev = true;
        storage.badgeQuest = true;
        storage.accentColor = "#5865f2";
        storage.accentColor2 = "#eb459e";
    } else if (name === "staff") {
        storage.fakeNitro = true;
        storage.badgeStaff = true;
        storage.badgePartner = true;
        storage.badgeActiveDev = true;
        storage.nitroLevel = "6";
        storage.clanTag = "STAFF";
        storage.accentColor = "#43b581";
        storage.accentColor2 = "#5865f2";
    } else if (name === "anime") {
        storage.fakeNitro = true;
        storage.badgeEarly = true;
        storage.badgeOrbs = true;
        storage.nitroLevel = "4";
        storage.decorationAsset = "1144308439720394944";
        storage.profileEffectId = `${CUSTOM_EFFECT_PREFIX}-sakura`;
        storage.accentColor = "#ff7ac8";
        storage.accentColor2 = "#8a7cff";
    } else if (name === "dark") {
        storage.fakeNitro = true;
        storage.badgeBugHunter2 = true;
        storage.nitroLevel = "7";
        storage.decorationAsset = "1341506443718688768";
        storage.profileEffectId = `${CUSTOM_EFFECT_PREFIX}-galaxy`;
        storage.accentColor = "#111111";
        storage.accentColor2 = "#5865f2";
    }
    notify(`Applied preset: ${name}`);
}

function SelectRows({ title, items, value, onChange }: { title: string; items: SelectItem[]; value: string; onChange: (value: string) => void; }) {
    return (
        <FormSection title={title}>
            {items.map(item => (
                <FormRow
                    key={item.value || "none"}
                    label={`${value === item.value ? "✓ " : ""}${item.label}`}
                    subLabel={item.subLabel ?? item.value}
                    onPress={() => onChange(item.value)}
                    trailing={FormRow.Arrow}
                />
            ))}
        </FormSection>
    );
}

function PreviewCard() {
    const badges = getCustomBadges(exportConfigObject()).slice(0, 9);
    const avatar = cleanUrl(storage.avatarUrl) || DEFAULT_AVATAR;
    const banner = cleanUrl(storage.bannerUrl);
    const name = String(storage.displayName || storage.username || "CustomProfileMobile");
    const user = String(storage.username || "username");
    const bio = String(storage.bio || "Your bio preview will appear here.");
    const primary = String(storage.accentColor || "#29353a");

    return (
        <View style={{ marginHorizontal: 12, marginVertical: 8, borderRadius: 16, overflow: "hidden", backgroundColor: "#1f2b30" }}>
            <View style={{ height: 86, backgroundColor: primary }}>
                {banner ? <Image source={{ uri: banner }} style={{ width: "100%", height: 86 }} /> : null}
            </View>
            <View style={{ padding: 14, paddingTop: 34 }}>
                <Image source={{ uri: avatar }} style={{ width: 72, height: 72, borderRadius: 36, position: "absolute", top: -42, left: 14, borderWidth: 4, borderColor: "#1f2b30" }} />
                <Text style={{ color: "white", fontSize: 22, fontWeight: "700" }}>{name}</Text>
                <Text style={{ color: "#c9d1d9", fontSize: 14 }}>{user}</Text>
                {!!badges.length && (
                    <View style={{ flexDirection: "row", flexWrap: "wrap", marginTop: 8 }}>
                        {badges.map((badge, i) => (
                            <Image key={`${badge.id}-${i}`} source={{ uri: badgeIconUrl(badge.icon) }} style={{ width: 22, height: 22, marginRight: 6, marginBottom: 6 }} />
                        ))}
                    </View>
                )}
                <Text style={{ color: "white", opacity: 0.9, marginTop: 8 }}>{bio}</Text>
            </View>
        </View>
    );
}

function Settings() {
    useProxy(storage);
    const hintStyle = { opacity: 0.7, marginHorizontal: 12, marginBottom: 4, marginTop: 8 } as any;

    return (
        <ScrollView>
            <FormSection title="CustomProfileMobile v2">
                <FormSwitchRow
                    label="Enabled"
                    subLabel="Local-only: changes are visible only on this phone"
                    value={!!storage.enabled}
                    onValueChange={(v: boolean) => { storage.enabled = v; }}
                    leading={<FormRow.Icon source={getAssetIDByName("settings")} />}
                />
                <FormRow label="Refresh note" subLabel="After changing major options, close and reopen the profile or restart Discord." />
            </FormSection>

            <PreviewCard />

            <FormSection title="Presets">
                <FormRow label="Nitro profile" subLabel="Nitro + boost + developer style" onPress={() => applyPreset("nitro")} trailing={FormRow.Arrow} />
                <FormRow label="Staff profile" subLabel="Staff/partner/developer badges" onPress={() => applyPreset("staff")} trailing={FormRow.Arrow} />
                <FormRow label="Anime profile" subLabel="Pink theme, orbs, sakura decoration" onPress={() => applyPreset("anime")} trailing={FormRow.Arrow} />
                <FormRow label="Dark profile" subLabel="Dark colors and galaxy-style options" onPress={() => applyPreset("dark")} trailing={FormRow.Arrow} />
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
                <Text style={hintStyle}>Extra profile note</Text>
                <FormInput title="" placeholder="Additional local-only line" value={String(storage.extraProfileNote ?? "")} onChange={(v: string) => { storage.extraProfileNote = v; }} />
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

            <FormSection title="Nitro & boost">
                <FormSwitchRow label="Simulate Nitro locally" subLabel="Best-effort premiumType/profile theme display" value={!!storage.fakeNitro} onValueChange={(v: boolean) => { storage.fakeNitro = v; }} leading={<FormRow.Icon source={getAssetIDByName("ic_badge_staff")} />} />
                <Text style={hintStyle}>Nitro level index: 0 none, 1-9 levels</Text>
                <FormInput title="" placeholder="5" value={String(storage.nitroLevel ?? "0")} onChange={(v: string) => { storage.nitroLevel = v.replace(/[^0-9]/g, ""); }} />
                <Text style={hintStyle}>Boost level index: 0 none, 1-9 levels</Text>
                <FormInput title="" placeholder="3" value={String(storage.boostLevel ?? "0")} onChange={(v: string) => { storage.boostLevel = v.replace(/[^0-9]/g, ""); }} />
            </FormSection>

            <FormSection title="Badges">
                {BADGES.map(badge => (
                    <FormSwitchRow key={badge.key} label={badge.label} subLabel={badge.description} value={!!storage[badge.key]} onValueChange={(v: boolean) => { storage[badge.key] = v; }} leading={<FormRow.Icon source={getAssetIDByName("ic_badge_staff")} />} />
                ))}
            </FormSection>

            <SelectRows title="Avatar decoration" items={DECORATIONS} value={String(storage.decorationAsset ?? "")} onChange={(v: string) => { storage.decorationAsset = v; }} />
            <FormSection title="Custom decoration URL">
                <Text style={hintStyle}>Direct image/gif URL overrides the preset above</Text>
                <FormInput title="" placeholder="https://.../decoration.png" value={String(storage.customDecorationUrl ?? "")} onChange={(v: string) => { storage.customDecorationUrl = v; }} />
            </FormSection>

            <SelectRows title="Profile effect" items={EFFECTS} value={String(storage.profileEffectId ?? "")} onChange={(v: string) => { storage.profileEffectId = v; }} />
            <FormSection title="Custom effect ID">
                <Text style={hintStyle}>Paste an official effect sku/id if you have one</Text>
                <FormInput title="" placeholder="effect id" value={String(storage.customEffectId ?? "")} onChange={(v: string) => { storage.customEffectId = v; }} />
            </FormSection>

            <FormSection title="Nameplate / clan / orbs">
                <Text style={hintStyle}>Clan tag (best-effort)</Text>
                <FormInput title="" placeholder="TAG" value={String(storage.clanTag ?? "")} onChange={(v: string) => { storage.clanTag = v.slice(0, 8); }} />
                <Text style={hintStyle}>Nameplate text (best-effort)</Text>
                <FormInput title="" placeholder="Custom nameplate" value={String(storage.nameplateText ?? "")} onChange={(v: string) => { storage.nameplateText = v; }} />
                <Text style={hintStyle}>Fake Orbs Balance (best-effort)</Text>
                <FormInput title="" placeholder="2170" value={String(storage.orbsBalance ?? "")} onChange={(v: string) => { storage.orbsBalance = v.replace(/[^0-9]/g, ""); }} />
            </FormSection>

            <FormSection title="Fake connections">
                {[1, 2, 3].map(i => (
                    <View key={`conn-${i}`}>
                        <Text style={hintStyle}>Connection {i}: type</Text>
                        <FormInput title="" placeholder="github / spotify / steam" value={String(storage[`conn${i}Type`] ?? "")} onChange={(v: string) => { storage[`conn${i}Type`] = v; }} />
                        <Text style={hintStyle}>Connection {i}: name</Text>
                        <FormInput title="" placeholder="username" value={String(storage[`conn${i}Name`] ?? "")} onChange={(v: string) => { storage[`conn${i}Name`] = v; }} />
                        <Text style={hintStyle}>Connection {i}: URL</Text>
                        <FormInput title="" placeholder="https://..." value={String(storage[`conn${i}Url`] ?? "")} onChange={(v: string) => { storage[`conn${i}Url`] = v; }} />
                    </View>
                ))}
            </FormSection>

            <FormSection title="Profile slots">
                {["1", "2", "3"].map(slot => (
                    <View key={`slot-${slot}`}>
                        <FormRow label={`Save slot ${slot}`} subLabel="Save current profile settings" onPress={() => saveSlot(slot)} trailing={FormRow.Arrow} />
                        <FormRow label={`Load slot ${slot}`} subLabel={storage.profileSlots?.[slot] ? "Load saved profile" : "Empty"} onPress={() => loadSlot(slot)} trailing={FormRow.Arrow} />
                        <FormRow label={`Delete slot ${slot}`} subLabel="Remove saved profile" onPress={() => deleteSlot(slot)} trailing={FormRow.Arrow} />
                    </View>
                ))}
            </FormSection>

            <FormSection title="Import / export">
                <FormRow label="Copy current profile JSON" subLabel="Share/backup your preset" onPress={copyExport} trailing={FormRow.Arrow} />
                <Text style={hintStyle}>Paste profile JSON to import</Text>
                <FormInput title="" placeholder="{ ... }" value={String(storage.importJson ?? "")} onChange={(v: string) => { storage.importJson = v; }} />
                <FormRow label="Import JSON" subLabel="Applies pasted config" onPress={importFromField} trailing={FormRow.Arrow} />
            </FormSection>

            <FormSection title="Local overrides for other users">
                <Text style={hintStyle}>User ID</Text>
                <FormInput title="" placeholder="1234567890" value={String(storage.overrideUserId ?? "")} onChange={(v: string) => { storage.overrideUserId = v.replace(/[^0-9]/g, ""); }} />
                <Text style={hintStyle}>Display name</Text>
                <FormInput title="" placeholder="Local name" value={String(storage.overrideDisplayName ?? "")} onChange={(v: string) => { storage.overrideDisplayName = v; }} />
                <Text style={hintStyle}>Username</Text>
                <FormInput title="" placeholder="local_username" value={String(storage.overrideUsername ?? "")} onChange={(v: string) => { storage.overrideUsername = v; }} />
                <Text style={hintStyle}>Avatar URL</Text>
                <FormInput title="" placeholder="https://..." value={String(storage.overrideAvatarUrl ?? "")} onChange={(v: string) => { storage.overrideAvatarUrl = v; }} />
                <Text style={hintStyle}>Banner URL</Text>
                <FormInput title="" placeholder="https://..." value={String(storage.overrideBannerUrl ?? "")} onChange={(v: string) => { storage.overrideBannerUrl = v; }} />
                <Text style={hintStyle}>Bio</Text>
                <FormInput title="" placeholder="Local bio" value={String(storage.overrideBio ?? "")} onChange={(v: string) => { storage.overrideBio = v; }} />
                <FormRow label="Save user override" subLabel="Only visible locally" onPress={saveUserOverride} trailing={FormRow.Arrow} />
                <FormRow label="Remove user override" subLabel="Uses the User ID above" onPress={removeUserOverride} trailing={FormRow.Arrow} />
            </FormSection>

            <FormSection title="Actions">
                <FormRow label="Reset custom profile" subLabel="Clears all local CustomProfileMobile fields" onPress={resetProfile} trailing={FormRow.Arrow} />
            </FormSection>
        </ScrollView>
    );
}

export default {
    onLoad: () => {
        initDefaults();
        installPatches();
        notify("CustomProfileMobile v2 loaded");
    },
    onUnload: () => {
        for (const unpatch of patches) {
            try { unpatch(); } catch { /* ignore */ }
        }
        patches = [];
    },
    settings: Settings,
};
