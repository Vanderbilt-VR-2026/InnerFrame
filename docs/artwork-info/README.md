# Artwork information — standalone Figma design (#14, Draft PR #112)

The detail generator implements [Detail board, node 2:37](https://www.figma.com/design/dRg2LX8SNV5wouBX1Zr76N/InnerFrame?node-id=2-37). It preserves the existing traditional ivory/brass wall placard and its More action. No Gallery, Bedroom, transition, build setting, or project-wide renderer changes are made.

**Status:** generator source and local design-input bundle supplied; Unity 6000.3.23f1 compilation, generated prefabs, rendered comparison, actual mouse clicks, Android build and Quest tests have **not** been performed on this host. Repository checks are not Unity acceptance. Do not integrate into Gallery until the standalone appearance is approved.

## Update on Mac

1. Exit Play Mode and save unrelated work. In GitHub Desktop select `feature/14-artwork-info-display`, Fetch origin, then Pull origin. Do not discard local changes.
2. Download the accompanying **InnerFrameFigmaAssets.zip**. It contains the exact individual Figma layer exports and the two static font files with OFL licenses. Keep the ZIP: the public PR does not contain the private design binaries. No temporary Figma URL is used at runtime.
3. Open Terminal at the repository root and run:

   ```bash
   bash scripts/artwork-info/install-source.sh --update-source --design-assets "$HOME/Downloads/InnerFrameFigmaAssets.zip"
   ```

   An extracted bundle directory is also accepted. Later source-only updates can use `--update-source` alone if the exact design inputs are already installed. Python 3 is required. The installer verifies every design file against `design-assets.json`, then checks all existing source/design destinations before copying anything. Locally edited files stop the update; preserve and compare them rather than forcing an overwrite. Existing `.meta` files, generated folders and scenes are preserved.
4. Let Unity import and compile. Keep TMP Essentials installed. Do not proceed if the Console has compilation errors. The selected TMP font (or TMP default) still supplies the **wall placard** only; detail typography is generated from the bundled fonts.
5. Open a saved working scene, outside Play Mode, and choose **Tools → InnerFrame → Artwork Info → 1. Generate Standalone Museum Assets**.
6. Use the newly opened `ArtworkInfoTestScene` in the new `Assets/ArtworkInfo/MuseumGenerated…` folder. Only use a folder with `GENERATION_STATUS.txt` marked **COMPLETE**. Previous generated versions, including partial `MuseumGenerated 4/5`, are not updated or deleted.

Missing design inputs fail before a new scene/folder is created. Failures after generation starts restore the previous saved scene setup (or a clean default scene for an untitled prior setup), rename only this run's folder `FAILED Museum Generation…`, and record the exception. `INCOMPLETE` and `FAILED` folders are not test-ready. COMPLETE means the generator saved and checked its assets; it does not mean visual/device acceptance.

## Design mapping and deliberate rendering approximation

The source design is 476 × 416. The root Canvas is **1428 × 1248**, at **0.001 m per Canvas unit**: every design coordinate and font size is multiplied by three, producing a **1.428 × 1.248 m** panel. Its thickness is 8 mm and its radius is 60 mm (20 design px). This retains the original aspect ratio and composition while increasing physical text size at the existing 1.35 m opening distance.

Coordinates below are original Figma top-left design pixels. Text is editable TMP, not baked into the images.

| Layer | x, y | Width × height | Font / size / color |
|---|---|---|---|
| Portrait 2:2 | 30, 48 | 112 × 136 | Exact individual 4× export, 448 × 544 |
| Title 1:7 | 28, 189 | 112 × 19 | Cormorant Garamond Medium, 16, black |
| Divider 2:9 | 28, 212 | 112.0045 × 3 rendered extent | Exact individual export |
| Artist/year 2:3 | 28, 219 | 116 × 12 | Inter Regular, 10, #5A5353 |
| Medium/collection 2:5 | 28, 234 | 110 × 24 | Inter Regular, 10, #5A5353 |
| Description 2:7 | 205, 48 | 243 × 285 | Inter Regular, 12, #5A5353; 15 px line height |
| ENTER background 2:30 | 26, 345 | 112 × 36 | Exact individual export of the three translucent pill layers |
| ENTER label 2:32 | 57, 354 | 51 × 19 | Cormorant Garamond Medium, 16, black |
| Circular X background 2:34 | 444, 20 | 21 × 21 | Exact individual export, 84 × 84 |
| X label 2:36 | 449, 20 | 11 × 19 | Cormorant Garamond Medium, 16, #AAAAAA |

The design deliberately uses a **Van Gogh portrait**, not a Bedroom painting image. It is retained exactly; the old CC0 Bedroom image and caption are not substituted. The copy is transcribed from Figma, including its paragraph breaks. Title/ENTER black and X gray follow their actual layers; the descriptive text uses the requested charcoal.

Both fonts are unmodified static TTFs from Google Fonts (weights 500 and 400). The Google-generated Cormorant font's internal family string includes “Light Medium”; its OS/2 weight is 500. The generator bakes ASCII plus the middle-dot into **two 1024² SDFAA atlases**, sampling size 90, padding 9, with material/atlas subassets saved alongside their TMP font assets. It then switches to Static population: no OS fallback or runtime atlas generation. Auto-size is disabled. Missing glyphs and overflowing text cause validation failure instead of silently replacing or shrinking text. Future copy outside that character set requires rebaking the glyph set.

The glass combines the design's gray gradient (alpha .2), two #F1E6D6 fills (alpha .2 each), and warm-white fill (alpha .39) into **one transparent tint map**, net interior alpha .68768. A subtle white edge rises to alpha .84. There is one rounded mesh (144 vertices, 284 triangles), one URP Unlit material and one 256 × 192 tint map. This approximates the Figma glass effect; it does **not** reproduce optical blur, refraction, dispersion, screen-space shadows or background-dependent Figma effects. No GrabPass, renderer feature, post-processing change, rear slab, opaque Canvas background or metallic frame is added. The exported button backgrounds preserve their individual Figma visual treatment but do not refract the live scene.

## Prefab hierarchy

```text
ArtworkWallPlacard [ArtworkInfoDisplay — unchanged construction]
  Brass beveled edging / Ivory inset face / Finished rear
  Rear mount upper / Rear mount lower
  Front Canvas [World Space; mouse + tracked-device raycasters]
    Title / Artist and Year / Divider / Collection / Introduction / More

ArtworkDetailPanel [ArtworkDetailPanel]
  Milky Glass Body [MeshFilter, MeshRenderer; no collider]
  Front Canvas [World Space; GraphicRaycaster, TrackedDeviceGraphicRaycaster]
    Rounded Input Shield [transparent Image, ArtworkRoundedRaycast]
    Portrait — Figma 2:2 [RawImage]
    Title — 1:7 [TMP]
    Divider — 2:9 [RawImage]
    Artist and Year — 2:3 [TMP]
    Medium and Collection — 2:5 [TMP]
    Introduction — 2:7 [TMP]
    ENTER (navigation unconnected) [RawImage, Button, ArtworkEnterAction]
    Enter Label — 2:32 [TMP]
    Close [RawImage, Button]
    Close Label — 2:36 [TMP]
```

Decorative images and all labels have `raycastTarget=false`. The button images accept mouse/XR UI rays and have default, visibly tinted hover and pressed states. The X hit rectangle is 63 × 63 mm; the visible circle remains the Figma size. The rounded transparent shield is first in the Canvas hierarchy to stop click-through behind the panel without intercepting its buttons. There are no colliders on the panel.

Front Canvas is a **root** World Space Canvas: `overrideSorting` controls nested Canvas inheritance and is not a root requirement. The Canvas uses the shell's sorting layer and one higher sorting order. UI quads sit 1.5 mm ahead of the glass front. The shell uses transparent queue 3000, straight alpha, back-face culling and ZWrite off; its shadow/depth/motion passes are disabled. Validation checks the real layer/order relationship, material queues, physical front separation, both raycasters and a live Close button. Saved prefabs are reloaded and checked again. A negative test lowers the UI order to the shell and must fail; a root Canvas with overrideSorting=false must pass. Temporary test changes are not saved.

## Mouse and appearance test

1. Enter Play Mode in the **new** standalone scene. Keep the Game view at a normal landscape aspect ratio. Release RMB so the cursor is visible, then left-click More.
2. The panel opens once, upright and facing the current camera, approximately 1.35 m away. It is independent in world space. Verify the portrait, two columns, title/divider/metadata positions, pill ENTER and circular X against Figma at the same relative scale.
3. The test wall has bright and dark halves and contrast markers. Verify that the live environment remains visible through the glass, with no black backing. Read the description on both backgrounds. Confirm crisp text, all four paragraphs, no cut-off lines, and distinct hover/press states.
4. Hold RMB and move with WASD; Q/E move down/up, Shift speeds up. Release RMB to click again. Inspect thin edges and the rear; confirm the panel stays fixed over multiple frames as the camera moves. Home resets the desktop camera.
5. Click X to close, then More to reopen. Repeat several times. ENTER logs that navigation is unconnected and raises its optional event; it does not change scenes or close the panel in this standalone design.
6. With the camera back at the initial pose, run **2. Check Behavior in Play Mode**. It raycasts through EventSystem at More, ENTER and Close, requires the intended first hit, invokes pointer handlers, verifies a single ENTER event, open/close/reuse, placement, immediate world-pose stability, structure and TMP overflow. This synthetic test does **not** prove hardware mouse/Trigger input or rendered pixels.
7. Capture the Game view with the panel front-facing and compare it side by side with Figma node 2:37. Match the panel bounds rather than comparing whole-window sizes. Record layout, font rendering, wrapping, image, button placement, transparency and contrast separately. A flat Figma screenshot cannot prove stereo rendering or real glass transparency. No such Unity comparison has been performed here.

## ENTER integration seam

`ArtworkEnterAction.EnterRequested` is a serialized UnityEvent. The existing repository has `SceneFader.FadeToScene(string, float, Texture2D)`, but the standalone prefab has no approved scene destination/context and this three-argument method is not a direct single-argument persistent Button binding. No compatible configured standalone Enter action was found. Therefore no global scene lookup or invented scene name is wired.

After Gallery integration is explicitly approved, an owner can bind the existing approved navigation component's compatible public method to the event or supply a small adapter. Do not replace the Gallery's current Enter the Painting control. Runtime listeners can also use `EnterRequested.AddListener`; the informational “no persistent binding” log concerns Inspector bindings only.

## Quest 3 validation — later, on device

1. Exit Play Mode and open the new saved `ArtworkInfoTestScene`.
2. Run **3. Create XR Test Copy From Open Standalone Scene**. It creates `ArtworkInfoXRTestScene`, reads the Gallery rig additively without saving Gallery, copies one existing XROrigin, removes desktop camera/input, and installs XRUIInputModule. It refuses to overwrite an existing XR test copy.
3. Inspect exactly one XR Origin/EventSystem, the tracked-device raycasters, controller UI Press/Trigger action assignments, and the camera reference. Existing bindings are not remapped.
4. From a clean worktree, build the isolated XR test using the Android profile with the repository's existing OpenXR/Meta Quest Support guardrails. Verify both eyes, Trigger hover/press/close, repeated cycles, 1.35 m placement, world locking, and legibility at actual headset resolution.
5. Measure frame time, overdraw and texture memory on Quest; inspect oblique views and UI sorting in both eyes. Do not claim device compatibility/performance based on source checks. Record tester, date, commit and immersive device results before marking the draft ready.

## Cormorant title line-box fix

The old `Figma SDF missing or text overflows` assertion combined unrelated failures. The bundled Cormorant TTF has 1000 units/em, ascent 924 and descent −287: at Figma's 16 px size, its natural metric span is **19.376 px**, larger than the **19 px** title box. Unity's sampled FaceInfo may round this further. TMP checks ascender/descender span against available height even for a single line; `<line-height=57>` controls subsequent baseline spacing and does not replace those metrics. The old zero-margin mapping was therefore incorrect. The screenshot alone cannot disclose Unity's actual sampled metrics or distinguish branches of that old assertion.

The generator now applies **half-leading margins** from the actual generated TMP FaceInfo: `(designLineHeight − scaledAscenderDescenderSpan) / 2` above and below. Negative half-leading is intentional when the font metric span exceeds the Figma line box. A 0.01 Canvas-unit allowance on the bottom avoids floating-point equality noise (0.0034 design px). The title remains exactly 16 design px / 48 Canvas units, in the same 112 × 19 px box at the same position. No font scale, glyph, atlas metric, composition or auto-size change is made. Single-line labels receive at most a half-design-pixel negative right margin to accommodate advance rounding; this does not change their RectTransform. Visible glyph ink is independently required to remain within the original design rectangle (0.5 design-pixel sampling tolerance); transparent SDF padding is excluded from that ink check. The error now reports whether TMP overflow is horizontal, vertical, or both, along with actual line ascender/descender, available box dimensions, and widest line advance before the per-glyph report.

Validation rebuilds RectTransforms, the completed Canvas layout, and TMP meshes, including inactive prefab contents. Font absence, invalid/static SDF state, missing/substituted glyphs, TMP overflow and visible-glyph escape produce **separate errors**. Reports include asset path/family/style, sampled point size/ascent/descent/line height, atlas dimensions, glyph count, font size, margins, rect, preferred dimensions, text bounds, overflow/index, line count, and per-character glyph ID/font/scale/ink bounds. Success reports are also logged as `Text layout PASS`; failures are retained in the existing failed-generation status file.

An additional generator regression check temporarily restores the title's old zero margins and requires a text-overflow failure, then restores the corrected margins and validates again. It also asserts the unchanged title box/font size. Existing saved-prefab checks remain. These Unity regression checks are supplied but **not executed on this host**. Font-table calculations and source inspection establish the mapping defect; final baseline appearance and native TMP readback still require local Unity generation/rendering.

To install this fix after the Figma bundle is already installed: pull the feature branch, run `bash scripts/artwork-info/install-source.sh --update-source`, wait for Unity compilation, then run menu step 1 again outside Play Mode. **No bundle re-download is needed.** Test the newly generated COMPLETE folder, not an earlier failed scene.
