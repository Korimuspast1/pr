"""Generate the Kotlin data files that must match the desktop Nimbus exactly.

    python3 tools/generate_kotlin_data.py /path/to/nimbus

Reads, from the desktop repository (Zalchik/nimbus):
  * nimbus/happ_keys.py  -> app/src/main/java/com/zalchik/nimbus/data/HappKeys.kt
  * nimbus/locale_en.py  -> app/src/main/java/com/zalchik/nimbus/i18n/LocaleEn.kt
    plus EXTRA_TEXTS/EXTRA_PLURALS below: strings the mobile port added or reworded.

The outputs are committed; re-run this after pulling a newer desktop Nimbus
(a newer Happ key table or new English texts) and commit the result.
"""
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE.parent / "app" / "src" / "main" / "java" / "com" / "zalchik" / "nimbus"

# Mobile-only strings (or desktop strings the phone UI reuses with new context).
# Keys are the Russian source texts passed to tr(); values are the English ones.
EXTRA_TEXTS = {
    # -- mobile specific ------------------------------------------------------------------
    "Nimbus для Android": "Nimbus for Android",
    "Подключить VPN": "Grant the VPN permission",
    "Для подключения Android спрашивает разрешение на VPN один раз":
        "Android asks for the VPN permission once",
    "Разрешение VPN не получено": "The VPN permission wasn't granted",
    "Уведомления": "Notifications",
    "Разрешите уведомления, чтобы видеть состояние подключения":
        "Allow notifications to see the connection status",
    "Автозапуск при включении телефона": "Start when the phone boots",
    "Подключение": "Connection",
    "Выберите приложения": "Pick applications",
    "Поиск приложений…": "Searching apps…",
    "Сканировать QR-код": "Scan a QR code",
    "Камера": "Camera",
    "Сохранить PNG": "Save a PNG",
    "QR-код сохранён": "The QR code is saved",
    "Установить": "Install",
    "Скачивание обновления…": "Downloading the update…",
    "Разрешите установку приложений из неизвестных источников, чтобы обновить Nimbus":
        "Allow installs from unknown sources to update Nimbus",
    "Вставка из буфера": "Clipboard",
    "В буфере обмена похоже ссылка — добавить?": "The clipboard looks like a link — add it?",
    "Быстрая плитка": "Quick settings tile",
    "Через VPN идут только выбранные приложения": "Only the selected apps go through the VPN",
    "Выбранные приложения идут мимо VPN": "The selected apps bypass the VPN",
    "Весь трафик телефона идёт через VPN — как TUN на компьютере":
        "All of the phone's traffic goes through the VPN, like TUN on a computer",
    "Задержка измеряется до сервера провайдера; в подключении балансировщик показывает реальные задержки ядер":
        "Latency is measured to the provider's server; while connected, the balancer shows the core's real delays",
    "Список приложений телефона заменяет список программ компьютера":
        "The phone's app list replaces the computer's program list",
    "XHTTP-серверы не поддерживаются мобильным ядром sing-box":
        "XHTTP servers aren't supported by the mobile sing-box core",
    "KCP не поддерживается мобильным ядром sing-box":
        "KCP isn't supported by the mobile sing-box core",
    "Протокол {proto} не поддерживается на телефоне": "Protocol {proto} isn't supported on the phone",
    "Ядро: sing-box {version} (libbox)": "Core: sing-box {version} (libbox)",
    "Экспортировать данные": "Export data",
    "Файлы настроек и подписок отправлены": "The settings and profile files are shared",
    "Импортировать данные с компьютера": "Import data from a computer",
    "Скопируйте profiles.json из папки данных Nimbus на компьютере и вставьте сюда":
        "Copy profiles.json from Nimbus' data folder on the computer and paste it here",
    "Включён постоянно (kill switch)": "Always-on (kill switch)",
    "Для kill switch включите «Постоянный VPN» в настройках Android и заблокируйте сети без VPN":
        "For a kill switch turn on Always-on VPN in Android's settings and block networks without VPN",
    "Открыть настройки VPN Android": "Open Android's VPN settings",
    "Режим": "Mode",
    "Стабильный — сервер меняется, только если новый быстрее хотя бы на 50 мс":
        "Stable — the server changes only if the new one is at least 50 ms faster",
    "Самый быстрый — всегда лучший сервер": "Fastest — always the best server",
    "Интервал проверки, с": "Check interval, s",
    "Тема": "Theme",
    "Тёмная": "Dark",
    "Светлая": "Light",
    "Как в системе": "Follow the system",
    "Связь": "Link",
    "Трафик": "Traffic",
    "Подключено": "Connected",
    "Отключено": "Disconnected",
    "Подключение…": "Connecting…",
    "Отключение…": "Disconnecting…",
    "Серверы": "Servers",
    "Балансировщики": "Balancers",
    "Мои серверы": "My servers",
    "Настройки": "Settings",
    "Логи": "Logs",
    "О программе": "About",
    "Проверить обновления": "Check for updates",
    "Обновление недоступно": "No update available",
    "У вас последняя версия": "You're on the latest version",
    "Что нового": "What's new",
    "Скачать": "Download",
    "Пропустить версию": "Skip this version",
    "Позже": "Later",
    "Пинг": "Ping",
    "Проверить задержку": "Check latency",
    "Поделиться": "Share",
    "Ссылка": "Link",
    "QR-код": "QR code",
    "Xray JSON": "Xray JSON",
    "Все ссылки списком": "All links as a list",
    "Скопировано": "Copied",
    "Добавить": "Add",
    "Подписка": "Subscription",
    "Ссылки или JSON-конфиги": "Links or JSON configs",
    "Группа": "Group",
    "Новая группа": "New group",
    "Балансировщик": "Balancer",
    "Изменить": "Edit",
    "Переименовать": "Rename",
    "Удалить": "Remove",
    "Обновить": "Update",
    "Обновить подписки": "Update subscriptions",
    "User-Agent": "User-Agent",
    "Отправлять HWID": "Send the HWID",
    "Подключиться": "Connect",
    "Отключиться": "Disconnect",
    "Сортировка": "Sort",
    "По источнику": "By source",
    "По задержке": "By latency",
    "По имени": "By name",
    "Поиск": "Search",
    "Сервер": "Server",
    "Готово": "Done",
    "Отмена": "Cancel",
    "Да": "Yes",
    "Понятно": "OK",
    "Ошибка": "Error",
    "Сайты": "Sites",
    "Приложения": "Apps",
    "Пресет": "Preset",
    "Пресеты": "Presets",
    "Умная": "Smart",
    "Весь трафик": "All traffic",
    "Российские сайты напрямую": "Russian sites direct",
    "Блокировать рекламу": "Block ads",
    "Локальные сети напрямую": "Local networks direct",
    "FakeDNS": "FakeDNS",
    "Удалённый DNS": "Remote DNS",
    "Прямой DNS": "Direct DNS",
    "Sniffing": "Sniffing",
    "Mux": "Mux",
    "Фрагментация TLS ClientHello": "TLS ClientHello fragmentation",
    "Уровень логов": "Log level",
    "Автоподключение": "Auto-connect",
    "Подключаться при запуске": "Connect at launch",
    "Язык": "Language",
    "Сообщать, когда балансировщик меняет сервер": "Notify when a balancer switches servers",
    "Проверять обновления": "Check for updates",
    "Автообновление подписок": "Auto-update subscriptions",
    "Интервал, ч": "Interval, h",
    "Обновлять при запуске": "Update at launch",
    "URL для пинга": "Ping URL",
    "Таймаут пинга, с": "Ping timeout, s",
    "Имя устройства": "Device name",
    "Kill switch": "Kill switch",
    "MTU": "MTU",
    "Скопировать ссылку": "Copy the link",
    "Скопировать JSON": "Copy the JSON",
    "Отправить файлом": "Send as a file",
    "Открыть подписку": "Open the subscription",
    "Поддержка": "Support",
    "Сайт": "Website",
    "Объявление": "Announcement",
    "HWID": "HWID",
    "Автоматически — только если без него подписка не загружается":
        "Automatically — only if the subscription won't load without it",
    "Всегда": "Always",
    "Никогда": "Never",
    "Панели (Remnawave, Marzban) выбирают формат подписки по User-Agent. Happ/3.0.0 получает полные Xray JSON-конфиги.":
        "Panels (Remnawave, Marzban) pick the subscription format by the User-Agent. Happ/3.0.0 gets full Xray JSON configs.",
    "HWID — идентификатор устройства для панелей с лимитом устройств (Remnawave и др.).":
        "The HWID is the device identifier for panels with a device limit (Remnawave and others).",
    "Подписки": "Subscriptions",
    "Дополнительно": "Advanced",
    "Маршруты": "Routes",
    "Приложение": "App",
    "Умная* — правила маршрутизации из подписки": "Smart* — the routing rules from the subscription",
    "* Правила задаёт ваш VPN-провайдер в конфиге: например, российские сайты напрямую.":
        "* Your VPN provider sets the rules in the config: for example, Russian sites direct.",
    "Без правил провайдера": "Without the provider's rules",
    "Весь трафик через VPN": "All traffic through the VPN",
    "Обновить geoip/geosite": "Update geoip/geosite",
    "Geo-файлы не нужны мобильному ядру: оно само скачивает свежие наборы правил":
        "The mobile core needs no geo files: it downloads fresh rule sets itself",
    "Mux и фрагментация применяются к серверам из ссылок; конфиги провайдера идут как есть":
        "Mux and fragmentation apply to servers from links; provider configs run as they are",
    "Сначала добавьте подписку или сервер.": "Add a subscription or a server first.",
    "Не удалось подключиться: {text}": "Couldn't connect: {text}",
    "Загрузка подписки…": "Loading the subscription…",
    "Обновление подписок…": "Updating the subscriptions…",
    "Проверка задержки…": "Checking latency…",
    "Добавлено: {title} — {n} {servers}": "Added: {title} — {n} {servers}",
    "Не удалось загрузить подписку: {err}": "Couldn't load the subscription: {err}",
    "Не обновились: ": "Didn't update: ",
    "Подписки обновлены": "The subscriptions are updated",
    "Не удалось распознать: ": "Couldn't recognise: ",
    "{name} не отвечает": "{name} doesn't respond",
    "на {ms} мс быстрее, чем {name}": "{ms} ms faster than {name}",
    "Сервер: {name}": "Server: {name}",
    "Ваш IP": "Your IP",
    "IP не determined": "IP unknown",
    "Обновить IP": "Refresh the IP",
    "Скорость": "Speed",
    "Вставьте ссылку на подписку или серверы": "Paste a subscription link or servers",
    "https:// или vless:// …": "https:// or vless:// …",
    "Ссылка на подписку": "Subscription link",
    "Ссылки, JSON-конфиги или подписка": "Links, JSON configs or a subscription",
    "Распознано серверов: {n}": "Servers recognised: {n}",
    "Не удалось расшифровать ссылку Happ: {err}": "Couldn't decrypt the Happ link: {err}",
    "Ключа от такой ссылки в этой версии нет — попросите у провайдера обычную ссылку https://…":
        "This version has no key for that link — ask your provider for a plain https://… link",
    "Ссылка добавлена": "The link is added",
    "Добавьте серверы в балансировщик": "Add servers to the balancer",
    "В балансировщике «{name}» нет серверов: добавьте их через «Изменить».":
        "The balancer “{name}” has no servers: add some with “Edit”.",
    "Отметьте подписку целиком — новые серверы попадут в балансировщик сами":
        "Tick a whole subscription — its new servers join the balancer by themselves",
    "Через какой сервер идёт трафик": "The server the traffic goes through",
    "Лучший сейчас: {name} ({ms} мс)": "Best right now: {name} ({ms} ms)",
    "Балансировщик переключился": "The balancer switched",
    "Постоянное уведомление показывает скорость": "The ongoing notification shows the speed",
    "Остановить": "Stop",
    "Nimbus подключён": "Nimbus is connected",
    "Запуск…": "Starting…",
    "Служба остановлена": "The service stopped",
    "Ядро остановилось: {last}": "The core stopped: {last}",
    "Переподключение…": "Reconnecting…",
    "Ошибка запуска: {err}": "Start failed: {err}",
    "Конфиг не прошёл проверку ядра: {err}": "The core rejected the config: {err}",
    "Версия": "Version",
    "Данные": "Data",
    "Открыть папку данных нельзя — экспортируйте файлы": "The data folder can't be opened — export the files",
    "Исходный код": "Source code",
    "Лицензии": "Licenses",
    "Порт настольного Nimbus (Xray-core/sing-box) на Android: то же ядро sing-box, те же подписки, ссылки Happ и балансировщики.":
        "The desktop Nimbus port (Xray-core/sing-box) for Android: the same sing-box core, the same subscriptions, Happ links and balancers.",

    # -- the phone's own screens (added by the Android port) --------------------------------
    'HWID — идентификация устройства': 'HWID — device identification',
    'IP если нет': 'IP if no match',
    'IP по запросу': 'IP on demand',
    'JSON скопирован': 'The JSON is copied',
    'MTU должен быть числом от 576 до 9000': 'The MTU must be a number from 576 to 9000',
    'Nimbus': 'Nimbus',
    'Nimbus {version}': 'Nimbus {version}',
    'TLS/SNI, HTTP Host и QUIC — правила по домену работают даже без fakeip': 'TLS/SNI, HTTP Host and QUIC — domain rules work even without fakeip',
    'User-Agent, автообновление, HWID': 'User agent, automatic updates, HWID',
    'allowInsecure игнорируется: соединение проверяет сертификат': 'allowInsecure is ignored: the connection checks the certificate',
    'gRPC multiMode не поддерживается мобильным ядром': "The mobile core doesn't support gRPC multiMode",
    'vless://…, vmess://…, несколько ссылок по строке, JSON или nimbus://…': 'vless://…, vmess://…, one link per line, JSON or nimbus://…',
    'Авто': 'Auto',
    'Автообновление': 'Automatic updates',
    'Адрес': 'Address',
    'Адрес для проверки': 'The address to check',
    'Адрес подписки': 'Subscription URL',
    'Балансировщик переключился — показать, куда': 'Show which node the balancer switched to',
    'Балансировщик сам переключается на самый быстрый сервер из выбранных.': 'A balancer switches to the fastest of the servers you picked.',
    'Без профиля': 'No profile',
    'Без разрешения VPN подключиться нельзя': "Nimbus can't connect without the VPN permission",
    'Блокировать трафик без VPN': 'Block traffic without the VPN',
    'Быстрее и надёжнее: домен виден даже когда приложение шлёт запрос первым': 'Faster and more reliable: the domain is visible even when an app sends a bare address',
    'Ваш адрес': 'Your address',
    'Ваши подписки': 'Your subscriptions',
    'Весь трафик устройства через виртуальный интерфейс': "All of the device's traffic through the virtual interface",
    'Виртуальный интерфейс: работает со всеми приложениями': 'A virtual interface: works with every app',
    'Включить': 'Turn on',
    'Включён фильтр приложений': 'The app filter is on',
    'Все разделы': 'All sections',
    'Все, кроме выбранных': 'All except the chosen ones',
    'Всегда лучший': 'Always the fastest',
    'Вставить': 'Paste',
    'Всё': 'All',
    'Выберите сервер, чтобы подключиться': 'Pick a server to connect',
    'Выбранные приложения': 'Chosen apps',
    'Выбрать': 'Choose',
    'Выкл': 'Off',
    'Выключить': 'Turn off',
    'Группа «{tag}»': 'The group “{tag}”',
    'Держать узел': 'Keep the node',
    'Диагностика': 'Diagnostics',
    'Добавить серверы': 'Add servers',
    'Добавленные': 'Added',
    'Домашняя сеть и принтеры видны напрямую': 'The home network and printers stay reachable directly',
    'Домен или его окончание: .ru, example.com': 'A domain or its suffix: .ru, example.com',
    'Дробить пакеты, чтобы обойти блокировку по TLS-отпечатку': 'Split packets to get past TLS-fingerprint blocking',
    'Если панель не прислала свой': "Used when the panel doesn't send its own",
    'Если туннель упадёт, интернет пропадёт вместо утечки. Включается в настройках Android.': "If the tunnel dies, the internet stops instead of leaking. Turned on in Android's own settings.",
    'Есть': 'Available',
    'Есть новая версия: {version}': 'A new version is out: {version}',
    'Ещё': 'More',
    'Журнал': 'Log',
    'Журнал пуст. Он заполняется, когда туннель работает.': 'The log is empty. It fills up while the tunnel runs.',
    'Задержка': 'Latency',
    'Задержка проверяется…': 'Measuring latency…',
    'Закрепление сертификата провайдером не поддерживается мобильным ядром': "The mobile core doesn't support the provider's certificate pinning",
    'Запускать после перезагрузки': 'Start after a reboot',
    'Запускать туннель с выбранным сервером': 'Start the tunnel with the chosen server',
    'Импорт': 'Import',
    'Импорт…': 'Importing…',
    'Интервал': 'Interval',
    'Интервал, часов': 'Interval, hours',
    'Инфо': 'Info',
    'Истекает': 'Expires',
    'Источники целиком': 'Whole sources',
    'Как в списке': 'As listed',
    'Как выбирать': 'How to choose',
    'Как ответил DNS — адрес или домен': 'Whatever DNS answered — an address or a domain',
    'Конфиг, который сейчас собирается': 'The config being built right now',
    'Куда': 'Where to',
    'Куда добавить': 'Where to add',
    'Лучшая задержка: {ms}': 'Best latency: {ms}',
    'Мимо VPN': 'Past the VPN',
    'Многие панели отдают ссылки только знакомому клиенту — поэтому по умолчанию стоит Happ.': 'Many panels only hand links to a client they know — which is why the default is Happ.',
    'Можно оставить пустым — тогда работают только сайты': 'Can stay empty — then only the sites apply',
    'Мультиплексирование': 'Multiplexing',
    'Наведите камеру на QR-код': 'Point the camera at a QR code',
    'Назад': 'Back',
    'Название': 'Name',
    'Название группы': 'Group name',
    'Название подписки': 'Subscription name',
    'Название сервера': 'Server name',
    'Настроить': 'Set up',
    'Настройки и профили лежат в папке приложения и совпадают по формату с настольной версией.': "Settings and profiles live in the app's folder and use the same format as the desktop version.",
    'Не выбран': 'Not chosen',
    'Не отправлять HWID': "Don't send the HWID",
    'Не отправлять вовсе': 'Never send it',
    'Не удалось импортировать': "Couldn't import",
    'Не удалось обновить подписку': "Couldn't update the subscription",
    'Не удалось проверить обновления': "Couldn't check for updates",
    'Не удалось прочитать файл': "Couldn't read the file",
    'Не удалось скачать обновление': "Couldn't download the update",
    'Не удалось собрать конфигурацию': "Couldn't build the config",
    'Не удалось узнать': "Couldn't find out",
    'Не удалось узнать адрес': "Couldn't find out the address",
    'Несколько соединений в одном — быстрее на плохой связи, но не все серверы это умеют': 'Several connections in one — faster on a bad link, but not every server supports it',
    'Нет': 'None',
    'Нет подключения': 'Not connected',
    'Новая версия {version}': 'New version {version}',
    'Новая версия доступна': 'A new version is available',
    'Новый профиль': 'New profile',
    'Нужно разрешение на автозапуск у производителя': "Needs the manufacturer's autostart permission",
    'ОК': 'OK',
    'Обновить до {version}': 'Update to {version}',
    'Обновлена': 'Updated',
    'Обновление подписки…': 'Updating the subscription…',
    'Обновлений нет': 'No updates',
    'Обновлять подписки, когда пришло их время': 'Update subscriptions when their time comes',
    'Обходить локальные адреса': 'Bypass local addresses',
    'Определять домен по трафику': 'Sniff the domain out of the traffic',
    'Оставить в конфиге то, что прислала подписка': 'Keep in the config what the subscription sent',
    'Отдельные серверы': 'Individual servers',
    'Откройте свойства подписки в настройках': "Open the subscription's properties in Settings",
    'Отправить': 'Send',
    'Отправить журнал': 'Send the log',
    'Отправить, только если без него пришли пустые серверы': 'Send it only if the servers came back empty without it',
    'Отправлять сразу — так делают мобильные клиенты': "Send it right away — that's what mobile clients do",
    'Оформление': 'Appearance',
    'Ошибки': 'Errors',
    'Панель требует идентификацию устройства': 'The panel asks for a device ID',
    'Папка данных': 'Data folder',
    'Параметры фрагментации': 'Fragmentation settings',
    'Переключаться на самый быстрый сразу — обрывы соединений возможны': 'Switch to the fastest at once — connections may drop',
    'Переключаться, только когда узел упал или другой заметно быстрее': 'Switch only when a node dies or another is clearly faster',
    'Переместить сервер': 'Move the server',
    'Поделиться ссылкой': 'Share the link',
    'Подменять адреса (fakeip)': 'Fake the addresses (fakeip)',
    'Подписка и её серверы будут удалены.': 'The subscription and its servers will be deleted.',
    'Подписка обновлена: {n}': 'Subscription updated: {n}',
    'Поиск приложения': 'Search apps',
    'Показать конфиг': 'Show the config',
    'Потоков в соединении': 'Streams per connection',
    'Почему не работает': "Why it doesn't work",
    'Правила провайдера': 'Provider rules',
    'Правила провайдера включены': "The provider's rules are on",
    'Правила сначала по домену, потом по адресу': 'Rules by domain first, then by address',
    'Предупр.': 'Warnings',
    'Принимаются ссылки Happ (включая зашифрованные), v2rayN/v2rayNG, clash, sing-box и обычный JSON Xray.': 'Happ links (encrypted ones included), v2rayN/v2rayNG, clash, sing-box and plain Xray JSON are all accepted.',
    'Приём': 'Download',
    'Проверить': 'Check',
    'Проверить просроченные подписки, когда приложение открыли': 'Check the overdue subscriptions when the app opens',
    'Проверка обновлений…': 'Checking for updates…',
    'Проверка…': 'Checking…',
    'Прокси': 'Proxy',
    'Пропустить': 'Skip',
    'Профилей нет. Профиль — это список приложений и сайтов, которые идут мимо туннеля (или только через него).': 'No profiles yet. A profile is a list of apps and sites that go past the tunnel — or only through it.',
    'Профили': 'Profiles',
    'Профиль': 'Profile',
    'Профиль маршрутизации': 'Routing profile',
    'Пусто': 'Nothing to add',
    'Работает, только когда включён TUN: sing-box сам узнаёт, какое приложение открыло соединение.': 'Works only with TUN on: sing-box finds out itself which app opened a connection.',
    'Раздел': 'Section',
    'Размер пакета виртуального интерфейса': "The virtual interface's packet size",
    'Разрешать домен в адрес, если правило требует адрес': 'Resolve a domain to an address when a rule needs one',
    'Режим, MTU, always-on, мультиплексирование, фрагментация': 'Mode, MTU, always-on, multiplexing, fragmentation',
    'Рекламные и счётчиковые домены не отвечают': 'Ad and tracker domains get no answer',
    'Российские адреса и сайты идут напрямую': 'Russian addresses and sites go direct',
    'Сайты и домены': 'Sites and domains',
    'Свой HWID': 'Your own HWID',
    'Свойства': 'Properties',
    'Сейчас {version}': 'Now {version}',
    'Сейчас в наборе: {n}': 'In the set now: {n}',
    'Сервер будет удалён из списка.': 'The server will be removed from the list.',
    'Серверов': 'Servers',
    'Серверов пока нет. Добавьте подписку, ссылку или конфиг.': 'No servers yet. Add a subscription, a link or a config.',
    'Серверы не найдены': 'No servers found',
    'Серверы останутся в списке.': 'The servers stay in the list.',
    'Серверы, подмена адресов, определение домена': 'Servers, fake addresses, domain sniffing',
    'Системная': 'System',
    'Системные': 'System apps',
    'Системный': 'System',
    'Скачать серверы заново': 'Download the servers again',
    'Сколько секунд ждать ответа': 'How many seconds to wait for an answer',
    'Сколько соединений уместить в одно': 'How many connections to fit into one',
    'Скопировать': 'Copy',
    'Слишком длинно для QR-кода': 'Too long for a QR code',
    'События': 'Events',
    'Соединение': 'Connection',
    'Соединений': 'Connections',
    'Списки идут напрямую, остальное — через туннель': 'The lists go direct, everything else through the tunnel',
    'Ссылка из панели: Nimbus скачает серверы и будет обновлять их сам.': 'A link from a panel: Nimbus downloads the servers and keeps updating them.',
    'Ссылки или конфиг': 'Links or a config',
    'Стратегия домена': 'Domain strategy',
    'Строки sing-box и ошибки подключения': "sing-box's own lines and connection errors",
    'Таймаут проверки': 'Check timeout',
    'Текущий конфиг': 'The current config',
    'Только VPN': 'VPN only',
    'Только выбранные': 'Only the chosen ones',
    'Только приложения, которые сами ходят через прокси': 'Only apps that use a proxy themselves',
    'Только приложения, которые умеют работать через прокси': 'Only apps that know how to work through a proxy',
    'Туннель': 'Tunnel',
    'Туннель работает только для выбранных приложений — или во всех, кроме них': 'The tunnel works only for the chosen apps — or for all except them',
    'Уведомлять о смене узла': 'Notify when the node changes',
    'Удалить балансировщик?': 'Delete the balancer?',
    'Удалить подписку?': 'Delete the subscription?',
    'Удалить профиль?': 'Delete the profile?',
    'Удалить сервер?': 'Delete the server?',
    'Удалённый — DoH (https://…) или адрес:порт. Прямой — то же самое, но его ответы идут мимо туннеля.': 'Remote is DoH (https://…) or address:port. Direct is the same, but its answers go past the tunnel.',
    'Узел': 'Node',
    'Узел можно выбрать самим.': 'You can pick the node yourself.',
    'Узел переключён': 'The node is switched',
    'Узлы': 'Nodes',
    'Узлы видны, пока подключение активно.': 'Nodes are visible while the connection is up.',
    'Уровень журнала': 'Log level',
    'Файл': 'File',
    'Файлом в другое приложение': 'As a file, to another app',
    'Фильтр приложений': 'App filter',
    'Фрагментация': 'Fragmentation',
    'Через туннель идут только эти списки': 'Only these lists go through the tunnel',
    'Через туннель идёт всё, кроме профилей ниже': 'Everything goes through the tunnel except the profiles below',
    'Это не ссылка и не конфиг': "That's neither a link nor a config",
    'Этот сервер не запустится на телефоне': "This server won't run on a phone",
    'Этот узел выбирает sing-box: он идёт через самый быстрый.': 'sing-box picks this node: it goes through the fastest one.',
    'авто': 'auto',
    'без правил провайдера': "without the provider's rules",
    'быстрый': 'fastest',
    'в релизе {tag} нет APK': 'release {tag} has no APK',
    'включён': 'on',
    'все, кроме них': 'all except them',
    'всегда лучший': 'always the fastest',
    'держит узел': 'keeps the node',
    'из адреса устройства': "from the device's address",
    'мимо VPN': 'past the VPN',
    'отправляется': 'sent',
    'по настройке': 'by the setting',
    'проверка каждые {n} с': 'checked every {n} s',
    'стабильный': 'stable',
    'только они': 'only they',
    'фильтр приложений': 'app filter',
    '…': '…',
}

EXTRA_PLURALS = {
    "приложение": ("app", "apps"),
    '{n} балансировщик': ('{n} balancer', '{n} balancers'),
    '{n} подписка не обновилась': ("{n} subscription didn't update", "{n} subscriptions didn't update"),
    '{n} приложение': ('{n} app', '{n} apps'),
    '{n} сайт': ('{n} site', '{n} sites'),
    '{n} сервер': ('{n} server', '{n} servers'),
    '{n} узел': ('{n} node', '{n} nodes'),
    'Добавлен {n} сервер': ('Added {n} server', 'Added {n} servers'),
    'сервер': ('server', 'servers'),
}


def kstr(s: str) -> str:
    out = []
    for ch in s:
        if ch == "\\":
            out.append("\\\\")
        elif ch == '"':
            out.append('\\"')
        elif ch == "$":
            out.append("\\$")
        elif ch == "\n":
            out.append("\\n")
        elif ch == "\r":
            out.append("\\r")
        elif ch == "\t":
            out.append("\\t")
        else:
            out.append(ch)
    return '"' + "".join(out) + '"'


def chunked_map(entries, per_line=1) -> str:
    lines = []
    for k, v in entries:
        lines.append(f"        {kstr(k)} to {v},")
    return "\n".join(lines)


def main() -> None:
    nimbus_root = Path(sys.argv[1] if len(sys.argv) > 1 else HERE.parent.parent / "nimbus")
    sys.path.insert(0, str(nimbus_root))
    from nimbus import happ_keys, locale_en  # noqa: E402

    # -- HappKeys.kt ------------------------------------------------------------------
    body = [
        'package com.zalchik.nimbus.data',
        '',
        '/* Generated by tools/generate_kotlin_data.py from nimbus/happ_keys.py — do not edit by',
        ' * hand: replace it whole from a newer desktop Nimbus (a newer Happ key table).',
        ' *',
        " * Happ's embedded RSA keys, one table per link format.",
        ' *',
        ' * PKCS1: happ://crypt/ (1024-bit) and crypt2…crypt4 (4096-bit), DER PKCS#1.',
        ' * CRYPT5: happ://crypt5/ picks its key by the 8-character marker the link starts and ends',
        " * with, DER PKCS#8. A marker that isn't here is reported as unknown and the user is asked",
        ' * for the plain https link instead.',
        ' */',
        'object HappKeys {',
        '    val PKCS1: Map<String, String> = mapOf(',
    ]
    for name in ("crypt", "crypt2", "crypt3", "crypt4"):
        body.append(f"        {kstr(name)} to {kstr(happ_keys.PKCS1_KEYS[name])},")
    body += ['    )', '', '    val CRYPT5: Map<String, String> = mapOf(']
    for marker, key in happ_keys.CRYPT5_KEYS.items():
        body.append(f"        {kstr(marker)} to {kstr(key)},")
    body += ['    )', '}', '']
    (OUT / "data" / "HappKeys.kt").write_text("\n".join(body), "utf-8")

    # -- LocaleEn.kt --------------------------------------------------------------------
    texts = dict(locale_en.TEXTS)
    for k, v in EXTRA_TEXTS.items():
        texts.setdefault(k, v)
    plurals = dict(locale_en.PLURALS)
    for k, v in EXTRA_PLURALS.items():
        plurals.setdefault(k, v)
    body = [
        'package com.zalchik.nimbus.i18n',
        '',
        '/* Generated by tools/generate_kotlin_data.py from nimbus/locale_en.py (plus the mobile',
        ' * extras in that script) — do not edit by hand. Keys are the Russian source texts exactly',
        ' * as they appear in tr(...) calls; a text missing here shows in Russian. */',
        'object LocaleEn {',
        '    val TEXTS: Map<String, String> = mapOf(',
    ]
    for k, v in texts.items():
        body.append(f"        {kstr(k)} to {kstr(v)},")
    body += ['    )', '', '    // plural(n, one, few, many) in English: the Russian "one" form -> (singular, plural).',
             '    val PLURALS: Map<String, Pair<String, String>> = mapOf(']
    for k, v in plurals.items():
        body.append(f"        {kstr(k)} to Pair({kstr(v[0])}, {kstr(v[1])}),")
    body += ['    )', '}', '']
    (OUT / "i18n" / "LocaleEn.kt").write_text("\n".join(body), "utf-8")
    print(f"HappKeys.kt: {len(happ_keys.PKCS1_KEYS)} + {len(happ_keys.CRYPT5_KEYS)} keys")
    print(f"LocaleEn.kt: {len(texts)} texts, {len(plurals)} plurals")


if __name__ == "__main__":
    main()
