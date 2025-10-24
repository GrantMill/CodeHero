# EnableSpeechApi

Purpose
- Exposes HTTP endpoints for speech features in the web app:
 - POST /api/stt accepts audio/wav and returns plain text (Speech-to-Text)
 - POST /api/tts accepts text/plain and returns audio/wav (Text-to-Speech)

Status
- Enabled by default in appsettings.Development.json: Features:EnableSpeechApi = true

How it is wired
- When enabled, the Blazor app registers the STT/TTS endpoints.
- STT provider selection is runtime-configured:
 - Azure AI Foundry: uses a deployment such as gpt-4o-transcribe-diarize.
 - Local Whisper: containers/stt-whisper FastAPI service (recommended for dev).
 - If neither is configured, STT returns empty string and UI shows no transcript.
- TTS provider selection is runtime-configured:
 - Azure Speech (Cognitive Services) when AzureAI:Speech:Key and Region are set.
 - Local HTTP TTS (if you run a simple tts-http service and set Tts:Endpoint).
 - If none is configured, TTS returns a short silent WAV so the UX flow continues.

Configuration (dev)
- CodeHero.Web/appsettings.Development.json
 - Features:EnableSpeechApi = true
 - Whisper local (recommended):
 - Speech:Endpoint = http://localhost:18000
 - Optional Azure Speech (for TTS):
 - AzureAI:Speech:Key and AzureAI:Speech:Region via user-secrets
 - Optional Azure AI Foundry (for STT):
 - AzureAI:Foundry:Endpoint and AzureAI:Foundry:Key and TranscribeDeployment

Behavior notes
- Scribe Chat uses STT/TTs automatically in Continuous mode.
- Responses include Cache-Control: no-store for audio results (see SpeechApiTests).

Related tests
- CodeHero.Tests/SpeechApiTests.cs validates /api/tts no-cache and /api/stt acceptance.

Disable
- Set Features:EnableSpeechApi = false and restart; the endpoints are not exposed.
