from __future__ import annotations

import struct
import tempfile
import unittest
from pathlib import Path

from ue_audio_extractor.extractor import ExtractionConfig, run_extraction


def make_wave() -> bytes:
    fmt = b"fmt " + struct.pack("<IHHIIHH", 16, 1, 1, 8000, 8000, 1, 8)
    data = b"data" + struct.pack("<I", 4) + b"\x01\x02\x03\x04"
    body = b"WAVE" + fmt + data
    return b"RIFF" + struct.pack("<I", len(body)) + body


class ExtractorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.source = self.root / "assets"
        self.output = self.root / "out"
        self.source.mkdir()

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_saves_embedded_audio_without_changing_source(self) -> None:
        wave = make_wave()
        prefix = b"serialized"
        package = self.source / "Theme.uexp"
        package.write_bytes(prefix + wave + b"tail")

        summary = run_extraction(ExtractionConfig(self.source, self.output))

        extracted = self.output / "Theme.uexp.audio_001.wav"
        self.assertEqual(summary.extracted, 1)
        self.assertEqual(summary.failed, 0)
        self.assertEqual(extracted.read_bytes(), wave)
        self.assertEqual(package.read_bytes(), prefix + wave + b"tail")

    def test_deduplicates_identical_streams(self) -> None:
        wave = make_wave()
        (self.source / "One.uexp").write_bytes(b"a" + wave)
        (self.source / "Two.ubulk").write_bytes(b"different-prefix" + wave)

        summary = run_extraction(ExtractionConfig(self.source, self.output))

        self.assertEqual(summary.extracted, 1)
        self.assertEqual(summary.duplicates, 1)
        self.assertEqual(len(list(self.output.glob("*.wav"))), 1)

    def test_direct_audio_file_is_copied(self) -> None:
        wave = make_wave()
        source_file = self.source / "Music.wav"
        source_file.write_bytes(wave)

        summary = run_extraction(ExtractionConfig(source_file, self.output))

        self.assertEqual(summary.extracted, 1)
        self.assertEqual((self.output / "Music.wav").read_bytes(), wave)

    def test_existing_output_is_not_overwritten(self) -> None:
        wave = make_wave()
        (self.source / "Music.wav").write_bytes(wave)
        self.output.mkdir()
        (self.output / "Music.wav").write_bytes(b"keep me")

        summary = run_extraction(ExtractionConfig(self.source / "Music.wav", self.output))

        self.assertEqual(summary.extracted, 1)
        self.assertEqual((self.output / "Music.wav").read_bytes(), b"keep me")
        self.assertEqual((self.output / "Music_02.wav").read_bytes(), wave)

    def test_rejects_output_equal_to_input_folder(self) -> None:
        with self.assertRaises(ValueError):
            run_extraction(ExtractionConfig(self.source, self.source))


if __name__ == "__main__":
    unittest.main()
