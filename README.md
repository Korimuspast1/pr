# Инструменты

## UE Audio Extractor

Офлайн-утилита для поиска и извлечения распространённых аудиопотоков из распакованных Unreal Engine ресурсов (`.uasset`, `.uexp`, `.ubulk` и других). Есть графический интерфейс и CLI.

- [Инструкция на русском](ue_audio_extractor/README.ru.md)
- Windows: двойной щелчок по [`UE Audio Extractor.pyw`](UE%20Audio%20Extractor.pyw) сразу открывает окно
- CLI: `python -m ue_audio_extractor --help`
- Чтобы собрать самостоятельный Windows `.exe`, запустите [`build_windows_exe.bat`](ue_audio_extractor/build_windows_exe.bat)

Утилита сохраняет исходный формат без потери качества по умолчанию. Для перекодирования в MP3/FLAC/WAV/OGG потребуется FFmpeg. Это не полный парсер всех версий Unreal: зашифрованные контейнеры и непрозрачные платформенные кодеки требуют отдельной распаковки/декодера.

## Custom Profile

Исходники существующего Elyx-плагина находятся в [`custom-profile/`](custom-profile/), инструкция по сборке и установке — в [`custom-profile/README.md`](custom-profile/README.md).
