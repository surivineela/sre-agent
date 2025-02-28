var consts = loadJsonContent('../consts.json')

param namePrefix string

resource dts 'Microsoft.DurableTask/schedulers@2024-10-01-preview' = {
  location: consts.defaultDTSLocation
  name: '${namePrefix}${consts.durableTaskSchedulerNameSuffix}'
  properties: {
    ipAllowlist: ['0.0.0.0/0']
    sku: {
      name: 'Dedicated'
      capacity: 1
    }
  }
}

resource taskhub 'Microsoft.DurableTask/schedulers/taskhubs@2024-10-01-preview' = {
  parent: dts
  name: '${namePrefix}${consts.durableTaskSchedulerTaskHubNameSuffix}'
}
