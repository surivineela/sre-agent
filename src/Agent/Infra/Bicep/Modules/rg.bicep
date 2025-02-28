param namePrefix string

module appConfigModule 'appConfig.bicep' = {
  name: 'appConfig'
  params: {
    namePrefix: namePrefix
  }
}
