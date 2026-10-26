"""Van Gogh voice proxy: speech-to-text (faster-whisper) -> Vincent (Ollama) -> speech (macOS `say`).

Runs on the computer next to the headset, so the app never holds a model or a key.
See README.md for how to run it and the contract.
"""
import hmac
import io
import json
import logging
import os
import re
import subprocess
import time
import urllib.error
import urllib.request
import uuid
import wave
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, File, Request, UploadFile
from fastapi.exceptions import RequestValidationError
from fastapi.responses import FileResponse, JSONResponse, PlainTextResponse
from starlette.exceptions import HTTPException

HERE = Path(__file__).parent
# huggingface_hub reads HF_HOME once, when faster_whisper imports it, so it must be set before that import.
# Otherwise its cache and logs go to ~/.cache/huggingface instead of this folder.
os.environ.setdefault("HF_HOME", str(HERE / ".hf"))
from faster_whisper import WhisperModel

OLLAMA_URL = os.environ.get("OLLAMA_URL", "http://127.0.0.1:11434")
OLLAMA_MODEL = os.environ.get("OLLAMA_MODEL", "qwen2.5:7b")
WHISPER_MODEL = os.environ.get("WHISPER_MODEL", "base.en")
WHISPER_DIR = os.environ.get("WHISPER_DIR", str(HERE / "models"))
VOICE = os.environ.get("VOICE", "Daniel")
ACCESS_CODE = os.environ.get("ACCESS_CODE", "")

OUT = HERE / "out"
PERSONA = (HERE / "persona_v0.txt").read_text(encoding="utf-8").strip()
DIDNT_CATCH = "Sorry, I didn't quite catch that. Could you say it again?"
NOT_SURE = "I'm not sure I understand you, my friend. Ask me about this room, or about my painting."
KEEP_AUDIO_FILES = 20

# A guard for D16's rule (nothing after October 1888) until Rowling's persona (#75) exists. The list is v0.
AFTER_1888 = re.compile(
    r"\b(ears?|razor|asylum|hospital|saint-r[ée]my|auvers|gachet|revolver|gun|shot|suicide|self-harm|brothel|1889|1890)\b",
    re.IGNORECASE,
)

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s")
log = logging.getLogger("vangogh")
state = {}


def ollama(path, body):
    request = urllib.request.Request(
        OLLAMA_URL + path, data=json.dumps(body).encode(), headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(request, timeout=120) as response:
        return json.load(response)


def transcribe(wav):
    # vad_filter skips silence; without it Whisper tends to "hear" words like "Thank you." in silence.
    segments, _ = state["whisper"].transcribe(io.BytesIO(wav), language="en", vad_filter=True)
    return " ".join(segment.text.strip() for segment in segments).strip()


def reply(text):
    body = {
        "model": OLLAMA_MODEL,
        "messages": [{"role": "system", "content": PERSONA}, {"role": "user", "content": text}],
        "stream": False,
        "keep_alive": -1,
        "options": {"temperature": 0.7, "num_predict": 80},
    }
    if state["can_think"]:
        body["think"] = False  # only for thinking models: Ollama rejects "think" on models without it
    answer = ollama("/api/chat", body)["message"]["content"].strip()
    # The persona asks for 1-3 sentences but the model doesn't always listen. Keeping whole sentences
    # also drops a last sentence that num_predict cut off halfway.
    sentences = re.findall(r"[^.!?]*[.!?]+[\"')\]]*", answer) or [answer]
    answer = " ".join(sentence.strip() for sentence in sentences[:3])
    hit = AFTER_1888.search(answer)
    if hit:
        log.info("guard: %r is after October 1888; replied with the fallback line", hit.group(0))
        return NOT_SURE
    return answer


def speak(text):
    audio_id = uuid.uuid4().hex
    said, final = OUT / f"{audio_id}.say.wav", OUT / f"{audio_id}.wav"
    # The text goes in on stdin, so a reply starting with "-" can't be read as an option.
    subprocess.run(
        ["say", "-v", VOICE, "-o", str(said), "--data-format=LEI16@22050", "-f", "-"],
        input=text.encode(), check=True,
    )
    # `say` adds a JUNK chunk and starts the audio at byte 4096; rewrite it with a plain 44-byte header.
    with wave.open(str(said), "rb") as src, wave.open(str(final), "wb") as dst:
        dst.setparams(src.getparams())
        dst.writeframes(src.readframes(src.getnframes()))
    said.unlink()
    for old in sorted(OUT.glob("*.wav"), key=lambda p: p.stat().st_mtime)[:-KEEP_AUDIO_FILES]:
        old.unlink(missing_ok=True)
    return audio_id


def answer_recording(npc, audio, with_voice):
    wav = audio.file.read()
    t0 = time.perf_counter()
    transcript = transcribe(wav)
    t1 = time.perf_counter()
    text = reply(transcript) if transcript else DIDNT_CATCH  # nothing heard: don't ask the model
    t2 = time.perf_counter()
    audio_id = speak(text) if with_voice else None
    t3 = time.perf_counter()
    ms = {"stt": round((t1 - t0) * 1000), "llm": round((t2 - t1) * 1000), "tts": round((t3 - t2) * 1000)}
    log.info("npc=%s transcript=%r reply=%r stt=%d llm=%d tts=%d", npc, transcript, text, *ms.values())
    return transcript, text, audio_id, ms


@asynccontextmanager
async def lifespan(app):
    OUT.mkdir(exist_ok=True)
    state["whisper"] = WhisperModel(WHISPER_MODEL, device="cpu", compute_type="int8", download_root=WHISPER_DIR)
    state["can_think"] = "thinking" in ollama("/api/show", {"model": OLLAMA_MODEL}).get("capabilities", [])
    reply("Hello.")  # loads the model now (keep_alive -1 keeps it loaded) instead of on the first visitor
    hello = speak("Hello.")  # the first `say` of a session is slow
    transcribe((OUT / f"{hello}.wav").read_bytes())  # and so is Whisper's first transcription
    log.info("ready: whisper=%s ollama=%s voice=%s", WHISPER_MODEL, OLLAMA_MODEL, VOICE)
    yield


app = FastAPI(lifespan=lifespan)


def error(status, message):
    return JSONResponse({"error": message}, status_code=status)


@app.middleware("http")
async def check_access_code(request: Request, call_next):
    sent = request.headers.get("X-Access-Code", "")
    if ACCESS_CODE and not hmac.compare_digest(sent.encode(), ACCESS_CODE.encode()):
        return error(401, "missing or wrong X-Access-Code")
    return await call_next(request)


@app.exception_handler(HTTPException)
async def http_error(request, exc):
    return error(exc.status_code, str(exc.detail))


@app.exception_handler(RequestValidationError)
async def bad_request(request, exc):
    return error(422, "send the recording as a WAV file in the multipart field 'audio'")


@app.exception_handler(urllib.error.URLError)  # also catches HTTPError, e.g. a model that isn't pulled
async def ollama_failed(request, exc):
    return error(502, f"Ollama call to {OLLAMA_URL} failed: {exc}")


@app.exception_handler(Exception)
async def crash(request, exc):
    return error(500, f"{type(exc).__name__}: {exc}")


@app.post("/npc/{npc}/listen")
def listen(npc: str, audio: UploadFile = File(...)):
    _, text, _, _ = answer_recording(npc, audio, with_voice=False)
    return PlainTextResponse(text)


@app.post("/npc/{npc}/talk")
def talk(npc: str, audio: UploadFile = File(...)):
    transcript, text, audio_id, ms = answer_recording(npc, audio, with_voice=True)
    return {"transcript": transcript, "reply": text, "audio_url": f"/audio/{audio_id}.wav", "timings_ms": ms}


@app.get("/audio/{audio_id}.wav")
def get_audio(audio_id: str):
    path = OUT / f"{audio_id}.wav"
    if not re.fullmatch(r"[0-9a-f]{32}", audio_id) or not path.exists():
        return error(404, "no such audio")
    return FileResponse(path, media_type="audio/wav")


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host=os.environ.get("HOST", "127.0.0.1"), port=int(os.environ.get("PORT", "8000")))
