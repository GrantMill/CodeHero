# Unified Backlog (Kanban-style)

Legend: [A] = Agent can implement via PR, [H] = Human-in-loop (env/UX/product decision or external systems)

Completed (most recent first)
- [x] [A][REQ-001] Conversational mode: tests for OnPhrase pipeline (bUnit) and integration happy path
- [x] [A][REQ-002] Unit tests for Orchestrator parsing (list/read/mishears)
- [x] [A][REQ-002] Wire MCP Orchestrator agent and make it the default `IAgentService` (fallback)
- [x] [A][REQ-001] Conversational scaffolding: Continuous UI toggle + VAD callbacks wired (client-side); phase chips; feature flag
- [x] [A][REQ-001] Integration tests for `/api/stt` and `/api/tts` (behind feature flag)
- [x] [A][REQ-001] Add targeted retries (connect/read only) and a circuit breaker for STT/TTS; no retry on4xx or large bodies
- [x] [A][REQ-001] Use `HttpCompletionOption.ResponseHeadersRead` for TTS and cap response size
- [x] [A][REQ-001] Make `webfrontend` `.WaitFor(stt)` and `.WaitFor(tts)` in AppHost (after health exists)
- [x] [A][REQ-001] Wire `WithHttpHealthCheck("/health")` for `stt-whisper` and `tts-http` in AppHost
- [x] [H][REQ-001] Add `/health` endpoints to Whisper and TTS containers (return200 when ready)
- [x] [A][REQ-001] Add diagnostics page showing current `Speech:Endpoint`/`Tts:Endpoint` and last call status
- [x] [A][REQ-001] Emit OpenTelemetry dependency traces and basic metrics for STT/TTS
- [x] [A][REQ-001] Add structured logs for STT/TTS calls (duration, status, sizes)
- [x] [A][REQ-001] Add request size limits and cancellation timeouts on `api/stt` and `api/tts`
- [x] [A][REQ-001] Disable output cache for `api/stt` and `api/tts` routes

Planned (priority order – top = next)
- [ ] [A][REQ-001] Feature flag `Features:ContinuousDictation` docs and defaults
- [ ] [A][REQ-002] Orchestrator LLM (Generalist with Tool Use)
 - [ ] [A][REQ-002] Define tool schema exposed to LLM (fs/list, fs/readText, fs/writeText, scribe/createRequirement)
 - [ ] [A][REQ-002] Prompt templates (system/developer) including guardrails and plan schema
 - [ ] [A][REQ-002] Planner returns plan JSON; Executor executes via IMcpClient, with approval gate for writes
 - [ ] [A][REQ-002] Telemetry: traces for plan steps and tool calls
- [ ] [A][REQ-003] RAG over Repo (Docs, Diagrams, Code)
 - [ ] [A][REQ-003] Choose embeddings model/provider and storage (local SQLite/FAISS)
 - [ ] [A][REQ-003] Indexer job to update embeddings on file changes
 - [ ] [A][REQ-003] Retrieval and citation formatting for answers
- [ ] [A][REQ-004] MCP Tooling for Code Changes (Plan, Edit, Diff)
 - [ ] [A][REQ-004] Implement `code/diff` tool
 - [ ] [A][REQ-004] Implement `code/edit` tool with safe patch apply
 - [ ] [A][REQ-004] Implement `code/test` tool and capture results
 - [ ] [A][REQ-004] Wire tools into Orchestrator planner/executor
- [ ] [A][REQ-001] Ensure `Expect:100-Continue` on STT uploads (verify header observed)
- [ ] [A][REQ-001] Add locale/voice pickers in `AgentsChat.razor`
- [ ] [A][REQ-001] Improve `wwwroot/js/audio.js` to guarantee WAV16?bit PCM @16kHz and better error handling
- [ ] [A][REQ-001] Add locale auto-detect fallback if `language` not provided (server-side)
- [ ] [A][REQ-001] Implement streaming transcription endpoint (server push partials) [future]
- [ ] [A][REQ-001] Add endpoint to stream TTS audio for immediate playback [future]
- [ ] [H][REQ-001] Define requirement IDs policy (REQ-### format, uniqueness, authoring rules)
- [ ] [H][REQ-001] Document endpoint scheme/port troubleshooting and dev cert trust steps
- [ ] [H][REQ-001] Document speech feature flags and typical dev `appsettings`
- [ ] [H][REQ-001] Update architecture diagrams to include health checks and named HttpClients
- [ ] [A][REQ-002] Add Foundry Agent service and demo flow (STT -> Agent -> TTS)

Notes
- Keep PRs small and check off each item as it merges.
- Items marked [H] may block dependent [A] work; sequence accordingly.
- If any [H] turns into [A] (requirements clarified), re-tag and proceed.
