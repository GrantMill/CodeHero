# Backlog

- [x] [A] [REQ-002] Heuristics: list/read/create intents incl. word numbers, "top N", and "last requirement"
- [x] [A] [REQ-002] Foundry models/deployments route support; demo STT ? Agent ? TTS flow
- [x] [A] [REQ-001] Ensure `Expect:100-Continue` on STT/TTS HttpClients
- [x] [A] [REQ-002] Telemetry: traces for plan steps and tool calls
- [x] [A] [REQ-002] Planner returns plan JSON; Executor executes via IMcpClient (read-only tools)
- [x] [A] [REQ-002] Prompt templates (system/developer) including guardrails and plan schema
- [x] [A] [REQ-002] Define tool schema and allowlist for LLM (fs/list, fs/readText, scribe/createRequirement)
- [x] [H] [REQ-001] Feature flag `Features:ContinuousDictation` documentation and defaults
- [x] [A] [REQ-001] Conversational mode: tests for OnPhrase pipeline (bUnit) and integration happy path
- [x] [A] [REQ-002] Unit tests for Orchestrator parsing (list/read/mishears)
- [x] [A] [REQ-002] Wire MCP Orchestrator agent and make it the default `IAgentService` (fallback)
- [x] [A] [REQ-001] Conversational scaffolding: Continuous UI toggle + VAD callbacks wired (client-side); phase chips; feature flag
- [x] [A] [REQ-001] Integration tests for `/api/stt` and `/api/tts` (behind feature flag)
- [x] [A] [REQ-001] Add targeted retries (connect/read only) and a circuit breaker for STT/TTS; no retry on4xx or large bodies
- [x] [A] [REQ-001] Use `HttpCompletionOption.ResponseHeadersRead` for TTS and cap response size
- [x] [A] [REQ-001] Make `webfrontend` `.WaitFor(stt)` and `.WaitFor(tts)` in AppHost (after health exists)
- [x] [A] [REQ-001] Wire `WithHttpHealthCheck("/health")` for `stt-whisper` and `tts-http` in AppHost
- [x] [H] [REQ-001] Add `/health` endpoints to Whisper and TTS containers (return200 when ready)
- [x] [A] [REQ-001] Add diagnostics page showing current `Speech:Endpoint`/`Tts:Endpoint` and last call status
- [x] [A] [REQ-001] Emit OpenTelemetry dependency traces and basic metrics for STT/TTS
- [x] [A] [REQ-001] Add structured logs for STT/TTS calls (duration, status, sizes)
- [x] [A] [REQ-001] Add request size limits and cancellation timeouts on `api/stt` and `api/tts`
- [x] [A] [REQ-001] Disable output cache for `api/stt` and `api/tts` routes
- [x] [A] [REQ-002] Orchestrator LLM (Generalist with Tool Use)
- [x] [A] [REQ-002] Define tool schema exposed to LLM (fs/list, fs/readText, scribe/createRequirement)
- [x] [A] [REQ-002] Prompt templates (system/developer) including guardrails and plan schema
- [x] [A] [REQ-002] Approval gate for write tools (preview diff, confirm before apply)
- [x] [A] [REQ-004] Diff/preview support: implement `code/diff` and wire into approval flow
- [x] [A] [REQ-002] Planner returns plan JSON; Executor executes via IMcpClient (read ops)
- [x] [A] [REQ-002] Telemetry: traces for plan steps and tool calls

---

## Outstanding work (prioritized phases)

### Phase0 — Validate Azure resources & access (blockers)
- [x] [H] [REQ-003] Validate resources in `rg-prod-ai-weu-gms` (Foundry, Key Vault, Storage, App Insights, Search)
- [x] [H] [REQ-003] Ensure Key Vault access: grant `get,list` to developer or service principal
- [x] [H] [REQ-003] Confirm storage account connection string (validate `stazureaiaip893893803122`)

### Phase1 — Provision minimal RAG infra & embedding deployment
- [ ] [H] [REQ-003] Register/deploy embedding model in Azure AI Foundry (choose model & deployment name)
- [x] [H] [REQ-003] Provision or confirm vector-capable index store (Azure Cognitive Search preferred)
- [ ] [H] [REQ-003] Create storage blob container for ingested docs (use `stazureaiaip893893803122`)
- [ ] [H] [REQ-003] Add Key Vault secrets for Foundry endpoint/key, Search key, and storage connection string
- [ ] [H] [REQ-003] Grant Managed Identity/service principal access to Key Vault, Storage, and Search

### Phase2 — Indexer implementation & CI
- [ ] [A] [REQ-003] Define index schema: passage text, source file, offsets, embedding vector, metadata
- [ ] [A] [REQ-003] Implement indexer (console app / Azure Function / GitHub Action) to extract docs, compute embeddings, and push to the index
- [ ] [A] [REQ-003] Add CI (GitHub Actions) to run indexer on push or schedule and store artifacts for debugging

### Phase3 — Retrieval API & observability
- [ ] [A] [REQ-003] Implement `/api/rag?q={q}&topK={k}` endpoint returning passages + citations
- [ ] [A] [REQ-003] Add logging and telemetry for embeddings, index updates, and retrieval latency (App Insights)
- [ ] [H] [REQ-003] Document indexing policy: includes/excludes, redaction rules, retention

### Phase4 — Orchestrator integration (RAG → Planner)
- [ ] [A] [REQ-003] Integrate retrieval into `LlmOrchestratorAgentService` (prepend top-K passages + citations to prompt)
- [ ] [A] [REQ-003] Update planner prompt templates to reference retrieved context & citation rules
- [ ] [A] [REQ-003] Add unit & integration tests for3 representative repo questions

### Phase5 — MCP code tooling (write flows and approvals)
- [ ] [A] [REQ-004] Implement `code/diff` and `code/edit` tools with safe patch apply and preview
- [ ] [A] [REQ-004] Implement `code/test` tool to run targeted tests / capture results
- [ ] [A] [REQ-004] Wire code/* tools into Orchestrator planner/executor and approval UI

### Phase6 — Planner/Executor hardening & telemetry
- [ ] [A] [REQ-002] Finalize planner scaffold and production executor with tool allowlist and guardrails
- [ ] [H] [REQ-002] Add tracing for plan steps, tool calls, and post-action evaluation
- [ ] [A] [REQ-002] Add unit/integration tests for planner → executor flows (include RAG influence)

### Phase7 — Ops, runbooks, cost control
- [ ] [A] [REQ-003] Add periodic index rebuild job and CI artifacts retention
- [ ] [H] [REQ-003] Create runbook: Key Vault rotation, index rebuild steps, and cost monitoring for Search & Foundry usage
- [ ] [H] [REQ-003] Document retention and operational run procedures

### Phase8 — Remaining UX & optional capabilities
- [ ] [A] [REQ-001] Finalize STT/TTS provider selection (Foundry/Whisper/Azure Speech) and wire secrets
- [ ] [A] [REQ-001] Add locale/voice pickers in `AgentsChat.razor` and improve `wwwroot/js/audio.js`
- [ ] [A] [REQ-001] Implement streaming transcription and TTS streaming endpoints (future)
- [ ] [H] [REQ-001] Define requirement ID policy and update architecture docs

---

Notes
- Priorities: unblockers first (Phase0), then infra (Phase1), indexer & CI (Phase2), retrieval API & observability (Phase3), orchestrator integration (Phase4), then code tooling and hardening.
- I removed duplicate/overlapping backlog entries and consolidated REQ-002/REQ-003 workstreams into the phased plan above.

