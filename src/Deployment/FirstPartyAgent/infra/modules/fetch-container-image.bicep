param exists bool
param name string

resource existingApp 'Microsoft.App/containerApps@2024-10-02-preview' existing = if (exists) {
  name: name
}

output containers array = exists ? existingApp.properties.template.containers : []
