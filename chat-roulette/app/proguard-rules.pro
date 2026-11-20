# R8 full mode уже включён по умолчанию (AGP 8).
# Агрессивная обфускация кода приложения: имена классов/методов/полей переименовываются,
# неиспользуемый код и ресурсы вырезаются, строки логов убираются правилами ниже.

-allowaccessmodification
-optimizationpasses 5

# WebRTC нативная библиотека вызывается через JNI — имена должны остаться.
-keep class org.webrtc.** { *; }
-dontwarn org.webrtc.**

# Kotlin metadata нужна рефлексии kotlinx.
-keepclassmembers class * {
    @kotlin.Metadata *;
}

# Убрать весь лог-код из релиза.
-assumenosideeffects class android.util.Log {
    public static *** d(...);
    public static *** v(...);
    public static *** i(...);
    public static *** w(...);
    public static *** e(...);
}
