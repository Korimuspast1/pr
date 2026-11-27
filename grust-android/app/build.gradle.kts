import java.io.FileInputStream
import java.util.Properties
import org.jetbrains.kotlin.gradle.dsl.JvmTarget

fun versionProp(name: String, fallback: String): String {
    val file = rootProject.file("version.properties")
    if (!file.exists()) return fallback
    val props = Properties()
    FileInputStream(file).use { props.load(it) }
    return props.getProperty(name) ?: fallback
}

plugins {
    id("com.android.application")
    // No org.jetbrains.kotlin.android: AGP 9 has built-in Kotlin (see the root build file).
    id("org.jetbrains.kotlin.plugin.compose")
}

android {
    namespace = "com.zalchik.grust"
    compileSdk = 37

    defaultConfig {
        applicationId = "com.zalchik.grust"
        minSdk = 26
        targetSdk = 37
        versionCode = versionProp("VERSION_CODE", "1").toInt()
        versionName = versionProp("VERSION_NAME", "1.0.0")
    }

    // Sideloaded build: the release APK is signed with the debug key so the CI artifact installs
    // straight away, without anybody having to keep a keystore for a hobby project.
    buildTypes {
        release {
            isMinifyEnabled = false
            signingConfig = signingConfigs.getByName("debug")
        }
        debug {
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
        }
    }

    dependenciesInfo {
        includeInApk = false
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        compose = true
    }

    packaging {
        resources {
            excludes += setOf("META-INF/*.version", "DebugProbesKt.bin", "kotlin-tooling-metadata.json")
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
        // The app leans on material3 components that are still experimental; opting in build-wide
        // keeps the annotations off every composable that uses them.
        freeCompilerArgs.add("-opt-in=androidx.compose.material3.ExperimentalMaterial3Api")
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.19.0")
    implementation("androidx.activity:activity-compose:1.13.0")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.11.0")

    val composeBom = platform("androidx.compose:compose-bom:2026.06.01")
    implementation(composeBom)
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material-icons-extended")
    implementation("androidx.compose.ui:ui-tooling-preview")
    debugImplementation("androidx.compose.ui:ui-tooling")

    // The statistics are plain Kotlin, so they are tested on the JVM without an emulator.
    testImplementation("junit:junit:4.13.2")
    // The real org.json, so the unit tests can exercise EntryCodec: the android.jar stub throws.
    testImplementation("org.json:json:20250107")
}
