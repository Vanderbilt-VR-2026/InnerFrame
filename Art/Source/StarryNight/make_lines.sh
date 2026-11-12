#!/usr/bin/env bash
# make_lines.sh - Vincent's Starry Night lines as WAV files, spoken by Piper in his voice, the same voice and format
# as the Bedroom's lines (Art/Source/VanGogh/make_mirror_lines.sh): mono, 16-bit, 22,050 Hz.
# Both call Art/Source/Voice/piper_lines.sh, which sets the voice and installs Piper on first run.
# One file per line of lines.txt (an id, then the text): Assets/StarryNight/Audio/SN_<id>.wav.
# Then run Tools > InnerFrame > Starry Night > Build in Unity to import them and update SN_Lines.asset.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
"${here}/../Voice/piper_lines.sh" "${here}/lines.txt" "${here}/../../../Assets/StarryNight/Audio" "SN_"
