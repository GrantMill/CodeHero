# Infrastructure as Code (Bicep)

This folder contains a minimal Azure baseline for CodeHero.

Resources provisioned by `bicep/main.bicep`:
- Storage account for artifacts and logs
- Log Analytics workspace
- Optional Azure AI Speech (Cognitive Services) (toggle via `createSpeech`)

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

Outputs include the Speech endpoint (if enabled), storage account name, and Log Analytics workspace id.

## Wire into the app

After deploying with `createSpeech=true`, set these secrets for CodeHero.Web:

- AzureAI:Speech:Key ? from the Speech resource (Keys and Endpoint)
- AzureAI:Speech:Region ? region of the Speech resource (e.g., `westeurope`)

Use `dotnet user-secrets`:

```
dotnet user-secrets set "AzureAI:Speech:Key" "<key>" --project CodeHero.Web

dotnet user-secrets set "AzureAI:Speech:Region" "westeurope" --project CodeHero.Web
```

Ensure `Features:EnableSpeechApi` is true in `appsettings.Development.json`.
