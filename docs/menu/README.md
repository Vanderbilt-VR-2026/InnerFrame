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
| Talk to the AI | **Talk**: speak, then press again to send. The reply appears in the panel as it's written |

The panel opens 0.55 m in front of you, inside the controllers' 2 m ray.

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
| `SpeechToProxy` | Records the microphone and sends it to the AI proxy; shows the streamed reply |
| `AIProxySettings` | Where the proxy is (see below) |

## The AI proxy

The app never holds an AI API key. Talk sends the recording to our proxy
server, which does speech-to-text, asks the AI and streams the reply back.

Contract the proxy must implement:

- `POST {serverUrl}/npc/{SceneName}/listen`, multipart form, field `audio`:
  16 kHz mono 16-bit WAV.
- Optional header `X-Access-Code` when `accessCode` is set.
- Response: the reply as plain text, streamed (chunked) as it's generated.

Point the app at your proxy: **Assets > Create > InnerFrame > AI Proxy
Settings**, save it as `AIProxySettings` inside a `Resources` folder (e.g.
`Assets/Script/Menu/Resources/`), and set **Server Url**. On a Quest,
`localhost` is the headset, so use the computer's IP, e.g.
`http://192.168.1.50:8000`. Without the asset the app uses
`http://localhost:8000` (works only in the Editor, with the proxy on the same
computer).

Quest builds also need **Player Settings > Android > Internet Access: Require**,
and **Allow downloads over HTTP** for a plain `http://` proxy.

## Hardware tests (please run on a headset)

1. Press ≡: the panel appears in front of you, level and readable.
2. Grab the title bar and walk: it follows your hand, faces you, stays put when you let go.
3. Teleport away and back: the panel hasn't moved.
4. X closes it; ≡ opens it again in front of you.
5. From Gallery go to another scene, open the menu: **Back to Gallery** returns you.
6. Talk: the first press asks for microphone permission; after allowing, record a sentence and send. With no proxy running you see "Couldn't reach the AI server".
