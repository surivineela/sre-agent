var consts = loadJsonContent('../consts.json')

param namePrefix string
var openAIName = '${namePrefix}${consts.openAIAccountNameSuffix}'
var userIdentityName = '${namePrefix}${consts.managedIdentityNameSuffix}'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' existing = {
  name: userIdentityName
}

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

resource kv 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: '${namePrefix}${consts.kvNameSuffix}'
}

resource openai 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: resourceGroup().location
  sku: {
    name: 'S0'
  }
  kind: 'OpenAI'
  properties: {
    publicNetworkAccess: 'Enabled'
    customSubDomainName: openAIName
    // restore: true
  }
}

// https://github.com/Azure/bicep-types-az/issues/1736

// resource llm 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
//   name: '${namePrefix}-${consts.openAILLMModel}'
//   sku: {
//     name: 'GlobalStandard'
//     capacity: 450
//   }
//   parent: openai
//   properties: {
//     model: {
//       name: consts.openAILLMModel
//       format: 'OpenAI'
//       version: consts.openAILLMModelVersion
//     }
//   }
// }
var llm = {
  name: '${namePrefix}-${consts.openAILLMModel}'
  sku: {
    name: 'GlobalStandard'
    capacity: 450
  }
  properties: {
    model: {
      name: consts.openAILLMModel
      format: 'OpenAI'
      version: consts.openAILLMModelVersion
    }
  }
}

// resource embeddingGenerator 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
//   name: '${namePrefix}-${consts.openAIEmbeddingGeneratorModel}'
//   sku: {
//     name: 'Standard'
//     capacity: 240
//   }
//   parent: openai
//   properties: {
//     model: {
//       name: consts.openAIEmbeddingGeneratorModel
//       format: 'OpenAI'
//       version: consts.openAIEmbeddingGeneratorModelVersion
//     }
//   }
// }
var embeddingGenerator = {
  name: '${namePrefix}-${consts.openAIEmbeddingGeneratorModel}'
  sku: {
    name: 'Standard'
    capacity: 240
  }
  properties: {
    model: {
      name: consts.openAIEmbeddingGeneratorModel
      format: 'OpenAI'
      version: consts.openAIEmbeddingGeneratorModelVersion
    }
  }
}

var deployments = [llm, embeddingGenerator]

// https://github.com/Azure/azure-sdk-for-net/issues/43219
@batchSize(1)
resource cognitiveServicesAccountDeployment_5K9aRgiZP 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = [for deployment in deployments: {
  parent: openai
  name: deployment.name
  properties: deployment.properties
  sku: deployment.sku
}]

// Settings
var openaiEndpoint = openai.properties.endpoint
resource openaiEndpointSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:OpenAI:Endpoint'
  parent: appConfig
  properties: {
    value: openaiEndpoint
  }
}

resource openaiLLMDeploymentNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:OpenAI:LLMDeploymentName'
  parent: appConfig
  properties: {
    value: '${namePrefix}-${consts.openAILLMModel}'
  }
}

resource openaiEmbeddingGeneratorDeploymentNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:OpenAI:EmbeddingGeneratorDeploymentName'
  parent: appConfig
  properties: {
    value: '${namePrefix}-${consts.openAIEmbeddingGeneratorModel}'
  }
}

resource openaiEmbeddingGeneratorModelNameSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:OpenAI:EmbeddingGeneratorModelName'
  parent: appConfig
  properties: {
    value: consts.openAIEmbeddingGeneratorModel
  }
}

// Secret Settings
var openaiApiKey = openai.listKeys().key1
resource openaiApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'openai-api-key'
  parent: kv
  properties: {
    value: openaiApiKey
  }
}
resource openaiApiKeySetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:OpenAI:ApiKey'
  parent: appConfig
  properties: {
    value: string({uri: openaiApiKeySecret.properties.secretUri})
    contentType: 'application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8'
  }
}

// Additional role assignment for embeddings access
resource cognitiveServicesUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2018-09-01-preview' = {
  name: guid(openAIName, identity.name, consts.CognitiveServicesUser)
  scope: openai
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      consts.CognitiveServicesUser
    )
  }
}
