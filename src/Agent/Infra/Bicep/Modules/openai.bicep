var consts = loadJsonContent('../consts.json')

param namePrefix string

resource openai 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: '${namePrefix}${consts.openAIAccountNameSuffix}'
  location: resourceGroup().location
  sku: {
    name: 'S0'
  }
  kind: 'OpenAI'
  properties: {
    publicNetworkAccess: 'Enabled'
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
