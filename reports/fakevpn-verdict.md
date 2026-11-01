# Разбор `fakevpn.zip` (MediaFire, quickkey `mrmg62ns6g296z9`)

**Дата разбора:** 2026-11-01
**Источник:** https://www.mediafire.com/file/mrmg62ns6g296z9/fakevpn.zip/file
**Загружен на MediaFire:** 2026-10-22 10:23 (регион загрузки — Украина)

## Короткий ответ

**Да, это вредонос.** Внутри — не VPN, а пара Android-приложений: **дроппер**
(ставит скрытый payload) и **банковский троян** (доступность, SMS, уведомления,
захват экрана). Оба подписаны одноразовыми самоподписанными сертификатами,
код обфусцирован. Устанавливать нельзя ни на телефоне, ни на эмуляторе с
рабочими аккаунтами.

| Что | Значение |
| --- | --- |
| Файл | `fakevpn.zip`, 853 572 байт |
| SHA-256 архива | `6ceee54407bc963308d59f3f18f117e4e1f251b819e792e4fe9e26253a43e4cc` |
| MD5 архива | `e0c0d377545c40f3b7bdc98f9ef7a421` |
| Шифрование | ZipCrypto (legacy), **пароль `111`** |
| Содержимое | `Button VPN.apk` (467 806 Б) и `Button VPN(1).apk` (427 124 Б) |
| Итог | троян + дроппер, риск **высокий** |

Пароль `111` — не защита, а приём: зашифрованные архивы не проверяются
антивирусами при загрузке, а распаковывает их сам пользователь.

## Образец 1 — `Button VPN.apk` (дроппер)

| Параметр | Значение |
| --- | --- |
| SHA-256 | `b44d0b07913b5512f12af4044af0f043be9f824a67d7457e00a898d9f178a369` |
| MD5 | `f1ecb09c002e05424a74dfb169f92c3b` |
| Package | `com.buttonvpn.service.v1ecdcd69` |
| Версия | 1.0, minSdk 21, compileSdk 34 |
| Файлов / DEX | 13 / 1 (`classes.dex`, 27 780 Б) |
| Сертификат | **CN=Android Debug**, самоподписанный, 2016-10-23 → 2044-03-10 |
| Отпечаток подписанта | `1e08a903aef9c3a721510b64ec764d01d3d094eb954161b62544ea8f187b5953` |

Разрешения: `INTERNET`, `READ/WRITE_EXTERNAL_STORAGE`, **`REQUEST_INSTALL_PACKAGES`**.

Компоненты: 1 activity, 1 service (`kit.node.FlowFactory`), 1 receiver.

В `assets/` лежит зашифрованный груз:

```
assets/api_patch.bin      427 136 Б   ← ровно на 12 байт больше второго APK
assets/util_config.cache    7 536 Б
assets/rx_5d19b1.bin        5 044 Б
assets/data_schema.idx      2 272 Б
assets/lib_patch.dat        2 256 Б
assets/app_icon.webp        1 192 Б
```

Код работает через `PackageInstaller` (установка APK без запроса),
`WebView.addJavascriptInterface` и `Cipher`; строки обфусцированы base64
(`cHJlcGFyZQ==` = `prepare`, `aXRlbV83Ng==` = `item_76`, `cnhfNWQxOWIxLmJpbg==` = `rx_5d19b1.bin`).
Попытка расшифровать `api_patch.bin` как ZipCrypto по словарю rockyou
результата не дала — используется другой алгоритм или ключ, зашитый в код.

**Вывод:** это загрузчик. Показывается как «VPN», при запуске ставит второй APK
из своего же `assets` (или скачивает его) — уже без участия пользователя.

## Образец 2 — `Button VPN(1).apk` (основной троян)

| Параметр | Значение |
| --- | --- |
| SHA-256 | `c3a5895b6d38af17da610c1e66d20888dc10b5c94bebf30d5c7acefa3c92ad85` |
| MD5 | `7833329e41945463ee5f21b9fea4e985` |
| Package | `com.buttonvpn.hjexqm` |
| Файлов / DEX | 82 / 1 (`classes.dex`, 1 387 264 Б) |
| Сертификат | `CN=f2bff280b2c5, OU=Mobile, O=51fce2cd7c, C=US` — самоподписанный, случайные поля |

Классы обфусцированы: `iq.mpm.xs.Dn4`, `iq.mpm.xs.Rfy9`, `iq.mpm.xs.Nf7` и т. п.
(11 сервисов, 8 активностей, 5 ресиверов).

### Что он просит (31 разрешение)

| Разрешение | Зачем это вредоносу |
| --- | --- |
| `BIND_ACCESSIBILITY_SERVICE` + `<meta-data android:name="android.accessibilityservice">` | читать экран, нажимать кнопки за пользователя |
| `READ_SMS`, `RECEIVE_SMS`, `SEND_SMS`, `RECEIVE_MMS`, `WRITE_SMS` | перехват кодов подтверждения банка, скрытые SMS |
| `BIND_NOTIFICATION_LISTENER_SERVICE` | чтение пуш-уведомлений и кодов 2FA |
| `FOREGROUND_SERVICE_MEDIA_PROJECTION` | запись/снятие экрана |
| `QUERY_ALL_PACKAGES` | найти установленные банковские приложения |
| `REQUEST_DELETE_PACKAGES` | удалять/подменять приложения |
| `MANAGE_EXTERNAL_STORAGE` | доступ ко всем файлам |
| `CAMERA`, `RECORD_AUDIO`, `READ_CONTACTS`, `READ_PHONE_STATE` | сбор данных |
| `RECEIVE_BOOT_COMPLETED` | автозапуск после перезагрузки |

### Что видно в коде

`AccessibilityService`, `NotificationListenerService`, `SmsManager`,
`PackageInstaller`, `MediaProjection`, `setComponentEnabledSetting`
(скрытие иконки/включение компонентов), `PathClassLoader` (подгрузка кода),
`addJavascriptInterface` (фишинг в WebView), `Cipher`, `getAccounts`.

Сетевых адресов почти нет: только `api.ipify.org`, `icanhazip.com`, `ident.me`
(проверка собственного IP) и технические `8.8.8.8`, `1.1.1.1`, `10.0.2.2`
(эмулятор). Адрес C2 собирается в рантайме — типично для таких семейств.

**Вывод:** классический банковский троян/стилер с удалённым управлением.
Сценарий: фейковое окно поверх банка → перехват логина и SMS-кода → перевод
денег; плюс слежка (экран, микрофон, камера, контакты).

## Исходный код

Декомпиляция выгружена в [`sample-sources/`](sample-sources/): манифесты,
ресурсы, карта классов и карта вызовов API по каждому APK. Полные java/smali
исходники (30 и 577 классов) лежат в артефакте Actions
`malware-sources-do-not-run` — в репозиторий они намеренно не коммитятся,
это работоспособный троян.

Что видно в исходниках:

**Дроппер** (`Button_VPN/AndroidManifest.xml`):

```xml
<queries>
    <package android:name="com.buttonvpn.hjexqm"/>   <!-- цель — второй APK -->
</queries>
<service android:name="kit.node.FlowFactory"
         android:permission="android.permission.BIND_VPN_SERVICE">
    <intent-filter><action android:name="android.net.VpnService"/></intent-filter>
</service>
<receiver android:name="core.async.DefaultProvider">
    <action android:name="io.beam.agent.v40ab3b.INSTALL_STATUS"/>
</receiver>
```

Он объявляет настоящий `VpnService` (чтобы просить «разрешение VPN» и выглядеть
легитимно), но его работа — поставить `com.buttonvpn.hjexqm` и получить отчёт
`INSTALL_STATUS`. Имена классов — мусор (`kit.node.FlowFactory`,
`core.async.DefaultProvider`, `sdk.store.LeanAdapter`), тело зашифровано
(`Base64->decode` вызывается 102 раза, `MessageDigest` — 16).

**Троян** (`Button_VPN_1_/AndroidManifest.xml`): 11 сервисов, 5 ресиверов,
1 провайдер, 8 активностей; маскируется под системные компоненты Google
(`com.google.android.gms.common.api.GoogleApiActivity`,
`com.system.framework.LoadingActivity`, действия `A11Y_HUB` и `HEARTBEAT`).
Дополнительно: `AccountAuthenticator` и `SyncAdapter` (закрепление в системе),
приём `SMS_DELIVER` / `WAP_PUSH_DELIVER`, `NotificationListenerService`.

Конфиг службы доступности (`res/xml/accessibility_service_config.xml`) — самое
показательное:

```xml
android:canRetrieveWindowContent="true"
android:canPerformGestures="true"        <!-- нажимать за пользователя -->
android:canTakeScreenshot="true"         <!-- снимать экран -->
android:accessibilityFlags="...|flagRequestFilterKeyEvents|flagRetrieveInteractiveWindows"
android:isAccessibilityTool="true"       <!-- притворяется полезным инструментом -->
android:packageNames="@null"             <!-- все приложения сразу -->
```

`flagRequestFilterKeyEvents` — перехват нажатий клавиш, то есть кейлоггер.
`canTakeScreenshot` + `canPerformGestures` — полный цикл «прочитать экран банка →
ввести данные → нажать подтверждение».

В строках кода есть `vless.dex` — динамически подгружаемый модуль VLESS-прокси.
Похоже, VPN-часть сделана настоящей: она нужна, чтобы приложение работало как
обещано, пока фон занимается деньгами.

## Чистая замена: `notmalware`

Собранное с нуля приложение — [`android/notmalware`](../android/notmalware),
готовый APK: [`android/notmalware/out/notmalware.apk`](../android/notmalware/out/notmalware.apk)
(70 КБ, SHA-256 `660105b4b1dfa9dda4b558cb3b782c24724bb1a6d3b019c4fad9f48a2e1c7310`).

| | |
| --- | --- |
| Название | `notmalware` |
| Package | `app.notmalware` |
| Иконка | своя, из `art/icon-1024.png` |
| Разрешения | **0** — в манифесте нет ни одного `<uses-permission>` |
| Компоненты | 1 activity; сервисов, ресиверов, провайдеров нет |
| Сеть | нет `INTERNET`, нет сокетов, нет WebView |
| Код | один `MainActivity.java`, ~200 строк, без сторонних SDK |

Проверено после сборки: `aapt2 dump permissions` — пусто,
`aapt2 dump xmltree` — ни `<service>`, ни `<receiver>`, ни `<provider>`.
Внутри APK 11 файлов: манифест, dex на 1,7 КБ, иконки и подпись.

Это не «вылеченный» Button VPN, а другое приложение: от оригинала в нём нет
ни строки. VPN-функций в нём тоже нет — их просто неоткуда взять чисто.

## Признаки семейства и контекст

- Пара «маленький загрузчик + большой троян» — стандартная двухступенчатая схема.
- Маскировка под VPN в 2026-м — известный тренд: банковские трояны просят
  «разрешение VPN», чтобы отрезать Google Play Protect от проверок
  ([TechRadar, 24.08.2026](https://www.techradar.com/vpn/vpn-privacy-security/this-android-banking-trojan-uses-a-fake-vpn-prompt-to-silence-googles-defenses)).
- На 2026-11-01 хеши **не встречаются** в публичных базах: AlienVault OTX — 0 пульсов,
  MalwareBazaar — «No records found». Это значит, что образец свежий/приватный,
  а не что он чистый.

## Что делать

1. **Не устанавливать.** Ни на телефон, ни на эмулятор с реальными аккаунтами.
2. Если уже установлено:
   - включите авиарежим, удалите оба приложения (`com.buttonvpn.*`);
   - **Настройки → Специальные возможности** — отключите неизвестные службы;
   - **Настройки → Уведомления → Доступ к уведомлениям** — отключите;
   - **Безопасность → Администраторы устройства** — снимите галочки;
   - смените пароли банка/почты/Telegram **с другого устройства**;
   - проверьте историю операций и включите 2FA заново;
   - при сомнениях — сброс телефона к заводским настройкам.
3. Проверьте хеши на [VirusTotal](https://www.virustotal.com):
   `b44d0b07913b5512f12af4044af0f043be9f824a67d7457e00a898d9f178a369`,
   `c3a5895b6d38af17da610c1e66d20888dc10b5c94bebf30d5c7acefa3c92ad85`,
   `6ceee54407bc963308d59f3f18f117e4e1f251b819e792e4fe9e26253a43e4cc`.
4. Не запускайте APK «на посмотреть» — загрузчик ставит payload без лишних
   подтверждений, а троян получает доступ к экрану и SMS.

## Как это проверялось

Разбор полностью статический: файл скачан, распакован с паролем `111`,
APK разобраны (манифест через androguard, DEX-строки, сертификаты, ресурсы).
Ничего не запускалось и не устанавливалось. Сырой лог — в
[`fakevpn-static-triage.txt`](fakevpn-static-triage.txt).

> Отчёт подготовлен автоматическим триажем; он описывает возможности кода,
> а не результаты его запуска. Для юридически значимых выводов нужен
> динамический анализ в изолированной среде.

## IOCs

```
sha256 6ceee54407bc963308d59f3f18f117e4e1f251b819e792e4fe9e26253a43e4cc  fakevpn.zip
sha256 b44d0b07913b5512f12af4044af0f043be9f824a67d7457e00a898d9f178a369  Button VPN.apk (dropper)
sha256 c3a5895b6d38af17da610c1e66d20888dc10b5c94bebf30d5c7acefa3c92ad85  Button VPN(1).apk (trojan)

package  com.buttonvpn.service.v1ecdcd69   (dropper, service kit.node.FlowFactory)
package  com.buttonvpn.hjexqm              (trojan, classes iq.mpm.xs.*)

cert     CN=Android Debug                    sha256 1e08a903aef9c3a721510b64ec764d01d3d094eb954161b62544ea8f187b5953
cert     CN=f2bff280b2c5, OU=Mobile, O=51fce2cd7c, C=US

asset    assets/api_patch.bin   427136 B  (зашифрованный payload)
asset    assets/util_config.cache, assets/rx_5d19b1.bin, assets/data_schema.idx, assets/lib_patch.dat

domain   api.ipify.org, icanhazip.com, ident.me   (проверка IP, не C2)
zip-pw   111
```
