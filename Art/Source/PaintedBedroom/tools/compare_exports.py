#!/usr/bin/env python3
"""Which objects did a generator export change? (#44)

    python3 compare_exports.py OLD_DATA_DIR NEW_DATA_DIR
    python3 compare_exports.py --git REF [NEW_DATA_DIR]

The second form takes the "before" from git: the room data as committed at REF (a branch, tag or
commit, e.g. `origin/main`), against NEW_DATA_DIR (default: the game's copy in this checkout,
Assets/InnerFrameBedroomWeb/Data). Needs git and git-lfs; the LFS objects must be fetched.

Both folders hold an export: room.json, room.bytes and Textures/. Lists the objects, materials and
textures that differ, so a PR can say exactly what changed and nobody's objects change by accident.
Plain Python 3, no packages needed. Exit code 0 when identical, 1 when different.

Typical use, after exporting from Blender (blender/room_io.py), before committing:
    python3 compare_exports.py --git origin/main
"""
import array
import hashlib
import json
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
GAME_DATA = os.path.join(REPO, "Assets", "InnerFrameBedroomWeb", "Data")
GAME_DATA_GIT = "Assets/InnerFrameBedroomWeb/Data"


def from_git(ref):
    """Write the room data committed at `ref` to a temp folder (LFS pointers resolved) and return it."""
    out = tempfile.mkdtemp(prefix="room_" + ref.replace("/", "_") + "_")
    # before the data moved into Assets/ (up to #108) it lived in the source package
    for base in (GAME_DATA_GIT, "Art/Source/PaintedBedroom/InnerFrameBedroomWeb/Data"):
        names = subprocess.run(["git", "-C", REPO, "ls-tree", "-r", "--name-only", ref, base],
                               check=True, capture_output=True, text=True).stdout.split()
        if any(n.endswith("room.json") for n in names):
            break
    else:
        sys.exit(f"no room data at {ref}")
    for path in names:
        if path.endswith(".meta"):
            continue
        blob = subprocess.run(["git", "-C", REPO, "show", f"{ref}:{path}"], check=True, capture_output=True).stdout
        if blob.startswith(b"version https://git-lfs"):
            blob = subprocess.run(["git", "-C", REPO, "lfs", "smudge"], input=blob, check=True, capture_output=True).stdout
        dest = os.path.join(out, os.path.relpath(path, base))
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(dest, "wb") as f:
            f.write(blob)
    return out


def load(d):
    with open(os.path.join(d, "room.json"), encoding="utf8") as f:
        room = json.load(f)
    with open(os.path.join(d, "room.bytes"), "rb") as f:
        raw = f.read()
    F = array.array("f"); F.frombytes(raw[:room["floatCount"] * 4])
    I = array.array("i"); I.frombytes(raw[room["floatCount"] * 4:room["floatCount"] * 4 + room["intCount"] * 4])
    if sys.byteorder != "little":
        F.byteswap(); I.byteswap()
    return room, F, I


def mesh_digest(m, room, F, I):
    """Fingerprint of one mesh: its vertex data, triangles and material (by name, not index)."""
    n = m["vertexCount"]
    h = hashlib.sha1()
    for key, size in (("positions", 3), ("normals", 3), ("uvs", 2)):   # by their own offsets, like the importer
        h.update(F[m[key]:m[key] + size * n].tobytes())
    h.update(I[m["indices"]:m["indices"] + m["indexCount"]].tobytes())
    h.update(room["materials"][m["material"]]["name"].encode())
    return h.hexdigest()


def objects(room, F, I):
    return {o["name"]: (tuple(round(x, 6) for x in o["position"]),
                        {m["name"]: mesh_digest(m, room, F, I) for m in o["meshes"]})
            for o in room["objects"]}


SCENE_KEYS_SKIPPED = {"objects", "materials", "textures", "floatCount", "intCount"}


def scene_settings(room):
    """Everything else in room.json: markers (painter's eye, spawn), painter FOV and lens shift..."""
    return {k: v for k, v in room.items() if k not in SCENE_KEYS_SKIPPED}


def file_digest(path):
    with open(path, "rb") as f:
        return hashlib.sha1(f.read()).hexdigest()


def main():
    if len(sys.argv) >= 3 and sys.argv[1] == "--git":
        old_dir = from_git(sys.argv[2])
        new_dir = sys.argv[3] if len(sys.argv) > 3 else GAME_DATA
    elif len(sys.argv) == 3:
        old_dir, new_dir = sys.argv[1], sys.argv[2]
    else:
        sys.exit(__doc__)
    a, Fa, Ia = load(old_dir)
    b, Fb, Ib = load(new_dir)
    oa, ob = objects(a, Fa, Ia), objects(b, Fb, Ib)

    added = sorted(set(ob) - set(oa)); removed = sorted(set(oa) - set(ob))
    moved, reshaped = [], []
    for name in sorted(set(oa) & set(ob)):
        (pa, ma), (pb, mb) = oa[name], ob[name]
        if pa != pb:
            moved.append(name)
        changed = sorted(k for k in set(ma) | set(mb) if ma.get(k) != mb.get(k))
        if changed:
            reshaped.append((name, changed))

    mats_a = {m["name"]: m for m in a["materials"]}; mats_b = {m["name"]: m for m in b["materials"]}
    tex_name = lambda room, m: room["textures"][m["texture"]] if m["texture"] >= 0 else None
    mat_changed = sorted(n for n in set(mats_a) & set(mats_b)
                         if {k: v for k, v in mats_a[n].items() if k != "texture"} != {k: v for k, v in mats_b[n].items() if k != "texture"}
                         or tex_name(a, mats_a[n]) != tex_name(b, mats_b[n]))
    def tex(d):
        folder = os.path.join(d, "Textures")
        if not os.path.isdir(folder):
            return {}
        return {f: file_digest(os.path.join(folder, f)) for f in os.listdir(folder) if f.endswith(".png")}
    ta, tb = tex(old_dir), tex(new_dir)
    tex_changed = sorted(f for f in set(ta) & set(tb) if ta[f] != tb[f])

    sa, sb = scene_settings(a), scene_settings(b)
    scene_changed = sorted(k for k in set(sa) | set(sb) if sa.get(k) != sb.get(k))

    print(f"scene settings changed (markers, painter camera...): {scene_changed or '-'}")
    print(f"objects: {len(oa)} -> {len(ob)}")
    print(f"  added:   {added or '-'}")
    print(f"  removed: {removed or '-'}")
    print(f"  moved:   {moved or '-'}")
    print(f"  meshes changed: {len(reshaped)} object(s)")
    for name, meshes in reshaped:
        print(f"    {name}: {len(meshes)} mesh(es) {meshes[:6]}{' ...' if len(meshes) > 6 else ''}")
    print(f"materials: added {sorted(set(mats_b) - set(mats_a)) or '-'}, removed {sorted(set(mats_a) - set(mats_b)) or '-'}, changed {mat_changed or '-'}")
    print(f"textures:  added {sorted(set(tb) - set(ta)) or '-'}, removed {sorted(set(ta) - set(tb)) or '-'}, changed {len(tex_changed)} {tex_changed[:8]}{' ...' if len(tex_changed) > 8 else ''}")
    same = not (scene_changed or added or removed or moved or reshaped or mat_changed or tex_changed
                or set(ta) ^ set(tb) or set(mats_a) ^ set(mats_b))
    print("identical" if same else "different")
    sys.exit(0 if same else 1)


if __name__ == "__main__":
    main()
