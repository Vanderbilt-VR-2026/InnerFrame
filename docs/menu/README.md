# Player menu (VR)

A panel the player opens in front of them, in every scene with an XR Origin.
Code: `Assets/Script/Menu/`. Nothing needs to be added to a scene: it creates
itself on startup.

## Controls

| Do | How |
|---|---|
| Open / close the menu | Left controller **Menu button (≡)**. In the Editor: **M** |
| Move it | Point at the title bar (or touch it) and hold **grip**, then move. It turns to face you |
| Close it | **X** in the title bar, or the Menu button again |
| Go back | **Back to …**: the scene you came from (or the Gallery if you started here). Hidden when there's nowhere to go back to |

The panel opens 0.55 m in front of you, inside the controllers' 2 m ray.

Talking to Vincent isn't on the menu: in the Gallery (his portrait) or the
Painted Bedroom (his figure), hold the right controller's **B** button (**T**
in the Editor), speak and let go. See `docs/vangogh/README.md`.

**Without a headset** (Editor Play mode): click the Game view, press **M**, and
click the buttons with the mouse. In the Painted Bedroom's desktop mode the
menu opens in front of the desktop camera and frees the mouse; mouse-look
pauses until you close it (WASD still walks). Dragging the panel needs a
controller (a headset or the XR Interaction Simulator).

## Scripts

| Script | What it does |
|---|---|
| `PlayerMenu` | Creates itself on startup. Listens for the Menu button, remembers the previous scene, adds an XR UI EventSystem if the scene has none |
| `PlayerMenuPanel` | Builds the panel in code (world-space canvas), the drag handle (XR Grab Interactable on the title bar) and the buttons |

## Hardware tests (please run on a headset)

1. Press ≡: the panel appears in front of you, level and readable.
2. Grab the title bar and walk: it follows your hand, faces you, stays put when you let go.
3. Teleport away and back: the panel hasn't moved.
4. X closes it; ≡ opens it again in front of you.
5. From Gallery go to another scene, open the menu: **Back to Gallery** returns you.
