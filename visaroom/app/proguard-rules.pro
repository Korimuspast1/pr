# visaroom release hardening (R8 full mode is the default in AGP 8).
# Goal: after unpacking the APK a decompiler shows renamed a/b/c classes,
# no source file names, no line numbers, no log calls and no debug data.

-optimizationpasses 5
-allowaccessmodification
-overloadaggressively

# Move all app classes into one flat package and rename them.
-repackageclasses ''
-flattenpackagehierarchy ''

# Remove source-file attribution (class names in stack traces / jadx headers).
-renamesourcefileattribute ''
-keepattributes Exceptions,InnerClasses,Signature,Deprecated,*Annotation*,EnclosingMethod

# Strip every android.util.Log call from the release build.
-assumenosideeffects class android.util.Log {
    public static int v(...);
    public static int d(...);
    public static int i(...);
    public static int w(...);
    public static int e(...);
    public static int wtf(...);
    public static boolean isLoggable(...);
}

# The Android system starts components by name from the manifest, so the
# activity entry points themselves must stay reachable. Everything else
# (helpers, network client, token storage) is renamed and repackaged.
-keep public class * extends android.app.Activity {
    public <init>(android.content.Context);
    public <init>();
}

-keep public class * extends android.app.Service {
    public <init>();
}

-keepclassmembers enum * {
    public static **[] values();
    public static ** valueOf(java.lang.String);
}

-dontwarn **
-ignorewarnings
