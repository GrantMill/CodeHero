# Backlog

# Core Completed (Historical)
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
- [x] [A] [REQ-002] Orchestrator LLM (Generalist with Tool Use) initial routing
- [x] [A] [REQ-002] Approval gate for write tools (preview diff, confirm before apply)
- [x] [A] [REQ-004] Diff/preview support: implement `code/diff` and wire into approval flow

---
## REQ-003 Repo & RAG Foundation (Merged former "Phase UX")
- [x] [A] Document Map UI (search, preview metadata/headings, DTO parsing, styling)
- [x] [A] Indexer API (AzureSearchIndexerService) invoked from UI (no console app / no GH Actions runner)
- [x] [A] Indexer run history UI (client localStorage persistence)
- [x] [x] Navigation entries (Repository Indexer, Document Map)
- [ ] [A] Persist indexer run history on server (API) to survive restarts
- [ ] [A] Tag / file-type filters in Document Map (e.g., REQ-* vs *.razor vs *.cs)
- [ ] [A] Deep-link selection in Document Map via `?path=` query param
- [x] [A] Expose RAG search.query & search.get endpoints (hybrid retrieval over existing indexed documents)
- [ ] [A] Add confidence scoring / relevance metrics in API response

> Notes (updated):
> - The indexer and Document Map UI are present and the indexer is invoked from the API service. Indexer run history is shown in the UI but currently stored in client localStorage; server persistence is pending.
> - Hybrid search endpoint is exposed (`/api/search/hybrid`) and the hybrid search implementation returns Azure Search result scores. This provides a basic relevance metric; we should normalize/map these scores to a confidence band and include explicit confidence and followup suggestions in the API response (task outstanding).

---
## New Agents Rollout (Model Strategy Separation)
### Orchestrator Routing (Enhancements)
- [ ] [A] Refine Orchestrator system prompt (strict JSON schema: route, reason, task)
- [ ] [A] Implement JSON schema validation + re-prompt on mismatch
- [ ] [A] Telemetry: log route decisions (HELPER_RAG / SCRIBE / ANSWER) + latency + token usage
- [ ] [A] Feature flag `features.orchestrator.strictJson`

### Helper Agent (RAG Answerer) — REQ-003
- [ ] [A] System prompt & developer prompt templates (retrieval-before-answer rule, confidence heuristic)
- [ ] [A] Implement MCP tools: helper.ask → retrieval pipeline (search.query → rerank.apply? → search.get)
- [ ] [A] Confidence scoring (high/medium/low) based on average relevance; include followups when <0.7
- [ ] [A] Citation formatting (file:line or doc:section) enforcement
- [ ] [A] Add feature flag `features.rag.enabled`
- [H] Curate evaluation set (10–20 queries) and store baseline relevance scores
- [ ] [A] Telemetry: retrieval quality (avg score, passages count), cache hits
- [ ] [A] Guardrail: redact secrets/tokens from snippets

### Scribe Agent (Requirement Drafter) — REQ-001 / REQ-002 integration
- [ ] [A] System + developer prompt templates (accept Helper context input)
- [ ] [A] MCP tool: scribe.draft returning structured requirement JSON
- [ ] [A] Chain: Orchestrator route → optional Helper pre-pass → Scribe
- [ ] [A] Acceptance criteria Gherkin validation (Given/When/Then pattern)
- [ ] [A] Feature flag `features.scribe.strictJson`
- [H] Human review checklist for generated requirements (quality gate)

### Planner Agent (Optional PR Breakdown) — Future
- [ ] [A] System prompt JSON schema for multi-PR plan (branch naming, tasks, telemetry, rollout)
- [ ] [A] MCP tool: planner.plan (only after Scribe stabilized)
- [ ] [A] Integrate plan preview & human approve flow

### Model & Deployment Strategy
- [ ] [H] Decide specific deployments: small model for Orchestrator, medium model for Helper (RAG), medium/large for Scribe
- [ ] [A] Wire per-agent client selection (DI factories) based on route
- [ ] [A] Token / cost tracking per agent model

---
## Phases (Updated)
### Phase0 — Validate Azure resources & access (blockers)
- [x] [H] [REQ-003] Validate resources in `rg-prod-ai-weu-gms` (Foundry, Key Vault, Storage, App Insights, Search)
- [x] [H] [REQ-003] Ensure Key Vault access: grant `get,list` to developer or service principal
- [x] [H] [REQ-003] Confirm storage account connection string (validate `stazureaiaip893893803122`)
- [ ] [H] [REQ-003] Grant Managed Identity/service principal access to Key Vault, Storage, Search (pending)

### Phase1 — RAG Infra Bootstrap (Index & Retrieval Foundations)
- [x] [A] Indexer API operational (repo scan -> Azure Search) (console/GH action approach deprecated)
- [x] [A] Document Map shipped
- [ ] [A] Server persistence for indexer history
- [x] [A] Expose search.query & search.get endpoints (hybrid retrieval)

### Phase2 — Helper Agent & Retrieval Quality
- [ ] [A] Helper prompt + tool chain
- [ ] [A] Confidence + citations + followups
- [ ] [H] Evaluation set & baseline relevance

### Phase3 — Scribe Agent & Requirement Drafting
- [ ] [A] Scribe prompt + MCP tool
- [ ] [A] Orchestrator chain integration (Helper→Scribe when needed)
- [ ] [H] Human QA checklist established

### Phase4 — Planner Agent & Multi-PR Workflow (optional)
- [ ] [A] Planner tool & JSON schema
- [ ] [A] Plan approval UX

### Phase5 — Hardening & Telemetry
- [ ] [A] JSON schema validators & re-prompt loops (all agents)
- [ ] [A] Token/cost dashboards per agent model
- [ ] [A] Retrieval cache & p95 latency < 1.5s goal

### Phase6 — Ops & Runbooks
- [ ] [H] Runbook: updating models, rotating keys, monitoring retrieval freshness
- [ ] [H] Cost monitoring and anomaly alerts

### Phase7 — Extended UX & Optional Features
- [ ] [A] Deep link Document Map (?path=) & tag filters
- [ ] [A] Rerank integration (if proved needed) feature flag `features.rag.rerank`
- [ ] [A] Streaming transcription & TTS (future) REQ-001

---
## Notes
- Indexer now runs via API service (AzureSearchIndexerService); no console app or GitHub Actions scheduled indexer.
- Phase UX tasks merged under REQ-003 foundations.
- Distinct models per agent (Orchestrator small, Helper medium, Scribe medium/large) reduce latency & cost.
- Helper always retrieves first; Scribe depends on Helper context for grounded drafting.
- Planner deferred until Helper + Scribe stable.


