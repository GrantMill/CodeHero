# Tech choices (initial)

- UI: Blazor Server (.NET 10 via Aspire)
- Composition: .NET Aspire AppHost + ServiceDefaults
- Agents runtime: MCP (console over stdio) in a background project
- Orchestration: start simple; later add Microsoft Agent Framework
- Models: Azure AI Foundry (Azure OpenAI) later via DI
- CI: GitHub Actions build + tests + basic traceability

Current progress
- Minimal MCP server implemented (initialize, ping, fs list/read/write) with stdio framing.
- Agents surface added: agents/list, agents/capabilities; scribe tool to create requirement files.
- Blazor UI includes Requirements/Architecture/Plan/Agents pages; Agents page can ping, list agents, view capabilities, and create requirements.
