# Multiplayer (Photon Fusion 2, Shared Mode)

Everyone in the same scene joins the same room and sees each other as a
coloured head with two hands. Shared objects can be picked up by anyone and
handed from one player to another.

Code: `Assets/Script/Network/`.

| Script | What it does |
|---|---|
| `NetworkSession` | Creates itself on startup. When a scene with an XR Origin loads, joins room `InnerFrame_<SceneName>` (max 8 players) and spawns your avatar. `SceneFader` makes it leave the room before a scene change. |
| `NetworkPlayer` | The avatar. Copies your headset and controllers onto it; your own copy is hidden. |
| `NetworkGrabbable` | Makes a grab object shared (see below). |
| `Editor/NetworkPlayerPrefabBuilder` | Builds `Resources/NetworkPlayer.prefab`. Menu: **InnerFrame → Multiplayer → Rebuild Network Player Prefab**. |
| `Editor/NetworkGrabbableMenu` | Makes many objects shared at once (see below). |

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

## Not synced (yet)

- Hinged doors (`BedroomHingedDoor`) and anything that isn't a grab object.
- Objects created at runtime. Spawn those with `Runner.Spawn` from a
  prefab that has a Network Object.
- Voice.
