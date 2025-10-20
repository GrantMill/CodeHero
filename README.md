# CodeHero

Build software from your repo as the source of truth with human oversight and assistive agents. Everything is files (docs, diagrams, code, plans), and changes flow through small, reviewable PRs.

Key features
- Blazor Server UI (.NET 10) to edit requirements, architecture, and plans; drive agents.
- Audio capture in-browser (MediaRecorder + PCM fallback) with streamed JS interop to avoid large SignalR payloads.
- Speech-to-Text via Azure AI Foundry (gpt-4o-transcribe-diarize). Optional Text-to-Speech via Azure AI Speech.
- Agents: process-based MCP server, Foundry agent stub; Human/Agent plan files for incremental delivery.
- Architecture as Mermaid in `docs/architecture`. IaC with Bicep and GitHub Actions.

Repo layout
- `CodeHero.Web` — Blazor app; endpoints `POST /api/stt`, `POST /api/tts`, `POST /api/agent/chat`
- `CodeHero.McpServer` — JSON‑RPC server used by the UI via `McpClient`
- `CodeHero.Tests` — tests
- `docs/architecture` — Mermaid: overview, sequences, state, use-cases
- `docs/requirements` — REQ‑### markdown
- `plan/HUMAN.md`, `plan/AGENT.md` — work plans
- `iac/bicep` — `main.bicep` (storage+logs), `parameters.dev.json`
- `.github/workflows` — `build.yml`, `iac.yml`

Run locally
1) Prereqs: .NET 10 SDK; modern Edge/Chrome/Firefox
2) Build/run: `dotnet build` then `dotnet run --project CodeHero.AppHost`
3) Browse: open the shown https://localhost:xxxx
4) Try: Plan/Requirements/Architecture pages; Agents and Scribe Chat

Configure Foundry STT
- In Azure AI Foundry create or locate a deployment `gpt-4o-transcribe-diarize`
- Set secrets for `CodeHero.Web`:
  - `dotnet user-secrets set "AzureAI:Foundry:Endpoint" "https://<workspace>.<region>.models.ai.azure.com" --project CodeHero.Web`
  - `dotnet user-secrets set "AzureAI:Foundry:Key" "<key>" --project CodeHero.Web`
  - `dotnet user-secrets set "AzureAI:Foundry:TranscribeDeployment" "gpt-4o-transcribe-diarize" --project CodeHero.Web`
  - (optional) `dotnet user-secrets set "AzureAI:Foundry:ApiVersion" "2024-08-01-preview" --project CodeHero.Web`
- Ensure `Features:EnableSpeechApi` = true in `appsettings.Development.json`

Optional TTS (Azure Speech)
- `dotnet user-secrets set "AzureAI:Speech:Key" "<key>" --project CodeHero.Web`
- `dotnet user-secrets set "AzureAI:Speech:Region" "<region>" --project CodeHero.Web`
- With both Foundry and Speech, the app uses Foundry for STT and Speech for TTS; otherwise TTS returns a short silent WAV.

Audio demo
- Scribe Chat → Start (allow mic) → speak → Stop → Transcribe. Speak last plays TTS when configured.

Diagrams (Mermaid)
- `docs/architecture/overview.mmd` — system overview
- `docs/architecture/sequence-stt.mmd` — recording → STT
- `docs/architecture/sequence-tts.mmd` — TTS
- `docs/architecture/sequence-agent.mmd` — agent chat
- `docs/architecture/state-diagram.mmd` — UI state
- `docs/architecture/use-cases.mmd` — scenarios

IaC
- Bicep in `iac/bicep/main.bicep`; Speech disabled in `parameters.dev.json` (use existing Foundry).
- GitHub Actions `.github/workflows/iac.yml` validates on PRs; what‑if/deploy via workflow_dispatch with OIDC.

Troubleshooting
- Mic ok but empty transcript → check Foundry endpoint/key and deployment name; `Features:EnableSpeechApi` must be true.
- Reconnects during audio → hard refresh; the app streams WAV blobs to avoid large SignalR messages.
- No MediaRecorder → PCM fallback is used; use a modern Chromium/Firefox build.

License: MIT
