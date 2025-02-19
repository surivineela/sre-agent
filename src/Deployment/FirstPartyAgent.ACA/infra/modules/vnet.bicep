@minLength(5)
param nsgName string
param location string
param tags object
@minLength(5)
param vnetName string
param vnetAddressPrefix string
param containerAppEnvSubnetPrefix string
param appGatewaySubnetPrefix string
param logicAppSubnetPrefix string

module nsg 'br/public:avm/res/network/network-security-group:0.5.0' = {
  name: 'nsg'
  params: {
    // name: '${abbrs.networkNetworkSecurityGroups}${resourceToken}'
    name: nsgName
    location: location
    tags: tags
    securityRules: [
      {
        name: 'NRMS-Rule-104'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'CorpNetSaw'
          destinationAddressPrefix: '*'
          access: 'Allow'
          direction: 'Inbound'
          priority: 104
        }
      }
      {
        name: 'NRMS-Rule-101'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: 'VirtualNetwork'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 101
          direction: 'Inbound'
        }
      }
      {
        name: 'NRMS-Rule-105'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 105
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: [
            '1433'
            '1434'
            '3306'
            '4333'
            '5432'
            '6379'
            '7000'
            '7001'
            '7199'
            '9042'
            '9160'
            '9300'
            '16379'
            '26379'
            '27017'
          ]
        }
      }
      {
        name: 'NRMS-Rule-107'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 107
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: [
            '23'
            '135'
            '445'
            '5985'
            '5986'
          ]
        }
      }
      {
        name: 'NRMS-Rule-108'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 108
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: [
            '13'
            '17'
            '19'
            '53'
            '69'
            '111'
            '123'
            '512'
            '514'
            '593'
            '873'
            '1900'
            '5353'
            '11211'
          ]
        }
      }
      {
        name: 'NRMS-Rule-106'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 106
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: [
            '22'
            '3389'
          ]
          sourceAddressPrefixes: []
          destinationAddressPrefixes: []
        }
      }
      {
        name: 'NRMS-Rule-103'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'CorpNetPublic'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 103
          direction: 'Inbound'
        }
      }
      {
        name: 'NRMS-Rule-109'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 109
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: [
            '119'
            '137'
            '138'
            '139'
            '161'
            '162'
            '389'
            '636'
            '2049'
            '2301'
            '2381'
            '3268'
            '5800'
            '5900'
          ]
          sourceAddressPrefixes: []
          destinationAddressPrefixes: []
        }
      }
      {
        name: 'AllowGatewayManagerInbound'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '65200-65535'
          sourceAddressPrefix: 'GatewayManager'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 119
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: []
          sourceAddressPrefixes: []
          destinationAddressPrefixes: []
        }
      }
      {
        name: 'AllowIcMAutomation_1'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: 'LogicApps'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 120
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: []
          sourceAddressPrefixes: []
          destinationAddressPrefixes: []
        }
      }
      {
        name: 'AllowIcMAutomation_2'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: 'AzureConnectors.WestUS'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 121
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: []
          sourceAddressPrefixes: []
          destinationAddressPrefixes: []
        }
      }
      {
        name: 'AllowIcMAutomation_3'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: 'AzureConnectors.WestUS3'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 122
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: []
          sourceAddressPrefixes: []
          destinationAddressPrefixes: []
        }
      }
      {
        name: 'AllowMSFTVPN'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '137.116.128.0/24'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 129
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: []
          sourceAddressPrefixes: []
          destinationAddressPrefixes: []
        }
      }
      {
        name: 'AllowedLogicApps'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '443'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 123
          direction: 'Inbound'
          sourcePortRanges: []
          destinationPortRanges: []
          sourceAddressPrefixes: [
            '40.123.47.55'
            '40.123.44.27'
            '40.123.41.219'
            '40.123.42.8'
            '40.84.30.124'
            '52.177.196.86'
            '52.247.57.175'
            '52.179.167.128'
            '52.232.245.226'
            '20.72.97.111'
            '20.96.210.231'
            '20.96.212.169'
            '20.96.212.184'
            '20.96.212.185'
            '20.96.212.196'
            '20.96.254.218'
            '20.96.254.225'
            '20.96.254.247'
            '20.96.255.32'
            '20.96.255.75'
            '20.96.255.110'
            '40.123.47.58'
          ]
          destinationAddressPrefixes: []
        }
      }
    ]
  }
}

module vnet 'br/public:avm/res/network/virtual-network:0.5.2' = {
  name: 'vnet-deployment'
  params: {
    // name: '${abbrs.networkVirtualNetworks}${resourceToken}'
    name: vnetName
    location: location
    tags: tags
    addressPrefixes: [vnetAddressPrefix]
    subnets: [
      // Please do not move the order of the subnets
      {
        name: 'aca-agent-web-subnet'
        addressPrefixes: [containerAppEnvSubnetPrefix]
        delegation: 'Microsoft.App/environments'
        networkSecurityGroupResourceId: nsg.outputs.resourceId
      }
      {
        name: 'app-gateway'
        addressPrefixes: [appGatewaySubnetPrefix]
        networkSecurityGroupResourceId: nsg.outputs.resourceId
      }
      {
        name: 'logicapp-subnet'
        addressPrefixes: [logicAppSubnetPrefix]
        networkSecurityGroupResourceId: nsg.outputs.resourceId
        delegation: 'Microsoft.Web/serverfarms'
      }
    ]
  }
}

output vnetResourceId string = vnet.outputs.resourceId
output nsgResourceId string = nsg.outputs.resourceId
output containerAppsSubnetResourceId string = vnet.outputs.subnetResourceIds[0]
output appGatewaySubnetResourceId string = vnet.outputs.subnetResourceIds[1]
