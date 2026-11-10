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
`voiceResponse`, `file`), with the key in the `CONVAI-API-KEY` header. The
returned `sessionID` is reused, so he remembers the conversation until the app
restarts.

Everything else is unchanged: the mirror scene's scripted lines, captions, and
shared Vincent (`Answered`). Convai doesn't return what was asked, so the other
players don't see the question.

## His voice on every headset (#157)

Convai's voice for an answer is about 500 KB, too big for the Photon messages
that share his answers. So only his **words** travel, and **each headset speaks
them itself**, in the voice of his scripted lines (Piper,
`en_GB-northern_english_male-medium`). Everyone hears him, not only the player
who asked.

- **Headset Voice** in `ConvaiDirectSettings` (on by default). On: Convai is asked
  for words only (`voiceResponse=False`, a smaller, faster reply), and
  `Scripts/VincentVoice.cs` speaks them, on the asker's headset and on everyone
  else's (`SpeechToProxy.SayRemote`). Off: Convai's own voice, which only the
  asker hears, as before.
- **The engine:** sherpa-onnx 1.13.8 (`Assets/Plugins/SherpaOnnx`: its C# wrapper,
  and native libraries for Android arm64, macOS and Windows x64). The voice, its
  tokens and the English part of espeak-ng's phoneme data are in
  `Assets/StreamingAssets/VincentVoice`. Not 2.x: sherpa-onnx 2.0 removed
  espeak-ng, which Piper voices need.
- **Streaming:** Convai is asked to stream (`stream=True`). Its reply comes as
  lines of `data: {json}`, a sentence or two each; each one is spoken as soon as it
  arrives, while the next is still being written, and goes to the other players the
  same way (`VincentLink`: one message per part, then an end message).
- **A thinking line at once:** the moment the question is sent, he says one of
  `VincentVoice.ThinkingLines` ("Hmm, let me think.", "Ah, a good question."...), on
  every headset, while *Vincent is thinking…* shows above him. They're made when the
  voice loads, so they cost nothing later.
- **Timing** (Editor on a Mac, 3 runs): thinking line at 0 s; his first sentence
  3.0–4.5 s after you let go (5.4–7 s before streaming); each sentence's voice made
  in 0.1–0.4 s, while the one before plays. Almost all the wait is Convai writing.
  The voice loads in the background when a Vincent starts (about 1 s on a Mac).
  Quest timing isn't measured yet; the Console says it:
  `[VanGogh] Convai (streamed): release->first words 2977 ms`.
- **First run on a Quest:** the voice files are copied out of the APK once (about
  63 MB, to the app's own storage), because native code can't read inside the APK.
- **If it fails** (a missing library or file), the Console says
  `[VincentVoice] off: ...`, and he shows his words with no voice, as before.
- **The library settings** (which platform loads which file) are in their `.meta`
  files. After updating the libraries, run **Tools > InnerFrame > Van Gogh > Set Up
  Headset Voice Libraries**.

### Test it

- **Tools > InnerFrame > Van Gogh > Test Headset Voice** (no Convai, no network): plays
  the Gallery and does what happens when another player asks: a thinking line, then
  an answer in two parts. Passes when he says them in order, in his headset voice,
  and is then free again.
- With Convai set up: `CONVAI_TEST_WAV=question.wav Unity -batchmode -projectPath .
  -executeMethod ConvaiDirectTest.RunFromCommandLine` passes when he says the
  thinking line, then every part of Convai's answer in his headset voice.
- Two players (Quest + Editor): ask from one. Both should hear the same thinking
  line at once, then his answer, sentence by sentence, at about the same time.

## Limits

- **His first words still take 3–4.5 s,** because that is how long Convai takes to
  write them (even a typed question takes 3.4–8 s). The thinking line covers it. To
  go faster, the character's settings on convai.com matter most: its language model,
  shorter answers, and features it doesn't need (narrative design, knowledge bank,
  long-term memory).
- **The proxy isn't streamed:** with Use Convai off, he answers as before.
- **The app is bigger:** about 63 MB for the voice and 27 MB for the Android libraries.
- **The API key ships in the APK.** Fine for team testing. For anything shared more
  widely, move the call to a server (the proxy, hosted online) so the key stays there.
- **The scene sentence isn't sent.** The mirror scene's `SceneContext` isn't passed
  to Convai. The proxy ignores it too, today.
