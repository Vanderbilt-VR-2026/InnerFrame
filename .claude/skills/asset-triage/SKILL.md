---
name: asset-triage
description: Decide, object by object, how each thing in an InnerFrame VR scene gets made (keep the existing one, build it in Blender, generate it with Tripo, use a 2D image, or fake it), with budget, owner and licence. Use before modelling or refining anything for a scene.
---

# Asset triage (v0.1)

Turns a storyboard, a screenshot or a scene proposal into a list of every object the scene needs and how each one gets made. Step 1 of the `asset-pipeline` skill. Adapted from Sac's Little Prince workflow (article of 15 Oct 2026: mark which assets Tripo generates and which are built directly), then changed by InnerFrame's first run on 26 Oct 2026. **The output is a proposal; it decides nothing.**

## Inputs

- The storyboard, a screenshot of the scene, or the scene proposal.
- Current facts, read fresh, never from memory: the team plan and the board (owners, sprint scope), `docs/ASSETS.md` (what exists and its licence), `AGENTS.md`, and the latest frame-rate and draw-call numbers (#98). Quote numbers with their dates.

## Steps

1. **List every object** seen or used, including small ones (a palette, a brush) and things that aren't meshes (a reflection, the images on a canvas). Number them.
2. **Put each object in a route:**

| Route | When | Starting budget `[I]` |
|---|---|---|
| Existing | Already in the scene or owned by someone (e.g. the Painted Bedroom's 37 named objects) | keep; its owner decides any refinement |
| Build | Simple, background or purely functional shapes: frames, ledges, simple furniture parts, colliders | a few hundred to ~2k triangles |
| Tripo | Things the player looks at closely or holds, and characters; made from styled multi-view references (`asset-pipeline`, step 2) | character ~10–15k triangles, prop up to ~5k; one 1–2K texture |
| 2D | Flat things: paintings, canvas stages, plaques, posters | texture size; licence |
| Fake / skip | Too expensive on Quest or not needed (real-time reflections, cloth simulation) | say what replaces it |

   The budgets are starting points. Check them against the scene's current numbers.

3. **For each object, record**:
   - why this route (one line);
   - budget;
   - owner (lane);
   - licence (a CC0 source, our own work, or the generator's terms; if unverified, say so);
   - where it will live;
   - whether it needs a decision from the team or its owner.

   Where things live: `.blend` sources under `Art/Source/<Feature>/`; FBX and textures under `Assets/<Feature>/Art/`; in Unity, a prefab spawned at runtime from its own folder, never saved into a generated scene (a re-import rebuilds those).
4. **Rules that override the table:**
   - Never propose replacing an existing object: scripts and physics data find objects by name.
   - An object in another person's lane: list it, mark "owner: <name>", and leave its plan to them.
   - Style has to match whichever route you pick. A built object needs a painted texture, because flat colours stand out next to the painted room (26 Oct). A generated object needs styled reference views.
   - Prefer Build when the shape is simple and the player won't look closely. Prefer Tripo when the player will.
   - Generator settings from Sac's article (H3.1 for detailed props; Smart Mesh P2.0 with multi-view input and quad topology for characters) are his reports, except Smart Mesh P2.0 multi-view for a character, which this project used on 26 Oct. Tag the untried ones `[I]`.
5. **Make an annotated picture**: the frame or the floor plan with numbered markers coloured by route (Tripo = red ring, Build = blue ring, 2D = blue dashed ring, Existing = grey ring, Fake = grey dashed ring), with a legend. Attach it to the PR or to the team's doc.
6. **Reply** with the table, then 2–3 lines: what needs a decision (and from whom), and what can start now.

## Table columns

`# · Object · Route · Why · Budget · Owner · Licence · Lives in · Decision needed?`

## Example: Vincent's corner of the Bedroom (26 Oct 2026, as built)

- Room furniture (37 objects): Existing; owner Charlyne.
- Vincent, full body: Tripo (team yes, 26 Oct); 10,193 triangles, one 2K texture; licence for a public repo recorded as unverified.
- Easel, palette, brush: Build; 108 / 242 / 94 triangles, flat colours. They looked flat in the room, so next time they get a painted texture, or Tripo from styled views.
- Portrait in the Gallery: 2D; the 1887 self-portrait, Art Institute of Chicago, public domain.
- Canvas stages for the mirror scene (later): 2D, made from a public-domain self-portrait.
- The mirror's reflection: Fake; no second render on Quest.

## Don't

- Don't present the triage as decided. It's a proposal until the team and each owner agree.
- Don't copy another project's workflow wholesale. Keep what fits this project's pipeline, lanes and budgets, and say what you changed.
