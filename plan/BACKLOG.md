# Unified Backlog (Kanban-style)

Legend: [A] = Agent can implement via PR, [H] = Human-in-loop (env/UX/product decision or external systems)

Priority order (top = next):

- [x] [A] Disable output cache for `api/stt` and `api/tts` routes
- [ ] [A] Add request size limits and cancellation timeouts on `api/stt` and `api/tts`
- [ ] [A] Add structured logs for STT/TTS calls (duration, status, sizes)
- [ ] [A] Emit OpenTelemetry dependency traces and basic metrics for STT/TTS
- [ ] [A] Unit tests for `WhisperAndHttpTtsSpeechService` (success/empty/5xx using fake handlers)
- [ ] [A] Add diagnostics page showing current `Speech:Endpoint`/`Tts:Endpoint` and last call status
- [ ] [H] Add `/health` endpoints to Whisper and TTS containers (return200 when ready)
- [ ] [A] Wire `WithHttpHealthCheck("/health")` for `stt-whisper` and `tts-http` in AppHost
- [ ] [A] Make `webfrontend` `.WaitFor(stt)` and `.WaitFor(tts)` in AppHost (after health exists)
- [ ] [A] Use `HttpCompletionOption.ResponseHeadersRead` for TTS and cap response size
- [ ] [A] Ensure `Expect:100-Continue` on STT uploads (already set for named client; verify)
- [ ] [A] Add targeted retries (connect/read only) and a circuit breaker for STT/TTS; no retry on4xx or large bodies
- [ ] [A] Integration tests for `/api/stt` and `/api/tts` (behind feature flag)
- [ ] [A] Add mic toggle + locale/voice pickers in `AgentsChat.razor`
- [ ] [A] Improve `wwwroot/js/audio.js` to guarantee WAV16-bit PCM @16kHz and better error handling
- [ ] [A] Add locale auto-detect fallback if `language` not provided (server-side)
- [ ] [A] Implement streaming transcription endpoint (server push partials)
- [ ] [A] Add endpoint to stream TTS audio for immediate playback
- [ ] [A] Wire agent turn-taking: STT -> Agent -> TTS, with UI playback
- [ ] [H] Define requirement IDs policy (REQ-### format, uniqueness, authoring rules)
- [ ] [H] Document endpoint scheme/port troubleshooting and dev cert trust steps
- [ ] [H] Document speech feature flags and typical dev `appsettings`
- [ ] [H] Update architecture diagrams to include health checks and named HttpClients
- [ ] [A] Add Foundry Agent service and demo flow (STT -> Agent -> TTS)

Notes
- Keep PRs small and check off each item as it merges.
- Items marked [H] may block dependent [A] work; sequence accordingly.
- If any [H] turns into [A] (requirements clarified), re-tag and proceed.
