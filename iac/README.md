# Infrastructure as Code (Bicep)

This folder contains a minimal Azure baseline for CodeHero.

Resources provisioned by `bicep/main.bicep`:
- Storage account for artifacts and logs
- Log Analytics workspace
- Optional Azure AI Speech (Cognitive Services) (toggle via `createSpeech`, disabled in dev params). For local dev, you can run Whisper in Docker and do not need to provision Speech.

## Deploy (Azure CLI)

Prerequisites:
- Azure CLI logged in: `az login`
- Select subscription: `az account set --subscription <id>`

Create a resource group:

```
az group create -n rg-codehero-dev -l westeurope
```

Deploy:

```
az deployment group create \
  -g rg-codehero-dev \
  -f iac/bicep/main.bicep \
  -p @iac/bicep/parameters.dev.json
```

Outputs include the storage account name and Log Analytics workspace id. Speech endpoint is only present when `createSpeech=true`.

## Wire into the app

For local dev without Azure Speech, run Whisper in Docker and set `CodeHero.Web/appsettings.Development.json`:

```
"Speech": { "Endpoint": "http://localhost:18000" }
```

To use Azure Speech instead, deploy with `createSpeech=true` and set:

- `AzureAI:Speech:Key` and `AzureAI:Speech:Region` via `dotnet user-secrets` on `CodeHero.Web`.
Ensure `Features:EnableSpeechApi` is true in `appsettings.Development.json`.
