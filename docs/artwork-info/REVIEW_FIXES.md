# PR112 review fixes — 2026-10-28

Base: `910c82f1493f5d9dec930bc90051c664ab4cadd5`; inspected main: `b9a50c1` (includes PR119). Prepared in an isolated worktree from the user-uploaded Mac snapshot. No Gallery, ProjectSettings, or unrelated team files were edited. The Mac project path itself is not mounted in this executor.

## Four fixes and status

1. **Wall clearance and following:** source implemented. Exact head rotation is preferred; whole glass/UI OBB constraints shorten the1.35m target distance, reduce tilt toward upright if necessary, and keep the entire object outside the near plane and wall. LateUpdate follows position and yaw continuously. Full pitch/roll return when space permits. This explicitly changes the prior position-once behavior as requested. No resizing, new input action or rig modification. More/Close source bindings preserved. No-safe-space states disable visual components and BOTH raycasters until recovery; these states cannot be called successful visible placement.
2. **Portrait:** generator and migration use canonical AIC1954.326 image at its existing main path/GUID. Original JPG and metadata copied exactly; no separate downloaded portrait or importer change. Notice/asset inventory now withdraw any unsupported licence claim for old Portrait.png. The author reports that menu 6 completed; the submitted Detail Board prefab uses this texture GUID and the old image is absent from the current tree. The old file remains in existing Git history, so Cici must decide the permitted merge strategy.
3. **Dimensions:** generator uses73.6 × 92.3 cm. Menu6 changes only exact old dimension text in existing prefabs; no extra detail-panel line is added. Font, size and layout unchanged. The submitted Info Board prefab contains the corrected dimensions; the Detail Board had no old dimension string.
4. **Folder:** menu6 moves MuseumGenerated16 → Generated using AssetDatabase.MoveAsset and verifies folder/prefab GUIDs. Paths in tooling updated. Existing approved folders cannot be overwritten or duplicated by generator1. The submitted `Generated` folder is present; its folder GUID and child asset GUIDs match the former active folder. Other generated folders are untouched.

## Verification actually performed

- Read Cici's actual review comment6476084361 and current PR head/body.
- Read Gallery source/FBX importer: imported Gallery mesh requests colliders. Current Gallery has no old dimension text override; prefab update can propagate without scene save.
- Byte/SHA verification: canonical AIC JPG and all imported parent/image metadata match main. Existing serialized Unity assets remain byte-identical to base before migration; source mirrors match.
- `git diff --check`: PASS. Installer shell syntax: PASS.
- Guarded installer: exact baseline upgrade, idempotent rerun, unchanged serialized assets/meta, and mutation-free refusal of unrelated local edits: PASS in temporary fixtures.
- Guarded review-package installer: check-only, install, idempotent rerun and mutation-free refusal of unknown local edits: PASS in temporary fixtures.
- Repository checker self-tests: all18 PASS.
- `bash scripts/ci-local.sh HEAD`: PASS for baseline 910c82f (11 commits). The review changes still require the repository CI run after the new commit.
- Independent analytical planar-wall check at head height1.6m / More height.765m: all four requested distances admit a non-clipping full-size pose with tilt constraint. At.5m, roughly20° panel tilt and.46–.48m distance (instead of59° headset pitch) can fit. This is a calculation, not PhysX/Unity or a comfort test.
- Baseline merge-tree against current main clean. Other fetched open heads109/110/111 had no baseline conflicts; uncommitted docs/asset changes still need final merge/CI review once authorized. No team branch merged or modified.
- Three independent review angles: runtime/collision, assets/licensing, verification. Confirmed near-plane support, look-away interval and unsafe smoothing gaps were fixed. Multi-frame test now asserts rotation AND position, plus Canvas AND shell corners.

## Not performed / acceptance blockers

The author supplied Unity evidence from Unity 6000.3.23f1: compilation/import completed; menu 7 dependency validation passed; menu 8 multi-frame follow/collision/recovery and X/reopen checks passed. The Console showed zero errors and one expected no-safe-space warning from the fail-closed test fixture. Unity/ADB/device are unavailable in this executor, so these checks were not independently reproduced. Actual rendered Gallery review, manual mouse/XR controller interaction, clean-checkout build, Quest 3 Build And Run and performance are not verified. The two new Editor `.meta` files are present. Keep the PR draft until visual and device acceptance.

At very close distance the unchanged full-size panel occupies a large visual angle. Native Quest testing must assess text readability and X reachability under constrained tilt; arbitrary narrow corners cannot guarantee a visible pose without size/offset changes. Fallback hides rather than clips. Keep the draft until accepted.

## Files changed in source (both mirrors where shown)

- `Assets/ArtworkInfo/Runtime/ArtworkHeadFollow.cs` and `scripts/artwork-info/Runtime/ArtworkHeadFollow.cs`
- `Assets/ArtworkInfo/Runtime/ArtworkInfoDisplay.cs` and `scripts/artwork-info/Runtime/ArtworkInfoDisplay.cs`
- `Assets/ArtworkInfo/Editor/ArtworkFigmaPanel.cs` and `scripts/artwork-info/Editor/ArtworkFigmaPanel.cs`
- `Assets/ArtworkInfo/Editor/ArtworkInfoTools.cs` and `scripts/artwork-info/Editor/ArtworkInfoTools.cs`
- `Assets/ArtworkInfo/Editor/ArtworkGalleryTestTools.cs` and `scripts/artwork-info/Editor/ArtworkGalleryTestTools.cs`
- New: `Assets/ArtworkInfo/Editor/ArtworkInfoReviewMigration.cs` and `scripts/artwork-info/Editor/ArtworkInfoReviewMigration.cs`
- New: `Assets/ArtworkInfo/Editor/ArtworkInfoFollowChecks.cs` and `scripts/artwork-info/Editor/ArtworkInfoFollowChecks.cs`
- `Assets/ArtworkInfo/Design/NOTICE.txt`; new source notice `scripts/artwork-info/NOTICE.txt`
- `scripts/artwork-info/design-assets.json`, `install-source.sh`; new `review-baseline-hashes.json`
- `docs/ASSETS.md`, `docs/artwork-info/README.md`, `SUBMISSION_REVIEW.md`, new `REVIEW_FIXES.md`
- Reused from main: `Assets/VanGogh.meta`, `Assets/VanGogh/Art.meta`, `Assets/VanGogh/Art/VG_SelfPortrait_1887.jpg` and `.jpg.meta`

The prepared tree contains the migration results: folder rename with preserved GUIDs; two existing prefabs updated via PrefabUtility; old Portrait.png and metadata removed after dependency check; canonical AIC image reuses its main GUID; two new Editor `.cs.meta` files are present. Gallery scene is byte-identical to base. No ProjectSettings, Packages, Bedroom, Issue75, networking or Enter navigation changes.
