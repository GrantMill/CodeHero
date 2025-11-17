# Contributing Notes — CodeHero

This note describes the minimal contribution requirements for the bootstrap baseline.

Quick checklist for contributors
- **CI green**: All PRs must run the repository CI (`.github/workflows/build.yml`). Make sure the build and tests pass on your PR before requesting review.
- **Traceability**: If your change touches requirements or specs (files under `docs/requirements/**` or `specs/**`), include a `Related REQ(s):` line in your PR description (e.g., `Related REQ(s): REQ-000`). A lightweight validation check runs on PRs and will block if requirement/spec files change without a REQ reference.
- **Constitution alignment**: Changes should follow the `constitution.md` governance. For the bootstrap baseline a temporary exception applies to coverage thresholds — see `docs/issues/001-temp-coverage-exception.md`.
- **Docs**: If your change alters public behavior, add or update documentation under `docs/`.
- **Small, focused PRs**: Keep diffs small and reviewable. Prefer many small PRs over one large change.

Branch and PR guidance
- Create topic branches from `master` with a short, descriptive name: `NNN-short-name` (e.g., `006-fix-readme-typo`).
- Use the PR template (the repository's pull request template prompts for `Related REQ(s):`) and fill it in.
- Add reviewers as appropriate and reference the related REQ(s) in the PR body.

CI artifacts and observability
- Test results and coverage artifacts are archived by CI in the `TestResults/` folder and uploaded as GitHub Action artifacts. After CI completes, reviewers can download the `test-results` artifact from the PR checks.

Where to get help
- If unsure which REQ to reference, ask maintainers and link to the related spec under `specs/`.
- For CI failures, attach the failing job logs and the `test-results` artifacts when filing an issue or asking for help.

This file was added as part of the REQ-000 bootstrap baseline follow-ups.
# CodeHero contributing notes (MCP + Scribe)

## Scribe workflow (MVP)
- The MCP server is a console app (stdio JSON-RPC) with methods: initialize, ping, fs/list, fs/readText, fs/writeText.
- Agents surface exposes a single agent: `scribe` with tool `scribe/createRequirement`.
- The Blazor Agents page provides:
  - Ping
  - List requirements via fs/list
  - List agents and view capabilities
  - Create a requirement (id/title) via `scribe/createRequirement`.

## Running locally
- Build the solution: `dotnet build CodeHero.sln`
- Run tests: `dotnet test`
- Launch AppHost for the full Aspire experience.

## Next steps
- Add chat-style agent UX and a lightweight intent mapper.
- Add a new-requirement checklist wizard.
- Add an analyzer or CI script to flag missing REQ tags in modified components.
