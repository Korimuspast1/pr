package com.windukk.hook;

import java.lang.reflect.Member;
import java.util.Set;

/** STUB — только для компиляции (см. docs/ttydyn-format.md). */
public final class HookBridge {

    private HookBridge() {
    }

    public static Set<HookMethod.Unhook> hookAllMethods(Class<?> clazz, String name, HookMethod callback) {
        throw new UnsupportedOperationException("stub");
    }

    public static Set<HookMethod.Unhook> hookAllConstructors(Class<?> clazz, HookMethod callback) {
        throw new UnsupportedOperationException("stub");
    }

    public static HookMethod.Unhook hookMethod(Member member, HookMethod callback) {
        throw new UnsupportedOperationException("stub");
    }

    public static Object invokeOriginalMethod(Member member, Object thisObject, Object[] args) {
        throw new UnsupportedOperationException("stub");
    }

    public static boolean deoptimizeMethod(Member member) {
        throw new UnsupportedOperationException("stub");
    }

    public static boolean disableHiddenApiRestrictions() {
        throw new UnsupportedOperationException("stub");
    }

    public static boolean pauseNativeLoggingForSnapshot() {
        throw new UnsupportedOperationException("stub");
    }

    public static void resumeNativeLoggingAfterSnapshot() {
        throw new UnsupportedOperationException("stub");
    }
}
