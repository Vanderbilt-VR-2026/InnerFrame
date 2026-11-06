---
name: prop-to-3d
description: Make a VR prop (e.g. an easel, palette, brush, a piece of furniture) as a scripted low-poly Blender build or a Tripo model from styled reference views, cleaned to budget with stable names, a painted look and an FBX. Use after asset-triage picks Build or Tripo for an object.
---

# Prop to 3D (v0.1)

**Status: v0.1, 27 Oct 2026.** Route A was used once, on 26 Oct 2026 (Vincent's easel, palette and brush). Route B hasn't been tried in this project yet: its settings come from Sac's Little Prince article (15 Oct 2026) and are tagged `[I]`. Update this file after each run.

Steps 3 and 4 of the `asset-pipeline` skill. The route comes from `asset-triage`. It ends with an FBX and its `.blend` source, ready for `quest-asset-import`. Nothing enters the repo in this skill.

Before starting: the object's owner agrees. A refined version of an existing room object is the room owner's call, and it keeps the existing object's name.

## Route A: Build (a scripted Blender model)

For simple shapes: boxes, cylinders, planes. A coding agent writes one headless Blender script per prop and runs it with `/Applications/Blender.app/Contents/MacOS/Blender -b -P build_<prop>.py` on macOS (precedent: `Art/Source/Gallery/build_gallery.py`).

1. **Parameters at the top of the script**: real-world dimensions in metres (with the source of each, or `[I]` if estimated) and segment counts.
2. **Named parts** (e.g. `VG_Easel`, with a child `VG_Canvas`). Names are stable once the prop is in Unity.
3. **Low poly**: a few hundred triangles (26 Oct: easel 108, palette 242, brush 94). Bevels only where the player looks closely.
4. **UVs**: smart project is enough. Give a canvas or label face clean 0–1 UVs, so an image can go on it later.
5. **A painted look, not flat colour.** On 26 Oct, flat-coloured props looked out of place next to the painted walls. Either:
   - make a texture from the object's styled reference views (`asset-pipeline`, step 2); or
   - use the room's own painted materials, if the room's look owner (#30, Charlyne) has a way to apply them.
6. **Pivot**: bottom centre for floor props.
7. **Hand-held props**: add an empty named `GrabPoint` where the palm holds it. On 26 Oct, Unity aligned each prop by its `GrabPoint` to the holder's hand, with a per-prop rotation, and it lined up on the first try. Grabbable props get their attach point from it at import.
8. **No colliders in the FBX**; `quest-asset-import` adds simple ones in Unity.

## Route B: Tripo (detailed or sculpted props)

1. Gates as in `character-to-3d`: the team's yes to Tripo (26 Oct, each member's own plan), the plan's terms recorded with the URL and date, the budget from triage.
2. Reference views: `asset-pipeline`, step 2. For a simple prop, one styled front view on a plain background can be enough; for anything round or detailed, use four views.
3. Sac's prop setting `[I]`: model H3.1 for detail. Set the polycount near the budget, the texture to 2K or less, and "Remove Lighting" on if offered.
4. Record settings, attempts, credits, counts and the privacy setting in the local notes (outside the repo).
5. Clean up in headless Blender: apply transforms, real-world size, decimate to budget (report before and after), one material, stable names, pivot and `GrabPoint` as in route A.

## Both routes

- **Export FBX**: Apply Scalings "FBX All", Forward −Z, Up Y, Apply Transform on (Blender is Z-up, Unity is Y-up).
- Keep the `.blend` and its script under `Art/Source/<Feature>/`, never under `Assets/` (Unity can only import a `.blend` where Blender is installed).
- Render a three-quarter view and a side view to PNG in the local folder, and report triangle and material counts.

**STOP:** show the person running this the renders and the counts before import.

## Don't

- Don't use Tripo for something a 20-line script can build and that the player won't look at closely.
- Don't model anything that already exists in the scene or belongs to another person's lane without that person's OK. Check the triage first.
