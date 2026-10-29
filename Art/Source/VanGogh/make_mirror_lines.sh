#!/usr/bin/env bash
# make_mirror_lines.sh - Vincent's mirror-scene lines as WAV files, with macOS `say` (voice "Daniel").
# One file per line of mirror_lines.txt (an id, then the text): Assets/VanGogh/Audio/VG_Mirror_<id>.wav.
# Then run Tools > InnerFrame > Van Gogh > Rebuild assets in Unity to import them and update the prefab.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${here}/../../../Assets/VanGogh/Audio"
mkdir -p "${out}"
while read -r id text; do
  [[ -z "${id}" || "${id}" == \#* ]] && continue
  say -v Daniel -o "${out}/VG_Mirror_${id}.wav" --data-format=LEI16@22050 "${text}"
  echo "VG_Mirror_${id}.wav"
done < "${here}/mirror_lines.txt"
