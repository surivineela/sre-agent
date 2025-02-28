var consts = loadJsonContent('consts.json')

targetScope = 'subscription'

param namePrefix string

var rgName = '${namePrefix}${consts.resourceGroupNameSuffix}'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: rgName
  location: consts.location
}

module rgModule 'Modules/rg.bicep' = {
  name: 'rgModule'
  scope: rg
  params: {
    namePrefix: namePrefix
  }
}
