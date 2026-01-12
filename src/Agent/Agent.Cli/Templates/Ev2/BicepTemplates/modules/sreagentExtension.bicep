// ============================================================
// Agent Extensions Deployment Module
// Deploys multiple subagent and tool extensions
// ============================================================
//
// This module takes arrays of extension configurations and deploys them
// to an existing agent as subagents or tools.
//
// Input: Arrays of parsed YAML objects containing agent configurations
// Processing: Extracts name from spec.name and converts object to JSON then base64
//
// Example input YAML object:
// {
//   spec: {
//     name: 'cri_meta_agent'
//   }
//   ...(other YAML content)
// }
//
// Output: Deployed as subagents/tools/skills with base64-encoded JSON content
// ============================================================

@description('Name of the parent agent')
param agentName string

@description('Array of parsed YAML objects for subagent extensions')
param subagents array

@description('Array of parsed YAML objects for tool extensions')
param tools array
@description('Array of parsed YAML objects for skill extensions')
param skills array
@description('Array of parsed YAML objects for scheduled task extensions')
param scheduledTasks array

// Reference to the existing parent agent
resource parentAgent 'Microsoft.App/agents@2025-05-01-preview' existing = {
  name: agentName
}

// Deploy subagent extensions sequentially
@batchSize(1)
resource subagentExtensions 'Microsoft.App/agents/subagents@2025-05-01-preview' = [for subagent in subagents: {
  parent: parentAgent
  name: subagent.metadata.name
  properties: {
    value: base64(string(subagent.spec))
  }
}]

// Deploy tool extensions sequentially
@batchSize(1)
resource toolExtensions 'Microsoft.App/agents/tools@2025-05-01-preview' = [for tool in tools: {
  parent: parentAgent
  name: tool.metadata.name
  properties: {
    value: base64(string(tool.spec))
  }
}]

// Deploy skill extensions sequentially
@batchSize(1)
resource skillExtensions 'Microsoft.App/agents/skills@2025-05-01-preview' = [for skill in skills: {
  parent: parentAgent
  name: skill.metadata.name
  properties: {
    value: base64(string({
      name: skill.metadata.metadata.name
      description: skill.metadata.spec.description
      skillContent: skill.skillContent
      additionalFiles: skill.additionalFiles
    }))
  }
}]

// Deploy scheduledTasks extensions sequentially
@batchSize(1)
resource scheduledTasksExtension 'Microsoft.App/agents/scheduledTasks@2025-05-01-preview' = [for scheduledTask in scheduledTasks: {
  parent: parentAgent
  name: scheduledTask.metadata.name
  properties: {
    value: base64(string(scheduledTask.spec))
  }
}]
