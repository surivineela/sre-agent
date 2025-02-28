var consts = loadJsonContent('../consts.json')

param namePrefix string

// Create Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' = {
  name: '${namePrefix}${consts.kvNameSuffix}'
  location: resourceGroup().location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: false
  }
}

// Assign Data Plane Role
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, deployer().objectId, consts.keyVaultSecretsUser)
  scope: keyVault
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', consts.keyVaultSecretsUser)
    principalId: deployer().objectId
    principalType: 'User'
  }
}
