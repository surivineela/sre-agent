var consts = loadJsonContent('../consts.json')

param namePrefix string

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: '${namePrefix}${consts.logAnalyticsWorkspaceNameSuffix}'
  location: resourceGroup().location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 180
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: '${namePrefix}${consts.appInsightsNameSuffix}'
  location: resourceGroup().location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    Flow_Type: 'Bluefield'
  }
}
