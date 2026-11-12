#!/usr/bin/env bash
#
# check-asset-register.sh: checks docs/ASSETS.md (the asset register) against
# the files in the repo. WARN-ONLY: it prints what it finds and exits 0, unless
# you ask for --strict. CI's blocking asset check is scripts/check-assets.sh;
# this script never replaces it and is not run by CI.
#
# The rules. Each finding is one line:
#   WARN <rule> docs/ASSETS.md:<line> <path> <detail>
# ("-" stands for the line when a finding has none, e.g. uncovered.)
#   stale-path   A repo path in backticks that names no file or folder.
#   duplicate    Two table rows with the same first cell, e.g. a row kept
#                twice when a merge conflict was solved with "Accept both".
#   uncovered    A Git LFS file under Assets/ that the register doesn't list,
#                neither the file itself nor any folder above it.
#   open-field   A row whose Source or License cell says "to confirm", "TBD",
#                "TODO", or holds a lone "?".
#
# How paths are read from docs/ASSETS.md (the same way as check C9 of
# scripts/check-assets.sh, so the two scripts never disagree on what the
# register lists; the logic is copied here, not shared):
#   - A path is the exact text between two backticks, e.g. `Assets/Art`.
#   - "Listed" means the path, in backticks, appears anywhere in the file: in
#     any column, any table or the notes. A file is covered if it, or a folder
#     above it up to Assets/<top folder>, is listed. Assets/ itself covers
#     nothing, and a trailing "/" doesn't count, exactly as in C9.
#   - stale-path only checks paths that start at the repo's root: Assets/,
#     Packages/, ProjectSettings/, Art/, docs/, scripts/ or Tools/. Names
#     written relative to a folder in the same row (`SN_Rock.fbx`, `Editor/`)
#     are not paths and aren't checked. A trailing "/" is ignored here.
#     A path with * or ? is a pattern: it is fine if it matches any file.
#     A path with "…", "<" or "{" is a placeholder and is skipped.
#     Not stale either: a path .gitignore ignores, as written (the register
#     may say what is NOT committed: `Assets/Photon/FusionDemos/`); and, in a
#     table row, a path that names a file inside the row's own first-column
#     folder or a folder above it, up to Assets/<top folder> (in the
#     `Assets/VanGogh/Art/Vincent` row, `Art/M_Vincent.mat` is
#     Assets/VanGogh/Art/M_Vincent.mat). .gitignore is read from the working
#     folder, also with a REF.
#   - A path exists if it is a file git sees (with no REF: tracked, or new and
#     not ignored, and still on disk; with a REF: committed at REF), or a
#     folder that holds one. Ignored files (Library/...) never count.
#   - duplicate compares whole first cells, spaces squeezed. Rows that share a
#     folder but list different files, e.g. "`Assets/StarryNight/Textures`
#     (`SN_Moon.png`)" and "... (`SN_Blanket.png`)", are not duplicates.
#   - open-field looks in the columns whose header says Source, License,
#     Licence or provenance. A row separated from its table by a blank line
#     keeps the last table's columns.
#   - uncovered uses `git lfs ls-files`. Without git-lfs it prints
#     "WARN lfs-unavailable" and skips that rule; a skipped rule never passes.
#
# Usage:  bash scripts/check-asset-register.sh [--strict] [REF]
#   no REF   the working folder's docs/ASSETS.md and files (your edits count)
#   REF      the docs/ASSETS.md and files committed at REF (ci-local uses this)
# Exit:   0 = done (warnings allowed)
#         1 = --strict, and stale-path or duplicate found something
#         2 = the script couldn't run
#
# No Unity, no network. Works when LFS files are still pointers: it reads
# names, never the content of LFS files.

set -u
set -o pipefail
export LC_ALL=C   # treat file names and contents as plain bytes

STRICT=0
REF=""
for arg in "$@"; do
  case $arg in
    --strict) STRICT=1 ;;
    -h | --help) sed -n '2,/^$/s/^# \{0,1\}//p' "$0"; exit 0 ;;
    -*) echo "check-asset-register: unknown option '$arg'" >&2; exit 2 ;;
    *) [ -z "$REF" ] || { echo "check-asset-register: one REF only" >&2; exit 2; }
       REF=$arg ;;
  esac
done

DOC=docs/ASSETS.md

# fail MESSAGE: stop with exit code 2 (the check itself couldn't run).
fail() {
  echo "check-asset-register: $1" >&2
  exit 2
}

scripts_dir=$(cd "$(dirname "$0")" && pwd)
cd "$scripts_dir/.." || fail "can't find the repo"

tmp=$(mktemp -d "${TMPDIR:-/tmp}/check-asset-register.XXXXXX") || fail "can't create a temp folder"
trap 'rm -rf "$tmp"' EXIT

# Writes $tmp/doc (the register) and $tmp/paths (the files that exist).
if [ -n "$REF" ]; then
  commit=$(git rev-parse --verify --quiet "$REF^{commit}") || fail "'$REF' is not a commit"
  where="$REF ($(git rev-parse --short "$commit"))"
  git cat-file -p "$commit:$DOC" > "$tmp/doc" 2> /dev/null || fail "no $DOC at $REF"
  git ls-tree -r --name-only "$commit" > "$tmp/paths" || fail "git ls-tree failed"
else
  where="the working folder"
  [ -f "$DOC" ] || fail "no $DOC here"
  cp "$DOC" "$tmp/doc" || fail "can't read $DOC"
  # Files git sees: tracked or new-but-not-ignored, minus those deleted from disk.
  git ls-files --cached --others --exclude-standard > "$tmp/seen" || fail "git ls-files failed"
  git ls-files --deleted > "$tmp/deleted" || fail "git ls-files failed"
  awk -v deleted="$tmp/deleted" '
    BEGIN { while ((getline p < deleted) > 0) gone[p] = 1 }
    !($0 in gone)' "$tmp/seen" > "$tmp/paths"
fi

# Writes $tmp/lfs: the LFS files (one path per line), or notes that git-lfs is missing.
lfs_ok=1
if git lfs version > /dev/null 2>&1; then
  if [ -n "$REF" ]; then
    git lfs ls-files -n "$commit" > "$tmp/lfs" 2> "$tmp/lfs.err" || fail "git lfs ls-files failed: $(cat "$tmp/lfs.err")"
  else
    git lfs ls-files -n > "$tmp/lfs" 2> "$tmp/lfs.err" || fail "git lfs ls-files failed: $(cat "$tmp/lfs.err")"
  fi
else
  lfs_ok=0
  : > "$tmp/lfs"
fi

# Writes $tmp/ignored: the repo paths the register mentions that .gitignore
# ignores, written exactly as in the register (git check-ignore exits 1 when
# none is ignored, which is fine).
grep -o '`[^`]*`' "$tmp/doc" | sed 's/^`//; s/`$//' |
  grep -E '^(Assets|Packages|ProjectSettings|Art|docs|scripts|Tools)(/|$)' |
  sort -u > "$tmp/tokens"
git check-ignore --stdin < "$tmp/tokens" > "$tmp/ignored"
[ $? -le 1 ] || fail "git check-ignore failed"

echo "check-asset-register: checking $DOC in $where (warnings only)"

awk -v doc="$tmp/doc" -v path_list="$tmp/paths" -v lfs_list="$tmp/lfs" \
    -v ignored_list="$tmp/ignored" \
    -v lfs_ok="$lfs_ok" -v docname="$DOC" -v tmpdir="$tmp" '
  # ignored(p): true if any part of p starts with "." or ends with "~"
  # (copied from check-assets.sh: Unity does not import those).
  function ignored(p,    parts, n, i) {
    n = split(p, parts, "/")
    for (i = 1; i <= n; i++)
      if (parts[i] ~ /^\./ || parts[i] ~ /~$/) return 1
    return 0
  }
  function trim(s) { sub(/^[ \t]+/, "", s); sub(/[ \t]+$/, "", s); return s }
  # is_repo_path(t): does t start at the repo root?
  function is_repo_path(t) {
    return t ~ /^(Assets|Packages|ProjectSettings|Art|docs|scripts|Tools)(\/|$)/
  }
  # glob_re(g): a glob (* and ? within one folder level) as an anchored regex.
  function glob_re(g,    r, i, c) {
    r = "^"
    for (i = 1; i <= length(g); i++) {
      c = substr(g, i, 1)
      if (c == "*") r = r "[^/]*"
      else if (c == "?") r = r "[^/]"
      else if (index(".^$+(){}|\\[]", c)) r = r "\\" c
      else r = r c
    }
    return r "$"
  }
  # label(cell): the first backticked text of a cell, or the cell, shortened.
  function label(cell,    s) {
    if (match(cell, /`[^`]+`/)) return substr(cell, RSTART + 1, RLENGTH - 2)
    s = trim(cell)
    return length(s) > 60 ? substr(s, 1, 57) "..." : s
  }
  # row_bases(l): for a table row, put the repo paths written in its first
  # cell into base[1..n] (no trailing "/") and return n; 0 for other lines.
  function row_bases(l,    c, cell, k, t) {
    if (l !~ /^[ \t]*\|/) return 0
    split(l, c, "|")
    cell = c[2]; k = 0
    while (match(cell, /`[^`]*`/)) {
      t = substr(cell, RSTART + 1, RLENGTH - 2)
      cell = substr(cell, RSTART + RLENGTH)
      sub(/\/+$/, "", t)
      if (is_repo_path(t)) base[++k] = t
    }
    return k
  }
  # inside_row_folder(t, n): does t name something inside base[1..n], or a
  # folder above one of them, up to Assets/<top folder>?
  function inside_row_folder(t, n,    i, d) {
    for (i = 1; i <= n; i++) {
      d = base[i]
      while (index(d, "/")) {
        if ((d "/" t) in exists) return 1
        sub(/\/[^\/]*$/, "", d)
      }
    }
    return 0
  }
  function warn(rule, line, path, detail) {
    print "WARN " rule " " docname ":" line " " path " " detail
    count[rule]++
  }

  BEGIN {
    # The files that exist, and every folder above them.
    while ((getline p < path_list) > 0) {
      exists[p] = 1
      files[++nfiles] = p
      d = p
      while (sub(/\/[^\/]*$/, "", d)) exists[d] = 1
    }
    # The register, line by line, and as one text (C9 searches the text).
    while ((getline line < doc) > 0) { lines[++nlines] = line; text = text line "\n" }

    # The mentioned paths that .gitignore ignores (documented as not committed).
    while ((getline p < ignored_list) > 0) ign[p] = 1

    # ---- stale-path: every backticked repo path ----
    for (ln = 1; ln <= nlines; ln++) {
      nbase = row_bases(lines[ln])
      rest = lines[ln]
      while (match(rest, /`[^`]*`/)) {
        tok = substr(rest, RSTART + 1, RLENGTH - 2)
        rest = substr(rest, RSTART + RLENGTH)
        if (!is_repo_path(tok) || (tok in ign)) continue
        if (index(tok, "\342\200\246") || index(tok, "<") || index(tok, "{")) continue
        t = tok
        sub(/\/+$/, "", t)
        if (t ~ /[*?]/) {
          re = glob_re(t); hit = 0
          for (i = 1; i <= nfiles && !hit; i++) if (files[i] ~ re) hit = 1
          if (!hit) warn("stale-path", ln, tok, "-> the pattern matches no file")
        } else if (!(t in exists) && !inside_row_folder(t, nbase)) {
          warn("stale-path", ln, tok, "-> no such file or folder")
        }
      }
    }

    # ---- duplicate and open-field: the table rows ----
    nroles = 0
    for (ln = 1; ln <= nlines; ln++) {
      l = lines[ln]
      if (l !~ /^[ \t]*\|/) continue
      if (l ~ /^[ \t]*\|[ \t:|-]*$/) continue                 # the |---|---| line
      n = split(l, cell, "|")
      if (ln < nlines && lines[ln + 1] ~ /^[ \t]*\|[ \t:|-]*-[ \t:|-]*$/) {
        # A header row: note which columns hold the source and the licence.
        delete role
        for (i = 2; i <= n; i++) {
          h = tolower(cell[i])
          if (h ~ /source|licen[cs]e|provenance/) role[i] = trim(cell[i])
        }
        continue
      }
      key = trim(cell[2]); gsub(/[ \t]+/, " ", key)
      if (key == "") continue
      if (key in first_seen) {
        warn("duplicate", ln, label(key), "-> same first cell as line " first_seen[key])
      } else {
        first_seen[key] = ln
      }
      for (i = 2; i <= n; i++) {
        if (!(i in role)) continue
        c = cell[i]; lc = tolower(c)
        why = ""
        if (index(lc, "to confirm")) why = "\"to confirm\""
        else if (lc ~ /(^|[^a-z])tbd([^a-z]|$)/) why = "\"TBD\""
        else if (lc ~ /(^|[^a-z])todo([^a-z]|$)/) why = "\"TODO\""
        else if (c ~ /(^|[ \t])\?([ \t]|$)/) why = "a lone \"?\""
        if (why != "") warn("open-field", ln, label(cell[2]), "-> " role[i] " says " why)
      }
    }

    # ---- uncovered: LFS files under Assets/ the register does not list ----
    if (lfs_ok) {
      while ((getline p < lfs_list) > 0) {
        if (index(p, "Assets/") != 1 || ignored(p)) continue
        covered = (index(text, "`" p "`") > 0)
        d = p
        while (!covered && sub(/\/[^\/]*$/, "", d) && d != "Assets")
          if (index(text, "`" d "`") > 0) covered = 1
        if (!covered) warn("uncovered", "-", p, "-> no row for it or a folder above it")
      }
    } else {
      print "WARN lfs-unavailable " docname ":- - git-lfs not found, so the uncovered rule was skipped"
    }

    print ""
    print "Summary (warnings; they never stop a push):"
    printf "  stale-path: %d\n", count["stale-path"]
    printf "  duplicate: %d\n", count["duplicate"]
    if (lfs_ok) printf "  uncovered: %d\n", count["uncovered"]
    else print "  uncovered: skipped (git-lfs not found)"
    printf "  open-field: %d\n", count["open-field"]
    total = count["stale-path"] + count["duplicate"] + count["uncovered"] + count["open-field"]
    blocking = count["stale-path"] + count["duplicate"]
    print total > (tmpdir "/total")
    print blocking > (tmpdir "/blocking")
  }
' || fail "the check could not run"

blocking=$(cat "$tmp/blocking" 2> /dev/null) || fail "the check could not run"
total=$(cat "$tmp/total")
if [ "$STRICT" -eq 1 ] && [ "$blocking" -gt 0 ]; then
  echo "check-asset-register: --strict: $blocking stale-path or duplicate finding(s)"
  exit 1
fi
echo "check-asset-register: $total warning(s)"
exit 0
