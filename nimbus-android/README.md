# Nimbus для Android

[English](README.en.md) · **Русский**

Порт настольного [Nimbus](https://github.com/Zalchik/nimbus) на телефон: тот же клиент подписок,
ссылок и балансировщиков, но на ядре [sing-box](https://github.com/SagerNet/sing-box), которое
работает внутри приложения через VPN-сервис Android.

Подписка, настройки и профили — те же файлы `settings.json` и `profiles.json`, что и на компьютере:
папку можно перенести с настольной версии, и она заработает как есть.

## Быстрый старт

1. Соберите APK (нужны JDK 17 и Android SDK; Go и NDK не нужны — ядро берётся готовым):

   ```sh
   cd nimbus-android
   . ./libbox.env.sh                      # скачает app/libs/libbox.aar (sing-box 1.14.4)
   ./gradlew :app:assembleRelease
   ```

2. APK лежат в `app/build/outputs/apk/release/`: по одному на ABI (`arm64-v8a`, `armeabi-v7a`,
   `x86_64`) и `universal`. Релизная сборка подписана отладочным ключом, поэтому ставится сразу;
   для своей подписи добавьте `signingConfig` в `app/build.gradle.kts`.
3. Установите, разрешите VPN, добавьте подписку или ссылку и нажмите кнопку подключения.

Готовые артефакты собирает GitHub Actions: `.github/workflows/nimbus-android.yml` (тесты →
APK по ABI → `SHA256SUMS.txt`).

## Что умеет

- **Подписки**: трафик, срок, объявления провайдера, HWID, свой User-Agent, автообновление по
  интервалу панели или по настройке. Полные Xray JSON-конфиги переводятся в sing-box и работают.
- **Ссылки**: VLESS (Reality, Vision), VMess, Trojan, Shadowsocks (SIP002, 2022, legacy),
  Hysteria2 (включая hop-порты и obfs), SOCKS5, WireGuard.
- **Зашифрованные ссылки Happ** (`happ://crypt…/…`, `crypt://…`): расшифровываются на телефоне —
  RSA-ключи из таблицы Happ и ChaCha20-Poly1305 (RFC 8439) реализованы в `engine/Happ.kt`, без
  сторонних библиотек.
- **Deep links** других клиентов: `nimbus://`, `happ://`, `v2rayn://`, `v2rayng://`, `clash://`,
  `clashmeta://`, `sing-box://`, а также «Поделиться» текстом и открытие файла конфига.
- **Балансировщики**: набор серверов из любых подписок и групп, режим «держать узел» (переключение
  только при заметном выигрыше) или «всегда лучший», интервал от 10 с, уведомление о смене узла.
  Внутри sing-box это группа `urltest`, поэтому переключение происходит без переподключения.
- **Маршрутизация**: правила провайдера, «российские сайты напрямую», блокировка рекламы, обход
  локальных адресов, свои профили по приложениям и сайтам, общий фильтр приложений.
- **DNS**: удалённый и прямой серверы (DoH/DoT/QUIC/UDP/TCP), fakeip, определение домена по
  трафику (TLS/SNI, HTTP Host, QUIC), стратегии домена.
- **Диагностика**: реальная задержка серверов, ваш IP со страной, журнал sing-box, текущий конфиг,
  отправка журнала файлом.
- **Поделиться**: ссылкой, QR-кодом, JSON-конфигом, всей подпиской.
- **Прочее**: плитка быстрых настроек, автозапуск после перезагрузки, автоподключение, тема
  (тёмная/светлая/системная), русский и английский, обновление приложения из релизов GitHub с
  проверкой SHA-256.

## Чем телефон отличается от компьютера

| Настольная версия | Android |
| --- | --- |
| Xray-core и sing-box, два процесса | только sing-box, внутри процесса (libbox) |
| Режимы «прокси» и «TUN» | TUN — основной; «прокси» оставлен для совместимости настроек |
| Пресеты по программам (`chrome.exe`) | профили по пакетам приложений (`package_name`) |
| Kill switch своими силами | always-on VPN и «блокировать трафик без VPN» в настройках Android |
| `geoip.dat` / `geosite.dat` | загружаемые rule-set'ы sing-box (`*.srs`) |
| WireGuard-аутбаунд | WireGuard как `endpoint` (аутбаунд удалён из sing-box) |
| Транспорт XHTTP / KCP | не поддерживается: sing-box их не умеет, Nimbus сообщает об этом до подключения |
| Обновление заменой файлов | APK из релиза, установка через систему |

Протоколы и транспорты, которых в мобильном ядре нет, не отбрасываются молча: ссылка помечается, и
приложение объясняет, почему сервер не запустится (`engine/Links.kt`, `ParsedLink.unsupported`).

## Устройство кода

```
app/src/main/java/com/zalchik/nimbus/
├── util/       JSON, base64, разбор URL — переносимые, без android.jar
├── i18n/       русский исходник + таблица английских текстов (Compose-состояние языка)
├── data/       settings.json, profiles.json, пути, ключи Happ
├── engine/     ссылки, Xray-конфиги, сборка конфига sing-box, балансировщики, подписки,
│               deep links, обмен, задержка, IP, QR, обновления
├── bg/         libbox: VPN-сервис, командный сервер/клиент, уведомление, плитка, автозапуск
└── ui/         Compose: экраны, состояние приложения, тема, виджеты
```

`engine/` и `data/` не знают про Android — поэтому их можно тестировать на JVM и поэтому они
совпадают с настольными модулями по ключам и поведению. Всё платформенное живёт в `bg/` и `ui/`.

Точка входа туннеля: `bg/NimbusVpnService` (Android VPN) → `bg/BoxService` (запуск ядра,
`CommandServerHandler`) → `bg/ServiceCommandClient` (состояние, группы, задержки, журнал) →
`bg/ServiceController` (Compose-состояние, которым пользуются экраны).

## Тесты

```sh
cd nimbus-android
./gradlew :app:testReleaseUnitTest
```

Тесты повторяют `tests/test_core_logic.py` настольной версии на тех же фикстурах: вектор
ChaCha20-Poly1305 из RFC 8439, настоящие зашифрованные ссылки Happ, разбор всех видов ссылок,
сборка конфигов (TUN, DNS, профили, балансировщики, конфиги провайдера), перевод конфига Xray в
sing-box, форматы подписок, deep links, QR, обновления, совместимость `settings.json` и
`profiles.json`, полнота английских переводов.

Отдельный тест (`GeneratedConfigDump`) раскладывает все собранные конфиги по
`app/build/generated-configs/` — CI сохраняет их артефактом, чтобы изменения схемы sing-box были
видны как diff, а не как догадка.

## Обновление версии ядра

`version.properties` фиксирует версию sing-box и адрес готового `libbox.aar` (их публикует
[singbox-android/libbox](https://github.com/singbox-android/libbox/releases) для каждого релиза).
Чтобы перейти на другую версию:

1. поменяйте `SINGBOX_VERSION` и `LIBBOX_AAR_URL` в `version.properties`;
2. удалите `app/libs/libbox.aar` и снова выполните `. ./libbox.env.sh`;
3. сверьте API: gomobile молча пропускает методы, которые не умеет связать, поэтому перед вызовом
   нового метода посмотрите сгенерированные Java-классы внутри `libbox-sources.jar` в AAR.

Файлы `data/HappKeys.kt` и `i18n/LocaleEn.kt` порождены из настольного репозитория:
`python3 tools/generate_kotlin_data.py /path/to/nimbus` (таблица ключей Happ и английские тексты,
включая добавленные портом). Иконки — `python3 tools/make_icons.py`.

## Лицензии

- sing-box: GPL-3.0 (текст в `licenses/LICENSE-sing-box`). На телефоне ядро связано с приложением
  через libbox, а не запущено отдельным процессом, как на компьютере, поэтому собранный APK
  распространяется на условиях GPL-3.0.
- Шрифты Montserrat и IBM Plex Mono: SIL Open Font License (`licenses/OFL-*.txt`).
- OkHttp и ZXing: Apache-2.0.
- Страна по IP: [IP Geolocation by DB-IP](https://db-ip.com) (CC BY 4.0).
