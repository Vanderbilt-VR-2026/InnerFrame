# Multiplayer (Photon Fusion 2, Shared Mode)

Everyone in the same scene joins the same room and sees each other as a
full body, the **Boy** or the **Girl** each player chose in the Entrance, in every scene (see "Player bodies" below). Shared objects can be picked up by anyone and
handed from one player to another.

Code: `Assets/Script/Network/`.

| Script | What it does |
|---|---|
| `NetworkSession` | Creates itself on startup. When a scene with an XR Origin loads (in the Editor without a headset: a desktop camera), joins room `InnerFrame_<SceneName>` (max 8 players) and spawns your avatar. `SceneFader` makes it leave the room before a scene change. |
| `DesktopTestRig` | Editor only. Without a headset, replaces a scene's XR Origin with a W A S D + mouse player, so the Editor can walk around and count as a player (see "Testing with one headset"). |
| `NetworkPlayer` | A player in the room: head and hands as three tracked points with no shapes, and `AvatarChoice`, the body they chose in the Entrance (sent to everyone). Copies your headset and controllers onto the points. Adds `PlayerAvatarBody` when it spawns. |
| `PlayerAvatarBody`, `PlayerAvatarRig` | The full body the others see, placed under the head and posed by Unity's Humanoid IK (see "Player bodies"). |
| `Editor/PlayerAvatarBuilder` | Builds `Resources/PlayerAvatars/Avatar_BoyWorker.prefab` and `Avatar_Girl.prefab`, with their materials and Animator, from the FBXs in `Avatars/`. Menu: **InnerFrame → Multiplayer → Build Player Avatars**. |
| `NetworkGrabbable` | Makes a grab object shared (see below). |
| `StarryNightLink` | On each avatar: shares The Starry Night's progress and its lantern, sketchbook and page (`docs/starry-night/README.md` → Two players). |
| `Editor/NetworkPlayerPrefabBuilder` | Builds `Resources/NetworkPlayer.prefab`, the one player prefab for every scene. Menu: **InnerFrame → Multiplayer → Rebuild Network Player Prefab**. |
| `Editor/NetworkGrabbableMenu` | Makes many objects shared at once (see below). |
| `Editor/PlayerAvatarPoseTest`, `Editor/PlayerAvatarRoomTest` | Checks for the player bodies (see "Player bodies"). |

## Player bodies

Each player picks who they'll be on the Entrance's "Choose Who You'll Be" page, **Boy** or **Girl**
(`EntranceChoice.Role`), and everyone else sees that body in every scene: `NetworkPlayer.AvatarChoice` carries the
choice into each room, and `PlayerAvatarBody` shows `PlayerAvatarBody.Order[choice]` from
`Resources/PlayerAvatars`. Starting a scene directly (the Editor, skipping the Entrance) gives the Boy. Your own body
stays hidden, and it doesn't animate either.

| Choice | Prefab | Model |
|---|---|---|
| Boy | `Avatar_BoyWorker` | a boy in a worker's cap and jacket, 1.70 m; Tripo, rigged in Mixamo |
| Girl | `Avatar_Girl` | a girl in a red Victorian dress and bonnet, 1.60 m; Tripo, rigged with Rigify in Blender (`Art/Source/Avatars/Girl/`), with 4 × 3 skirt bones (`Skirt_Front/Back/Left/Right_1-3`) under the hips. Body bones only, no fingers |

The skirt bones follow the hips; the skirt's front and sides also follow the thighs a little (in the weights), so
walking doesn't push the legs through it. Nothing animates the skirt bones in Unity yet: a deep crouch can show a knee
through the front.

- **Size:** scaled to the player's *standing* eye height (0.6 to 1.25 times the model). Crouching or sitting doesn't
  shrink it; the hips drop and the knees bend instead, up to 35% of eye height, then the back leans.
- **Feet:** stand on the floor found below the head (the hit nearest the last floor, so a table under your head isn't
  taken for it). Each foot stays planted and steps, one at a time, once it is 22 cm (times the body's scale) from its
  place under the body or the body has turned 35°. A foot left slightly off tidies up after 0.8 s; a jump of 1 m or more
  (a teleport) snaps both.
- **Arms and head:** the hands reach for the controllers and the head looks where the player looks.
- The body trails the head by one frame (Fusion moves the head just before LateUpdate), together with its arms.

To change a body, rig a model (Mixamo: `.claude/skills/tripo-mixamo-character/`; the Girl's Blender scripts:
`Art/Source/Avatars/Girl/scripts/`), export it with a one-frame standing take, put the FBX and its
`Textures/<name>_BaseColor.jpg` in `Assets/Script/Network/Avatars/`, set its name in `PlayerAvatarBody.Order` (the
index is the Entrance's `VisitorRole`), and run **Build Player Avatars**. That menu re-saves every body's importer;
commit only the ones you meant to change.

Checks (close Unity first, or run them from the Editor's Play mode):

```
Unity -batchmode -projectPath . -executeMethod PlayerAvatarPoseTest.RunFromCommandLine
```

`PlayerAvatarPoseTest` poses each body on an empty floor (rest, reach, wave, crouch, walk) and fails if a hand misses
its controller by more than 15 cm, the eyes miss the head by more than 12 cm (except crouching), a foot goes through the
floor or both float, the hips bone drops less than 20 cm crouching, or a foot is left 50 cm behind walking. Pictures go
to `Temp/PlayerAvatarPoses` (or `AVATAR_POSE_OUT`). It calls the posing code directly, so the sizing to standing
height, the floor finding, the tidy-up and the teleport snap are **not** covered: check those on a headset.

`PlayerAvatarRoomTest` (`-executeMethod PlayerAvatarRoomTest.RunFromCommandLine`, with a second player in the Painted
Bedroom's room, e.g. a headset) checks that each player has the body they chose, the others' are shown, your own is
hidden, and nothing else of the players shows. It skips the Entrance: set `AVATAR_CHOICE=1` for the Girl.

On a headset, with two players: stand, crouch, sit, walk, turn on the spot and teleport, and watch the other player's
body: feet on the floor, no sliding, hands on the controllers.

## One player prefab for every scene

The per-scene placeholder avatars are gone (the coloured head with a visor that the Gallery and most scenes showed,
and the Painted Bedroom's Van Gogh head with its straw hat, `NetworkPlayer_PaintedBedroom` and
`Editor/PaintedAvatarBuilder`). Every scene uses `Resources/NetworkPlayer`: three tracked points, nothing to see,
and the chosen body over them.

`NetworkSession` still looks for `Resources/NetworkPlayer_<SceneName>` before `Resources/NetworkPlayer`; don't add
one, or that scene's players lose the shared links on `NetworkPlayer` (voice, Vincent, the mirror, The Starry Night)
unless you copy them all. If you change the prefab, it needs `NetworkObject` and `NetworkPlayer` on the root with
**Head**, **Left Hand** and **Right Hand** set (head looks along +Z), a `NetworkTransform` on each, and no colliders.
Fusion registers it when it's imported; **Tools → Fusion → Rebuild Prefab Table** does it by hand.

## Making an object interactable by both players

**Add one script: `NetworkGrabbable`, on the object's root** (the same
GameObject as its XR Grab Interactable). Unity adds whatever is missing:
XR Grab Interactable, Rigidbody, Network Object and Network Transform.

Two ways:

- **One object:** select it → Add Component → *Network Grabbable*.
- **Many objects:** select them all → **InnerFrame → Multiplayer → Make
  Selected Objects Shared Grabbables**. Undo works.

Then **save the scene (or prefab)**. Fusion registers scene objects when the
scene is saved.

Checklist for a new shared object:

1. It works as a normal XR grab object first (it has a Collider, a Rigidbody
   and an XR Grab Interactable).
2. `NetworkGrabbable` is on the same GameObject as the XR Grab Interactable,
   **not** on a child, and no parent has a Network Object.
3. The scene is in **Build Settings**. If it isn't, its shared objects don't
   sync (the Console warns).
4. On the Network Object, leave **Is Master Client Object** off and
   **Allow State Authority Override** on. `NetworkGrabbable` sets these
   automatically.

### The Painted Bedroom

Open `PaintedBedroom.unity`, run **InnerFrame → Multiplayer → Make Painted
Bedroom Objects Shared**, and save the scene. It adds `NetworkGrabbable` to
every movable object (each one with a `PaintedBedroomMovable` and an XR Grab
Interactable) and skips the ones that already have it, so it's safe to run
again.

The one exception is Vincent's **Mirror**: it's shared by `MirrorLink` (PR
#120) instead, so the menu leaves it out and, if an older run made it shared,
takes `NetworkGrabbable`, `NetworkTransform` and `NetworkObject` off it again.

The bedroom's own scripts work with shared objects:

- Carrying or inspecting an object on desktop asks to own it first, so the
  other players see it move.
- **Backspace** (put everything back) moves each object once you own it, so
  everyone sees the room reset.
- An object with physics that rests until it's first grabbed stays woken
  when its owner changes.

Inspecting is still only on your screen apart from the move itself: the
others see the object float in front of you, not why.

Works on objects already in a scene and on prefabs. For a bedroom prefab
(`Assets/VanGoghBedroom/Prefabs/Tumbler.prefab` and the others), add it to the
prefab so every scene using it gets it. The bedroom belongs to Rowling, so
check with them first.

## How hand-off works

Whoever last grabbed an object owns it. The owner's physics moves the object,
and everyone else's copy is kinematic and follows the owner's position.

When player B grabs a cup player A is holding, B takes ownership and A's hand
lets go. When the owner drops or throws it, their physics plays out the fall
and everyone sees the result.

## Testing with one headset

Run a build on the Quest and press Play in the Editor at the same time; the
Editor is the second player.

Without a headset the Editor plays as a **desktop player**, and it still
joins the room: others see your avatar's head where your Game view camera is,
with its hands hanging at its sides. In scenes that only have an XR Origin
(the Gallery), `DesktopTestRig` swaps one in for you: **W A S D** to walk,
click and move the mouse to look (**Esc** frees the mouse, or hold the right
button), **Shift** to go faster. The dot in the middle of the view is an XR
ray: when it turns yellow you're aiming at something pressable, such as the
pedestal's scene button, and **E** (or a click while the mouse is captured)
presses it the way a controller would. It grabs nothing; grabbable objects
are skipped. The Painted Bedroom keeps its own desktop rig.
Both are Editor only; Quest builds don't have them. For grabbing and handing
things over from the Editor, use the XR Interaction Simulator instead (below).

- **Pin a region first:** Tools → Fusion → Realtime Settings → Fixed Region,
  for example `us`. Otherwise the two devices can land in different regions and
  never meet.
- **Internet Access:** Player Settings → Android → Internet Access → Require.

Order matters:

1. Keep the Quest awake. A sleeping headset pauses the app and Photon drops it.
   Run `adb shell am broadcast -a com.oculus.vrpowermanager.prox_close` (undo
   with `...automation_disable`, or reboot).
2. **Patch and Run** (or Build and Run) to the Quest with the Editor *not* in
   Play mode. Building stops Play mode and leaves the room.
3. Once the app is running in the headset, press **Play** in the Editor.
   Both Consoles should say `(2 in room)`. Each player has its own Console;
   the Quest's goes to Android Logcat.

### Moving the Editor player (XR Interaction Simulator)

One-time setup, Editor only (Quest builds are unaffected):

1. Package Manager → XR Interaction Toolkit → Samples → import
   **XR Interaction Simulator**.
2. Project Settings → XR Plug-in Management → XR Interaction Toolkit → tick
   **Use XR Interaction Simulator in scenes** and keep **Instantiate In Editor
   Only** ticked.

Controls in Play mode (click the Game view first):

| Do | Keys |
|---|---|
| Look around | Hold right mouse button and move the mouse (or arrow keys) |
| Move | W A S D, Q / E for down / up |
| Choose what you move | Tab cycles head → left controller → right controller; or H (head), `[` (left), `]` (right) |
| Grab (grip) | G |
| Trigger | T |
| Thumbstick | I J K L |
| Reset pose | R |

To hand something over: Tab to a controller, move it onto the object, press
G to grab, move it towards the headset player, and let them grab it.

With one headset you can check that a shared object syncs: pick it up in the
headset and watch it move in the Editor's Scene view, then drop it and check
it lands in the same place on both. A real hand-off needs a second pair of
hands: two headsets, or the XR Device Simulator (an XR Interaction Toolkit
sample) driving the Editor player.

## Vincent

Talking to Vincent and the Painted Bedroom's mirror scene are shared: see `docs/vangogh/README.md` →
Shared Vincent (`VincentLink`, `MirrorLink` on each avatar).

## The Starry Night

Same setup as the other scenes: `NetworkSession` joins `InnerFrame_StarryNight` by itself. Its objects are spawned at
runtime, so instead of `NetworkGrabbable` the scene is shared by `StarryNightLink` on each avatar, the same pattern as
`MirrorLink`: each player writes its own avatar's state, everyone reads it. See `docs/starry-night/README.md` → Two
players.

## Not synced (yet)

- Hinged doors (`BedroomHingedDoor`) and anything that isn't a grab object.
- Objects created at runtime. Spawn those with `Runner.Spawn` from a
  prefab that has a Network Object.
- Voice: see `docs/voice/README.md` (push-to-talk with Photon Voice).
