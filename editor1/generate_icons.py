#!/usr/bin/env python3
"""Rasterise the bundled Lucide SVGs into alpha masks for the native UI.
Requires resvg-py and Pillow. Run after editing an SVG; icons.h is committed.
"""
from pathlib import Path
from io import BytesIO
import resvg_py
from PIL import Image

root = Path(__file__).resolve().parent
names = ['plus', 'search', 'file-text', 'folder-open', 'download', 'pencil', 'trash', 'book-open', 'pen-line', 'check', 'editor-mark']
lines = ['/* Generated from the SVG icon assets by generate_icons.py. */', '#ifndef EDITOR_ICONS_H', '#define EDITOR_ICONS_H', 'enum { ' + ', '.join('ICON_' + x.upper().replace('-', '_') for x in names) + ' };', 'static const unsigned char icon_masks[][400] = {']
for name in names:
    source = (root / 'assets' / 'icons' / (name + '.svg')).read_bytes()
    png = resvg_py.svg_to_bytes(svg_string=source.decode("utf-8"), width=20, height=20)
    alpha = Image.open(BytesIO(png)).convert('RGBA').getchannel('A')
    vals = list(alpha.getdata())
    lines.append('  { /* ' + name + ' */')
    for row in range(20):
        lines.append('    ' + ','.join(str(v) for v in vals[row * 20:(row + 1) * 20]) + ',')
    lines.append('  },')
lines += ['};', '#endif', '']
(root / 'icons.h').write_text('\n'.join(lines))
