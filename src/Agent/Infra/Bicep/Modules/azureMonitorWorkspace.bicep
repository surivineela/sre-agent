var consts = loadJsonContent('../consts.json')
param namePrefix string
param location string = resourceGroup().location
param grafanaManagedIdentityId string


resource monitoringReaderRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: '43d0d8ad-25c7-4714-9337-8ba259a9fe05'
  scope: subscription()
}

resource monitoringDataReaderRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: 'b0d8363b-8ddd-447d-831f-62ca05bff136'
  scope: subscription()
}

resource azureMonitorWorkspace 'Microsoft.Monitor/accounts@2023-04-03' = {
  name: '${namePrefix}${consts.azureMonitorWorkspaceNameSuffix}'
  location: location
}

// Assign the Monitoring Reader role to the Azure Managed Grafana system-assigned managed identity at the workspace scope
resource monitoringReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name:  guid(namePrefix, azureMonitorWorkspace.id, monitoringReaderRole.id)
  scope: azureMonitorWorkspace
  properties: {
    roleDefinitionId: monitoringReaderRole.id
    principalId: grafanaManagedIdentityId
    principalType: 'ServicePrincipal'
  }
}

// Assign the Monitoring Data Reader role to the Azure Managed Grafana system-assigned managed identity at the workspace scope
resource monitoringDataReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name:  guid(namePrefix, azureMonitorWorkspace.id, monitoringDataReaderRole.id)
  scope: azureMonitorWorkspace
  properties: {
    roleDefinitionId: monitoringDataReaderRole.id
    principalId: grafanaManagedIdentityId
    principalType: 'ServicePrincipal'
  }
}

module dataCollectionRule 'dataCollectionRule.bicep' = {
  name: 'dataCollectionRuleDeployment'
  scope: resourceGroup('MA_${azureMonitorWorkspace.name}_${location}_managed')
  params: {
    namePrefix: namePrefix
    // The auto-generated dataCollectionRule name is the same as the Azure Monitor Workspace name.
    dataCollectionRuleName: azureMonitorWorkspace.name
  }
}
