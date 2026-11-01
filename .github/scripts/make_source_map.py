#!/usr/bin/env python3
"""Собирает карту исходников из распакованных jadx/apktool-каталогов.

Кладёт в reports/sample-sources: манифест, ресурсы, список классов, карту
вызовов API и строковых констант. Исполняемый код (java/smali) не копируется —
он остаётся только в артефакте Actions.
"""

from __future__ import annotations

import os
import re
import shutil
import sys
import traceback

API_RE = re.compile(r"(L[a-zA-Z0-9_/$]+;->[a-zA-Z0-9_$]+\()")
STR_RE = re.compile(r'"(https?://[^"]+|assets/[^"]+|[A-Za-z0-9_/]+\.(?:bin|dat|cache|idx|dex|apk))"')
SMALI_STR_RE = re.compile(r'const-string(?:/jumbo)?\s+v\d+,\s*"(https?://[^"]+|assets/[^"]+|[A-Za-z0-9_/]+\.(?:bin|dat|cache|idx|dex|apk))"')


def collect(root: str, dest_root: str, log) -> None:
    apks = [d for d in sorted(os.listdir(root)) if os.path.isdir(os.path.join(root, d))]
    readme = [
        "# Исходники образца fakevpn.zip",
        "",
        "> **Внимание.** Это автоматический разбор вредоносного кода.",
        "> Полные java/smali-исходники лежат в артефакте Actions",
        "> `malware-sources-do-not-run` (хранится 7 дней) и намеренно не",
        "> коммитятся: это работоспособный банковский троян.",
        "> Здесь только манифест, ресурсы и карта классов — без тела методов.",
        "",
    ]

    for apk in apks:
        src = os.path.join(root, apk)
        dest = os.path.join(dest_root, apk)
        os.makedirs(dest, exist_ok=True)
        log(f"== {apk}")

        summary = os.path.join(src, "SUMMARY.txt")
        text = ""
        if os.path.isfile(summary):
            with open(summary, encoding="utf-8", errors="replace") as fh:
                text = fh.read()
            shutil.copy2(summary, dest)
        readme += [f"## {apk}", "", "```", text.strip(), "```", ""]

        man = os.path.join(src, "apktool", "AndroidManifest.xml")
        if os.path.isfile(man):
            shutil.copy2(man, os.path.join(dest, "AndroidManifest.xml"))
            log(f"   манифест скопирован ({os.path.getsize(man)} Б)")
        else:
            log("   манифест не найден")

        res = os.path.join(src, "apktool", "res")
        if os.path.isdir(res):
            target = os.path.join(dest, "res")
            if os.path.isdir(target):
                shutil.rmtree(target)
            shutil.copytree(res, target)
            count = sum(len(f) for _, _, f in os.walk(target))
            log(f"   ресурсы скопированы: {count} файлов")

        # карта классов
        classes = []
        java_root = os.path.join(src, "java")
        for base, _, files in os.walk(java_root):
            for f in files:
                if f.endswith(".java"):
                    classes.append(os.path.relpath(os.path.join(base, f), java_root))
        smali = []
        apk_root = os.path.join(src, "apktool")
        for base, _, files in os.walk(apk_root):
            for f in files:
                if f.endswith(".smali"):
                    smali.append(os.path.relpath(os.path.join(base, f), apk_root))

        with open(os.path.join(dest, "class-map.txt"), "w", encoding="utf-8") as fh:
            fh.write("Java-классы (jadx):\n")
            fh.writelines(f"  {c}\n" for c in sorted(classes)[:500])
            fh.write("\nSmali (apktool):\n")
            fh.writelines(f"  {c}\n" for c in sorted(smali)[:500])
        log(f"   классов java: {len(classes)}, smali: {len(smali)}")

        # вызовы API и строки
        api: dict[str, int] = {}
        strings: set[str] = set()
        # вызовы API надёжнее брать из smali: там сохраняются дескрипторы методов
        for base, _, files in os.walk(apk_root):
            for f in files:
                if not f.endswith(".smali"):
                    continue
                try:
                    with open(os.path.join(base, f), encoding="utf-8", errors="replace") as fh:
                        body = fh.read()
                except OSError:
                    continue
                for m in API_RE.finditer(body):
                    api[m.group(1)] = api.get(m.group(1), 0) + 1
                for m in SMALI_STR_RE.finditer(body):
                    strings.add(m.group(1))

        for base, _, files in os.walk(java_root):
            for f in files:
                if not f.endswith(".java"):
                    continue
                try:
                    with open(os.path.join(base, f), encoding="utf-8", errors="replace") as fh:
                        body = fh.read()
                except OSError:
                    continue
                for m in API_RE.finditer(body):
                    api[m.group(1)] = api.get(m.group(1), 0) + 1
                for m in STR_RE.finditer(body):
                    strings.add(m.group(1))

        with open(os.path.join(dest, "api-usage.txt"), "w", encoding="utf-8") as fh:
            fh.write("Вызовы Android API (по частоте):\n")
            for call, n in sorted(api.items(), key=lambda kv: -kv[1])[:250]:
                fh.write(f"  {n:5d}  {call}\n")
            fh.write("\nСтроковые константы (URL, assets, имена файлов):\n")
            for s in sorted(strings)[:250]:
                fh.write(f"  {s}\n")
        log(f"   уникальных API-вызовов: {len(api)}, строковых констант: {len(strings)}")

        readme.append(f"- `{apk}/AndroidManifest.xml`, `{apk}/res/`, "
                      f"`{apk}/class-map.txt`, `{apk}/api-usage.txt`")
        readme.append("")

    readme.append("Полный архив кода: Actions → *Dump sources of the sample* → "
                  "Artifacts → `malware-sources-do-not-run`.")
    with open(os.path.join(dest_root, "README.md"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(readme) + "\n")


def main() -> int:
    if len(sys.argv) < 3:
        print("usage: make_source_map.py <sources_dir> <dest_dir>")
        return 64
    root, dest_root = sys.argv[1], sys.argv[2]
    os.makedirs(dest_root, exist_ok=True)
    log_path = os.path.join(dest_root, "_dump.log")
    with open(log_path, "w", encoding="utf-8") as log_fh:
        def log(msg: str) -> None:
            print(msg)
            log_fh.write(msg + "\n")
            log_fh.flush()

        try:
            collect(root, dest_root, log)
            log("готово")
            return 0
        except Exception:  # noqa: BLE001
            log("ОШИБКА:\n" + traceback.format_exc())
            return 1


if __name__ == "__main__":
    sys.exit(main())
