#!/usr/bin/env bash
# piper_lines.sh - speaks a lines file (an id, then the text, one per line) as WAV files with Piper, in Vincent's voice.
# Shared by Art/Source/VanGogh/make_mirror_lines.sh and Art/Source/StarryNight/make_lines.sh.
#
#   piper_lines.sh <lines.txt> <output dir> <file prefix>
#   -> <output dir>/<file prefix><id>.wav, mono, 16-bit, 22,050 Hz (the format Unity already imports for his lines)
#
# Vincent's voice is VOICE below. The same voice speaks his live answers on the headset (#157; there in its int8
# version, Assets/StreamingAssets/VincentVoice), so change both together.
# The first run installs Piper (pip package piper-tts, pinned) into ~/.cache/innerframe-piper and downloads the voice
# from the rhasspy/piper-voices repo on Hugging Face (pinned revision, checked by SHA-256). Nothing it downloads is
# committed. Needs python3 (3.9 or later) and curl. Works on macOS, Linux and Git Bash on Windows.
set -euo pipefail

VOICE="en_US-norman-medium"
VOICE_DIR="en/en_US/norman/medium"
VOICE_SHA256="b9739443232a80a59c7d18810dd856899bf16a7964725f5ab81ea49b1351cb71"
VOICE_JSON_SHA256="6c2db7f558a4a8deb9fe822583c1c5105f6c4e834dd0f9de8ad17a888ee9fe1d"
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
if (( ${#home} > 120 )); then
  echo "${home} is too long a path for Piper's espeak-ng (about 160 characters with its data): set HOME shorter" >&2
  exit 1
fi
mkdir -p "${home}/voices" "${out}"

# Python 3: `python3` on macOS and Linux; on Windows (Git Bash) often only `python` or `py -3`.
if command -v python3 >/dev/null && python3 -c 'import sys' 2>/dev/null; then system_py=(python3)
elif command -v python >/dev/null && python -c 'import sys; sys.exit(sys.version_info < (3, 9))' 2>/dev/null; then system_py=(python)
elif command -v py >/dev/null; then system_py=(py -3)
else echo "Python 3.9 or later is needed (python.org)" >&2; exit 1
fi

if [[ -x "${venv}/bin/python" ]]; then py="${venv}/bin/python"; else py="${venv}/Scripts/python.exe"; fi
if ! "${py}" -c "import piper" 2>/dev/null || [[ "$("${py}" -m pip show piper-tts 2>/dev/null | sed -n 's/^Version: //p')" != "${PIPER_VERSION}" ]]; then
  echo "Installing Piper ${PIPER_VERSION} into ${venv}..."
  "${system_py[@]}" -m venv "${venv}"
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
import os, sys, wave
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
        part = f"{out}/{name}.part"   # the real file is replaced only once its new version is whole
        try:
            with wave.open(part, "wb") as w:
                voice.synthesize_wav(text.strip(), w)
            with wave.open(part, "rb") as r:
                if r.getnframes() == 0:
                    raise RuntimeError("Piper made no sound (see the error above)")
        except Exception as e:
            if os.path.exists(part):
                os.remove(part)
            sys.exit(f"{name}: {e}; the existing file is left as it was")
        os.replace(part, f"{out}/{name}")
        print(name)
EOF
