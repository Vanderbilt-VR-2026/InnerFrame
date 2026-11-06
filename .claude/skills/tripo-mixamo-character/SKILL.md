---
name: tripo-mixamo-character
description: Rig and animate a humanoid character for free: a Tripo model in a neutral 大 pose, auto-rigged in Mixamo, clips found by keyword and downloaded as FBX for Unity. Use when a scene needs a character that moves.
---

# Tripo → Mixamo character (v0)

**Status: v0, 4 Nov 2026, written from the first real run.** Vincent v2 went from a Tripo multi-view model to a Mixamo rig with eight clips, and Claude Code checked them in Blender (report on Cici's Mac: `cc-reports/2026-11-04-vincent-mixamo/`). Steps 1–5 are what was done there. Step 6 (Unity) hasn't run yet; update it after The Starry Night's step 3.

Tags: `[V]` done and checked in this project · `[I]` expected, not yet tried here · `[docs: …]` from the vendor's page, with the date it was read.

## What it costs

- **Mixamo is free.** It needs a personal Adobe ID, no Creative Cloud subscription. Characters and animations are royalty-free for personal, commercial and non-profit projects, games included. `[docs: Adobe Mixamo FAQ, 2026-11-04]`
- **Tripo is not free for us.** Outputs made on a free plan stay Tripo's (User Agreement §5.2.1). A paid plan gives you the rights, including publishing (§5.2.2). `[checked 2026-10-27]`
  - Don't buy a plan just for one character: **Cici and Charlyne have paid plans.** Send one of them your reference sheet and they generate the model (step 2).
  - Ask first; their credits are limited.

So the whole pipeline costs the person asking nothing.

## Who does what

| Step | Who | Where |
|---|---|---|
| 1. Reference sheet | you; the character's look owner approves it | ChatGPT or a drawing |
| 2. Generate | Cici or Charlyne | Tripo web app (paid plan) |
| 3. Rig | you | mixamo.com |
| 4. Animations | you | mixamo.com |
| 5. Check (optional) | Claude Code | headless Blender on a Mac |
| 6. Unity | the scene's owner | Unity Editor |

## Gates before starting

- **Humanoids only.** Mixamo's auto-rigger only rigs two-legged humanoids. The character needs:
  - a distinguishable head, body, arms and legs;
  - no wings, tails, extra limbs, big hair or big clothing.

  The file holds one model, centred at the origin, with no gaps or floating parts. `[docs: Adobe Mixamo FAQ, 2026-11-04]` For animals and props, use something else.
- **Adobe account.** Mixamo doesn't open with Enterprise or Federated IDs, and school-managed Adobe accounts are often one of those. It also doesn't open for accounts with a China country code. `[docs: Adobe Mixamo FAQ, 2026-11-04]` If your account can't open Mixamo, ask a teammate whose account can to do steps 3–4.
- **Our repo is public.**
  - Adobe's FAQ allows use in games but doesn't say whether the raw files may be published. `[docs: Adobe Mixamo FAQ, 2026-11-04]`
  - A 2022 community FAQ (not Adobe staff) says raw character and animation files must not be given to anyone outside the team. `[docs: Adobe Community, 2022-09-29]`
  - Until the team has checked with Molvig and decided, keep Mixamo files out of the repo, in a folder outside it, e.g. `CS-VR/refs/<name>-mixamo/`.
- **No face.** Mixamo makes body bones only: no jaw, no eyes. Vincent v2 has 33 bones and none of them is on his face. `[V]` A talking face needs its own plan. InnerFrame's plan is a jaw bone and eyelid cards, after Sonya's Convai answer.

## 1. Reference sheet: the 大 pose

The pose you generate is the pose Mixamo rigs. A character modelled holding things or with bent arms can't be rigged. The first Vincent (10/26) held a brush and a palette fused into his hands, so he can only stay a statue.

- **Body:** standing straight, arms out like 大, in a T-pose (arms horizontal) or an A-pose (arms about 45° down). Leave clear gaps under the arms and between the legs.
- **Hands:** open, **fingers spread, thumb away from the hand.** Fingers that touch fuse in the mesh, and fused fingers give mitten hands: one finger chain, no thumb. Vincent v2 got mittens and can't point a finger. `[V]`
- **Feet and face:** feet a little apart and flat. Face forward, neutral, mouth closed.
- **Nothing extra:** nothing in the hands, no hat, no bag, no cape. Props are separate models attached in Unity, so one character works in every scene.
- **Clothes:** fitted clothes rather than long, loose ones. Vincent's jacket tails balloon when he crouches (Sit To Stand), and close into a shell under him when he sits on nothing. `[V]`
- **Four views at one scale:** Front, Left, Right, Back. Left and Right are the character's own sides, not the viewer's. Use flat, even light on a plain grey background, with no shadows.

The look's owner approves the sheet before anyone spends credits.

## 2. Generate in Tripo (Cici or Charlyne, paid plan)

- **Settings:** multi-view to 3D with the four views; Smart Mesh, quad topology. Vincent v2 has 10,619 triangles. `[V]`
- **Budget:** about 10–15k triangles for a full body, one 2K texture.
- **Counting:** a quad face count isn't a triangle count. The cypress was 5,999 faces and 9,401 triangles. `[V]`
- **Check the untextured mesh first:** arms and legs apart from the body, fingers apart, nothing floating. If any of that fails, regenerate before texturing.
- If Tripo offers a T-pose option at generation, turn it on. `[I: Tripo's blog mentions one; not tried here]`
- **Export FBX with the Mixamo preset,** with textures embedded. Tripo's FBX export has presets for Blender, Mixamo and 3ds Max. `[docs: Tripo API fbx_preset, read in the tripo-api crate's source, 2026-11-04]` Mixamo takes FBX, OBJ or a ZIP, not GLB. Export the GLB too, as a backup.
- **What to expect from the FBX,** from Vincent v2's: `[V]`
  - he came in about 1.00 m tall, whatever the character's real height;
  - Z-up, in centimetres;
  - Metallic 1.0 in his material.

  Mixamo doesn't mind; step 6 fixes all three in Unity.
- **Send the files with a short note:** who generated it, on which plan, the settings, the face and triangle counts, the texture size, the date. That note is the licence evidence for `docs/ASSETS.md`.

## 3. Rig in Mixamo (you, free)

1. **Upload.** On mixamo.com, sign in, then **Upload Character**, and drop in the FBX. Keep it under about 50 MB. `[docs: Cinevva and Neural4D rigging guides, 2026-11-04]`
2. **Place the markers** (the "paint every part" step):
   - **chin:** the centre of the jaw, not the neck;
   - **wrists, elbows and knees:** both sides, at the joint's centre;
   - **groin.**

   Zoom in for each one, and keep **Use Symmetry** on.
3. **Skeleton LOD: Standard Skeleton (65),** the one with full fingers. If the hands still come out as a single finger, the mesh's fingers are fused. Either accept mittens (point with a prop, step 6) or go back to step 1.
4. **Check the preview.** Click Next and wait, up to about 2 minutes. Mixamo then plays a preview: watch the armpits, elbows, knees and clothes. If something tears, go Back and move the markers.
5. **Download the rigged character right away.** Click Download and choose:
   - Format **FBX for Unity (.fbx)**;
   - Pose **T-pose**;
   - Skin **With Skin**. `[V]`

   Mixamo keeps only the last character you uploaded. `[docs: Adobe Mixamo FAQ]` Upload another one first, and this one is gone.
6. **Adding clips later:** upload this rigged FBX again. Mixamo accepts an already-rigged character, and the rig is kept. `[docs: Adobe Mixamo FAQ]` That it skips the markers is `[I]`. Share the rigged FBX inside the team, so anyone can add clips without placing markers again.

## 4. Animations (you, free)

1. **Search the library by keyword.** What Vincent needed:

   | Need | Search | Vincent v2 used `[V]` |
   |---|---|---|
   | standing idle | idle, breathing | Breathing Idle |
   | sitting idle | sitting | Sitting Idle |
   | talking | talking, sitting talking | Sitting Talking (the standing Talking is still to download) |
   | standing up, sitting down | sit to stand, stand to sit | Sit To Stand |
   | gesture | pointing, waving, gesture | Pointing |
   | walking | walking, turn | Walking, Walking Left Turn, Walking Turn 180 |

2. **Set the clip's controls on the right before downloading:**
   - **In Place:** shown only on clips that travel, such as Walking. Tick it, or he walks off and Unity has to fix it. Vincent's Walking was downloaded without it and travels 0.94 m per cycle. `[V]`
   - **Character Arm-Space:** widen it if the hands or arms pass through the body or the clothes.
   - **Trim** (cut the start or end), **Overdrive** (speed) and **Mirror**, as needed.
   - Clips that sit, stand or turn move the body anyway; Sit To Stand moves 0.25 m forward. `[V]` Unity handles that (step 6).
3. **Download each clip** with these settings: `[V]`
   - Format **FBX for Unity (.fbx)**;
   - Skin **Without Skin**: the motion only, no mesh or texture, so the files stay small and there's only one character;
   - Frames per Second **30**;
   - Keyframe Reduction **none**.
4. **Name and keep them together, the Unity way:**

   ```
   CS-VR/refs/<name>-mixamo/            (outside the repo)
     <name>-rigged.fbx                  the With Skin character
     <name>@SittingIdle.fbx             one per clip: CamelCase, no spaces
     <name>@Walking.fbx
     source/                            Tripo's FBX and GLB
     README.md                          who made what, the Tripo plan and settings, the download settings, the date
   ```

## 5. Check (optional; Claude Code, headless Blender)

Before Unity, have Claude Code gather, rename and check the files. Its checks:
- **the character:** bones, height, T-pose, unweighted vertices;
- **each clip:** length, whether it loops, how far the root travels, and the seat height for sitting clips;
- **renders** of where the mesh breaks.

Ask Cici for the 11/04 check prompt; it's written for her Mac's folders, so the paths need changing.

- **Seats:** sitting clips assume a seat at a fixed height relative to the body. Vincent's is 0.185 of his height: 0.26 m when he's 1.40 m tall. `[V]` Build the seat to match the clip, not the clip to the seat.

## 6. Unity (the scene's owner) `[I: not yet run]`

Cici's quest-asset-import skill has the general import steps for this project (folders, LFS, importer settings by script, budget); ask her. For a Mixamo character:

- **Character FBX:** Rig → Animation Type **Humanoid**; Avatar Definition **Create From This Model**. If Unity says the character isn't in a T-pose: Configure → Pose → Enforce T-Pose.
- **Clip FBXs:** Humanoid, then **Copy From Other Avatar** with the character's avatar. Tick Loop Time on idles and talking. For clips that travel, choose root motion or Bake Into Pose.
- **Scale:** from Tripo's 1.00 m to the character's real height (Vincent: ×1.70 for 1.70 m). Set it on the importer, by script.
- **Material:** Metallic 0, since Tripo's FBX gives 1.0 and that makes him dark and mirror-like. Texture 2048.
- **Quest:**
  - the Mobile quality level uses Skin Weights 2, while Mixamo gives up to 6 per vertex and Unity keeps 4;
  - elbows and hips may crease, so check them in the headset;
  - changing QualitySettings affects the whole team, so agree on it first.
- **Mitten hands, looking, pointing:** Unity's Humanoid IK turns the head to look at a target and moves a hand to a target. A prop held in a mitten hand can do the pointing.
- **Licence rows** in `docs/ASSETS.md`: Tripo (who, paid plan, §5.2.2) and Mixamo (Adobe ID, date). Add them only once the repo question is answered.

## Don't

- Don't generate on a free Tripo plan: the model wouldn't be ours.
- Don't model props, hats or bent arms into the character.
- Don't upload a second character before downloading the first one rigged.
- Don't download every clip With Skin: one character, many motions.
- Don't commit Mixamo files to the public repo until the team decides.

## Sources

- Adobe, Mixamo FAQ (free with an Adobe ID; royalty-free use; humanoid requirements; account types; last character only): https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html, read 2026-11-04
- Adobe Community, "Mixamo FAQ: licensing, royalties, ownership, EULA and TOS" (2022-09-29, a community member, not Adobe staff): https://community.adobe.com/questions-696/mixamo-faq-licensing-royalties-ownership-eula-and-tos-589400
- Tripo User Agreement, last updated 2025-07-11, §5.2.1 and §5.2.2, checked 2026-10-27
- Tripo API, model conversion, `fbx_preset` (Blender/Mixamo/3DsMax), read in the tripo-api crate's source: https://docs.rs/crate/tripo-api/latest/source/src/tasks/convert_model.rs, 2026-11-04
- Tripo blog, "How to rig an AI-generated character for Mixamo" (the T-pose option, the marker positions): https://www.tripo3d.ai/blog/rig-ai-generated-character-for-mixamo
