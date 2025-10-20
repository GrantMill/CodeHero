from faster_whisper import WhisperModel
from fastapi import FastAPI, UploadFile, File
from pydantic import BaseModel
import os, tempfile

model_size   = os.getenv("WHISPER_MODEL", "small")  # small|medium|large-v3
compute_type = os.getenv("COMPUTE_TYPE", "int8")    # int8|float16|float32
download_root= os.getenv("MODEL_ROOT", "/models")   # persisted cache

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
