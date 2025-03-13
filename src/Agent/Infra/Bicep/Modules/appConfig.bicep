var consts = loadJsonContent('../consts.json')

param namePrefix string

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
}

// Dependencies
module openaiModule 'openai.bicep' = {
  name: 'openaiDeployment'
  params: {
    namePrefix: namePrefix
  }
  dependsOn: [
    appConfig
    keyVault
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

module dts 'dts.bicep' = {
  name: 'dtsDeployment'
  params: {
    namePrefix: namePrefix
  }
  dependsOn: [
    appConfig
    keyVault
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
