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
  `VanGoghSpawner` to match. His Tripo licence for a public repo is not verified yet
  (see `docs/ASSETS.md`).
- Why B and not A: A is already bound to XRI's Jump on the right hand.
- The caption under the portrait shows the state: *Hold B to ask Vincent*, *Listening…*,
  *Vincent is thinking…*, nothing while he speaks, or *Vincent can't hear you right now*.
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
3. Walk to the portrait, hold **T**, speak, let go. The caption should go *Listening…*,
   then *Vincent is thinking…*, and then he answers. macOS asks once for microphone
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
