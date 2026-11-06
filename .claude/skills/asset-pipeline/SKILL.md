---
name: asset-pipeline
description: Make or improve one object in InnerFrame end to end, one item at a time - decide how it gets made, generate multi-view reference images with ChatGPT in the room's painted style, generate it with Tripo or build it in Blender, clean it up, and bring it into Unity for Quest. Use when someone wants to add or refine an object, prop or character.
---

# Asset pipeline (v0.1)

**Status: v0.1, 27 Oct 2026.** Run end to end once, on 26 Oct 2026, for Vincent and his easel, palette and brush (`feat/vangogh-talk`). Not yet tested on Quest. After each real run, update this file and the step skills with what actually happened.

Every object gets its own design pass ("per item"): what it is for, who owns it, how close the player gets, and how it will look next to the painted room. The steps:

| Step | Skill | Who | Result |
|---|---|---|---|
| 1. Triage | `asset-triage` | anyone; each object's owner agrees | one row per object: route, budget, owner, licence |
| 2. Reference views | this file, below | the object's owner | four consistent views in the room's painted style |
| 3a. Generate | `character-to-3d`, or `prop-to-3d` route B | the person with a Tripo plan | raw model plus generation notes |
| 3b. Build | `prop-to-3d` route A | a coding agent, headless Blender | scripted low-poly model |
| 4. Clean up | `character-to-3d` / `prop-to-3d` | a coding agent, headless Blender | FBX plus `.blend` source, on budget, stable names |
| 5. Import | `quest-asset-import` | a coding agent, Unity in batchmode | runtime-spawned prefab, licence row, PR |

## Rules for every step

- **Owners.** An object in someone else's lane is changed only with that person's OK. Check the board or the team plan for the current owner. As of late Oct 2026: the room's 37 named objects and the room's look (#30, #32–#36) are Charlyne's; the Bedroom scene and the portrait (#57) are Rowling's; Vincent and his props are Cici's; anything shared between players goes through Sonya.
- **Generators.** The team agreed on 26 Oct 2026 to use Tripo; each member uses their own plan. For every generator (Tripo, ChatGPT images), record in `docs/ASSETS.md` what its terms say about the output (who owns it, whether it may sit in this public repo), with the URL and the date. If you haven't checked, write "NOT verified; check before push" and check before pushing.
- **Never replace an existing object.** Scripts and physics data find objects by name; refine the owner's object with them instead.
- **What enters the repo.** Only the cleaned FBX, its textures, the `.blend` source and its build script, plus approved reference views if the team wants them versioned. Generation notes, raw downloads, renders and reports stay in a local folder outside the repo.
- **Stops.** Stop before every commit. Push only when the person running the work says so for that push.

## Step 2: reference views with ChatGPT

Tripo's multi-view mode turns four consistent views into a model, so the views decide how the model looks. Make them match the room here, not later. On 26 Oct both the Tripo Vincent and the scripted props looked out of place next to the painted walls.

1. **Collect references.**
   - Style: *The Bedroom* (Art Institute of Chicago, CC0), already in the repo at `Assets/Art/Painting/The_Bedroom_Van_Gogh.jpg`, plus a screenshot of the object's spot in the Painted Bedroom.
   - Content: for a historical subject, public-domain sources such as museum open-access pages. List each source and its licence.
   - Never upload an image you have no right to use.
2. **Generate the front view first**, then ask for the other views of the same object. Template:

   ```
   A <object>: <material, size, the details that matter>, for a VR scene inside Van Gogh's "The Bedroom".
   Paint it in the style of the attached painting: thick, visible brushstrokes and the same colours
   (pale violet walls, red floor tiles, butter-yellow wood, lemon green, scarlet).
   Front view. The whole object in frame and centred, plain light-grey background,
   even flat lighting, no cast shadows, no text, nothing else in the picture.
   ```

   Then, one at a time: "Now the same <object>, identical in shape, size, colours and brushwork, seen from the three-quarter left / the left side / the back. Same background and lighting."
   For a character, add: neutral expression, mouth closed, arms a little away from the body (or the pose the scene needs), whole figure in frame.
3. **Check the four views side by side**: same proportions, same colours, nothing added or missing. Regenerate any view that drifts; Tripo copies inconsistencies into the mesh.
4. **The object's owner approves the views** before anyone spends Tripo credits.
5. **Record** in the local notes: the ChatGPT model, the date, the prompts, the number of attempts, and the reference images used.
6. If the team wants the approved views versioned, keep them next to the `.blend` source in `Art/Source/<Feature>/references/` (PNG, stored with LFS); otherwise keep them in the local folder.

## Steps 3 to 5

Follow `character-to-3d` (people) or `prop-to-3d` (objects), then `quest-asset-import`. Before marking the PR ready, run the repo's `adversarial-review` skill.

## What the first run taught us (26 Oct 2026)

- Tripo Smart Mesh P2.0, multi-view: a full-body Vincent, 10,193 triangles, one 2K texture. His front faced Blender −Y and came into Unity facing +Z.
- Scripted props (easel 108 triangles, palette 242, brush 94) with a `GrabPoint` empty lined up in his hands on the first try. But flat colours looked out of place next to the room: a built prop needs a painted texture, or should be generated from styled views.
- Scale things to the player, not to the doors. The XR rig is scaled 0.8 in the Painted Bedroom, so a person's eyes belong at 1.60 m × 0.8 = 1.28 m in world units. A rule based on the room's 2.73 m door made Vincent feel 2.9 m tall.
- Batchmode renders from the player's arrival point at eye height let the agent check placement and scale before anyone opens the editor.
