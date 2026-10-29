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
| `Editor/NetworkPlayerVoiceBuilder` | Called by both avatar builders (**InnerFrame → Multiplayer → Rebuild Network Player Prefab** and **Rebuild Painted Bedroom Avatar**): adds `VoiceNetworkObject`, `Recorder` and `PlayerVoice` to the root, and a 3D `AudioSource` + `Speaker` on `Head/Voice`. |

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

## Setup

The Photon Voice SDK (v2.64) is in the repo under `Assets/Photon/PhotonVoice`,
and `PhotonVoice.Fusion` is in Fusion's Assemblies To Weave. Each machine only
needs the App IDs:

1. **Tools → Fusion → Realtime Settings → App Settings**:
   - **App Id Fusion**: the team's Fusion App ID (ask Sonya)
   - **App Id Voice**: the team's Voice App ID (ask Serena)
   - **Fixed Region**: `us`
2. That asset is git-ignored: the repo is public, so the IDs never go in a commit.

The Voice app must be created in the Photon dashboard with **Photon SDK: Voice**.
An app of another type (Fusion, Realtime) makes the voice client fail with
`Failed to join room ... Error: 32752. Message: Unsupported Plugin`.

**After changing an avatar builder**, rebuild both avatars so they keep their
voice parts: **InnerFrame → Multiplayer → Rebuild Network Player Prefab** and
**Rebuild Painted Bedroom Avatar**.

## Test it

Same setup as `docs/multiplayer/README.md` → *Testing with one headset*
(region pinned, Quest awake, build first, then Play in the Editor).

1. Both Consoles show `(2 in room)`, then Photon Voice logs
   `Calling OpJoinOrCreateRoom for room name 'InnerFrame_<Scene>_voice'` with no error after it.
   Expected warnings: `TransmitEnabled is false` (push-to-talk is off until X is held) and
   `Voice client is busy (ConnectingToNameServer)` (Voice connects a moment after Fusion).
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
- Vincent's answers reach everyone in the room through his own Photon Voice
  stream (`VincentRoom`, see `docs/multiplayer/README.md`). Your question to
  him still isn't broadcast as voice chat.
