# Cici cards in the updated Gallery

Based on main b94d86e5228ad0b26f8f2274f9544afcf4982373 (includes merged Gallery remodel PR #186). Does not include unmerged lighting PR #187.

Open Assets/Scenes/Gallery.unity. Two introduction pairs only: The Bedroom on the north wall and The Starry Night on the east wall. The existing runtime 1887 self-portrait and its voice interaction are unchanged and have no added introduction card.

More opens a level detail panel in front of the visitor; X closes it; More reopens it. Enter calls the corresponding existing SceneChangeButton and SceneFader. Existing pedestal buttons remain usable. Buttons use the Gallery's XR selection path. In the Editor, use the free mouse to click More, X, or ENTER directly. Opening a detail frees the mouse and keeps the panel still for clicking. Aim the central dot and press E remains supported (WASD moves; clicking elsewhere captures mouse look; Escape releases the mouse). Headset validation remains required.

The Bedroom detail retains the previously approved 1887 AIC self-portrait image. The Starry Night detail uses the Orsay 1889 self-portrait. Both images have explicit portrait captions; artwork text continues to describe the painting, not the inset portrait.

Tools > InnerFrame > Cici UI > Test Gallery cards (offline) runs runtime checks in Play mode, including the XR interaction-manager selection path and both destination scenes. Output: Logs/GalleryCards/play-check.txt and PNG previews. It deliberately prevents joining multiplayer rooms while testing.

Tools > InnerFrame > Cici UI > Install cards in updated Gallery rebuilds only the two cards and hides superseded placards. It does not rebuild the Gallery model/decor or change project settings. This installation replaces existing generated Cici cards, so save manual card adjustments separately before rebuilding.

This card integration is now included in the latest Gallery / Entrance integration branch. See `docs/integration/README.md` for current delivery and validation details. No main merge is performed.

Implementation notes: the merged repository lacked TMP Settings and related standard text resources. Missing TextMesh Pro Essential Resources were copied from the working preview project; existing resources were not overwritten. A static Libre Baskerville atlas is generated for the card text so it survives fresh imports. The detail card has an opaque rounded navy backing to prevent overlapping wall text. Decorative wall geometry is also handled by a placard-plane distance limit.

Unity's Photon initializer added PHOTON_REALTIME_4 to the Standalone scripting defines in ProjectSettings/ProjectSettings.asset. The integrated delivery also saves Entrance first in EditorBuildSettings and removes the two disabled scene entries; Android/XR settings and Packages are unchanged.

Preview shortcuts: Tools > InnerFrame > Cici UI > Preview Bedroom/StarryNight Placard or Detail. These only move the runtime desktop camera and never save those positions into the scene.

Multiplayer behavior: introduction canvases and open/closed state are client-local MonoBehaviours, never Fusion NetworkObjects/RPCs. Only the current client's other detail is closed when opening a different card. Other players can simultaneously read different artwork. Each ENTER validates its own expected destination before calling its assigned SceneChangeButton. Existing SceneFader leaves only this client's runner and loads only its local scene; it never invokes a shared Runner.LoadScene. Remote/unowned network-avatar colliders are rejected for local trigger entry and card XR input. Legacy pedestal mouse events are ignored while this client's detail is open, preventing click-through.

Gallery routes: Bedroom Introduction / Button_Bedroom -> PaintedBedroom; StarryNight Introduction / Button_TheStarryNight -> StarryNight. The self-portrait is a voice interaction and has no added introduction card or painting entrance.
