"""Put each check render next to its target image, one sheet per camera.

    python3 Art/Source/CafeTerrace/make_compare.py --targets <dir with the target images> --renders <dir> --out <dir>
        [--label "Blender blockout"]

The targets (ChatGPT images, the painting) are raw inputs and stay out of the repo; pass their folder.
Which render goes with which target comes from cafe_layout.json ("cameras": target).
"""
import argparse
import json
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
H = 900       # height of both halves
BAR = 44      # caption bar


def font(size):
    for path in ("/System/Library/Fonts/Helvetica.ttc", "/Library/Fonts/Arial.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def fit(img):
    return img.convert("RGB").resize((round(img.width * H / img.height), H), Image.LANCZOS)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--targets", required=True)
    ap.add_argument("--renders", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--label", default="Blender blockout")
    a = ap.parse_args()
    with open(os.path.join(HERE, "cafe_layout.json")) as f:
        cams = json.load(f)["cameras"]
    os.makedirs(a.out, exist_ok=True)
    f = font(24)
    for name, spec in cams.items():
        if name.startswith("_"):
            continue
        render_path = os.path.join(a.renders, name + ".png")
        if not os.path.exists(render_path):
            continue
        halves = []
        if spec.get("target"):
            halves.append(("target: " + spec["target"], fit(Image.open(os.path.join(a.targets, spec["target"])))))
        halves.append(("%s: %s" % (a.label, name), fit(Image.open(render_path))))
        gap = 12
        w = sum(h[1].width for h in halves) + gap * (len(halves) - 1)
        sheet = Image.new("RGB", (w, H + BAR), (24, 24, 28))
        d = ImageDraw.Draw(sheet)
        x = 0
        for caption, img in halves:
            sheet.paste(img, (x, BAR))
            d.text((x + 10, 8), caption, fill=(235, 235, 235), font=f)
            x += img.width + gap
        out = os.path.join(a.out, "compare_%s.jpg" % name)
        sheet.save(out, quality=88)
        print("COMPARE|%s" % out)


main()
