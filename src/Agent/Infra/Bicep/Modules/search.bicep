var consts = loadJsonContent('../consts.json')

param namePrefix string

var location string = resourceGroup().location
var userIdentityName = '${namePrefix}${consts.managedIdentityNameSuffix}'

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' existing = {
  name: userIdentityName
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

output searchServiceName string = searchService.name
output searchServiceId string = searchService.id
output searchServiceEndpoint string = 'https://${searchService.name}.search.windows.net'
