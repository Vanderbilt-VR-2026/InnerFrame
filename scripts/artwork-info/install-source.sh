#!/usr/bin/env bash
# Copy C# only. Unity owns all metadata and serialized assets.
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
python3 - "$root" "${1:-}" <<'PY'
import hashlib,json,shutil,sys
from pathlib import Path
root=Path(sys.argv[1]); mode=sys.argv[2]
source=root/'scripts/artwork-info'; target=root/'Assets/ArtworkInfo'
if mode not in ('','--update-source'): sys.exit('Usage: install-source.sh [--update-source]')
if target.exists() and not mode: sys.exit('Assets/ArtworkInfo exists. Use --update-source for a guarded C# update; generated assets are never touched.')
manifest=target/'.source-hashes.json'
known=json.loads(manifest.read_text()) if manifest.exists() else {}
# Exact hashes of the PR #112 initial source, accepted for the first guarded upgrade.
legacy={'Runtime/ArtworkInfoDisplay.cs': '8446b7a878d306dcda324a5bf7f9b52e5ca709da0f84a10b12b6d676ece61d79', 'Runtime/ArtworkInfoLayout.cs': '41577b5638f39ee3021c7b318085bbc7f65b6c8999f6ec3eb5efb0cd8eb8c3c0', 'Editor/ArtworkInfoTools.cs': 'f49e68d73711f879f71fdcce1eb94c2f6554914174929fde22924743f0cc373d'}
files=sorted(source.glob('Runtime/*.cs'))+sorted(source.glob('Editor/*.cs'))
digest=lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
for file in files:
    relative=str(file.relative_to(source)); destination=target/relative
    if destination.exists() and digest(destination) not in (known.get(relative),legacy.get(relative),digest(file)):
        sys.exit('Local source differs: '+str(destination)+'. No files copied. Preserve/compare your changes before retrying.')
for file in files:
    destination=target/file.relative_to(source); destination.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(file,destination)
manifest.write_text(json.dumps({str(f.relative_to(source)):digest(f) for f in files},indent=2)+'\n')
print('C# source updated. Existing .meta, prefabs, scenes and materials preserved. Let Unity compile, then Tools > InnerFrame > Artwork Info > 1. Generate Standalone Museum Assets.')
PY
