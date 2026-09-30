#!/usr/bin/env bash
#
# Acceptance tests for scripts/check-assets.sh.
#
# Each case below:
#   1. makes a brand-new throwaway git repo in a temp folder,
#   2. commits a few small files (the case's "setup_" function),
#   3. runs check-assets.sh inside that repo,
#   4. passes only if BOTH the exit code AND an expected piece of text match.
# The temp folder is deleted afterwards, so cases never affect each other.
#
# Usage:
#   bash scripts/tests/test-check-assets.sh
#   CHECK_ASSETS=/path/to/copy.sh bash scripts/tests/test-check-assets.sh
#     (runs the same tests against a different copy of the script)

set -u

tests_dir=$(cd "$(dirname "$0")" && pwd)
CHECK_ASSETS=${CHECK_ASSETS:-"$tests_dir/../check-assets.sh"}
if [ ! -f "$CHECK_ASSETS" ]; then
  echo "test-check-assets: script not found: $CHECK_ASSETS" >&2
  exit 2
fi

# Isolate the test repos from this computer's own git settings (global and
# system config). Otherwise a machine with Git LFS set up would silently turn
# the test .png files into LFS pointers, and the C3 cases would test nothing.
fake_home=$(mktemp -d "${TMPDIR:-/tmp}/check-assets-home.XXXXXX")
trap 'rm -rf "$fake_home"' EXIT
export HOME="$fake_home"
export GIT_CONFIG_NOSYSTEM=1
unset XDG_CONFIG_HOME
export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com
export GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com

# A valid Git LFS pointer: the small text file git stores instead of a big file.
LFS_POINTER='version https://git-lfs.github.com/spec/v1
oid sha256:4d7a214614ab2935c943f9e0ff69d22eadbb8f32b1258daaa5e2ca24d17e2393
size 12345'

# The .gitattributes line that sends .png files to Git LFS.
LFS_PNG_RULE='*.png filter=lfs diff=lfs merge=lfs -text'

# ---- Helpers for the setup functions (they run inside the temp repo) ----

# put_file PATH [CONTENT]: create a text file, and its folders if needed.
put_file() {
  mkdir -p "$(dirname "$1")"
  printf '%s\n' "${2:-placeholder}" > "$1"
}

# put_binary PATH: create a fake PNG image: the PNG signature followed by
# awkward bytes (NUL, newline, carriage return, bytes above 127).
put_binary() {
  mkdir -p "$(dirname "$1")"
  printf '\211PNG\r\n\032\n' > "$1"
  printf '\000\001\n\r\377\376\200 not text \000\n\n\377' >> "$1"
  dd if=/dev/zero bs=1024 count=2 2>/dev/null >> "$1"
  printf '\n\033\177end' >> "$1"
}

# commit_all MESSAGE: stage everything and commit it.
commit_all() {
  git add -A && git commit -q -m "$1"
}

# ---- The cases ----

# T0: no Assets/ folder at all.
setup_T0() {
  put_file README.md
  commit_all "readme only"
}

# T1: a clean project: file + .meta, folder + .meta, and an LFS .png that
# was committed as a pointer (the way Git LFS stores it).
setup_T1() {
  put_file .gitattributes "$LFS_PNG_RULE"
  put_file Assets/Art.meta
  put_file Assets/Art/Frame.fbx
  put_file Assets/Art/Frame.fbx.meta
  put_file Assets/Art/Photo.png "$LFS_POINTER"
  put_file Assets/Art/Photo.png.meta
  commit_all "clean project"
}

# T2: a file without its .meta.
setup_T2() {
  put_file Assets/Art.meta
  put_file Assets/Art/Frame.fbx
  commit_all "file without meta"
}

# T3: a folder without its .meta (the file inside does have one).
setup_T3() {
  put_file Assets/Art/Frame.fbx
  put_file Assets/Art/Frame.fbx.meta
  commit_all "folder without meta"
}

# T4: a .meta whose file was never committed.
setup_T4() {
  put_file Assets/Ghost.png.meta
  commit_all "meta without file"
}

# T5: a .png committed as a real image, and only afterwards marked as LFS.
setup_T5() {
  put_binary Assets/Photo.png
  put_file Assets/Photo.png.meta
  commit_all "raw png"
  put_file .gitattributes "$LFS_PNG_RULE"
  commit_all "LFS rule added too late"
}

# T6: an 11 MB file that is not in LFS (run with two different limits).
# It is filled with the letter "a", not zero bytes: a file of zeros counts as
# binary and would also trip C5, and this case is only about C4.
setup_T6() {
  head -c 11534336 /dev/zero | tr '\000' a > big.bin
  commit_all "big file"
}

# T7: names with spaces; the file's .meta is missing.
setup_T7() {
  put_file "Assets/My Folder.meta"
  put_file "Assets/My Folder/My Frame.fbx"
  commit_all "names with spaces"
}

# T8: hidden (.name) and backup (name~) folders. Unity doesn't import them,
# so they need no .meta files.
setup_T8() {
  put_file Assets/.hidden/x
  put_file "Assets/Backup~/y"
  commit_all "folders Unity ignores"
}

# T9 (extra): a real image sorted before a valid pointer. Makes sure reading
# the image's raw bytes doesn't confuse C3, so exactly ONE file is reported.
setup_T9() {
  put_file .gitattributes "$LFS_PNG_RULE"
  put_binary Assets/A.png
  put_file Assets/A.png.meta
  put_file Assets/B.png "$LFS_POINTER"
  put_file Assets/B.png.meta
  commit_all "raw image, then pointer"
}

# T10: an uppercase .PNG committed as a real image, checked against THIS
# repo's real .gitattributes. core.ignorecase=false makes git compare names
# case-sensitively, as on Linux (GitHub CI), even on macOS and Windows.
repo_gitattributes="$tests_dir/../../.gitattributes"
setup_T10() {
  git config core.ignorecase false
  cp "$repo_gitattributes" .gitattributes
  put_binary Assets/Photo.PNG
  put_file Assets/Photo.PNG.meta
  commit_all "uppercase raw png"
}

# T11 (C5): a binary file whose type .gitattributes doesn't send to LFS.
setup_T11() {
  put_file .gitattributes "$LFS_PNG_RULE"
  put_file Assets/Plugins.meta
  put_binary Assets/Plugins/libnative.so
  put_file Assets/Plugins/libnative.so.meta
  commit_all "raw binary plugin"
}

# T12 (C6): Unity's generated folders committed by mistake, in two spellings.
setup_T12() {
  put_file Library/ArtifactDB
  put_file userSettings/Layouts/default.dwlt
  commit_all "generated folders"
}

# T13 (C7): the project was saved by a different Unity version.
setup_T13() {
  put_file ProjectSettings/ProjectVersion.txt "m_EditorVersion: 6000.3.22f1"
  commit_all "other unity version"
}

# T14 (C8 problem): Unity AI tools, Sentis and the Meta XR Simulator.
setup_T14() {
  put_file Packages/manifest.json '{
  "dependencies": {
    "com.meta.xr.simulator": "77.0.0",
    "com.unity.ai.assistant": "1.0.0",
    "com.unity.ai.inference": "2.2.0",
    "com.unity.sentis": "2.1.0"
  }
}'
  commit_all "blocked packages"
}

# T15 (C7 + C8 pass): the right Unity version; AI Navigation is allowed.
setup_T15() {
  put_file ProjectSettings/ProjectVersion.txt "m_EditorVersion: 6000.3.23f1"
  put_file Packages/manifest.json '{
  "dependencies": {
    "com.unity.ai.navigation": "2.0.14",
    "com.unity.xr.openxr": "1.16.1"
  }
}'
  commit_all "allowed packages"
}

# T16 (C8 warning): another Meta XR package. A warning, but still exit 0.
setup_T16() {
  put_file Packages/manifest.json '{ "dependencies": { "com.meta.xr.sdk.core": "207.0.0" } }'
  commit_all "meta package"
}

# T17 (C9 warning): a sample folder that docs/ASSETS.md doesn't list.
setup_T17() {
  put_file Assets/Samples.meta
  put_file "Assets/Samples/XR Hands.meta"
  put_file "Assets/Samples/XR Hands/Hand.prefab"
  put_file "Assets/Samples/XR Hands/Hand.prefab.meta"
  commit_all "sample without docs"
}

# T18 (C9 pass): the same folders, listed in docs/ASSETS.md.
setup_T18() {
  setup_T17
  put_file docs/ASSETS.md '| `Assets/Samples` | samples |
| `Assets/Samples/XR Hands` | HandVisualizer |'
  commit_all "sample with docs"
}

# ---- Runner ----

failures=0

# run_case ID SETUP_FUNCTION EXPECTED_EXIT EXPECTED_TEXT [MAX_NON_LFS_MB]
run_case() {
  id=$1; setup=$2; want_exit=$3; want_text=$4; max_mb=${5:-10}
  repo=$(mktemp -d "${TMPDIR:-/tmp}/check-assets-test.XXXXXX")

  if ! (cd "$repo" && git init -q && "$setup") > /dev/null 2>&1; then
    echo "FAIL $id: the test setup itself failed"
    failures=$((failures + 1))
    rm -rf "$repo"
    return
  fi

  output=$(cd "$repo" && MAX_NON_LFS_MB=$max_mb bash "$CHECK_ASSETS" 2>&1)
  got_exit=$?
  rm -rf "$repo"

  case $output in
    *"$want_text"*) text_found=yes ;;
    *) text_found=no ;;
  esac

  if [ "$got_exit" -eq "$want_exit" ] && [ "$text_found" = yes ]; then
    echo "PASS $id"
  else
    echo "FAIL $id: wanted exit $want_exit + text '$want_text'; got exit $got_exit:"
    printf '%s\n' "$output" | sed 's/^/    | /'
    failures=$((failures + 1))
  fi
}

run_case T0  setup_T0 0 "skipped: no Assets/ yet"
run_case T1  setup_T1 0 "no problems found"
run_case T2  setup_T2 1 "[C1 missing-meta] Assets/Art/Frame.fbx ->"
run_case T3  setup_T3 1 "[C1 missing-meta] Assets/Art (folder) ->"
run_case T4  setup_T4 1 "[C2 orphan-meta] Assets/Ghost.png.meta ->"
run_case T5  setup_T5 1 "[C3 lfs-not-pointer] Assets/Photo.png ->"
run_case T6a setup_T6 1 "[C4 oversized-non-lfs] big.bin" 10
run_case T6b setup_T6 0 "no problems found" 20
run_case T7  setup_T7 1 "[C1 missing-meta] Assets/My Folder/My Frame.fbx ->"
run_case T8  setup_T8 0 "no problems found"
run_case T9  setup_T9 1 "C3 lfs-not-pointer: 1 found"
run_case T10 setup_T10 1 "[C3 lfs-not-pointer] Assets/Photo.PNG ->"
run_case T11 setup_T11 1 "[C5 binary-not-in-lfs] Assets/Plugins/libnative.so ->"
run_case T12 setup_T12 1 "C6 generated-folder: 2 found"
run_case T13 setup_T13 1 "[C7 unity-version] ProjectSettings/ProjectVersion.txt says 6000.3.22f1"
run_case T14 setup_T14 1 "C8 blocked-package: 4 found"
run_case T15 setup_T15 0 "C8 blocked-package: 0 found"
run_case T16 setup_T16 0 "[C8 needs-team-decision] WARNING com.meta.xr.sdk.core ->"
run_case T17 setup_T17 0 "[C9 not-in-assets-doc] WARNING Assets/Samples/XR Hands ->"
run_case T18 setup_T18 0 "C9 not-in-assets-doc: 0 warning(s)"

echo
if [ "$failures" -eq 0 ]; then
  echo "All cases passed."
  exit 0
fi
echo "$failures case(s) failed."
exit 1
