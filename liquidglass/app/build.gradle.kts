plugins {
    id("com.android.application")
}

android {
    namespace = "dev.arena.liquidglasslab"
    compileSdk = 35

    defaultConfig {
        applicationId = "dev.arena.liquidglasslab"
        // RuntimeShader (AGSL) is API 33; below that the app falls back to a frosted
        // surface with a rim, but the lab is only interesting with real lensing.
        minSdk = 33
        targetSdk = 35
        versionCode = 1
        versionName = "1.0"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            // signed with a throwaway key so `assembleRelease` produces an installable APK
            signingConfig = signingConfigs.getByName("debug")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        buildConfig = false
        aidl = false
        renderScript = false
        shaders = false
    }

    lint {
        abortOnError = false
    }
}
