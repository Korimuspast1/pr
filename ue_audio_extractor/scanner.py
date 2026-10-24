"""Find self-describing audio streams in Unreal package sidecars.

Unreal's package serialization varies by engine version and cooked codec. This
module intentionally does not guess at raw PCM/ADPCM/XMA payloads: it carves
standard containers whose boundaries can be validated (RIFF/WAVE, Ogg, MP3,
AIFF, CAF, and ISO-BMFF). See README.ru.md for the supported scope.
"""

from __future__ import annotations

import mmap
from dataclasses import dataclass
from pathlib import Path
from typing import List, Optional, Sequence, Tuple


# Audio files and banks which can be passed to FFmpeg and/or vgmstream-cli.
# The bank formats are kept as-is with --format original; a decoder may be able
# to render them when a converted format is requested.
AUDIO_FILE_EXTENSIONS = {
    ".aac", ".ac3", ".acb", ".adx", ".aif", ".aiff", ".alac", ".ape",
    ".at3", ".at9", ".awb", ".bank", ".bik", ".bnk", ".caf", ".flac",
    ".fsb", ".hca", ".m4a", ".mka", ".mkv", ".mp3", ".mp4", ".oga",
    ".ogg", ".opus", ".pck", ".s3m", ".wav", ".wave", ".wem", ".webm",
    ".wma", ".xm", ".xma",
}

# Files in which Unreal commonly stores serialized properties or bulk audio.
# --scan-all can be used for unfamiliar package/container suffixes.
UNREAL_PACKAGE_EXTENSIONS = {
    ".uasset", ".uexp", ".ubulk", ".uptnl", ".umap", ".pak", ".ucas",
    ".utoc", ".udic", ".upayload", ".ushaderbytecode", ".bin", ".bundle",
    ".blob", ".ipk",
}

# Sound banks can contain several independently addressable streams, so scan
# their contents before falling back to treating the whole bank as one input.
AUDIO_BANK_EXTENSIONS = {".acb", ".awb", ".bank", ".bnk", ".fsb", ".pck"}


@dataclass(frozen=True)
class AudioCandidate:
    """A complete audio file or a byte range carved from a package."""

    source: Path
    offset: int
    length: int
    extension: str
    codec: str
    direct: bool = False


_MPEG1_BITRATES = {
    3: (0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0),
    2: (0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0),
    1: (0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0),
}
_MPEG2_BITRATES = {
    3: (0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0),
    2: (0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0),
    1: (0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0),
}
_MPEG_SAMPLE_RATES = (44100, 48000, 32000)


def scan_file(path: Path, scan_all: bool = False) -> List[AudioCandidate]:
    """Return audio candidates in a direct audio file or Unreal package file."""

    path = Path(path)
    try:
        size = path.stat().st_size
    except OSError:
        raise
    if size <= 0 or not path.is_file():
        return []

    suffix = path.suffix.lower()
    if suffix in AUDIO_FILE_EXTENSIONS and suffix not in AUDIO_BANK_EXTENSIONS:
        extension = suffix
        if suffix == ".wave":
            extension = ".wav"
        elif suffix == ".oga":
            extension = ".ogg"
        return [AudioCandidate(path, 0, size, extension, "standalone " + suffix[1:], True)]

    if not scan_all and suffix not in UNREAL_PACKAGE_EXTENSIONS and suffix not in AUDIO_BANK_EXTENSIONS:
        return []

    candidates: List[AudioCandidate] = []
    # mmap keeps large .ucas/.pak files out of Python's heap. Extracted ranges
    # are copied in chunks by the exporter, so a multi-gigabyte package is safe.
    with path.open("rb") as stream:
        with mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
            candidates.extend(_scan_riff(data, path))
            candidates.extend(_scan_aiff(data, path))
            candidates.extend(_scan_caf(data, path))
            candidates.extend(_scan_ogg(data, path))
            candidates.extend(_scan_mp3(data, path))
            candidates.extend(_scan_mp4(data, path))

    if not candidates and suffix in AUDIO_BANK_EXTENSIONS:
        return [AudioCandidate(path, 0, size, suffix, "standalone bank " + suffix[1:], True)]
    return _non_overlapping(candidates)


def _non_overlapping(candidates: Sequence[AudioCandidate]) -> List[AudioCandidate]:
    """Drop nested signatures (e.g. a random OggS string inside a valid RIFF)."""

    ordered = sorted(
        candidates,
        key=lambda item: (item.offset, -item.length, item.extension, item.codec),
    )
    selected: List[AudioCandidate] = []
    end = -1
    seen = set()
    for candidate in ordered:
        identity = (candidate.offset, candidate.length, candidate.extension)
        if identity in seen:
            continue
        seen.add(identity)
        if candidate.offset < end:
            continue
        selected.append(candidate)
        end = candidate.offset + candidate.length
    return selected


def _scan_riff(data: mmap.mmap, source: Path) -> List[AudioCandidate]:
    found: List[AudioCandidate] = []
    for magic, byte_order in ((b"RIFF", "little"), (b"RIFX", "big")):
        cursor = 0
        while True:
            offset = data.find(magic, cursor)
            if offset < 0:
                break
            cursor = offset + len(magic)
            if offset + 12 > len(data) or data[offset + 8 : offset + 12] != b"WAVE":
                continue

            declared_size = int.from_bytes(data[offset + 4 : offset + 8], byte_order)
            length = declared_size + 8
            end = offset + length
            if declared_size < 4 or end > len(data):
                continue
            if not _has_wave_chunks(data, offset, end, byte_order):
                continue

            extension = ".wav"
            # Wwise WEM files are RIFF/WAVE files but use a nonstandard codec
            # payload; the `vorb` chunk / extensible format tag is a useful hint.
            fmt_tag = data[offset + 20 : offset + 22]
            header_end = min(end, offset + 65536)
            if b"vorb" in data[offset + 12 : header_end] or fmt_tag == b"\xff\xff":
                extension = ".wem"
            found.append(AudioCandidate(source, offset, length, extension, "RIFF/WAVE"))
    return found


def _has_wave_chunks(data: mmap.mmap, start: int, end: int, byte_order: str) -> bool:
    cursor = start + 12
    found_fmt = False
    found_data = False
    chunks = 0
    while cursor + 8 <= end and chunks < 256:
        chunk_id = data[cursor : cursor + 4]
        chunk_size = int.from_bytes(data[cursor + 4 : cursor + 8], byte_order)
        payload_start = cursor + 8
        payload_end = payload_start + chunk_size
        if payload_end > end:
            return False
        if chunk_id == b"fmt " and chunk_size >= 16:
            found_fmt = True
        elif chunk_id == b"data":
            found_data = True
        if found_fmt and found_data:
            return True
        cursor = payload_end + (chunk_size & 1)
        chunks += 1
    return False


def _scan_aiff(data: mmap.mmap, source: Path) -> List[AudioCandidate]:
    found: List[AudioCandidate] = []
    cursor = 0
    while True:
        offset = data.find(b"FORM", cursor)
        if offset < 0:
            break
        cursor = offset + 4
        if offset + 12 > len(data) or data[offset + 8 : offset + 12] not in (b"AIFF", b"AIFC"):
            continue
        length = int.from_bytes(data[offset + 4 : offset + 8], "big") + 8
        end = offset + length
        if length < 12 or end > len(data):
            continue
        # An AIFF file should at least contain its common sound-description
        # and sound-data chunks. This keeps incidental FORM strings out.
        scan_end = min(end, offset + 1024 * 1024)
        body = data[offset + 12 : scan_end]
        if b"COMM" not in body or b"SSND" not in body:
            continue
        found.append(AudioCandidate(source, offset, length, ".aiff", "AIFF/AIFC"))
    return found


def _scan_caf(data: mmap.mmap, source: Path) -> List[AudioCandidate]:
    found: List[AudioCandidate] = []
    cursor = 0
    while True:
        offset = data.find(b"caff", cursor)
        if offset < 0:
            break
        cursor = offset + 4
        if offset + 8 > len(data):
            continue
        version = int.from_bytes(data[offset + 4 : offset + 6], "big")
        if version not in (1,):
            continue

        pos = offset + 8
        end = pos
        have_desc = False
        have_data = False
        chunks = 0
        while pos + 12 <= len(data) and chunks < 256:
            chunk_type = data[pos : pos + 4]
            chunk_size = int.from_bytes(data[pos + 4 : pos + 12], "big", signed=True)
            if chunk_size == -1:
                end = len(data)
                if chunk_type == b"data":
                    have_data = True
                break
            if chunk_size < 0:
                break
            chunk_end = pos + 12 + chunk_size
            if chunk_end > len(data):
                break
            if chunk_type == b"desc" and chunk_size >= 32:
                have_desc = True
            if chunk_type == b"data":
                have_data = True
            end = chunk_end
            pos = chunk_end
            chunks += 1
            if have_desc and have_data:
                # CAF's data chunk is the last useful chunk for audio; continue
                # through following metadata chunks when they are present.
                continue
        if have_desc and have_data and end > offset:
            found.append(AudioCandidate(source, offset, end - offset, ".caf", "Core Audio Format"))
    return found


def _ogg_candidate(data: mmap.mmap, source: Path, offset: int) -> Optional[AudioCandidate]:
    size = len(data)
    pos = offset
    first_serial: Optional[int] = None
    expected_sequence: Optional[int] = None
    page_count = 0
    end = offset
    eos = False
    found_audio_codec = False

    while pos + 27 <= size and data[pos : pos + 4] == b"OggS":
        if data[pos + 4] != 0:
            break
        header_type = data[pos + 5]
        segment_count = data[pos + 26]
        table_start = pos + 27
        table_end = table_start + segment_count
        if table_end > size:
            break
        body_size = sum(data[table_start:table_end])
        page_end = table_end + body_size
        if page_end > size:
            break

        serial = int.from_bytes(data[pos + 14 : pos + 18], "little")
        sequence = int.from_bytes(data[pos + 18 : pos + 22], "little")
        if page_count == 0:
            # A self-contained stream starts with a beginning-of-stream page.
            if not (header_type & 0x02):
                break
            first_serial = serial
        elif serial != first_serial or sequence != expected_sequence:
            # Chained Ogg files may start a new logical stream after EOS. Other
            # multiplexed streams are uncommon in game audio and are rejected.
            if not (eos and (header_type & 0x02)):
                break
            first_serial = serial
            eos = False

        if page_count == 0:
            body = data[table_end:page_end]
            found_audio_codec = any(
                marker in body
                for marker in (b"\x01vorbis", b"OpusHead", b"\x7fFLAC", b"Speex   ")
            )
        page_count += 1
        end = page_end
        eos = bool(header_type & 0x04)
        expected_sequence = (sequence + 1) & 0xFFFFFFFF
        pos = page_end
        if eos and not (pos + 4 <= size and data[pos : pos + 4] == b"OggS"):
            break

    if page_count < 2 or not found_audio_codec or end <= offset:
        return None
    return AudioCandidate(source, offset, end - offset, ".ogg", "Ogg audio")


def _scan_ogg(data: mmap.mmap, source: Path) -> List[AudioCandidate]:
    found: List[AudioCandidate] = []
    cursor = 0
    while True:
        offset = data.find(b"OggS", cursor)
        if offset < 0:
            break
        cursor = offset + 4
        candidate = _ogg_candidate(data, source, offset)
        if candidate is not None:
            found.append(candidate)
            cursor = offset + candidate.length
    return found


def _mp3_frame_info(data: mmap.mmap, offset: int) -> Optional[Tuple[int, int, int, int]]:
    if offset + 4 > len(data):
        return None
    header = int.from_bytes(data[offset : offset + 4], "big")
    if (header >> 21) & 0x7FF != 0x7FF:
        return None

    version = (header >> 19) & 0x3  # 3=MPEG-1, 2=MPEG-2, 0=MPEG-2.5
    layer = (header >> 17) & 0x3   # 3=I, 2=II, 1=III
    bitrate_index = (header >> 12) & 0xF
    sample_rate_index = (header >> 10) & 0x3
    padding = (header >> 9) & 0x1
    if version == 1 or layer == 0 or bitrate_index in (0, 15) or sample_rate_index == 3:
        return None

    bitrate_table = _MPEG1_BITRATES if version == 3 else _MPEG2_BITRATES
    bitrate = bitrate_table[layer][bitrate_index] * 1000
    sample_rate = _MPEG_SAMPLE_RATES[sample_rate_index]
    if version == 2:
        sample_rate //= 2
    elif version == 0:
        sample_rate //= 4
    if bitrate <= 0 or sample_rate <= 0:
        return None

    if layer == 3:
        frame_length = (12 * bitrate // sample_rate + padding) * 4
    elif layer == 1 and version != 3:
        frame_length = 72 * bitrate // sample_rate + padding
    else:
        frame_length = 144 * bitrate // sample_rate + padding
    if frame_length < 24:
        return None
    return frame_length, version, layer, sample_rate


def _mp3_audio_length(data: mmap.mmap, offset: int) -> Optional[int]:
    first = _mp3_frame_info(data, offset)
    if first is None:
        return None
    _, version, layer, sample_rate = first
    pos = offset
    frames = 0
    while pos + 4 <= len(data):
        info = _mp3_frame_info(data, pos)
        if info is None:
            break
        frame_length, current_version, current_layer, current_sample_rate = info
        if (current_version, current_layer, current_sample_rate) != (version, layer, sample_rate):
            break
        frame_end = pos + frame_length
        if frame_end > len(data):
            break
        frames += 1
        pos = frame_end

    if frames < 2:
        return None
    if pos + 128 <= len(data) and data[pos : pos + 3] == b"TAG":
        pos += 128  # optional ID3v1 footer
    return pos - offset


def _id3v2_end(data: mmap.mmap, offset: int) -> Optional[int]:
    if offset + 10 > len(data) or data[offset : offset + 3] != b"ID3":
        return None
    major = data[offset + 3]
    if major not in (2, 3, 4) or data[offset + 4] == 0xFF:
        return None
    size_bytes = data[offset + 6 : offset + 10]
    if any(byte & 0x80 for byte in size_bytes):
        return None
    tag_size = (size_bytes[0] << 21) | (size_bytes[1] << 14) | (size_bytes[2] << 7) | size_bytes[3]
    end = offset + 10 + tag_size
    return end if end <= len(data) else None


def _scan_mp3(data: mmap.mmap, source: Path) -> List[AudioCandidate]:
    found: List[AudioCandidate] = []

    cursor = 0
    while True:
        offset = data.find(b"ID3", cursor)
        if offset < 0:
            break
        cursor = offset + 3
        audio_start = _id3v2_end(data, offset)
        if audio_start is None:
            continue
        audio_length = _mp3_audio_length(data, audio_start)
        if audio_length is not None:
            found.append(
                AudioCandidate(source, offset, audio_start - offset + audio_length, ".mp3", "MPEG audio")
            )

    # Also find streams with no ID3 tag. Searching for the sync byte in mmap
    # avoids reading a large package into memory or testing every Python byte.
    cursor = 0
    while True:
        offset = data.find(b"\xff", cursor)
        if offset < 0:
            break
        if offset + 3 >= len(data):
            break
        second = data[offset + 1]
        third = data[offset + 2]
        # Cheap header checks before parsing a possible frame. This matters
        # when scanning large compressed .pak/.ucas files containing many FFs.
        if (
            (second & 0xE0) != 0xE0
            or (second & 0x18) == 0x08
            or (second & 0x06) == 0
            or ((third >> 4) & 0x0F) in (0, 15)
            or ((third >> 2) & 0x03) == 3
        ):
            cursor = offset + 1
            continue
        length = _mp3_audio_length(data, offset)
        if length is not None:
            found.append(AudioCandidate(source, offset, length, ".mp3", "MPEG audio"))
            # Avoid rescanning every sync-looking byte inside a successfully
            # validated stream (important for large compressed game payloads).
            cursor = offset + length
        else:
            cursor = offset + 1
    return found


def _mp4_candidate(data: mmap.mmap, source: Path, offset: int) -> Optional[AudioCandidate]:
    size = len(data)
    pos = offset
    saw_ftyp = False
    saw_mdat = False
    saw_moov = False

    while pos + 8 <= size:
        box_size = int.from_bytes(data[pos : pos + 4], "big")
        box_type = data[pos + 4 : pos + 8]
        header_size = 8
        if box_size == 1:
            if pos + 16 > size:
                break
            box_size = int.from_bytes(data[pos + 8 : pos + 16], "big")
            header_size = 16
        elif box_size == 0:
            box_size = size - pos
        if box_size < header_size or pos + box_size > size:
            break
        if not all(32 <= byte <= 126 for byte in box_type):
            break

        if box_type == b"ftyp":
            if box_size < header_size + 8:
                break
            brand = data[pos + header_size : pos + header_size + 4]
            known_brands = (b"M4A ", b"isom", b"mp41", b"mp42", b"iso2", b"qt  ", b"3gp4", b"dash")
            if brand not in known_brands:
                break
            saw_ftyp = True
        elif box_type == b"mdat":
            saw_mdat = True
        elif box_type == b"moov":
            saw_moov = True

        pos += box_size
        if box_size == 0:
            break

    if saw_ftyp and saw_mdat and saw_moov and pos > offset:
        return AudioCandidate(source, offset, pos - offset, ".m4a", "ISO-BMFF audio")
    return None


def _scan_mp4(data: mmap.mmap, source: Path) -> List[AudioCandidate]:
    found: List[AudioCandidate] = []
    cursor = 4
    while True:
        marker = data.find(b"ftyp", cursor)
        if marker < 0:
            break
        cursor = marker + 4
        offset = marker - 4
        if offset < 0:
            continue
        candidate = _mp4_candidate(data, source, offset)
        if candidate is not None:
            found.append(candidate)
    return found
