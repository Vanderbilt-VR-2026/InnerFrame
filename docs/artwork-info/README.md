# Museum placard and light frosted-glass panel — Issue #14 / Draft PR #112

Source generation implementation, awaiting Unity 6000.3.23f1 compilation,
asset generation, visual review and Play Mode/device tests. **The C# source
is not itself a completed prefab.** Gallery (Cici), Painted Bedroom (Rowling),
transition controls, shared settings, and Issue #57 remain untouched.

## Update the prototype already installed on your Mac

1. Exit Play Mode. Save your work. Pull `feature/14-artwork-info-display`.
2. At the repository root run:
   ```bash
   bash scripts/artwork-info/install-source.sh --update-source
   ```
   This updates C# only, accepts the exact initial PR #112 source or the last
   installed hashes, and refuses locally edited files before copying anything.
   Preserve and compare reported local edits yourself; do not delete them to
   bypass this safeguard. Existing `.meta`, scenes, prefabs and materials are
   preserved. First-time installs omit `--update-source`. Requires Python 3.
3. Let Unity finish importing/compiling. Fix any Console errors before going
   on. TMP Essentials must already be imported, as in your current setup.
4. Optional: select an existing, licensed **TMP font asset** in Project before
   generation for a museum serif typeface. Otherwise the project default TMP
   font is used; no new font is downloaded. A default sans font will not match
   the reference's serif title exactly. Import a licensed serif yourself and
   select its TMP asset to generate a matching hierarchy. Record its license
   in `docs/ASSETS.md` before committing a new font.
5. Choose **Tools > InnerFrame > Artwork Info > 1. Generate Standalone Museum
   Assets**. Save prompts protect the currently open scene. Every run creates
   a unique `Assets/ArtworkInfo/MuseumGenerated…` folder. Its assets are
   generated through Unity APIs; it does not overwrite your old prototype.
6. The new `ArtworkInfoTestScene` opens automatically. The Console prints its
   folder. Review the two prefabs in Prefab Mode and the new test scene.
   Re-run generation to try another font; earlier generated work stays intact.
   Partial failures also remain isolated in the printed folder for inspection.

## Generated assets and hierarchy

Dimensions are in world meters; root scales are one. The front faces **local
-Z** (Unity World Space UI convention). Place the rear toward the wall.
The wall placard keeps its original three 60-triangle chamfered slabs and two
mount rails (204 triangles total). The detail has **one** rounded closed shell:
1.24 × 0.94 × 0.008 m, 45 mm corner radius, 1 mm edge bevel, 144 vertices and
284 triangles (8 arc segments per corner, four rings). No opaque rear, mounts,
brass edging, shadow plane, or solid front slab is added to the detail.

```text
ArtworkWallPlacard (ArtworkInfoDisplay) — unchanged physical construction
  Brass beveled edging / Ivory inset face / Finished rear
  Rear mount upper / Rear mount lower
  Front Canvas (World Space, 0.001 m/pixel)
    Ivory (invisible raycast blocker)
    Title / Artist and Year / Divider / Collection / Introduction (TMP)
    More (original Image, Button, TMP label and brass palette)

ArtworkDetailPanel (ArtworkDetailPanel) — 1.24 × 0.94 × 0.008 m
  Milky Glass Body (one MeshFilter/MeshRenderer)
  Front Canvas (World Space, 0.001 m/pixel)
    Rounded Input Shield (clear Image, ArtworkRoundedRaycast)
    Title / Artist and Year / Divider (neutral gray)
    Artwork Image (optional RawImage) / Image Placeholder / Image Caption
    Medium and Collection / Introduction (about 100 words)
    Close (76 × 76 mm target, minimal × glyph, hover/press tint)
```

The floating panel uses neutral charcoal text, uppercase title, generous
margins, image on the left and editorial text on the right. Metadata remains
oil on canvas / Art Institute of Chicago. The placard's warm ivory/brass,
typography, More palette and rear mounts are unchanged. No runtime placement,
lifecycle, mouse actions, camera controls or controller bindings are changed.

### Lightweight glass approximation (no optical blur)

The glass uses **URP Unlit**, one material, and one generated 256 × 192 RGBA32
bilinear/clamped tint texture without mipmaps (196,608 bytes of base texels,
plus Unity overhead; CPU-readable for validation). The tint is cool neutral
milky-white. Interior alpha varies approximately 0.60–0.72, with softly
localized milkiness behind the right reading column and a narrow edge
highlight reaching 0.84. This leaves background detail visible without a
second full-panel translucent overlay. It is an approximation: the scene
behind remains sharp. No GrabPass, scene color/depth sampling, refraction,
full-screen blur, added renderer feature, post-processing or global URP
change is used. Unlit tint stays light even in shadow; edge highlights are
procedural, not physically computed reflections.

Material configuration: Transparent surface, straight `SrcAlpha /
OneMinusSrcAlpha` blending, `_SURFACE_TYPE_TRANSPARENT` keyword, `ZWrite=0`,
back-face culling, no alpha clipping, render queue **3000**. The closed shell
renders only its outward-facing surfaces so front and rear faces do not stack
as two translucent full-panel layers. Background world UI shares that queue so distance sorting can put it behind
and tint it through glass. This panel has a **root World Space Canvas** with the same sorting layer as
the shell and a higher sortingOrder (normally 1 versus 0); its UI plane is
also physically in front. `overrideSorting` is a nested-Canvas inheritance
switch, not a requirement for this root Canvas. It is deliberately left false. This ordering avoids forcing all background UI to draw after glass. ShadowCaster, depth-only,
depth-normal and motion-vector passes are disabled on the glass material;
its renderer casts/receives no shadows. No collider intercepts controller rays.
Saved materials reference the shader/texture and prefabs reference the meshes;
no player-time Shader.Find is needed. Actual stereo rendering/sorting remains
pending Unity and Quest inspection, especially near other transparent objects.

The invisible input shield uses a rounded raycast filter to match the panel
silhouette; it creates no visible background. Transparent mesh culling is
explicitly disabled for this clear UI graphic so it can participate in UI
raycasts. More and Close are ordinary Buttons; the shield is first in the
Canvas hierarchy, behind the controls in graphic order. Meshes do not intercept
UI events. Corner cutouts are excluded by the filter. Both standard and
tracked graphic raycasters remain, with one input module per test scene.

The generator runs structure checks before saving the detail prefab and the
Play Mode check repeats them: exactly one glass mesh, expected transparent
blend/queue/keyword/depth state, tint alpha below 0.9, no colliders, and one
rounded input shield. These checks are **provided but not executed here**
because they require Unity. They do not prove the rendered appearance.

The placard serializes a separate detail **prefab reference**. In the test
scene it also references a separate, initially inactive detail **instance**.
Outside that scene it instantiates one on first use. More reuses the same
panel while open. Close hides it. Reopening places it again at the new
viewpoint. The panel is detached, scale one, 1.35 world meters ahead using
horizontal camera forward; it stays upright at eye height even when looking
up/down. Its -Z front faces the viewer. No Update/late head tracking moves it.
Disabling a placard closes its panel; destroying it destroys only a panel it
created, preserving a separately assigned scene instance.

## Desktop mouse acceptance test (required on your Mac)

The generated desktop scene has **InputSystemUIInputModule**, its default
mouse actions (reassigned on every scene load by ArtworkDesktopInput), one EventSystem, a regular camera and GraphicRaycaster on
both canvases. It deliberately has no XR Origin and no second UI module.
TrackedDeviceGraphicRaycaster is also present for use in the separate XR
scene. No project bindings or global XR settings are changed.

1. Open the generated `ArtworkInfoTestScene`, enter Play Mode, and click the
   Game view to focus it. With RMB released the cursor must be visible.
2. **Left-click More**. The larger physical panel must appear in front of the
   current camera. Check the milky-white **translucent** shell, neutral edge
   highlight, rounded corners, charcoal title/description, image and upper-right
   ×. Inspect Console. The two-tone background and narrow vertical markers are
   environment objects, not panel children: see them through the glass on both
   sides. The small warm-white placard mount is also a separate environment
   object. There must be no black rectangle moving with the panel.
3. **Left-click ×**. Repeat at least ten More/Close cycles; confirm one
   detail instance, no duplicate panels, no Console errors.
4. Hold **right mouse button** to look around; while held use **WASD** to move,
   **Q/E** down/up, **Shift** faster. Release RMB before clicking UI. **Home**
   restores the camera. The wall and models intentionally have no collision
   in this inspection scene: move behind the wall to inspect the finished back.
5. Open the panel, move sideways and rotate over several frames. It must stay
   at its opening world position/rotation. Close/reopen from a different
   viewpoint: the panel must appear at the new viewpoint. Looking away while
   it is open does not reposition it. Use Home if you lose the display.
6. Run **2. Check Behavior in Play Mode**. It invokes the actual Button events
   and checks distance/orientation/scale, instance reuse, immediate camera
   independence, reopen/Close and text overflow. More and Close additionally
   go through EventSystem.RaycastAll at their screen centers, asserting that
   each is the first Button hit, then dispatch a synthetic pointer click. It does **not** prove that a
   physical mouse or controller event reaches the Button, or prove stability
   over multiple frames. Those are steps 2–5 above.
7. Exit Play Mode and inspect the geometry in Scene/Prefab view from front,
   side and rear. Judge materials under the simple neutral wall/key light.
   The separate bright-left/dark-right backgrounds are at z=2.48 m; narrow
   contrast markers are at z=2.40 m. Camera starts at z=0, glass opens at
   z=1.35, placard is at z=1.60 and its mount at z=1.68. Move sideways to
   judge text over both backgrounds and verify the markers stay in the room
   while the panel remains world-locked. Inspect front, oblique and rear views;
   check for doubled opacity, sorting artifacts and missing × glyph. Bright
   backgrounds must not wash out text; dark backgrounds must remain visible.
   No replacement Gallery is built. Do not commit generated assets until
   generation, references, Console, text wrapping and visuals have passed.

## Image and facts

The user's floating artwork interface screenshot guides composition and rounded,
translucent styling; its dark tint is intentionally replaced with light white.
The screenshot is not copied into Unity. No external assets are downloaded.
The generator reuses `Assets/Art/Painting/The_Bedroom_Van_Gogh.jpg` when Unity
can load it. Its exact provenance is already recorded in `docs/ASSETS.md`:
Art Institute of Chicago, public domain painting, CC0 reproduction. It is not
modified or duplicated. The 480 px wide RawImage preserves source aspect ratio;
the caption states the source and CC0. No new image license is inferred.

If that texture is absent/unavailable, the intentional **Image Placeholder**
reads “THE BEDROOM / 1889” and the caption reads “Artwork image”. To replace it,
open the new detail prefab in Prefab Mode, assign a verified image to **Artwork
Image (optional)** RawImage, set its color alpha to 1, resize that Rect to retain
the texture aspect ratio, disable **Image Placeholder**, and update the caption.
Record any replacement asset's exact source/license/path in `docs/ASSETS.md`.
Check the project painting file is fetched through Git LFS if it cannot import.
No pagination, share or favourite controls from the reference are implemented.

New editorial prose relies on these museum sources (checked for the original
PR on 2026-10-26; re-verification may be blocked by the museum web server):

- https://www.artic.edu/artworks/28560/the-bedroom — Vincent van Gogh, 1889,
  oil on canvas, Art Institute of Chicago.
- https://archive.artic.edu/van-gogh-bedrooms/timeline — depicted room in
  Arles; Chicago version made in Saint-Rémy in 1889 after the 1888 original.
- https://archive.artic.edu/van-gogh-bedrooms/about-paintings — three versions.

## Later Quest 3 validation

After reviewing the standalone desktop design, exit Play Mode and choose
**3. Create XR Test Copy From Open Standalone Scene**. This creates a separate
`ArtworkInfoXRTestScene`, reads/copies the current Gallery rig, removes the
desktop camera/module, installs XRUIInputModule, and preserves the copied
rig's Trigger UI Press bindings. Gallery is opened read-only for copying and
never saved. Existing XR test files are not overwritten. Inspect exactly one
XR Origin, one EventSystem/module, canvas event cameras, UI interaction
settings and controller UI Press references in that isolated scene.

Use a temporary local Android build profile containing only the XR test.
Preserve shared build settings: Unity 6000.3.23f1, OpenXR Meta Quest Support,
minimum API 32, GameActivity. Build from a clean worktree. Test immersive
launch, each controller's ray/Trigger on More and Close, hover/pressed states,
release/held Trigger, repeated cycles, head turns and walking over several
frames, eye-height placement, readable text at 1.35 m and placard at 0.5–1 m,
and no input errors. Also test rounded silhouettes in both eyes, alpha blending
against bright/dark gallery backgrounds, reading-column contrast, oblique/rear
views, no opaque rear or doubled opacity, × hover/press states, and any artifacts
where other transparent objects overlap. Record tester/date/commit and results. A stationary FPS
or source review is not evidence of working controller interaction.

Do not integrate these assets into Gallery or modify Bedroom until the
standalone design has been reviewed and approved. The former Gallery
integration menu has been removed from these tools to prevent accidental use.
PR #112 stays draft; no new PR and no merge.

## Validation boundaries

Run `bash scripts/ci-local.sh` before push. This checks repository assets and
history, not C# compilation, generated mesh appearance, mouse clicks or XR.
Unity compilation, generation, Play Mode, screenshot inspection of actual
Unity geometry, mouse input and Quest tests have **not** been performed on
the execution host. All runtime outcomes remain pending local validation.

## Validation report for the glass revision

- Code/generator: added rounded glass shell, procedural tint and edge highlight,
  neutral × button/charcoal typography, rounded input shield, existing CC0 image
  reuse, contrast-test backgrounds and Unity structure assertions.
- Repository checks and guarded installer checks: results are recorded in PR #112
  after running against the source commit. They are not Unity rendering checks.
- Actual Unity compilation: not run (Editor unavailable on execution host).
- Revised prefab generation: not run; must generate locally in the new folder.
- Actual rendered visual inspection: not run; screenshot reference inspection
  does not count as inspection of generated Unity output.
- Mouse input/Play Mode: not run; button-event tests also require local execution.
- Quest 3 build/controller/performance/stereo validation: not run.

The new generator does not patch old generated prefabs: use menu step 1, open
its newly generated scene, and judge that output. Keep the old models for
comparison. No generated assets are committed or Gallery integration approved.

## Fix for root Canvas sorting validation (after 03694a9)

The old `CheckGlassStructure` required `canvas.overrideSorting == true` on
`Front Canvas`, even though its parent is a GameObject with no Canvas. Unity
[documents overrideSorting](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Canvas-overrideSorting.html)
as allowing **nested** canvases to override their parent's sorting order.
Requiring that flag on a root World Space Canvas was an invalid assumption.
The old combined assertion also hid which value failed. The reported stack
trace does not provide the native flag/order readbacks; this host cannot
reproduce those values without Unity.

The generator now sets the root Canvas's sorting layer to the glass renderer's
layer and its order one higher, without requiring a nested override. Validation
checks the actual layer/order relationship, root World Space mode, transparent
glass state, every UI quad physically in front of the glass, UI material queues,
both raycasters, a raycastable Close button and input shield sibling ordering.
Failures include actual mode/root/override/layer/order values. This retains
rendering and input checks rather than deleting the failing assertion.

Generation reloads the saved detail prefab through Unity's Prefab APIs and
validates it again. It also checks that a root Canvas with overrideSorting=false
passes, and deliberately gives it the glass's sortingOrder to ensure the
validator rejects that regression. These temporary checks are never saved to
the prefab. Menu step 2 now exercises screen-point UI raycasts and Button
pointer handlers in addition to existing behavior/overflow checks. Neither
structural validation nor a synthetic pointer click proves rendered pixels,
physical mouse events, or Quest Trigger delivery: inspect the actual view and
click the buttons after generation. All added Unity checks are unrun here.

### Recover and regenerate on the Mac

1. Exit Play Mode. Save any unrelated work. In GitHub Desktop select
   `feature/14-artwork-info-display`, Fetch origin, then Pull origin.
2. In a Terminal at the repository root run:
   `bash scripts/artwork-info/install-source.sh --update-source`.
   If it reports locally edited source, preserve/compare that file; do not
   delete or overwrite it to bypass protection.
3. Return to Unity 6000.3.23f1 and wait for compilation. Open a **saved** scene
   you can return to, preferably a previous working standalone test scene.
   Do not use objects left in the failed untitled scene.
4. Run Tools > InnerFrame > Artwork Info > **1. Generate Standalone Museum
   Assets**. Wait for the success log. Open/use the newly generated scene;
   confirm its folder's `GENERATION_STATUS.txt` begins **COMPLETE**.
5. Enter Play Mode, focus Game view, release RMB, click More, then ×. Check
   that the dark text and × stay visible above the glass as you move sideways;
   repeat opening/closing. Run menu step **2. Check Behavior in Play Mode**.
   A passing check still needs visual and actual mouse confirmation.

Older `MuseumGenerated 4` / `MuseumGenerated 5` folders are **not repaired or
removed**. Keep them for diagnosis or delete only those known failed folders
through Unity's Project window after confirming they contain no work to keep.
Do not drag their partial prefabs into the new scene.

Every new run writes INCOMPLETE before generating and COMPLETE only after
prefabs are validated and the test scene is successfully saved. On failure,
the generator restores the previously saved scene setup (or a clean default
scene if the prior scene was untitled), marks only the new output folder
`FAILED Museum Generation…`, and writes a FAILED status with the exception.
Existing folders are never renamed. If recovery itself fails, the original
error and output path are still reported and partial scene roots are disabled.
A COMPLETE marker means successful generation/structure checks, not successful
visual, mouse or device acceptance. No Gallery/Bedroom integration is added.
