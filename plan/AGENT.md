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
- [x] Add local Whisper STT container orchestrated by Aspire
- [x] Add HTTP TTS container (optional) orchestrated by Aspire
- [ ] Add UI capture/playback and endpoint tests
- [ ] Define `IAgentService` and stub Azure Foundry Agent integration

## Next agent tasks (voice chat)
- [ ] Implement streaming transcription endpoint (server push partials)
- [ ] Add endpoint to stream TTS audio for immediate playback
- [ ] Wire agent chat turn-taking with STT->Agent->TTS cycle

