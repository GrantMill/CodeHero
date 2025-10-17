# Tech choices (initial)

- UI: Blazor Server (.NET 10 via Aspire)
- Composition: .NET Aspire AppHost + ServiceDefaults
- Agents runtime: MCP (console over stdio) in a background project
- Orchestration: start simple; later add Microsoft Agent Framework
- Models: Azure AI Foundry (Azure OpenAI) later via DI
- CI: GitHub Actions build + tests + basic traceability
