---
name: character-to-3d
description: Turn a character (e.g. Vincent, full body or a bust) from approved multi-view reference images into a cleaned, on-budget FBX plus its .blend source, via Tripo and headless Blender. Use after asset-triage picks Tripo for a character.
---

# Character to 3D (v0.1)

**Status: v0.1, 27 Oct 2026.** Used once, on 26 Oct 2026, for a full-body Vincent (no rig). Settings that this project hasn't tried yet come from Sac's Little Prince article (15 Oct 2026) and are tagged `[I]`. Update this file after each run.

Steps 3a and 4 of the `asset-pipeline` skill. It ends with a cleaned FBX and its `.blend` source, ready for `quest-asset-import`. The roles: a person with a Tripo plan works the Tripo web app; a coding agent runs Blender headless; nothing enters the repo in this skill.

## Gates before starting

- **Team and owner.** Tripo was agreed by the team on 26 Oct 2026; each member uses their own plan. The character's owner (Vincent: Cici; his portrait #57: Rowling) agrees to this character being made.
- **Licence.** Read your plan's terms: who owns the output, whether free-tier models are public, whether a model may sit in this public repo. Write the answer down with the URL and the date. It goes into `docs/ASSETS.md` at import.
- **Budget**, from `asset-triage`. Default `[I]`: a bust ~10–15k triangles; a full body around the same (Vincent: 10,193); one material; one 1–2K texture.
- **Scope.** A standing figure in a fixed pose, or a bust, needs no skeleton. Animation (walking, gestures) needs rigging and clips (Sac: Auto-Rig Pro, a paid add-on, plus Mixamo); only do that if the team plan asks for it.

## 1. Reference views

Follow `asset-pipeline`, step 2: four consistent views in the room's painted style, generated with ChatGPT from public-domain sources, and approved by the character's owner before any credits are spent.

## 2. Generate (in the Tripo web app)

- Multi-view to 3D, uploading the four views.
- Used on 26 Oct: Smart Mesh P2.0, multi-view, Privacy set to Private. Result: 10,193 triangles, one 2K texture.
- Sac's other settings `[I]`: quad topology, highest precision, "Remove Lighting" on if offered.
- Set the polycount near the budget and the texture to 2K.
- Check the untextured mesh first, and fix wrong parts ("edit mesh") before texturing.
- Export FBX (or GLB).
- **Record in the local notes** (outside the repo): model and settings, attempts, credits spent, triangle and vertex counts, texture size, privacy setting, export format.

## 3. Clean up (a coding agent, headless Blender)

Run Blender headless, e.g. `/Applications/Blender.app/Contents/MacOS/Blender -b -P <script.py>` on macOS (precedent: `Art/Source/Gallery/build_gallery.py`). Keep the script small and explainable (course AI policy), and put every step in it, so the next person can rerun it.

1. Import, then apply all transforms.
2. **Real-world size**: a full figure at its real height (Vincent: 1.70 m), origin between the feet; a bust about 0.23 m chin to crown `[I]`, origin at the base centre. Don't bake the room's scale in; `quest-asset-import` sets the size in the room.
3. **Facing**: front along Blender −Y. It comes into Unity facing +Z (checked on 26 Oct).
4. Decimate to the budget; report the triangle count before and after.
5. One material; texture downsized to the budget.
6. **Stable names** (e.g. `VG_Vincent`). Names never change after import: scripts find objects by name.
7. If the character holds props, note where the hands are. On 26 Oct the hands were estimated from the mesh, and each prop's `GrabPoint` empty was aligned to them.
8. Mouth movement for speech, chosen after seeing the mesh `[I]`:
   - A. split a jaw piece along the mouth line, with its pivot at the jaw hinge, rotated in Unity by voice loudness;
   - B. a `MouthOpen` shape key;
   - C. no mouth movement; the head nods while he speaks.
   Not done yet for Vincent.
9. **Style**: the room's look (#30: colour grading, ink outlines) is Charlyne's. Ask her how a new character joins that look before repainting textures by hand.
10. **Export FBX**: Apply Scalings "FBX All", Forward −Z, Up Y, Apply Transform on. The defaults import rotated or scaled wrong, because Blender is Z-up and Unity is Y-up.
11. Save the `.blend` and its script under `Art/Source/<Feature>/`, never under `Assets/`: Unity can only import a `.blend` on machines with Blender installed. The FBX and textures stay in the local folder until import.
12. Render front, side and back views to PNG in the local folder, next to the reference views.

**STOP:** show the person running this the before/after counts, the renders next to the references, and the chosen mouth option.

## Then

Hand the FBX, its textures and the `.blend` to `quest-asset-import`.

## Don't

- Don't spend credits before the views are approved.
- Don't commit a generated model whose licence isn't recorded in `docs/ASSETS.md`.
- Don't hand-tune in the Blender GUI without saving the steps into the script.
