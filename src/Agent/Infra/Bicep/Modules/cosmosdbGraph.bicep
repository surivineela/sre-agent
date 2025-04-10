var consts = loadJsonContent('../consts.json')

param namePrefix string

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

resource kv 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: '${namePrefix}${consts.kvNameSuffix}'
}

resource cosmosdbAccount 'Microsoft.DocumentDB/databaseAccounts@2021-04-15' = {
  name: '${namePrefix}${consts.cosmosAccountNameSuffix}'
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
    capabilities: [
      {
        name: 'EnableGremlin'
      }
    ]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/gremlinDatabases@2022-05-15' = {
  name: consts.cosmosDatabaseName
  parent: cosmosdbAccount
  properties: {
    resource: {
      id: consts.cosmosDatabaseName
    }
  }
}

resource graph 'Microsoft.DocumentDb/databaseAccounts/gremlinDatabases/graphs@2022-05-15' = {
  name: consts.cosmosGraphName
  parent: database
  properties: {
    resource: {
      id: consts.cosmosGraphName
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/resourceType'
        ]
        kind: 'Hash'
      }
    }
    options: {
      autoscaleSettings: {
        maxThroughput: 5000
      }
    }
  }
}

// Settings
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

// Secret Settings
var cosmosApiKey = cosmosdbAccount.listKeys().primaryMasterKey
resource cosmosApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'graph-cosmos-api-key'
  parent: kv
  properties: {
    value: cosmosApiKey
  }
}
resource cosmosApiKeySetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:CosmosDB:Graph:ApiKey'
  parent: appConfig
  properties: {
    value: string({uri: cosmosApiKeySecret.properties.secretUri})
    contentType: 'application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8'
  }
}
