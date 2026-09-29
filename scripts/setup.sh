#!/usr/bin/env bash
#
# setup.sh: run once after cloning (macOS: Terminal; Windows: Git Bash).
# Running it again is harmless: a second run changes nothing.
#
#   1. Checks that git and Git LFS are installed. It installs nothing.
#   2. Tells git to use this repo's hooks in .githooks/.
#   3. Turns on Git LFS for this repo.
#   4. Downloads the real LFS files (images, models...) in place of pointers.
#   5. Finds Unity's merge tool (UnityYAMLMerge) and registers it, so git can
#      merge scenes and prefabs. If Unity isn't found, it warns and goes on.
#   6. Prints a summary.
#
# All settings go into this repo's own .git/config ("--local"), never into
# your global git settings, so this is safe on a shared computer.
#
# Usage:  bash scripts/setup.sh
# If Unity is installed somewhere unusual, point to its merge tool:
#   UNITY_YAML_MERGE="/path/to/UnityYAMLMerge" bash scripts/setup.sh

set -u

case $(uname -s) in
  Darwin) os=mac ;;
  MINGW* | MSYS* | CYGWIN*) os=windows ;;
  *) os=linux ;;
esac

# ---- 1. Check the tools ----

if ! command -v git > /dev/null 2>&1; then
  echo "git is not installed."
  case $os in
    mac) echo "Install it with: xcode-select --install" ;;
    windows) echo "Install Git for Windows: https://git-scm.com/download/win" ;;
    linux) echo "Install it with: sudo apt install git" ;;
  esac
  exit 1
fi

if ! git lfs version > /dev/null 2>&1; then
  echo "Git LFS is not installed."
  case $os in
    mac) echo "Install it with: brew install git-lfs" ;;
    windows) echo "Git LFS comes with Git for Windows: re-run its installer" \
                  "(https://git-scm.com/download/win) and keep 'Git LFS' ticked." ;;
    linux) echo "Install it with: sudo apt install git-lfs" ;;
  esac
  echo "Then run this script again."
  exit 1
fi

# Work from the repo's top folder (this script lives in <repo>/scripts/).
cd "$(dirname "$0")/.." || exit 1

# ---- 2. Use the repo's hooks ----

git config --local core.hooksPath .githooks

# ---- 3. Turn on Git LFS for this repo only ----

# --skip-repo: set up LFS's clean/smudge filters but leave the hooks alone.
# Our hooks in .githooks/ already call git-lfs (see .githooks/pre-push).
git lfs install --local --skip-repo

# ---- 4. Download the real LFS files ----

# A clone made before this setup may contain small pointer files instead of
# the real images/models. This replaces them. (Prints nothing if up to date.)
git lfs pull

# ---- 5. Find Unity's merge tool and register it ----

# Where Unity Hub installs editors on each OS. VERSION stands for the
# Unity version folder, e.g. 6000.3.23f1.
# (On Windows, Git Bash writes C:\Program Files as /c/Program Files.)
case $os in
  mac)
    pattern="/Applications/Unity/Hub/Editor/VERSION/Unity.app/Contents/Tools/UnityYAMLMerge" ;;
  windows)
    pattern="/c/Program Files/Unity/Hub/Editor/VERSION/Editor/Data/Tools/UnityYAMLMerge.exe" ;;
  linux)
    pattern="$HOME/Unity/Hub/Editor/VERSION/Editor/Data/Tools/UnityYAMLMerge" ;;
esac
before_version=${pattern%%VERSION*}   # the part of the path before VERSION
after_version=${pattern#*VERSION}     # the part of the path after VERSION

# The Unity version this project uses, if the Unity project exists.
project_version=""
version_file=ProjectSettings/ProjectVersion.txt
if [ -f "$version_file" ]; then
  project_version=$(sed -n 's/^m_EditorVersion: *//p' "$version_file" | tr -d '\r')
fi

# The newest installed Unity version (sorted by the numbers in "6000.3.23f1").
newest_version=$(
  for tool in "$before_version"*"$after_version"; do
    [ -f "$tool" ] || continue
    version=${tool#"$before_version"}
    echo "${version%"$after_version"}"
  done | sort -t . -k 1,1n -k 2,2n -k 3,3n | tail -n 1
)

merge_tool=""
if [ -n "${UNITY_YAML_MERGE:-}" ]; then
  merge_tool=$UNITY_YAML_MERGE
  echo "Unity merge tool: using UNITY_YAML_MERGE"
elif [ -n "$project_version" ] && [ -f "$before_version$project_version$after_version" ]; then
  merge_tool="$before_version$project_version$after_version"
  echo "Unity merge tool: from Unity $project_version (the project's version)"
elif [ -n "$newest_version" ]; then
  merge_tool="$before_version$newest_version$after_version"
  echo "Unity merge tool: from Unity $newest_version (newest installed;" \
       "the project uses ${project_version:-an unknown version})"
else
  echo "WARNING: Unity's merge tool was not found in ${before_version}..."
  echo "  Scenes and prefabs will be merged as plain text for now."
  echo "  After installing Unity, run this script again."
fi

if [ -n "$merge_tool" ]; then
  # UNVERIFIED: these flags follow the commonly used setup and have not yet
  # been checked against UnityYAMLMerge's own usage text (no Unity was
  # installed where this was written). %O = common ancestor, %B = their
  # version, %A = our version, which is also where the result is written.
  git config --local merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
  git config --local merge.unityyamlmerge.driver "'$merge_tool' merge -h -p --force %O %B %A %A"
  git config --local merge.unityyamlmerge.recursive binary
fi

# ---- 6. Summary (read back from git, not just what we meant to set) ----

echo
echo "Setup summary for $(pwd)"
echo "  hooks path:   $(git config --local --get core.hooksPath)"
if [ -n "$(git config --local --get filter.lfs.process)" ]; then
  echo "  Git LFS:      on for this repo ($(git lfs version))"
else
  echo "  Git LFS:      NOT set up; see the messages above"
fi
driver=$(git config --local --get merge.unityyamlmerge.driver || echo "not configured")
echo "  merge driver: $driver"
echo
echo "On a shared computer: run 'gh auth logout' when you are done."
