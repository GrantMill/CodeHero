# CodeHero vision

CodeHero turns your Git repository into the source of truth for building software with human oversight and safe, assistive agents.

What it does
- Keep requirements, architecture notes, code, tests, and policies in sync via small, reviewable pull requests.
- Let agents propose changes; humans review and approve requirements, security, and architectural trade-offs.
- Enforce traceability (REQ-###) across docs, code, and tests through CI checks.
- Capture decisions and outcomes so the system improves using only your repository's history.

Principles
- Repo first: everything is a file; all automation flows through PRs.
- Safe increments: tiny diffs with clear rationale; always revertible.
- Human-in-the-loop: critical changes need explicit approval; routine ones can be automated when checks pass.
- Transparency: agent actions are visible, auditable, and policy-constrained.

Initial demo scope
- Blazor UI to view/edit Markdown requirements and architecture notes stored in the repo.
- Lightweight agent server to propose the next small task; a human applies it.
- CI that builds, runs tests, and enforces basic repo conventions.
