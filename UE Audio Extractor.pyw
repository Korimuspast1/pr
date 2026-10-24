"""Double-click launcher for the UE Audio Extractor window on Windows."""

from __future__ import annotations

import os
import sys
from pathlib import Path


sys.path.insert(0, str(Path(__file__).resolve().parent))

try:
    from ue_audio_extractor.gui import launch_gui

    launch_gui()
except Exception as error:
    message = "Не удалось запустить UE Audio Extractor.\n\n{}".format(error)
    if os.name == "nt":
        import ctypes

        ctypes.windll.user32.MessageBoxW(0, message, "UE Audio Extractor", 0x10)
    else:
        raise
