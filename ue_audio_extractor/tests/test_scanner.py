from __future__ import annotations

import struct
import tempfile
import unittest
from pathlib import Path

from ue_audio_extractor.scanner import scan_file


def make_wave(payload: bytes = b"\x00\x00\x00\x00") -> bytes:
    fmt = b"fmt " + struct.pack("<IHHIIHH", 16, 1, 1, 8000, 8000, 1, 8)
    data = b"data" + struct.pack("<I", len(payload)) + payload
    body = b"WAVE" + fmt + data
    return b"RIFF" + struct.pack("<I", len(body)) + body


def make_ogg_page(body: bytes, serial: int, sequence: int, flags: int) -> bytes:
    header = (
        b"OggS"
        + b"\x00"
        + bytes((flags,))
        + struct.pack("<qIIIB", 0, serial, sequence, 0, 1)
    )
    return header + bytes((len(body),)) + body


def make_mp3_frames(count: int = 3) -> bytes:
    # MPEG-1 Layer III, 128 kbit/s, 44.1 kHz: 417 bytes per unpadded frame.
    frame = bytes.fromhex("fffb9000") + bytes(417 - 4)
    return frame * count


class ScannerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def write(self, name: str, payload: bytes) -> Path:
        path = self.root / name
        path.write_bytes(payload)
        return path

    def test_extracts_wave_from_uexp_with_exact_bounds(self) -> None:
        wave = make_wave(b"\x01\x02\x03\x04")
        prefix, suffix = b"package-header", b"serialized-tail"
        path = self.write("Music.uexp", prefix + wave + suffix)

        result = scan_file(path)

        self.assertEqual(len(result), 1)
        self.assertEqual(result[0].offset, len(prefix))
        self.assertEqual(result[0].length, len(wave))
        self.assertEqual(result[0].extension, ".wav")
        self.assertEqual(path.read_bytes()[result[0].offset : result[0].offset + result[0].length], wave)

    def test_finds_ogg_vorbis_pages(self) -> None:
        ogg = (
            make_ogg_page(b"\x01vorbis" + bytes(20), serial=42, sequence=0, flags=0x02)
            + make_ogg_page(bytes(16), serial=42, sequence=1, flags=0x04)
        )
        prefix = b"properties"
        path = self.write("Theme.uasset", prefix + ogg + b"tail")

        result = scan_file(path)

        self.assertEqual(len(result), 1)
        self.assertEqual(result[0].offset, len(prefix))
        self.assertEqual(result[0].length, len(ogg))
        self.assertEqual(result[0].extension, ".ogg")

    def test_finds_raw_mp3_frames(self) -> None:
        prefix = b"unreal-properties"
        audio = make_mp3_frames()
        path = self.write("Voice.uexp", prefix + audio + b"after")

        result = scan_file(path)

        self.assertEqual(len(result), 1)
        self.assertEqual(result[0].offset, len(prefix))
        self.assertEqual(result[0].length, len(audio))
        self.assertEqual(result[0].extension, ".mp3")

    def test_finds_mp3_with_id3v2_tag(self) -> None:
        tag = b"ID3\x03\x00\x00\x00\x00\x00\x00"
        audio = tag + make_mp3_frames()
        path = self.write("Intro.ubulk", b"prefix" + audio)

        result = scan_file(path)

        self.assertEqual(len(result), 1)
        self.assertEqual(result[0].offset, len(b"prefix"))
        self.assertEqual(result[0].length, len(audio))

    def test_ignores_invalid_riff_signature(self) -> None:
        path = self.write("Texture.uasset", b"RIFF" + struct.pack("<I", 20) + b"NOPE" + bytes(16))
        self.assertEqual(scan_file(path), [])

    def test_direct_audio_file_is_returned_as_a_whole(self) -> None:
        payload = make_wave()
        path = self.write("theme.wave", payload)

        result = scan_file(path)

        self.assertEqual(len(result), 1)
        self.assertTrue(result[0].direct)
        self.assertEqual(result[0].offset, 0)
        self.assertEqual(result[0].length, len(payload))
        self.assertEqual(result[0].extension, ".wav")

    def test_scans_audio_streams_inside_sound_banks(self) -> None:
        wave = make_wave()
        prefix = b"BKHD-DIDX-DATA"
        path = self.write("SoundBank.bnk", prefix + wave)

        result = scan_file(path)

        self.assertEqual(len(result), 1)
        self.assertFalse(result[0].direct)
        self.assertEqual(result[0].offset, len(prefix))
        self.assertEqual(result[0].extension, ".wav")

    def test_unknown_extension_is_scanned_only_when_requested(self) -> None:
        path = self.write("mystery.data", b"x" + make_wave())
        self.assertEqual(scan_file(path), [])
        self.assertEqual(len(scan_file(path, scan_all=True)), 1)


if __name__ == "__main__":
    unittest.main()
