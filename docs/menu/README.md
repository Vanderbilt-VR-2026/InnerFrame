# Player menu (VR)

A panel the player opens in front of them, in every scene with an XR Origin.
Code: `Assets/Script/Menu/`. Nothing needs to be added to a scene: it creates
itself on startup.

It has one job: getting back to the Gallery (#160, #161). It is built in the
same look as the dialogue box and its hint (Libre Baskerville, the rounded
plate, chrome yellow), in one centred column:

- a grip bar to move it, and **X** to close it;
- **You are in** and the painting's name (*The Bedroom, 1888*);
- **Back to Gallery**, in every scene except the Gallery. In the Gallery
  there's nowhere to go back to, so this row says how to enter a painting
  instead;
- how to move and close it.

## Controls

| Do | How |
|---|---|
| Open / close the menu | Left controller **Menu button (≡)**. In the Editor: **M** |
| Move it | Point at the top bar (or touch it) and hold **grip**, then move. It turns to face you |
| Close it | **X**, or the Menu button again |
| Go back | **Back to Gallery**, from every scene except the Gallery. It always leads to the Gallery, whatever path brought you here |

The panel opens 0.6 m in front of you, slightly below eye level and inside the
controllers' 2 m ray.

**The wrist tag.** Outside the Gallery, a small **(≡) Menu** tag rides just
above the left controller and turns to face you, so a glance at your hand shows
where the way back is. It hides while the menu is open and in desktop mode.
To switch it off, untick `PlayerMenu.wristTag`.

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
| `PlayerMenu` | Creates itself on startup. Listens for the Menu button, decides where Back goes (`BackFrom`: the Gallery from anywhere else), adds the wrist tag outside the Gallery, adds an XR UI EventSystem if the scene has none |
| `PlayerMenuPanel` | Builds the panel in code (world-space canvas), the drag handle (XR Grab Interactable on the top bar) and the buttons |
| `MenuWristTag` | The **(≡) Menu** tag on the left controller |
| `MenuLook` | The shared look: the dialogue font and sprites, read from `Resources/VG_Caption`'s dialogue prefab, and the colours |
| `PaintingNames` | What players read for each scene ("The Bedroom", "The Starry Night"). Add a row when a painting joins the Gallery |

## Checks

**InnerFrame > Menu > Run Checks** (or, in batch,
`-executeMethod PlayerMenuCheck.RunBatch [-menuShots <folder>] -quit`) checks
the back rule, the panel's words, font, size and reach, and the wrist tag.
With `-menuShots` it also renders the panel and the tag to PNGs.

## Hardware tests (please run on a headset)

1. In a painting, press ≡: the panel appears in front of you, centred, level and readable.
2. It says **You are in** and the painting's name, and the yellow button says **Back to Gallery**.
3. Grab the top bar and walk: it follows your hand, faces you, stays put when you let go.
4. Teleport away and back: the panel hasn't moved.
5. X closes it; ≡ opens it again in front of you.
6. **Back to Gallery** fades you back to the Gallery's start, from the Painted Bedroom and from The Starry Night.
7. In the Gallery, press ≡: no Back button, a line about the glowing pedestals instead.
8. Outside the Gallery, look at your left controller: the **(≡) Menu** tag is readable and doesn't block the ray. It's gone in the Gallery.
