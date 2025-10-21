- [x] Approve folder conventions and VISION.md
- [x] Create solution (CodeHero.sln) and add all projects
- [x] Land MCP server (initialize, ping, fs/*) and agents surface (scribe)
- [x] Add Architecture Mermaid rendering + TechChoices display
- [x] Add Agents page scaffold + MCP client
- [x] Review & merge MCP server + Agents UI PR
- [x] Approve any changes under docs/requirements and docs/architecture
- [x] Enable branch protection + required checks in GitHub (CI build/test on CodeHero.sln)
- [ ] Define requirement IDs policy (REQ-### format, uniqueness, authoring rules)

## Demo: Speech/Audio/Azure Foundry Agent
- [x] Add Azure Speech SDK service with TTS/STT
- [x] Add feature-gated endpoints `/api/tts` and `/api/stt`
- [x] Add null fallback to keep tests green without secrets
- [x] Add configuration keys and CI env wiring
- [x] Add simple UI to record/play/push audio
- [ ] Add Foundry Agent service and demo flow (STT -> Agent -> TTS)

## Local STT with Whisper (Docker)
- [x] Create `stt-whisper` folder with `app.py`, `Dockerfile`, `docker-compose.yml`
- [x] Run container in Aspire (AppHost) with bind mount for models
- [x] Wire `Speech__Endpoint` from Aspire to Web via env
- [x] Verify: Agents Chat ? record ? Stop ? Transcribe shows text via Whisper
- [ ] Add health endpoints to Whisper and TTS containers; wire `WithHttpHealthCheck` in AppHost
- [ ] Make `webfrontend` wait for `stt` and `tts` readiness in AppHost

## HTTP & resilience hardening
- [ ] Ensure named HttpClients ("stt","tts") are used; add100-Continue and HTTP/1.1 defaults
- [ ] Add targeted retries (connect/read only), circuit breaker; no retry on4xx or large bodies
- [ ] Cap request/response sizes and use ResponseHeadersRead for TTS

## Tests
- [ ] Add unit tests for `WhisperAndHttpTtsSpeechService` (success/empty/5xx)
- [ ] Add integration tests for `/api/stt` and `/api/tts` behind feature flag
- [ ] Extend Aspire test to wait for health and then call endpoints

## Blazor UX & diagnostics
- [ ] Add mic toggle + locale/voice pickers to `AgentsChat.razor`
- [ ] Improve `wwwroot/js/audio.js` to ensure WAV16-bit PCM @16kHz and better error handling
- [ ] Add diagnostics UI: endpoint values, last STT/TTS status, and simple logs
- [ ] Disable output cache for `/api/stt` and `/api/tts` routes

## Documentation
- [ ] Document endpoint scheme/port troubleshooting and cert trust steps
- [ ] Document speech feature flags and typical dev appsettings
- [ ] Update architecture diagrams to include health and named HttpClients
