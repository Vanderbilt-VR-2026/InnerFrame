---
name: adversarial-review
description: Pre-merge review of an InnerFrame (Unity/Quest) PR or branch. Use before marking a PR ready, before approving a teammate's PR, when asked to "review", "check this PR", "run the adversarial-review skill on my branch", or "why is CI failing".
---

# Adversarial review (InnerFrame)

This review produces **verdicts with evidence**, not a list of suspicions.
A plausible but wrong finding that gets "fixed" blindly is worse than no
review. Zero findings is an acceptable outcome; don't invent severity to
look thorough.

## Iron rule

A finding isn't a finding until it has a verdict:

- **CONFIRMED**: with the command, the `file:line`, or the output that shows it.
- **REFUTED**: with the written reason, so nobody raises it again next round.
- **QUESTION**: it depends on intent or on a team decision nobody has made.
  Ask the author; it isn't a defect.

Severity (blocker / fix-before-merge / nit) comes only after the verdict.

## Step 0: ground truth

1. `git fetch origin`. For a PR: `gh pr view <n>` and `gh pr checks <n>`.
   A failed check is finding #1: read its log before anything else.
2. Diff against the **real** base: `git diff --stat origin/<base>...<head>`.
   Is the branch point stale?
   `git merge-base origin/main <head> | xargs git log -1 --oneline`
3. Read `AGENTS.md` (the rules) and `references/known-false-positives.md`
   (the noise). A match with a known false positive shifts the burden: show
   why *this* instance differs.
4. Measure before you judge: lines and files by top-level folder and by
   commit. Unity scenes and prefabs are huge by nature; size alone is not a
   finding.

## Effort: say which level you chose

| Change | How |
|---|---|
| ≤ ~200 lines, or docs only | Review it yourself, 1–2 angles |
| New scene, prefab set, or scripts; touches shared settings | 3–5 finder subagents, one angle each |
| XR / package / settings setup, or asked for "max" | All 9 angles, one subagent each |

Give each finder **one** angle, the diff range, and this file. Finders don't
see each other's output. Findings several finders reach independently are
signal; so are contradictions between them.

## The nine angles

1. **LFS and history.** Run `bash scripts/ci-local.sh <head>`. It checks the
   tip merged into main (what CI checks) and every commit merged into main
   (what CI misses: a raw binary fixed later still lands in main's history).
   PR #7: 0 problems alone, 56 merged.
2. **GUID and reference integrity.** Duplicate GUIDs across `.meta` files;
   references to GUIDs that aren't committed and aren't in a package. The
   same path created on two branches with different GUIDs is a conflict.
   Trace which top-level folders reference which: a folder nothing references
   is a trim candidate, but check `Shader.Find`, `Resources.Load` and
   `MenuItem` strings before calling it dead.
3. **Shared-settings drift.** Every hunk in `ProjectSettings/`, `Packages/`,
   `Assets/XR*/` and `EditorBuildSettings` must be named in the PR body with
   a reason. Watch graphics APIs, `preloadedAssets`, `runInBackground`,
   `UnityConnectSettings`, `ProjectVersion`, layers and tags, and the physics
   collision matrix.
4. **XR guardrails and Meta residue.** Meta Quest Support (Android) on, min
   API 32, GameActivity. No `com.meta.xr.*` package, no `Assets/Oculus`, no
   Meta assets in `Assets/Resources`, no missing-script sub-assets in
   `OpenXRPackageSettings.asset`. One XR Origin per scene; no stacked
   interactables or duplicate colliders.
5. **Merge topology.** For every open PR:
   `git merge-tree --write-tree --name-only <head> <other>`. Does this PR
   redo setup another PR owns? Propose a merge order.
6. **Code.** Editor-only code lives under an `Editor/` folder. Duplicate
   class, `MenuItem` or shader names across folders. Null paths, per-frame
   allocations in `Update`, physics-layer assumptions. A shader loaded only
   by `Shader.Find` must be referenced or Always Included, or the build
   strips it.
7. **Quest performance, as facts only.** Triangle counts, texture sizes,
   realtime lights, transparent materials. The budget is a pending team
   decision: report numbers, don't grade them.
8. **Licensing and public repo.** Every new top-level `Assets/` folder has a
   `docs/ASSETS.md` row with source and license. No tokens. No Fab or Asset
   Store content without a license check.
9. **Evidence of claims and test vacuousness.** Every "works" in the PR body
   needs evidence: editor or headset? built from a clean worktree? For every
   new test: would it fail if the behavior regressed?

## Verification loop (for each finding)

1. Check `references/known-false-positives.md`.
2. Reproduce or trace it **in this repo** with real commands and
   `file:line`. "Verified mentally" isn't verification; without evidence the
   finding is at most PLAUSIBLE, never CONFIRMED.
3. Write the verdict, then the severity.
4. For every fix you propose: is it in the author's power, and does it
   rewrite anyone's history? Prefer a new branch or a squash-merge over
   force-pushing a teammate's branch.

## Closure

- CONFIRMED: fixed in the branch, or filed as an issue with a reason.
- REFUTED: the reason stays in the review so it isn't argued again.
- New traps go into `references/known-false-positives.md` (noise) or into
  `AGENTS.md` (a rule).

## Output

1. One line on the effort level you chose.
2. A table: `# | angle | finding | verdict | evidence | severity | fix`.
3. Refuted findings, each with its reason.
4. Questions for the author.
5. A proposed merge order, if other open PRs interact with this one.
6. If the author is a teammate, a draft PR comment: friendly, blockers first,
   the exact commands to run, no blame.

## STOP table

| Thought | Reality |
|---|---|
| "CI is green, so it's fine" | CI checks one tree. PR #7's history problem passes once the tip is fixed. Run ci-local. |
| "140k lines, obviously bloated" | Measure it. Scenes and prefabs are legitimately large; unreferenced folders are the signal. |
| "Plausible, just fix it" | Unverified fix = new bug. Reproduce first. |
| "Force-push their branch to clean it" | It's someone else's work. Build a new branch or squash instead. |
| "I'll decide the graphics API / perf budget here" | Team decision. File it as a QUESTION. |
| "Works on my machine" | Clean worktree + headset build, or the claim is [I], not [V]. |
