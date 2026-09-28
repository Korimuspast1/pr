/*
 * VisualOwnerMode — local-only visual owner/admin spoof for Discord Android mods.
 * It does NOT grant real permissions and cannot actually give roles.
 * Server-side role/permission actions still require real Discord permissions.
 */

import { findByProps, findByStoreName } from "@vendetta/metro";
import { React, ReactNative, clipboard } from "@vendetta/metro/common";
import { after } from "@vendetta/patcher";
import { storage } from "@vendetta/plugin";
import { useProxy } from "@vendetta/storage";
import { Forms } from "@vendetta/ui/components";
import { showToast } from "@vendetta/ui/toasts";
import { getAssetIDByName } from "@vendetta/ui/assets";

const { ScrollView, Text } = ReactNative;
const { FormSection, FormRow, FormSwitchRow, FormInput } = Forms;

const UserStore = findByStoreName("UserStore") || findByProps("getCurrentUser", "getUser");
const AuthStore = findByStoreName("AuthenticationStore") || findByProps("getId", "getToken");
const GuildStore = findByStoreName("GuildStore") || findByProps("getGuild", "getGuilds");
const GuildMemberStore = findByStoreName("GuildMemberStore") || findByProps("getMember", "getMembers");
const GuildRoleStore = findByStoreName("GuildRoleStore") || findByProps("getRoles", "getRole");
const PermissionStore = findByStoreName("PermissionStore") || findByProps("can", "computePermissions");
const SelectedGuildStore = findByStoreName("SelectedGuildStore") || findByProps("getGuildId", "getLastSelectedGuildId");

const cleanups: Array<() => void> = [];

const FAKE_ROLE_PREFIX = "visual-owner-mode-role";
const ALL_PERMISSIONS_STRING = "2251799813685247";

function initDefaults() {
    storage.enabled ??= true;
    storage.currentServerOnly ??= true;
    storage.targetGuildId ??= "";
    storage.visualOwner ??= true;
    storage.spoofPermissions ??= true;
    storage.showAdminButtons ??= true;
    storage.fakeOwnerRole ??= true;
    storage.fakeRoleName ??= "👑 Owner";
    storage.fakeRoleColor ??= "#f1c40f";
    storage.fakeRoleHoist ??= true;
    storage.grantAllRolesToSelf ??= false;
    storage.selfExtraRoleIds ??= "";
    storage.targetUserId ??= "";
    storage.targetRoleIds ??= "";
    storage.targetNick ??= "";
    storage.userRoleOverrides ??= {};
}

initDefaults();

function notify(message: string, icon = "check") {
    try { showToast(message, getAssetIDByName(icon)); }
    catch { showToast(message); }
}

function myId() {
    try { return UserStore?.getCurrentUser?.()?.id || AuthStore?.getId?.() || ""; }
    catch { return ""; }
}

function selectedGuildId() {
    try { return SelectedGuildStore?.getGuildId?.() || SelectedGuildStore?.getLastSelectedGuildId?.() || ""; }
    catch { return ""; }
}

function configuredGuildId() {
    return String(storage.targetGuildId || "").trim();
}

function isTargetGuild(guildId?: string | null) {
    if (!storage.enabled) return false;
    const gid = String(guildId || "");
    if (!gid) return !storage.currentServerOnly;
    const configured = configuredGuildId();
    if (configured) return gid === configured;
    if (storage.currentServerOnly) return gid === selectedGuildId();
    return true;
}

function csv(value: unknown) {
    return String(value || "")
        .split(/[ ,;\n]+/)
        .map(x => x.trim())
        .filter(Boolean);
}

function hexToInt(hex: unknown) {
    const clean = String(hex || "").trim().replace(/^#/, "");
    if (!/^[0-9a-f]{6}$/i.test(clean)) return 0xf1c40f;
    return parseInt(clean, 16);
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
        try { Object.defineProperty(clone, key, { configurable: true, enumerable: true, writable: true, value }); }
        catch { clone[key] = value; }
    }
    return clone;
}

function fakeRoleId(guildId: string) {
    return `${FAKE_ROLE_PREFIX}-${guildId || "global"}`;
}

function fakeRole(guildId: string) {
    const color = hexToInt(storage.fakeRoleColor);
    return {
        id: fakeRoleId(guildId),
        guild_id: guildId,
        guildId,
        name: String(storage.fakeRoleName || "👑 Owner"),
        color,
        colorString: `#${color.toString(16).padStart(6, "0")}`,
        hoist: !!storage.fakeRoleHoist,
        managed: false,
        mentionable: true,
        position: 9999,
        permissions: ALL_PERMISSIONS_STRING,
        permissions_new: ALL_PERMISSIONS_STRING,
        tags: null,
    };
}

function getRolesObject(guildId: string) {
    try { return GuildRoleStore?.getRoles?.(guildId) || {}; }
    catch { return {}; }
}

function getAllRoleIds(guildId: string) {
    const roles = getRolesObject(guildId);
    const ids = Object.values(roles)
        .map((r: any) => String(r?.id || ""))
        .filter(id => id && id !== guildId);
    return Array.from(new Set(ids));
}

function roleIdsForMember(guildId: string, userId: string, originalRoles: any) {
    const out = new Set<string>(Array.isArray(originalRoles) ? originalRoles.map(String) : []);
    if (!isTargetGuild(guildId)) return Array.from(out);

    const self = userId === myId();
    if (self && storage.fakeOwnerRole) out.add(fakeRoleId(guildId));
    if (self && storage.grantAllRolesToSelf) for (const id of getAllRoleIds(guildId)) out.add(id);
    if (self) for (const id of csv(storage.selfExtraRoleIds)) out.add(id);

    const override = storage.userRoleOverrides?.[userId];
    if (override) {
        if (override.fakeOwnerRole) out.add(fakeRoleId(guildId));
        for (const id of csv(override.roleIds)) out.add(id);
    }

    return Array.from(out);
}

function patchGuild(guild: any) {
    if (!storage.enabled || !storage.visualOwner || !guild?.id || !isTargetGuild(guild.id)) return guild;
    const id = myId();
    if (!id) return guild;
    return copyWithDescriptors(guild, {
        ownerId: id,
        owner_id: id,
        ownerID: id,
        isOwner: true,
    });
}

function patchMember(member: any, guildId: string, userId: string) {
    if (!storage.enabled || !member || !guildId || !userId || !isTargetGuild(guildId)) return member;
    const roles = roleIdsForMember(guildId, userId, member.roles);
    const override = storage.userRoleOverrides?.[userId];
    const isSelf = userId === myId();
    const fakeRoleObj = fakeRole(guildId);
    const nickOverride = override?.nick ? String(override.nick) : "";
    const overrides: Record<string, any> = {
        roles,
        colorRoleId: roles.includes(fakeRoleObj.id) ? fakeRoleObj.id : member.colorRoleId,
        hoistRoleId: roles.includes(fakeRoleObj.id) ? fakeRoleObj.id : member.hoistRoleId,
    };

    if (nickOverride) overrides.nick = nickOverride;
    if (isSelf && storage.visualOwner) {
        overrides.owner = true;
        overrides.isOwner = true;
    }
    return copyWithDescriptors(member, overrides);
}

function allPermsLike(ret: any) {
    if (typeof ret === "bigint") return (1n << 53n) - 1n;
    if (typeof ret === "number") return Number.MAX_SAFE_INTEGER;
    if (typeof ret === "string") return ALL_PERMISSIONS_STRING;
    if (ret && typeof ret === "object") return ret;
    return true;
}

function shouldSpoofPerms(args: any[]) {
    if (!storage.enabled || !storage.spoofPermissions) return false;
    // If a guild id is present, respect current-server filtering.
    for (const arg of args || []) {
        const gid = typeof arg === "string" ? arg : (arg?.guild_id || arg?.guildId || arg?.id);
        if (gid && isTargetGuild(String(gid))) return true;
    }
    return !storage.currentServerOnly || !!selectedGuildId();
}

function saveTargetOverride() {
    const userId = String(storage.targetUserId || "").trim();
    if (!/^\d{5,}$/.test(userId)) return notify("Enter a valid user ID", "small");
    storage.userRoleOverrides = {
        ...(storage.userRoleOverrides || {}),
        [userId]: {
            roleIds: String(storage.targetRoleIds || ""),
            nick: String(storage.targetNick || ""),
            fakeOwnerRole: true,
        },
    };
    notify("Saved visual role override");
}

function clearTargetOverride() {
    const userId = String(storage.targetUserId || "").trim();
    if (!userId) return notify("Enter user ID first", "small");
    const next = { ...(storage.userRoleOverrides || {}) };
    delete next[userId];
    storage.userRoleOverrides = next;
    notify("Cleared visual override");
}

function copySelectedGuildId() {
    const id = selectedGuildId();
    try {
        clipboard.setString(id || "");
        notify(id ? "Guild ID copied" : "No selected guild", id ? "copy" : "small");
    } catch { notify("Copy failed", "small"); }
}

function copyRoleIds() {
    const gid = configuredGuildId() || selectedGuildId();
    const roles = getRolesObject(gid);
    const text = Object.values(roles).map((r: any) => `${r?.name || "unknown"}: ${r?.id || ""}`).join("\n");
    try {
        clipboard.setString(text);
        notify("Role IDs copied", "copy");
    } catch { notify("Copy failed", "small"); }
}

function Settings() {
    useProxy(storage);
    const hint = { opacity: 0.7, marginHorizontal: 12, marginVertical: 6 } as any;
    const gid = configuredGuildId() || selectedGuildId();
    const overrideCount = Object.keys(storage.userRoleOverrides || {}).length;

    return (
        <ScrollView style={{ flex: 1 }}>
            <FormSection title="VisualOwnerMode">
                <FormSwitchRow label="Enabled" value={!!storage.enabled} onValueChange={(v: boolean) => { storage.enabled = v; }} />
                <Text style={hint}>Local-only visual spoof. It does not grant real server permissions and cannot actually give roles.</Text>
                <FormSwitchRow label="Only current / target server" value={!!storage.currentServerOnly} onValueChange={(v: boolean) => { storage.currentServerOnly = v; }} />
                <Text style={hint}>Target guild ID (empty = selected server)</Text>
                <FormInput title="" placeholder={selectedGuildId() || "server id"} value={String(storage.targetGuildId || "")} onChange={(v: string) => { storage.targetGuildId = v.replace(/[^0-9]/g, ""); }} />
                <FormRow label="Copy selected guild ID" subLabel={selectedGuildId() || "Open a server first"} onPress={copySelectedGuildId} trailing={FormRow.Arrow} />
            </FormSection>

            <FormSection title="Owner / permissions visual">
                <FormSwitchRow label="Show me as server owner" value={!!storage.visualOwner} onValueChange={(v: boolean) => { storage.visualOwner = v; }} />
                <FormSwitchRow label="Spoof permission checks" subLabel="Shows admin/manage UI locally; server will still reject real actions" value={!!storage.spoofPermissions} onValueChange={(v: boolean) => { storage.spoofPermissions = v; }} />
                <FormSwitchRow label="Show admin buttons locally" value={!!storage.showAdminButtons} onValueChange={(v: boolean) => { storage.showAdminButtons = v; }} />
            </FormSection>

            <FormSection title="Fake owner role">
                <FormSwitchRow label="Add fake owner role to me" value={!!storage.fakeOwnerRole} onValueChange={(v: boolean) => { storage.fakeOwnerRole = v; }} />
                <Text style={hint}>Fake role name</Text>
                <FormInput title="" placeholder="👑 Owner" value={String(storage.fakeRoleName || "")} onChange={(v: string) => { storage.fakeRoleName = v; }} />
                <Text style={hint}>Fake role color (#rrggbb)</Text>
                <FormInput title="" placeholder="#f1c40f" value={String(storage.fakeRoleColor || "")} onChange={(v: string) => { storage.fakeRoleColor = v; }} />
                <FormSwitchRow label="Hoist fake role" value={!!storage.fakeRoleHoist} onValueChange={(v: boolean) => { storage.fakeRoleHoist = v; }} />
            </FormSection>

            <FormSection title="My visual roles">
                <FormSwitchRow label="Give me all existing roles visually" subLabel="Only affects your client" value={!!storage.grantAllRolesToSelf} onValueChange={(v: boolean) => { storage.grantAllRolesToSelf = v; }} />
                <Text style={hint}>Extra role IDs for me, comma-separated</Text>
                <FormInput title="" placeholder="roleId1, roleId2" value={String(storage.selfExtraRoleIds || "")} onChange={(v: string) => { storage.selfExtraRoleIds = v; }} />
                <FormRow label="Copy role IDs in target server" subLabel={gid || "No server selected"} onPress={copyRoleIds} trailing={FormRow.Arrow} />
            </FormSection>

            <FormSection title="Visually give roles to another user">
                <Text style={hint}>User ID</Text>
                <FormInput title="" placeholder="1234567890" value={String(storage.targetUserId || "")} onChange={(v: string) => { storage.targetUserId = v.replace(/[^0-9]/g, ""); }} />
                <Text style={hint}>Role IDs, comma-separated. The fake owner role is also added.</Text>
                <FormInput title="" placeholder="roleId1, roleId2" value={String(storage.targetRoleIds || "")} onChange={(v: string) => { storage.targetRoleIds = v; }} />
                <Text style={hint}>Optional visual nickname</Text>
                <FormInput title="" placeholder="new local nick" value={String(storage.targetNick || "")} onChange={(v: string) => { storage.targetNick = v; }} />
                <FormRow label="Save visual role override" subLabel={`${overrideCount} override(s) saved`} onPress={saveTargetOverride} trailing={FormRow.Arrow} />
                <FormRow label="Clear this user override" onPress={clearTargetOverride} trailing={FormRow.Arrow} />
            </FormSection>
        </ScrollView>
    );
}

export default {
    onLoad: () => {
        initDefaults();

        if (GuildStore?.getGuild) {
            cleanups.push(after("getGuild", GuildStore, (_args: any[], guild: any) => patchGuild(guild)));
        }

        if (GuildMemberStore?.getMember) {
            cleanups.push(after("getMember", GuildMemberStore, (args: any[], member: any) => {
                const guildId = String(args?.[0] || "");
                const userId = String(args?.[1] || member?.userId || member?.user_id || "");
                return patchMember(member, guildId, userId);
            }));
        }

        if (GuildRoleStore?.getRoles) {
            cleanups.push(after("getRoles", GuildRoleStore, (args: any[], roles: any) => {
                const guildId = String(args?.[0] || "");
                if (!storage.enabled || !storage.fakeOwnerRole || !isTargetGuild(guildId) || !roles || typeof roles !== "object") return roles;
                return { ...roles, [fakeRoleId(guildId)]: fakeRole(guildId) };
            }));
        }

        if (GuildRoleStore?.getRole) {
            cleanups.push(after("getRole", GuildRoleStore, (args: any[], role: any) => {
                const guildId = String(args?.[0] || "");
                const roleId = String(args?.[1] || "");
                if (!role && storage.enabled && storage.fakeOwnerRole && isTargetGuild(guildId) && roleId === fakeRoleId(guildId)) return fakeRole(guildId);
                return role;
            }));
        }

        const permMethods = ["can", "canWithPartialContext", "canManageUser", "canAccessGuildSettings", "isRoleHigher", "isMemberHigher"];
        for (const method of permMethods) {
            if (PermissionStore?.[method]) {
                cleanups.push(after(method, PermissionStore, (args: any[], ret: any) => {
                    if (storage.showAdminButtons && shouldSpoofPerms(args)) return true;
                    return ret;
                }));
            }
        }
        for (const method of ["computePermissions", "getGuildPermissions", "getChannelPermissions"]) {
            if (PermissionStore?.[method]) {
                cleanups.push(after(method, PermissionStore, (args: any[], ret: any) => {
                    if (shouldSpoofPerms(args)) return allPermsLike(ret);
                    return ret;
                }));
            }
        }

        notify("VisualOwnerMode loaded");
    },
    onUnload: () => {
        while (cleanups.length) {
            try { cleanups.pop()?.(); } catch { }
        }
    },
    settings: Settings,
};
