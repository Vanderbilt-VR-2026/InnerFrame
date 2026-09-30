#!/usr/bin/env bash
#
# ci-local.sh: runs on your computer what GitHub CI runs, BEFORE you push.
# Run it, read the last line, and only push when it says PASS.
#
#   1/3  The checker's own tests (scripts/tests/test-check-assets.sh).
#        This is the first step of CI, unchanged.
#   2/3  The asset check (scripts/check-assets.sh) on your branch AS IF IT
#        WERE MERGED INTO main. That is what CI checks: not your branch by
#        itself, but the pull request's merge with main, where main's
#        .gitattributes (the Git LFS rules) applies. A branch cut before those
#        rules existed can show 0 problems on its own and 56 once merged
#        (PR #7). The merge is built in memory: no branch, no file on disk.
#   3/3  The same check on EVERY commit of the branch, each merged into main.
#        CI only checks the last commit. But a big file committed the wrong
#        way and fixed in a later commit still lands in main's history when
#        the pull request is merged with a merge commit.
#
# Usage:  bash scripts/ci-local.sh [BRANCH]     (default: the branch you're on)
# Exit:   0 = PASS, safe to push
#         1 = something to fix before you push
#         2 = the script couldn't run (offline with no origin/main, old git...)
#
# Needs git 2.38 or newer (for "git merge-tree --write-tree").

set -u

BRANCH=${1:-HEAD}

scripts_dir=$(cd "$(dirname "$0")" && pwd)
cd "$scripts_dir/.." || exit 2

# stop MESSAGE: the script itself couldn't run (exit 2).
stop() {
  echo "ci-local: $1" >&2
  exit 2
}

# step N TITLE: print a step header.
step() {
  echo
  echo "=== $1/3  $2"
}

tmp=$(mktemp -d "${TMPDIR:-/tmp}/ci-local.XXXXXX") || stop "can't create a temp folder"
trap 'rm -rf "$tmp"' EXIT

# ---- Ground truth: the latest main, and the branch to check ----

if git fetch --quiet origin main 2> "$tmp/fetch.err"; then
  echo "ci-local: fetched the latest origin/main"
else
  echo "ci-local: WARNING could not fetch origin/main (offline?);" \
       "using the copy from the last fetch" >&2
fi
git rev-parse --verify --quiet origin/main^{commit} > /dev/null ||
  stop "no origin/main here. Run: git fetch origin"

tip=$(git rev-parse --verify --quiet "$BRANCH^{commit}") ||
  stop "'$BRANCH' is not a branch or commit"
echo "ci-local: checking $BRANCH ($(git rev-parse --short "$tip"))" \
     "against origin/main ($(git rev-parse --short origin/main))"

# merge_with_main COMMIT: builds, in memory, the commit CI would check: COMMIT
# merged into origin/main. Writes its id to $tmp/merged. No branch points at
# it and nothing in your working folder changes.
# Returns 1 if COMMIT conflicts with main (the files are in $tmp/conflicts),
# 2 if git couldn't do it.
merge_with_main() {
  git merge-tree --write-tree --name-only origin/main "$1" \
    > "$tmp/merge" 2> "$tmp/merge.err"
  case $? in
    0) ;;
    1) sed -n '2,/^$/p' "$tmp/merge" | sed '/^$/d' > "$tmp/conflicts"; return 1 ;;
    *) cat "$tmp/merge.err" >&2
       echo "ci-local: 'git merge-tree --write-tree' failed; git 2.38 or newer is needed" >&2
       return 2 ;;
  esac
  tree=$(head -n 1 "$tmp/merge")
  git -c user.name=ci-local -c user.email=ci-local@localhost \
    commit-tree "$tree" -p origin/main -p "$1" -m "ci-local: throwaway merge" \
    > "$tmp/merged" 2> "$tmp/merge.err" || { cat "$tmp/merge.err" >&2; return 2; }
}

# ---- 1/3 ----

step 1 "Test the checker itself (same as CI's first step)"
bash "$scripts_dir/tests/test-check-assets.sh"
case $? in
  0) ;;
  2) stop "the checker's tests couldn't run" ;;
  *) echo
     echo "FAIL: the checker's own tests fail, so CI would fail too."
     echo "Something in scripts/ changed. Don't push; ask Cici."
     exit 1 ;;
esac

# ---- 2/3 ----

step 2 "Check $BRANCH as if merged into origin/main (what CI checks)"
merge_with_main "$tip"
case $? in
  0) ;;
  1) echo "FAIL: $BRANCH conflicts with main in these files:"
     sed 's/^/    /' "$tmp/conflicts"
     echo
     echo "CI can't check a conflicting pull request. Get main's changes first:"
     echo "    git pull origin main"
     echo "then fix the conflicts, commit, and run this script again."
     exit 1 ;;
  *) exit 2 ;;
esac
merged=$(cat "$tmp/merged")
bash "$scripts_dir/check-assets.sh" "$merged"
case $? in
  0) ;;
  1) echo
     echo "FAIL: CI would stop this pull request. Fix the problems above,"
     echo "commit, and run this script again."
     exit 1 ;;
  *) stop "the asset check couldn't run" ;;
esac

# ---- 3/3 ----

step 3 "Check every commit of $BRANCH the same way (a few seconds each)"
git rev-list --reverse --no-merges "origin/main..$tip" > "$tmp/commits"
count=$(grep -c . "$tmp/commits")
bad=0
skipped=0
if [ "$count" -eq 0 ]; then
  echo "No commits beyond origin/main: nothing to check."
fi
while read -r commit; do
  label="$(git rev-parse --short "$commit") $(git log -1 --format=%s "$commit")"
  if [ "$commit" = "$tip" ]; then
    echo "  ok       $label  (checked in step 2)"
    continue
  fi
  merge_with_main "$commit"
  case $? in
    0) ;;
    1) echo "  skipped  $label  (conflicts with main on its own)"
       skipped=$((skipped + 1))
       continue ;;
    *) exit 2 ;;
  esac
  merged=$(cat "$tmp/merged")
  bash "$scripts_dir/check-assets.sh" "$merged" > "$tmp/out" 2>&1
  case $? in
    0) echo "  ok       $label" ;;
    1) echo "  PROBLEM  $label"
       # The summary lines whose count isn't 0, indented.
       grep -E '^  C[0-9]' "$tmp/out" | grep -Ev ': 0 (found|warning)' | sed 's/^/         /'
       bad=$((bad + 1)) ;;
    *) cat "$tmp/out"; stop "the asset check couldn't run on $label" ;;
  esac
done < "$tmp/commits"

echo
if [ "$bad" -gt 0 ]; then
  echo "FAIL: $bad earlier commit(s) have a problem. The last commit is fine,"
  echo "so CI would pass, but the bad files would still go into main's history."
  echo "Don't push, and don't force-push. Ask Cici: the branch needs rebuilding"
  echo "as new commits from a fresh copy of main."
  exit 1
fi
if [ "$skipped" -gt 0 ]; then
  echo "Note: $skipped commit(s) were skipped because they conflict with main on"
  echo "their own. The last commit merges cleanly, so this is fine."
fi
echo "PASS: $BRANCH is safe to push ($count commit(s) checked against origin/main)."
exit 0
