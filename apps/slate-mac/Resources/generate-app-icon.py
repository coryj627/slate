#!/usr/bin/env python3
# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Generate the Slate app-icon .icns (#875). This is a PLACEHOLDER mark —
# a slate-gradient rounded square with a light "note card" glyph — meant
# to clear the HIG "no generic icon" gap (app-icons.md:16) before any
# distributed build. Swap in real brand art by replacing AppIcon.icns
# (or editing this generator and re-running it); the build wiring in
# scripts/build-mac-app.sh consumes AppIcon.icns unchanged.
#
# Font-free by design (draws geometry only) so it reproduces on any box
# without bundling a typeface. Requires Pillow, iconutil, sips.
#
# Run:  python3 apps/slate-mac/Resources/generate-app-icon.py

import os
import subprocess
import tempfile

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_ICNS = os.path.join(HERE, "AppIcon.icns")

S = 1024  # master canvas
MARGIN = 92  # macOS icons sit in a rounded square inside a transparent margin
RADIUS = 228  # ~squircle corner radius for the 840px inner square

# Slate palette (APCA-safe light mark on a dark slate ground).
SLATE_TOP = (60, 71, 87)     # #3C4757
SLATE_BOTTOM = (33, 40, 51)  # #212833
CARD = (237, 239, 242)       # #EDEFF2 off-white
LINE = (138, 149, 165)       # #8A95A5 mid-slate "text"
ACCENT = (94, 129, 172)      # #5E81AC slate-blue accent bar


def rounded_mask(size, radius):
    m = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)
    return m


def vertical_gradient(size, top, bottom):
    g = Image.new("RGB", (1, size))
    for y in range(size):
        t = y / (size - 1)
        g.putpixel(
            (0, y),
            tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3)),
        )
    return g.resize((size, size))


def build_master():
    icon = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    inner = S - 2 * MARGIN

    # Rounded-square slate ground.
    ground = vertical_gradient(inner, SLATE_TOP, SLATE_BOTTOM).convert("RGBA")
    ground.putalpha(rounded_mask(inner, RADIUS))
    icon.paste(ground, (MARGIN, MARGIN), ground)

    d = ImageDraw.Draw(icon)

    # Centered "note card": a light rounded rect with an accent top bar and
    # three text lines. Reads as a notes/knowledge app and stays legible
    # when downscaled to 16px.
    cw, ch = int(inner * 0.52), int(inner * 0.60)
    cx = (S - cw) // 2
    cy = (S - ch) // 2
    card_r = int(cw * 0.14)
    d.rounded_rectangle([cx, cy, cx + cw, cy + ch], radius=card_r, fill=CARD)

    # Accent bar across the top of the card.
    bar_h = int(ch * 0.13)
    d.rounded_rectangle(
        [cx, cy, cx + cw, cy + bar_h + card_r],
        radius=card_r,
        fill=ACCENT,
    )
    d.rectangle([cx, cy + bar_h, cx + cw, cy + bar_h + card_r], fill=CARD)
    d.rectangle([cx, cy + bar_h - card_r, cx + cw, cy + bar_h], fill=ACCENT)

    # Text lines.
    line_h = int(ch * 0.055)
    gap = int(ch * 0.13)
    lx = cx + int(cw * 0.16)
    ly = cy + bar_h + int(ch * 0.22)
    widths = [0.68, 0.68, 0.44]
    for w in widths:
        d.rounded_rectangle(
            [lx, ly, lx + int(cw * w), ly + line_h],
            radius=line_h // 2,
            fill=LINE,
        )
        ly += gap

    return icon


def main():
    master = build_master()
    sizes = [16, 32, 64, 128, 256, 512, 1024]
    names = {
        16: ["icon_16x16.png"],
        32: ["icon_16x16@2x.png", "icon_32x32.png"],
        64: ["icon_32x32@2x.png"],
        128: ["icon_128x128.png"],
        256: ["icon_128x128@2x.png", "icon_256x256.png"],
        512: ["icon_256x256@2x.png", "icon_512x512.png"],
        1024: ["icon_512x512@2x.png"],
    }
    with tempfile.TemporaryDirectory() as tmp:
        iconset = os.path.join(tmp, "AppIcon.iconset")
        os.makedirs(iconset)
        for size in sizes:
            scaled = master.resize((size, size), Image.LANCZOS)
            for name in names[size]:
                scaled.save(os.path.join(iconset, name))
        subprocess.run(
            ["iconutil", "-c", "icns", iconset, "-o", OUT_ICNS], check=True
        )
    print(f"wrote {OUT_ICNS}")


if __name__ == "__main__":
    main()
