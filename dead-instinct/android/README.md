# Сборка APK

Приложение — это тонкая оболочка (`java/com/deadinstinct/game/MainActivity.java`) с полноэкранным
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

Готовый файл: `release/DeadInstinct-<версия>.apk`. Версия берётся из `GAME_VERSION` в `www/js/data.js`.

Автоматически то же самое делает GitHub Actions (`.github/workflows/dead-instinct-release.yml`):
на каждый пуш в `main`/`arena/*` с изменениями в `dead-instinct/**` собирается APK, а с тегом
`dead-instinct-v*` — публикуется в GitHub Releases.

## Ключ подписи

Перед сборкой ключ создаёт `android/keygen.sh` (openssl, без JDK): `android/keystore/deadinstinct.p12`,
пароль — в `password.txt` рядом. В CI ключ не хранится, а генерируется заново при каждой сборке.

- Обновление ставится поверх старой версии **только если подписано тем же ключом**.
- Ключ **нельзя выкладывать в открытый доступ**: папка `android/keystore/` внесена в `.gitignore`.

## Что делает оболочка

- Разрешений нет: у приложения нет доступа к интернету, файлам, камере и т. д.
- Страница не может открыть ничего, кроме файлов игры: переходы и запросы наружу отклоняются.
- Кнопка «Назад» закрывает открытое окно игры, в игре открывает меню, в главном меню — выходит.
- При сворачивании партия сохраняется, звук останавливается.
- Если системный WebView не запускается, игрок видит объяснение, а не пустой экран.
- Экран не гаснет во время игры; ориентация — портретная.

Минимальная версия Android — 6.0 (API 23).
