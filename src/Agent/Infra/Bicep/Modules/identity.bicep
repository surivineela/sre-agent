var consts = loadJsonContent('../consts.json')

param namePrefix string
var userIdentityName = '${namePrefix}${consts.managedIdentityNameSuffix}'

resource userIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' = {
  name: userIdentityName
  location: resourceGroup().location
}

output name string = userIdentity.name
output resourceId string = userIdentity.id
output clientId string = userIdentity.properties.clientId
output principalId string = userIdentity.properties.principalId
