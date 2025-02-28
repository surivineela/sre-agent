var consts = loadJsonContent('../consts.json')

param namePrefix string

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2022-05-01' existing = {
  name: '${namePrefix}${consts.appConfigNameSuffix}'
}

resource kv 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: '${namePrefix}${consts.kvNameSuffix}'
}

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

// Settings

// Secret Settings
resource appInsightsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: 'app-insights-connection-string'
  parent: kv
  properties: {
    value: appInsights.properties.ConnectionString
  }
}
resource appInsightsConnectionStringSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2022-05-01' = {
  name: 'AppSettings:Core:Azure:AppInsights:ConnectionString'
  parent: appConfig
  properties: {
    value: string({uri: appInsightsConnectionStringSecret.properties.secretUri})
    contentType: 'application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8'
  }
}
