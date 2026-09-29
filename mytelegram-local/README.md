# MyTelegram Local Lab

Это отдельный приватный тестовый комплект на базе:

- серверного API [MyTelegram](https://github.com/loyldg/mytelegram);
- Android-клиента [mytelegram-android](https://github.com/loyldg/mytelegram-android);
- локального тестового входа без SMS и без Telegram Bot API.

Клиент сохраняет интерфейс Telegram-подобного Android-клиента, но подключается к
адресу из `ConnectionsManager.cpp`, а не к production DC. Серверные данные лежат в
локальных Docker volumes.

> Это не официальный Telegram и не подключение к аккаунтам Telegram. Номера и код
> `22222` относятся только к локальному тестовому серверу. `bot.txt` намеренно не
> запускается: он обращается к реальному Bot API и не нужен для all-local режима.

## Быстрый запуск backend на Linux/ПК

Нужны Docker Engine и Docker Compose v2:

```bash
cd mytelegram-local
./start-server.sh 192.168.1.25
```

Замените `192.168.1.25` на LAN-IP компьютера, который будет доступен с телефона.
Скрипт создаст `docker/.env`, включит тестовые пользователи и фиксированный код:

```text
тестовый номер: 12222222222 (синтетический, не настоящий номер)
код входа: 22222
порты: 20443, 20543, 20643, 20644, 30443, 30444
```

При первом запуске Docker скачает образы MongoDB, Redis, RabbitMQ, MinIO и MyTelegram.
Данные сохраняются в `mytelegram-local/data/` и не отправляются в Telegram.

Проверка и остановка:

```bash
./server-status.sh
./stop-server.sh
```

Обычный Android/Termux не предоставляет Docker daemon. Поэтому для первого запуска
используйте Linux ПК или VPS. Телефон подключается к серверу по LAN-IP. После
проверки можно отдельно собирать ARM64-вариант для устройства, если среда поддержит
Docker и нужные образы.

## Сборка Android APK

Нужны JDK 17, Android SDK 35, NDK `21.4.7075529` и Gradle-зависимости. На Linux/ПК:

```bash
cd mytelegram-local
./build-client.sh 192.168.1.25
```

Скрипт:

1. скачает `mytelegram-android` в игнорируемую папку `source/`;
2. заменит только тестовый endpoint в
   `TMessagesProj/jni/tgnet/ConnectionsManager.cpp`;
3. соберёт `TMessagesProj_App:assembleAfatRelease`;
4. положит APK в `mytelegram-local/build/apk/`.

В GitHub Actions доступна ручная сборка:

```text
Actions → build-ttydyn → Run workflow → server_ip
```

После завершения APK будет в артефактах workflow. Для телефона указывайте LAN-IP
компьютера, а не `127.0.0.1`. `127.0.0.1` подходит только если backend реально
запущен на том же устройстве и порт доступен приложению.

## Готовый пример APK

В комплекте сохранён проверенный ARM64 APK:

```text
mytelegram-local/apk/MyTelegram-local-arm64-v8a-36586737127.apk
SHA-256: dade130029d3dc1819968108c917220e50f0f7c83934dc2e0fccb55619ae98bb
```

Этот пример собран с endpoint `192.168.1.100`, поэтому он подходит только если
backend действительно доступен по этому адресу. Для другого компьютера сначала
запустите backend с его LAN-IP, затем пересоберите APK командой выше с тем же
`SERVER_IP`; это не требует ручной правки исходников. Копия также опубликована в
[release asset](https://github.com/Korimuspast1/pr/releases/tag/mytelegram-apk-36586737127).

## Почему не используется bot.txt

`bot.txt` содержит `aiogram` и `Bot(token=...)`, поэтому он требует внешний Telegram
Bot API. Это противоречит требованию «всё локально». Кроме того, случайная строка
в `numbers.json` не является настоящим номером телефона и не получает SMS.

Для локального теста сервер использует `App__FixedVerifyCode=22222`. Не вставляйте
в этот комплект реальные номера, SMS-коды, bot tokens или production-сессии.

## Ограничения

Это стартовый local-lab комплект: backend и Android-клиент подключаются друг к
другу, а тестовые данные хранятся локально. Настоящие Telegram Premium, Stars,
платежи и production-аккаунты в нём не создаются. Если серверная версия содержит
тестовые флаги Premium/Stars, они являются только локальными полями sandbox.

Администрирование через базу данных и добавление отдельной локальной панели можно
сделать следующим этапом после успешного запуска чистого сервера. Сначала нужно
проверить, что клиент и API layer совпадают.
