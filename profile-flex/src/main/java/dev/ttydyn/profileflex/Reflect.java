package dev.ttydyn.profileflex;

import android.util.Log;

import java.lang.reflect.Field;
import java.lang.reflect.Member;
import java.lang.reflect.Method;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

/** Небольшие помощники рефлексии: имена полей TikTok меняются от версии к версии. */
public final class Reflect {

    private static final String TAG = "ProfileFlex";
    private static final Map<String, Field> FIELD_CACHE = new ConcurrentHashMap<String, Field>();

    private Reflect() {
    }

    public static Class<?> findClass(ClassLoader loader, String name) {
        try {
            return Class.forName(name, false, loader);
        } catch (Throwable ignored) {
            return null;
        }
    }

    public static Object callNoArg(Object target, String... candidates) {
        if (target == null) {
            return null;
        }
        for (String name : candidates) {
            Class<?> current = target.getClass();
            while (current != null && current != Object.class) {
                try {
                    Method method = current.getDeclaredMethod(name);
                    method.setAccessible(true);
                    return method.invoke(target);
                } catch (NoSuchMethodException ignored) {
                    current = current.getSuperclass();
                } catch (Throwable error) {
                    return null;
                }
            }
        }
        return null;
    }

    public static Field findField(Class<?> type, String... candidates) {
        for (String name : candidates) {
            String key = type.getName() + "#" + name;
            Field cached = FIELD_CACHE.get(key);
            if (cached != null) {
                return cached;
            }
            Class<?> current = type;
            while (current != null && current != Object.class) {
                try {
                    Field field = current.getDeclaredField(name);
                    try {
                        field.setAccessible(true);
                    } catch (Throwable ignored) {
                        // На новых Android скрытые поля могут не разрешать
                        // setAccessible; обычный field.set всё равно попробуем.
                    }
                    FIELD_CACHE.put(key, field);
                    return field;
                } catch (NoSuchFieldException ignored) {
                    current = current.getSuperclass();
                }
            }
        }
        return null;
    }

    public static boolean setNumber(Object target, long value, String... candidates) {
        if (target == null) {
            return false;
        }
        Field field = findField(target.getClass(), candidates);
        if (field == null) {
            return false;
        }
        try {
            Class<?> type = field.getType();
            if (type == int.class || type == Integer.class) {
                field.set(target, Integer.valueOf((int) Math.min(value, Integer.MAX_VALUE)));
            } else if (type == long.class || type == Long.class) {
                field.set(target, Long.valueOf(value));
            } else if (type == String.class) {
                field.set(target, String.valueOf(value));
            } else {
                return false;
            }
            return true;
        } catch (Throwable error) {
            Log.w(TAG, "setNumber failed for " + field.getName(), error);
            return false;
        }
    }

    public static boolean setObject(Object target, Object value, String... candidates) {
        if (target == null) {
            return false;
        }
        Field field = findField(target.getClass(), candidates);
        if (field == null) {
            return false;
        }
        try {
            field.set(target, value);
            return true;
        } catch (Throwable error) {
            return false;
        }
    }

    /**
     * Приводит значение к типу, который вернул оригинальный метод: TikTok держит счётчики
     * то как int, то как long, то как строку — подстраиваемся под конкретную сборку.
     */
    public static Object matchType(Object original, long value) {
        return matchType(original, value, null);
    }

    /** Подбирает тип результата даже когда оригинальный метод вернул null. */
    public static Object matchType(Object original, long value, Class<?> declaredType) {
        if (declaredType == Integer.TYPE || declaredType == Integer.class || original instanceof Integer) {
            return Integer.valueOf((int) Math.max(Integer.MIN_VALUE, Math.min(value, Integer.MAX_VALUE)));
        }
        if (declaredType == Long.TYPE || declaredType == Long.class || original instanceof Long) {
            return Long.valueOf(value);
        }
        if (declaredType == String.class || original instanceof String) {
            return String.valueOf(value);
        }
        if (declaredType == Short.TYPE || declaredType == Short.class || original instanceof Short) {
            return Short.valueOf((short) value);
        }
        if (declaredType == Byte.TYPE || declaredType == Byte.class || original instanceof Byte) {
            return Byte.valueOf((byte) value);
        }
        if (declaredType == Double.TYPE || declaredType == Double.class || original instanceof Double) {
            return Double.valueOf((double) value);
        }
        if (declaredType == Float.TYPE || declaredType == Float.class || original instanceof Float) {
            return Float.valueOf((float) value);
        }
        return Long.valueOf(value);
    }

    public static Class<?> returnType(Member member) {
        return member instanceof Method ? ((Method) member).getReturnType() : null;
    }

    public static String text(Object value) {
        return value == null ? null : String.valueOf(value);
    }
}
