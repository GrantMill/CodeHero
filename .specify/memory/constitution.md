# CodeHero Constitution

## Core Principles

### I. Test-First and Quality Gates (NON-NEGOTIABLE)
- TDD is mandatory: write tests → see them fail → implement → refactor.
- Every defect requires a failing test before the fix.
- Minimum coverage thresholds enforced in CI:
  - Libraries: ≥ 85% line and branch coverage; business-critical libraries: target 95%+.
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

## Hierarchy of truth
- Purpose: make authoritative precedence explicit for tools and teammates.
1. Constitution (this document) — governance and non-negotiables.
2. Human-written requirements (`REQ-####`) — product/business contract and acceptance criteria.
3. `Spec` (Spec Kit outputs) — agent-operational contracts derived from `REQ`s; proposals when they disagree.
4. Implementation — code, tests, documentation that implement the contract.

## Repository Contract and Agent Boundaries (from current codebase)

- Content roots (allowed write paths for agents and automation):
  - `docs/requirements`, `docs/architecture`, `docs/features`, `artifacts`, `plan/BACKLOG.md` (and optional `plan/HUMAN.md`, `plan/AGENT.md`).
  - Project source folders (explicitly included): all solution project directories (for this repo e.g. `CodeHero.ApiService/`, `CodeHero.Web/`, `CodeHero.Indexer/`, `CodeHero.McpServer/`, `CodeHero.ServiceDefaults/`, `CodeHero.AppHost/`, `CodeHero.Tests/`).
  - Additional allowed patterns: `src/**` or `CodeHero.*/**` may be treated as allowed roots for agent proposals and automation where present.
  - Only approved extensions: `.md`, `.mmd`, `.json`, `.yml/.yaml`, `.razor`, `.cs`, `.csproj`, `.sln`, `.ts/.js`, `.css`, `.ps1`, `.sh`.
  - Block path traversal; no writes outside the configured roots. Any write must be reviewable via PR.

- Agents interact via MCP only; do not add ad-hoc backdoors:
  - Allowlist MCP methods; log and audit calls; no arbitrary shell exec.
  - In dev, MCP may run as a child process; in prod, isolate as a worker/service.

- Traceability (requirements ↔ code/tests):
  - Use `REQ-####` tags in code comments/tests matching requirement files. CI must fail if requirements referenced in docs are missing in code/tests.
  - PR titles/descriptions should reference affected `REQ-####` where applicable.

### Source code edits and constraints
- Agents may propose and create changes to source code files (`.cs`, `.csproj`, `.razor`, etc.) within the allowed project folders and `src/**` areas, subject to the following rules:
  - All code changes proposed by agents must be delivered as PRs with tests, a changelog entry or PR description referencing the related `REQ`(s), and a clear rationale.
  - Agents MUST NOT add or update package references, central package management files, or change restore feeds. Any change touching dependencies must follow the Dependency Request workflow and require explicit human approval.
  - Changes that affect public API contracts or acceptance criteria must include integration/contract tests and an RFC if the change modifies requirements.
  - Small non-functional edits (formatting, comments, whitespace) may be allowed to auto-merge via automation only if they are explicitly whitelisted in repo policy and pass CI.

### Human responsibilities versus agent prohibitions
- Agents must not:
  - Execute generated scripts.
  - Add or update package references (except propose patch updates via PR as defined elsewhere).
  - Introduce embedded/bundled third-party code without approval.
  - Add new external endpoints, telemetry, or services without approval.
- Humans must follow the approval workflows for actions above: dependency requests, telemetry additions, embedded third-party code, and other sensitive changes require documented approval, code owner sign-off, and the appropriate RFC/change process.

## Automation scripts and execution policy
- Agents may generate automation scripts (e.g., `.sh`, `.ps1`, `.yaml`, `.json`) and store them in approved locations (prefer `scripts/automation/`, `artifacts/`, or `tools/`), but agents MUST NOT execute generated scripts themselves.
- Human execution requirement:
  - Any agent-generated script intended to perform destructive or environment-modifying actions must be accompanied by a `README` or metadata file describing purpose, inputs, expected outputs, safety checks, and a short risk assessment.
  - Scripts must be applied only after human review and approval (PR approval or a documented sign-off). Execution must be performed by an authorized human or a controlled automation pipeline with explicit approvals.
- Auditing:
  - CI must preserve the generated script content as artifact and record provenance (generator id, timestamp, PR id).
  - Scripts that alter infrastructure, deployments, or production resources must be gated by separate runbooks and change management processes.

## HTTP, Resilience, and External Calls (grounded by current services)

- Use Aspire `ServiceDefaults` for OpenTelemetry, service discovery, and default resilience. Do not remove OTEL or standard resilience handlers.
- Named external client `foundry` requirements:
  - Prefer HTTP/1.1, `SocketsHttpHandler` with sane pool lifetimes; disable `Expect: 100-continue` for chat/LLM calls when needed.
  - Infinite client timeout is allowed only with strict per-request/call `CancellationToken` caps.
  - Log TTFB and total time for outbound calls; cap logged payloads (≤ 2,000 chars). Never log secrets.
  - Treat 5xx/429 as transient with bounded retries; do not retry on caller cancellations/timeouts.
  - Do not inherit global handlers for `foundry` unless explicitly intended; clear `HttpMessageHandlerBuilderActions` as needed.

## AI/Foundry Configuration and Safety

- All Azure AI Foundry/OpenAI settings must come from configuration keys: `AzureAI:Foundry:(Endpoint|Key|ApiVersion|ChatDeployment|PhiDeployment|EmbeddingDeployment)`; no hardcoded values or keys.
- Default model names may be provided in config only; code must not hardcode model IDs beyond safe fallbacks.
- Redact/omit PII in prompts and logs; truncate remote responses when logging.
- For embeddings, vector fields and dimensions must be explicit in index definitions; recreate indexes only via approved migration PRs.

## AI output quality and guardrails
- AI-generated code and text must follow established repository patterns and style (`.editorconfig`, analyzers).
- Agents must not produce or merge untested code; generated code must be accompanied by tests or a PR that adds tests where feasible.
- Agent PRs must reference the originating `REQ` or `Spec` in the PR description and include provenance metadata (generator id, version, timestamp).

## Health, Telemetry, and Defaults

- Health endpoints `/health` and `/alive` follow ServiceDefaults; expose publicly only with review and proper controls. Exclude health endpoints from tracing.
- Use correlation headers (e.g., `X-Correlation-Id`) across UI ⇄ API calls.
- Measure and emit: rephrase/search/answer stage timings, indexing counts, and background job outcomes.

## Blazor UI Non-Negotiables

- Keep components thin; put logic in injectable services. Avoid blocking calls on the UI thread.
- For long-running actions, show progress and ensure cancelability; propagate `CancellationToken`.
- No direct file system access from UI beyond whitelisted roots via services.

## CI/CD and Branch Protection (codified from repo practices)

- CI must use SDK matching TFM (`10.0.x`), set `DOTNET_CLI_TELEMETRY_OPTOUT=1` and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`.
- Configure job `concurrency` and timeouts; publish artifacts for diagnostic assets (e.g., `data/document-map.json`).
- Sensitive paths always require human approval via CODEOWNERS/branch protection: `docs/requirements/**`, `docs/architecture/**`, `policies/**`, `CodeHero.ServiceDefaults/**`.

## CI Blocking Conditions
- CI must block merges when any of the following occur:
  - Tests fail (unit, integration, critical BDD scenarios).
  - Coverage drops below configured thresholds for the impacted projects.
  - Static analysis or analyzer rules produce warnings treated-as-errors.
  - Security scans (SCA/SAST/secret scanning) report actionable failures.
  - Spec ↔ REQ mismatches detected by traceability checks.
  - Dependency policy violations (unauthorized package changes, disallowed license detected).

## Development Workflow, Reviews, and Gates

- Every PR must:
  - Build successfully and pass all tests.
  - Meet/maintain coverage thresholds.
  - Pass static analysis and security scans.
  - Include docs updates for public surface changes.
  - Receive approval from code owners for affected areas.
- Dependency change workflow (MANDATORY):
  1) Open a “Dependency Request” issue with purpose, alternatives considered, license, size, and security assessment.
  2) Obtain approval from designated approvers (security + domain owner).
  3) Land via a dedicated PR that only changes dependency declarations and related glue.
- Agents/automation rules:
  - Must not change `.sln` or `Directory.Packages.props` for new or upgraded packages. Agents may create PRs that only bump package patch versions (semver PATCH, e.g., `1.2.3` → `1.2.4`) but are NOT permitted to commit or merge these changes. Patch-update PRs created by agents must:
    - Follow the Dependency Request workflow with purpose, alternatives considered, license and security assessment.
    - Run SCA, license checks, and full CI (build, tests, analyzers).
    - Include provenance metadata (agent id, proposed version delta, timestamp).
    - Require explicit approval from the designated dependency approver or code owner before merge.
  - Minor and major version changes remain disallowed for agents and require a human-authored RFC and approval.

## Authority: REQ, Spec, and the Constitution — Precedence & Conflict Resolution

- Definitions
  - `Constitution` — governance rules and non-negotiables (this document). Always highest authority.
  - `REQ` — human-written contractual requirement (source of truth for behavior, acceptance criteria, and business intent).
  - `Spec` — agent-operational contract derived from a `REQ` (machine-friendly implementation details, test scaffolds, or agent plans).

- Precedence
  1. Constitution (highest) — rules both humans and agents must obey.
  2. REQ — human-authored contract and single source of truth for product intent.
  3. Spec — operationalization derived from REQ; considered a proposal when it disagrees with REQ.

- Conflict rules (mandatory)
  - If a `Spec` materially disagrees with its source `REQ`, the `Spec` is treated as a proposed change, not authoritative.
  - Agents MUST NOT auto-apply or merge changes that modify `REQ` or resolve `REQ`/`Spec` conflicts without an explicit human approval step.
  - When a conflict is detected, agents must:
    1. Open a `Specification Conflict` issue linking the `REQ` id(s) and the `Spec` file(s).
    2. Produce a PR that contains a clear proposal (either to update the `Spec` to match `REQ`, or to update the `REQ` with an RFC/justification), include failing tests or diffs that demonstrate the discrepancy, and include provenance metadata (generator id, timestamp, version).
    3. Mark the PR as requiring human approval from the code/req owners; the PR must not be auto-merged.

- Change workflows
  - Changing a `REQ` always requires a human-authored RFC, explicit approval, and an associated migration plan and tests. Agents may draft RFC text and tests, but MAY NOT finalize or merge them without human sign-off.
  - Updating a `Spec` to better implement a `REQ` may be proposed by agents. Such `Spec` PRs must reference the source `REQ` and include automated tests. Small, non-contract operational corrections (typos, formatting) may be allowed to auto-merge only if the change does not alter acceptance criteria and CI gates accept it; this behavior must be explicitly whitelisted in repo policy.

- Metadata & provenance (required on Specs)
  - Each `Spec` must include front-matter or a header with: `source_req: REQ-XXXX`, `generator: <agent-or-tool-id>`, `version`, and `timestamp`.
  - Keep immutable history of `Spec` versions; CI artifacts must preserve the originating `Spec` and `REQ` snapshots used during generation.

- CI and enforcement
  - CI gates will detect REQ↔Spec inconsistencies and fail the PR until a valid resolution (PR + human approval) is present.
  - Traceability checks must ensure every `Spec` references a `REQ` and every `REQ` referenced in code/tests is satisfied by tests or an open work item.

- Example (short)
  - REQ: "REQ-123: GET /items returns 200 and JSON array of items."
  - Spec (agent-generated) claims: "Response includes pagination metadata by default." If pagination is not in `REQ-123`, agent must open conflict issue and propose either an RFC to change `REQ-123` or a Spec update that omits pagination; agent must not merge either without human approval.

Summary: Treat `REQ` as the human contract, `Spec` as the machine-operational proposal derived from that contract, and the `Constitution` as the governance that both must obey. Conflicts require explicit, auditable human-driven resolution; agents can propose but not unilaterally decide.

## Governance

- This Constitution supersedes other practices for agents and automated changes.
- Enforcement occurs via CI policies, branch protection, CODEOWNERS, and required reviews.
- Amendments require an RFC, risk assessment, and migration plan; changes take effect only after approval and communicated rollout.

Version: 1.0.2 | Ratified: 2025-11-15 | Last Amended: 2025-11-15
