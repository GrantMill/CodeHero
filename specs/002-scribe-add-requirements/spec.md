# Agentic Scribe: Conversational Requirements Capture

Metadata
- source_req: REQ-001
- generator: SpecKit.speckit.specify (GitHub Copilot)
- version: 1.0.0
- timestamp: 2025-11-15T00:00:00Z

- Feature Branch: `002-scribe-add-requirements`
- Created: 2025-11-15
- Status: Draft

## Summary
Enable a microphone-driven Scribe that captures requirements conversationally, grounds answers on architecture and code, and supports basic UI navigation hints. This reduces typing friction and keeps requirements aligned with the repo.

## Goals
- Allow users to create and update requirement docs via voice input.
- Ground answers on repo architecture/code to reduce ambiguity.
- Provide spoken responses for confirmations and clarifications.
- Lay groundwork for UI navigability by voice (basic intents only).

## Non-Goals
- Full UI control and complex navigation across the app.
- Advanced NLU beyond capturing clear requirement statements and metadata.
- Production-grade multi-language transcription models.

## User Personas and Primary User Flows
- Product Owner: dictates a new requirement and confirms acceptance criteria.
- Developer: asks scribe to read/locate requirements and architecture notes.

Primary flows
- Create a requirement  capture title/body/acceptance  write `docs/requirements/REQ-###.md` (new ID policy TBD).
- Read requirement  scribe reads and cites doc sections; can answer clarifying questions grounded in repo.

## Domain and Technical Constraints
- Repo-first and PR-based edits; scribe proposes changes, user reviews in PR.
- Voice capture must be opt-in and clearly indicated when recording.
- Use existing architecture constraints in `docs/architecture/TechChoices.md`; avoid locking tool/model choices.
- Constitution: `constitution.md` governs agent boundaries, privacy, dependency policy, and CI gates.

## Integration Points
- STT/TTS endpoints (local dev containers or cloud per TechChoices).
- File operations for `docs/requirements/**` under allowed paths only.
- Grounding sources: `docs/architecture/**`, `CodeHero.*` public APIs, and `docs/requirements/**`.

## Observability and Resilience Requirements
- Log scribe sessions with timestamps and anonymized event types (no raw audio in logs).
- Record success/failure of writes and referenced files for traceability.
- Graceful fallback: if transcription fails, prompt user to repeat or switch to keyboard.

## Security, Privacy, and Data Handling Requirements
- Microphone permission must be explicit; visible recording state.
- Avoid storing raw audio by default; if stored, short retention and user consent required.
- Only modify requirement files under `docs/requirements/`; guardrail for path traversal.

## Acceptance Criteria
- Creating a requirement via voice produces a properly formatted Markdown file with title and acceptance list.
- Scribe answers where is X requirement? grounded with citations to the file path.
- Scribe can read back an existing requirement and acceptance list via TTS.
- UI navigation intent is acknowledged verbally; deep navigation beyond scope.

## Success Criteria
- 90% of dictated requirements include title and 3 acceptance bullets without manual correction.
- 95% of scribe read operations cite the correct file path.
- Time to draft a requirement via voice  3 minutes for typical length (P95  5 minutes).
- Zero writes outside `docs/requirements/**` (enforced by guardrails).

### Key Failure Modes

- Transcription fails or low confidence  user is prompted to repeat or switch to keyboard.
- Write attempt outside `docs/requirements/` or path traversal detected  operation is blocked and user informed.
- TTS unavailable  fallback to text-only response with instruction to enable audio.

## Risks and Open Questions
- REQ numbering policy for new files (central allocator vs next free ID) needs team decision.
- Accessibility considerations for users without microphone access must be addressed.
- STT/TTS model/provider selection remains flexible; define minimal quality thresholds.

related_requirements:
 - docs/requirements/REQ-001.md
