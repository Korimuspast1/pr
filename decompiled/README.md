# Результат восстановления RyuuManifestToolV2.exe

## Краткий итог

`RyuuManifestToolV2.exe` — не .NET-сборка и не архив PyInstaller. Это нативное
64-битное Windows-приложение, собранное из Rust в release-режиме на Tauri 2.9.2.
Оригинальный Rust был оптимизирован и очищен от таблицы символов, поэтому
восстановить **байт-в-байт исходные `.rs` файлы**, их исходные имена, комментарии,
макросы и структуру модулей из этого EXE технически невозможно.

При этом встроенная веб-часть Tauri восстановлена полностью: четыре Brotli-потока
извлечены без изменения содержимого. Это весь поставляемый приложением frontend.
Для JS и CSS также сделаны форматированные копии, пригодные для чтения.

## Найденный файл

- Исходный файл: `../RyuuManifestToolV2.exe`
- SHA-256: `25a33d74a2271607b1bcb2a4729fcbfc587a90d33165b2531482d286ee6d72bd`
- Формат: PE32+, x86-64, Windows GUI
- Размер: 7 287 296 байт
- Image base: `0x140000000`
- Entry point RVA: `0x4280fc`
- CLR/.NET header отсутствует
- Таблица экспортов отсутствует
- Таблица символов отсутствует
- PE overlay отсутствует
- Время PE-заголовка: 2026-09-28 00:11:54 UTC
- PDB-путь, оставшийся в бинарнике: `RyuuManifestToolV2.pdb`

## Структура результата

### `frontend/`

Точное извлечённое содержимое:

- `index.html` — 788 байт;
- `vite.svg` — 1 497 байт;
- `assets/index--wENrqoK.js` — 496 739 байт;
- `assets/index-B1sg37uI.css` — 66 753 байта.

Копии для чтения:

- `index.pretty.html`;
- `assets/index.pretty.js` — основной восстановленный код интерфейса;
- `assets/index.pretty.css`.

Форматирование сделано Prettier 3.6.2. Оно меняет только представление кода, но
не возвращает утраченные Vite-минификатором исходные названия. Source map в EXE
не найден. В bundle присутствуют React, JSX runtime и прикладная логика GUI.

### `analysis/`

- `backend-command-contract.txt` — 99 прикладных Tauri-команд, которые frontend
  вызывает или выбирает динамически;
- `frontend-invoked-commands.txt` — непосредственно найденные вызовы, включая
  стандартные команды плагинов Tauri;
- `frontend-command-calls.txt` — контекст важнейших вызовов с аргументами;
- `rust-crates.txt` — 92 зависимости и версии, восстановленные из путей panic/debug;
- `SHA256SUMS` — контрольные суммы извлечённых файлов.

Среди восстановленных backend-контрактов: `load_games`, `add_game_to_library`,
`read_manifest_files`, `deploy_hook_mode`, `authenticate_user`,
`generator_login_with_code`, `depot_dumper_*`, `token_sharing_*`,
`ryuu_tool_denuvo_*`, `check_for_app_update`, `apply_app_update`,
`run_cloud_redirect`, операции с настройками и Steam.

### `resources/`

- `app.ico` — собранная из шести RT_ICON-записей иконка приложения;
- `app.manifest.xml` — Windows assembly manifest;
- исходные RT_ICON/RT_GROUP_ICON/RT_VERSION ресурсы в `.bin`.

### `tools/extract_tauri_assets.py`

Воспроизводимый extractor. Он проверяет SHA-256 EXE, находит четыре имени
ресурсов и потоково распаковывает следующие за ними Brotli-данные.

Запуск:

```bash
python -m pip install brotli
python decompiled/tools/extract_tauri_assets.py RyuuManifestToolV2.exe output
```

## Что удалось определить о native backend

- Компилятор/язык: Rust, release build;
- framework: Tauri 2.9.2, `tauri-utils` 2.8.0;
- HTTP-стек содержит `reqwest`, `hyper`, `rustls`/Windows TLS-компоненты;
- сериализация: `serde`, `serde_json`;
- архивы/сжатие: Brotli, bzip2, flate2, zip;
- приложение работает с Steam config/depotcache, manifest-файлами, Steam ticket,
  обновлениями, hook modes и API `https://generator.ryuu.lol`;
- известные строки ошибок, URL и параметры вызовов сохранены внутри
  форматированного frontend и списка контрактов.

Полный список обнаруженных библиотек находится в `analysis/rust-crates.txt`.

## Почему native часть нельзя превратить в оригинальный source code полностью

Release-компиляция Rust уже выполнила monomorphization, inline, constant folding,
устранила неиспользуемый код и превратила async-функции в state machines. В PE нет
символов и исходного PDB. Декомпилятор способен дать только большой приближённый
C-подобный псевдокод с адресами вроде `FUN_140...`; это не исходный Rust и не
собираемый эквивалент. Для данного файла это дополнительно смешало бы прикладной
код примерно с десятью тысячами функций Rust/Tauri/HTTP runtime.

Таким образом, каталог содержит максимум достоверно восстанавливаемого кода без
угадывания: frontend извлечён полностью и точно, интерфейс native backend и
зависимости документированы, Windows-ресурсы извлечены. Для точного backend source
нужны оригинальные `.rs` файлы либо соответствующий PDB/репозиторий сборки.
