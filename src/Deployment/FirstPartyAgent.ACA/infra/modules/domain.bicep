param domainName string

resource appServiceDomain 'Microsoft.DomainRegistration/domains@2024-04-01' existing = {
  name: domainName
}

resource dnsZone 'Microsoft.Network/dnsZones@2023-07-01-preview' existing = {
  name: domainName
}

resource certOrder 'Microsoft.CertificateRegistration/certificateOrders@2024-04-01' existing = {
  name: 'aca-agent'
}
