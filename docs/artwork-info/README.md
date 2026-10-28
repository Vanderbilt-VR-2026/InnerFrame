# Artwork information — Issue #14

## Open the checked-in feature

Use Unity **6000.3.23f1** and open `Assets/Scenes/Gallery.unity`.
The submitted scene uses the existing Gallery XR rig. It references the
`ArtworkWallPlacard.prefab` and `ArtworkDetailPanel.prefab` in
`Assets/ArtworkInfo/MuseumGenerated 16/`. Do not regenerate assets to open or
review the feature. Preserve their `.meta` files and GUIDs.

The wall label is a white, serif museum label with a headphone/12 graphic,
metadata, divider, description and visible More button. Its generated body is
0.22 × 0.3055556 × 0.003 m. The Gallery instance overrides its front material
with `Assets/ArtworkInfo/GalleryPreviewWhite.mat` (URP Unlit white), so gallery
lighting does not tint the board gray. The material's Preview name is historical;
it is a production scene dependency. This trades lit surface shading for stable
white readability; the physical geometry is retained.

The detail panel preserves the user's Figma 2:37 composition: 476 × 416 design
units mapped to a 1428 × 1248 World Space Canvas at 0.001 m/unit, two columns,
separate portrait image, editable TMP text, ENTER and X. The thin rounded shell
uses a lightweight translucent URP glass approximation, not optical blur.
Cormorant Garamond Medium and Inter Regular use baked SDF assets; the fonts,
licenses, design inputs and TMP Essential Resources are included.

## Interaction and placement

- Point the existing controller UI ray at **More** and activate UI Press/Trigger.
- `ArtworkInfoDisplay` uses `scenePanel` if assigned, otherwise creates the
  referenced detail prefab once and reuses it for subsequent openings. The
  Gallery placard has `scenePanel = None` and uses the prefab path.
- `viewingCamera = None` is supported. Resolution prefers an explicitly assigned
  active XR Origin camera, then the unique active `XROrigin.Camera`. Multiple
  candidates produce a diagnostic instead of selecting arbitrarily. While XR is
  enabled, the resolver does not fall back to a desktop camera. Outside XR,
  an active assigned camera or `Camera.main` supports desktop tests.
- The panel opens **1.35 m** along the camera's full forward direction and uses
  its rotation. Its local -Z front faces the viewer. It is detached from the rig.
- **Final submitted behavior is world-fixed after opening, not head-following.**
  `ArtworkHeadFollow` retains its historical name, but calls `RefreshPose` only
  from `Begin`; it has no Update/LateUpdate/before-render tracking callback.
- **X** hides the panel. Reopening places the same panel in front of the current
  camera pose. The wall placard never moves with the player.
- ENTER remains the existing separate event hook. An unbound hook must not be
  described as working painting navigation. The existing painting-entry
  interaction is unchanged by this submission.

Both UI raycasters and the existing Canvas/glass sorting are preserved. No
project-wide rendering changes, XR action changes, or controller bindings are
introduced. Panel placement is not collision-aware: a nearby wall can obscure a
panel opened 1.35 m away. This submission does not add an always-on-top mode or
extend the controller ray reach.

## Desktop and device checks

The retained isolated scene is
`Assets/ArtworkInfo/MuseumGenerated 16/ArtworkInfoTestScene.unity`.
Enter Play Mode; release RMB so the cursor is visible; click More and X.
Hold RMB with WASD/QE to navigate, Shift to move faster, Esc to release the
cursor, and Home to reset the camera. Move after opening to verify world lock;
close and reopen from another direction to verify placement and reuse.

The generator's menu **2. Check Behavior in Play Mode** exercises synthetic UI
raycasts and button handlers, placement, reuse, layout and an immediate
camera-motion assertion. It is not a physical mouse/controller test and does not
prove multi-frame world stability. Observe several frames manually.

For Quest, build the existing Gallery using the project's Android/OpenXR setup.
Verify immersive stereo, More hover/Trigger, readable front-facing text, X,
repeated opening from different poses, fixed placement while turning, and
painting-entry behavior. Check Console/player logs. Keep other team features and
rig/settings unchanged. An independent tester is still required by Issue #14.

## Source maintenance

Unity compiles `Assets/ArtworkInfo/{Runtime,Editor}`. Matching source copies live
under `scripts/artwork-info/{Runtime,Editor}`. The uploaded snapshot had three
stale source mirrors; this submission copies the installed Unity versions into
those mirrors (ArtworkInfoDisplay, ArtworkInfoTools and ArtworkFigmaPanel).
Installed scripts, scene, prefabs, materials and metadata are otherwise preserved
from the supplied archive.

`bash scripts/artwork-info/install-source.sh --update-source` is a guarded source
update, not a required setup step for this now self-contained checkout. It refuses
unrecognized local edits and preserves serialized assets and metadata. Its final
generation suggestion applies to generator development, not review of the
checked-in Gallery. No private download is required for the included inputs.

Menu 1 generates a new unique standalone output folder. Do not run it to replace
the reviewed Gallery design. Menu 3 creates an isolated XR test. Menu 4 creates a
Gallery desktop integration copy, and menu 5 changes only that preview's white
surface. These optional tools are retained with their original guards and are
not run by this submission. Failed/old generated outputs and the desktop Gallery
copy are not included; the one reviewed generated folder retains its test scene.

## Evidence and remaining acceptance

- [V — author-reported] Rowling reports building and successfully testing the
  integrated application on Meta Quest 3 with Unity 6000.3.23f1, including More
  opening the Detail Board. No build hash, APK or device log accompanied the
  source archive. This report is not a clean-checkout headset verification.
- [V — source inspection] The submitted installed code is position-once/world-fixed.
  The earlier author summary said head-following; the archive takes precedence for
  documenting implementation. Which APK was tested is not independently established.
- [I — pending] Unity compilation/import from a clean checkout, physical mouse and
  Quest interaction on the submitted commit, X/reopen cycles, comfort/occlusion,
  and independent-user testing. Do not infer these from GENERATION_STATUS.txt.
- [V — source inspection] Gallery also contains an active empty root named
  ArtworkDetailPanel with no children and no Close reference. The placard does not
  reference it; the real panel comes from detailPrefab. It is retained byte-for-byte
  to avoid changing the author's tested scene. Review it in Unity before any cleanup.
- [I — review] Gallery owner Cici should review placement, especially alongside
  the portrait placement proposed in PR #118. No changes to Issue #75 are included.

See `SUBMISSION_REVIEW.md` for packaging checks and exclusions. Third-party sources
and permissions are recorded in `docs/ASSETS.md` and the supplied license files.
