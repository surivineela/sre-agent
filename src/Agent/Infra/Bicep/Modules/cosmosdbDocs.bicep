var consts = loadJsonContent('../consts.json')

param namePrefix string

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

resource kv 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: '${namePrefix}${consts.kvNameSuffix}'
}

resource cosmosdbAccount 'Microsoft.DocumentDB/databaseAccounts@2021-04-15' = {
  name: '${namePrefix}${consts.cosmosDocDbAccountNameSuffix}'
  location: resourceGroup().location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: resourceGroup().location
        failoverPriority: 0
      }
    ]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-02-15-preview' = {
  name: consts.cosmosDocDbDatabaseName
  parent: cosmosdbAccount
  properties: {
    resource: {
      id: consts.cosmosDocDbDatabaseName
    }
  }
}

// Settings
resource cosmosAccountNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:CosmosDB:Docs:AccountEndpoint'
  parent: appConfig
  properties: {
    value: cosmosdbAccount.properties.documentEndpoint
  }
}

resource cosmosDatabaseSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:CosmosDB:Docs:Database'
  parent: appConfig
  properties: {
    value: consts.cosmosDocDbDatabaseName
  }
}

// Secret Settings
var cosmosApiKey = cosmosdbAccount.listKeys().primaryMasterKey
resource cosmosApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'cosmos-api-key'
  parent: kv
  properties: {
    value: cosmosApiKey
  }
}
resource cosmosApiKeySetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:CosmosDB:Docs:ApiKey'
  parent: appConfig
  properties: {
    value: string({uri: cosmosApiKeySecret.properties.secretUri})
    contentType: 'application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8'
  }
}
