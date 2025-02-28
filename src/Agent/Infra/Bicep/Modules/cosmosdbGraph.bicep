var consts = loadJsonContent('../consts.json')

param namePrefix string

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
  }
}
