# InnerFrame: how we work

Rules for everyone on the team, and for any AI agent (Claude Code, Codex,
Cursor) working in this repo. `CLAUDE.md` just points here.

## Start here (everyone, not just engineers)

**Before you push a branch for a pull request, run this** (on Windows, in
Git Bash):

```
bash scripts/ci-local.sh
```

It runs the same checks GitHub runs, on your computer, in well under a minute.
The last line tells you what to do:

| Last line | Meaning | What to do |
|---|---|---|
| `PASS` | Safe to push. | `git push`, then open the pull request. |
| `FAIL: CI would stop this pull request` | Your last commit has a problem (a big file not in LFS, a missing `.meta`...). | Fix what it lists, commit, run it again. |
| `FAIL: N earlier commit(s) have a problem` | Your last commit is fine, but an older one isn't. | Don't push. Don't force-push. Ask Cici: the branch needs rebuilding. |
| `FAIL: ... conflicts with main` | Your branch and `main` changed the same files. | `git pull origin main`, fix the conflicts, commit, run it again. |
| `ci-local: ...` and nothing else | The script couldn't run (offline, old git...). | Read the message; ask if unclear. |

Why this matters: a big file committed the wrong way stays in the repo's
history forever, even after it is "fixed" in a later commit. GitHub only
catches it after the push, and only in the last commit.

If you use an AI agent, ask it to **"run the adversarial-review skill on my
branch"** before marking the PR ready for review.

## The stack (pinned)

- Unity **6000.3.23f1**, URP 17.3.
- OpenXR, XR Interaction Toolkit **3.3.2**, XR Hands.
- Build with the **Android** build profile, not a "Meta Quest" one.

Guardrails that must stay set: OpenXR feature **Meta Quest Support (Android)**;
Android **minimum API 32**; application entry point **GameActivity**.

Not used, by team decision: any `com.meta.xr.*` package (Meta XR SDK, Meta
XR Simulator), the VR Multiplayer template, and `com.unity.ai.*` except
`com.unity.ai.navigation`. The asset check (`scripts/check-assets.sh`)
blocks most of these.

## Hard rules

- **Branch from the current `origin/main`.** Run `git fetch origin` first.
- **No history rewrites and no force-push** on a branch anyone else may have
  pulled. If a branch needs rebuilding, build a new one.
- **Stage files by name** (`git add path/to/file`), never `git add -A` or
  `git add .` in a PR that touches shared settings.
- **Shared settings** (`ProjectSettings/`, `Packages/`, `Assets/XR*`,
  `EditorBuildSettings`) change only with each change **named in the PR
  body** with a reason.
- **XR setup exists once, on one branch** (#5). Two branches each creating
  `Assets/XR*` get different GUIDs for the same files: 29 conflicting files
  between #5 and #7.
- **Never commit:** `Library/`, `Temp/`, `Logs/`, `obj/`, `Builds/`, APKs,
  `Assets/Oculus/`, Meta assets in `Assets/Resources/`,
  `PerformanceTestRun*.json`, tokens or keys, or `.blend` files inside
  `Assets/` (source files go in `Art/Source/`, in LFS).
- **Third-party assets need a row in `docs/ASSETS.md`** (what, source,
  license). This is a public repo.
- **No `_V2` / `_V3` copy folders.** Git keeps the history; edit in place.
- **No hand-editing Unity YAML** (`.unity`, `.prefab`, `.asset`, `.meta`).
  Use the editor, so references stay valid.
- **Lowercase `docs/` only.** A `Docs/` folder collides with it on macOS.

## Before coding

1. Name the scene you're changing and its owner: **Gallery = Cici**,
   **Bedroom = Rowling**. Changes to someone else's scene go through them.
2. Check overlap with every open PR before you start:
   `git merge-tree --write-tree --name-only <your-branch> <their-branch>`.
   Conflicting files are listed after the first line. If there are any,
   agree who lands first.

## Definition of done

Tag every claim in the PR body **[V]** (verified: say how and where) or
**[I]** (inferred, not tested).

- `bash scripts/ci-local.sh` says `PASS`. [V] with the output.
- The project opens clean from a fresh checkout: `git worktree add ../check
  <branch>`, open it in Unity, no Console errors, no missing scripts, and
  `git status` stays clean afterwards.
- One XR Origin per scene. No stacked interactables, no duplicate colliders.
- "Works on Quest" only with a headset build **from that clean worktree**:
  who tested, when, and that it ran immersive, not as a 2D window.
- Anything beyond a small fix has had an adversarial review (below), and
  its blockers are closed.

## Where things are

| What | Where |
|---|---|
| Rules for people (setup, daily loop) | `CONTRIBUTING.md`, `README.md` |
| Pre-push check, same as CI | `scripts/ci-local.sh` |
| Asset checker and its tests | `scripts/check-assets.sh`, `scripts/tests/` |
| CI | `.github/workflows/check-assets.yml` |
| Git hooks (installed by `scripts/setup.sh`) | `.githooks/` |
| Asset inventory (source, license) | `docs/ASSETS.md` |
| Per-feature docs | `docs/<feature>/README.md` |
| Review skill for AI agents | `.claude/skills/adversarial-review/` |
| Known review false positives | `.claude/skills/adversarial-review/references/known-false-positives.md` |

Each feature (Gallery, Bedroom...) gets `docs/<feature>/README.md`, modeled
on PR #7's Bedroom README: how to open it, controls, physics layers, how to
build, and the hardware tests you want teammates to run on a headset.
