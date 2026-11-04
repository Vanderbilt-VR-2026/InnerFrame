#!/usr/bin/env bash
# make_lines.sh - Vincent's Starry Night lines as WAV files, with macOS `say` (voice "Daniel"), the same voice and
# format as the Bedroom's lines (Art/Source/VanGogh/make_mirror_lines.sh): mono, 16-bit, 22,050 Hz.
# One file per line of lines.txt (an id, then the text): Assets/StarryNight/Audio/SN_<id>.wav.
# Then run Tools > InnerFrame > Starry Night > Build in Unity to import them and update SN_Lines.asset.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${here}/../../../Assets/StarryNight/Audio"
mkdir -p "${out}"
while read -r id text; do
  [[ -z "${id}" || "${id}" == \#* ]] && continue
  say -v Daniel -o "${out}/SN_${id}.wav" --data-format=LEI16@22050 "${text}"
  echo "SN_${id}.wav"
done < "${here}/lines.txt"
