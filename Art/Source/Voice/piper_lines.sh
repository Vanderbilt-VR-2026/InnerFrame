#!/usr/bin/env bash
# piper_lines.sh - speaks a lines file (an id, then the text, one per line) as WAV files with Piper, in Vincent's voice.
# Shared by Art/Source/VanGogh/make_mirror_lines.sh and Art/Source/StarryNight/make_lines.sh.
#
#   piper_lines.sh <lines.txt> <output dir> <file prefix>
#   -> <output dir>/<file prefix><id>.wav, mono, 16-bit, 22,050 Hz (the format Unity already imports for his lines)
#
# Vincent's voice is VOICE below. The same voice speaks his live answers on the headset (#157), so change both together.
# The first run installs Piper (pip package piper-tts, pinned) into ~/.cache/innerframe-piper and downloads the voice
# from the rhasspy/piper-voices repo on Hugging Face (pinned revision, checked by SHA-256). Nothing it downloads is
# committed. Needs python3 (3.9 or later) and curl. Works on macOS, Linux and Git Bash on Windows.
set -euo pipefail

VOICE="en_GB-northern_english_male-medium"
VOICE_DIR="en/en_GB/northern_english_male/medium"
VOICE_SHA256="57a219ae8e638873db7d18893304be5069c42868f392bb95c3ff17f0690d0689"
VOICE_JSON_SHA256="69557ed3d974463453e9b0c09dd99a7ed0e52b8b87b64b357dbeeb2540a97d47"
VOICES_REVISION="cf910fb59f1d79ab3b625a63993a108751c6c25d"
PIPER_VERSION="1.8.0"

if [[ $# -ne 3 ]]; then
  echo "usage: $0 <lines.txt> <output dir> <file prefix>" >&2
  exit 2
fi
lines="$1"
out="$2"
prefix="$3"

# A short home for Piper: its espeak-ng phonemizer can't read its data from a path longer than about 160 characters.
home="${HOME}/.cache/innerframe-piper"
venv="${home}/venv"
mkdir -p "${home}/voices" "${out}"

if [[ -x "${venv}/bin/python" ]]; then py="${venv}/bin/python"; else py="${venv}/Scripts/python.exe"; fi
if ! "${py}" -c "import piper" 2>/dev/null || [[ "$("${py}" -m pip show piper-tts 2>/dev/null | sed -n 's/^Version: //p')" != "${PIPER_VERSION}" ]]; then
  echo "Installing Piper ${PIPER_VERSION} into ${venv}..."
  python3 -m venv "${venv}"
  if [[ -x "${venv}/bin/python" ]]; then py="${venv}/bin/python"; else py="${venv}/Scripts/python.exe"; fi
  "${py}" -m pip install --quiet "piper-tts==${PIPER_VERSION}"
fi

sha256() {
  if command -v shasum >/dev/null; then shasum -a 256 "$1" | cut -d' ' -f1; else sha256sum "$1" | cut -d' ' -f1; fi
}
fetch() {   # fetch <file name> <expected sha256>
  local file="${home}/voices/$1"
  if [[ ! -f "${file}" || "$(sha256 "${file}")" != "$2" ]]; then
    echo "Downloading $1..."
    curl -sfL -o "${file}.part" "https://huggingface.co/rhasspy/piper-voices/resolve/${VOICES_REVISION}/${VOICE_DIR}/$1"
    if [[ "$(sha256 "${file}.part")" != "$2" ]]; then
      rm -f "${file}.part"
      echo "$1: checksum does not match; not using it" >&2
      exit 1
    fi
    mv "${file}.part" "${file}"
  fi
}
fetch "${VOICE}.onnx" "${VOICE_SHA256}"
fetch "${VOICE}.onnx.json" "${VOICE_JSON_SHA256}"

# The espeak-ng data Piper ships, copied to the short home (see above).
data="${home}/espeak-ng-data-${PIPER_VERSION}"
if [[ ! -d "${data}" ]]; then
  cp -R "$("${py}" -c 'import piper, os; print(os.path.join(os.path.dirname(piper.__file__), "espeak-ng-data"))')" "${data}"
fi

"${py}" -I - "${home}/voices/${VOICE}.onnx" "${data}" "${lines}" "${out}" "${prefix}" <<'EOF'
import sys, wave
from piper import PiperVoice

model, data, lines, out, prefix = sys.argv[1:]
voice = PiperVoice.load(model, espeak_data_dir=data)
with open(lines, encoding="utf-8") as f:
    for row in f:
        row = row.strip()
        if not row or row.startswith("#"):
            continue
        line_id, _, text = row.partition(" ")
        name = f"{prefix}{line_id}.wav"
        with wave.open(f"{out}/{name}", "wb") as w:
            voice.synthesize_wav(text.strip(), w)
        print(name)
EOF
