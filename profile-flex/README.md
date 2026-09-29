# Profile Flex — модуль `.ttydyn` для TikTok You

Локальное визуальное оформление профиля в моде **TikTok You**: галочка верификации
и произвольные счётчики (подписчики, подписки, лайки, количество видео), при желании —
статистика под своими роликами.

> **Только локально.** Модуль меняет объекты внутри процесса приложения на вашем устройстве.
> Ни одного запроса к серверам TikTok не отправляется, на аккаунте ничего не меняется,
> другие пользователи ничего не видят. Это не настоящая верификация и не накрутка;
> скриншот такого профиля нельзя использовать как доказательство для кого-либо.

## Что внутри

| Файл | Назначение |
| --- | --- |
| `src/main/java/com/windukk/mod/Main.java` | точка входа `start(Application, Context)` — её вызывает стабильная часть мода |
| `src/main/java/dev/ttydyn/profileflex/ProfileFlex.java` | хуки `com.ss.android.ugc.aweme.profile.model.User` и `Aweme#getStatistics` |
| `src/main/java/dev/ttydyn/profileflex/Config.java` | чтение и горячая перезагрузка настроек из JSON |
| `src/main/java/dev/ttydyn/profileflex/Reflect.java` | рефлексия с запасными именами полей/методов |
| `stubs/com/windukk/hook/*.java` | заглушки API мода, только для компиляции (в dex не попадают) |
| `build.sh` | javac → d8 → `tools/ttydyn.py pack` |
| `profile_flex.json` | пример конфига |

Формат контейнера и API мода разобраны в [`../docs/ttydyn-format.md`](../docs/ttydyn-format.md).

## Сборка

```bash
export ANDROID_HOME=~/Android/Sdk      # нужен android.jar и d8 из build-tools
./profile-flex/build.sh
# → release/profile-flex.ttydyn (+ ключ keys/dev.key.pem при первом запуске)
```

Отдельно упаковать готовый dex или посмотреть чужой модуль:

```bash
python3 tools/ttydyn.py inspect  любой-модуль.ttydyn --classes 20
python3 tools/ttydyn.py keygen   --out keys/dev
python3 tools/ttydyn.py pack     --dex build/classes.dex --out release/profile-flex.ttydyn --key keys/dev.key.pem
python3 tools/ttydyn.py verify   release/profile-flex.ttydyn --pub keys/dev.pub.pem
```

## Настройка

Положите `profile_flex.json` в `Android/data/<пакет TikTok>/files/` (обычно
`com.zhiliaoapp.musically`). Файл перечитывается на лету, перезапуск не нужен.

| Ключ | Что делает |
| --- | --- |
| `enabled` | общий выключатель |
| `selfOnly` | применять только к своему профилю (по `uid` или `uniqueId`) |
| `uid` / `uniqueId` | ваш числовой uid и/или `@username` без собаки |
| `badge` | `none` / `personal` / `business` |
| `badgeLabel` | текст в поле `custom_verify` (по умолчанию `verified`) |
| `spoofCounters` | включить подмену счётчиков профиля |
| `followerCount`, `followingCount`, `likeCount`, `videoCount`, `friendsCount` | значения счётчиков |
| `spoofVideoStats` | подменять лайки/просмотры/комментарии/репосты под своими роликами |
| `videoDiggCount`, `videoPlayCount`, `videoCommentCount`, `videoShareCount` | значения для роликов |
| `verbose` | подробный лог в logcat по тегу `ProfileFlex` |

## Установка

1. Соберите `release/profile-flex.ttydyn`.
2. В моде: пункт **«Установить .ttydyn»** → выберите файл.
3. Проверьте logcat: `adb logcat -s ProfileFlex`.

## Ограничения, которые нужно понимать

1. **Подпись.** Официальная сборка TikTok You проверяет ECDSA-подпись модуля нативным кодом
   (`verifySignatureNative`) по зашитому публичному ключу. Приватного ключа авторов нет,
   поэтому самосборный модуль примет только сборка мода с отключённой проверкой или
   с заменённым на ваш публичным ключом.
2. **`payloadType: full`.** Модуль заменяет весь динамический код мода: после установки
   останется «чистый» TikTok + этот модуль, без региона, скачивания, фильтров и прочего.
   Сохраните оригинальный `.ttydyn`, чтобы вернуться назад.
3. **Имена методов TikTok** (`getFollowerCount`, `getTotalFavorited`, `getCustomVerify` …)
   зависят от версии приложения и обфускации. Если хук не встал, в логе будет
   `method not found: User.getXxx` — поправьте список в `ProfileFlex.install`.
4. Сервер всё равно отдаёт настоящие цифры: при обновлении экрана значения перерисуются
   уже подменёнными, но в вебе и у других людей профиль остаётся прежним.

## Лицензия

MIT (см. `../custom-profile/LICENSE`).
