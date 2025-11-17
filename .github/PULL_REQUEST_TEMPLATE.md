<!-- PR template to encourage traceability and checklist for baseline CI -->
## Summary

Related REQ(s):

Provide a short description of the change and link any related requirement IDs (e.g., `REQ-000`, `REQ-003`).

## Checklist
- [ ] CI green (build/tests) — verify `build.yml` checks pass
- [ ] Related REQ(s) referenced in this PR description
- [ ] Change aligns with `constitution.md` (quality gates, dependency policy)
- [ ] Requirements referenced in code/tests/docs where appropriate
- [ ] Docs updated if public behavior changes (add or update `docs/` files)

Optional notes for reviewers:
- Any special instructions or context to help reviewers
<!--
Fill out this template for code & doc changes. Link one or more REQ IDs when applicable.
-->

## Summary
Short description of the change.

**Related REQ(s):** REQ-____ (add one or more)

## Checklist
- [ ] I have added tests where applicable
- [ ] I have updated documentation or `docs/VISION.md` if needed
- [ ] CI passes (build/tests)
- [ ] This PR references the related `REQ-####` when changing requirements or specs

## Notes for reviewer
Add any important context or acceptance criteria.
<!-- Please include a short description of the change and the related REQ ID(s) -->

<!-- Reference requirements, e.g. REQ-000 -->
- Related REQ(s):

- [ ] CI: Build and tests pass (see checks)
- [ ] Linked requirement(s): REQ-####
- [ ] Reviewer assigned

Summary:

What to review / notes:

Test instructions (local):
- `dotnet test CodeHero.sln`

---
This repository tracks requirements using `REQ-####` IDs. Please reference the REQ in the PR title or body when applicable to help traceability.
