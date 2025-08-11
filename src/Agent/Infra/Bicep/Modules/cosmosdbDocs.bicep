var consts = loadJsonContent('../consts.json')

param namePrefix string

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

resource cosmosdbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: '${namePrefix}${consts.cosmosDocDbAccountNameSuffix}'
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
    capabilities: [ { name: 'EnableNoSQLVectorSearch' } ]
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
  name: 'AppSettings:Core:Azure:CosmosDB:Docs:AccountName'
  parent: appConfig
  properties: {
    value: cosmosdbAccount.name
  }
}

resource cosmosDatabaseSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:CosmosDB:Docs:Database'
  parent: appConfig
  properties: {
    value: consts.cosmosDocDbDatabaseName
  }
}

// Assign data plane role
var roleAssignmentId = '/${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.DocumentDB/databaseAccounts/${namePrefix}${consts.cosmosDocDbAccountNameSuffix}/sqlRoleDefinitions/${consts.cosmosDBDataContributor}' // 'Contributor' role ID
resource roleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-04-15' = {
  name: guid(cosmosdbAccount.id, deployer().objectId, consts.cosmosDBDataContributor)
  parent: cosmosdbAccount
  properties: {
    principalId: deployer().objectId
    roleDefinitionId: roleAssignmentId
    scope: cosmosdbAccount.id
  }
}
