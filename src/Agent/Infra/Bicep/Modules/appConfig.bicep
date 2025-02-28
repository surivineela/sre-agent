var consts = loadJsonContent('../consts.json')

param namePrefix string

// Dependencies
module openaiModule 'openai.bicep' = {
  name: 'openaiDeployment'
  params: {
    namePrefix: namePrefix
  }
}

module cosmosdbGraphModule 'cosmosdbGraph.bicep' = {
  name: 'cosmosdbGraphDeployment'
  params: {
    namePrefix: namePrefix
  }
}

module keyVault 'kv.bicep' = {
  name: 'kvDeployment'
  params: {
    namePrefix: namePrefix
    cosmosApiKey: cosmosApiKey
    openaiApiKey: openaiApiKey
    appInsightsConnectionString: appInsightsConnectionString
  }
}

module monitoring 'monitoring.bicep' = {
  name: 'monitoringDeployment'
  params: {
    namePrefix: namePrefix
  }
}

module dts 'dts.bicep' = {
  name: 'dtsDeployment'
  params: {
    namePrefix: namePrefix
  }
}

// References
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2021-04-15' existing = {
  name: '${namePrefix}${consts.cosmosAccountNameSuffix}'
  dependsOn: [
    cosmosdbGraphModule
  ]
}

var cosmosApiKey = cosmosAccount.listKeys().primaryMasterKey

resource openAIAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: '${namePrefix}${consts.openAIAccountNameSuffix}'
  dependsOn: [
    openaiModule
  ]
}

var openaiEndpoint = openAIAccount.properties.endpoint
var openaiApiKey = openAIAccount.listKeys().key1

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: '${namePrefix}${consts.appInsightsNameSuffix}'
  dependsOn: [
    monitoring
  ]
}

var appInsightsConnectionString = appInsights.properties.ConnectionString

// Create Azure App Configuration
resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
  location: resourceGroup().location
  sku: {
    name: 'standard'
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

// Store non-sensitive values directly in App Config
resource cosmosAccountNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:CosmosDB:Graph:AccountName'
  parent: appConfig
  properties: {
    value: '${namePrefix}${consts.cosmosAccountNameSuffix}'
  }
}

resource cosmosDatabaseSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:CosmosDB:Graph:Database'
  parent: appConfig
  properties: {
    value: consts.cosmosDatabaseName
  }
}

resource cosmosCollectionSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:CosmosDB:Graph:Collection'
  parent: appConfig
  properties: {
    value: consts.cosmosGraphName
  }
}

resource openaiEndpointSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:OpenAI:Endpoint'
  parent: appConfig
  properties: {
    value: openaiEndpoint
  }
}

resource openaiLLMDeploymentNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:OpenAI:LLMDeploymentName'
  parent: appConfig
  properties: {
    value: '${namePrefix}-${consts.openAILLMModel}'
  }
}

resource openaiEmbeddingGeneratorDeploymentNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:OpenAI:EmbeddingGeneratorDeploymentName'
  parent: appConfig
  properties: {
    value: '${namePrefix}-${consts.openAIEmbeddingGeneratorModel}'
  }
}

// Store Key Vault references in App Config instead of raw values
resource cosmosApiKeySetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:CosmosDB:Graph:ApiKey'
  parent: appConfig
  properties: {
    value: string({uri: keyVault.outputs.cosmosApiKeyURI})
    contentType: 'application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8'
  }
}

resource openaiApiKeySetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:OpenAI:ApiKey'
  parent: appConfig
  properties: {
    value: string({uri: keyVault.outputs.openAIApiKeyURI})
    contentType: 'application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8'
  }
}

resource appInsightsConnectionStringSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:AppInsights:ConnectionString'
  parent: appConfig
  properties: {
    value: string({uri: keyVault.outputs.appInsightsConnectionStringURI})
    contentType: 'application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8'
  }
}
