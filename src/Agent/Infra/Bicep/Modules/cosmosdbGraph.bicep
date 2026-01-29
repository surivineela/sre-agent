var consts = loadJsonContent('../consts.json')

param namePrefix string

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

// Reference the user-assigned managed identity used by the application
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' existing = {
  name: '${namePrefix}${consts.managedIdentityNameSuffix}'
}

resource cosmosdbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: '${namePrefix}${consts.cosmosAccountNameSuffix}'
  location: resourceGroup().location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    disableLocalAuth: true
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

// Assign data plane role for managed identity
// Using Cosmos DB Built-in Data Contributor role for Gremlin API access
var roleDefinitionId = '/${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.DocumentDB/databaseAccounts/${namePrefix}${consts.cosmosAccountNameSuffix}/sqlRoleDefinitions/${consts.cosmosDBDataContributor}'

// User-assigned managed identity access for Azure deployments (application identity)
resource identityRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-04-15' = {
  name: guid(cosmosdbAccount.id, identity.id, consts.cosmosDBDataContributor)
  parent: cosmosdbAccount
  properties: {
    principalId: identity.properties.principalId
    roleDefinitionId: roleDefinitionId
    scope: cosmosdbAccount.id
  }
}

// Local user access for local development deployments
resource deployerRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-04-15' = {
  name: guid(cosmosdbAccount.id, deployer().objectId, consts.cosmosDBDataContributor)
  parent: cosmosdbAccount
  properties: {
    principalId: deployer().objectId
    roleDefinitionId: roleDefinitionId
    scope: cosmosdbAccount.id
  }
}
