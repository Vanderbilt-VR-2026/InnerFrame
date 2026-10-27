#!/usr/bin/env bash
# Guarded source/design installation. Unity owns all metadata and serialized assets.
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
python3 - "$root" "$@" <<'PY'
import argparse,hashlib,json,sys,zipfile
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument('root')
parser.add_argument('--update-source',action='store_true')
parser.add_argument('--design-assets',help='Supplied InnerFrameFigmaAssets.zip, or its extracted folder')
args=parser.parse_args()
root=Path(args.root); source=root/'scripts/artwork-info'; target=root/'Assets/ArtworkInfo'
if target.exists() and not args.update_source:
    sys.exit('Assets/ArtworkInfo exists. Use --update-source; generated assets are never touched.')
manifest=target/'.source-hashes.json'
known=json.loads(manifest.read_text()) if manifest.exists() else {}
legacy={'Runtime/ArtworkInfoDisplay.cs': '8446b7a878d306dcda324a5bf7f9b52e5ca709da0f84a10b12b6d676ece61d79', 'Runtime/ArtworkInfoLayout.cs': '41577b5638f39ee3021c7b318085bbc7f65b6c8999f6ec3eb5efb0cd8eb8c3c0', 'Editor/ArtworkInfoTools.cs': 'f49e68d73711f879f71fdcce1eb94c2f6554914174929fde22924743f0cc373d'}
files=sorted(source.glob('Runtime/*.cs'))+sorted(source.glob('Editor/*.cs'))
payload={str(f.relative_to(source)):f.read_bytes() for f in files}
digest=lambda data:hashlib.sha256(data).hexdigest()
expected=json.loads((source/'design-assets.json').read_text())
bundle=Path(args.design_assets).expanduser() if args.design_assets else None
archive=zipfile.ZipFile(bundle) if bundle and bundle.is_file() else None
try:
    for name,sha in expected.items():
        relative='Design/'+name
        if archive:
            # Read only named manifest members; no extractall or user-controlled output paths.
            info=archive.getinfo(relative)
            if info.file_size>8*1024*1024: sys.exit('Oversize design input: '+name)
            data=archive.read(relative)
        elif bundle:
            data=(bundle/relative).read_bytes()
        elif (target/relative).exists():
            data=(target/relative).read_bytes()
        else:
            sys.exit('Missing design bundle. Download InnerFrameFigmaAssets.zip, then pass --design-assets ~/Downloads/InnerFrameFigmaAssets.zip. No files copied.')
        if digest(data)!=sha: sys.exit('Design asset hash mismatch: '+name+'. No files copied.')
        payload[relative]=data
finally:
    if archive: archive.close()
# Preflight EVERY destination before any mutation, including a previous source-only install.
for relative,data in payload.items():
    destination=target/relative
    if destination.is_symlink(): sys.exit('Refusing symlink destination: '+str(destination))
    if any(p.is_symlink() for p in destination.parents if p!=root.parent):
        sys.exit('Refusing symlink parent: '+str(destination))
    if destination.exists() and digest(destination.read_bytes()) not in (known.get(relative),legacy.get(relative),digest(data)):
        sys.exit('Local source/design differs: '+str(destination)+'. No files copied. Preserve/compare your changes before retrying.')
for relative,data in payload.items():
    destination=target/relative; destination.parent.mkdir(parents=True,exist_ok=True)
    destination.write_bytes(data)
manifest.write_text(json.dumps({name:digest(data) for name,data in payload.items()},indent=2)+'\n')
print('Source and exact Figma inputs installed. Existing .meta, prefabs, scenes and materials preserved. Let Unity compile, then Tools > InnerFrame > Artwork Info > 1. Generate Standalone Museum Assets.')
PY
