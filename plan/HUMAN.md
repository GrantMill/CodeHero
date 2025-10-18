- [x] Approve folder conventions and VISION.md
- [x] Create solution (CodeHero.sln) and add all projects
- [x] Land MCP server (initialize, ping, fs/*) and agents surface (scribe)
- [x] Add Architecture Mermaid rendering + TechChoices display
- [x] Add Agents page scaffold + MCP client
- [x] Review & merge MCP server + Agents UI PR
- [x] Approve any changes under docs/requirements and docs/architecture
- [x] Enable branch protection + required checks in GitHub (CI build/test on CodeHero.sln)
- [ ] Define requirement IDs policy (REQ-### format, uniqueness, authoring rules)

## Demo: Speech/Audio/Azure Foundry Agent
- [x] Add Azure Speech SDK service with TTS/STT
- [x] Add feature-gated endpoints `/api/tts` and `/api/stt`
- [x] Add null fallback to keep tests green without secrets
- [x] Add configuration keys and CI env wiring
- [ ] Add simple UI to record/play/push audio
- [ ] Add Foundry Agent service and demo flow (STT -> Agent -> TTS)
