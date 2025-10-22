# Unified Backlog (Kanban-style)

Legend: [A] = Agent can implement via PR, [H] = Human-in-loop (env/UX/product decision or external systems)

Completed (most recent first)
- [x] [A] Unit tests for Orchestrator parsing (list/read/mishears)
- [x] [A] Wire MCP Orchestrator agent and make it the default `IAgentService` (fallback)
- [x] [A] Conversational scaffolding: Continuous UI toggle + VAD callbacks wired (client-side); phase chips; feature flag
- [x] [A] Integration tests for `/api/stt` and `/api/tts` (behind feature flag)
- [x] [A] Add targeted retries (connect/read only) and a circuit breaker for STT/TTS; no retry on4xx or large bodies
- [x] [A] Use `HttpCompletionOption.ResponseHeadersRead` for TTS and cap response size
- [x] [A] Make `webfrontend` `.WaitFor(stt)` and `.WaitFor(tts)` in AppHost (after health exists)
- [x] [A] Wire `WithHttpHealthCheck("/health")` for `stt-whisper` and `tts-http` in AppHost
- [x] [H] Add `/health` endpoints to Whisper and TTS containers (return200 when ready)
- [x] [A] Add diagnostics page showing current `Speech:Endpoint`/`Tts:Endpoint` and last call status
- [x] [A] Emit OpenTelemetry dependency traces and basic metrics for STT/TTS
- [x] [A] Add structured logs for STT/TTS calls (duration, status, sizes)
- [x] [A] Add request size limits and cancellation timeouts on `api/stt` and `api/tts`
- [x] [A] Disable output cache for `api/stt` and `api/tts` routes

Planned (priority order – top = next)
- [ ] [A] Conversational mode: tests for OnPhrase pipeline (bUnit) and integration happy path
- [ ] [A] Feature flag `Features:ContinuousDictation` docs and defaults
- [ ] [A] Orchestrator LLM (Generalist with Tool Use) [REQ-002]
- [ ] [A] RAG over Repo (Docs, Diagrams, Code) [REQ-003]
- [ ] [A] MCP Tooling for Code Changes (Plan, Edit, Diff) [REQ-004]
- [ ] [A] Ensure `Expect:100-Continue` on STT uploads (verify header observed)
- [ ] [A] Add locale/voice pickers in `AgentsChat.razor`
- [ ] [A] Improve `wwwroot/js/audio.js` to guarantee WAV16?bit PCM @16kHz and better error handling
- [ ] [A] Add locale auto-detect fallback if `language` not provided (server-side)
- [ ] [A] Implement streaming transcription endpoint (server push partials) [future]
- [ ] [A] Add endpoint to stream TTS audio for immediate playback [future]
- [ ] [H] Define requirement IDs policy (REQ-### format, uniqueness, authoring rules)
- [ ] [H] Document endpoint scheme/port troubleshooting and dev cert trust steps
- [ ] [H] Document speech feature flags and typical dev `appsettings`
- [ ] [H] Update architecture diagrams to include health checks and named HttpClients
- [ ] [A] Add Foundry Agent service and demo flow (STT -> Agent -> TTS)

Notes
- Keep PRs small and check off each item as it merges.
- Items marked [H] may block dependent [A] work; sequence accordingly.
- If any [H] turns into [A] (requirements clarified), re-tag and proceed.
