# Talking to Vincent (#73)

In the Gallery, Van Gogh's 1887 self-portrait hangs beside *The Bedroom*. Hold the right
controller's **B** button, ask him something, and let go: the question goes to a small
proxy on a computer nearby (speech-to-text, a local language model playing Vincent in
October 1888, text-to-speech), and his answer plays from the portrait.

- Code and assets: `Assets/VanGogh/`. Nothing is added to a scene: `VanGoghSpawner`
  places the portrait when the scene named `Gallery` loads (the same pattern as
  `PlayerMenu`).
- **The gallery portrait** is enlarged as much as *The Bedroom*. *The Bedroom* hangs 3.0 m wide for its real 92.3 cm,
  which is ×3.25, so the 41 × 32.5 cm portrait shows at 1.06 × 1.33 m.
  - It is framed like *The Bedroom*: four boxes, 18 cm wide and 12 cm deep, with *The Bedroom* frame's own material,
    found at runtime and shared, not copied.
  - It hangs on the same wall at the same centre height, on the side away from the placard, with 1.2 m between the
    two frames. Its frame ends 0.70 m from the corner; nothing else hangs on that wall.
  - Its caption hangs below the frame, 25 cm out from the wall.
  - The numbers come from `Art/Source/Gallery/build_gallery.py`; see the constants at the top of `VanGoghSpawner`.
- In the Painted Bedroom, a 3D Vincent (Tripo model) stands by the left wall with
  an easel, palette and brush, facing where you arrive, and answers the same way.
  He is 1.70 m tall in the room, as in The Starry Night (Cici, 2026-11-17; `VincentScale` in
  `VanGoghSpawner`, 1.0). The XR rig is scaled 0.8 here as there, so he feels about 2.1 m to you in both;
  his eyes (1.55 m) are above a standing visitor's (1.28 m). He was 1.40 m before. He was made with Tripo on a paid plan, whose terms give
  paid users the rights to their models (see `docs/ASSETS.md`).
  - **His easel and palette** are Tripo models too (Smart Mesh P2.0 from painted reference images, 2026-10-27), cleaned
    by `Art/Source/VanGogh/Props/clean_easel.py` and `clean_palette.py` (no decimation: 4,727 and 4,884 triangles, one
    1024² base colour each). Their materials, `Art/Props/M_Easel.mat` and `M_Palette.mat`, are made by the asset tool
    with the room furniture's URP Lit settings (no metal, no gloss, no highlights or reflections, two-sided). The canvas
    is still our own plane, `VG_Canvas`, standing on the new ledge at the old canvas's place, so the mirror scene is
    unchanged. The scripts take the exported GLB as their argument; the licence of these two models for the public repo
    is not yet confirmed (see `docs/ASSETS.md`).
- Why B and not A: A is already bound to XRI's Jump on the right hand.
- **Captions:** `Scripts/VanGoghCaption.cs`, made by `SpeechToProxy`, in one of two styles, chosen by the `style` field
  on `Resources/VG_Caption` (in `Assets/Shared/UI/Resources/`; the asset tool sets **Box**; switch back to **Sign** with that one field).
- **Box style** (the default, from Cici's concepts V1–V3, 2026-11-04; after the 11/03 playtest found the sign and the
  pill too easy to miss and VR text too small):
  - His words sit in a dark dialogue box low in your view: night blue at 88 % with a 4 mm chrome-yellow border, his
    name on a tab centred on that border at the left (a star in a thin circle, then *Vincent*; opaque, drawn over the
    border), the words in cream, left-aligned, 3.2 cm capitals, up to three lines. The words start one capital height
    below the tab (the top padding is computed from the tab's size). A small star in its bottom-right corner pulses
    while more pages follow.
  - Just above its left end, a hint: a small star, the title (*The Yellow House* in the Painted Bedroom, a placeholder;
    none at the Gallery portrait), and one instruction line on a clean, softly edged plate, 2 cm above the tab. The
    plate is the box's night blue at "70 %" as the concept composites it (in sRGB); the project blends in linear space,
    where that takes 85 % (`plate_alpha` in the sprite script), else it reads grey over light things. The line
    shows, first, his state (*Listening…*, *Vincent is thinking…*, *Vincent can't hear you right now*, or who else is
    asking); else the mirror's next step (*Take the mirror off the wall*, *Hold the mirror up to Vincent*, *Let go: the
    mirror stays here*, *Hang the mirror back on the wall*, from `MirrorScene.Now`); else *Hold (B) to ask Vincent*
    with the B in its circle (B in the Editor; T also accepted).
  - The hint shows within 4 m of Vincent or the portrait; the box whenever he speaks. Both fade over 0.2 s.
  - Both hang 1.3 m ahead of your eyes, 15° below your gaze (`VG_FollowGroup`). They stay put until your gaze is 20°
    away, then ease after it (98 % in 0.5 s); they follow pitch between −30° and +45° of gaze and stay upright. When they
    appear they're already in place. Sizes are world metres: 0.95 m wide is about 40° at 1.3 m whatever the rig's scale.
  - They are drawn over the room (a UI material with `unity_GUIZTestMode` = Always), so the easel or the chair never cut
    through them. Something nearer than 1.3 m (the easel, his palette, the mirror you hold) is still drawn behind them:
    judge on the Quest whether that feels wrong.
  - Pages, timing and fades are the same rules as the sign's (below), at one size: a sentence that needs more than three
    lines splits at the comma nearest its middle.
  - Code: `Assets/Shared/UI/Scripts/` (`VG_DialogueUI` and its parts `VG_FollowGroup`, `VG_DialogueBox`, `VG_Hint`, `VG_Paging`,
    `VG_UI`, with `VG_DialogueSettings`). They know nothing about Vincent or the mirror; `VanGoghCaption` tells them
    what to show. See *Use the dialogue UI in another scene* below.
  - Sprites (`Assets/Shared/UI/Art/`): `VG_Box.png`, `VG_NameTab.png`, `VG_HintPlate.png` (9-slice, 64 px borders), `VG_Star.png`,
    `VG_StarRing.png`, drawn by `Art/Source/VanGogh/make_box_sprites.py` (Pillow); the asset tool imports them (borders,
    mipmaps, max 1024). The key circle is the sign's `VG_Caption_Button.png`.
- **Sign style** (2026-10-27, storybook style, from Cici's concept):
  - His words, scripted lines and AI answers alike, appear on the wooden sign Cici made with Tripo, baked flat into a
    picture (`Art/Source/VanGogh/Caption/bake_sign.py` renders its front twice: `VG_Sign_A.png` unlit, `VG_Sign_B.png`
    softly lit, the default; `sign_rect.json` says where its parchment panel is). The sign is a fixed 1.00 m wide, so his
    words show one sentence per page, each page's share of his voice clip being its share of the characters (without a
    clip, 0.06 s a character and at least 2 s a page), with a 0.15 s cross-fade between pages. A sentence shrinks from
    2.8 cm to 2.2 cm capitals to stay on two lines, then takes a third line at the size that fits the panel, then splits
    at the comma nearest its middle. The v1.3 parchment lies over the panel, because the ink's contrast on the bake alone
    falls to 4.4:1 in its darkest strokes. They fade out a second after his voice stops.
  - Hints appear on a small dark pill: *Hold B to ask Vincent* (*B* on the keyboard; *T* also accepted) with the key in a circle,
    *Listening…*, *Vincent is thinking…*, or *Vincent can't hear you right now* for 3 s. The pill hides while he
    speaks.
  - Over Vincent the pill sits 8 cm above his head and his words above it. Under the portrait his words hang below
    the frame, with the pill below them. Both turn to face your head and fade in and out over 0.2 s.
  - Sizes are world metres, named at the top of `VanGoghCaption`: the sign 1.00 m wide (its panel gives a 0.76 × 0.10 m
    text area), 2.8 cm capitals shrinking to 2.2 cm; a 6 cm pill. They are guesses until tried on a Quest.
  - The text is uGUI Text in Libre Baskerville (`Assets/Shared/UI/Fonts`, OFL), laid out at a large font size and scaled down so it
    stays sharp. TextMesh Pro's Essential Resources aren't in the project, and importing them would be a shared change.
  - The parchment, pill and button sprites are drawn by `Art/Source/VanGogh/make_caption_sprites.py`. The asset tool
    imports the baked sign and sets the sprites' 9-slice borders, and builds `Resources/VG_Caption` (in `Assets/Shared/UI/`), which holds them
    and the font.
- Each question logs one line starting with `[VanGogh]` (times in ms, plus the proxy's
  own timings).
- After changing the image or its material, run **Tools > InnerFrame > Van Gogh > Rebuild
  assets**.

## Use the dialogue UI in another scene

The box and hint are one prefab, `Assets/Shared/UI/Prefabs/VG_DialogueUI.prefab` (made by the asset tool), with no tie to
Vincent, the mirror or the Bedroom.

1. Put the prefab in the scene (or `Instantiate` it), anywhere; it follows the main camera.
2. Give it a settings asset: `VG_Dialogue_Bedroom` (as approved: 1.3 m, 15° under the gaze, a 40° box) or
   `VG_Dialogue_StarryNight` (1.6 m and the blockout's text sizes; `belowGaze` is the field to tune so the box
   clears the village). For a new scene, duplicate one. Set it before the first call; the UI builds itself then.
   - In each asset: distance, `belowGaze`, dead zone, gaze pitch clamp, ease time; sizes as angles (box width,
     capitals); colours and tints; the hint's title and its fade-in on each change.
   - The tool makes these two once and then leaves them alone, so tuning in the Inspector stays.
3. Call it:
   - `Say(speaker, text, clip)`: one sentence a page, timed to the clip (0.06 s a character without one). Play the
     clip yourself, and set `Voice` to that AudioSource so the pages keep its time.
   - `Clear()`: the words stay a second, then fade.
   - `SetHint(title, line)`: `title` null takes the asset's; `""` hides the title row. `{B}` in the line draws B in
     a button circle. A null line hides the hint.
   - `IsSpeaking`: from `Say` until a second after `Clear`.
   - Also: `Prewarm(texts)` (font atlas, at load), `Snap()` (straight into place), `Head` (follow something other
     than the main camera), `autoStep = false` + `Step(dt)` (drive it from your own clock), `HintLine`, `BoxHeight`,
     `VG_Paging.Estimate(text)` (a clip-less line's length).
4. Line queues, priorities and what the hint says are the scene's own logic: in the Bedroom `VanGoghCaption`, in The
   Starry Night `SN_Voice` and `SN_Game`.

## Run the proxy (on a Mac)

The proxy is `Tools/VanGoghProxy` (branch `feat/vangogh-proxy`; see its README).

```
ollama serve                                    # one terminal, leave it running
cd Tools/VanGoghProxy && .venv/bin/python proxy.py   # another terminal: 127.0.0.1:8000
```

## Test in the Editor

1. Start the proxy.
2. Open `Assets/Scenes/Gallery.unity` and press Play (no headset needed).
3. Walk to the portrait, hold **T**, speak, let go. The pill should go *Listening…*,
   then *Vincent is thinking…*, and then he answers, with his words in the wooden frame. macOS asks once for microphone
   access for Unity.

## Test on the Quest

Build with the Android profile as usual. Then either:

- **USB:** `adb reverse tcp:8000 tcp:8000`. The app's default `http://localhost:8000`
  then reaches the Mac's proxy. Re-run the command after every reconnect.
- **Wi-Fi hotspot:** start the proxy with `HOST=0.0.0.0 ACCESS_CODE=<something>`.
  Create **Assets > Create > InnerFrame > AI Proxy Settings** in a `Resources` folder,
  named `AIProxySettings`, and set Server Url to `http://<Mac's IP>:8000` and Access Code
  to the same code. Don't commit that asset: the repo is public.

The first press asks for microphone permission; press again after allowing it.
`adb logcat -s Unity | grep VanGogh` shows each question's timings.

Plain `http://` needs **Player Settings > Other Settings > Configuration > Allow downloads
over HTTP = Always allowed**; this feature sets it.

## Shared Vincent (multiplayer)

With other players in the room (`docs/multiplayer/README.md`), Vincent is the same for everyone, in the Gallery and
in the Painted Bedroom.

**Talking to him** (both scenes):

- When someone holds B, everyone else's hint says *Player 1 is asking Vincent…* and B does nothing for them.
- When his answer comes, everyone hears it from him and sees his words; the hint shows what was asked
  (*Player 1 asked: "…"*). When he stops, anyone may ask.
- If two players press B at once, the lower player number goes first; the other's question is dropped.
- A player who leaves mid-question frees him for the others; a lock nobody frees ends after 75 s.

**The mirror scene** (Painted Bedroom, #77):

- Whoever holds the mirror, everyone sees it in their hands; nobody else can take it meanwhile. Two grabs at the same
  moment: the lower player number keeps it. Let go, and every headset does the same: near his face before he's done,
  it glides to the spot beside the canvas; anywhere else, it hangs itself back on the wall (`MirrorScene.Rehang`).
- One headset decides the scene: the player already deciding, or (the first one in, or when the decider leaves) the
  lowest player number. Everyone sees the same canvas stage and hears each scripted line once, together.
- A player who takes over carries on from where the canvas was; a player who joins mid-portrait sees it as it is.
- His scripted lines wait while anyone is asking him, and never play over your own question.

**Everyone must reach the same proxy**, because each headset plays his answers from it (`audio_url`):

- **Quest + Editor on one Mac:** the USB setup above already does it: the Quest's `localhost:8000` is the Mac.
- **Several headsets:** the Wi-Fi setup above on one Mac, with every headset's AI Proxy Settings pointing at it.
- A headset that can't reach it still shows his words, timed as if he spoke them, and logs a warning.
- His scripted mirror lines are recorded clips in the app, so they need no proxy.

**Code** (both on each player's avatar; both avatar builders add them):

- `Assets/Script/Network/VincentLink.cs`: questions and answers (Fusion RPCs from the asker's avatar; texts in
  100-character pieces, since a normal RPC carries at most 512 bytes). Hooks marked *Shared Vincent* in
  `Scripts/SpeechToProxy.cs` (`IsAsking`, `Answered`, `BlockedBy`, `SayRemote`).
- `Assets/Script/Network/MirrorLink.cs`: the mirror and the scene (networked properties: the holder's mirror pose; the
  decider's stage, progress, scene and latest line). Hooks marked *Shared mirror scene* in `Scripts/MirrorScene.cs`
  (`Follow`, `HeldByOther`, `ApplyFromRoom`, `SayLine`, `FollowPose`, `Rehang`, `Resume`...).
- Alone, or outside a room, everything runs as before.
- After pulling this, rebuild both avatars: **InnerFrame → Multiplayer → Rebuild Network Player Prefab** and
  **Rebuild Painted Bedroom Avatar**.

### Test it (Quest + Editor)

Start the proxy, `adb reverse tcp:8000 tcp:8000`, build to the Quest, then press Play in the Editor
(as in `docs/multiplayer/README.md`). Both Consoles: `(2 in room)`.

Gallery:

1. Hold B in the headset and ask something. The Editor's hint shows *Player N is asking Vincent…*; B does nothing there.
2. The answer plays in both, from the portrait; the Editor's hint shows the question.
3. When he stops, ask from the Editor (B; T also accepted): the same, the other way round.
4. Press B and T at the same moment: only the lower player number's question is answered.

Painted Bedroom (both players go there through the menu or the door):

5. His greeting plays once in both, at the same time (the first player in decides).
6. Take the mirror in the headset: the Editor sees it move with your hand, and can't take it.
7. Hold it beside the canvas: both canvases go sketch → colour → finished together, and each line plays once in both.
8. Let go near his face: the mirror goes to the spot beside the canvas in both. Let go far from him: back on the wall in
   both.
9. Ask him (B) while he's painting: both hear the answer, and it fits what's happening.
10. Leave the room on the deciding player's side mid-portrait: the other carries on from the same canvas stage.

Known limits: the held mirror moves on the others' screens at the network's rate (no smoothing yet), so it can look
slightly steppy; and a scripted line that started just before someone pressed B can be cut by the answer. Let go right
at the zone's edge (1.0 m from his head), two headsets may disagree for that release: one parks it, the other hangs it
back, because the others see the holder's pose a network tick late. Not seen yet; untested.

## Hold the mirror (Painted Bedroom)

Vincent is painting himself and needs a mirror (concept sheet, frame 07).
1. About 2 s after you arrive he greets you. Ten seconds later (or as soon as he has answered your first question) he
   explains, and asks you to take the mirror off the back wall, by the towel.
2. Pick it up with the ray (point at it from up to 2 m away; on the Quest the glass is 1.8–2.3 m up, out of a hand's
   reach). You can still walk around.
   Until you do, a solid chrome-yellow outline glows around it on the wall.
3. Bring it near his face: within 1.0 m of his head, in any orientation. He paints. The canvas goes from blank to a
   charcoal sketch, to colour at halfway, to the finished 1887 self-portrait, and he comments at each stage. While you
   hold it, a dashed outline beside the canvas shows a good place; it pulses while the mirror is too far, and turns
   solid and brighter once it's near enough.
4. Move it away and he pauses; nothing is lost, and he carries on from the same point when it's back. After 1.5 s held
   away he asks for it back, at most once every 10 s.
5. Let go near him before he's done: the mirror glides (0.25 s) to the outline's spot beside the canvas, upright, glass
   to him, and stays there as if on a stand while he finishes. Take it again whenever you like.
6. Let go anywhere else, or after he's done: it hangs itself back on the wall. When the portrait is finished he thanks
   you.

Talking with B works the whole time. His scripted lines never cut into a question or an answer: the newest one waits
until his voice is free. B stops a scripted line, as it stops any of his answers.

- **Code:** `Assets/VanGogh/Scripts/MirrorScene.cs`, on the `VG_Vincent` prefab (the asset tool adds it).
  - All the decisions are in `MirrorScene.Decide(...)`, which takes plain inputs (time step, held, the mirror's centre,
    voice busy, has answered), so it can be tested without XR. `Track(...)` runs on every headset after it: the glide,
    the outlines, and `MirrorScene.Now`, the step the hint shows (asked, held outside, held inside, parked, finished
    away from the wall).
  - Releases go through `MirrorScene.Rehang()`, which `MirrorLink` also calls on every headset: inside the zone and
    unfinished → the Target pose; otherwise the wall. Both are teleports of the mirror's kinematic body (transform,
    Rigidbody and XRI's target pose), so it doesn't drift and interpolation doesn't flash it back toward the hand.
  - `VG_MirrorOutline.cs`: the two outlines, square tubes in a generated mesh (unlit, depth-tested, `VG_Outline.mat`),
    one draw call while shown, never both at once.
  - In Play mode the Scene view shows the zone as a yellow sphere around his head, with a line to the target.
- **Constants** (top of `MirrorScene`):

  | Constant | Value |
  |---|---|
  | `EyeHeight` | 1.55 on the model, which is 1.28 m in the room |
  | `BesideCanvas` | 0.25 m: the target is half the canvas's width plus this, sideways from its centre |
  | `ZoneRadius` | 1.0 m from the glass's centre to his head (the target is 0.88 m out); no facing check |
  | `GlideSeconds` | 0.25 s, from where you let go to the Target pose |
  | `PaintSeconds` | 24 s near his face (held or parked), in total |
  | When he speaks | greet at 2 s; explain 10 s after the greeting ends; ask again 20 s after the ask; the "back" line after 1.5 s held away, at most every 10 s |
  | Outlines (`VG_MirrorOutline`) | #E9C14A square tubes, 2 cm outside the frame: on the wall 18 mm (seen from across the room); the target 9 mm, 6–12 mm while pulsing at 1 Hz, dashes 4 cm with 2.5 cm gaps; #FFE286 when in place |

  All of these are guesses until tried on a Quest.
- **The mirror** is the room's own `Mirror`, with the XR Grab Interactable the room's builder gave it; no second grab.
  - The room's physics table has it as not liftable, and this scene needs it picked up, so `MirrorScene` switches that
    same grab on while Vincent is in the room: in the first frame (after the room has applied its table) and once more
    a second later, logging `[VanGogh] mirror grab on`. Nothing in the room's assets or the table changes.
  - Its glass is its local −Z. Its centre is the `Mirror_00` part; if that is missing (the room's meshes are
    regenerated), a warning names the mirror's parts and the whole mirror's centre is used.
  - The reflection is faked: it keeps its painted glass, and there is no second camera.
- **The scene for his answers:** `MirrorScene` keeps `SpeechToProxy.SceneContext` set to one sentence about what is
  happening (mirror on the wall, held, sketching, colour, paused, finished; the exact texts are at the top of
  `MirrorScene`). Each question sends it as the form field `scene`. The proxy uses it once `feat/vangogh-persona` is
  merged; until then it ignores it. The Gallery portrait never sets it, so its requests are unchanged.
- **Multiplayer (#77):** shared; see *Shared Vincent* above (`MirrorLink`).
- **What the model can't do:** Vincent is imported as a still mesh (no rig, no animation), so he doesn't move his arm
  or turn his head; only the brush in his hand makes a small loop while he paints. He doesn't look at you or at the
  mirror.
- **Canvas images:** `Assets/VanGogh/Art/Canvas/`, made from the portrait by `Art/Source/VanGogh/make_canvas_stages.py`
  (Pillow).
- **Lines:** `Art/Source/VanGogh/mirror_lines.txt`, made into WAVs by `make_mirror_lines.sh` (Piper, voice `en_US-norman-medium`, set in `Art/Source/Voice/piper_lines.sh`)
  in `Assets/VanGogh/Audio/`. Rowling may rewrite the lines (#75).
- After changing the images or the lines, run **Tools > InnerFrame > Van Gogh > Rebuild assets**.

### Test in the Editor (no headset)

1. Open `Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity` and press Play. The desktop walker is active
   (WASD, mouse look; click in the view first).
2. Listen for the greeting, then the explanation and the request.
3. Walk to the mirror on the back wall, in the left corner by the towel. Point the centre dot at it and hold the left
   mouse button (the desktop grabber). He says "Yes, that one…". The Console shows `[VanGogh] mirror grab on` once,
   right after Play starts.
4. Carry it toward him; the dashed outline shows beside the canvas. Within 1.0 m of his head it turns solid, the
   sketch appears and he speaks.
5. Release the button there: the mirror moves to the outline's spot and stays. At about 12 s the colour appears; at
   24 s the portrait is finished and he thanks you.
6. Take it again and walk it away: he asks for it back after 1.5 s. Release it far from him: it's back on the wall.

On the desktop the mirror counts as held once it has left the wall, not at the click.

### Test on the Quest

1. Build as usual.
2. In the room, turn left to find Vincent. The mirror's glass is 1.8–2.3 m up in the room, out of a hand's reach at
   this rig scale, so take it with the ray.
3. Bring it near his face; let go there once.
4. Check:
   - each stage and line;
   - that the outlines read against the blue walls, and don't shimmer;
   - that letting go near him parks it beside the canvas (it shouldn't drift or jitter), and far away hangs it back;
   - that B works while he paints;
   - that his words show in full, and whether the dialogue box's size and sharpness feel right (text, border, plate);
   - that the box and hint follow your head calmly (no swimming while you look around a little), and how it feels when
     the easel or the mirror is nearer than the box but drawn behind it.

## Vincent v2 in the Painted Bedroom (2026-11-17)

`VG_Vincent` is now Vincent v2, the rigged model The Starry Night uses (`Assets/StarryNight/Models/Vincent/VincentV2.fbx`,
its material and clips; imported by the Starry Night's setup), built by Tools > InnerFrame > Van Gogh > Rebuild assets
(`Editor/VanGoghAssetSetup.cs`). The static v1 (`Art/Vincent/VG_Vincent.fbx`, `M_Vincent.mat`) is no longer in him; its
files are still in the repo, unused.
- **At the easel:** his spot and facing are v1's (`VanGoghSpawner`). Since 2026-11-17 he is 1.70 m in the room (v1
  was 1.40 m), the model's own size. His easel, canvas, palette and brush are in his prefab and grow with him (21 %),
  as v1's were scaled with v1; the parked mirror stays 10 cm off the wall, his canvas too. The mirror's Target follows
  his head bone (1.45 m), 0.97 m from it, inside the 1 m zone: `MirrorScene.BesideCanvas` went from 0.25 to 0.19 for
  that (at 0.25 the Target was 1.03 m out, so the mirror parked there would have stopped him painting).
- **His clips:** `Art/Vincent/VG_VincentV2.controller`, made by the asset tool: Standing (BreathingIdle) all the time,
  while he paints and while he talks (there is no standing Talking clip), and Walking for the way out. His voice is in
  his head bone.
- **His hands** (`Scripts/VG_Hands.cs`, Animator IK): the palette is in his left hand, held from below as v1 held it.
  It is attached to his left hand bone, and IK puts his wrist in front of him at his waist and turns his hand palm up,
  fingers forward under the board, his fingertips at the thumb hole. None of his hand, finger or forearm vertices is
  inside the palette in any frame of his idle loop (the v2 check counts them, with a control). The brush (`VG_Brush_01`)
  is in his right hand. On the way out he tips the palette up against his chest and takes its other edge with his right
  hand, his elbows in (the room is too tight for it held out flat), and reaches for the door with his right hand; the brush's tip goes a
  few centimetres into the door's edge for that second.
- **His look:** v2 is as bright as v1 at the easel (his face's mean brightness 97 % of v1's in edit mode, 105 % in Play
  with his real skin; the same spread, no dark patches). The dark, blotchy v2 in the first pictures was the pictures'
  fault: the check drew a copy of his skin moved to world space with its normals left in the skin's space, and the PC
  renderer's SSAO (on in the Editor, not on the Quest's Mobile renderer) darkened it. The room has no light probes and
  flat white ambient light (its two lights are at intensity 0), so his material and texture weren't the cause; neither
  changed, and The Starry Night's Vincent is untouched.
- **The mirror scene's rules are unchanged** (`MirrorScene`, `MirrorLink`): the 1.0 m zone round his head, the Target
  pose beside the canvas, hanging back on the wall. Only his head point changed: it is his head bone as he spawns
  (8 cm lower than v1's, which was his eyes), so the Target is 8 cm lower too.
- Check: Tools > InnerFrame > Van Gogh > Run v2 check (`Editor/VG_BedroomV2Check.cs`): v2's rig and controller, the
  framing, Head = his head bone, every mirror rule, the palette in his left hand held from below (palm up, board flat,
  at his waist; nothing of his hand inside it all through his idle loop), the brush in his right hand, no rig or Avatar
  warning.

## The way on from the Painted Bedroom (2026-11-17)

`Assets/VanGogh/BedroomExit/` (added to Vincent by `VanGoghSpawner`), on the same beats as The Starry Night's exit
(`Assets/VanGogh/Lead/VG_ExitFlow.cs`). On each headset by itself:
1. Once the portrait is done and his closing line ("There, it's done…") has ended, and 3 s of quiet after it, he asks:
   "There's something outside I want to show you. Will you come with me?" (`follow` in
   `Art/Source/VanGogh/mirror_lines.txt`, Piper, the same voice as his other lines).
2. When his line ends, the card: **Follow Vincent** or **Back to the Gallery**, pointed at with the controller ray and
   picked with the trigger, as on the player menu. It follows your view and stays until you answer.
3. **Back to the Gallery:** the player menu's own way back.
4. **Follow Vincent:** he tips his palette up and walks round the front chair (`Chair_Front`; the gap between it and
   the wall is narrower than he is) and through the 0.43 m between its back corner and the washstand's front legs to
   `Door_Left`, the light-blue door by the window. At 1.70 m his left thigh and toe pass within 1 cm of the chair there,
   not into it; his walk starts easing into standing 0.2 m before his place, so his last stride doesn't reach the shut
   door. He turns to its handle and reaches
   it with his right hand, and the door swings 90° into its niche, showing stairs: a landing and four steps going up
   into the wall, lit gold from the top. He walks in and up the first two steps as the gold rises from the stairwell;
   under it Café Terrace at Night starts (the next ready painting in the story order), as it does from the Gallery.
- **The door** is the room's own `Door_Left`, opened through its own networked path, the way a grab or the room's reset
  moves it: `NetworkGrabbable.WhenOwned` asks for its state authority, and once your headset has it the door turns on
  its hinge, so its pose goes to everyone (the other players see it open; it stays open). Its components and settings
  are not touched, and there is never a second door. The door stays open for everyone: another player's Vincent finds
  it open, so the shut door's pose is kept in its frames' space (`Door_Frames`) from when the room loads, and an open
  door isn't reached for or swung again. Open, the door lies flat against the niche's side by the window; its last
  8.6 cm go into the niche's back (`Door_Frames_12`), which the other players still see (hidden behind it, see
  `bedroom-7-other-players-view.png` in the PR's pictures). On your headset only, the niche's back (`Door_Frames_12`) is
  hidden so the stairs can go deeper than the niche; its collider stays. `Door_Right` is untouched. Nothing in the
  room's files changed. His way and the stairs are in the door's own space, so they follow the room's runtime scale
  (0.889, `PaintedBedroomPhysicsApplier`).
- **The card's controller rays** need an EventSystem with XRI's UI input module; the room has none until the player
  menu first opens, so the card makes one (as `PlayerMenu` does) when it shows.
- **The stairs** (`BedroomExit/Resources/VG_Stairs.prefab`, made by Tools > InnerFrame > Van Gogh > Build the Bedroom's
  stairs): one mesh, 44 triangles, 2 materials (2 draw calls), no collider: nobody can walk or teleport onto them.
  Unlit (`Art/VG_StairsUnlit.shader`: the texture times each face's shade, plus a gold vertex colour brightest at the
  top) on Cici's textures `VG_Stairs_Wood.png` and `VG_Stairs_Wall.png` (imported at 1024).
- Checks (the room scaled as in Play): Tools > InnerFrame > Van Gogh > Run the Bedroom exit check (with pictures) (edit
  mode: the question only after the portrait and his lines, the card after the question; all the way, his skin (every
  third vertex) in none of the furniture or his easel (a ray test on their meshes, with a control on their own
  surfaces) and within 1 cm of no wall or door, and the vertices that pass within 1 cm of the furniture listed; his last
  stride not past the shut door's face; the palette within 1 cm of nothing; his head at least 5 cm under the doorway and
  the stairwell; the reach; the real door turning on its hinge, one door in the
  doorway at every moment, its swing never into the room and clear of the easel, him and the chair; `Door_Right`
  untouched; the stairs seen from six places across the room only through the doorway, never through the window or
  a wall, with a positive control; the gold; Back) and `VG_BedroomExitPlayCheck.RunBatch` (Play mode: both answers
  through SceneFader; the door under a Fusion runner in Single mode, its networked pose the open door's; the gold full
  on the Café's first frame and clearing only after it is up; the Café starting exactly as from the Gallery).
- Known limits: he climbs a smooth ramp through the treads' middles, so his feet float or sink a few centimetres on the
  steps for the second before the gold covers him; the door stays open after he has gone (for everyone in the room); if
  the door's state authority never comes (a network fault), he waits at the handle and the gold comes after 30 s; the
  room's owners may want to look at the door and the niche. With Café Terrace off in `VG_StoryOrder`, The Bedroom leads
  to The Starry Night instead.

Headset checklist (not yet tried on a Quest): is the card readable and easy to point at from where you hold the mirror?
Does his walk round the chair to the door, the palette tipped up, the reach and the swing read? From where you stand,
do you see the stairs through the doorway, and does the gold rising from them read as "he goes up into the light"? With
two headsets: does the other player's door open smoothly and stay open?

## Vincent feels alive (Painted Bedroom)

Vincent's model has no blendshapes, so `Scripts/VincentAlive.cs` (on the `VG_Vincent` root) moves him as a whole, on
top of his BreathingIdle clip (v2, 2026-11-17). There is no mouth or blink animation. While he walks out (above) it is
switched off.
- **Breathing:** his height swells by 0.4 % every 4 s.
- **Turning:** when your head is within 3.5 m he turns toward you, at most 12° either way, easing there in about
  0.6 s. While he paints he turns toward the mirror instead (`MirrorScene` tells him), and otherwise he turns back to
  the front. A slow ±1° sway keeps him from standing dead still.
- **Leaning in:** while he speaks, the loudness of his voice (256 samples, RMS) leans him forward by up to 2°, about
  his feet.
- **The Figure:** the asset tool puts all of him (the palette, brush and voice are in his bones) under a `Figure` whose
  pivot is between his feet; the easel stays put. Only the Figure moves. `MirrorScene` finds the brush inside it; its
  mirror target and his head point are taken once, as he spawns, so they don't move.
- Every amplitude is a named constant at the top of `VincentAlive`. Untick **Alive** on the component to stop all of
  it.
