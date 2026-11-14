plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

val releaseStoreFileValue = providers.gradleProperty("VERSIONSHELF_STORE_FILE").orNull
val releaseStorePassword = providers.gradleProperty("VERSIONSHELF_STORE_PASSWORD").orNull
val releaseKeyAlias = providers.gradleProperty("VERSIONSHELF_KEY_ALIAS").orNull
val releaseKeyPassword = providers.gradleProperty("VERSIONSHELF_KEY_PASSWORD").orNull

android {
    namespace = "app.versionshelf"
    compileSdk = 35

    defaultConfig {
        applicationId = "app.versionshelf"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = "1.0.0"
    }

    signingConfigs {
        create("release") {
            if (releaseStoreFileValue != null) {
                storeFile = file(releaseStoreFileValue)
                storePassword = releaseStorePassword
                keyAlias = releaseKeyAlias
                keyPassword = releaseKeyPassword
            }
        }
    }

    buildTypes {
        debug {
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
        }
        release {
            isMinifyEnabled = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
            // An unsigned release can be assembled for inspection. A user-owned key is attached only
            // when all VERSIONSHELF_* signing properties are passed to Gradle.
            if (releaseStoreFileValue != null) {
                signingConfig = signingConfigs.getByName("release")
            }
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }
}
