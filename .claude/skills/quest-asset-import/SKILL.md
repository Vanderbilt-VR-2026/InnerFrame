---
name: quest-asset-import
description: Bring a new or refined 3D object into InnerFrame's Unity project for Quest - feature folder, LFS, importer settings and prefab by an editor script, runtime spawn and placement, scale for the player, budget and licence checks, PR. Use after character-to-3d or prop-to-3d hands over an FBX.
---

# Quest asset import (v0.1)

**Status: v0.1, 27 Oct 2026.** Used once, on 26 Oct 2026, for Vincent and his props (`feat/vangogh-talk`: `Assets/VanGogh/`). Imported, placed and built into an APK. Not yet tested on Quest. Update this file after each run.

Step 5 of the `asset-pipeline` skill. Input: an FBX (plus textures) and its `.blend` source from `character-to-3d` or `prop-to-3d`.

## Guardrails (restate them in every prompt)

- Unity **6000.3.23f1** only, in batchmode where possible. Unity must be closed on a folder while git changes files in it.
- Work on a new branch (`feat/<feature>-<asset>`) from `origin/main`, ideally in its own git worktree, so the main clone isn't switched under an open editor.
- Stage files by name. Never hand-edit Unity YAML or `.meta` files: change assets and settings through Unity's editor API.
- A shared setting changes only if the PR body names the change and the reason (`AGENTS.md`).
- Never push unless the person running the work says so for that push.
- Reports, renders and scratch files go to a local folder outside the repo.

## Steps

1. **Place the files.**
   - FBX and textures under `Assets/<Feature>/Art/<Asset>/`; the `.blend` and its build script under `Art/Source/<Feature>/` (outside `Assets/`).
   - Check that `.gitattributes` covers every extension in any letter case. LFS patterns are case-sensitive on Linux CI, and downloads often have uppercase extensions like `.FBX`. Then run `bash scripts/check-assets.sh <branch>`.
2. **Importer settings by a committed editor tool.**
   - One small, idempotent script per feature: `Assets/<Feature>/Editor/<Feature>AssetSetup.cs`, with a menu item (Tools > InnerFrame > <Feature> > Rebuild assets) that also runs with `-executeMethod`. Example: `Assets/VanGogh/Editor/VanGoghAssetSetup.cs`.
   - Settings: Read/Write off; mesh compression Medium; no cameras, lights, animation or generated colliders; scaled so the model has its real height (check the bounds); one URP Lit material using the base colour texture (max 2048, mipmaps on, default Android compression).
   - Read the settings back as evidence. Running the tool a second time must change nothing.
3. **Prefab, built by the same tool.**
   - If it's spawned at runtime with `Resources.Load` (the `PlayerMenu` pattern), put it under `Assets/<Feature>/Resources/`. Don't leave an empty `Prefabs/` folder behind: it leaves an orphan `.meta`.
   - Add simple colliders on the Default layer, never the Teleport layer, so nobody teleports into the object. A speaking character gets an `AudioSource` at mouth height. A grabbable prop gets `XRGrabInteractable`, with its attach point at the `GrabPoint` empty.
4. **Runtime spawn.**
   - A small static bootstrap (`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` plus `SceneManager.sceneLoaded`) spawns the prefab when its scene loads, and finds room objects by name. `PlayerMenu.cs` is the pattern; `Assets/VanGogh/Scripts/VanGoghSpawner.cs` is an example.
   - Never save into `Scenes/PaintedBedroom.unity` or `Generated/` (a re-import rebuilds them), or into the shared `Gallery.unity`. Runtime spawn also keeps the object easy to move or remove.
5. **Placement and scale.**
   - Positions: take them from a read-only dump of the scene (a throwaway editor script that opens it without saving and lists names and bounds). Keep at least 0.4 m from doors, windows and furniture, and stay off the player's main path.
   - Scale for the player. In the Painted Bedroom the XR rig is scaled 0.8 at load (`PaintedBedroomPhysicsTable.asset`, applied by `PaintedBedroomPhysicsApplier.cs`). The player's eyes are therefore about 1.60 m × 0.8 = 1.28 m in world units `[I]` (a standing adult, floor tracking).
     - A person, or anything you face or hold, should feel right to the player's body: put a character's eyes at the player's eye height.
     - Furniture-like objects should instead match the neighbouring room objects.
     - On 26 Oct a door-based rule made Vincent feel 2.9 m tall.
   - Look before handing over: render to PNG in batchmode (a throwaway script with a camera at the arrival point, at the player's world eye height, plus one face to face), and check the facing, the scale and the props.
6. **Look.** Use the object's own materials. Joining the room's look (#30: colour grading, ink outlines), or touching shared URP or quality assets, needs the look owner's OK (Charlyne) and a named change in the PR.
7. **Networking.** If players share the object, agree with the multiplayer owner (Sonya) first. Known conflict: `NetworkGrabbable` resets `isKinematic` and fights the room's `Movable`.
8. **Budget.** The tool or a throwaway script reports what you added: triangles, materials, renderers and texture memory (26 Oct: Vincent with props 10,637 triangles, 5 renderers, one 2K ASTC texture of ~2.4 MB). Then test on a Quest from a clean checkout: fps with post-processing on, with the object in view. Compare against the latest room numbers (#98).
9. **Licence.** Add a row to `docs/ASSETS.md` for each new top-level `Assets/` folder and each new `Art/Source/` folder: the source, the generator and whose plan, what its terms say (URL and date) or "NOT verified; check before push", and the date.
10. **Checks.**
    - `bash scripts/ci-local.sh <branch>` must say PASS. It checks the branch merged into `main`, and every commit.
    - Build noise: every player build removes two XR entries from `preloadedAssets` in `ProjectSettings/ProjectSettings.asset`, because `main` has a mid-build state committed. After a build, restore that file from HEAD, then re-apply any intended setting through the API. Never commit the noise.
    - Input: the XRI rig binds the right controller's A button to Jump, so pick another button for features (Vincent listens on B).
11. **STOP before commit.** Show `git status` and the diff stat. Split the work into commits that each work alone. For example, guard against a missing prefab, so that a commit with only the scripts still runs.
12. **PR body**, as markdown:
    - what was added and where;
    - the budget measured;
    - `[V]` or `[I]` on every claim, and "works on Quest" only after a headset test;
    - shared settings named;
    - the scene owners' OK asked (`AGENTS.md`).

## Cleanup

- Remove throwaway editor scripts with their `.meta` files and any folders they created, plus debug logging.
- After the PR merges, remove the task's worktree.

## Don't

- Don't commit an asset whose licence isn't recorded in `docs/ASSETS.md`.
- Don't place an object by editing a scene; spawn it at runtime.
