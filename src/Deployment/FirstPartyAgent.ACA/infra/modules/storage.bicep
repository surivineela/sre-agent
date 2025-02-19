param accountName string
param queueName string
param fileshareName string
param tags object
param userAssignedIdentityId string
param userAssignedIdentityPrincipalId string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: accountName
  location: resourceGroup().location
  sku: {
    name: 'Standard_LRS'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  kind: 'StorageV2'
  tags: tags
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
  properties: {
    cors: {
      corsRules: [
        {
          allowedOrigins: ['*']
          allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
          allowedHeaders: ['*']
          exposedHeaders: ['*']
          maxAgeInSeconds: 3600
        }
      ]
    }
  }
}

resource queue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  name: queueName
  parent: queueService
  properties: {}
}

resource fileshareService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
  properties:{
    protocolSettings:{
      smb: {}
    }
  }
}

resource fileshare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  name: fileshareName
  parent: fileshareService
  properties:{}
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, 'Storage Queue Data Contributor')
  scope: storageAccount
  properties: {
    principalId: userAssignedIdentityPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88') // Storage Queue Data Contributor
  }
}

output storageAccountName string = storageAccount.name
output queueName string = queue.name
output queueServiceName string = queueService.name
