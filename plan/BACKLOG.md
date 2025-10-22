# Unified Backlog (Kanban-style)

Legend: [A] = Agent can implement via PR, [H] = Human-in-loop (env/UX/product decision or external systems)

Completed (most recent first)
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
- [ ] [A] Conversational mode (continuous mic ? transcribe ? orchestrate MCP ? TTS)
 - [x] [A] AgentsChat: add "Continuous" toggle and status chips (Listening / Thinking / Speaking)
 - [x] [A] JS (audio.js): add simple VAD/silence detection, slice phrases, post each phrase to .NET via JSInvokable callback
 - [ ] [A] .NET: finalize `OnPhraseAsync` pipeline (error surfaces, cancellations, rate-limit)
 - [ ] [A] After final phrase, call orchestrator agent (see below) ? then TTS the reply and play
 - [ ] [A] Feature flag `Features:ContinuousDictation` (config + docs)
 - [ ] [A] Tests: bUnit for UI toggles + JS callbacks; integration happy-path (small WAV ? agent reply ? TTS)
- [ ] [A] Orchestration agent for MCP actions (turn user requests into safe tool calls)
 - [x] [A] Define allowlist of MCP tools (fs/list, fs/readText, fs/writeText, scribe/createRequirement)
 - [x] [A] Add `IAgentService` implementation `McpOrchestratorAgentService` (rule-based intents)
 - [ ] [A] Add LLM planning mode when Foundry configured: prompt ? JSON plan ? execute ? summarize
 - [x] [A] Wire `/api/agent/chat` to use orchestrator by default (when Foundry agent not configured)
 - [ ] [A] Tests: unit (intent parsing), integration (end-to-end tool calls in a temp repo)
- [ ] [A] Ensure `Expect:100-Continue` on STT uploads (already set for named client; verify)
- [ ] [A] Add mic toggle + locale/voice pickers in `AgentsChat.razor` (toggle done; add locale/voice)
- [ ] [A] Improve `wwwroot/js/audio.js` to guarantee WAV16-bit PCM @16kHz and better error handling
- [ ] [A] Add locale auto-detect fallback if `language` not provided (server-side)
- [ ] [A] Wire agent turn-taking: STT -> Agent -> TTS, with UI playback (covered by Conversation mode above)
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
