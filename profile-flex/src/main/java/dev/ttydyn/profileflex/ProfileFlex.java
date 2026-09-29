package dev.ttydyn.profileflex;

import android.app.Application;
import android.content.Context;
import android.util.Log;

import com.windukk.hook.HookBridge;
import com.windukk.hook.HookMethod;

import java.util.Set;

/**
 * Локальное визуальное оформление профиля: галочка и произвольные счётчики.
 *
 * ВАЖНО: всё меняется только в объектах внутри процесса приложения на этом устройстве.
 * Ни один запрос на сервер TikTok не отправляется, другие пользователи изменений не видят,
 * реальную верификацию или подписчиков это не даёт.
 */
public final class ProfileFlex {

    private static final String TAG = "ProfileFlex";

    private static final String USER_CLASS = "com.ss.android.ugc.aweme.profile.model.User";
    private static final String AWEME_CLASS = "com.ss.android.ugc.aweme.feed.model.Aweme";

    private static Config config;
    private static int hookCount;
    private static boolean installed;

    private ProfileFlex() {
    }

    public static synchronized void install(Application application, Context context) {
        if (installed) {
            Log.i(TAG, "hooks already installed");
            return;
        }
        config = Config.load(context);
        hookCount = 0;
        Log.i(TAG, "config: " + config.path());

        ClassLoader loader = context.getClassLoader();
        Class<?> userClass = Reflect.findClass(loader, USER_CLASS);
        if (userClass == null) {
            userClass = Reflect.findClass(ProfileFlex.class.getClassLoader(), USER_CLASS);
        }
        if (userClass == null) {
            Log.e(TAG, "class not found: " + USER_CLASS + " — проверьте версию TikTok");
            return;
        }

        hookCounter(userClass, "getFollowerCount", Counter.FOLLOWERS);
        hookCounter(userClass, "getFollowingCount", Counter.FOLLOWING);
        hookCounter(userClass, "getTotalFavorited", Counter.LIKES);
        hookCounter(userClass, "getAwemeCount", Counter.VIDEOS);
        hookCounter(userClass, "getFavoritingCount", Counter.LIKES_GIVEN);
        hookCounter(userClass, "getFriendCount", Counter.FRIENDS);
        hookCounter(userClass, "getFriendsCount", Counter.FRIENDS);

        hookBadge(userClass, "getVerificationType", Badge.TYPE);
        hookBadge(userClass, "getCustomVerify", Badge.CUSTOM_VERIFY);
        hookBadge(userClass, "getEnterpriseVerifyReason", Badge.ENTERPRISE);
        hookBadge(userClass, "isVerified", Badge.BOOLEAN);

        Class<?> awemeClass = Reflect.findClass(loader, AWEME_CLASS);
        if (awemeClass != null) {
            hookVideoStatistics(awemeClass);
        }

        installed = true;
        Log.i(TAG, "installed, hooks: " + hookCount);
    }

    // ------------------------------------------------------------------
    // Счётчики профиля
    // ------------------------------------------------------------------
    private enum Counter { FOLLOWERS, FOLLOWING, LIKES, VIDEOS, LIKES_GIVEN, FRIENDS }

    private enum Badge { TYPE, CUSTOM_VERIFY, ENTERPRISE, BOOLEAN }

    private static void hookCounter(Class<?> userClass, final String method, final Counter counter) {
        hook(userClass, method, new HookMethod() {
            @Override
            public void afterHookedMethod(HookMethodParam param) {
                Config current = config;
                current.reloadIfChanged(false);
                if (!current.enabled || !current.spoofCounters || !isTarget(param.thisObject, current)) {
                    return;
                }
                long value;
                switch (counter) {
                    case FOLLOWERS:   value = current.followerCount; break;
                    case FOLLOWING:   value = current.followingCount; break;
                    case LIKES:       value = current.likeCount; break;
                    case VIDEOS:      value = current.videoCount; break;
                    case LIKES_GIVEN: value = current.likeCount; break;
                    case FRIENDS:     value = current.friendsCount; break;
                    default: return;
                }
                if (counter == Counter.FRIENDS && value <= 0) {
                    return;
                }
                param.setResult(Reflect.matchType(
                        param.getResult(), value, Reflect.returnType(param.method)));
                if (current.verbose) {
                    Log.d(TAG, method + " -> " + value);
                }
            }
        });
    }

    // ------------------------------------------------------------------
    // Галочка
    // ------------------------------------------------------------------
    private static void hookBadge(Class<?> userClass, final String method, final Badge kind) {
        hook(userClass, method, new HookMethod() {
            @Override
            public void afterHookedMethod(HookMethodParam param) {
                Config current = config;
                current.reloadIfChanged(false);
                if (!current.enabled || !isTarget(param.thisObject, current)) {
                    return;
                }
                boolean on = current.badgeEnabled();
                switch (kind) {
                    case TYPE:
                        param.setResult(Reflect.matchType(
                                param.getResult(), current.verificationType(), Reflect.returnType(param.method)));
                        break;
                    case CUSTOM_VERIFY:
                        param.setResult(on ? current.customVerify() : "");
                        break;
                    case ENTERPRISE:
                        // Непустая строка = бизнес-галочка. Для личной галочки поле должно быть пустым.
                        param.setResult("business".equalsIgnoreCase(current.badge) ? current.customVerify() : "");
                        break;
                    case BOOLEAN:
                        param.setResult(Boolean.valueOf(on));
                        break;
                    default:
                        break;
                }
            }
        });
    }

    // ------------------------------------------------------------------
    // Статистика роликов (лайки/просмотры под своими видео)
    // ------------------------------------------------------------------
    private static void hookVideoStatistics(Class<?> awemeClass) {
        hook(awemeClass, "getStatistics", new HookMethod() {
            @Override
            public void afterHookedMethod(HookMethodParam param) {
                Config current = config;
                current.reloadIfChanged(false);
                if (!current.enabled || !current.spoofVideoStats) {
                    return;
                }
                Object author = Reflect.callNoArg(param.thisObject, "getAuthor");
                if (!isTarget(author, current)) {
                    return;
                }
                Object statistics = param.getResult();
                if (statistics == null) {
                    return;
                }
                Reflect.setNumber(statistics, current.videoDiggCount, "diggCount", "mDiggCount");
                Reflect.setNumber(statistics, current.videoPlayCount, "playCount", "mPlayCount");
                Reflect.setNumber(statistics, current.videoCommentCount, "commentCount", "mCommentCount");
                Reflect.setNumber(statistics, current.videoShareCount, "shareCount", "mShareCount");
            }
        });
    }

    // ------------------------------------------------------------------
    // Общее
    // ------------------------------------------------------------------
    private static boolean isTarget(Object user, Config current) {
        if (user == null) {
            return false;
        }
        if (!current.selfOnly) {
            return true;
        }
        String uid = Reflect.text(Reflect.callNoArg(user, "getUid", "getUserId"));
        if (uid != null && current.uid.length() > 0 && current.uid.equals(uid)) {
            return true;
        }
        String unique = Reflect.text(Reflect.callNoArg(user, "getUniqueId", "getNickname"));
        return unique != null && current.uniqueId.length() > 0
                && current.uniqueId.equalsIgnoreCase(unique.startsWith("@") ? unique.substring(1) : unique);
    }

    private static void hook(Class<?> target, String method, HookMethod callback) {
        try {
            Set<HookMethod.Unhook> hooks = HookBridge.hookAllMethods(target, method, callback);
            int added = hooks == null ? 0 : hooks.size();
            hookCount += added;
            if (added == 0) {
                Log.w(TAG, "method not found: " + target.getSimpleName() + "." + method);
            }
        } catch (Throwable error) {
            Log.w(TAG, "hook failed: " + method, error);
        }
    }
}
