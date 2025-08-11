var consts = loadJsonContent('../consts.json')

param namePrefix string

var location string = resourceGroup().location

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' existing = {
  name: '${namePrefix}${consts.managedIdentityNameSuffix}'
}

resource searchService 'Microsoft.Search/searchServices@2025-02-01-preview' = {
  name: '${namePrefix}-search'
  location: location
  sku: {
    name: 'standard'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    networkRuleSet: {
      ipRules: []
    }
    encryptionWithCmk: {
      enforcement: 'Unspecified'
    }
    disableLocalAuth: false
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
    semanticSearch: 'free'
  }
}

// Settings
resource searchEndpointSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:Indexing:SearchEndpoint'
  parent: appConfig
  properties: {
    value: 'https://${searchService.name}.search.windows.net'
  }
}

resource identitySetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:Indexing:ManagedIdentityResourceId'
  parent: appConfig
  properties: {
    value: identity.id
  }
}

// AgentMemory Settings
resource agentMemorySearchNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:AgentMemory:AzureAISearchName'
  parent: appConfig
  properties: {
    value: searchService.name
  }
}

resource agentMemoryManagedIdentityResourceIdSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:AgentMemory:ManagedIdentityResourceId'
  parent: appConfig
  properties: {
    value: identity.id
  }
}

resource agentMemorySearchIndexNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:AgentMemory:AzureAISearchIndexName'
  parent: appConfig
  properties: {
    value: '${namePrefix}-index'
  }
}

resource agentMemorySearchDataSourceNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:AgentMemory:AzureAISearchDataSourceName'
  parent: appConfig
  properties: {
    value: '${namePrefix}-docs-ds'
  }
}

resource agentMemorySearchSkillSetNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:AgentMemory:AzureAISearchSkillSetName'
  parent: appConfig
  properties: {
    value: '${namePrefix}-docs-skills'
  }
}

resource agentMemorySearchIndexerNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:AgentMemory:AzureAISearchIndexerName'
  parent: appConfig
  properties: {
    value: '${namePrefix}-docs-indexer'
  }
}

// local user access to search service for local deployments
resource searchIndexDataRoleAssignment 'Microsoft.Authorization/roleAssignments@2018-09-01-preview' = {
  name: guid(searchService.name, deployer().objectId, consts.SearchIndexDataContributorRoleDefinition)
  scope: searchService
  properties: {
    principalId: deployer().objectId
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      consts.SearchIndexDataContributorRoleDefinition
    )
  }
}

// local user access to search service for local deployments
resource searchDeployerRoleAssignment 'Microsoft.Authorization/roleAssignments@2018-09-01-preview' = {
  name: guid(searchService.name, deployer().objectId, consts.SearchServiceContributorRoleDefinition)
  scope: searchService
  properties: {
    principalId: deployer().objectId
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      consts.SearchServiceContributorRoleDefinition
    )
  }
}

output searchServiceName string = searchService.name
output searchServiceId string = searchService.id
output searchServiceEndpoint string = 'https://${searchService.name}.search.windows.net'
