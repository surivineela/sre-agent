var consts = loadJsonContent('../consts.json')

param namePrefix string

var globalUniqueSuffix string = uniqueString(resourceGroup().id)
var storageAccountName = '${take(namePrefix, 5)}${globalUniqueSuffix}'
var userIdentityName = '${namePrefix}${consts.managedIdentityNameSuffix}'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' existing = {
  name: userIdentityName
}

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: resourceGroup().location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    defaultToOAuthAuthentication: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    isLocalUserEnabled: false
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
      ipRules: []
      virtualNetworkRules: []
    }
  }
}
resource dataConnectorContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccount.name}/default/dataconnectors'
  properties: {
    publicAccess: 'None'
  }
}

resource agentDocumentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccount.name}/default/agent-documents'
  properties: {
    publicAccess: 'None'
  }
}

// Settings
resource storageResourceIdSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:Indexing:BlobStorageResourceId'
  parent: appConfig
  properties: {
    value: storageAccount.id
  }
}

resource blobEndpointSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:Storage:BlobEndpoint'
  parent: appConfig
  properties: {
    value: storageAccount.properties.primaryEndpoints.blob
  }
}

// AgentMemory Settings
resource agentMemoryStorageAccountNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:AgentMemory:StorageAccountName'
  parent: appConfig
  properties: {
    value: storageAccount.name
  }
}

resource agentMemoryBlobStorageResourceIdSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:AgentMemory:BlobStorageResourceId'
  parent: appConfig
  properties: {
    value: storageAccount.id
  }
}

resource agentMemoryBlobStorageContainerNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:AgentMemory:BlobStorageContainerName'
  parent: appConfig
  properties: {
    value: 'agent-documents'
  }
}

// user-assigned identity access to blob storage for Azure deployments
resource storageIdentityRoleAssignment 'Microsoft.Authorization/roleAssignments@2018-09-01-preview' = {
  name: guid(storageAccountName, identity.name, consts.StorageBlobDataContributorRoleDefinition)
  scope: storageAccount
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      consts.StorageBlobDataContributorRoleDefinition
    )
  }
}

// local user access to blob storage for local deployments
resource storageDeployerRoleAssignment 'Microsoft.Authorization/roleAssignments@2018-09-01-preview' = {
  name: guid(storageAccountName, deployer().objectId, consts.StorageBlobDataContributorRoleDefinition)
  scope: storageAccount
  properties: {
    principalId: deployer().objectId
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      consts.StorageBlobDataContributorRoleDefinition
    )
  }
}
