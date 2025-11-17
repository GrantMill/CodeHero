# Implementation Plan — 001-bootstrap-baseline

Goal: implement the minimal, auditable baseline required by the spec `specs/001-bootstrap-baseline/spec.md` (REQ-000). Only tasks required to satisfy the Success Criteria are included.

Assumptions and constraints:
- Work is confined to repository files; do not change global org settings or external services in this plan.
- Treat partially implemented features elsewhere as out-of-scope unless explicitly required by REQ-000.
- This plan produces small, reviewable PRs that reference `REQ-000`.

Checklist (numbered tasks):

1. Verify `docs/VISION.md` exists and meets acceptance criteria
   - Outcome: `docs/VISION.md` contains a concise vision and principles per the spec.
   - Steps:
     - Open `docs/VISION.md` and confirm presence of: repo-first principle, human-in-the-loop, CI requirement, and demo scope.
     - If missing or malformed, create/repair `docs/VISION.md` with the content from the spec and add front-matter comments linking to `REQ-000`.
   - Verification: PR with `docs/VISION.md` present and a reviewer acceptance.

2. Ensure single backlog `plan/BACKLOG.md` exists and distinguishes human vs agent tasks
   - Outcome: `plan/BACKLOG.md` present and has clear sections/headings for Human Tasks and Agent Tasks.
   - Steps:
     - Inspect `plan/BACKLOG.md` and confirm headings or markers that separate human [H] vs agent [A] items.
     - Add a short README comment at top documenting the format and how to add items (REQ tag requirement optional).
   - Verification: PR updating `plan/BACKLOG.md` (if needed) reviewed and merged.

3. Add a CI workflow that builds the solution and runs tests on every PR
   - Outcome: Every PR triggers a GitHub Actions workflow that runs `dotnet restore`, `dotnet build`, and `dotnet test`, publishes test results, and fails the check on test failures.
   - Steps:
     - Add or update `.github/workflows/bootstrap-baseline.yml` (or `build.yml`) with a single job targeting the repository's SDK that:
       - Runs on `pull_request` and `push` to branches containing `feature/*` and `master`/`main`.
       - Restores, builds, and runs tests for the solution (`CodeHero.sln`).
       - Produces and uploads `TestResults` artifacts (TRX and Cobertura/XML where possible).
       - Exits non-zero on test failures so GitHub marks the check as failed.
     - Keep matrix small (one OS) to meet the median CI duration requirement; optimize later if needed.
   - Verification: Create a test PR that changes only a Markdown file; confirm the workflow runs and shows test results. Create a second PR that intentionally fails a test and confirm the check fails and blocks merge.

4. Add a PR template that requires a `REQ-####` reference when applicable
   - Outcome: `.github/PULL_REQUEST_TEMPLATE.md` exists and instructs contributors to reference REQ IDs and link to specs.
   - Steps:
     - Create `.github/PULL_REQUEST_TEMPLATE.md` containing a short checklist including: link to `REQ-000` when bootstrapping, a field `Related REQ(s): REQ-####`, and a small section for acceptance/testing notes.
   - Verification: Open a PR using the template and confirm the template appears in the PR body.

5. Add a CI traceability check that validates PR body references a `REQ-####` when the change touches requirement/spec files
   - Outcome: A lightweight CI step that examines the PR body (and changed files) and issues a failing check only when changes touch `docs/requirements` or `specs/**` but the PR body contains no `REQ-####` token.
   - Steps:
     - Implement a small script (e.g., `.github/scripts/validate_req_reference.py` or bash) that:
       - Reads changed files from the GitHub event payload.
       - If changed files include `docs/requirements` or `specs/**`, scan the PR body for `REQ-\d{3,}` and fail if none found.
     - Wire this script into the PR workflow as an early step. Keep the logic conservative (only fail when requirement-related files change) to reduce false positives.
   - Verification: PR that edits a requirement/spec without adding a REQ reference should fail the traceability check; PR that includes a `REQ-###` should pass.

6. Ensure test results and logs are archived on PR runs and easily accessible
   - Outcome: CI uploads `TestResults` and a minimal text summary; logs contain links to artifacts.
   - Steps:
     - In the CI workflow job, upload `TestResults/**` and `coverage-report/**` as artifacts.
     - Ensure the `dotnet test` command uses `--logger:'trx;LogFileName=TestResults.trx'` or equivalent so PR checks include test summaries.
   - Verification: After a PR run, the Actions UI shows artifacts and the test summary in the logs.

7. Document the merge gating and branch protection steps for maintainers
   - Outcome: A short `docs/CONTRIBUTION.md` section (or repo admin note) explaining required checks: CI green, traceability, and reviewer approval.
   - Steps:
     - Add or update `docs/CONTRIBUTING-notes.md` with the exact checks that must pass and instruct repo admins to enable branch protection requiring the workflow(s).
   - Verification: PR merges are blocked by branch protection when enabled by maintainers (this is a manual admin step; document it here).

8. End-to-end validation and acceptance testing (deliverable)
   - Outcome: Demonstrable evidence that the Success Criteria are met.
   - Steps:
     - Create a small validation PR that makes a non-functional change (for example, update `README.md` or fix a minor typo in `docs/VISION.md`) and reference `REQ-000` in the PR body.
     - Confirm the CI workflow runs on the PR and that `dotnet restore`, `dotnet build`, and `dotnet test` complete successfully; confirm `TestResults` artifacts are uploaded and visible in the Actions UI.
     - If the PR edits requirement/spec files, confirm the traceability check fails if the PR body lacks a `REQ-###` reference and passes when one is present.
     - Record CI runtime metrics for the validation PR (median and P95); if median > 10 minutes identify the slow step and propose an optimization (optimization work is out of scope for this plan unless trivial).
   - Verification Criteria (to mark plan complete):
     - Validation PR executes CI successfully and uploads artifacts.
     - PR template is available for new PRs and includes `Related REQ(s):` field.
     - Traceability check behaves as specified (fails when required and passes when present).

Deliverables
- `specs/001-bootstrap-baseline/plan.md` (this file)
- `.github/workflows/bootstrap-baseline.yml` (or updated `build.yml`) implementing build/test on PRs
- `.github/PULL_REQUEST_TEMPLATE.md` instructing REQ references
- Optional: `.github/scripts/validate_req_reference.py` (script used by CI)
- Confirmed `docs/VISION.md` and `plan/BACKLOG.md` (or minimal updates applied)

Notes and next steps (post-acceptance)
- After the baseline is accepted and merged, schedule follow-ups to:
  - Add coverage collection and reporting (Cobertura/ReportGenerator) and surface coverage in PR comments.
  - Transition per-package coverage checks from warn-only to blocking as packages meet targets.
  - Add scheduled CI or monitoring to collect CI duration metrics and act on P95 results.

References
- `specs/001-bootstrap-baseline/spec.md`
- `plan/BACKLOG.md` (REQ-000 entry)
- `docs/VISION.md`
