var consts = loadJsonContent('consts.json')

targetScope = 'subscription'

@minLength(2)
param namePrefix string

@description('Set to true if OpenAI resource already exists with old naming, false to use openAiName for subdomain')
param useOldOpenAIName bool = false

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
    useOldOpenAIName: useOldOpenAIName
  }
}
