"""The wind's path from Cici's red line (sky-painted-swirl-line.png in refs/3-starry-night/art-inputs, outside the repo).

    /usr/bin/python3 -I make_path.py <art-inputs dir>

The pure-red stroke (R > 200, G < 80, B < 80), its largest 8-connected component, thinned to a one-pixel centre line
(Zhang-Suen), ordered by geodesic distance from the end nearest the image border (the outer end) to the farthest
skeleton pixel (the swirl's centre). Writes swirl_line.json beside this script: (u, v) with u = x / width and v = y /
height from the TOP, outer end first; the centre line every 4 px and the 24 points resampled at equal lengths. The
image's layout matches sky-painted.png (cc-reports/2026-11-04-starry-night-art/raw/inputs.md), so the same (u, v) are
used on SN_SkyPainted.png. SN_Layout.guidePath holds them converted to (yaw, pitch) with the sky panel's mapping.
"""
import json
import sys
from collections import deque
from pathlib import Path

sys.path.append("/Users/cicisss/Library/Python/3.9/lib/python/site-packages")
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
a = np.asarray(Image.open(Path(sys.argv[1]) / "sky-painted-swirl-line.png").convert("RGB")).astype(int)
H, W = a.shape[:2]
red = (a[..., 0] > 200) & (a[..., 1] < 80) & (a[..., 2] < 80)

lab = np.zeros(red.shape, dtype=np.int32)
sizes = []
for y, x in zip(*np.nonzero(red)):
    if lab[y, x]:
        continue
    n = len(sizes) + 1
    q = deque([(y, x)]); lab[y, x] = n; c = 0
    while q:
        cy, cx = q.popleft(); c += 1
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                ny, nx = cy + dy, cx + dx
                if 0 <= ny < H and 0 <= nx < W and red[ny, nx] and not lab[ny, nx]:
                    lab[ny, nx] = n; q.append((ny, nx))
    sizes.append(c)
main = lab == (int(np.argmax(sizes)) + 1)

ys, xs = np.nonzero(main)
y0, y1, x0, x1 = max(ys.min() - 2, 0), ys.max() + 3, max(xs.min() - 2, 0), xs.max() + 3
img = main[y0:y1, x0:x1].astype(np.uint8)


def zhang_suen(img):
    img = img.copy()
    changed = True
    while changed:
        changed = False
        for step in (0, 1):
            P = np.pad(img, 1)
            p2, p3, p4, p5 = P[:-2, 1:-1], P[:-2, 2:], P[1:-1, 2:], P[2:, 2:]
            p6, p7, p8, p9 = P[2:, 1:-1], P[2:, :-2], P[1:-1, :-2], P[:-2, :-2]
            nb = [p2, p3, p4, p5, p6, p7, p8, p9]
            B = sum(n.astype(int) for n in nb)
            seq = nb + [p2]
            A = sum(((seq[i] == 0) & (seq[i + 1] == 1)).astype(int) for i in range(8))
            c = ((p2 * p4 * p6 == 0) & (p4 * p6 * p8 == 0)) if step == 0 else ((p2 * p4 * p8 == 0) & (p2 * p6 * p8 == 0))
            rm = (img == 1) & (B >= 2) & (B <= 6) & (A == 1) & c
            if rm.any():
                img[rm] = 0
                changed = True
    return img


sy, sx = np.nonzero(zhang_suen(img))
skel = set(zip(sy.tolist(), sx.tolist()))


def nbrs(p):
    y, x = p
    return [(y + dy, x + dx) for dy in (-1, 0, 1) for dx in (-1, 0, 1) if (dy or dx) and (y + dy, x + dx) in skel]


ends = sorted(p for p in skel if len(nbrs(p)) == 1)
outer = min(ends, key=lambda p: min(p[1] + x0, W - 1 - p[1] - x0, p[0] + y0, H - 1 - p[0] - y0))
dist, parent = {outer: 0}, {outer: None}
q = deque([outer])
while q:
    p = q.popleft()
    for n in sorted(nbrs(p)):
        if n not in dist:
            dist[n] = dist[p] + 1; parent[n] = p; q.append(n)
inner = max(sorted(dist), key=dist.get)
path = []
p = inner
while p is not None:
    path.append(p); p = parent[p]
path.reverse()
P = np.array([(x + x0, y + y0) for y, x in path], dtype=np.float64)
s = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])


def resample(n):
    t = np.linspace(0, s[-1], n)
    return np.stack([np.interp(t, s, P[:, 0]), np.interp(t, s, P[:, 1])], axis=1)


uv = lambda q: [round(float((q[0] + 0.5) / W), 5), round(float((q[1] + 0.5) / H), 5)]
out = {"source": "sky-painted-swirl-line.png", "image_size": [W, H], "red_pixels": int(red.sum()),
       "components": len(sizes), "centre_line_length_px": round(float(s[-1]), 1),
       "note": "(u, v): u = x / width, v = y / height from the TOP; outer end first, the swirl's centre last",
       "points24_uv": [uv(q) for q in resample(24)],
       "centre_line_uv_every_4px": [uv(q) for q in resample(int(s[-1] // 4) + 1)]}
json.dump(out, open(HERE / "swirl_line.json", "w"), indent=1)
print("PATH", out["red_pixels"], out["components"], out["centre_line_length_px"], out["points24_uv"][0], out["points24_uv"][-1])
