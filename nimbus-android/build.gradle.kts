// AGP 9.3 is the line whose maximum supported API level is 37, which is what app/build.gradle.kts
// compiles against, and 9.3.3 is the first patch that does not crash lint on JDK 17 - lintVital
// runs as part of assembleRelease, so that crash would land in the APK job, not the test job.
//
// AGP 9 compiles Kotlin itself ("built-in Kotlin"), so org.jetbrains.kotlin.android must not be
// applied on top of it - the build fails with "no longer required for Kotlin support since AGP
// 9.0". Declaring the Compose compiler plugin here at 2.4.10 is also what pins the Kotlin Gradle
// plugin version AGP resolves (it aligns KGP to any Kotlin plugin >= 2.2.10 declared here).
plugins {
    id("com.android.application") version "9.3.3" apply false
    id("org.jetbrains.kotlin.plugin.compose") version "2.4.10" apply false
}
