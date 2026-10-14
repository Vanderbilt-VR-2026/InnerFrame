#!/usr/bin/env python3
"""Which objects did a generator export change? (#44)

    python3 compare_exports.py OLD_DATA_DIR NEW_DATA_DIR

Both folders hold an export: room.json, room.bytes and Textures/. Lists the objects, materials and
textures that differ, so a PR can say exactly what changed and nobody's objects change by accident.
Plain Python 3, no packages needed. Exit code 0 when identical, 1 when different.

Typical use, before exporting from the generator:
    cp -R ../InnerFrameBedroomWeb/Data /tmp/data_before
    (export)
    python3 compare_exports.py /tmp/data_before ../InnerFrameBedroomWeb/Data
"""
import array
import hashlib
import json
import os
import sys


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
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    old_dir, new_dir = sys.argv[1], sys.argv[2]
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
