# Vincent through Convai (web API)

Vincent can answer through Convai's cloud character instead of the AI proxy.
The headset talks to Convai directly, so there's no proxy, no Ollama, and no USB
cable while testing. There's no Convai SDK in the project either: only one web
request.

| | AI proxy (default) | Convai web API |
|---|---|---|
| What runs during a test | A Mac with Ollama + `Tools/VanGoghProxy`, and the headset on USB (`adb reverse`) or pointed at the Mac's IP | Nothing. The headset needs internet. |
| His character | `Tools/VanGoghProxy/persona_*.txt` | Vincent's character on convai.com |
| API key | None in the app | **In the app** (git-ignored file, but built into the APK) |

## Set up (each machine)

1. **Tools > InnerFrame > Van Gogh > Set Up Convai Direct** creates
   `Assets/VanGogh/Resources/ConvaiDirectSettings` (Use Convai + Character ID) and
   an empty `Assets/VanGogh/Resources/ConvaiApiKey.txt`.
2. Paste Vincent's **Character ID** (convai.com, only the ID:
   `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`) into `ConvaiDirectSettings`, and press Cmd+S.
3. Paste your **API key** (convai.com, under the key icon or Account Settings) into
   `ConvaiApiKey.txt`, alone on one line, and save. Git ignores this file. Never commit it.
4. Press Play. The Console says `[VanGogh] VanGogh_Portrait: talking through Convai's web API`,
   or why it's using the proxy instead.

To go back to the proxy, untick **Use Convai**.

## How it works

`Scripts/SpeechToProxy.cs` records while B (T in the Editor) is held, as before.
When `ConvaiDirectSettings` is ready, `Ask()` sends the WAV to
`https://api.convai.com/character/getResponse` (`charID`, `sessionID`,
`voiceResponse=True`, `file`), with the key in the `CONVAI-API-KEY` header. It
plays the voice that comes back (base64 WAV) and shows his words. The returned
`sessionID` is reused, so he remembers the conversation until the app restarts.

Everything else is unchanged: the mirror scene's scripted lines, captions, and
shared Vincent (`Answered`). Other players see his words. Convai doesn't return
what was asked, so they don't see the question.

## Limits

- **Answers aren't streamed.** He answers a few seconds after you let go, like the proxy.
- **The API key ships in the APK.** Fine for team testing. For anything shared more
  widely, move the call to a server (the proxy, hosted online) so the key stays there.
- **The scene sentence isn't sent.** The mirror scene's `SceneContext` isn't passed
  to Convai. The proxy ignores it too, today.
