# Сборка APK

Приложение — это тонкая оболочка (`java/com/kingdomcrash/game/MainActivity.java`) с полноэкранным
WebView, который показывает игру из папки `www/`. Gradle и Android Studio не нужны.

## Что нужно

- JDK 11 или новее (`javac`, `keytool`);
- Android build-tools 34 и платформа android-34. Скачать без Android Studio:

```sh
mkdir -p ~/.cache/android-sdk && cd ~/.cache/android-sdk
curl -sO https://dl.google.com/android/repository/build-tools_r34-linux.zip
curl -sO https://dl.google.com/android/repository/platform-34-ext7_r03.zip
unzip -q -o build-tools_r34-linux.zip -d bt && unzip -q -o platform-34-ext7_r03.zip -d pf
```

## Сборка

```sh
sh android/build.sh
```

Готовый файл: `release/KingdomCrash-<версия>.apk`. Версия берётся из `GAME_VERSION` в `www/js/data.js`.

## Ключ подписи

При первой сборке создаётся ключ `android/keystore/kingdomcrash.jks` (пароль — в `password.txt` рядом).

- Обновление ставится поверх старой версии **только если подписано тем же ключом**. Потеряете ключ —
  игрокам придётся удалять игру и ставить заново, сохранения пропадут.
- Ключ **нельзя выкладывать в открытый доступ**: папка `android/keystore/` внесена в `.gitignore`.
  Храните её копию в надёжном месте.

## Что делает оболочка

- Разрешений нет: у приложения нет доступа к интернету, файлам, камере и т. д.
- Страница не может открыть ничего, кроме файлов игры: переходы и запросы наружу отклоняются.
- Кнопка «Назад» закрывает открытое окно игры, в игре открывает меню, в главном меню — выходит.
- При сворачивании партия сохраняется, звук останавливается.
- Если системный WebView не запускается, игрок видит объяснение, а не пустой экран.
- Экран не гаснет во время игры; ориентация — портретная.

Минимальная версия Android — 6.0 (API 23).
