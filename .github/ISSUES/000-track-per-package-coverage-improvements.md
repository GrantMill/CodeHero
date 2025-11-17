<!--
Title: Track per-package test coverage improvements
Labels: enhancement, ci, testing
Assignees: @maintainers
-->

# Track per-package coverage improvements

Goal: Incrementally improve test coverage per package so that per-package thresholds can become blocking checks in CI.

Current measured coverage (from latest CI run):

- **Overall:** 7.3%
- **CodeHero.Web:** 5.4%
- **CodeHero.ApiService:** 11.9%
- **CodeHero.ServiceDefaults:** 0.0%
- **CodeHero.AppHost:** 0.0%
- **CodeHero.Indexer:** (not measured in last run)

Why:

- We relaxed per-package checks to warn-only for the baseline PR to avoid blocking progress. This issue tracks work to raise coverage so checks can be made blocking again.

Checklist (incremental work items):

- [ ] T001 [P] Create a test-plan document `docs/testing/coverage-plan.md` describing areas to test and owned modules (`CodeHero.Web`, `CodeHero.ApiService`, `CodeHero.Indexer`, `CodeHero.ServiceDefaults`, `CodeHero.AppHost`).
- [ ] T002 [P] Add unit tests for `CodeHero.ServiceDefaults` core utilities (target: 30% coverage)
- [ ] T003 [P] Add unit tests for `CodeHero.Web` controllers and helpers (target: 25% coverage)
- [ ] T004 [P] Add integration tests for `CodeHero.ApiService` endpoints (target: 30% coverage)
- [ ] T005 [P] Add tests for `CodeHero.Indexer` indexer logic (target: 30% coverage)
- [ ] T006 [ ] Add CI job or scheduled workflow to run coverage-report and post results to this issue (optional)
- [ ] T007 [ ] Once a package reaches its target, update `.github/coverage-thresholds.json` to raise the threshold and make the check blocking for that package.

Acceptance criteria:

- Each package has a realistic per-package target documented and a set of tests that demonstrate the coverage increase.
- Per-package thresholds are raised and validated by CI runs.

Milestone: Coverage Improvement Sprint 1 (see `.github/MILESTONES/coverage-improvement.md`)

Notes:

- Keep per-package checks warn-only until we have at least one full sprint of improvements.
- Consider adding code owners and assigning test tasks to package maintainers.
