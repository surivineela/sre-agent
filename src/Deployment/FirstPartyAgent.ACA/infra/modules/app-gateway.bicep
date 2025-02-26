param appGatewayName string
param publicIpName string
param location string = resourceGroup().location
param appGatewaySubnetId string
param backendPoolFqdn string
param identityResourceId string
// param identityPrincipalId string
// param identityClientId string
param tags object
param enableAppGatewayHttps bool
param keyVaultResourceId string
param keyVaultName string



@secure()
@description('The ID of the secret in the Key Vault that contains the certificate for the Application Gateway that usually follows the format of `https://<key-vault-url/secrets/<secret-name>`')
param appGatewayCertId string

module domain 'domain.bicep' = if (enableAppGatewayHttps) {
  name: 'domain'
  params: {
    keyVaultResourceId: keyVaultResourceId
    keyVaultName: keyVaultName
  }
}

resource publicIP 'Microsoft.Network/publicIPAddresses@2024-05-01' = {
  name: publicIpName
  tags: tags
  location: location
  zones: [
    '1'
    '2'
    '3'
  ]
  properties: {
    // ipAddress: '4.152.2.217'
    publicIPAddressVersion: 'IPv4'
    publicIPAllocationMethod: 'Static'
    idleTimeoutInMinutes: 4
    ipTags: []
  }
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
}

var frontendIPConfigurationName = 'appGwPublicFrontendIpIPv4'
// var appGatewayName = 'my-container-apps-agw'
// http is only used for testing purposes when the app gateway is not yet configured to use https on first deployment
var port80 = 'port_80'
var port443 = 'port_443'
var backendPoolName = 'backend-pool-aca'
var backendSettingName = 'backend-setting-aca'

resource appGateway 'Microsoft.Network/applicationGateways@2024-05-01' = {
  name: appGatewayName
  tags: tags
  zones: [
    '1'
    '2'
    '3'
  ]
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityResourceId}': {
        // principalId: identityPrincipalId
        // clientId: identityClientId
      }
    }
  }
  properties: {
    sku: {
      name: 'Standard_v2'
      tier: 'Standard_v2'
      family: 'Generation_2'
    }
    gatewayIPConfigurations: [
      {
        name: 'appGatewayIpConfig'
        properties: {
          subnet: {
            id: appGatewaySubnetId
          }
        }
      }
    ]
    sslCertificates: [
      {
        name: 'acaagent'
        properties: {
          keyVaultSecretId: appGatewayCertId
        }
      }
    ]
    trustedRootCertificates: []
    trustedClientCertificates: []
    sslProfiles: []
    frontendIPConfigurations: [
      {
        name: frontendIPConfigurationName
        properties: {
          privateIPAllocationMethod: 'Dynamic'
          publicIPAddress: {
            id: publicIP.id
          }
        }
      }
    ]
    frontendPorts: union(
      [
        {
          name: port80
          properties: {
            port: 80
          }
        }
      ],
      enableAppGatewayHttps
        ? [
            {
              name: port443
              properties: {
                port: 443
              }
            }
          ]
        : []
    )
    backendAddressPools: [
      {
        name: backendPoolName
        properties: {
          backendAddresses: [
            {
              fqdn: backendPoolFqdn
            }
          ]
        }
      }
    ]
    loadDistributionPolicies: []
    backendHttpSettingsCollection: [
      {
        name: backendSettingName
        properties: {
          port: 443
          protocol: 'Https'
          cookieBasedAffinity: 'Disabled'
          pickHostNameFromBackendAddress: true
          affinityCookieName: 'ApplicationGatewayAffinity'
          path: null
          requestTimeout: 20
          probe: {
            id: resourceId('Microsoft.Network/applicationGateways/probes', appGatewayName, 'health-probe')
          }
        }
      }
    ]
    backendSettingsCollection: []
    httpListeners: union(
      [
        {
          name: 'http-listener'
          properties: {
            frontendIPConfiguration: {
              id: resourceId(
                'Microsoft.Network/applicationGateways/frontendIPConfigurations',
                appGatewayName,
                frontendIPConfigurationName
              )
            }
            frontendPort: {
              id: resourceId('Microsoft.Network/applicationGateways/frontendPorts', appGatewayName, port80)
            }
            protocol: 'Http'
          }
        }
      ],
      enableAppGatewayHttps
        ? [
            {
              name: 'https-listener'
              properties: {
                frontendIPConfiguration: {
                  id: resourceId(
                    'Microsoft.Network/applicationGateways/frontendIPConfigurations',
                    appGatewayName,
                    frontendIPConfigurationName
                  )
                }
                frontendPort: {
                  id: resourceId('Microsoft.Network/applicationGateways/frontendPorts', appGatewayName, port443)
                }
                protocol: 'Https'
                sslCertificate: {
                  id: resourceId('Microsoft.Network/applicationGateways/sslCertificates', appGatewayName, 'acaagent')
                }
                hostNames: []
                requireServerNameIndication: false
                customErrorConfigurations: []
              }
            }
          ]
        : []
    )
    listeners: []
    urlPathMaps: []
    requestRoutingRules: union(
      enableAppGatewayHttps
        ? [
            {
              name: 'https-routing-rule'
              properties: {
                ruleType: 'Basic'
                priority: 1
                httpListener: {
                  id: resourceId(
                    'Microsoft.Network/applicationGateways/httpListeners',
                    appGatewayName,
                    'https-listener'
                  )
                }
                backendAddressPool: {
                  id: resourceId(
                    'Microsoft.Network/applicationGateways/backendAddressPools',
                    appGatewayName,
                    backendPoolName
                  )
                }
                backendHttpSettings: {
                  id: resourceId(
                    'Microsoft.Network/applicationGateways/backendHttpSettingsCollection',
                    appGatewayName,
                    backendSettingName
                  )
                }
              }
            }
          ]
        : [],
      [
        {
          name: 'http'
          properties: {
            ruleType: 'Basic'
            priority: 2
            httpListener: {
              id: resourceId('Microsoft.Network/applicationGateways/httpListeners', appGatewayName, 'http-listener')
            }
            backendAddressPool: {
              id: resourceId(
                'Microsoft.Network/applicationGateways/backendAddressPools',
                appGatewayName,
                backendPoolName
              )
            }
            backendHttpSettings: {
              id: resourceId(
                'Microsoft.Network/applicationGateways/backendHttpSettingsCollection',
                appGatewayName,
                backendSettingName
              )
            }
          }
        }
      ]
    )
    routingRules: []
    probes: [
      {
        name: 'health-probe'
        properties: {
          protocol: 'Https'
          path: '/api/Health'
          interval: 30
          timeout: 30
          unhealthyThreshold: 3
          pickHostNameFromBackendHttpSettings: true
          minServers: 0
          match: {
            statusCodes: [
              '200-399'
            ]
          }
        }
      }
    ]
    rewriteRuleSets: []
    redirectConfigurations: []
    privateLinkConfigurations: []
    enableHttp2: true
    autoscaleConfiguration: {
      minCapacity: 0
      maxCapacity: 10
    }
  }
}

output appGatewayId string = appGateway.id
output publicIp string = publicIP.properties.ipAddress
