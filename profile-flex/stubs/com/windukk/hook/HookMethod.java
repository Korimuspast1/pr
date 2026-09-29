package com.windukk.hook;

import java.lang.reflect.Member;

/**
 * STUB — только для компиляции. Реальная реализация лежит в APK мода TikTok You
 * (движок exteraHook). В classes.dex модуля эти классы попадать не должны.
 */
public class HookMethod {

    public static class HookMethodParam {
        public Object thisObject;
        public Object[] args;
        public Member method;

        public Object getResult() {
            throw new UnsupportedOperationException("stub");
        }

        public void setResult(Object result) {
            throw new UnsupportedOperationException("stub");
        }
    }

    public interface Unhook {
        void unhook();
    }

    public void beforeHookedMethod(HookMethodParam param) {
    }

    public void afterHookedMethod(HookMethodParam param) {
    }
}
