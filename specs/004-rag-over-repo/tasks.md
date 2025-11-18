**REQ-003 Tasks (RAG over Repo) — Checklist**

Notes:
- Sources: `specs/003-rag-over-repo/spec.md`, `docs/requirements/REQ-003*.md`, `constitution.md`.
- Each task is small and PR-sized; file paths are suggested change targets.

- [x] T001 Add `Rag:Enabled` config boolean and read it in `CodeHero.Web/Services/RagClient.cs` and `CodeHero.ApiService/Program.cs` to short-circuit retrieval when disabled. (spec: Acceptance Criteria / REQ-003)
- [x] T002 Make provider and model configuration-only: add `Rag:Provider` and `Providers:*` config entries and update `CodeHero.ApiService/Services/Rag/*` to read deployments from `IConfiguration` only (no hard-coded IDs). (spec: Config & toggles / constitution)
- [x] T003 Introduce `IEmbeddingProvider` interface in `CodeHero.ApiService/Abstractions/IEmbeddingProvider.cs` and update callers to depend on the interface. (plan: 1.3)
- [x] T004 Implement `FoundryEmbeddingProvider` in `CodeHero.ApiService/Services/FoundryEmbeddingProvider.cs` that implements `IEmbeddingProvider` and reads `AzureAI:Foundry:*` from config. (plan: 1.3)
- [ ] T005 Refactor `AzureSearchIndexerService.ScanRepositoryFiles` in `CodeHero.ApiService/AzureSearchIndexerService.cs` to produce chunked passages (≤ configurable chunk size). (plan: 1.4)
- [ ] T006 Add per-chunk `lineRange` metadata (start/end line numbers) to index documents and to `data/document-map.json` in `CodeHero.ApiService/AzureSearchIndexerService.cs`. (plan: 1.4)
- [ ] T007 Implement `Indexer:Denylist` (glob patterns) check and make denylist take precedence over allowlist in `AzureSearchIndexerService.cs` (log audit entries to `artifacts/indexer/<jobId>.json`). (plan: 1.1 / 5.1)
- [ ] T008 Add runtime secret pattern scanning in `AzureSearchIndexerService.cs` using `Indexer:SecretPatterns` config and refuse to index files containing secrets; redact content in logs. (plan: 5.1)
- [ ] T009 Enforce a configurable `Indexer:MaxFileSizeBytes` check and skip binary files in `AzureSearchIndexerService.cs`, emit `index_skipped_files_total` metric. (plan: 5.2)
- [ ] T010 Emit `data/document-map.json` with enriched provenance fields (`contentHash`, `indexedAt`, `indexedBy`, per-chunk `lineRange`) in `AzureSearchIndexerService.cs`. (plan: 1.5)
- [ ] T011 Add an incremental file-watcher hosted service `CodeHero.ApiService/Services/FileWatcherIndexTrigger.cs` to queue index jobs for changed files (optional feature flag `Indexer:EnableFileWatcher`). (plan: 1.2)
- [ ] T012 Standardize the top-k retrieval response shape: update `IHybridSearchService.SearchAsync` to return passages with `score`, `path`, `lineRange`, and `text` (files: `CodeHero.ApiService/Services/Rag/IHybridSearchService.cs` and `HybridSearchService.cs`). (plan: 2.1)
- [ ] T013 Update `RagAnswerService` in `CodeHero.ApiService/Services/Rag/RagAnswerService.cs` to accept passages with `lineRange` and include citations formatted as `(Source: <relative-path>:<start>-<end>)`. Add unit-level formatting verification. (plan: 2.2)
- [ ] T014 Add orchestrator fallback: implement `Rag:MinScoreForUse` config and update `CodeHero.Web/Services/LlmOrchestratorAgentService.cs` to prefer RAG only when passages meet the threshold. Log fallback decisions in `AgentTelemetry`. (plan: 2.3)
- [ ] T015 Add structured index job logs and basic metrics: instrument `AzureSearchIndexerService.CreateIndexAndRunAsync` to emit `index_job_duration_seconds`, `index_documents_processed_total`, and `index_failures_total` via existing metrics or in-memory counters (files: `AzureSearchIndexerService.cs`). (plan: 4.1)
- [ ] T016 Add tracing spans around `GetEmbeddingAsync`, `SearchClient` calls, and `RagAnswerService` chat calls; correlate using `requestId`/`jobId` (files: `AzureSearchIndexerService.cs`, `HybridSearchService.cs`, `RagAnswerService.cs`). (plan: 4.2)
- [ ] T017 Implement retry/backoff for transient failures when calling embedding and chat endpoints, honoring `Resilience:*` configuration (files: `HybridSearchService.cs`, `RagAnswerService.cs`). (plan: 4.3)
- [ ] T018 Add unit tests for indexer denylist, allowlist, and secret detection in `CodeHero.Tests/AzureSearchIndexerServiceTests.cs` (new test cases). (plan: 6.1)
- [x] T019 Add unit tests for `IEmbeddingProvider` (mock) and `HybridSearchService.EmbedAsync` behavior in `CodeHero.Tests/HybridSearchServiceTests.cs`. (plan: 6.2)
- [ ] T020 Add unit tests for `RagAnswerService` verifying citation formatting and that the service includes only provided passages in answers (mock chat responses) in `CodeHero.Tests/RagAnswerServiceTests.cs`. (plan: 6.2)
- [ ] T021 Add a fast E2E test `CodeHero.Tests/RagPipelineE2ETests.cs` that runs rephrase->search->answer using mocked provider responses and asserts citations and structure (CI-friendly, no external network). (plan: 6.3)
- [ ] T022 Create a CI pre-index check script `scripts/preindex-check.ps1` that validates `Indexer:Denylist` and `Indexer:SecretPatterns` and fails the build on violations (unless `Indexer:AllowUnsafe=true`). Document usage in `docs/REQ-003/runbook.md`. (plan: 5.3)
- [ ] T023 Add docs `docs/REQ-003/quickstart.md` explaining `Rag:Enabled`, `Providers:*` config, and how to run the indexer (include sample `appsettings.Development.json`). (plan: 7.1)
- [ ] T024 Add `docs/REQ-003/security.md` describing denylist, secret patterns, audit logs, and CI gating guidelines (plan: 7.2)

Validation tasks (manual/automated):

- [ ] V001 Run `scripts/preindex-check.ps1` against a sample repo and verify it fails on denylist/secret matches, and confirm audit file created in `artifacts/indexer/`. (REQ acceptance)
- [ ] V002 Run the E2E RAG test (`CodeHero.Tests/RagPipelineE2ETests`) in CI and confirm it passes without external provider access. (REQ acceptance)

---

Notes:
- T001–T004 implemented and present in current branch: embedding abstraction, Foundry provider, Rag toggle, DI wiring.
- T019 implemented: embedding provider tests and HybridSearchService tests added to `CodeHero.Tests` and passing locally.

---

Each task is intentionally scoped to be small and reviewable as a single PR. When ready, mark the corresponding todo entry in the workspace todo list.
**REQ-003 Tasks (RAG over Repo) — Checklist**

Notes:
- Sources: `specs/003-rag-over-repo/spec.md`, `docs/requirements/REQ-003*.md`, `constitution.md`.
- Each task is small and PR-sized; file paths are suggested change targets.

- [ ] T001 Add `Rag:Enabled` config boolean and read it in `CodeHero.Web/Services/RagClient.cs` and `CodeHero.ApiService/Program.cs` to short-circuit retrieval when disabled. (spec: Acceptance Criteria / REQ-003)
- [ ] T002 Make provider and model configuration-only: add `Rag:Provider` and `Providers:*` config entries and update `CodeHero.ApiService/Services/Rag/*` to read deployments from `IConfiguration` only (no hard-coded IDs). (spec: Config & toggles / constitution)
- [ ] T003 Introduce `IEmbeddingProvider` interface in `CodeHero.ApiService/Abstractions/IEmbeddingProvider.cs` and update callers to depend on the interface. (plan: 1.3)
- [ ] T004 Implement `FoundryEmbeddingProvider` in `CodeHero.ApiService/Services/FoundryEmbeddingProvider.cs` that implements `IEmbeddingProvider` and reads `AzureAI:Foundry:*` from config. (plan: 1.3)
- [ ] T005 Refactor `AzureSearchIndexerService.ScanRepositoryFiles` in `CodeHero.ApiService/AzureSearchIndexerService.cs` to produce chunked passages (≤ configurable chunk size). (plan: 1.4)
- [ ] T006 Add per-chunk `lineRange` metadata (start/end line numbers) to index documents and to `data/document-map.json` in `CodeHero.ApiService/AzureSearchIndexerService.cs`. (plan: 1.4)
- [ ] T007 Implement `Indexer:Denylist` (glob patterns) check and make denylist take precedence over allowlist in `AzureSearchIndexerService.cs` (log audit entries to `artifacts/indexer/<jobId>.json`). (plan: 1.1 / 5.1)
- [ ] T008 Add runtime secret pattern scanning in `AzureSearchIndexerService.cs` using `Indexer:SecretPatterns` config and refuse to index files containing secrets; redact content in logs. (plan: 5.1)
- [ ] T009 Enforce a configurable `Indexer:MaxFileSizeBytes` check and skip binary files in `AzureSearchIndexerService.cs`, emit `index_skipped_files_total` metric. (plan: 5.2)
- [ ] T010 Emit `data/document-map.json` with enriched provenance fields (`contentHash`, `indexedAt`, `indexedBy`, per-chunk `lineRange`) in `AzureSearchIndexerService.cs`. (plan: 1.5)
- [ ] T011 Add an incremental file-watcher hosted service `CodeHero.ApiService/Services/FileWatcherIndexTrigger.cs` to queue index jobs for changed files (optional feature flag `Indexer:EnableFileWatcher`). (plan: 1.2)
- [ ] T012 Standardize the top-k retrieval response shape: update `IHybridSearchService.SearchAsync` to return passages with `score`, `path`, `lineRange`, and `text` (files: `CodeHero.ApiService/Services/Rag/IHybridSearchService.cs` and `HybridSearchService.cs`). (plan: 2.1)
- [ ] T013 Update `RagAnswerService` in `CodeHero.ApiService/Services/Rag/RagAnswerService.cs` to accept passages with `lineRange` and include citations formatted as `(Source: <relative-path>:<start>-<end>)`. Add unit-level formatting verification. (plan: 2.2)
- [ ] T014 Add orchestrator fallback: implement `Rag:MinScoreForUse` config and update `CodeHero.Web/Services/LlmOrchestratorAgentService.cs` to prefer RAG only when passages meet the threshold. Log fallback decisions in `AgentTelemetry`. (plan: 2.3)
- [ ] T015 Add structured index job logs and basic metrics: instrument `AzureSearchIndexerService.CreateIndexAndRunAsync` to emit `index_job_duration_seconds`, `index_documents_processed_total`, and `index_failures_total` via existing metrics or in-memory counters (files: `AzureSearchIndexerService.cs`). (plan: 4.1)
- [ ] T016 Add tracing spans around `GetEmbeddingAsync`, `SearchClient` calls, and `RagAnswerService` chat calls; correlate using `requestId`/`jobId` (files: `AzureSearchIndexerService.cs`, `HybridSearchService.cs`, `RagAnswerService.cs`). (plan: 4.2)
- [ ] T017 Implement retry/backoff for transient failures when calling embedding and chat endpoints, honoring `Resilience:*` configuration (files: `HybridSearchService.cs`, `RagAnswerService.cs`). (plan: 4.3)
- [ ] T018 Add unit tests for indexer denylist, allowlist, and secret detection in `CodeHero.Tests/AzureSearchIndexerServiceTests.cs` (new test cases). (plan: 6.1)
- [ ] T019 Add unit tests for `IEmbeddingProvider` (mock) and `HybridSearchService.EmbedAsync` behavior in `CodeHero.Tests/HybridSearchServiceTests.cs`. (plan: 6.2)
- [ ] T020 Add unit tests for `RagAnswerService` verifying citation formatting and that the service includes only provided passages in answers (mock chat responses) in `CodeHero.Tests/RagAnswerServiceTests.cs`. (plan: 6.2)
- [ ] T021 Add a fast E2E test `CodeHero.Tests/RagPipelineE2ETests.cs` that runs rephrase->search->answer using mocked provider responses and asserts citations and structure (CI-friendly, no external network). (plan: 6.3)
- [ ] T022 Create a CI pre-index check script `scripts/preindex-check.ps1` that validates `Indexer:Denylist` and `Indexer:SecretPatterns` and fails the build on violations (unless `Indexer:AllowUnsafe=true`). Document usage in `docs/REQ-003/runbook.md`. (plan: 5.3)
- [ ] T023 Add docs `docs/REQ-003/quickstart.md` explaining `Rag:Enabled`, `Providers:*` config, and how to run the indexer (include sample `appsettings.Development.json`). (plan: 7.1)
- [ ] T024 Add `docs/REQ-003/security.md` describing denylist, secret patterns, audit logs, and CI gating guidelines (plan: 7.2)

Validation tasks (manual/automated):

- [ ] V001 Run `scripts/preindex-check.ps1` against a sample repo and verify it fails on denylist/secret matches, and confirm audit file created in `artifacts/indexer/`. (REQ acceptance)
- [ ] V002 Run the E2E RAG test (`CodeHero.Tests/RagPipelineE2ETests`) in CI and confirm it passes without external provider access. (REQ acceptance)

---

Each task is intentionally scoped to be small and reviewable as a single PR. When ready, mark the corresponding todo entry in the workspace todo list.
