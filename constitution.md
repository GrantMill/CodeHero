# CodeHero Constitution

## Core Principles

### I. Test-First and Quality Gates (NON-NEGOTIABLE)
- TDD is mandatory: write tests → see them fail → implement → refactor.
- Every defect requires a failing test before the fix.
- Minimum coverage thresholds enforced in CI:
  - Libraries: ≥ 75% line and branch coverage; business-critical libraries: target 80%+.
  - Web/UI projects: meaningful component/unit tests for logic; end-to-end coverage for critical paths.
- Tests must be fast, deterministic, and isolated (no real network, file system, or time dependencies without explicit integration test designation).
- No code merged to `main` without green CI, coverage above thresholds, and reviewer approval.

### II. Dependency Governance and Supply Chain Security (NON-NEGOTIABLE)
- No new external dependencies/packages without human approval via a documented change request (RFC/issue). This includes:
  - Adding `PackageReference`, updating versions, enabling source generators, or introducing transitive dependencies.
- Prefer .NET BCL and existing, approved first-party libraries before introducing any dependency.
- Use Central Package Management (`Directory.Packages.props`) and exact/pinned versions.
- Maintain an allowlist of approved packages and licenses; unapproved licenses are rejected.
- All dependency updates must come via dedicated PRs and require security review (SCA) and license check.
- Generate SBOM for releases; verify package signatures when supported.
- Agents must not:
  - Add or update any `PackageReference` except as described below for patch-level updates.
  - Modify `Directory.Packages.props` or equivalent central management files.
  - Add new NuGet feeds or change restore sources.
  - Introduce embedded/bundled third‑party code without approval.
- Security posture: no secrets in code; use environment configuration or secret stores; validate all inputs; use parameterized queries; avoid unsafe code unless approved.

### III. Library-First, Modular Architecture
- New functionality starts as a small, testable library with a clear purpose and minimal public surface.
- Public APIs are stable and documented; prefer `internal` by default.
- Cross-project reuse via libraries; avoid copy/paste duplication.

### IV. Observability by Default
- Structured logging using `Microsoft.Extensions.Logging`; no PII in logs.
- Correlate operations with activity/correlation IDs; honor cancellation tokens.
- Emit metrics and traces where applicable; ensure logs/tests cover failure paths.

### V. Versioning and Breaking Changes
- Semantic Versioning for libraries: MAJOR.MINOR.PATCH.
- Breaking changes require RFC, migration guides, and deprecation windows.
- Contract and integration tests guard public behavior.

### VI. Simplicity and Readability
- Prefer simple, maintainable solutions over cleverness.
- Lint/analyzers enabled; warnings treated as errors in CI.
- Consistent code style via `.editorconfig`; documented patterns and anti‑patterns.

## Additional Constraints and Standards
- Platform: .NET 10. Prioritize Blazor patterns for UI; keep UI thin and logic in libraries.
- Async all the way; avoid blocking calls on UI threads; respect `CancellationToken`.
- Deterministic builds and reproducible CI; lock tool versions where practical.
- Security checks in CI: SAST, SCA, secret scanning. Fails are blocking.
- Performance discipline: avoid allocating hot paths; measure before optimizing; include perf budgets for critical scenarios.

## Hierarchy of Truth
1. Constitution (this document) — governance and non-negotiables.
2. Human-written requirements (`REQ-####`) — product/business contract and acceptance criteria.
3. Specs (`specs/**/spec.md`) — agent-operational contracts derived from `REQ`s; proposals when they disagree.
4. Implementation — code, tests, documentation that implement the contract.

## Repository Contract and Agent Boundaries
- Allowed write roots: `docs/requirements`, `docs/architecture`, `docs/features`, `artifacts`, `plan/BACKLOG.md`, solution project directories (`CodeHero.*`), and optionally `src/**` where present.
- Approved extensions: `.md`, `.mmd`, `.json`, `.yml/.yaml`, `.razor`, `.cs`, `.csproj`, `.sln`, `.ts/.js`, `.css`, `.ps1`, `.sh`.
- Block path traversal; all writes must be reviewable via PR.
- Agents interact only via MCP; allowlist and audit calls; no arbitrary shell execution.
- Traceability: use `REQ-####` tags in code/tests; CI fails on missing links.

## Source Code Edits and Constraints
- Agent-proposed code changes must include tests, rationale, and REQ references.
- Agents MUST NOT change dependencies (except proposing patch updates via workflow) or feeds.
- Public API impacting changes require integration tests and possibly RFCs.
- Minor non-functional edits may auto-merge only if explicitly whitelisted and CI passes.

## Human Responsibilities vs Agent Prohibitions
- Agents must not execute generated scripts, add/update dependencies, introduce third-party code, or add external telemetry/services without approval.
- Humans perform approvals for dependency changes, telemetry additions, and sensitive modifications.

## Automation Scripts Policy
- Agents may generate scripts but NOT execute them.
- Scripts require README/metadata (purpose, inputs, outputs, safety checks, risk assessment).
- Execution only after human review/approval; CI preserves artifacts and provenance.

## AI/Foundry Configuration and Safety
- All Azure AI/OpenAI settings sourced from config keys (`AzureAI:Foundry:*`).
- No hardcoded secrets or model IDs beyond safe fallbacks.
- Redact PII in prompts/logs; truncate remote responses.
- Embedding indexes defined explicitly; migrations via approved PRs.

## AI Output Quality and Guardrails
- Generated code conforms to style (`.editorconfig`, analyzers) and includes tests where feasible.
- PRs include provenance metadata (generator id, version, timestamp).
- No unauthorized telemetry changes.

## Health, Telemetry, and Defaults
- `/health` and `/alive` follow ServiceDefaults; sensitive exposure requires review.
- Use correlation headers across UI ⇄ API.
- Emit timings (rephrase/search/answer), indexing counts, background job outcomes.

## Blazor UI Non-Negotiables
- Thin components; logic in services.
- Long-running actions show progress and support cancellation.
- No direct FS access outside whitelisted roots.

## CI/CD and Branch Protection
- CI uses matching SDK, sets telemetry opt-outs, enforces concurrency/timeouts.
- Sensitive paths require code owner approval.

## CI Blocking Conditions
- Block merges on test failures, coverage drops, analyzer warnings, security scan failures, Spec↔REQ mismatches, dependency policy violations.

## Development Workflow and Gates
- Every PR: builds, passes tests, coverage thresholds, static analysis, security scans, docs updates, owner approvals.
- Dependency changes follow request workflow and dedicated PR.

## Authority and Conflict Resolution
- Precedence: Constitution > REQ > Spec > Implementation.
- Spec disagreement with REQ becomes a proposal; agents open a conflict issue + PR with rationale and tests; human approval required.
- Changing a REQ requires human-authored RFC and approval.
- Specs must include metadata: `source_req`, `generator`, `version`, `timestamp`.
- CI enforces traceability and resolves inconsistencies only after human approval.

## Spec Kit
This repository uses GitHub Spec Kit.
Specs under `specs/**` derive from the human-written requirements in `docs/requirements`.
Agents MUST follow this Constitution when generating specs, plans, tasks, and code.

## Compatibility Note
Specs, plans, and tasks are subordinate to the Constitution and referenced `REQ` documents, ensuring clarity for Spec Kit’s planner.

Version: 1.0.2 | Ratified: 2025-11-15 | Last Amended: 2025-11-15
