#!/usr/bin/env bash
#
# asset-usage.sh: what uses this asset? Follows Unity's references upward
# (clip -> controller -> prefab -> scene) and prints them as a tree.
#
# Usage:  bash scripts/asset-usage.sh <path>
#   <path>  an asset file, or a folder: then every file under it that has a
#           .meta. Relative to where you run it, or to the repo's root.
#
# How it works:
#   1. The asset's GUID is read from its .meta ("guid: ...").
#   2. A reference is a line with "guid: <GUID>" in a tracked text file under
#      Assets/ or ProjectSettings/ of one of these types: .unity .prefab
#      .controller .overrideController .anim .asset .mat .playable .mask
#      .meta. The asset's own .meta is skipped. Only tracked files are
#      searched (git grep), so Library/ and untracked files never count; your
#      uncommitted edits to tracked files do.
#   3. Each file that refers to the asset is followed the same way, up to a
#      scene, a file nothing refers to, or 6 levels. A file already shown in
#      the tree is not expanded again ("shown above"), which also stops cycles.
#      When the reference is in another asset's .meta (e.g. a model whose
#      importer copies this model's avatar), that other asset is followed.
#   4. Scenes are marked from ProjectSettings/EditorBuildSettings.asset:
#      [in build], [in build, disabled] or [not in build].
#   5. Code. For a file in the tree that sits under a Resources/ folder, its
#      load key is the path after Resources/, without the extension; *.cs
#      lines that contain "Resources.Load" and that key are printed as
#      "code ref (Resources.Load, by name, not verified)". The script holding
#      such a line, and each script in "possible code references" at the end,
#      is then followed by its own GUID like any asset ("via script
#      <file>:<line>"), up to the prefabs and scenes that carry it.
#   6. A script that nothing refers to by GUID (one that starts itself, e.g. a
#      static class with [RuntimeInitializeOnLoadMethod]) may pick its scene by
#      name. Its string literals that equal a scene's name (a .unity file under
#      Assets/, without the extension) are listed as "scene named in code, by
#      name, not verified", with the scene's build mark.
#
# Not covered, and said so at the end of every run: Addressables, a load key
# or scene name built at run time, and file names inside model files (an
# OBJ's .mtl names its textures). The script lists the *.cs lines that
# contain the asset's file name (those with the whole name first), as
# "possible code references, not checked".
#
# No Unity, no network. Works when LFS files are still pointers: models,
# images and audio don't refer to other assets, and they aren't searched.
# Exit: 0 = done, 2 = the script couldn't run (bad path, not a git repo...).

set -u
export LC_ALL=C

MAX_DEPTH=6
TYPES="unity prefab controller overrideController anim asset mat playable mask meta"

fail() {
  echo "asset-usage: $1" >&2
  exit 2
}

[ $# -eq 1 ] || fail "usage: bash scripts/asset-usage.sh <asset file or folder>"
case $1 in -h | --help) sed -n '2,/^$/s/^# \{0,1\}//p' "$0"; exit 0 ;; esac

# The path, relative to the repo's root.
prefix=$(git rev-parse --show-prefix 2> /dev/null) || fail "not inside a git repo"
root=$(git rev-parse --show-toplevel) || fail "not inside a git repo"
target=$1
case $target in
  "$root"/*) target=${target#"$root"/} ;;
  /*) fail "$1 is outside this repo ($root)" ;;
  *) [ -e "$target" ] && target=$prefix$target ;;
esac
cd "$root" || fail "can't go to $root"
while :; do case $target in */) target=${target%/} ;; ./*) target=${target#./} ;; *) break ;; esac; done
case $target in *.meta) target=${target%.meta} ;; esac
[ -e "$target" ] || fail "no such file or folder: $1"

tmp=$(mktemp -d "${TMPDIR:-/tmp}/asset-usage.XXXXXX") || fail "can't create a temp folder"
trap 'rm -rf "$tmp"' EXIT

# The pathspecs for git grep: Assets/*.unity, ProjectSettings/*.unity, ...
# (in a git pathspec, * also matches "/", so these reach every subfolder).
set --
for t in $TYPES; do set -- "$@" "Assets/*.$t" "ProjectSettings/*.$t"; done

# Scenes in the build: "path<TAB>enabled" lines.
awk '
  $1 == "-" && $2 == "enabled:" { enabled = $3 }
  $1 == "path:" { sub(/^[ \t]*path:[ \t]*/, ""); print $0 "\t" enabled }
' ProjectSettings/EditorBuildSettings.asset > "$tmp/build" 2> /dev/null
# Every scene (for scene names written in code).
git ls-files -- 'Assets/*.unity' > "$tmp/scenes"

# scene_mark PATH: [in build], [in build, disabled] or [not in build].
scene_mark() {
  awk -F '\t' -v p="$1" '
    $1 == p { found = 1; print ($2 == "1") ? "[scene, in build]" : "[scene, in build, disabled]"; exit }
    END { if (!found) print "[scene, not in build]" }' "$tmp/build"
}

# guid_of ASSET: the GUID in ASSET.meta, or nothing.
guid_of() {
  [ -f "$1.meta" ] || return 1
  awk '$1 == "guid:" { print $2; exit }' "$1.meta"
}

# users_of GUID OWN_META OUT PATHSPEC...: the files that refer to GUID, into
# OUT, one line each: "path<TAB>count<TAB>first line<TAB>fileID on that line".
users_of() {
  local guid=$1 own=$2 out=$3
  shift 3
  git grep -n -F -e "guid: $guid" -- "$@" > "$tmp/grep.raw" 2> /dev/null
  [ $? -le 1 ] || return 1
  awk -v own="$own" -v guid="$guid" '
    {
      i = index($0, ":"); path = substr($0, 1, i - 1); rest = substr($0, i + 1)
      j = index(rest, ":"); line = substr(rest, 1, j - 1); text = substr(rest, j + 1)
      if (path == own) next
      fid = "?"
      if (match(text, /fileID: *-?[0-9]+, *guid: *[0-9a-f]+/)) {
        m = substr(text, RSTART, RLENGTH)
        if (index(m, guid)) { sub(/^fileID: */, "", m); sub(/,.*/, "", m); fid = m }
      }
      if (!(path in count)) { order[++n] = path; first[path] = line; fileid[path] = fid }
      count[path]++
    }
    END { for (k = 1; k <= n; k++) print order[k] "\t" count[order[k]] "\t" first[order[k]] "\t" fileid[order[k]] }
  ' "$tmp/grep.raw" > "$out"
}

# show ASSET DEPTH INDENT: print the files that use ASSET, and follow them.
show() {
  local asset=$1 depth=$2 indent=$3 guid file n line fid what next mark
  guid=$(guid_of "$asset") || { echo "${indent}(no .meta: can't follow)"; return; }
  [ -n "$guid" ] || { echo "${indent}(no guid in its .meta: can't follow)"; return; }
  users_of "$guid" "$asset.meta" "$tmp/users.$depth" "${pathspecs[@]}" ||
    fail "git grep failed"
  [ -s "$tmp/users.$depth" ] || echo "${indent}no references"
  while IFS="$(printf '\t')" read -r file n line fid; do
    what="line $line"
    [ "$n" -gt 1 ] && what="$what (+$((n - 1)) more)"
    [ "$fid" = "?" ] || what="$what, fileID $fid"
    case $file in
      *.unity)
        echo "${indent}<- $file  $what  $(scene_mark "$file")"
        continue ;;
      *.meta)
        next=${file%.meta}
        echo "${indent}<- $next  (in its .meta: $what)" ;;
      ProjectSettings/*)
        echo "${indent}<- $file  $what  [project settings]"
        continue ;;
      *)
        next=$file
        echo "${indent}<- $file  $what" ;;
    esac
    if grep -qxF "$next" "$tmp/shown"; then
      echo "${indent}   (shown above)"
    elif [ "$depth" -ge "$MAX_DEPTH" ]; then
      echo "${indent}   (stopped: $MAX_DEPTH levels)"
    else
      echo "$next" >> "$tmp/shown"
      show "$next" $((depth + 1)) "$indent   "
    fi
  done < "$tmp/users.$depth"
  code_refs "$asset" "$depth" "$indent"
  case $asset in
    *.cs) [ -s "$tmp/users.$depth" ] || scenes_named_in "$asset" "$indent" ;;
  esac
}

# scenes_named_in SCRIPT INDENT: the string literals in SCRIPT that equal the
# name of a scene (a .unity file under Assets/), each with that scene's mark.
scenes_named_in() {
  local script=$1 indent=$2 lineno name scene
  awk -v scene_list="$tmp/scenes" '
    BEGIN {
      while ((getline p < scene_list) > 0) {
        n = p; sub(/^.*\//, "", n); sub(/\.unity$/, "", n); is_scene[n] = 1
      }
    }
    {
      rest = $0
      while (match(rest, /"[^"]*"/)) {
        lit = substr(rest, RSTART + 1, RLENGTH - 2)
        rest = substr(rest, RSTART + RLENGTH)
        if (lit in is_scene) print FNR "\t" lit
      }
    }' "$script" > "$tmp/named"
  while IFS="$(printf '\t')" read -r lineno name; do
    awk -v n="$name" '{ b = $0; sub(/^.*\//, "", b); sub(/\.unity$/, "", b) } b == n' \
      "$tmp/scenes" > "$tmp/named.scenes"
    while IFS= read -r scene; do
      echo "${indent}scene named in code, by name, not verified: $scene" \
           "($script:$lineno \"$name\")  $(scene_mark "$scene")"
    done < "$tmp/named.scenes"
  done < "$tmp/named"
}

# code_refs NODE DEPTH INDENT: if NODE sits under a Resources/ folder, print the
# *.cs lines that pass its load key to Resources.Load, and follow their scripts.
code_refs() {
  local node=$1 depth=$2 indent=$3 key hit file rest line text
  case $node in */Resources/*) ;; *) return ;; esac
  key=${node##*/Resources/}
  key=${key%.*}
  git grep -n -F -e "$key" -- '*.cs' 2> /dev/null | grep -F 'Resources.Load' > "$tmp/load.$depth"
  while IFS= read -r hit; do
    file=${hit%%:*}; rest=${hit#*:}; line=${rest%%:*}; text=${rest#*:}
    text=$(printf '%s\n' "$text" | sed 's/^[ 	]*//' | cut -c1-120)
    echo "${indent}code ref (Resources.Load, by name, not verified): $file:$line  $text"
    follow_script "$file" "$line" "$depth" "$indent   "
  done < "$tmp/load.$depth"
}

# follow_script FILE LINE DEPTH INDENT: follow the script FILE by its GUID.
follow_script() {
  local file=$1 line=$2 depth=$3 indent=$4
  echo "${indent}via script $file:$line"
  if grep -qxF "$file" "$tmp/shown"; then
    echo "${indent}   (shown above)"
  elif [ "$depth" -ge "$MAX_DEPTH" ]; then
    echo "${indent}   (stopped: $MAX_DEPTH levels)"
  else
    echo "$file" >> "$tmp/shown"
    show "$file" $((depth + 1)) "$indent   "
  fi
}

# The assets to look at: the file, or every file with a .meta under the folder.
if [ -d "$target" ]; then
  git ls-files -- "$target/" | awk '
    /\.meta$/ { meta[substr($0, 1, length($0) - 5)] = 1; next }
    { files[++n] = $0 }
    END { for (i = 1; i <= n; i++) if (files[i] in meta) print files[i] }' > "$tmp/assets"
  [ -s "$tmp/assets" ] || fail "no tracked file with a .meta under $target/"
else
  echo "$target" > "$tmp/assets"
fi

pathspecs=("$@")
while IFS= read -r asset; do
  echo "$asset  (guid $(guid_of "$asset" || echo '?'))"
  echo "$asset" > "$tmp/shown"
  show "$asset" 1 "  "
  echo
done < "$tmp/assets"

echo "Not covered: Addressables, a load key or scene name built at run time, and"
echo "file names inside model files (an OBJ's .mtl)."
echo "possible code references, not checked (each script followed by its GUID):"
found=0
while IFS= read -r asset; do
  file=$(basename "$asset"); name=${file%.*}
  [ -n "$name" ] || continue
  git grep -n -F -e "$name" -- '*.cs' > "$tmp/code.raw" 2> /dev/null
  [ -s "$tmp/code.raw" ] || continue
  # Lines with the whole file name ("Layout.wlt") first: they are likelier loads.
  { grep -F -e "$file" "$tmp/code.raw"; grep -v -F -e "$file" "$tmp/code.raw"; } > "$tmp/code"
  found=1
  total=$(wc -l < "$tmp/code"); total=$((total + 0))
  echo "  $name: $total line(s)"
  head -n 20 "$tmp/code" > "$tmp/code.shown"
  cut -c1-200 "$tmp/code.shown" | sed 's/^/    /'
  [ "$total" -le 20 ] || echo "    ... $((total - 20)) more (git grep -n -F '$name' -- '*.cs')"
  # Each script of the lines shown, once (at its first line), by its GUID.
  echo "$asset" > "$tmp/shown"
  awk -F ':' '!seen[$1]++ { print $1 "\t" $2 }' "$tmp/code.shown" > "$tmp/scripts"
  while IFS="$(printf '\t')" read -r sfile sline; do
    follow_script "$sfile" "$sline" 1 "    "
  done < "$tmp/scripts"
done < "$tmp/assets"
[ "$found" -eq 1 ] || echo "  (no *.cs line contains these file names)"
exit 0
