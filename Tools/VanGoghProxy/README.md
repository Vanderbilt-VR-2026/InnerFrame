# Van Gogh voice proxy

A small local server that lets a visitor talk to Vincent. It takes a recording,
turns it into text (faster-whisper), asks a local model to answer as Vincent
(Ollama), and turns the answer into speech (macOS `say`). Everything runs on
the computer next to the headset: no cloud service and no API key.

`persona_v0.txt` is still a placeholder until Rowling's persona (#75).

## What Vincent knows

The persona is set in Arles in October 1888, just after he painted *The Bedroom*.
Its room colours come from Vincent's own description in his letter to Theo of
16 October 1888 (letter 705):
https://vangoghletters.org/vg/letters/let705/letter.html
("The walls are of a pale violet. The floor — is of red tiles. The bedstead and
the chairs are fresh butter yellow…").

The model doesn't always keep to the persona's rule of "nothing after October
1888", so `proxy.py` also checks each reply for words from his later life (ear,
razor, asylum, Saint-Rémy, Auvers, Gachet, 1889, 1890 and a few more, as whole
words). If one appears, Vincent says instead: "I'm not sure I understand you, my
friend. Ask me about this room, or about my painting." The server log names the
word. The list is a first version, kept until the real persona exists.

## Run it (macOS)

```
brew install ollama                 # once
ollama pull qwen2.5:7b              # once, about 4.7 GB
ollama serve                        # leave running in its own terminal

cd Tools/VanGoghProxy
python3 -m venv .venv               # once
.venv/bin/pip install -r requirements.txt
.venv/bin/python proxy.py           # starts uvicorn on 127.0.0.1:8000
```

The first start downloads the Whisper model (about 150 MB) into `models/` in
this folder (not committed). Startup also loads the Ollama model and keeps it in
memory (`keep_alive: -1`) until Ollama stops, and warms up `say` and Whisper so
the first visitor doesn't wait for them.

Settings (environment variables):

| Variable | Default | What it does |
|---|---|---|
| `OLLAMA_MODEL` | `qwen2.5:7b` | The model that answers as Vincent |
| `OLLAMA_URL` | `http://127.0.0.1:11434` | Where Ollama runs |
| `WHISPER_MODEL` | `base.en` | faster-whisper model name |
| `WHISPER_DIR` | `models/` in this folder | Where the Whisper model is downloaded |
| `HF_HOME` | `.hf/` in this folder | Hugging Face's own cache and logs (instead of `~/.cache/huggingface`) |
| `VOICE` | `Daniel` | A `say` voice (`say -v '?'` lists them) |
| `HOST` / `PORT` | `127.0.0.1` / `8000` | Where the proxy listens. A headset on Wi-Fi needs `HOST=0.0.0.0` and the computer's IP |
| `ACCESS_CODE` | empty | If set, every request needs a matching `X-Access-Code` header, or gets a 401 |

## Contract

| Request | Response |
|---|---|
| `POST /npc/{npc}/listen`, multipart field `audio` (WAV) | `200 text/plain; charset=utf-8`: Vincent's reply |
| `POST /npc/{npc}/talk`, multipart field `audio` (WAV) | `200 application/json`: `{"transcript", "reply", "audio_url": "/audio/<id>.wav", "timings_ms": {"stt", "llm", "tts"}}` |
| `GET /audio/{id}.wav` | `200 audio/wav`: 16-bit mono 22050 Hz, plain 44-byte header |

Errors are non-200 with `{"error": "..."}`. Every `{npc}` answers as Vincent for
now; the server logs which one was asked. If nothing was heard, the reply is
"Sorry, I didn't quite catch that. Could you say it again?" without asking the
model. The server keeps the newest 20 reply files in `out/`.

`/listen` is what the menu's Talk button calls (see `docs/menu/README.md` on
`sonya/multiplayer-menu`).

## Try it with curl

```
say -o question.wav --data-format=LEI16@16000 "Why is your room so colourful?"
curl -F audio=@question.wav http://127.0.0.1:8000/npc/Gallery/talk
```
