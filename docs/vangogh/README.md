# Talking to Vincent (#73)

In the Gallery, Van Gogh's 1887 self-portrait hangs beside *The Bedroom*. Hold the right
controller's **B** button, ask him something, and let go: the question goes to a small
proxy on a computer nearby (speech-to-text, a local language model playing Vincent in
October 1888, text-to-speech), and his answer plays from the portrait.

- Code and assets: `Assets/VanGogh/`. Nothing is added to a scene: `VanGoghSpawner`
  places the portrait when the scene named `Gallery` loads (the same pattern as
  `PlayerMenu`).
- In the Painted Bedroom, a 3D Vincent (Tripo model) stands by the left wall with
  an easel, palette and brush, facing where you arrive, and answers the same way.
  His eyes are level with yours, so you look at each other while you talk: the XR rig
  is scaled 0.8 in this room, which makes him 1.40 m tall in the room and about
  1.75 m as you feel it. If the rig scale changes, change `VincentScale` in
  `VanGoghSpawner` to match. He was made with Tripo on a paid plan, whose terms give
  paid users the rights to their models (see `docs/ASSETS.md`).
- Why B and not A: A is already bound to XRI's Jump on the right hand.
- **Captions** (storybook style, from Cici's concept): `Scripts/VanGoghCaption.cs`, made by `SpeechToProxy`.
  - His words, scripted lines and AI answers alike, appear on a parchment panel in a rustic wooden frame. They fade
    out a second after his voice stops.
  - Hints appear on a small dark pill: *Hold B to ask Vincent* (*T* in the Editor) with the key in a circle,
    *Listening…*, *Vincent is thinking…*, or *Vincent can't hear you right now* for 3 s. The pill hides while he
    speaks.
  - Over Vincent the pill sits 8 cm above his head and his words above it. Under the portrait his words hang below
    the frame, with the pill below them. Both turn to face your head and fade in and out over 0.2 s.
  - Sizes are world metres, named at the top of `VanGoghCaption`: text up to 0.80 m wide with 2.8 cm capitals,
    shrinking to 2.2 cm before it takes a third line; a 6 cm pill. They are guesses until tried on a Quest.
  - The text is uGUI Text in Libre Baskerville (`Art/Fonts`, OFL), laid out at a large font size and scaled down so it
    stays sharp. TextMesh Pro's Essential Resources aren't in the project, and importing them would be a shared change.
  - The sprites are drawn by `Art/Source/VanGogh/make_caption_sprites.py`. The asset tool sets their 9-slice borders
    and builds `Resources/VG_Caption`, which holds the sprites and the font.
- Each question logs one line starting with `[VanGogh]` (times in ms, plus the proxy's
  own timings).
- After changing the image or its material, run **Tools > InnerFrame > Van Gogh > Rebuild
  assets**.

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

## Hold the mirror (Painted Bedroom)

Vincent is painting himself and needs a mirror (concept sheet, frame 07).
1. About 2 s after you arrive he greets you. Ten seconds later (or as soon as he has answered your first question) he
   explains, and asks you to take the mirror off the back wall, by the towel.
2. Pick it up with the ray (point at it from up to 2 m away; on the Quest the glass is 1.8–2.3 m up, out of a hand's
   reach). You can still walk around.
3. Hold it beside his canvas, to the left of the canvas as you face it, level with his eyes, with the glass facing him.
   He paints. The canvas goes from blank to a charcoal sketch, to colour at halfway, to the finished 1887
   self-portrait, and he comments at each stage. There are no bars or UI: the canvas is the progress.
4. Move it away and he stops; nothing is lost. After 1.5 s he asks for it back, at most once every 10 s.
5. Let go and the mirror hangs itself back on the wall. When the portrait is finished he thanks you.

Talking with B works the whole time. His scripted lines never cut into a question or an answer: the newest one waits
until his voice is free. B stops a scripted line, as it stops any of his answers.

- **Code:** `Assets/VanGogh/Scripts/MirrorScene.cs`, on the `VG_Vincent` prefab (the asset tool adds it).
  - All the decisions are in `MirrorScene.Decide(...)`, which takes plain inputs (time step, held, the mirror's centre
    and face, voice busy, has answered), so it can be tested without XR.
  - In Play mode the Scene view shows the target as a yellow sphere, with a line to his eyes.
- **Constants** (top of `MirrorScene`):

  | Constant | Value |
  |---|---|
  | `EyeHeight` | 1.55 on the model, which is 1.28 m in the room |
  | `BesideCanvas` | 0.25 m: the target is half the canvas's width plus this, sideways from its centre |
  | `TargetRadius` | 0.45 m |
  | `FacingAngle` | 50° |
  | `PaintSeconds` | 24 s of holding it right, in total |
  | When he speaks | greet at 2 s; explain 10 s after the greeting ends; ask again 20 s after the ask; the "back" line after 1.5 s out of place, at most every 10 s |
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
- **Not networked (#77):** the mirror and the canvas are local to each headset.
- **What the model can't do:** Vincent is imported as a still mesh (no rig, no animation), so he doesn't move his arm
  or turn his head; only the brush in his hand makes a small loop while he paints. He doesn't look at you or at the
  mirror.
- **Canvas images:** `Assets/VanGogh/Art/Canvas/`, made from the portrait by `Art/Source/VanGogh/make_canvas_stages.py`
  (Pillow).
- **Lines:** `Art/Source/VanGogh/mirror_lines.txt`, made into WAVs by `make_mirror_lines.sh` (macOS `say`, voice Daniel)
  in `Assets/VanGogh/Audio/`. Rowling may rewrite the lines (#75).
- After changing the images or the lines, run **Tools > InnerFrame > Van Gogh > Rebuild assets**.

### Test in the Editor (no headset)

1. Open `Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity` and press Play. The desktop walker is active
   (WASD, mouse look; click in the view first).
2. Listen for the greeting, then the explanation and the request.
3. Walk to the mirror on the back wall, in the left corner by the towel. Point the centre dot at it and hold the left
   mouse button (the desktop grabber). He says "Yes, that one…". The Console shows `[VanGogh] mirror grab on` once,
   right after Play starts.
4. Carry it to the left of his canvas, near the front wall. Scroll to bring it nearer or farther, and use Z/X to turn
   it until the glass faces him. The sketch appears and he speaks.
5. Hold it there. At about 12 s the colour appears; at 24 s the portrait is finished and he thanks you.
6. Walk it away for a couple of seconds: he asks for it back. Release the button: the mirror is back on the wall.

On the desktop the mirror counts as held once it has left the wall, not at the click.

### Test on the Quest

1. Build as usual.
2. In the room, turn left to find Vincent. The mirror's glass is 1.8–2.3 m up in the room, out of a hand's reach at
   this rig scale, so take it with the ray.
3. Hold it to the left of his canvas, at his eye level, and turn your wrist until the glass faces him.
4. Check:
   - each stage and line;
   - that letting go hangs it back;
   - that B works while he paints;
   - that his words show in full, and whether the caption's size and sharpness feel right.

## Vincent feels alive (Painted Bedroom)

Vincent's model has no rig and no blendshapes, so `Scripts/VincentAlive.cs` (on the `VG_Vincent` root) moves him as a
whole. There is no mouth or blink animation.
- **Breathing:** his height swells by 0.4 % every 4 s.
- **Turning:** when your head is within 3.5 m he turns toward you, at most 12° either way, easing there in about
  0.6 s. While he paints he turns toward the mirror instead (`MirrorScene` tells him), and otherwise he turns back to
  the front. A slow ±1° sway keeps him from standing dead still.
- **Leaning in:** while he speaks, the loudness of his voice (256 samples, RMS) leans him forward by up to 2°, about
  his feet.
- **The Figure:** the asset tool groups his mesh, palette, brush and voice under a `Figure` whose pivot is between his
  feet; the easel stays put. Only the Figure moves, so the palette and brush stay in his hands. `MirrorScene` finds the
  brush inside the Figure, and its mirror target and his head point are still computed from the root, so they don't
  move.
- Every amplitude is a named constant at the top of `VincentAlive`. Untick **Alive** on the component to stop all of
  it.
