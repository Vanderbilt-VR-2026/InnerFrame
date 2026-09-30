#!/usr/bin/env bash
#
# check-assets.sh: checks the files COMMITTED at a git ref (default: HEAD).
# It never looks at your working folder, so uncommitted files can't fail it.
#
# The checks, in plain English. C1-C7 and C8's blocked packages are PROBLEMS:
# they make the script fail. The rest are WARNINGS: printed, but exit code 0.
#   C1 missing-meta       Unity needs a .meta file for every file and folder in
#                         Assets/. Without it, whoever opens the project next
#                         gets new IDs and broken references.
#   C2 orphan-meta        A .meta file whose file or folder isn't committed.
#                         Usually something was deleted or forgotten.
#   C3 lfs-not-pointer    Files that .gitattributes sends to Git LFS (images,
#                         models, audio...) must be committed as small LFS
#                         "pointer" files. A real image here means Git LFS
#                         wasn't set up on the computer that committed it.
#   C4 oversized-non-lfs  A file that is NOT in LFS but is bigger than
#                         MAX_NON_LFS_MB. Big files belong in LFS.
#   C5 binary-not-in-lfs  A binary file (judged by its content, not its name)
#                         whose file type .gitattributes doesn't send to LFS.
#   C6 generated-folder   Anything under Library/, Temp/, Logs/, obj/ or
#                         UserSettings/. Unity rebuilds these on every computer.
#   C7 unity-version      ProjectSettings/ProjectVersion.txt must name the
#                         team's Unity version (UNITY_VERSION below).
#   C8 blocked-package    Packages the team has ruled out: com.unity.ai.*
#                         (except com.unity.ai.navigation), Sentis, and the
#                         Meta XR Simulator.
#   C8 needs-team-decision   WARNING: any other com.meta.xr.* package.
#   C9 not-in-assets-doc  WARNING: a top-level folder under Assets/, or a
#                         package folder under Assets/Samples/, that
#                         docs/ASSETS.md doesn't list.
#
# C1, C2 and C9 skip hidden (".name") and backup ("name~") files and folders,
# because Unity doesn't import them.
#
# Usage:  scripts/check-assets.sh [REF]
# Env:    MAX_NON_LFS_MB   size limit for non-LFS files, in MB (default 10)
# Exit:   0 = no problems (warnings allowed), 1 = problems found,
#         2 = the script couldn't run
#
# Speed: Unity projects have tens of thousands of files, and starting one
# program per file is very slow on Windows. So the script asks git for
# everything in three bulk calls (ls-tree, check-attr, cat-file) and does
# the per-file work inside awk.

set -u
set -o pipefail
export LC_ALL=C   # treat file names and contents as plain bytes

REF=${1:-HEAD}
MAX_NON_LFS_MB=${MAX_NON_LFS_MB:-10}
LFS_HEADER='version https://git-lfs.github.com/spec/v1'
UNITY_VERSION=6000.3.23f1   # C7: the Unity version the whole team uses

# fail MESSAGE: stop with exit code 2 (the check itself couldn't run).
fail() {
  echo "check-assets: $1" >&2
  exit 2
}

case $MAX_NON_LFS_MB in
  '' | *[!0-9]*) fail "MAX_NON_LFS_MB must be a whole number, got '$MAX_NON_LFS_MB'" ;;
esac
commit=$(git rev-parse --verify --quiet "$REF^{commit}") || fail "'$REF' is not a commit"

tmp=$(mktemp -d "${TMPDIR:-/tmp}/check-assets.XXXXXX") || fail "can't create a temp folder"
trap 'rm -rf "$tmp"' EXIT

# Writes $tmp/tree  (one line per committed file: "mode type id size<TAB>path")
# and    $tmp/paths (just the paths).
list_committed_files() {
  # -z separates entries with NUL bytes, which is safe for any file name.
  git ls-tree -r -l -z "$commit" > "$tmp/tree.z" || fail "git ls-tree failed"
  # Below we switch to one line per file, so a newline inside a file name
  # would break things. Refuse to continue in that (very unusual) case.
  newlines=$(tr -dc '\n' < "$tmp/tree.z" | wc -c)
  if [ $newlines -ne 0 ]; then
    fail "a committed file name contains a newline; rename it, commit, try again"
  fi
  tr '\0' '\n' < "$tmp/tree.z" > "$tmp/tree"
  cut -f 2- "$tmp/tree" > "$tmp/paths"
}

# Writes $tmp/lfs: the committed paths whose "filter" attribute is "lfs".
find_lfs_files() {
  # --source reads .gitattributes from the commit itself (git 2.40 or newer).
  # Older git can only read the .gitattributes in the working folder.
  if git check-attr --source="$commit" filter -- .gitattributes > /dev/null 2>&1; then
    source_option="--source=$commit"
    attr_mode="read from $REF"
  else
    source_option=""
    attr_mode="read from the working folder (git is older than 2.40)"
  fi
  # check-attr -z prints three NUL-separated fields per path:
  # path, attribute name ("filter"), value. Keep paths whose value is "lfs".
  tr '\n' '\0' < "$tmp/paths" |
    git check-attr $source_option --stdin -z filter |
    tr '\0' '\n' |
    awk 'NR % 3 == 1 { path = $0 }  NR % 3 == 0 && $0 == "lfs" { print path }' \
    > "$tmp/lfs" || fail "git check-attr failed"
}

# Awk code shared by C1 and C2. It reads $tmp/paths and remembers:
#   tracked[p]  every committed path under Assets/  (files[1..nfiles] in order)
#   folder[d]   every folder under Assets/ that contains committed files
#               (folders[1..nfolders] in order)
# ignored(p) is true if any part of p starts with "." or ends with "~".
ASSETS_INDEX_AWK='
function ignored(p,    parts, n, i) {
  n = split(p, parts, "/")
  for (i = 1; i <= n; i++)
    if (parts[i] ~ /^\./ || parts[i] ~ /~$/) return 1
  return 0
}
index($0, "Assets/") == 1 {
  tracked[$0] = 1
  files[++nfiles] = $0
  d = $0
  # Walk up the parent folders, stopping at Assets/ (which has no .meta).
  while (sub(/\/[^\/]*$/, "", d) && d != "Assets") {
    if (!(d in folder)) { folder[d] = 1; folders[++nfolders] = d }
  }
}
'

# C1: every file and folder under Assets/ needs a committed .meta.
check_missing_meta() {
  awk "$ASSETS_INDEX_AWK"'
  END {
    hint = " (open the project in Unity once to generate it)"
    for (i = 1; i <= nfiles; i++) {
      p = files[i]
      if (p ~ /\.meta$/ || ignored(p) || ((p ".meta") in tracked)) continue
      print "[C1 missing-meta] " p " -> commit " p ".meta" hint
    }
    for (i = 1; i <= nfolders; i++) {
      d = folders[i]
      if (ignored(d) || ((d ".meta") in tracked)) continue
      print "[C1 missing-meta] " d " (folder) -> commit " d ".meta" hint
    }
  }' "$tmp/paths"
}

# C2: every .meta under Assets/ must belong to a committed file or folder.
check_orphan_meta() {
  awk "$ASSETS_INDEX_AWK"'
  END {
    for (i = 1; i <= nfiles; i++) {
      p = files[i]
      if (p !~ /\.meta$/ || ignored(p)) continue
      target = substr(p, 1, length(p) - 5)
      if ((target in tracked) || (target in folder)) continue
      print "[C2 orphan-meta] " p " -> nothing named " target " is committed;" \
            " delete this .meta, or commit what it belongs to"
    }
  }' "$tmp/paths"
}

# C3: files that .gitattributes sends to LFS must be committed as LFS pointers.
check_lfs_pointers() {
  # From the tree listing, keep the LFS files: one "id<TAB>path" line each.
  # (Symlinks, mode 120000, never go through LFS, so they are skipped.)
  awk -v lfs_list="$tmp/lfs" '
    BEGIN { while ((getline p < lfs_list) > 0) is_lfs[p] = 1 }
    {
      tab = index($0, "\t")
      split(substr($0, 1, tab - 1), f, " ")   # f[1]=mode f[2]=type f[3]=id
      path = substr($0, tab + 1)
      if (f[2] == "blob" && f[1] != "120000" && (path in is_lfs)) print f[3] "\t" path
    }' "$tmp/tree" > "$tmp/lfs_blobs"
  [ -s "$tmp/lfs_blobs" ] || return 0

  # Ask git for the content of all of them at once. For each file, git prints
  # a header line "<id> blob <size>", then exactly <size> bytes, then a newline.
  # The awk below counts bytes to find where each file ends, and checks that
  # each file's first line is the LFS pointer header.
  # tr turns NUL bytes into spaces so that awk can safely read binary files.
  cut -f 1 "$tmp/lfs_blobs" |
    git cat-file --batch |
    tr '\0' ' ' |
    awk -v blob_list="$tmp/lfs_blobs" -v header="$LFS_HEADER" '
      BEGIN {
        while ((getline line < blob_list) > 0) path[++n] = substr(line, index(line, "\t") + 1)
        fix = " -> committed as the real file, not an LFS pointer. Run scripts/setup.sh," \
              " then: git add --renormalize -- <this path>, and commit"
      }
      left > 0 {                       # a line of file content
        if (first && index($0, header) != 1) print "[C3 lfs-not-pointer] " path[i] fix
        first = 0
        left -= length($0) + 1         # + 1 for the newline awk removed
        next
      }
      $2 != "blob" { i = -1; exit }    # unexpected header: stop, reported below
      { i++; left = $3 + 1; first = 1 }  # a header line; + 1 for the newline git adds
      END {
        if (i != n) {
          print "check-assets: C3 could not read the LFS files correctly" | "cat 1>&2"
          exit 2
        }
      }'
}

# C4: files NOT in LFS must not be bigger than MAX_NON_LFS_MB.
check_oversized() {
  awk -v lfs_list="$tmp/lfs" -v max_mb="$MAX_NON_LFS_MB" '
    BEGIN {
      while ((getline p < lfs_list) > 0) is_lfs[p] = 1
      fix = " -> add its file type to Git LFS (.gitattributes), or keep it out of git"
    }
    {
      tab = index($0, "\t")
      split(substr($0, 1, tab - 1), f, " ")   # f[2]=type f[4]=size in bytes
      path = substr($0, tab + 1)
      if (f[2] != "blob" || (path in is_lfs)) next
      if (f[4] > max_mb * 1048576) {
        size = sprintf("%.1f MB > %d MB", f[4] / 1048576, max_mb)
        print "[C4 oversized-non-lfs] " path " (" size ")" fix
      }
    }' "$tmp/tree"
}

# C5: a binary file must be in LFS. "Binary" is git's own test: a NUL byte in
# the first 8000 bytes. LFS pointers are text, so they never count. Files that
# .gitattributes already sends to LFS are left to C3, so nothing is reported
# twice. Two bulk calls: git grep -l lists every file with at least one line,
# and -I makes it skip binary files; the binary files are the difference.
# (git grep exits 1 when nothing matches, which is fine; 2 or more is an error.)
check_binary_not_lfs() {
  git grep -z -l -e '' "$commit" > "$tmp/all.z"
  [ $? -le 1 ] || return 1
  git grep -z -l -I -e '' "$commit" > "$tmp/text.z"
  [ $? -le 1 ] || return 1
  # git grep prints "<commit>:<path>"; keep only the path.
  tr '\0' '\n' < "$tmp/all.z" | sed "s|^$commit:||" | sort > "$tmp/all"
  tr '\0' '\n' < "$tmp/text.z" | sed "s|^$commit:||" | sort > "$tmp/text"
  comm -23 "$tmp/all" "$tmp/text" |
    awk -v lfs_list="$tmp/lfs" '
      BEGIN { while ((getline p < lfs_list) > 0) is_lfs[p] = 1 }
      !($0 in is_lfs) {
        print "[C5 binary-not-in-lfs] " $0 " -> add its file type to Git LFS" \
              " (.gitattributes), then: git add --renormalize -- <this path>, and commit"
      }'
}

# C6: nothing from the folders Unity (or your IDE) regenerates on every
# computer. .gitignore keeps them out; this catches a forced add.
check_generated_folders() {
  awk '{
    top = $0
    if (!sub(/\/.*/, "", top)) next          # a file at the top level: skip
    t = tolower(top)
    if (t == "library" || t == "temp" || t == "logs" || t == "obj" || t == "usersettings")
      print "[C6 generated-folder] " $0 " -> remove it from git: git rm -r --cached " top
  }' "$tmp/paths"
}

# C7: the project must stay on the team's Unity version. Opening it in another
# version silently upgrades files that everyone else then has to take.
check_unity_version() {
  version=$(git cat-file -p "$commit:ProjectSettings/ProjectVersion.txt" |
    awk '$1 == "m_EditorVersion:" { sub(/\r$/, "", $2); print $2 }') || return 1
  if [ "$version" != "$UNITY_VERSION" ]; then
    echo "[C7 unity-version] ProjectSettings/ProjectVersion.txt says ${version:-nothing}," \
         "not $UNITY_VERSION -> open the project in Unity $UNITY_VERSION"
  fi
}

# list_packages: the package names in Packages/manifest.json, one per line
# (every "com.something": key; strings inside lists have no ":" after them).
list_packages() {
  git cat-file -p "$commit:Packages/manifest.json" |
    awk '{
      while (match($0, /"com\.[A-Za-z0-9._-]+"[ \t]*:/)) {
        name = substr($0, RSTART + 1, RLENGTH - 1)
        sub(/".*/, "", name)
        print name
        $0 = substr($0, RSTART + RLENGTH)
      }
    }'
}

# C8 (problem): packages the team has ruled out. com.unity.ai.navigation is
# Unity's NavMesh package, so it stays allowed; the other com.unity.ai.*
# packages are Unity's AI tools, including the Inference Engine (formerly
# Sentis, com.unity.sentis).
check_blocked_packages() {
  list_packages | awk '
    $0 == "com.unity.ai.navigation" { next }
    /^com\.unity\.ai\./ || $0 == "com.unity.sentis" || /^com\.meta\.xr\.simulator/ {
      print "[C8 blocked-package] " $0 " -> remove it in Package Manager;" \
            " the team has ruled it out"
    }'
}

# C8 (warning): other Meta XR packages are not ruled out, but nobody should
# add one alone: it changes project settings for everyone.
check_team_packages() {
  list_packages | awk '
    /^com\.meta\.xr\./ && !/^com\.meta\.xr\.simulator/ {
      print "[C8 needs-team-decision] WARNING " $0 " -> needs a team decision" \
            " before it is merged"
    }'
}

# C9 (warning): docs/ASSETS.md must name every top-level folder under Assets/
# and every package folder under Assets/Samples/, written in backticks.
check_assets_doc() {
  git cat-file -p "$commit:docs/ASSETS.md" > "$tmp/assets_doc" 2> /dev/null ||
    : > "$tmp/assets_doc"   # no docs/ASSETS.md yet: every folder is missing
  awk -v doc="$tmp/assets_doc" '
    # want DIR: warn once if DIR (in backticks) is not in the doc.
    function want(dir) {
      if (dir in seen) return
      seen[dir] = 1
      if (index(text, "`" dir "`") == 0)
        print "[C9 not-in-assets-doc] WARNING " dir " -> add it to docs/ASSETS.md" \
              " (what it is, source, license)"
    }
    BEGIN { while ((getline line < doc) > 0) text = text line "\n" }
    index($0, "Assets/") == 1 {
      n = split($0, part, "/")
      if (n < 3 || part[2] ~ /^\./ || part[2] ~ /~$/) next   # a file, or ignored
      want("Assets/" part[2])
      if (part[2] == "Samples" && n >= 4 && part[3] !~ /^\./ && part[3] !~ /~$/)
        want("Assets/Samples/" part[3])
    }' "$tmp/paths"
}

# run_check ID NAME FUNCTION: run one check, print its problems, count them.
problems=0
summary=""
run_check() {
  "$3" > "$tmp/out" || fail "check $1 could not run"
  count=$(wc -l < "$tmp/out")
  count=$((count + 0))   # wc pads the number with spaces on macOS
  cat "$tmp/out"
  problems=$((problems + count))
  summary="$summary  $1 $2: $count found
"
}

# run_warn ID NAME FUNCTION: like run_check, but what it finds are warnings:
# they are printed and counted, and don't change the exit code.
warnings=0
run_warn() {
  "$3" > "$tmp/out" || fail "check $1 could not run"
  count=$(wc -l < "$tmp/out")
  count=$((count + 0))
  cat "$tmp/out"
  warnings=$((warnings + count))
  summary="$summary  $1 $2: $count warning(s)
"
}

# skip_check ID NAME [REASON]: note in the summary that a check was skipped.
skip_check() {
  summary="$summary  $1 $2: skipped: ${3:-no Assets/ yet}
"
}

# is_committed PATH: true if PATH is one of the committed files.
is_committed() {
  grep -qxF "$1" "$tmp/paths"
}

list_committed_files
find_lfs_files
echo "check-assets: checking files committed at $REF ($(git rev-parse --short "$commit"))"
echo "check-assets: LFS rules $attr_mode"

if grep -q '^Assets/' "$tmp/paths"; then
  run_check C1 missing-meta check_missing_meta
  run_check C2 orphan-meta check_orphan_meta
else
  skip_check C1 missing-meta
  skip_check C2 orphan-meta
fi
run_check C3 lfs-not-pointer check_lfs_pointers
run_check C4 oversized-non-lfs check_oversized
run_check C5 binary-not-in-lfs check_binary_not_lfs
run_check C6 generated-folder check_generated_folders
if is_committed ProjectSettings/ProjectVersion.txt; then
  run_check C7 unity-version check_unity_version
else
  skip_check C7 unity-version "no ProjectSettings/ProjectVersion.txt yet"
fi
if is_committed Packages/manifest.json; then
  run_check C8 blocked-package check_blocked_packages
  run_warn C8 needs-team-decision check_team_packages
else
  skip_check C8 packages "no Packages/manifest.json yet"
fi
if grep -q '^Assets/' "$tmp/paths"; then
  run_warn C9 not-in-assets-doc check_assets_doc
else
  skip_check C9 not-in-assets-doc
fi

echo
echo "Summary:"
printf '%s' "$summary"
if [ "$warnings" -gt 0 ]; then
  note=" ($warnings warning(s) above: please fix them, but they don't stop a push)"
else
  note=""
fi
if [ "$problems" -eq 0 ]; then
  echo "check-assets: no problems found$note"
  exit 0
fi
echo "check-assets: $problems problem(s) found$note"
exit 1
