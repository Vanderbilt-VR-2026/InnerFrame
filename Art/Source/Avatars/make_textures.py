"""Player avatars: each avatar's base colour, out of its Tripo GLB (the same texture Mixamo's FBX embeds), at
2048 x 2048 for the Quest, as Assets/Script/Network/Avatars/Textures/<name>_BaseColor.jpg. PlayerAvatarBuilder points
the avatar's material at it, so Unity never extracts the FBX's embedded 4096 copy.

    /usr/bin/python3 -I make_textures.py        (macOS: resizes with the built-in `sips`)
"""
import json
import struct
import subprocess
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
OUT = REPO / "Assets/Script/Network/Avatars/Textures"
SIZE = 2048
AVATARS = {
    "boy worker 3d model.glb": "Avatar_BoyWorker",
}


def base_colour(glb: Path) -> bytes:
    data = glb.read_bytes()
    json_len = struct.unpack_from("<I", data, 12)[0]
    gltf = json.loads(data[20:20 + json_len])
    material = gltf["materials"][0]["pbrMetallicRoughness"]["baseColorTexture"]["index"]
    image = gltf["images"][gltf["textures"][material]["source"]]
    view = gltf["bufferViews"][image["bufferView"]]
    start = 20 + json_len + 8 + view.get("byteOffset", 0)
    return data[start:start + view["byteLength"]]


OUT.mkdir(parents=True, exist_ok=True)
for src, name in AVATARS.items():
    jpg = base_colour(HERE / src)
    with tempfile.NamedTemporaryFile(suffix=".jpg") as tmp:
        tmp.write(jpg)
        tmp.flush()
        out = OUT / f"{name}_BaseColor.jpg"
        subprocess.run(["sips", "-z", str(SIZE), str(SIZE), "-s", "formatOptions", "90", tmp.name, "--out", str(out)],
                       check=True, capture_output=True)
    print(f"[avatars] {out.relative_to(REPO)}: {out.stat().st_size / 1e6:.1f} MB")
