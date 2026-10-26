#!/usr/bin/env bash
# Stage source for Unity to import. Unity creates every .meta and serialized asset.
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
source_dir="$root/scripts/artwork-info"
target="$root/Assets/ArtworkInfo"
if [[ -e "$target" ]]; then
  echo 'Assets/ArtworkInfo already exists. Refusing to overwrite local work.' >&2
  exit 1
fi
mkdir -p "$target/Runtime" "$target/Editor"
cp "$source_dir/Runtime/ArtworkInfoDisplay.cs" "$source_dir/Runtime/ArtworkInfoLayout.cs" "$target/Runtime/"
cp "$source_dir/Editor/ArtworkInfoTools.cs" "$target/Editor/"
echo 'Source staged. Open Unity 6000.3.23f1 and let it import; do not commit until generated .meta files are present.'
echo 'Import TMP Essential Resources if needed, then use Tools > InnerFrame > Artwork Info > 1. Build Isolated Test Scene.'
