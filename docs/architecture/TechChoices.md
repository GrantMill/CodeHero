# Tech Choices

- Blazor Server (.NET 10) for rapid iteration with server-side rendering and SignalR transport. Rationale: simplifies auth/state and enables server resource access; voice UI needs minimal client JS.
- Mermaid for architecture docs committed in-repo; diagrams render in-app and in GitHub.
- Aspire AppHost for dev-time orchestration of local services (Whisper STT, HTTP TTS) with typed endpoint injection.
- Whisper (faster-whisper) for local STT in development; Azure AI Foundry (gpt-4o-transcribe-diarize) for cloud STT.
- Azure Speech for high-quality TTS in cloud; simple HTTP TTS container as local/dev placeholder.
- Docker Desktop (Linux containers); bind-mount model cache to host to speed iterations.
- Bicep for IaC; GitHub Actions for CI and optional what-if/deploy using OIDC.

## Current Status
- Local Whisper STT runs under Aspire; TTS optional via HTTP container or Azure Speech.
- STT/TTS endpoints are injected to `CodeHero.Web` via environment variables.
- Agents run via local MCP server; Foundry agent integration stubbed.

## Near-Term Decisions
- Audio format on the wire: keep WAV in `/api/stt` and add server-side transcode if needed; or accept WebM and transcode in container (ffmpeg).
- Streaming: adopt server-sent events or SignalR streaming for partial STT and progressive TTS.
- Agent backend: finalize Azure AI Foundry chat model and tool-calling surface.

## Alternatives Considered
- Blazor WebAssembly: increases client complexity for audio capture and auth.
- Self-hosted STT like Vosk: inferior accuracy to Whisper for current languages.
- Terraform vs Bicep: Bicep aligns better with Azure-first workflows and GitHub Actions.

