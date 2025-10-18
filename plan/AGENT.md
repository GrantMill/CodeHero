# Agent plan (small, testable tasks)

- [x] Propose new requirement template (create docs/requirements/REQ-001.md)
- [x] Implement MCP server (initialize, ping, fs/*) and stdio framing
- [x] Expose agents/list and agents/capabilities; add scribe/createRequirement tool
- [x] Add minimal Agents page and MCP client; support ping/list/capabilities/create
- [ ] Suggest checklist for "New Requirement" flow (fields, acceptance, traceability)
- [ ] Detect missing REQ tags in changed Blazor components (simple analyzer or CI script)
- [ ] Add chat-style Agents UI (conversation with scribe, map intents to tools)
- [ ] Add client/server timeouts + resilient lifecycle management

## Speech/Audio integration
- [x] Implement `ISpeechService` with Azure + Null implementations
- [x] Feature-flag endpoints and CI secret wiring
- [ ] Add UI capture/playback and endpoint tests
- [ ] Define `IAgentService` and stub Azure Foundry Agent integration

