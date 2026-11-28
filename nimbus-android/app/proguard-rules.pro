# The app ships unminified; gomobile's generated bindings (go.Seq) rely on reflection.
-keep class go.** { *; }
-keep class io.nekohasekai.libbox.** { *; }
