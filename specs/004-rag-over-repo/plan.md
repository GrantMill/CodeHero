**REQ-003 Implementation Plan (RAG over Repo)**

This plan implements REQ-003 (RAG over Repo) and maps tasks to the repository's constitution, spec, and acceptance criteria. It avoids adding new PackageReferences unless explicitly called out as a "requires dependency request".

Summary
- **Goal:** Build and operate a reproducible, auditable Retrieval-Augmented-Generation (RAG) system over repository docs, requirements, and opted-in code.
- **Primary constraints:** No hard-coded model IDs or keys; config-driven model/provider selection; respect `constitution.md` rules (no unreviewed dependencies, auditable data handling).

How to read this plan
- Each major area has numbered tasks. Use the todo list for tracking progress: tasks were created in the workspace task tracker.

1. **Indexer**

1.1 Add allowlist/denylist enforcement
- Description: Add a configurable `Indexer:Allowlist` (file extensions) and `Indexer:Denylist` (glob patterns) with strict precedence: denylist wins. Log every denied candidate to the audit log.
- Acceptance criteria: Files matching denylist are not indexed; every denylist match produces an audit entry with path, rule matched, and timestamp.

1.2 File-watcher and incremental updates
- Description: Add an optional filesystem watcher (hosted background service) to trigger incremental index updates for changed files; fallback to scheduled full-run on demand.
- Acceptance criteria: On file change, indexing job for affected documents is queued within 60s; `document-map.json` and index are updated incrementally.

1.3 Embedding provider abstraction
- Description: Introduce `IEmbeddingProvider` and a default `FoundryEmbeddingProvider` that reads `AzureAI:Foundry:*` from config. Keep implementation internal; callers depend on the interface only.
- Acceptance criteria: No callers reference Foundry endpoints directly; switching provider requires only DI changes and config.
- Note: This is library-first; no new external packages are required for the abstraction. If a new SDK is desired (e.g., vendor client), open a dependency request.

1.4 Content chunking with line-range metadata
- Description: When chunking files for indexing, record per-chunk source: file path + start/end line numbers, and include this metadata in `document-map.json` and index documents.
- Acceptance criteria: Search results include `path` and line-range for each passage; `RagAnswerService` can reference line ranges in citations.

1.5 Document-map and audit logs
- Description: Ensure `data/document-map.json` contains per-document provenance, contentHash, timestamps, and `indexedBy` metadata. Emit audit entries for index jobs (started, completed, failed) with error details.
- Acceptance criteria: Index job produces `document-map.json` and a machine-readable audit file under `artifacts/indexer/<jobId>.json`.

2. **Orchestrator integration (querying & citation binding)**

2.1 Top-k retrieval binding
- Description: Standardize a `SearchRequest` shape for top-k retrieval, returning passages with `score`, `path`, and `lineRange`. Implement `IHybridSearchService.SearchAsync` to return top-K passages.
- Acceptance criteria: Orchestrator can request top-K and receive passage metadata including line ranges.

2.2 Citation binding in answers
- Description: Update `RagAnswerService` to accept passages with line-range metadata and produce citations in the form `(Source: <relative-path>:<start>-<end>)`. Keep the system prompt explicit about citation format.
- Acceptance criteria: Answers include citations for each quoted or paraphrased passage; tests verify citation formatting.

2.3 Intent fallback and safe defaults
- Description: Orchestrator should prefer RAG answers when `Rag:Enabled=true` and the top-k contains high-confidence passages; otherwise fallback to model-only flow. Implement configurable thresholds.
- Acceptance criteria: Configurable threshold `Rag:MinScoreForUse` is respected; orchestrator logs fallback decisions.

3. **Configuration & toggles**

3.1 Add `Rag:Enabled` toggle
- Description: Add explicit `Rag:Enabled` (bool) to `appsettings.*` and ensure `RagClient`, `LlmOrchestratorAgentService`, and API endpoints short-circuit when disabled.
- Acceptance criteria: Setting `Rag:Enabled=false` disables retrieval stages but keeps the UI and endpoints available for model-only queries.

3.2 Provider-only configuration (no hard-coded IDs)
- Description: Make provider/model choices entirely configuration-driven. Add `Rag:Provider` and nested provider settings (e.g., `Providers:Foundry:DeploymentId`) but never hard-code IDs in code.
- Acceptance criteria: All model and embedding references are read from configuration and can be changed without code edits.

3.3 Backward-compat flags
- Description: Provide `Indexer:EnableEmbedding` and `Indexer:ForceReindex` flags to control index operations safely.

4. **Observability (logs, traces, metrics)**

4.1 Structured logs and metrics for indexing
- Description: Emit structured logs (JSON) for index start/completion/failure, with counts, durations, and errors. Expose metrics: `index_job_duration_seconds`, `index_documents_processed_total`, `index_failures_total`.
- Acceptance criteria: Metrics are emitted via the existing metric pipeline (or simple in-memory counters if none). Logs include jobId and timestamps.

4.2 Tracing spans for embedding/search/answer
- Description: Add tracing spans around `GetEmbeddingAsync`, `SearchClient` calls, and `RagAnswerService` chat calls. Correlate traces via `jobId` / `requestId` across services.
- Acceptance criteria: Traces show call latencies and include TTFB/Total where applicable.

4.3 Query failure modes & retry semantics
- Description: Define and implement retry/backoff for transient failures (embedding and search). Log failures and emit a `query_failure` metric with cause classification (timeout, auth, network, provider-error).
- Acceptance criteria: Retries respect `Resilience:*` configuration and are observable in logs and metrics.

5. **Security & data handling**

5.1 Denylist + secrets avoidance
- Description: Implement `Indexer:Denylist` and a runtime secret scanner that refuses to index files matching common secret patterns; add `Indexer:SecretPatterns` config. Do not log secrets (redact in logs).
- Acceptance criteria: Indexer refuses to index files containing secrets; such events are audited with severity and file path (content redacted).

5.2 Binary & large-file avoidance
- Description: Enforce max file size/configurable threshold and skip binary files. Emit metric `index_skipped_files_total` with reasons.

5.3 CI gating and pre-index checks
- Description: Add a lightweight pre-index validation step suitable for CI that enforces denylist rules and fails builds when policy is violated unless `Indexer:AllowUnsafe=true` is set.
- Acceptance criteria: A script or target exists to run pre-index checks and exit non-zero on failures.

6. **Tests & coverage**

6.1 Unit tests for indexer behaviors
- Description: Add unit tests for allowlist/denylist logic, chunking, line-range extraction, and `document-map.json` generation. Use existing test patterns (mocks for filesystem and SearchClient).
- Acceptance criteria: Tests cover edge cases (huge files, denylist matches, binary detection).

6.2 Unit tests for retriever & answer services
- Description: Add unit tests for `HybridSearchService` (mock embedding provider) and `RagAnswerService` (mock provider responses) verifying citation inclusion and system prompt enforcement.
- Acceptance criteria: Tests assert citations and that the prompt enforces "Use ONLY provided passages" behavior via post-processing logic.

6.3 Integration/E2E test for RAG pipeline
- Description: Add an E2E test that runs rephrase->search->answer with in-memory or mocked provider backends to validate the pipeline and diagnostic responses.
- Acceptance criteria: The pipeline returns an `AnswerResponse` including citations and the expected structure. Tests should run fast and be CI-friendly.

7. **Docs & runbooks**

7.1 Config quickstart and runbook
- Description: Add `docs/REQ-003/quickstart.md` and `docs/REQ-003/runbook.md` describing `appsettings` flags, how to enable/disable RAG, and operational troubleshooting steps.

7.2 Security & audit guidance
- Description: Document denylist patterns, secret patterns, and CI gating instructions. Include developer guidance on what counts as "opt-in" code/docs to index.

8. **Constitution compliance & constraints**

- No new PackageReference is added without a dependency request. If a future vector store adapter (native client) or tracing library is desired, open a formal dependency request. This plan assumes no new dependencies.
- Library-first approach: implement interfaces and keep default concrete types inside existing projects; prefer tests and abstractions first.
- All model/embedding endpoints and keys remain in configuration, not in code.

9. **Work item checklist (logical grouping)**

- Indexer: items 1.1, 1.2, 1.3, 1.4, 1.5
- Orchestrator: items 2.1, 2.2, 2.3
- Config & toggles: items 3.1, 3.2, 3.3
- Observability: items 4.1, 4.2, 4.3
- Security & data handling: items 5.1, 5.2, 5.3
- Tests & coverage: items 6.1, 6.2, 6.3
- Docs: items 7.1, 7.2

10. **Acceptance criteria (cross-cutting)**

- RAG can be toggled via `Rag:Enabled` without code changes.
- The indexer will not index denylisted or secret-containing files and will record audit events for each such occurrence.
- Answers returned via the orchestrator contain in-line citations referencing file paths and line ranges when applicable.
- All new behavior is covered by unit tests and at least one E2E test; tests run in CI without external provider dependencies (use mocks).

11. **Notes & dependency requests**

- If you want vendor SDKs for additional vector stores (Chroma/FAISS) or tracing (OpenTelemetry), create a formal dependency request. This plan assumes no new PackageReference changes.

---

Created-by: REQ-003 plan generator
**REQ-003 Implementation Plan (RAG over Repo)**

This plan implements REQ-003 (RAG over Repo) and maps tasks to the repository's constitution, spec, and acceptance criteria. It avoids adding new PackageReferences unless explicitly called out as a "requires dependency request".

Summary
- **Goal:** Build and operate a reproducible, auditable Retrieval-Augmented-Generation (RAG) system over repository docs, requirements, and opted-in code.
- **Primary constraints:** No hard-coded model IDs or keys; config-driven model/provider selection; respect `constitution.md` rules (no unreviewed dependencies, auditable data handling).

How to read this plan
- Each major area has numbered tasks. Use the todo list for tracking progress: tasks were created in the workspace task tracker.

1. **Indexer**

1.1 Add allowlist/denylist enforcement
- Description: Add a configurable `Indexer:Allowlist` (file extensions) and `Indexer:Denylist` (glob patterns) with strict precedence: denylist wins. Log every denied candidate to the audit log.
- Acceptance criteria: Files matching denylist are not indexed; every denylist match produces an audit entry with path, rule matched, and timestamp.

1.2 File-watcher and incremental updates
- Description: Add an optional filesystem watcher (hosted background service) to trigger incremental index updates for changed files; fallback to scheduled full-run on demand.
- Acceptance criteria: On file change, indexing job for affected documents is queued within 60s; `document-map.json` and index are updated incrementally.

1.3 Embedding provider abstraction
- Description: Introduce `IEmbeddingProvider` and a default `FoundryEmbeddingProvider` that reads `AzureAI:Foundry:*` from config. Keep implementation internal; callers depend on the interface only.
- Acceptance criteria: No callers reference Foundry endpoints directly; switching provider requires only DI changes and config.
- Note: This is library-first; no new external packages are required for the abstraction. If a new SDK is desired (e.g., vendor client), open a dependency request.

1.4 Content chunking with line-range metadata
- Description: When chunking files for indexing, record per-chunk source: file path + start/end line numbers, and include this metadata in `document-map.json` and index documents.
- Acceptance criteria: Search results include `path` and line-range for each passage; `RagAnswerService` can reference line ranges in citations.

1.5 Document-map and audit logs
- Description: Ensure `data/document-map.json` contains per-document provenance, contentHash, timestamps, and `indexedBy` metadata. Emit audit entries for index jobs (started, completed, failed) with error details.
- Acceptance criteria: Index job produces `document-map.json` and a machine-readable audit file under `artifacts/indexer/<jobId>.json`.

2. **Orchestrator integration (querying & citation binding)**

2.1 Top-k retrieval binding
- Description: Standardize a `SearchRequest` shape for top-k retrieval, returning passages with `score`, `path`, and `lineRange`. Implement `IHybridSearchService.SearchAsync` to return top-K passages.
- Acceptance criteria: Orchestrator can request top-K and receive passage metadata including line ranges.

2.2 Citation binding in answers
- Description: Update `RagAnswerService` to accept passages with line-range metadata and produce citations in the form `(Source: <relative-path>:<start>-<end>)`. Keep the system prompt explicit about citation format.
- Acceptance criteria: Answers include citations for each quoted or paraphrased passage; tests verify citation formatting.

2.3 Intent fallback and safe defaults
- Description: Orchestrator should prefer RAG answers when `Rag:Enabled=true` and the top-k contains high-confidence passages; otherwise fallback to model-only flow. Implement configurable thresholds.
- Acceptance criteria: Configurable threshold `Rag:MinScoreForUse` is respected; orchestrator logs fallback decisions.

3. **Configuration & toggles**

3.1 Add `Rag:Enabled` toggle
- Description: Add explicit `Rag:Enabled` (bool) to `appsettings.*` and ensure `RagClient`, `LlmOrchestratorAgentService`, and API endpoints short-circuit when disabled.
- Acceptance criteria: Setting `Rag:Enabled=false` disables retrieval stages but keeps the UI and endpoints available for model-only queries.

3.2 Provider-only configuration (no hard-coded IDs)
- Description: Make provider/model choices entirely configuration-driven. Add `Rag:Provider` and nested provider settings (e.g., `Providers:Foundry:DeploymentId`) but never hard-code IDs in code.
- Acceptance criteria: All model and embedding references are read from configuration and can be changed without code edits.

3.3 Backward-compat flags
- Description: Provide `Indexer:EnableEmbedding` and `Indexer:ForceReindex` flags to control index operations safely.

4. **Observability (logs, traces, metrics)**

4.1 Structured logs and metrics for indexing
- Description: Emit structured logs (JSON) for index start/completion/failure, with counts, durations, and errors. Expose metrics: `index_job_duration_seconds`, `index_documents_processed_total`, `index_failures_total`.
- Acceptance criteria: Metrics are emitted via the existing metric pipeline (or simple in-memory counters if none). Logs include jobId and timestamps.

4.2 Tracing spans for embedding/search/answer
- Description: Add tracing spans around `GetEmbeddingAsync`, `SearchClient` calls, and `RagAnswerService` chat calls. Correlate traces via `jobId` / `requestId` across services.
- Acceptance criteria: Traces show call latencies and include TTFB/Total where applicable.

4.3 Query failure modes & retry semantics
- Description: Define and implement retry/backoff for transient failures (embedding and search). Log failures and emit a `query_failure` metric with cause classification (timeout, auth, network, provider-error).
- Acceptance criteria: Retries respect `Resilience:*` configuration and are observable in logs and metrics.

5. **Security & data handling**

5.1 Denylist + secrets avoidance
- Description: Implement `Indexer:Denylist` and a runtime secret scanner that refuses to index files matching common secret patterns; add `Indexer:SecretPatterns` config. Do not log secrets (redact in logs).
- Acceptance criteria: Indexer refuses to index files containing secrets; such events are audited with severity and file path (content redacted).

5.2 Binary & large-file avoidance
- Description: Enforce max file size/configurable threshold and skip binary files. Emit metric `index_skipped_files_total` with reasons.

5.3 CI gating and pre-index checks
- Description: Add a lightweight pre-index validation step suitable for CI that enforces denylist rules and fails builds when policy is violated unless `Indexer:AllowUnsafe=true` is set.
- Acceptance criteria: A script or target exists to run pre-index checks and exit non-zero on failures.

6. **Tests & coverage**

6.1 Unit tests for indexer behaviors
- Description: Add unit tests for allowlist/denylist logic, chunking, line-range extraction, and `document-map.json` generation. Use existing test patterns (mocks for filesystem and SearchClient).
- Acceptance criteria: Tests cover edge cases (huge files, denylist matches, binary detection).

6.2 Unit tests for retriever & answer services
- Description: Add unit tests for `HybridSearchService` (mock embedding provider) and `RagAnswerService` (mock provider responses) verifying citation inclusion and system prompt enforcement.
- Acceptance criteria: Tests assert citations and that the prompt enforces "Use ONLY provided passages" behavior via post-processing logic.

6.3 Integration/E2E test for RAG pipeline
- Description: Add an E2E test that runs rephrase->search->answer with in-memory or mocked provider backends to validate the pipeline and diagnostic responses.
- Acceptance criteria: The pipeline returns an `AnswerResponse` including citations and the expected structure. Tests should run fast and be CI-friendly.

7. **Docs & runbooks**

7.1 Config quickstart and runbook
- Description: Add `docs/REQ-003/quickstart.md` and `docs/REQ-003/runbook.md` describing `appsettings` flags, how to enable/disable RAG, and operational troubleshooting steps.

7.2 Security & audit guidance
- Description: Document denylist patterns, secret patterns, and CI gating instructions. Include developer guidance on what counts as "opt-in" code/docs to index.

8. **Constitution compliance & constraints**

- No new PackageReference is added without a dependency request. If a future vector store adapter (native client) or tracing library is desired, open a dependency request. This plan assumes no new dependencies.
- Library-first approach: implement interfaces and keep default concrete types inside existing projects; prefer tests and abstractions first.
- All model/embedding endpoints and keys remain in configuration, not in code.

9. **Work item checklist (logical grouping)**

- Indexer: items 1.1, 1.2, 1.3, 1.4, 1.5
- Orchestrator: items 2.1, 2.2, 2.3
- Config & toggles: items 3.1, 3.2, 3.3
- Observability: items 4.1, 4.2, 4.3
- Security & data handling: items 5.1, 5.2, 5.3
- Tests & coverage: items 6.1, 6.2, 6.3
- Docs: items 7.1, 7.2

10. **Acceptance criteria (cross-cutting)**

- RAG can be toggled via `Rag:Enabled` without code changes.
- The indexer will not index denylisted or secret-containing files and will record audit events for each such occurrence.
- Answers returned via the orchestrator contain in-line citations referencing file paths and line ranges when applicable.
- All new behavior is covered by unit tests and at least one E2E test; tests run in CI without external provider dependencies (use mocks).

11. **Notes & dependency requests**

- If you want vendor SDKs for additional vector stores (Chroma/FAISS) or tracing (OpenTelemetry), create a formal dependency request. This plan assumes no new PackageReference changes.

---

Created-by: REQ-003 plan generator
