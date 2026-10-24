"""Command line entry point for ``python -m ue_audio_extractor``."""

from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path
from typing import Optional, Sequence

from .extractor import ExtractionConfig, run_extraction


FORMAT_CHOICES = ("original", "mp3", "flac", "wav", "ogg")


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="ue_audio_extractor",
        description="Извлекает встроенные аудиопотоки из распакованных Unreal-пакетов и конвертирует их.",
        epilog=(
            "Пример: python -m ue_audio_extractor -i \"D:/Game/Content\" "
            "-o \"D:/Music\" --format mp3"
        ),
    )
    parser.add_argument("-i", "--input", help="Файл или папка с Unreal-ресурсами")
    parser.add_argument("-o", "--output", help="Папка для результата (по умолчанию ./UE_Audio_Extracted)")
    parser.add_argument(
        "--format",
        choices=FORMAT_CHOICES,
        default="original",
        help="Формат результата; original сохраняет исходный поток без перекодирования (по умолчанию)",
    )
    parser.add_argument(
        "--scan-all",
        action="store_true",
        help="Искать сигнатуры аудио во всех файлах, не только в известных типах UE-пакетов",
    )
    parser.add_argument("--ffmpeg", help="Путь к ffmpeg (необязательно, ищется в PATH)")
    parser.add_argument("--vgmstream", help="Путь к vgmstream-cli для WEM/BNK и других игровых кодеков")
    parser.add_argument("--gui", action="store_true", help="Открыть графический интерфейс")
    return parser


def _launch_gui() -> int:
    try:
        from .gui import launch_gui

        launch_gui()
        return 0
    except Exception as error:
        print(
            "Не удалось открыть окно. Установите Python с Tkinter или запустите утилиту с --input.\n"
            + str(error),
            file=sys.stderr,
        )
        return 2


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = _parser()
    args = parser.parse_args(argv)
    if args.gui or not args.input:
        return _launch_gui()

    source = Path(args.input).expanduser()
    if not source.exists():
        parser.error("Путь не найден: {}".format(source))
    output = Path(args.output).expanduser() if args.output else Path.cwd() / "UE_Audio_Extracted"

    config = ExtractionConfig(
        source=source,
        output=output,
        output_format=args.format,
        scan_all=args.scan_all,
        ffmpeg=args.ffmpeg or shutil.which("ffmpeg"),
        vgmstream=args.vgmstream or shutil.which("vgmstream-cli"),
    )

    def log(message: str) -> None:
        print(message, flush=True)

    try:
        summary = run_extraction(config, log=log)
    except (OSError, ValueError) as error:
        print("Ошибка: {}".format(error), file=sys.stderr)
        return 2
    return 1 if summary.failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
