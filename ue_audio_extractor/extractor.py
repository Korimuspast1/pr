"""Orchestration, safe output naming, and optional audio transcoding."""

from __future__ import annotations

import hashlib
import os
import shutil
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, List, Optional, Tuple

from .scanner import AudioCandidate, scan_file


LogCallback = Callable[[str], None]
_SUPPORTED_OUTPUT_FORMATS = {"original", "mp3", "flac", "wav", "ogg"}
_VGMSTREAM_EXTENSIONS = {
    ".acb", ".adx", ".at3", ".at9", ".awb", ".bank", ".bnk", ".fsb",
    ".hca", ".pck", ".wem", ".xma",
}


@dataclass(frozen=True)
class ExtractionConfig:
    source: Path
    output: Path
    output_format: str = "original"
    scan_all: bool = False
    ffmpeg: Optional[str] = None
    vgmstream: Optional[str] = None


@dataclass
class ExtractionSummary:
    scanned_files: int = 0
    found_streams: int = 0
    extracted: int = 0
    duplicates: int = 0
    failed: int = 0
    output_dir: Optional[Path] = None


def run_extraction(config: ExtractionConfig, log: Optional[LogCallback] = None) -> ExtractionSummary:
    """Scan an input file/folder and write discovered audio to ``output``.

    Sources are read-only. Existing output files are never overwritten; a
    numeric suffix is added when the same destination name already exists.
    Identical audio byte ranges are de-duplicated by SHA-256.
    """

    emit = log or (lambda _message: None)
    source = Path(config.source).expanduser().resolve()
    output = Path(config.output).expanduser().resolve()
    output_format = config.output_format.lower()
    if output_format not in _SUPPORTED_OUTPUT_FORMATS:
        raise ValueError("Неподдерживаемый формат результата: {}".format(config.output_format))
    if not source.exists():
        raise FileNotFoundError("Исходный путь не найден: {}".format(source))
    if not source.is_file() and not source.is_dir():
        raise ValueError("Укажите обычный файл или папку: {}".format(source))
    if source.is_dir() and output == source:
        raise ValueError("Папка результата не может совпадать с папкой исходников.")
    if source.is_file() and output == source:
        raise ValueError("Папка результата не может совпадать с исходным файлом.")

    output.mkdir(parents=True, exist_ok=True)
    files = _collect_files(source, output)
    summary = ExtractionSummary(scanned_files=len(files), output_dir=output)
    emit("Найдено файлов для проверки: {}".format(len(files)))

    candidates: List[AudioCandidate] = []
    for file_path in files:
        try:
            candidates.extend(scan_file(file_path, scan_all=config.scan_all))
        except (OSError, ValueError) as error:
            summary.failed += 1
            emit("Не удалось прочитать {}: {}".format(_display_path(file_path, source), error))

    summary.found_streams = len(candidates)
    if not candidates:
        emit(
            "Аудиопотоки не найдены. Проверьте, что пакеты уже распакованы и не зашифрованы; "
            "для неизвестных расширений попробуйте --scan-all."
        )
        emit("Проверено файлов: {}".format(summary.scanned_files))
        return summary

    emit("Найдено потоков/аудиофайлов: {}".format(len(candidates)))
    tools = _resolve_tools(config.ffmpeg, config.vgmstream)
    if output_format != "original" and not tools[0] and not tools[1]:
        emit("Предупреждение: ffmpeg и vgmstream-cli не найдены; исходные форматы можно сохранить без перекодирования.")

    seen: Dict[str, Path] = {}
    per_source_number: Dict[Path, int] = {}
    for index, candidate in enumerate(candidates, start=1):
        per_source_number[candidate.source] = per_source_number.get(candidate.source, 0) + 1
        relative = _relative_path(candidate.source, source)
        label = "{} ({}/{})".format(_display_path(candidate.source, source), index, len(candidates))
        try:
            digest = _hash_range(candidate.source, candidate.offset, candidate.length)
            previous = seen.get(digest)
            if previous is not None:
                summary.duplicates += 1
                emit("Дубликат пропущен: {} (уже сохранён как {})".format(label, previous.name))
                continue

            destination = _destination_path(
                output,
                relative,
                candidate,
                per_source_number[candidate.source],
                output_format,
            )
            destination = _unique_path(destination)
            if destination.resolve() == candidate.source.resolve():
                raise ValueError("Результат совпадает с исходным файлом; выберите другую папку вывода.")

            action = _export_candidate(
                candidate,
                destination,
                output_format,
                ffmpeg=tools[0],
                vgmstream=tools[1],
            )
            seen[digest] = destination
            summary.extracted += 1
            emit("Сохранено: {} [{}]".format(destination, action))
        except (OSError, RuntimeError, ValueError) as error:
            summary.failed += 1
            emit("Ошибка: {} — {}".format(label, error))

    emit(
        "Готово. Сохранено: {} | дубликатов: {} | ошибок: {} | папка: {}".format(
            summary.extracted, summary.duplicates, summary.failed, output
        )
    )
    return summary


def _collect_files(source: Path, output: Path) -> List[Path]:
    if source.is_file():
        return [source]

    files: List[Path] = []
    for root, directories, names in os.walk(source, followlinks=False):
        root_path = Path(root)
        directories[:] = sorted(
            name
            for name in directories
            if (root_path / name).resolve() != output
        )
        for name in names:
            path = root_path / name
            try:
                if path.is_file() and path.resolve() != output:
                    files.append(path)
            except OSError:
                continue
    files.sort(key=lambda item: str(item).casefold())
    return files


def _relative_path(path: Path, source: Path) -> Path:
    if source.is_dir():
        try:
            return path.relative_to(source)
        except ValueError:
            pass
    return Path(path.name)


def _display_path(path: Path, source: Path) -> str:
    try:
        return str(path.relative_to(source if source.is_dir() else source.parent))
    except ValueError:
        return str(path)


def _resolve_tools(ffmpeg: Optional[str], vgmstream: Optional[str]) -> Tuple[Optional[str], Optional[str]]:
    def resolve(value: Optional[str], command: str) -> Optional[str]:
        if value:
            expanded = str(Path(value).expanduser())
            if Path(expanded).is_file():
                return str(Path(expanded).resolve())
            return shutil.which(value)
        return shutil.which(command)

    return resolve(ffmpeg, "ffmpeg"), resolve(vgmstream, "vgmstream-cli")


def _hash_range(path: Path, offset: int, length: int) -> str:
    digest = hashlib.sha256()
    remaining = length
    with path.open("rb") as source:
        source.seek(offset)
        while remaining:
            chunk = source.read(min(1024 * 1024, remaining))
            if not chunk:
                raise OSError("Файл закончился до конца найденного аудиопотока.")
            digest.update(chunk)
            remaining -= len(chunk)
    return digest.hexdigest()


def _destination_path(
    output: Path,
    relative: Path,
    candidate: AudioCandidate,
    number: int,
    output_format: str,
) -> Path:
    if candidate.direct and output_format == "original":
        return output / relative

    extension = candidate.extension if output_format == "original" else "." + output_format
    if candidate.direct:
        return (output / relative).with_suffix(extension)
    name = "{}.audio_{:03d}{}".format(relative.name, number, extension)
    return output / relative.parent / name


def _unique_path(path: Path) -> Path:
    if not path.exists():
        return path
    for number in range(2, 100000):
        candidate = path.with_name("{}_{:02d}{}".format(path.stem, number, path.suffix))
        if not candidate.exists():
            return candidate
    raise OSError("Не удалось подобрать свободное имя для {}".format(path))


def _is_same_format(extension: str, output_format: str) -> bool:
    extension = extension.lower()
    if output_format == "original":
        return True
    if output_format == "wav":
        return extension in (".wav", ".wave")
    if output_format == "ogg":
        return extension in (".ogg", ".oga")
    return extension == "." + output_format


def _export_candidate(
    candidate: AudioCandidate,
    destination: Path,
    output_format: str,
    ffmpeg: Optional[str],
    vgmstream: Optional[str],
) -> str:
    destination.parent.mkdir(parents=True, exist_ok=True)
    if _is_same_format(candidate.extension, output_format):
        _atomic_copy_range(candidate.source, candidate.offset, candidate.length, destination)
        return "исходный поток"

    with tempfile.TemporaryDirectory(prefix="ue-audio-") as work_directory:
        work = Path(work_directory)
        input_path = candidate.source
        if not candidate.direct:
            input_path = work / ("input" + candidate.extension)
            _copy_range(candidate.source, candidate.offset, candidate.length, input_path)

        staged_output = work / ("converted." + output_format)
        vgmstream_errors: List[str] = []
        prefer_vgmstream = candidate.extension.lower() in _VGMSTREAM_EXTENSIONS

        if vgmstream and prefer_vgmstream:
            converted, error = _try_vgmstream(
                vgmstream, input_path, output_format, ffmpeg, work
            )
            if converted:
                _atomic_copy_file(converted, destination)
                return "vgmstream → {}".format(output_format.upper())
            vgmstream_errors.append(error)

        ffmpeg_error = ""
        if ffmpeg:
            ffmpeg_error = _run_ffmpeg(ffmpeg, input_path, staged_output, output_format)
            if not ffmpeg_error:
                _atomic_copy_file(staged_output, destination)
                return "ffmpeg → {}".format(output_format.upper())

        if vgmstream and not prefer_vgmstream:
            converted, error = _try_vgmstream(
                vgmstream, input_path, output_format, ffmpeg, work
            )
            if converted:
                _atomic_copy_file(converted, destination)
                return "vgmstream → {}".format(output_format.upper())
            vgmstream_errors.append(error)

        messages = []
        if ffmpeg_error:
            messages.append("ffmpeg: " + ffmpeg_error)
        messages.extend(error for error in vgmstream_errors if error)
        if not ffmpeg and not vgmstream:
            messages.append("для перекодирования установите FFmpeg; для игровых банков также полезен vgmstream-cli")
        raise RuntimeError("; ".join(messages) or "не удалось декодировать аудио")


def _try_vgmstream(
    vgmstream: str,
    input_path: Path,
    output_format: str,
    ffmpeg: Optional[str],
    work: Path,
) -> Tuple[Optional[Path], str]:
    decoded_wav = work / "vgmstream-decoded.wav"
    result_path = work / ("vgmstream-result." + output_format)
    command = [vgmstream, "-o", str(decoded_wav), str(input_path)]
    result = _run(command)
    if result.returncode != 0 or not decoded_wav.is_file() or decoded_wav.stat().st_size == 0:
        return None, _command_error("vgmstream-cli", result)

    if output_format == "wav":
        return decoded_wav, ""
    if not ffmpeg:
        return None, "vgmstream декодировал WAV, но для {} нужен FFmpeg".format(output_format.upper())
    error = _run_ffmpeg(ffmpeg, decoded_wav, result_path, output_format)
    if error:
        return None, "vgmstream декодировал WAV, " + error
    return result_path, ""


def _run_ffmpeg(ffmpeg: str, input_path: Path, output_path: Path, output_format: str) -> str:
    codec_args = {
        "mp3": ["-c:a", "libmp3lame", "-q:a", "2"],
        "flac": ["-c:a", "flac", "-compression_level", "8"],
        # 24-bit PCM is a practical lossless WAV export for typical game audio.
        "wav": ["-c:a", "pcm_s24le"],
        "ogg": ["-c:a", "libvorbis", "-q:a", "5"],
    }[output_format]
    command = [
        ffmpeg,
        "-nostdin",
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-i",
        str(input_path),
        "-map",
        "0:a:0",
        "-vn",
        "-sn",
        "-dn",
        "-map_metadata",
        "0",
        *codec_args,
        str(output_path),
    ]
    result = _run(command)
    if result.returncode != 0 or not output_path.is_file() or output_path.stat().st_size == 0:
        return _command_error("ffmpeg", result)
    return ""


def _run(command: List[str]) -> subprocess.CompletedProcess:
    try:
        return subprocess.run(command, capture_output=True, text=True, check=False)
    except OSError as error:
        return subprocess.CompletedProcess(command, 127, "", str(error))


def _command_error(program: str, result: subprocess.CompletedProcess) -> str:
    details = (result.stderr or result.stdout or "код возврата {}".format(result.returncode)).strip()
    # Error output is useful for diagnostics but can otherwise contain several
    # pages of decoder logs. Keep the UI/console message compact.
    if len(details) > 1000:
        details = details[-1000:]
    return "{}: {}".format(program, details)


def _copy_range(source_path: Path, offset: int, length: int, destination: Path) -> None:
    remaining = length
    with source_path.open("rb") as source, destination.open("wb") as output:
        source.seek(offset)
        while remaining:
            chunk = source.read(min(1024 * 1024, remaining))
            if not chunk:
                raise OSError("Файл закончился до конца найденного аудиопотока.")
            output.write(chunk)
            remaining -= len(chunk)


def _atomic_copy_range(source: Path, offset: int, length: int, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(
        prefix=".ue-audio-", suffix=destination.suffix, dir=str(destination.parent)
    )
    os.close(fd)
    temporary = Path(temporary_name)
    try:
        _copy_range(source, offset, length, temporary)
        os.replace(str(temporary), str(destination))
    finally:
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass


def _atomic_copy_file(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(
        prefix=".ue-audio-", suffix=destination.suffix, dir=str(destination.parent)
    )
    os.close(fd)
    temporary = Path(temporary_name)
    try:
        shutil.copyfile(source, temporary)
        os.replace(str(temporary), str(destination))
    finally:
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass
