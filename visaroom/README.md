# visaroom — Android

Нативное Android-приложение (Java, только Android Framework, без сторонних библиотек).
Package: `lav.visaroom.alav`, min Android 8.0 (API 26).

Экраны: вход / регистрация по email и паролю → главный экран с нижней навигацией:
**Chats** (официальный канал visaroom, поиск по чатам/каналам и @юзернеймам),
**Profile** (аватар, юзернейм, био, email и числовой ID 100000-5000000 — выдаётся
при регистрации) и **Settings** (размер текста и фон чатов, время сообщений,
уведомления, двухэтапная проверка вторым паролем и приватность: показ био,
аватара и ID другим пользователям). Профиль человека открывается по тапу на его
имя/аватар в поиске и в чате. Аккаунт-администратор (по email владельца,
проверяется и в правилах) открывает профиль через поиск или
**Settings → Moderation** и может там — в том числе на своём профиле —
сменить ID, убрать юзернейм, очистить био и удалить фото. Второй пароль хранится только как PBKDF2-хэш.
Внутри официального чата есть поиск по сообщениям. Интерфейс на английском,
чёрно-белый, иконки — свои vector-drawable (аналог SVG в Android).

Профили и поиск по юзернеймам заработают в облаке после деплоя обновлённых правил
из `../firebase`. Без них профиль сохраняется на устройстве. Email в поиске не
показывается: публичный каталог юзернеймов содержит только юзернейм и ID.

## Перед первым запуском — Firebase Console

Проект: `lool-4d49c` (конфиг — `app/google-services.json`).

1. Authentication → Sign-in method → включить **Email/Password**. Без этого вход не сработает.
2. Project settings → Android-приложение `lav.visaroom.alav` → Add fingerprint →
   SHA-1 подписи приложения: `76:AA:50:71:53:C4:F8:E3:61:B0:A4:A7:8E:4A:00:EC:C5:AD:70:CE`.
   Без этого Google может отклонять запросы с ошибкой «Requests from this Android
   client application are blocked» (API key ограничен зарегистрированным приложением).
3. Firestore Database → Create database (Production mode).
4. Опубликовать правила из папки `../firebase` в **Firebase Console**:
   - слева **Build → Firestore Database → Rules** — заменить весь текст
     содержимым `../firebase/firestore.rules` и нажать **Publish**;
   - слева **Build → Storage → Rules** — заменить весь текст содержимым
     `../firebase/storage.rules` и нажать **Publish**;
   - **Realtime Database приложение не использует**, создавать её только ради
     правил не нужно. Если такая база уже создана, открыть
     **Build → Realtime Database → Rules**, вставить
     `../firebase/database.rules.json` и нажать **Publish**.

   Вместо ручной вставки можно задеплоить все правила одной командой
   из папки `../firebase`:

   ```bash
   cd ../firebase
   npx firebase-tools deploy --only firestore:rules,storage,database
   ```

Аккаунты создаются в обычном Firebase Auth. Приложение ходит в Firebase по официальному
REST API, поэтому Firebase SDK внутри APK нет — меньше кода, который можно вытащить.

## Сборка

Готовый подписанный APK: `../release/visaroom-1.2.1.apk` (подпись проверена apksigner,
workflow `Verify signed APK`).

R8-сборка идёт в GitHub Actions (`.github/workflows/build-apk.yml`). Если в репозитории
заданы секреты `VISAROOM_KEYSTORE_B64` (keystore в base64) и `VISAROOM_KEYSTORE_PASSWORD`,
CI сам подписывает APK и кладёт его в артефакт `visaroom-apk`. Без секретов CI собирает
unsigned APK в `build-transfer/` — он подписывается ключом владельца вне CI.

Локально (нужен JDK 17, Android SDK 34, Gradle 8.7):

```bash
gradle :app:assembleRelease
```

## Защита APK — что включено

- R8: обфускация имён классов/методов/полей, перенос классов в один пакет, оптимизация.
- Убраны имена исходных файлов и номера строк (`-renamesourcefileattribute`).
- `shrinkResources`, удалены все Log-вызовы, `debuggable=false`.
- Ключ Firebase и адреса API не лежат открытым текстом (XOR, `Vault.java`).
- Сессия (refresh-token) хранится зашифрованной AES-256-GCM, ключ — в Android Keystore.
- `allowBackup=false`, данные исключены из бэкапа/переноса, только HTTPS.

Честно: на 100% «зашифровать» код клиентского приложения нельзя — APK всегда можно
распаковать (jadx/apktool), вопрос только в том, что там увидит человек. С этой сборкой
он увидит переименованные `a/b/c` классы без исходников и строк ключей. Настоящая защита
данных — не скрытность APK, а правила Firebase: коллекции не перечисляются (`list` закрыт),
каждый читает только свой документ, записи в чужие документы и служебные поля запрещены.

Не публикуйте вместе с APK:

- `mapping.txt` — он расшифровывает обфускацию обратно;
- `visaroom-release.p12` и пароль от него — без них нельзя выпустить обновление
  поверх уже установленного приложения.
