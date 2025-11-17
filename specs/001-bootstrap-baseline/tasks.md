# Tasks — 001-bootstrap-baseline

Checklist: sequential, small, reviewable tasks required to satisfy the Success Criteria in `specs/001-bootstrap-baseline/spec.md` (REQ-000).

- [ ] T001 Verify `docs/VISION.md` content and add front-matter if needed — edit `docs/VISION.md` (ensure repo-first, human-in-the-loop, CI requirement, demo scope). [Spec: Goals; Acceptance Criteria]
- [ ] T002 Confirm `plan/BACKLOG.md` differentiates Human vs Agent tasks and add top README comment if missing — edit `plan/BACKLOG.md`. [Spec: Goals; Acceptance Criteria]
- [x] T003 Add CI workflow file to build & test PRs — create/modify `.github/workflows/bootstrap-baseline.yml` to run `dotnet restore`, `dotnet build`, `dotnet test` and upload `TestResults/**` artifacts. [Spec: Goals; Acceptance Criteria; Observability]
- [x] T004 Ensure CI workflow runs on `pull_request` (and `push` to `feature/*`, `master`/`main`) and keep matrix to a single OS to meet timing goals — update workflow triggers and jobs in `.github/workflows/bootstrap-baseline.yml`. [Spec: Success Criteria; Observability]
- [ ] T005 Add PR template that prompts for `Related REQ(s):` and acceptance notes — create `.github/PULL_REQUEST_TEMPLATE.md`. [Spec: Traceability; Acceptance Criteria]
- [ ] T006 Implement lightweight traceability check (warn/fail) run in CI: add `.github/scripts/validate_req_reference.py` (or bash) and wire it as an early workflow step; script fails only when requirement/spec files are changed but PR body contains no `REQ-\d{3,}`. [Spec: Traceability; Acceptance Criteria]
- [x] T007 Ensure test results & coverage artifacts are uploaded by the CI job: confirm `dotnet test` uses a TRX logger and upload `TestResults/**` and `coverage-report/**` as Actions artifacts — update `.github/workflows/bootstrap-baseline.yml` step(s). [Spec: Observability; Acceptance Criteria]
- [ ] T008 Add a short contributor note documenting required checks and branch protection guidance — edit `docs/CONTRIBUTING-notes.md` (or add `docs/CONTRIBUTION.md`) describing: CI green, traceability, and reviewer approval. [Spec: Domain and Technical Constraints]
- [ ] T009 Create a small validation PR that references `REQ-000` and demonstrates CI behavior (non-functional change such as README typo) — PR body must reference `REQ-000` and prove artifacts are uploaded. [Spec: Acceptance Criteria; Success Criteria]
- [ ] T010 Record validation results and close the plan: capture CI timing (median, P95) for the validation PR and file a short follow-up note under `docs/` or an issue recommending optimizations if median runtime > 10 minutes. [Spec: Success Criteria; Observability]

Notes
- Each task should be a small PR that references `REQ-000` in the PR description when relevant.
- Keep changes minimal and focused: do not alter unrelated backlog items or other specs.
- After tasks are merged, schedule follow-ups to add coverage collection/reporting and to migrate per-package checks from warn-only to blocking as packages meet targets.

- Coverage note: Coverage thresholds are temporarily set below the Constitution requirements and per-package checks are currently warn-only; this temporary exception is documented in `docs/issues/001-temp-coverage-exception.md` and governs the ramp plan.
