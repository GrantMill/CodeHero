# CodeHero

Build software from your repo as the source of truth with human oversight and assistive agents. Everything is files (docs, diagrams, code, plans), and changes flow through small, reviewable PRs.

Key features
- Blazor Server UI (.NET 10) to edit requirements, architecture, and plans; drive agents.
- Audio capture in-browser (MediaRecorder + PCM fallback) with streamed JS interop to avoid large SignalR payloads.
- Speech-to-Text via Azure AI Foundry (gpt-4o-transcribe-diarize). Optional Text-to-Speech via Azure AI Speech.
- Agents: process-based MCP server, Foundry agent stub; Human/Agent plan files for incremental delivery.
- Architecture as Mermaid in `docs/architecture`. IaC with Bicep and GitHub Actions.

Repo layout
- `CodeHero.Web` — Blazor app; endpoints `POST /api/stt`, `POST /api/tts`, `POST /api/agent/chat`
- `CodeHero.McpServer` — JSON‑RPC server used by the UI via `McpClient`
- `CodeHero.Tests` — tests
- `docs/architecture` — Mermaid: overview, sequences, state, use-cases
- `docs/requirements` — REQ‑### markdown
- `plan/HUMAN.md`, `plan/AGENT.md` — work plans
- `iac/bicep` — `main.bicep` (storage+logs), `parameters.dev.json`
- `.github/workflows` — `build.yml`, `iac.yml`

Run locally
1) Prereqs: .NET 10 SDK; modern Edge/Chrome/Firefox
2) Build/run: `dotnet build` then `dotnet run --project CodeHero.AppHost`
3) Browse: open the shown https://localhost:xxxx
4) Try: Plan/Requirements/Architecture pages; Agents and Scribe Chat

Configure STT/TTS (choose one)
- Whisper local (recommended for dev)
  - Run a local container exposing POST /stt (see below) and set `CodeHero.Web/appsettings.Development.json`:
    - `"Speech": { "Endpoint": "http://localhost:18000" }`
  - (Optional) Add a local HTTP TTS container exposing POST /tts and set:
    - `"Tts": { "Endpoint": "http://localhost:18010" }`
  - The app will use `WhisperAndHttpTtsSpeechService` when both endpoints are present, else `WhisperClientSpeechService` (silent TTS fallback).
- Foundry (cloud)
- In Azure AI Foundry create or locate a deployment `gpt-4o-transcribe-diarize`
- Set secrets for `CodeHero.Web`:
  - `dotnet user-secrets set "AzureAI:Foundry:Endpoint" "https://<workspace>.<region>.models.ai.azure.com" --project CodeHero.Web`
  - `dotnet user-secrets set "AzureAI:Foundry:Key" "<key>" --project CodeHero.Web`
  - `dotnet user-secrets set "AzureAI:Foundry:TranscribeDeployment" "gpt-4o-transcribe-diarize" --project CodeHero.Web`
  - (optional) `dotnet user-secrets set "AzureAI:Foundry:ApiVersion" "2024-08-01-preview" --project CodeHero.Web`
- Ensure `Features:EnableSpeechApi` = true in `appsettings.Development.json`

Optional TTS (Azure Speech)
- `dotnet user-secrets set "AzureAI:Speech:Key" "<key>" --project CodeHero.Web`
- `dotnet user-secrets set "AzureAI:Speech:Region" "<region>" --project CodeHero.Web`
- With both Foundry and Speech, the app uses Foundry for STT and Speech for TTS; otherwise TTS returns a short silent WAV.

Audio demo
- Scribe Chat → Start (allow mic) → speak → Stop → Transcribe. Speak last plays TTS when configured.

Diagrams (Mermaid)
- `docs/architecture/overview.mmd` — system overview
- `docs/architecture/sequence-stt.mmd` — recording → STT
- `docs/architecture/sequence-tts.mmd` — TTS
- `docs/architecture/sequence-agent.mmd` — agent chat
- `docs/architecture/state-diagram.mmd` — UI state
- `docs/architecture/use-cases.mmd` — scenarios

IaC
- Bicep in `iac/bicep/main.bicep`; Speech disabled in `parameters.dev.json` (use existing Foundry).
- GitHub Actions `.github/workflows/iac.yml` validates on PRs; what‑if/deploy via workflow_dispatch with OIDC.

Troubleshooting
- Mic ok but empty transcript → check Foundry endpoint/key and deployment name; `Features:EnableSpeechApi` must be true.
- Reconnects during audio → hard refresh; the app streams WAV blobs to avoid large SignalR messages.
- No MediaRecorder → PCM fallback is used; use a modern Chromium/Firefox build.

License: MIT

Appendix: run Whisper STT locally (Docker)
1) Create a folder (e.g., `C:\codehero\stt-whisper`) with `app.py`, `Dockerfile`, and `docker-compose.yml`:

`app.py`
```
from faster_whisper import WhisperModel
from fastapi import FastAPI, UploadFile, File
from pydantic import BaseModel
import os, tempfile

model_size   = os.getenv("WHISPER_MODEL", "small")
compute_type = os.getenv("COMPUTE_TYPE", "int8")
download_root= os.getenv("MODEL_ROOT", "/models")

model = WhisperModel(model_size, device="cpu", compute_type=compute_type, download_root=download_root)
app = FastAPI()

class STTOut(BaseModel):
    text: str
    segments: list[dict]

@app.post("/stt", response_model=STTOut)
async def stt(file: UploadFile = File(...), language: str | None = None):
    with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as tmp:
        tmp.write(await file.read()); tmp.flush()
        segments, _ = model.transcribe(tmp.name, language=language, vad_filter=True)
    segs, text = [], []
    for s in segments:
        segs.append({"start": s.start, "end": s.end, "text": s.text})
        text.append(s.text)
    return {"text": " ".join(text).strip(), "segments": segs}
```

`Dockerfile`
```
FROM python:3.11-slim
RUN pip install --no-cache-dir fastapi uvicorn[standard] faster-whisper
ENV WHISPER_MODEL=small COMPUTE_TYPE=int8 MODEL_ROOT=/models
WORKDIR /app
COPY app.py /app/app.py
VOLUME ["/models"]
EXPOSE 8000
CMD ["uvicorn","app:app","--host","0.0.0.0","--port","8000"]
```

`docker-compose.yml`
```
services:
  stt-whisper:
    build: .
    image: codehero/stt-whisper:cpu
    restart: unless-stopped
    environment:
      WHISPER_MODEL: "small"
      COMPUTE_TYPE: "int8"
      MODEL_ROOT: "/models"
    volumes:
      - C:\\codehero\\models\\whisper:/models
    ports:
      - "127.0.0.1:18000:8000"
```

Start: `docker compose up -d --build`

Set `"Speech": { "Endpoint": "http://localhost:18000" }` in `appsettings.Development.json` and restart the app.

Appendix: add a simple HTTP TTS container (optional)
Use any TTS you prefer that accepts text/plain and returns audio/wav on POST /tts. Example minimal Python server (edge cases omitted):
```
from fastapi import FastAPI, Request
from TTS.utils.synthesizer import Synthesizer
import io
from starlette.responses import Response

app = FastAPI()
syn = Synthesizer(tts_checkpoint=None, tts_config=None)  # replace with a real model/config

@app.post('/tts')
async def tts(req: Request):
    text = await req.body()
    wav = syn.tts(text.decode('utf-8'))
    buf = io.BytesIO()
    # write WAV to buf ...
    return Response(buf.getvalue(), media_type='audio/wav')
```
Expose it on http://localhost:18010 and set `"Tts": { "Endpoint": "http://localhost:18010" }`.
