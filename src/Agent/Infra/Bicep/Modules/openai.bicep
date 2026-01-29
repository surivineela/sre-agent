var consts = loadJsonContent('../consts.json')

param namePrefix string
param useOldOpenAIName bool

var openAIName = '${namePrefix}${consts.openAIAccountNameSuffix}'
var userIdentityName = '${namePrefix}${consts.managedIdentityNameSuffix}'

var customSubDomainName = useOldOpenAIName ? namePrefix : openAIName

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' existing = {
  name: userIdentityName
}

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

// gpt-5-mini is not supported in all regions
resource openai 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: 'eastus2'
  sku: {
    name: 'S0'
  }
  kind: 'OpenAI'
  properties: {
    publicNetworkAccess: 'Enabled'
    customSubDomainName: customSubDomainName
    disableLocalAuth: true
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

// Role assignments for managed identity access (replaces API key auth)
// User-assigned managed identity access for Azure deployments (application identity)
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

// Local user access for local development deployments
resource cognitiveServicesUserDeployerRoleAssignment 'Microsoft.Authorization/roleAssignments@2018-09-01-preview' = {
  name: guid(openAIName, deployer().objectId, consts.CognitiveServicesUser)
  scope: openai
  properties: {
    principalId: deployer().objectId
    principalType: 'User'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      consts.CognitiveServicesUser
    )
  }
}
