Historical card-only validation record; the subsequent GitHub integration checks are recorded in `../integration/verification/`.

# Local integration verification — 2026-11-11

Reviewed with three independent review angles: asset/GUID integrity, interaction behavior, and evidence quality. This is a downloaded main snapshot, not a PR branch; Git history/merge CI has not been run and no GitHub changes have been submitted.

| Finding | Verdict and resolution |
|---|---|
| Trigger buttons ignored by existing desktop ray | Confirmed; now non-trigger UI colliders. Physics hit verified with the existing ray mask and query setting. |
| Edit-mode UnityEvent test did not invoke RuntimeOnly listeners | Confirmed; replaced with serialized-route checks plus Play-mode XR interaction manager selection. |
| Prefab changes might not persist | Prevented by unpacking through Unity before installing and saving scene; reload test passed. |
| Near-wall hit could create negative scale | Confirmed; minimum distance includes wall clearance plus camera near clip. Decorative walls also have a placard-plane placement cap. |
| Missing text in fresh imported Gallery | Confirmed visually; added missing standard TMP resources and a generated static font atlas. Final Play-mode renders have readable text. |
| Wall text visible through Detail Card | Confirmed visually; added opaque rounded navy fill behind shared UI art. |
| Duplicate GUIDs or missing new .meta files | Refuted by final filesystem checks: zero duplicates; all new assets have .meta. |

[V] Unity 6000.3.23f1 batch import/compile and final Play test passed; final test log has no Exception, error CS, Shader error, MissingReferenceException, or NullReferenceException.
[V] Exactly two introduction pairs; no self-portrait introduction card; existing Gallery portrait spawns.
[V] Both details initially hidden, More opens, X closes, More reopens. Test raycasts with the existing desktop ray settings and drives XRInteractionManager.SelectEnter/Exit. It does not synthesize physical controller input.
[V] ENTER actually loads PaintedBedroom / StarryNight through existing SceneFader, returns to Gallery, and details reset hidden.
[V] Final screenshots visually inspected for text, X, portrait insets, borders and background readability.
[V] Exported unitypackage contains Gallery, TMP Settings and all new integration assets; 393 entries.
[I] Quest controller tracking, headset comfort/readability and Android build performance are not tested.

Source: main b94d86e5228ad0b26f8f2274f9544afcf4982373, merged Gallery remodel PR #186. PR #187 lighting remains excluded. Open draft PR #112 overlaps Gallery and would need reconciliation before any future merge. There is no proposed auto-merge.

## Mouse-close fix

The previous XR manager tests did not cover ordinary mouse clicks. DesktopTestRig captured the left button for look, so clicking the visible X was not equivalent to selecting it with the centre ray. The free cursor now performs a screen-point physics hit against the existing card Button collider before camera capture; normal centre-ray and headset routing remain intact. A desktop card disables its competing GraphicRaycaster target so a mouse-up cannot fire ENTER twice. Opening details frees the cursor and freezes desktop panel following while reading.

[V] Added Input System mouse-state events at the projected on-screen buttons, consumed by the enabled DesktopTestRig.Update loop. Both Bedroom and StarryNight passed More opens, X closes, More reopens, and cursor remains unlocked. These are injected Unity mouse input events, not hardware mouse automation. The existing XR manager route and both scene transitions also passed again. See play-check.txt.

[V] Modified source: Assets/Script/Network/DesktopTestRig.cs and Assets/ArtworkCici runtime/editor helpers. No change to Android input or headset configuration.

## Route and multiplayer isolation correction

Confirmed mechanism: ShowDetails previously left other cards open; two opened at the same local camera could overlap and compete for the same pointer ray. No incorrect serialized destination was found in the two Gallery entries. The correction makes detail opening exclusive within the local scene instance, adds expectedScene validation before entry, and prevents legacy pedestal mouse click-through while reading. The installer audits each card's ENTER target and each pedestal's XR event target, not just their method names.

Each player's scene has its own unsynchronized UI. This does not impose a room-wide single-open-card restriction. NetworkSession.Leave shuts down this client's Runner; SceneFader then uses Unity SceneManager locally. No shared scene change or new RPC is sent. Invalid/nonlocal network avatars cannot trigger local card XR input or local scene-entry colliders.

Validation includes Input System mouse events into card ENTER with the other card opened first, actual destination-scene assertions, original pedestal OnMouseDown callbacks and XR-manager selection, followed by the previous card XR route tests. Local UI isolation is exercised with two separate scene contexts in one Editor process, and unowned network-avatar trigger input is rejected. This is structural/logic validation, not a two-device network session. Photon authentication currently reports InvalidAuthentication, so live two-player and Quest behavior remains unverified.
