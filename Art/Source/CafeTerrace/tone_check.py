"""Cafe Terrace: measure every surface's colour in the Unity check shots against the same surface in the target
(effect) images, and retune the paint so they match.

    python3 Art/Source/CafeTerrace/tone_check.py --targets <dir with the target images> --shots <dir with Unity's shots>
        [--apply]

For each surface: the median colour of a patch in the target and of the matching patch in the Unity shot (patches
picked by hand on gridded copies of both; the check cameras are fixed, so they stay put). It prints both and, with
--apply, multiplies that surface's entry in cafe_layout.json "tone" by target / Unity (in linear light, damped), and
does the same for the sky's two colours. Then: make_unity_export.py, Build, the shots again, and this again, until the
table agrees. The targets are raw inputs and stay out of the repo.
"""
import argparse
import colorsys
import json
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
K = 1.536   # patches were read off 1000-px-wide gridded views of the 1536-px images

# surface: (target image, patch), (Unity shot, patch); patches are x0, y0, x1, y1 on the 1000-px views
PATCHES = {
    "house":   (("07_right.webp", (780, 150, 880, 280)), ("elev_right.png", (460, 230, 550, 330))),
    "shutter": (("07_right.webp", (485, 160, 510, 250)), ("elev_right.png", (690, 125, 715, 205))),
    "door":    (("07_right.webp", (480, 350, 540, 500)), ("elev_right.png", (575, 380, 645, 530))),
    "glow":    (("07_right.webp", (525, 165, 570, 245)), ("elev_right.png", (380, 365, 445, 480))),
    "pave":    (("07_right.webp", (300, 545, 900, 570)), ("elev_right.png", (290, 590, 890, 640))),
    "cafe":    (("09_left.webp", (790, 250, 850, 480)), ("elev_left.png", (690, 400, 790, 580))),
    "cafe_upper": (("09_left.webp", (60, 150, 130, 500)), ("elev_left.png", (10, 60, 190, 250))),
    "shop":    (("09_left.webp", (200, 150, 720, 180)), ("elev_left.png", (230, 285, 670, 310))),
    "sky":     (("03_overview.webp", (440, 150, 500, 200)), ("overview.png", (495, 117, 520, 150))),
    "house_far": (("03_overview.webp", (640, 230, 700, 300)), ("overview.png", (455, 163, 520, 215))),
    "cobble":  (("03_overview.webp", (650, 430, 820, 470)), ("overview.png", (530, 480, 690, 560))),
    "plank":   (("03_overview.webp", (120, 540, 175, 575)), ("overview.png", (280, 480, 330, 510))),
    "awning":  (("03_overview.webp", (200, 200, 400, 280)), ("overview.png", (320, 210, 430, 270))),
}
TUNED = ("house", "shutter", "door", "glow", "pave", "cafe", "shop", "cobble", "plank", "awning")   # layout "tone" entries


def lin(c):
    c = np.asarray(c, dtype=float) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_hex(linear):
    c = np.clip(linear, 0, 1)
    s = np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)
    return "#%02x%02x%02x" % tuple(int(round(v * 255)) for v in s)


def median(path, box):
    im = np.asarray(Image.open(path).convert("RGB")).astype(float)
    x0, y0, x1, y1 = [int(round(v * K)) for v in box]
    return np.median(im[y0:y1, x0:x1].reshape(-1, 3), axis=0)


def describe(rgb):
    h, s, v = colorsys.rgb_to_hsv(*(rgb / 255.0))
    return "#%02x%02x%02x h%3.0f s%.2f v%.2f" % (int(rgb[0]), int(rgb[1]), int(rgb[2]), h * 360, s, v)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--targets", required=True)
    ap.add_argument("--shots", required=True)
    ap.add_argument("--apply", action="store_true")
    a = ap.parse_args()
    path = os.path.join(HERE, "cafe_layout.json")
    with open(path) as f:
        layout = json.load(f)
    tone = layout.setdefault("tone", {})
    worst = 0.0
    for name, ((timg, tbox), (uimg, ubox)) in PATCHES.items():
        t, u = median(os.path.join(a.targets, timg), tbox), median(os.path.join(a.shots, uimg), ubox)
        ratio = (lin(t) + 0.004) / (lin(u) + 0.004)
        err = float(np.abs(t - u).max())
        worst = max(worst, err)
        print("%-11s target %s | unity %s | off by %3.0f" % (name, describe(t), describe(u), err))
        if not a.apply:
            continue
        step = np.clip(ratio, 0.4, 2.5) ** 0.85
        if name in TUNED:
            old = np.array(tone.get(name, [1.0, 1.0, 1.0]))
            tone[name] = [round(float(v), 4) for v in np.clip(old * step, 0.05, 12.0)]
        elif name == "sky":
            for key in ("sky", "sky_horizon"):
                layout["light"][key] = to_hex(lin(np.array([int(layout["light"][key][i:i + 2], 16) for i in (1, 3, 5)])) * step)
    print("WORST|%.0f" % worst)
    if a.apply:
        with open(path, "w") as f:
            json.dump(layout, f, indent=2, ensure_ascii=False)
            f.write("\n")
        print("APPLIED|%s" % path)


main()
