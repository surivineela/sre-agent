param namePrefix string
param useOldOpenAIName bool

module appConfigModule 'appConfig.bicep' = {
  name: 'appConfig'
  params: {
    namePrefix: namePrefix
    useOldOpenAIName: useOldOpenAIName
  }
}
