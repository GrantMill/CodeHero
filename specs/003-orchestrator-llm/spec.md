# Orchestrator LLM (Generalist with Tool Use)

Metadata:
- source_req: REQ-002
- generator: SpecKit.speckit.specify (GitHub Copilot)
- version: 1.0.0
- timestamp: 2025-11-15T00:00:00Z
- Feature Branch: `003-orchestrator-llm`
- Created: 2025-11-15
- Status: Draft

## Summary
Provide a generalist Orchestrator that answers questions and, when needed, plans and executes allowed tools (MCP) with user approval. It operates in answer-only and plan+act modes, with guardrails and telemetry.

## Goals
- Define prompts and policies for Orchestrator behavior and tool use.
- Expose a tool schema and allowlist for safe operations.
- Implement plan generation and execution with user approval for writes.
- Provide PII-safe telemetry of plans, tool calls, and outcomes.

## Non-Goals
- Selecting a specific model/provider for production.
- Building every tool; start with a minimal allowlist sufficient for repo tasks.
- Fully autonomous changes without human approval for writes.

## User Personas and Primary User Flows
- Developer: asks repo questions; Orchestrator answers with citations; when actions are needed, it proposes a plan for approval.
- Product Owner: requests simple operations (e.g., locate docs), reviews proposed changes before apply.

Primary flows
- Answer-only: question  retrieve context  answer with citations.
- Plan+Act: request change  propose plan (diffs/previews)  user approves  execute tool calls  summarize results.

## Domain and Technical Constraints
- Respect repo-first, PR-based workflow; any write requires explicit approval.
- Tool allowlist limited to safe ops (list/read/write files within approved roots, test run, diff preview).
- Telemetry must avoid storing raw user content beyond whats necessary for auditing.
- Constitution: `constitution.md` defines guardrails for dependency policy, test gates, and agent boundaries.

## Integration Points
- MCP tools: fs/list, fs/readText, fs/writeText; later code/diff, code/edit, code/test per roadmap.
- RAG index (when available) to ground answers with citations.
- Logging/telemetry pipeline for plan and tool call traces.

## Observability and Resilience Requirements
- Each plan and tool call is traced with duration and result.
- Failures provide actionable messages and a fallback to answer-only mode.
- Dry-run and diff previews available before any write.

## Security, Privacy, and Data Handling Requirements
- Approval gates for write operations; interactive confirmation per plan.
- Enforce tool parameter schemas and allowed roots; reject unsafe paths.
- PII-safe logging; redact secrets and sensitive file contents in traces.

## Acceptance Criteria
- Prompting: system/developer prompts define role, safety, and tool policies.
- Tool schema: each exposed tool has name, description, and JSON parameters.
- Planner: returns structured plans with tool steps.
- Executor: runs plan via MCP client, summarizes results to the user.
- Guardrails: approval gate for writes with dry-run previews.
- Fallback: degraded but functional answer-only mode.
- Telemetry: traces exist for plan, tool calls, durations, outcomes.

## Success Criteria
-  95% of write-capable operations require and honor explicit user approval.
-  90% of tool plans execute without unexpected errors; failures provide actionable messages.
- Telemetry present for 100% of plan executions with durations and outcomes recorded.
- Zero unsafe path writes outside allowed roots (enforced via allowlist validation).

### Key Failure Modes

- Plan validation fails or unsafe parameters detected  plan is rejected with explanation; no actions taken.
- User declines approval  execution is canceled and summary is provided with next steps.
- Tool call fails (timeout/error)  error is surfaced and partial results summarized; fallback to answer-only when possible.

## Risks and Open Questions
- Tool surface expansion may require additional guardrails and review UX.
- Balancing verbosity vs clarity in plan explanations.
- Model/provider choice deferred; define minimal quality bar for eval.

related_requirements:
  - docs/requirements/REQ-002.md
