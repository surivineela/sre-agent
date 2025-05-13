targetScope = 'resourceGroup'

@description('The location where the resources will be deployed.')
param location string = 'swedencentral'

@description('The name of the user-assigned managed identity.')
param managedIdentityNameForKustoAccess string = 'ACA1PAgent-uami'

@description('The resource group of the managed identity.')
param managedIdentityResourceGroupName string = 'ACA1PAgent-rg'

@description('The subscription ID of the managed identity.')
param managedIdentitySubscriptionId string = 'be8d491e-109c-4ee1-aaee-dc7615af0a42'

@description('The name of the Log Analytics workspace.')
param logAnalyticsWorkspaceName string = 'RCAAgent-log-analytics'

@description('The name of the RCA agent.')
param agentName string

@description('The container image for the RCA agent.')
param agentImage string

@description('The container registry username.')
param registryUserName string

@secure()
@description('The container registry password.')
param registryPassword string

@description('Indicates whether to include the FirstPartyConfiguration block.')
param includeFirstPartyConfiguration bool = false

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: managedIdentityNameForKustoAccess
  scope: resourceGroup(managedIdentitySubscriptionId, managedIdentityResourceGroupName)
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource agent 'Microsoft.App/agents@2025-05-01-preview' = {
  name: agentName
  location: location
  properties: {
    knowledgeGraphConfiguration: {
      identity: userAssignedIdentity.id
      managedResources: [
        '/subscriptions/${subscription().subscriptionId}/resourceGroups/${resourceGroup().name}'
      ]
    }
    outboundConnectionConfiguration: {
      azureBotConfiguration: {
        identity: userAssignedIdentity.id
      }
    }
    logConfiguration: {
      logAnalyticsConfiguration: {
        workspaceId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
    firstPartyConfiguration: includeFirstPartyConfiguration ? {
      agentTypeName: 'RCAAgent'
      agentImageConfiguration: {
        imageName: agentImage
        registryUserName: registryUserName
        registryPassword: registryPassword
      }
    } : null
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
}
