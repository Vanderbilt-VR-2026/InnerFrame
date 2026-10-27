# Voice chat (#65)

Players in the same room talk to each other. **Hold X** on the left controller
(**V** in the Editor) to talk; let go to stop. Each voice plays from that
player's avatar head, so you hear where they are standing and voices fade with
distance.

Built on the multiplayer in `docs/multiplayer/README.md`: the voice room follows
the Fusion room (`InnerFrame_<SceneName>`), so whoever you can see, you can hear.

Code: `Assets/Script/Voice/`.

| Script | What it does |
|---|---|
| `VoiceSetup` | Adds Photon Voice's `FusionVoiceClient` to the NetworkRunner before it starts (one call in `NetworkSession`). Voice joins and leaves together with Fusion. |
| `PlayerVoice` | On the avatar. Push-to-talk: the microphone records only while X is held. Pauses while B (Ask Vincent) is held. |
| `Editor/NetworkPlayerVoiceBuilder` | Called by **InnerFrame → Multiplayer → Rebuild Network Player Prefab**: adds `VoiceNetworkObject`, `Recorder` and `PlayerVoice` to the root, and a 3D `AudioSource` + `Speaker` on `Head/Voice`. |

## Why Photon Voice 2

- Fusion doesn't carry audio, and Photon Voice is Photon's own add-on for it:
  the voice room follows the Fusion session and each voice plays from the
  speaker's NetworkObject.
- Same Photon account and dashboard as Fusion; the Voice App ID goes in the
  same `PhotonAppSettings` as the Fusion App ID.
- Alternatives considered: Unity Vivox and Agora don't need Fusion, but would
  run a second, separate room system next to it and need their own wiring to
  the avatars.

**Free plan limits** (photonengine.com/voice/pricing, checked 2026-10-27):
20 players connected at once, no burst, 60 GB traffic per month, **development
only**. Enough for class demos (rooms hold 8). A public release would need a
paid plan.

## Buttons and the microphone

| Button | Does |
|---|---|
| X (left), V in the Editor | Hold to talk to other players |
| B (right), T in the Editor | Ask Vincent (`Assets/VanGogh`, on main). Voice chat pauses while held |
| A (right) | Jump (XRI) |
| Menu (left), M in the Editor | Player menu |

Ask Vincent records with Unity's `Microphone` and voice chat records with
Photon's Recorder. Only one can hold the microphone, and a question to Vincent
shouldn't be broadcast, so voice chat only records while X is held and never
while B is held. If you press X while asking Vincent, nothing happens until you
let go of B.

The first press of X on the Quest asks for microphone permission; press again
after allowing it.

## One-time setup

The Photon Voice SDK is not in the repo yet. Whoever lands this branch does
steps 1–4 once and commits the result; everyone else only does step 5.

1. **Import Photon Voice 2** from the Unity Asset Store (Fusion here is 2.0.13,
   so the Fusion Hub's one-click Voice install, which needs Fusion 2.1, doesn't
   apply). In the import window, **untick** `Photon/PhotonChat`,
   `Photon/PhotonUnityNetworking`, `Photon/PhotonVoice/Code/Pun` and all of
   `Photon/PhotonVoice/Demos`.
2. **Weave the Voice assembly:** select
   `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` → Weaving
   Settings → Assemblies To Weave → add `PhotonVoice.Fusion` → **Apply**.
3. **Rebuild the avatar:** InnerFrame → Multiplayer → Rebuild Network Player
   Prefab, then **Fusion → Rebuild Object Table**.
4. Record the SDK in `docs/ASSETS.md` (the version is in the package's
   release notes) and commit with the new `.meta` files.
5. **Each machine:** in the Photon dashboard, create a **Voice** app (separate
   from the Fusion app) and paste its App ID into **Tools → Fusion → Realtime
   Settings → App Id Voice**. That asset is git-ignored: the repo is public.
   Ask Sonya or Serena for the team's Voice App ID.

## Test it

Same setup as `docs/multiplayer/README.md` → *Testing with one headset*
(region pinned, Quest awake, build first, then Play in the Editor).

1. Both Consoles show `(2 in room)`, then Photon Voice logs joining its room.
2. Hold X in the headset and speak: the Quest Console shows
   `[PlayerVoice] Talking`, and the Editor plays your voice from the headset
   player's head. Walk away and it gets quieter.
3. Hold V in the Editor (Game view focused) and speak into the computer's mic:
   you hear it in the headset, from the Editor player's head.
4. Hold B and X together in the headset: no voice reaches the Editor.
5. Leave the scene through the menu: voice stops with the room, and works
   again in the next scene.

To hear yourself with one device, turn on **Debug Echo Mode** on the
Recorder of your avatar while in Play mode.

## Not done yet

- No "who's talking" indicator on the avatars.
- Voice volume and distances are a first guess (`NetworkPlayerVoiceBuilder`).
- Other players don't hear Vincent's answers or your questions to him; that's
  a design decision for the Van Gogh track.
