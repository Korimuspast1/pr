// Root build file. The project uses no third-party dependencies at all: the glass
// material is plain android.graphics (RenderNode + RuntimeShader) and the UI is the
// framework's own View system, so `./gradlew assembleDebug` works fully offline once
// the Android Gradle plugin itself is in the Gradle cache.
plugins {
    id("com.android.application") version "8.7.3" apply false
}
