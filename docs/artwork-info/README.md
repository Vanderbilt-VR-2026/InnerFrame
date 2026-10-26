# Museum placard and detail panel — Issue #14 / Draft PR #112

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
Each slab is a closed chamfered mesh with 32 vertices / 60 triangles. Each
prefab has three slabs (180 triangles); the placard adds two cube mount rails
(24 triangles). Meshes and URP Lit materials are saved assets referenced by
prefabs, so build stripping does not depend on runtime Shader.Find.

```text
ArtworkWallPlacard (ArtworkInfoDisplay) — 0.64 × 0.60 × 0.022 m
  Brass beveled edging (MeshFilter/MeshRenderer)
  Ivory inset face (MeshFilter/MeshRenderer)
  Finished rear (MeshFilter/MeshRenderer)
  Rear mount upper / Rear mount lower
  Front Canvas (World Space, 0.001 m/pixel)
    Ivory (transparent raycast blocker; physical mesh provides the surface)
    Title / Artist and Year / Divider / Collection / Introduction (TMP)
    More (Image, Button, TMP label)

ArtworkDetailPanel (ArtworkDetailPanel) — 1.12 × 0.86 × 0.026 m
  Brass beveled edging / Ivory inset face / Finished rear
  Front Canvas (World Space, 0.001 m/pixel)
    Ivory / Title / Artist and Year / Divider
    Medium and Collection / Introduction (about 100 words)
    Image Placeholder / Artwork Image (optional RawImage)
    Close (Image, Button, TMP label, upper right)
```

Ivory is matte, edging is champagne brass, rear is dark and finished. No
physics colliders or grab interactables are added to the display. More and
Close have explicit default, hover, selected and pressed color transitions;
the buttons have 72 mm high targets. Text uses fixed 26–48 px sizes with dark
ink and generous margins. Headset legibility and wrapping remain unverified;
the Play Mode check detects TMP overflow. Refine based on actual viewing.

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
   current camera. Check title, metadata, ~100-word introduction, placeholder,
   brass border, front/side depth, and upper-right Close. Inspect Console.
3. **Left-click Close**. Repeat at least ten More/Close cycles; confirm one
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
   independence, reopen/Close and text overflow. It does **not** prove that a
   physical mouse or controller event reaches the Button, or prove stability
   over multiple frames. Those are steps 2–5 above.
7. Exit Play Mode and inspect the geometry in Scene/Prefab view from front,
   side and rear. Judge materials under the simple neutral wall/key light.
   No replacement Gallery is built. Do not commit generated assets until
   generation, references, Console, text wrapping and visuals have passed.

## Image and facts

The reference design board is a visual guide, not an asset or texture. No
external images/fonts/models are added by this change. The panel reserves a
420 × 360 px image space. To use a verified image, open the detail prefab in
Prefab Mode, assign its texture to **Artwork Image (optional)** RawImage,
set its color alpha to 1, preserve image aspect ratio by resizing that Rect,
and disable **Image Placeholder**. Apply to the prefab and recheck both test
scenes. A verified Art Institute public-domain/CC0 reproduction can be used;
record the exact image URL, rights statement, and asset path in
`docs/ASSETS.md` before adding it. No pagination dots imply nonexistent slides.

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
and no input errors. Record tester/date/commit and results. A stationary FPS
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
