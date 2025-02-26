@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param agentWebExists bool
@secure()
param agentWebDefinition object

@description('Id of the user or app to assign application roles')
param principalId string

@description('azd env name')
param environmentName string

@description('Image to deploy')
param imageToDeploy string = ''

param vnetAddressPrefix string = '10.0.0.0/16'
param containerAppEnvSubnetPrefix string = '10.0.0.0/21'
param appGatewaySubnetPrefix string = '10.0.8.0/24'
param logicAppSubnetPrefix string = '10.0.9.0/24'

param icmClientCertName string = 'IcmClientCert'
param icmClientCertSubject string = 'icm-client.agent.azurecontainerapps.dev'
param fileshareName string = 'aca-agent-share'

param enableAppGatewayHttps bool

var abbrs = loadJsonContent('./abbreviations.json')
var uniqueToken = substring(uniqueString(subscription().id, resourceGroup().id, location), 0, 3)
var resourceToken = 'aca-agent-1p-${uniqueToken}'

// Monitor application with Azure Monitor
module monitoring 'br/public:avm/ptn/azd/monitoring:0.1.0' = {
  name: 'monitoring'
  params: {
    logAnalyticsName: '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: '${abbrs.insightsComponents}${resourceToken}'
    applicationInsightsDashboardName: '${abbrs.portalDashboards}${resourceToken}'
    location: location
    tags: tags
  }
}

module vnet 'modules/vnet.bicep' = {
  name: 'vnet'
  params: {
    nsgName: '${abbrs.networkNetworkSecurityGroups}${resourceToken}'
    location: location
    tags: tags
    vnetName: '${abbrs.networkVirtualNetworks}${resourceToken}'
    vnetAddressPrefix: vnetAddressPrefix
    containerAppEnvSubnetPrefix: containerAppEnvSubnetPrefix
    appGatewaySubnetPrefix: appGatewaySubnetPrefix
    logicAppSubnetPrefix: logicAppSubnetPrefix
  }
}


// Container registry
module containerRegistry 'br/public:avm/res/container-registry/registry:0.1.1' = {
  name: 'registry'
  params: {
    name: replace('${abbrs.containerRegistryRegistries}${resourceToken}', '-', '')
    location: location
    acrAdminUserEnabled: true
    tags: tags
    publicNetworkAccess: 'Enabled'
    roleAssignments: [
      {
        principalId: agentWebIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: subscriptionResourceId(
          'Microsoft.Authorization/roleDefinitions',
          '7f951dda-4ed3-4680-a7ca-43fe172d538d'
        )
      }
    ]
  }
}

// Container apps environment
module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.9.0' = {
  name: 'container-apps-environment'
  params: {
    logAnalyticsWorkspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
    name: '${abbrs.appManagedEnvironments}${resourceToken}'
    location: location
    zoneRedundant: true
    internal: true
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    infrastructureSubnetId: vnet.outputs.containerAppsSubnetResourceId
    storages:[
      {
        storageAccountName: storage.outputs.storageAccountName
        accessMode: 'ReadWrite'
        kind: 'SMB'
        shareName: fileshareName
      }
    ]
  }
}

module agentWebIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'agentWebidentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}agentWeb-${resourceToken}'
    location: location
  }
}

module deploymentScriptIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'deploymentScriptIdentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}script-${resourceToken}'
    location: location
  }
}

module agentWebFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'agentWeb-fetch-image'
  params: {
    exists: agentWebExists
    name: 'agent-web'
  }
}

var agentWebAppSettingsArray = filter(array(agentWebDefinition.settings), i => i.name != '')
var agentWebSecrets = map(filter(agentWebAppSettingsArray, i => i.?secret != null), i => {
  name: i.name
  value: i.value
  secretRef: i.?secretRef ?? take(replace(replace(toLower(i.name), '_', '-'), '.', '-'), 32)
})
var agentWebEnv = map(filter(agentWebAppSettingsArray, i => i.?secret == null), i => {
  name: i.name
  value: i.value
})
var openaiApiKeySecretName = 'openai-api-key'

module agentWeb 'br/public:avm/res/app/container-app:0.12.2' = {
  name: 'agentWeb'
  // dependsOn: [GenerateIcmClientCertScript]
  params: {
    name: 'agent-web'
    ingressTargetPort: 8080
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    secrets: {
      secureList: union(
        [
          {
            name: 'icm-automation-client-cert'
            keyVaultUrl: '${keyVault.outputs.uri}secrets/${icmClientCertName}'
            identity: agentWebIdentity.outputs.resourceId
          }
          {
            name: 'logicapp-post-incident-discussion-url'
            keyVaultUrl: '${keyVault.outputs.uri}secrets/logicapp-post-incident-discussion-url'
            identity: agentWebIdentity.outputs.resourceId
          }
          {
            name: openaiApiKeySecretName
            keyVaultUrl: '${keyVault.outputs.uri}secrets/${openaiApiKeySecretName}'
            identity: agentWebIdentity.outputs.resourceId
          }
        ],
        map(agentWebSecrets, secret => {
          name: secret.secretRef
          value: secret.value
        })
      )
    }
    workloadProfileName: 'Consumption'
    containers: [
      {
        // image: agentWebFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
        image: agentWebExists && !empty(imageToDeploy)
          ? imageToDeploy
          : 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
        name: 'main'
        resources: {
          cpu: json('0.5')
          memory: '1.0Gi'
        }
        env: union(
          [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: monitoring.outputs.applicationInsightsConnectionString
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: agentWebIdentity.outputs.clientId
            }
            {
              name: 'PORT'
              value: '8080'
            }
            {
              name: 'Azure__OpenAI__DeploymentName'
              value: 'gpt-4o'
            }
            {
              name: 'Azure__OpenAI__Endpoint'
              value: openai.properties.endpoint
            }
            // TODO: put API key in key vault
            {
              name: 'Azure__OpenAI__ApiKey'
              // value: openai.listKeys().key1
              secretRef: openaiApiKeySecretName
            }
            {
              name: 'Azure__TaskStorage__FilePath'
              value: '/mnt/task-storage/tasks.json'
            }
            {
              name: 'Kusto__ManagedIdentityClientId'
              value: agentWebIdentity.outputs.clientId
            }
            {
              name: 'ICM__ServiceId'
              value: 'f7c85136-4f1f-417c-bb3d-d540a26746c8'
            }
            {
              name: 'ICM__CertificateFilePath'
              value: 'base64:/mnt/icm-automation/client-cert.pfx'
            }
            {
              name: 'ICM__WorkflowNames__FetchICMIncidentInfo'
              value: 'Workflow-GetIncidentInfo'
            }
            {              
              name: 'ICM__PostIncidentDiscussionUrl'
              secretRef: 'logicapp-post-incident-discussion-url'
            }
          ],
          agentWebEnv,
          map(agentWebSecrets, secret => {
            name: secret.name
            secretRef: secret.secretRef
          })
        )
        volumeMounts: [
          {
            volumeName: 'icm-automation-client-cert-vol'
            mountPath: '/mnt/icm-automation'
          }
          {
            volumeName: 'task-storage'
            mountPath: '/mnt/task-storage'
          }
        ]
      }
    ]
    volumes: [
      {
        name: 'icm-automation-client-cert-vol'
        storageType: 'Secret'
        secrets: [
          {
            secretRef: 'icm-automation-client-cert'
            path: 'client-cert.pfx'
          }
        ]
      }
      {
        name: 'task-storage'
        storageType: 'AzureFile'
        storageName: fileshareName
      }
    ]
    managedIdentities: {
      systemAssigned: false
      userAssignedResourceIds: [agentWebIdentity.outputs.resourceId]
    }
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: agentWebIdentity.outputs.resourceId
      }
    ]
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    location: location
    tags: union(tags, { 'azd-service-name': 'agent-web' })
  }
}

// Create a keyvault to store secrets
module keyVault 'br/public:avm/res/key-vault/vault:0.12.0' = {
  name: 'keyvault'
  params: {
    name: '${abbrs.keyVaultVaults}${resourceToken}'
    location: location
    tags: tags
    enableRbacAuthorization: false
    accessPolicies: [
      {
        objectId: principalId
        permissions: {
          secrets: ['get', 'list']
          certificates: ['get', 'list']
        }
      }
      {
        objectId: agentWebIdentity.outputs.principalId
        permissions: {
          secrets: ['get', 'list']
          certificates: ['get', 'list']
        }
      }
      {
        objectId: deploymentScriptIdentity.outputs.principalId
        permissions: {
          certificates: ['get', 'create']
        }
      }
      {
        objectId: 'f3c21649-0979-4721-ac85-b0216b2cf413' // Microsoft.Azure.CertificateRegistration
        permissions: {
          secrets: ['get', 'list', 'set', 'delete', 'recover', 'backup', 'restore']
        }
      }
    ]
    secrets: [
      {
        name: openaiApiKeySecretName
        value: openai.listKeys().key1
      }
    ]
  }
}

/*
module GenerateIcmClientCertScript 'br/public:avm/res/resources/deployment-script:0.5.1' = {
  name: 'GenerateIcmClientCert'
  params: {
    name: 'GenerateIcmClientCert'
    location: location
    kind: 'AzurePowerShell'
    azPowerShellVersion: '10.0'
    arguments: '-name ${icmClientCertName} -keyVault ${keyVault.name} -subject ${icmClientCertSubject}'
    scriptContent: '''
      param(
        [Parameter(Mandatory=$true)][string] $name,  
        [Parameter(Mandatory=$true)][string] $keyVault,  
        [Parameter(Mandatory=$true)][string] $subject
      )
      $ErrorActionPreference = 'Stop"
      $DeploymentScriptOutputs = @{}
      $DeploymentScriptOutputs['text'] = $output
      Connect-AzAccount -Identity
      $Cert = Get-AzKeyVaultCertificate -VaultName "$keyVault" -Name "$name"
      if ($Cert -eq $null)
      {
          Write-Host "No client certifate of Icm is found. Will generate a new one..."
          $Policy = New-AzKeyVaultCertificatePolicy -SecretContentType "application/x-pkcs12" -SubjectName "CN=$subject" -IssuerName "Self" -ValidityInMonths 6 -ReuseKeyOnRenewal -DnsName "$subject"
          Add-AzKeyVaultCertificate -VaultName "$keyVault" -Name "$name" -CertificatePolicy $Policy
      }
      else
      {
          Write-Host "The client certifate of Icm already exists."
      }
    '''
    timeout: 'PT1H'
    retentionInterval: 'PT1H'
    managedIdentities: {
      userAssignedResourceIds: [
        deploymentScriptIdentity.outputs.resourceId
      ]
    }
  }
}
*/

resource openai 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: '${abbrs.cognitiveServicesAccounts}${resourceToken}'
  location: location
  sku: {
    name: 'S0'
  }
  kind: 'OpenAI'
  tags: tags
  properties: {}
}

// Use conditional expression as a workaround. A model deployment will fail if the model already exists with error message 'InvalidResourceProperties: The sku of model deployment is not provided.'
resource gpt4o 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (!agentWebExists) {
  parent: openai
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: 10
  }
  properties: {
    model: {
      name: 'gpt-4o'
      format: 'OpenAI'
      version: '2024-05-13'
    }
    raiPolicyName: 'Microsoft.Default'
    versionUpgradeOption: 'OnceCurrentVersionExpired'
  }
}

module privateDnsZone 'br/public:avm/res/network/private-dns-zone:0.7.0' = {
  name: 'privateDnsZone'
  params: {
    name: containerAppsEnvironment.outputs.defaultDomain
    // private dns zone is a global resource
    // location: location
    tags: tags
    virtualNetworkLinks: [
      {
        name: 'my-custom-vnet-pdns-link'
        virtualNetworkResourceId: vnet.outputs.vnetResourceId
      }
    ]
    a: [
      {
        name: '*'
        aRecords: [
          {
            ipv4Address: containerAppsEnvironment.outputs.staticIp
          }
        ]
      }
      {
        name: '@'
        aRecords: [
          {
            ipv4Address: containerAppsEnvironment.outputs.staticIp
          }
        ]
      }
    ]
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    // storage account name can't contain '-'
    accountName: replace('${abbrs.storageStorageAccounts}${resourceToken}', '-', '')
    queueName: 'aca-agent-queue'
    fileshareName: fileshareName
    tags: tags
    userAssignedIdentityId: agentWebIdentity.outputs.resourceId
    userAssignedIdentityPrincipalId: agentWebIdentity.outputs.principalId
  }
}

module appGateway 'modules/app-gateway.bicep' = {
  name: 'appGateway'
  params: {
    appGatewayName: '${abbrs.networkApplicationGateways}${resourceToken}'
    publicIpName: '${abbrs.networkPublicIPAddresses}-agw-${resourceToken}'
    location: location
    tags: tags
    appGatewaySubnetId: vnet.outputs.appGatewaySubnetResourceId
    appGatewayCertId: '${keyVault.outputs.uri}secrets/acaagentcert'
    identityResourceId: agentWebIdentity.outputs.resourceId
    backendPoolFqdn: agentWeb.outputs.fqdn
    // identityPrincipalId: agentWebIdentity.outputs.principalId
    // identityClientId: agentWebIdentity.outputs.clientId
    enableAppGatewayHttps: enableAppGatewayHttps
    keyVaultResourceId: keyVault.outputs.resourceId
    keyVaultName: keyVault.outputs.name
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.uri
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output AZURE_RESOURCE_AGENT_WEB_ID string = agentWeb.outputs.resourceId
output AZURE_OPENAI_ENDPOINT string = openai.properties.endpoint
output AZURE_APP_GATEWAY_FRONTEND_IP string = appGateway.outputs.publicIp
output AZURE_KEY_VAULT_ID string = keyVault.outputs.resourceId
