var consts = loadJsonContent('../consts.json')

param namePrefix string
param useOldOpenAIName bool

// Create Azure App Configuration
resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
  location: resourceGroup().location
  sku: {
    name: 'standard'
  }
}

module keyVault 'kv.bicep' = {
  name: 'kvDeployment'
  params: {
    namePrefix: namePrefix
  }
  dependsOn: [
    appConfig
    identity
  ]
}

module openaiModule 'openai.bicep' = {
  name: 'openaiDeployment'
  params: {
    namePrefix: namePrefix
    useOldOpenAIName: useOldOpenAIName
  }
  dependsOn: [
    appConfig
    keyVault
    identity
  ]
}

module cosmosdbGraphModule 'cosmosdbGraph.bicep' = {
  name: 'cosmosdbGraphDeployment'
  params: {
    namePrefix: namePrefix
  }
  dependsOn: [
    appConfig
    keyVault
  ]
}

module cosmosDbDocModule 'cosmosdbDocs.bicep' = {
  name: 'comsosdbDocsDeployment'
  params: {
    namePrefix: namePrefix
  }
  dependsOn: [
    appConfig
    keyVault
  ]
}

module monitoring 'monitoring.bicep' = {
  name: 'monitoringDeployment'
  params: {
    namePrefix: namePrefix
  }
  dependsOn: [
    appConfig
    keyVault
  ]
}

module search 'search.bicep' = {
  name: 'searchDeployment'
  params: {
    namePrefix: namePrefix
  }
  dependsOn: [
    appConfig
    identity
  ]
}

module storage 'storage.bicep' = {
  name: 'storageDeployment'
  params: {
    namePrefix: namePrefix
  }
  dependsOn: [
    appConfig
    identity
  ]
}

module identity 'identity.bicep' = {
  name: 'identityDeployment'
  params: {
    namePrefix: namePrefix
  }
  dependsOn: [
    appConfig
  ]
}

// General settings
resource subIdSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:Crawler:SubscriptionId'
  parent: appConfig
  properties: {
    value: subscription().subscriptionId
  }
}

resource tenantIdSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:Crawler:TenantId'
  parent: appConfig
  properties: {
    value: subscription().tenantId
  }
}

// Assign Data Plane Role
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appConfig.id, deployer().objectId, consts.appConfigurationDataReader) // 'Contributor' role ID
  scope: appConfig
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', consts.appConfigurationDataReader)
    principalId: deployer().objectId
    principalType: 'User'
  }
}

// In production the Azure Managed Grafana should be customer provided.
module azureManagedGrafana 'azureManagedGrafana.bicep' = {
  name: 'azureManagedGrafanaDeployment'
  params: {
    namePrefix: namePrefix
    location: resourceGroup().location
  }
}

// In production, azure monitor workspace(managed prometheus) should be customer provided.
module azureMonitorWorkspace 'azureMonitorWorkspace.bicep' = {
  name: 'azureMonitorWorkspaceDeployment'
  params: {
    namePrefix: namePrefix
    location: resourceGroup().location
    grafanaManagedIdentityId: azureManagedGrafana.outputs.managedIdentityId
  }
}
