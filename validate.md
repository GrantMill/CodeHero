**REQ-003 Validation Plan (RAG over Repo)**

Purpose
- This document defines a validation approach for REQ-003 (RAG over Repo) that can be implemented as automated tests, CI checks, and manual runbooks. It references `specs/003-rag-over-repo/spec.md`, `docs/requirements/REQ-003*.md`, and `constitution.md` and maps validation methods to acceptance & success criteria.

1) Automated validation

A. Unit tests (fast, deterministic)
- Indexer unit tests (CodeHero.Tests/AzureSearchIndexerServiceTests.cs additions):
  - Denylist/Allowlist logic: given input file paths and configured `Indexer:Denylist`/`Indexer:Allowlist`, assert which files are accepted/denied and that denied entries produce audit events.
  - Chunking & line-range extraction: for a small test file, assert produced chunks contain correct `startLine`/`endLine` mapping and that `document-map.json` entries reference these ranges.
  - Secret scanner: feed sample files containing secret-like strings (API key patterns) and assert indexing is refused and redaction occurs in logs.

- Retrieval unit tests (CodeHero.Tests/HybridSearchServiceTests.cs):
  - Embed mock: inject a mock `IEmbeddingProvider` returning deterministic vectors; assert `HybridSearchService.SearchAsync` builds vector query and returns expected top-K items when `SearchClient` is mocked.

- Answering unit tests (CodeHero.Tests/RagAnswerServiceTests.cs):
  - Citation formatting: pass sample passages (with `path` and `lineRange`) and a mocked provider response; assert final answer contains citations formatted as `(Source: <path>:<start>-<end>)` and that any returned text references only provided passages.

B. Integration tests (isolated, CI-friendly; mocks replace external network)
- RAG E2E test (`CodeHero.Tests/RagPipelineE2ETests.cs`): spin up the pipeline in-memory using mocked embedding and chat providers and a seeded in-memory index containing small canonical docs.
  - Test 1: Ask "What does Conversational mode do?" Assert that the top-cited passages include `README.md` and `docs/architecture/<diagram>.md` (or expected fixture path) and that the final `AnswerResponse` includes at least one citation to those files.
  - Test 2: Ask "Where are requirements stored?" Assert citations referencing `docs/requirements/` files and the FileStore API file/path (e.g., `CodeHero.ApiService/Services/FileStore.cs` or similar). The test must assert at least one citation pointing to a `docs/requirements` file and one to the FileStore API.
  - Test 3 (RAG toggle): Run the same queries with `Rag:Enabled=false` set in configuration; assert that returned answers do not include citations and that orchestrator falls back to non-RAG answer flow (e.g., model-only stubbed response). Ensure the pipeline logs a fallback event.

C. Guard tests / failure-mode tests
- Index build failure: Mock `SearchClient` or throw an exception during index upload; assert that the index job emits an audit failure entry under `artifacts/indexer/<jobId>.json`, logs an error, and the orchestrator uses fallback behavior for queries (no crash).
- Denylist enforcement: Attempt to index a denylisted path in a test fixture; assert the entry is absent from the index and audit log contains denylist match with rule id and timestamp.

Test harness notes
- Use existing `CodeHero.Tests` patterns (xUnit or MSTest per repo) and mocking/fakes already present (see `AzureSearchIndexerServiceTests`).
- Provide test fixtures under `CodeHero.Tests/Fixtures/REQ-003/` containing small files: `README.md` (with `Conversational mode` text), `docs/requirements/req1.md`, and a `FileStore` API sample file. Seed the mocked index with these documents and deterministic vectors.
- Keep all integration tests fast (<5s ideally) by mocking network calls and keeping small datasets.

2) Governance validation (Constitution mapping)

A. Dependency control
- CI check that verifies no `PackageReference` / `PackageReference` additions occurred in the PR without a linked dependency RFC: implement a CI job that diffs `**/*.csproj` for `PackageReference` additions and fails unless the PR includes a `DEPENDENCY-RFC.md` file or label.

B. Logging and secrets
- Static check for logging: run a scan of emitted logs (during test runs or pre-production runs) to ensure no raw secrets were written. Implement this via:
  - A test that runs the indexer against a fixture containing fake secrets and asserts that logs contain redacted markers (e.g., `REDACTED_SECRET`) and not the secret patterns themselves.
  - A CI job that scans new/changed code for obvious unsafe logging patterns (e.g., `Log.*(".*password|secret|token.*")` with interpolated variables) and flags them for review.

C. Coverage & thresholds
- When adding new RAG libraries or substantial code, require coverage checks: ensure unit tests cover new library code and meet the repository's coverage policy (use existing coverage pipeline). If thresholds are not met, the PR should include a test plan and justification.

3) Manual validation steps (for reviewers / operators)

A. Local quick validation script (PowerShell)
- `scripts/validate-local-rag.ps1` (manual, not yet implemented) — sample steps for a reviewer to run:
  1. Start the ApiService and Web locally in Development mode with `Rag:Enabled=true` and default `Providers:Foundry` pointing to a test deployment (or mocks).
  2. Run the indexer (via API: POST `/api/search/indexer/run`) against a repo subset or use prebuilt `artifacts/indexer/sample-job.json`.
  3. Query the API endpoint `POST /api/agent/chat` or `POST /api/chat/answer` with the JSON body { "question": "What does Conversational mode do?" }.
  4. Inspect the response's `citations` array and assert README and architecture-diagram paths are present.
  5. Inspect `artifacts/indexer/<jobId>.json` for audit entries and `data/document-map.json` for per-chunk lineRange metadata.

  Example `Invoke-RestMethod` snippet (PowerShell):

  ```powershell
  $uri = 'http://localhost:5000/api/chat/answer'
  $body = @{ question = 'What does Conversational mode do?' } | ConvertTo-Json
  Invoke-RestMethod -Method Post -Uri $uri -Body $body -ContentType 'application/json'
  ```

B. Manual denylist / secret checks
- Place a file matching the denylist and a file containing a fake secret in a test folder, run the indexer, and verify:
  - Denylisted file is not present in the index (search endpoint returns no matches).
  - Audit logs in `artifacts/indexer/` contain denylist entries and secret-detection entries with redaction.

C. Observability inspection
- Inspect logs for structured JSON entries with `jobId` and `requestId`. Verify metrics exposed by the app (if Prometheus endpoint exists) include `index_job_duration_seconds` and `index_documents_processed_total`.

4) Test data & fixtures
- Provide deterministic fixtures under `CodeHero.Tests/Fixtures/REQ-003/`:
  - `README.md` with a paragraph about "Conversational mode".
  - `docs/requirements/req-001.md` describing where requirements are stored.
  - `CodeHero.ApiService/Services/FileStore.cs` (or minimal API file stub) to validate API file citations.

5) CI integration recommendations
- Add CI jobs/stages:
  - `validate-deps` — run dependency diff check (fail on unapproved `PackageReference`).
  - `unit-tests` — run all unit tests including new RAG tests.
  - `rag-e2e-mocks` — run the E2E RAG tests (with mocks) to assert citations and toggle behavior.
  - `preindex-check` — run `scripts/preindex-check.ps1` to validate denylist/secret patterns.
  - `validation-report` — publish artifacts: `artifacts/indexer/*.json`, `data/document-map.json`, and test-report XML.

6) Mapping to spec acceptance criteria
- For each automated test above, include in the test metadata which spec acceptance criteria it proves (e.g., citation presence, index-update behavior, denylist enforcement). Keep a small table in the CI job output linking tests → acceptance criteria.

7) Implementation notes & next steps
- Start by adding fixtures and unit tests (fast feedback). Implement mockable abstractions (`IEmbeddingProvider`) to make tests deterministic. Then implement integration tests and CI steps.
- Create `scripts/validate-local-rag.ps1` as a convenience tool for reviewers after tests are stable.

---

Created-by: REQ-003 validation designer
