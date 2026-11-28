// AGP 9.3 compiles Kotlin itself ("built-in Kotlin"), so org.jetbrains.kotlin.android must not be
// applied on top of it. The Compose compiler plugin also pins the Kotlin Gradle plugin version.
plugins {
    id("com.android.application") version "9.3.3" apply false
    id("org.jetbrains.kotlin.plugin.compose") version "2.4.10" apply false
}
