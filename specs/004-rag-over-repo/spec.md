# RAG over Repo (Docs, Diagrams, Code)

Metadata
- source_req: REQ-003
- generator: SpecKit.speckit.specify (GitHub Copilot)
- version: 1.0.0
- timestamp: 2025-11-15T00:00:00Z

- Feature Branch: `004-rag-over-repo`
- Created: 2025-11-15
- Status: Draft

## Summary
Provide a local knowledge index over requirements, architecture, selected code surfaces, and docs so users get grounded answers with citations. Responses reference source files and, where possible, line ranges.

## Goals
- Build an index over `docs/requirements`, `docs/architecture`, and selected code/docs agreed by maintainers.
- Support chunking and embeddings stored locally or in a pluggable index.
- Enable the Orchestrator to query top-k passages before answering.
- Return answers with file citations and optional line references.

## Non-Goals
- Finalize embedding model/provider; keep swappable.
- Indexing all files in the repository by default.
- Cross-repo search.

## User Personas and Primary User Flows
- Developer: asks questions about architecture/requirements; receives grounded answers with citations.
- Reviewer: validates that responses cite correct files/sections.

Primary flows
- Index build/refresh when files change.
- Question → retrieve top-k passages → answer with citations and footnotes.

## Domain and Technical Constraints
- Respect file allowlists; avoid indexing secrets or generated binaries.
- Toggle to enable/disable RAG and choose model/provider via configuration.
- Align with repo-first; all config lives in repo.
- Constitution: `.specify/memory/constitution.md` governs data handling, observability, and blocking CI gates.

## Integration Points
- File system watcher or build step to trigger re-index.
- Storage for embeddings (local store first; cloud optional).
- Orchestrator query pipeline to consume top-k context.

## Observability and Resilience Requirements
- Indexing logs include file counts, durations, and failures.
- Query traces show retrieved sources and scores (non-sensitive).
- Partial failures degrade gracefully (answer with caveats, or fallback to non-RAG answer).

## Security, Privacy, and Data Handling Requirements
- Exclude sensitive files by default; maintain a denylist and allowlist.
- Do not store proprietary content outside the repo without approval.
- Surface citations without revealing secrets; redact as needed in traces.

## Acceptance Criteria
- Index job builds/updates when watched files change.
- “What does Conversational mode do?” cites README and sequence diagrams.
- “Where are requirements stored?” cites `docs/requirements` and `FileStore` APIs.
- Responses include footnote links to files (and line ranges where feasible).
- Config switch to disable/enable RAG and choose model/provider.

## Success Criteria
- ≥ 95% of answers include at least one correct citation to a repo file when RAG is enabled.
- Index refresh completes within 5 minutes for current repo size (P95 ≤ 10 minutes).
- Zero inclusion of files outside the allowlist; denylist events are logged with file paths.
- Toggle reliably disables RAG with clear user feedback; no citations shown when disabled.

### Key Failure Modes

- Index build fails or corrupts → system reports failure with affected files; queries fall back to non-RAG answers with a caveat.
- Sensitive files accidentally included → denylist prevents inclusion; audit log flags any attempted inclusion.
- Citation extraction fails → answer still returned with a notice that citations are unavailable for that response.

## Risks and Open Questions
- Tradeoffs between recall and precision for small repos.
- Handling large binary diagrams; prefer text sources where possible.
- Footnote line-range extraction may require parser support.

related_requirements:
  - docs/requirements/REQ-003.md
