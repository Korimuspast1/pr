# Profile Flex — модуль `.ttydyn` для TikTok You

Локальное визуальное оформление профиля в моде **TikTok You**: галочка верификации и
произвольные счётчики. Все изменения происходят только в объектах внутри процесса на
этом устройстве.

> Модуль не отправляет запросы к TikTok, не изменяет аккаунт на сервере и не создаёт
> настоящую верификацию или накрутку. `.ttydyn` из этого проекта рассчитан на
> **собственную/debug-сборку TikTok You**, в которую зашит `keys/dev.pub.pem`.

## Что внутри

| Файл | Назначение |
| --- | --- |
| `src/main/java/com/windukk/mod/Main.java` | обязательная точка входа `start(Application, Context)` |
| `src/main/java/dev/ttydyn/profileflex/ProfileFlex.java` | хуки модели `User` и `Aweme#getStatistics` |
| `src/main/java/dev/ttydyn/profileflex/Config.java` | JSON-конфиг с горячей перезагрузкой |
| `src/main/java/dev/ttydyn/profileflex/Reflect.java` | рефлексия с запасными именами полей/методов |
| `stubs/com/windukk/hook/*.java` | заглушки API, только для javac, в DEX не попадают |
| `build.sh` | `javac` → удаление заглушек → `d8` → signed `.ttydyn` |
| `profile_flex.json` | пример конфига |

Формат контейнера описан в [`../docs/ttydyn-format.md`](../docs/ttydyn-format.md).

## Сборка

Нужны JDK 17+, Android SDK с `android.jar` и `d8`, а также Python 3:

```bash
export ANDROID_HOME="$HOME/Android/Sdk"
python3 tools/ttydyn.py keygen --out keys/dev       # только один раз
./profile-flex/build.sh
python3 tools/ttydyn.py verify release/profile-flex.ttydyn --pub keys/dev.pub.pem
```

Если SDK лежит нестандартно, задайте `ANDROID_JAR` и `D8` явно. Скрипт больше не
создаёт новый ключ молча: это могло привести к тому, что новый пакет не принимался
APK со старым публичным ключом. Для осознанной одноразовой dev-сборки:

```bash
TTY_GENERATE_KEY=1 ./profile-flex/build.sh
```

В этом случае нужно взять **новый** `keys/dev.pub.pem` и зашить его в свою сборку
мода. Ключи из публичного репозитория подходят только для разработки — для выпуска
используйте закрытый ключ вне Git и соответствующий публичный ключ.

Готовый `release/profile-flex.ttydyn` уже содержит `manifest.json`, `classes.dex` и
`signature.der`. Перед установкой проверьте его целиком:

```bash
python3 tools/ttydyn.py inspect release/profile-flex.ttydyn --classes 20
python3 tools/ttydyn.py verify release/profile-flex.ttydyn --pub keys/dev.pub.pem
unzip -t release/profile-flex.ttydyn
```

CI собирает тот же архив на GitHub Actions и прикладывает к артефакту пакет вместе с
публичным ключом. Публичный ключ должен быть вшит в тот APK мода, который будет
импортировать пакет.

## Настройка

Положите `profile_flex.json` в каталог приложения:
`Android/data/<пакет TikTok>/files/` (обычно `com.zhiliaoapp.musically`). Файл
перечитывается во время работы. Для режима `selfOnly` укажите свой `uid` или
`uniqueId` без символа `@`.

| Ключ | Что делает |
| --- | --- |
| `enabled` | общий выключатель |
| `selfOnly` | применять только к профилю, совпадающему с `uid` или `uniqueId` |
| `uid` / `uniqueId` | числовой uid и/или имя пользователя без `@` |
| `badge` | `none` / `personal` / `business` |
| `badgeLabel` | текст для поля `custom_verify` |
| `spoofCounters` | включить подмену счётчиков |
| `followerCount`, `followingCount`, `likeCount`, `videoCount`, `friendsCount` | значения счётчиков |
| `spoofVideoStats` | подменять статистику своих роликов |
| `videoDiggCount`, `videoPlayCount`, `videoCommentCount`, `videoShareCount` | значения для роликов |
| `verbose` | подробный logcat по тегу `ProfileFlex` |

## Установка

1. Соберите пакет или скачайте готовый `release/profile-flex.ttydyn` вместе с
   соответствующим `keys/dev.pub.pem`.
2. Убедитесь, что публичный ключ из файла вшит в вашу debug/собственную сборку
   TikTok You. Это обязательное условие проверки подписи.
3. В моде выберите **Настройки мода → Установить .ttydyn**, укажите файл и подтвердите.
4. Полностью перезапустите приложение.
5. Для диагностики используйте `adb logcat -s ProfileFlex`.

## Почему официальный APK может отказать

Официальная сборка TikTok You проверяет ECDSA-подпись `.ttydyn` нативным кодом по
публичному ключу, зашитому в APK. Приватный ключ официальной сборки не находится в
этом репозитории и математически не может быть восстановлен из публичного ключа.
Поэтому самосборный пакет **невозможно** сделать принимаемым официальным APK только
изменением архива.

Рабочие варианты:

- использовать собственную сборку мода с `keys/dev.pub.pem` или с отключённой
  проверкой подписи;
- попросить авторов мода подписать исходники их ключом;
- не пытаться заменять `signature.der` вручную — это сделает пакет невалидным.

Кроме подписи, `payloadType: full` означает, что пакет заменяет весь dynamic dex
мода. Если нужно сохранить функции региона, скачивания и фильтров, положите
оригинальный `classes.dex` как `profile_flex_base.dex` в каталог external files:
модуль попробует запустить его до установки собственных хуков.

## Лицензия

MIT.
