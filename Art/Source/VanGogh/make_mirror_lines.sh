#!/usr/bin/env bash
# make_mirror_lines.sh - Vincent's mirror-scene lines as WAV files, spoken by Piper in his voice
# (Art/Source/Voice/piper_lines.sh, which sets the voice and installs Piper on first run).
# One file per line of mirror_lines.txt (an id, then the text): Assets/VanGogh/Audio/VG_Mirror_<id>.wav.
# Then run Tools > InnerFrame > Van Gogh > Rebuild assets in Unity to import them and update the prefab.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
"${here}/../Voice/piper_lines.sh" "${here}/mirror_lines.txt" "${here}/../../../Assets/VanGogh/Audio" "VG_Mirror_"
