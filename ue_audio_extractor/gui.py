"""Tkinter UI; imported lazily so the CLI still works without Tk installed."""

from __future__ import annotations

import os
import queue
import shutil
import subprocess
import threading
import sys
from pathlib import Path
from tkinter import END, Tk, StringVar, filedialog, messagebox, ttk
from tkinter.scrolledtext import ScrolledText
from typing import Any, Dict

from .extractor import ExtractionConfig, ExtractionSummary, run_extraction


_FORMAT_LABELS: Dict[str, str] = {
    "Исходный формат (рекомендуется)": "original",
    "MP3 — совместимость": "mp3",
    "FLAC — архив без дополнительной потери": "flac",
    "WAV — несжатый PCM": "wav",
    "OGG Vorbis": "ogg",
}


def launch_gui() -> None:
    root = Tk()
    root.title("UE Audio Extractor")
    root.geometry("920x730")
    root.minsize(780, 620)

    input_value = StringVar()
    output_value = StringVar(value=str(Path.cwd() / "UE_Audio_Extracted"))
    format_value = StringVar(value=next(iter(_FORMAT_LABELS)))
    ffmpeg_value = StringVar(value=shutil.which("ffmpeg") or "")
    vgmstream_value = StringVar(value=shutil.which("vgmstream-cli") or "")
    scan_all_value = StringVar(value="0")
    status_value = StringVar(value="Готово к работе")
    messages: "queue.Queue[Any]" = queue.Queue()

    outer = ttk.Frame(root, padding=14)
    outer.pack(fill="both", expand=True)
    outer.columnconfigure(1, weight=1)

    ttk.Label(outer, text="Исходные файлы Unreal Engine", font=("TkDefaultFont", 11, "bold")).grid(
        row=0, column=0, columnspan=3, sticky="w", pady=(0, 6)
    )
    ttk.Entry(outer, textvariable=input_value).grid(row=1, column=0, columnspan=3, sticky="ew", pady=(0, 6))
    input_buttons = ttk.Frame(outer)
    input_buttons.grid(row=2, column=0, columnspan=3, sticky="w", pady=(0, 10))

    def choose_folder() -> None:
        chosen = filedialog.askdirectory(title="Выберите папку с распакованными Unreal-ресурсами")
        if chosen:
            input_value.set(chosen)

    def choose_file() -> None:
        chosen = filedialog.askopenfilename(
            title="Выберите Unreal-пакет или аудиофайл",
            filetypes=(
                ("Unreal и аудио", "*.uasset *.uexp *.ubulk *.uptnl *.umap *.pak *.ucas *.utoc *.wem *.bnk *.ogg *.wav *.mp3"),
                ("Все файлы", "*"),
            ),
        )
        if chosen:
            input_value.set(chosen)

    ttk.Button(input_buttons, text="Выбрать папку…", command=choose_folder).pack(side="left", padx=(0, 6))
    ttk.Button(input_buttons, text="Выбрать файл…", command=choose_file).pack(side="left")

    ttk.Label(outer, text="Папка результата").grid(row=3, column=0, sticky="w", pady=4)
    ttk.Entry(outer, textvariable=output_value).grid(row=3, column=1, sticky="ew", padx=8, pady=4)

    def choose_output() -> None:
        chosen = filedialog.askdirectory(title="Куда сохранить аудио")
        if chosen:
            output_value.set(chosen)

    ttk.Button(outer, text="Обзор…", command=choose_output).grid(row=3, column=2, sticky="e", pady=4)

    ttk.Label(outer, text="Формат").grid(row=4, column=0, sticky="w", pady=4)
    ttk.Combobox(
        outer,
        textvariable=format_value,
        values=list(_FORMAT_LABELS),
        state="readonly",
        width=43,
    ).grid(row=4, column=1, sticky="w", padx=8, pady=4)
    ttk.Checkbutton(
        outer,
        text="Сканировать файлы с любым расширением (медленнее)",
        variable=scan_all_value,
        onvalue="1",
        offvalue="0",
    ).grid(row=5, column=0, columnspan=3, sticky="w", pady=(2, 8))

    tools = ttk.LabelFrame(outer, text="Дополнительные декодеры (необязательно)", padding=8)
    tools.grid(row=6, column=0, columnspan=3, sticky="ew", pady=(0, 10))
    tools.columnconfigure(1, weight=1)
    ttk.Label(tools, text="FFmpeg").grid(row=0, column=0, sticky="w", padx=(0, 8), pady=3)
    ttk.Entry(tools, textvariable=ffmpeg_value).grid(row=0, column=1, sticky="ew", pady=3)
    ttk.Label(tools, text="vgmstream-cli").grid(row=1, column=0, sticky="w", padx=(0, 8), pady=3)
    ttk.Entry(tools, textvariable=vgmstream_value).grid(row=1, column=1, sticky="ew", pady=3)

    def choose_tool(target: StringVar) -> None:
        chosen = filedialog.askopenfilename(
            title="Выберите исполняемый файл декодера",
            filetypes=(("Программы", "*.exe"), ("Все файлы", "*")),
        )
        if chosen:
            target.set(chosen)

    ttk.Button(tools, text="…", width=4, command=lambda: choose_tool(ffmpeg_value)).grid(
        row=0, column=2, padx=(6, 0), pady=3
    )
    ttk.Button(tools, text="…", width=4, command=lambda: choose_tool(vgmstream_value)).grid(
        row=1, column=2, padx=(6, 0), pady=3
    )

    notice = (
        "Один ресурс выбирайте кнопкой «Выбрать файл…», сразу много — кнопкой «Выбрать папку…». "
        "Ищутся стандартные аудиопотоки внутри .uasset/.uexp/.ubulk и других файлов. "
        "Зашифрованные .pak/.ucas, сырые UE-кодеки и данные, распиленные между файлами, "
        "эта утилита автоматически не декодирует — сначала распакуйте их подходящим инструментом."
    )
    ttk.Label(outer, text=notice, wraplength=870, justify="left", foreground="#555555").grid(
        row=7, column=0, columnspan=3, sticky="new", pady=(0, 9)
    )

    log_frame = ttk.LabelFrame(outer, text="Журнал", padding=6)
    log_frame.grid(row=8, column=0, columnspan=3, sticky="nsew")
    outer.rowconfigure(8, weight=1)
    log_frame.rowconfigure(0, weight=1)
    log_frame.columnconfigure(0, weight=1)
    log_box = ScrolledText(log_frame, height=14, wrap="word", state="disabled")
    log_box.grid(row=0, column=0, sticky="nsew")

    def add_log(text: str) -> None:
        log_box.configure(state="normal")
        log_box.insert(END, text + "\n")
        log_box.see(END)
        log_box.configure(state="disabled")

    bottom = ttk.Frame(outer)
    bottom.grid(row=9, column=0, columnspan=3, sticky="ew", pady=(10, 0))
    bottom.columnconfigure(1, weight=1)
    start_button = ttk.Button(bottom, text="Найти и извлечь", width=20)
    start_button.grid(row=0, column=0, sticky="w")
    ttk.Label(bottom, textvariable=status_value).grid(row=0, column=1, sticky="w", padx=12)

    def start() -> None:
        raw_source = input_value.get().strip()
        raw_output = output_value.get().strip()
        if not raw_source:
            messagebox.showerror("Нет исходников", "Выберите файл или папку с Unreal-ресурсами.", parent=root)
            return
        if not raw_output:
            messagebox.showerror("Нет папки результата", "Укажите папку для сохранения.", parent=root)
            return
        source = Path(raw_source).expanduser()
        if not source.exists():
            messagebox.showerror("Путь не найден", str(source), parent=root)
            return
        config = ExtractionConfig(
            source=source,
            output=Path(raw_output).expanduser(),
            output_format=_FORMAT_LABELS.get(format_value.get(), "original"),
            scan_all=scan_all_value.get() == "1",
            ffmpeg=ffmpeg_value.get().strip() or shutil.which("ffmpeg"),
            vgmstream=vgmstream_value.get().strip() or shutil.which("vgmstream-cli"),
        )
        start_button.configure(state="disabled")
        status_value.set("Сканирование…")
        add_log("— Запуск —")

        def worker() -> None:
            try:
                summary = run_extraction(config, log=lambda line: messages.put(("log", line)))
                messages.put(("done", summary))
            except Exception as error:  # keep the GUI responsive and show the failure
                messages.put(("fatal", error))

        threading.Thread(target=worker, name="ue-audio-extractor", daemon=True).start()

    start_button.configure(command=start)

    def open_output() -> None:
        path = Path(output_value.get()).expanduser()
        path.mkdir(parents=True, exist_ok=True)
        try:
            if os.name == "nt":
                os.startfile(str(path))  # type: ignore[attr-defined]
            elif sys.platform == "darwin":
                subprocess.Popen(["open", str(path)])
            else:
                subprocess.Popen(["xdg-open", str(path)])
        except (OSError, subprocess.SubprocessError) as error:
            messagebox.showerror("Не удалось открыть папку", str(error), parent=root)

    ttk.Button(bottom, text="Открыть папку результата", command=open_output).grid(row=0, column=2, sticky="e")

    def process_messages() -> None:
        while True:
            try:
                kind, payload = messages.get_nowait()
            except queue.Empty:
                break
            if kind == "log":
                add_log(str(payload))
            elif kind == "done":
                summary: ExtractionSummary = payload
                start_button.configure(state="normal")
                status_value.set("Готово: {} сохранено, {} ошибок".format(summary.extracted, summary.failed))
            elif kind == "fatal":
                start_button.configure(state="normal")
                status_value.set("Завершено с ошибкой")
                add_log("Критическая ошибка: {}".format(payload))
                messagebox.showerror("Ошибка извлечения", str(payload), parent=root)
        root.after(120, process_messages)

    root.after(120, process_messages)
    root.mainloop()
