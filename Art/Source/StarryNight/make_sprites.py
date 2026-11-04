"""Placeholder sprites for The Starry Night scene, drawn with Pillow and numpy (same output every run).

    /usr/bin/python3 -I make_sprites.py

Writes power-of-two PNGs to Assets/StarryNight/Textures/, then run Tools > InnerFrame > Starry Night > Build in Unity
(it sets the import settings):
  SN_StarCore.png      a star's bright core;  SN_StarHalo.png  its halo (soft concentric rings)
  SN_Moon.png          a crescent moon with a glow;  SN_Dot.png  a soft dot (the wind's dotted guide, village lights)
  SN_Flame.png         the lantern's flame;  SN_Mist.png  a soft fog card
  SN_Line.png          soft across, even along: the beam (LineRenderer)
The dialogue box and the hint are the shared VG_DialogueUI (Assets/VanGogh/Prefabs), with its own sprites.
"""
import math
import sys
from pathlib import Path

sys.path.append("/Users/cicisss/Library/Python/3.9/lib/python/site-packages")   # Pillow, numpy for /usr/bin/python3 -I
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parents[3] / "Assets/StarryNight/Textures"
SS = 4                                   # drawn at 4x, then downsampled: smooth edges
SEED = 1889


def save(img, name):
    OUT.mkdir(parents=True, exist_ok=True)
    img.save(OUT / name, optimize=True)
    print(name, img.size)


def radial(size, fn):
    y, x = np.mgrid[0:size, 0:size]
    r = np.hypot(x - (size - 1) / 2, y - (size - 1) / 2) / (size / 2)
    return fn(r)


def to_rgba(rgb, alpha):
    a = np.clip(alpha, 0, 1)
    arr = np.dstack([np.full(a.shape, c, dtype=np.float64) for c in rgb] + [a * 255])
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))


def star_core(size=128):
    a = radial(size, lambda r: np.clip(1 - r, 0, 1) ** 3 + 0.6 * np.exp(-(r / 0.12) ** 2))
    return to_rgba((255, 246, 214), a / a.max())


def star_halo(size=256):
    # soft concentric rings that fade outward, like the painted halos
    def f(r):
        rings = 0.55 + 0.45 * np.cos(r * 2 * math.pi * 3.2)
        return np.clip(1 - r, 0, 1) ** 1.6 * rings * np.exp(-(r / 0.75) ** 4)
    a = radial(size, f)
    return to_rgba((0xCA, 0xB2, 0x4F), a / a.max() * 0.85)


def moon(size=256):
    y, x = np.mgrid[0:size, 0:size].astype(np.float64)
    c = (size - 1) / 2
    r = size * 0.30
    disc = np.hypot(x - c, y - c) < r
    bite = np.hypot(x - c - r * 0.55, y - c + r * 0.25) < r * 0.95
    cres = (disc & ~bite).astype(np.float64)
    img = Image.fromarray((cres * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    glow = img.filter(ImageFilter.GaussianBlur(size * 0.08))
    a = np.maximum(np.asarray(img, dtype=np.float64) / 255, 0.55 * np.asarray(glow, dtype=np.float64) / 255)
    return to_rgba((0xF2, 0xD8, 0x6A), a)


def dot(size=64):
    return to_rgba((255, 255, 255), radial(size, lambda r: np.exp(-(r / 0.45) ** 2) * (r < 1)))


def flame(w=64, h=128):
    y, x = np.mgrid[0:h, 0:w].astype(np.float64)
    u = (x - (w - 1) / 2) / (w / 2)            # -1..1 across
    v = 1 - y / (h - 1)                        # 0 at the bottom, 1 at the tip
    width = np.clip(np.sin(np.clip(v, 0, 1) * math.pi) ** 0.8 * (1 - v) ** 0.35 + 0.0, 0, 1) * 0.9
    a = np.clip(1 - np.abs(u) / np.maximum(width, 1e-3), 0, 1) ** 1.5
    a *= np.clip(v * 6, 0, 1)
    hot = np.clip(1 - v * 1.4, 0, 1)
    rgb = np.dstack([np.full_like(v, 255), 150 + 90 * hot, 40 + 120 * hot ** 2])
    arr = np.dstack([rgb, a * 255])
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))


def mist(w=256, h=128):
    rng = np.random.default_rng(SEED)
    base = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(base)
    for _ in range(40):
        cx, cy = rng.uniform(0.1, 0.9) * w, rng.uniform(0.35, 0.75) * h
        r = rng.uniform(0.08, 0.2) * w
        d.ellipse([cx - r, cy - r * 0.6, cx + r, cy + r * 0.6], fill=int(rng.uniform(90, 200)))
    soft = np.asarray(base.filter(ImageFilter.GaussianBlur(w * 0.05)), dtype=np.float64) / 255
    y, x = np.mgrid[0:h, 0:w].astype(np.float64)
    fade = np.clip(np.minimum(x, w - 1 - x) / (w * 0.2), 0, 1) * np.clip(np.minimum(y, h - 1 - y) / (h * 0.25), 0, 1)
    return to_rgba((0xC8, 0xD2, 0xF0), np.clip(soft * 1.4, 0, 1) * fade)


def soft_line(w=64, h=32):
    y = np.mgrid[0:h, 0:w][0].astype(np.float64)
    v = (y - (h - 1) / 2) / (h / 2)
    return to_rgba((255, 255, 255), np.exp(-(v / 0.45) ** 2) * (np.abs(v) < 1))


save(star_core(), "SN_StarCore.png")
save(star_halo(), "SN_StarHalo.png")
save(moon(), "SN_Moon.png")
save(dot(), "SN_Dot.png")
save(flame(), "SN_Flame.png")
save(mist(), "SN_Mist.png")
save(soft_line(), "SN_Line.png")
