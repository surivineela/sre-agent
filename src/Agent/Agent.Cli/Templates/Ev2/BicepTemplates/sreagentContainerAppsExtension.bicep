// ============================================================
// Container Apps Agent Extensions Deployment
// Deploys multiple subagent and tool extensions from YAML files
// ============================================================

@description('Name of the parent agent to attach extensions to')
param agentName string

// Load extension files and get processed data
module extensionFiles 'modules/sreagentExtensionFile.bicep' = {
  name: 'extension-files'
}

// Deploy extensions using the loaded data
module extensionDeployment 'modules/sreagentExtension.bicep' = {
  params: {
    agentName: agentName
    subagents: extensionFiles.outputs.subagents
    tools: extensionFiles.outputs.tools
    skills: extensionFiles.outputs.skills
    scheduledTasks: extensionFiles.outputs.scheduledTasks
    incidentFilters: extensionFiles.outputs.incidentFilters
  }
}
