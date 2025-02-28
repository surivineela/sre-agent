var consts = loadJsonContent('../consts.json')

param namePrefix string
@secure()
param cosmosApiKey string
@secure()
param openaiApiKey string
@secure()
param appInsightsConnectionString string

// Create Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' = {
  name: '${namePrefix}${consts.kvNameSuffix}'
  location: resourceGroup().location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: false
  }
}

// Store secrets in Key Vault
resource cosmosApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'cosmos-api-key'
  parent: keyVault
  properties: {
    value: cosmosApiKey
  }
}

resource openaiApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'openai-api-key'
  parent: keyVault
  properties: {
    value: openaiApiKey
  }
}

resource appInsightsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'app-insights-connection-string'
  parent: keyVault
  properties: {
    value: appInsightsConnectionString
  }
}

// Assign Data Plane Role
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, deployer().objectId, consts.keyVaultSecretsUser)
  scope: keyVault
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', consts.keyVaultSecretsUser)
    principalId: deployer().objectId
    principalType: 'User'
  }
}

output cosmosApiKeyURI string = cosmosApiKeySecret.properties.secretUri
output openAIApiKeyURI string = openaiApiKeySecret.properties.secretUri
output appInsightsConnectionStringURI string = appInsightsConnectionStringSecret.properties.secretUri
