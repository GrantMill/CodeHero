# Agent plan (small, testable tasks)

- [x] Propose new requirement template (create docs/requirements/REQ-001.md)
- [x] Implement MCP server (initialize, ping, fs/*) and stdio framing
- [x] Expose agents/list and agents/capabilities; add scribe/createRequirement tool
- [x] Add minimal Agents page and MCP client; support ping/list/capabilities/create
- [ ] Suggest checklist for "New Requirement" flow (fields, acceptance, traceability)
- [ ] Detect missing REQ tags in changed Blazor components (simple analyzer or CI script)
- [ ] Add chat-style Agents UI (conversation with scribe, map intents to tools)
- [ ] Add client/server timeouts + resilient lifecycle management

## Speech/Audio integration
- [x] Implement `ISpeechService` with Azure + Null implementations
- [x] Feature-flag endpoints and CI secret wiring
- [x] Add local Whisper STT container orchestrated by Aspire
- [x] Add HTTP TTS container (optional) orchestrated by Aspire
- [ ] Add UI capture/playback and endpoint tests
- [ ] Define `IAgentService` and stub Azure Foundry Agent integration

## Reliability & health (voice path)
- [ ] Log resolved `Speech:Endpoint` and `Tts:Endpoint` at startup
- [ ] Add `/health` endpoints to `stt-whisper` and `tts-http` containers and wire `WithHttpHealthCheck("/health")`
- [ ] In AppHost, `.WaitFor(stt)` and `.WaitFor(tts)` before starting `webfrontend`
- [ ] Add request size limits and cancellation timeouts on `/api/stt` and `/api/tts`

## HTTP hardening (STT/TTS)
- [ ] Keep named HttpClients (`"stt"`, `"tts"`) and add resilience: retry(connect/read only) + circuit breaker; avoid retries on large bodies
- [ ] Use `HttpCompletionOption.ResponseHeadersRead` for TTS responses and cap body size
- [ ] Ensure `Expect:100-Continue` on STT uploads and sensible timeouts

## Tests
- [ ] Unit-test `WhisperAndHttpTtsSpeechService` with fake handlers: success JSON, empty transcript, and5xx (assert throws)
- [ ] Integration-test `/api/stt` and `/api/tts` (guarded by feature flag) returning text/WAV
- [ ] Aspire test: wait for `stt`/`tts` health before exercising endpoints

## Blazor UX for voice
- [ ] Add mic toggle, locale picker, and voice picker to `AgentsChat.razor`
- [ ] Update `wwwroot/js/audio.js` to guarantee16-bit PCM16kHz WAV and robust error handling
- [ ] Explicitly disable output cache for `/api/stt` and `/api/tts`

## Observability
- [ ] Add structured logs for STT/TTS (duration, status code, payload sizes)
- [ ] Emit OpenTelemetry dependency traces for STT/TTS calls and basic metrics (requests, errors)
- [ ] Add diagnostics page showing current endpoints and last call status

## Next agent tasks (voice chat)
- [ ] Implement streaming transcription endpoint (server push partials)
- [ ] Add endpoint to stream TTS audio for immediate playback
- [ ] Wire agent chat turn-taking with STT -> Agent -> TTS cycle
- [ ] Add locale auto-detect fallback when language is not provided
- [ ] Apply exponential backoff only on connect/read errors (no retry on4xx)

