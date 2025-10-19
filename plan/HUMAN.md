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

## Wire Azure Foundry STT (gpt-4o-transcribe-diarize)
- [ ] In Azure AI Foundry (project/workspace), create or locate a deployment of `gpt-4o-transcribe-diarize`.
  - Name it clearly, e.g. `gpt-4o-transcribe-diarize` (note exact deployment name).
  - Ensure the project has an endpoint and API key access enabled for your account/app.
- [ ] Capture the endpoint and key:
  - Endpoint (similar to `https://<workspace>.<region>.models.ai.azure.com`)
  - Key (Foundry project key)
- [ ] Set local secrets for CodeHero.Web:
  - `dotnet user-secrets set "AzureAI:Foundry:Endpoint" "<endpoint>" --project CodeHero.Web`
  - `dotnet user-secrets set "AzureAI:Foundry:Key" "<key>" --project CodeHero.Web`
  - `dotnet user-secrets set "AzureAI:Foundry:TranscribeDeployment" "gpt-4o-transcribe-diarize" --project CodeHero.Web`
  - (Optional) `dotnet user-secrets set "AzureAI:Foundry:ApiVersion" "2024-08-01-preview" --project CodeHero.Web`
- [ ] Ensure `Features:EnableSpeechApi` is `true` in `appsettings.Development.json`.
- [ ] Restart the app. In Agents Chat:
  - Record ? Stop ? Transcribe. Expect text from Foundry.
  - TTS will produce a short silent WAV (no Azure Speech required). If TTS is needed later, add Speech key+region.
