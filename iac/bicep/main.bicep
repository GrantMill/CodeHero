@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Name prefix used for resources (lowercase, letters/numbers only)')
param namePrefix string = 'codehero'

@description('Apply common tags to all resources')
param tags object = {
  project: 'CodeHero'
}

@description('Provision Azure AI Speech (Cognitive Services)')
param createSpeech bool = false

@description('SKU for Speech resource (e.g., S0, S1)')
@allowed([ 'S0' 'S1' ])
param speechSku string = 'S0'

@description('Storage SKU for artifact storage')
@allowed([ 'Standard_LRS' 'Standard_GRS' 'Standard_RAGRS' 'Standard_ZRS' 'Premium_LRS' ])
param storageSku string = 'Standard_LRS'

@description('Log Analytics workspace SKU')
@allowed([ 'PerGB2018' ])
param workspaceSku string = 'PerGB2018'

// Storage account for artifacts and logs
resource sa 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: toLower('${namePrefix}sa${uniqueString(resourceGroup().id)}')
  location: location
  sku: {
    name: storageSku
  }
  kind: 'StorageV2'
  tags: tags
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

// Log Analytics workspace for diagnostics
resource law 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${namePrefix}-logs'
  location: location
  tags: tags
  properties: {
    retentionInDays: 30
    features: {
      searchVersion: 2
    }
    workspaceCapping: {
      dailyQuotaGb: 0.5
    }
  }
  sku: {
    name: workspaceSku
  }
}

// Optional Azure AI Speech (Cognitive Services)
@description('Azure AI Speech service (optional)')
resource speech 'Microsoft.CognitiveServices/accounts@2023-10-01' = if (createSpeech) {
  name: '${namePrefix}-speech'
  location: location
  kind: 'SpeechServices'
  sku: {
    name: speechSku
  }
  tags: union(tags, { service: 'speech' })
  properties: {
    publicNetworkAccess: 'Enabled'
    customSubDomainName: '${namePrefix}-speech-${uniqueString(resourceGroup().id)}'
  }
}

output storageAccountName string = sa.name
output logAnalyticsWorkspaceId string = law.id
@secure()
output speechEndpoint string = createSpeech ? speech.properties.endpoint : ''
