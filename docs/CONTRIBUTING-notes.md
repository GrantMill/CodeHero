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
