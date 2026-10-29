# Multiplayer (Photon Fusion 2, Shared Mode)

Everyone in the same scene joins the same room and sees each other as a
head with two hands (each scene can have its own avatar, see below). Shared objects can be picked up by anyone and
handed from one player to another.

Code: `Assets/Script/Network/`.

| Script | What it does |
|---|---|
| `NetworkSession` | Creates itself on startup. When a scene with an XR Origin loads (in the Editor without a headset: a desktop camera), joins room `InnerFrame_<SceneName>` (max 8 players) and spawns your avatar. `SceneFader` makes it leave the room before a scene change. |
| `DesktopTestRig` | Editor only. Without a headset, replaces a scene's XR Origin with a W A S D + mouse player, so the Editor can walk around and count as a player (see "Testing with one headset"). |
| `NetworkPlayer` | The avatar. Copies your headset and controllers onto it; your own copy is hidden. |
| `NetworkGrabbable` | Makes a grab object shared (see below). |
| `Editor/NetworkPlayerPrefabBuilder` | Builds `Resources/NetworkPlayer.prefab`. Menu: **InnerFrame → Multiplayer → Rebuild Network Player Prefab**. |
| `Editor/PaintedAvatarBuilder` | Builds the Painted Bedroom's avatar, `Resources/NetworkPlayer_PaintedBedroom.prefab`, and its textures, materials and meshes in `PaintedAvatar/`. Menu: **InnerFrame → Multiplayer → Rebuild Painted Bedroom Avatar**. |
| `Editor/NetworkGrabbableMenu` | Makes many objects shared at once (see below). |

## A different avatar per scene

`NetworkSession` first looks for `Resources/NetworkPlayer_<SceneName>`, and
uses `Resources/NetworkPlayer` if there isn't one. Everyone in a room is in
the same scene, so everyone sees the same kind of avatar there.

| Scene | Avatar |
|---|---|
| `PaintedBedroom` | `NetworkPlayer_PaintedBedroom`: a Van Gogh head and two hands, painted like the room. Brushstroke textures drawn by the room's own recipe (`strokeCanvas()` in `Art/Source/PaintedBedroom/web/index.html`, ported to C#), matte materials with the room's glow, and the room's dark blue ink outlines. Skin, a ginger beard, and the straw hat from the painting. The hat band and the cuffs are in the player's colour |
| Everything else | `NetworkPlayer`: a coloured head with a visor and two hands |

To change the painted avatar, edit `PaintedAvatarBuilder.cs` (shapes, sizes,
palettes) and run its menu item again. Everything it makes keeps its GUID, so
nothing that points at it breaks.

To give another scene its own avatar, make a prefab named
`NetworkPlayer_<SceneName>` in a `Resources` folder. The easiest way is to
duplicate an existing avatar prefab and change its pieces. It needs:

- `NetworkObject` and `NetworkPlayer` on the root, with **Head**, **Left
  Hand** and **Right Hand** set. Head looks along +Z; each hand points along +Z.
- A `NetworkTransform` on each of those three parts. Put the shapes under
  them, so you can scale and offset the shapes freely.
- No colliders, so avatars don't push things.
- **Tinted** on `NetworkPlayer`: the renderers that take the player's colour.
  If it's empty, every renderer except one named `Visor` is tinted.

Fusion registers the prefab when it's imported; **Tools → Fusion → Rebuild
Prefab Table** does it by hand.

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

## Not synced (yet)

- Hinged doors (`BedroomHingedDoor`) and anything that isn't a grab object.
- Objects created at runtime. Spawn those with `Runner.Spawn` from a
  prefab that has a Network Object.
- Voice: see `docs/voice/README.md` (push-to-talk with Photon Voice).
