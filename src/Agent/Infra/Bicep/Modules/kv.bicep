var consts = loadJsonContent('../consts.json')

param namePrefix string

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' existing = {
  name: '${namePrefix}${consts.managedIdentityNameSuffix}'
}

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

resource vaultUriSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:FirstParty:KeyVaultConfiguration:KeyVaultUri'
  parent: appConfig
  properties: {
    value: keyVault.properties.vaultUri
  }
}

resource vaultIdentitySetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:FirstParty:KeyVaultConfiguration:Identity'
  parent: appConfig
  properties: {
    value: identity.id
  }
}

