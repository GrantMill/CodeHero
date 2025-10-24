# EnableAgentApi

Purpose
- Enables agent-facing HTTP endpoints and services used by the UI to orchestrate repo work via an MCP server.
- Surfaces the Agents page (list tools/agents) and Scribe Chat features.

Status
- Enabled by default in appsettings.Development.json: Features:EnableAgentApi = true

How it is wired
- UI calls IAgentService (MCP Orchestrator) through HTTP endpoints in CodeHero.Web.
- IMcpClient talks to the process-based MCP server with tools:
 - fs/list, fs/readText, fs/writeText
 - scribe/createRequirement
- AgentsChat page provides continuous conversational mode wired to these APIs.

Configuration
- No secrets required for the local MCP flow.
- Optional Azure Foundry Agent stub exists (AzureAI:Foundry:*). If not configured it answers with a stub message.

Behavior notes
- Agents page allows: Ping, List requirements via fs/list, List agents, View tool schemas, Create Requirement.
- AgentsChat has approval dialog for destructive ops and streams audio replies via TTS when available.

Disable
- Set Features:EnableAgentApi = false and restart; agent HTTP endpoints won’t be available.
