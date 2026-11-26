// Toolchain versions mirror the ones the official sing-box CI builds libbox and the
// reference Android client with (JDK 17, NDK r28), so the libbox.aar our workflow
// compiles from SagerNet/sing-box v1.15.0-alpha.12 drops straight in.
plugins {
    id("com.android.application") version "9.3.1" apply false
    id("org.jetbrains.kotlin.android") version "2.4.10" apply false
    id("org.jetbrains.kotlin.plugin.compose") version "2.4.10" apply false
}
