# Continuous Dictation (Conversational Mode)

Default UX for Scribe Chat is continuous-only: the app listens for speech, segments on silence (VAD), transcribes the audio, sends the text to the agent, and plays back the reply via TTS.

Configuration
- `Features:ContinuousDictation` (bool)
 - Development default: true (enabled)
 - Production default: false (opt-in)
- `Features:EnableSpeechApi` (bool) enables `/api/stt` and `/api/tts` endpoints.
- Optional endpoints for local dev:
 - `Speech:Endpoint` (e.g., `http://localhost:18000`) — Whisper container
 - `Tts:Endpoint` (e.g., `http://localhost:18010`) — HTTP TTS container

How it works
- Browser captures audio locally via MediaRecorder with a PCM fallback.
- VAD in `wwwroot/js/audio.js` segments on silence and resamples to16 kHz WAV.
- Each phrase is posted to `/api/stt`, then `/api/agent/chat`, then `/api/tts`.
- The UI shows phase badges: Listening ? Thinking ? (auto) playback.

Tuning
- Sensitivity (threshold): lower is more sensitive to quiet speech; start around `0.003`–`0.010`.
- Silence window (ms): how long of silence ends a phrase; start around `700–1000ms`.

Troubleshooting
- Empty transcripts
 - Lower sensitivity; increase silence window; ensure mic permission is granted.
 - Verify `/api/stt` responds with non-empty text.
- WebSocket disconnects (`/_blazor`)
 - Refresh; continuous mode streams small blobs, but SignalR size/timeouts can be tuned in Program.
- No MediaRecorder
 - The PCM fallback path captures audio; try a modern Chromium/Firefox build.

Security & privacy
- Audio is kept in the browser until you send for STT/TTS.
- Only the captured WAV for each phrase is posted to the server.
