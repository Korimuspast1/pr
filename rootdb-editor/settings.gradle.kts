pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
        // libsu (root) раздаётся через JitPack
        maven { url = uri("https://jitpack.io") }
    }
}
rootProject.name = "RootDB Pro"
include(":app")
