param namePrefix string
@description('Resource group name of the managed resource group of the Azure Monitor Workspace that hosts its data collection rule.')
param dataCollectionRuleName string

resource monitoringMetricsPublisherRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: '3913510d-42f4-4e42-8a64-420c390055eb'
  scope: subscription()
}

resource dataCollectionRule 'Microsoft.Insights/dataCollectionRules@2023-03-11' existing = {
  name: dataCollectionRuleName
}

// Grant deployer the Monitoring Metrics Publisher role at the data collection rule scope
// So metrics can be proactively pushed to Azure Monitor Workspace when testing locally
resource monitoringMetricsPublisherRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name:  guid(namePrefix, dataCollectionRuleName, monitoringMetricsPublisherRole.id)
  scope: dataCollectionRule
  properties: {
    roleDefinitionId: monitoringMetricsPublisherRole.id
    principalId: deployer().objectId
    principalType: 'User'
  }
}
