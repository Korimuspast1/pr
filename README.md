# SteamFinder

Нативное Windows-приложение в стиле клиента Steam: вводишь название игры — приложение
обращается к публичному API магазина Steam и показывает список игр с картинками, а по
выбранной позиции — обложку, цену, скидку, жанры, разработчика, дату выхода, Metacritic
и описание.

Готовый файл: **`dist/SteamFinder.exe`** (x64, Windows 7+, ~310 КБ, без установки и без
зависимостей — просто скачать и запустить двойным кликом).

## Что умеет

* Поиск по магазину Steam (`storesearch`) — до 25 результатов.
* Миниатюра-капсула, тип (Игра / DLC / Комплект) и цена для каждого результата.
* Карточка игры (`appdetails`, русская локаль, цены в рублях):
  * обложка 460×215,
  * цена, старая цена и размер скидки,
  * тип, разработчик, издатель, дата выхода,
  * жанры, платформы, оценка Metacritic, AppID,
  * краткое описание.
* Кнопка «Открыть страницу в Steam» — открывает страницу игры в браузере.
* Двойной клик по результату в списке делает то же самое.
* Горячие клавиши: **Enter** — искать, **Esc** — очистить поле поиска.

## Технологии

* Чистый Win32 API на C — окно, тёмная тема Steam, owner-draw список и кнопки.
* **WinHTTP** — HTTPS-запросы к `store.steampowered.com`.
* **GDI+** — загрузка и отрисовка JPEG/PNG обложек прямо из памяти.
* Собственный минимальный JSON-парсер (`src/json.c`), UTF-8 → UTF-16.
* Сеть работает в фоновых потоках, интерфейс не зависает.
* Никаких фреймворков, рантаймов и .NET — один exe-файл.

## Используемые API Steam

```
https://store.steampowered.com/api/storesearch/?term=<запрос>&l=russian&cc=RU
https://store.steampowered.com/api/appdetails?appids=<appid>&l=russian&cc=ru
```

Ключ Steam Web API не нужен — это публичные эндпоинты магазина.

## Структура

```
src/main.c        — интерфейс, отрисовка, потоки, логика
src/http.c/.h     — HTTPS-клиент на WinHTTP
src/json.c/.h     — JSON-парсер
src/app.rc        — иконка, манифест (visual styles, DPI), версия
src/app.manifest  — манифест приложения
assets/app.ico    — иконка приложения
build.sh          — кросс-сборка exe
dist/SteamFinder.exe — собранное приложение
```

## Пересборка

Собирается из Linux/macOS кросс-компилятором Zig (он же clang + mingw-w64):

```bash
python3 -m venv ~/.zigvenv
~/.zigvenv/bin/pip install ziglang
./build.sh            # результат: dist/SteamFinder.exe
```

На самой Windows можно собрать и через MSVC:

```
cl /O2 /DUNICODE /D_UNICODE src\main.c src\json.c src\http.c app.res ^
   winhttp.lib gdiplus.lib ole32.lib shell32.lib gdi32.lib user32.lib ^
   /link /SUBSYSTEM:WINDOWS /OUT:SteamFinder.exe
```

## Примечания

* Приложение ходит в интернет напрямую (WinHTTP, системные настройки прокси).
  При первом запуске Windows SmartScreen может предупредить о неизвестном издателе —
  exe не подписан цифровой подписью, это нормально: «Подробнее» → «Выполнить в любом случае».
* Цены отдаются Steam для региона RU в рублях; интерфейс и данные — на русском.
