/**
 * Example File Generator
 *
 * Generates example YAML files for agents and tools during /init
 * Matches Agent.Cli's example files exactly
 */
import * as fs from 'fs/promises';
import * as path from 'path';

/**
 * Example agent YAML content (V2 format)
 */
export const EXAMPLE_AGENT_YAML = `apiVersion: srectl.agent/v2
kind: ExtendedAgent
metadata:
  name: example_agent
spec:
  instructions: |
    You are an example SRE agent designed to demonstrate the capabilities of the SREAgent system.
    You can help with basic incident management and provide guidance on SRE best practices.
    Always be helpful, professional, and focused on solving operational problems.
  handoffDescription: Use this agent for general SRE tasks and as an example of agent configuration.
  handoffs:
    - meta_agent
  tools:
    - example_tool
  allowParallelToolCalls: false
  maxReflectionCount: 0
  criticOnHandoff: false
  temperature: 0.7
  vanillaMode: false
  enableSkills: false
`;

/**
 * Example tool YAML content (KustoTool V2 format)
 */
export const EXAMPLE_TOOL_YAML = `apiVersion: srectl.agent/v2
kind: ExtendedAgentTool
metadata:
  name: example_tool
spec:
  type: KustoTool
  connector: example_connector
  description: |
    An example tool that demonstrates how to create tools for SRE agents.
    This tool queries data from Azure Data Explorer (Kusto).
  database: example_database
  query: |
    MyTable
    | where TimeGenerated > ago(1h)
    | summarize count() by OperationName
  parameters:
    - name: timeRange
      type: string
      description: Time range for the query (e.g., "1h", "24h", "7d")
      required: true
`;

/**
 * Example skill YAML content (V2 format)
 */
export const EXAMPLE_SKILL_YAML = `apiVersion: srectl.agent/v2
kind: ExtendedSkill
metadata:
  name: example_skill
spec:
  description: |
    An example skill that demonstrates reusable agent capabilities.
    Skills can be shared across multiple agents.
  instructions: |
    When using this skill, follow these guidelines:
    1. Always acknowledge the user's request
    2. Provide clear and concise responses
    3. Ask for clarification when needed
  tools: []
  enabled: true
`;

/**
 * Create workspace directories
 */
export async function createWorkspaceDirectories(
  basePath: string
): Promise<void> {
  const directories = ['agents', 'tools', 'skills', 'scheduledtasks'];

  for (const dir of directories) {
    const dirPath = path.join(basePath, dir);
    await fs.mkdir(dirPath, { recursive: true });
  }
}

/**
 * Create example files in the workspace
 */
export async function createExampleFiles(basePath: string): Promise<string[]> {
  const createdFiles: string[] = [];

  // Create example agent
  const agentPath = path.join(basePath, 'agents', 'example_agent.yaml');
  await fs.writeFile(agentPath, EXAMPLE_AGENT_YAML, 'utf-8');
  createdFiles.push(agentPath);

  // Create example tool
  const toolPath = path.join(basePath, 'tools', 'example_tool.yaml');
  await fs.writeFile(toolPath, EXAMPLE_TOOL_YAML, 'utf-8');
  createdFiles.push(toolPath);

  // Create example skill
  const skillPath = path.join(basePath, 'skills', 'example_skill.yaml');
  await fs.writeFile(skillPath, EXAMPLE_SKILL_YAML, 'utf-8');
  createdFiles.push(skillPath);

  return createdFiles;
}

/**
 * Agent template for creating new agents
 */
export function getAgentTemplate(name: string, instructions?: string): string {
  return `apiVersion: srectl.agent/v2
kind: ExtendedAgent
metadata:
  name: ${name}
spec:
  instructions: |
    ${instructions || 'Enter your agent instructions here.\n    Describe what this agent does and how it should behave.'}
  handoffDescription: Describe when to hand off to this agent
  handoffs: []
  tools: []
  allowParallelToolCalls: true
  maxReflectionCount: 3
  criticOnHandoff: false
  temperature: 0.7
  vanillaMode: false
  enableSkills: false
`;
}

/**
 * Tool templates for different tool types
 */
export function getKustoToolTemplate(
  name: string,
  connector?: string,
  database?: string
): string {
  return `apiVersion: srectl.agent/v2
kind: ExtendedAgentTool
metadata:
  name: ${name}
spec:
  type: KustoTool
  connector: ${connector || 'your_connector_name'}
  description: |
    Describe what this tool does and when to use it.
  database: ${database || 'your_database'}
  query: |
    // Your Kusto query here
    YourTable
    | where TimeGenerated > ago(1h)
    | take 100
  parameters:
    - name: timeRange
      type: string
      description: Time range for the query
      required: false
`;
}

export function getLinkToolTemplate(name: string, template?: string): string {
  return `apiVersion: srectl.agent/v2
kind: ExtendedAgentTool
metadata:
  name: ${name}
spec:
  type: LinkTool
  description: |
    Describe what this link tool provides.
  template: ${template || 'https://example.com/resource/{resourceId}'}
  parameters:
    - name: resourceId
      type: string
      description: The resource ID to include in the URL
      required: true
`;
}

export function getPythonToolTemplate(name: string): string {
  return `apiVersion: srectl.agent/v2
kind: ExtendedAgentTool
metadata:
  name: ${name}
spec:
  type: PythonTool
  description: |
    Describe what this Python tool does.
  functionCode: |
    def run(params):
        """
        Execute the tool logic.

        Args:
            params: Dictionary of input parameters

        Returns:
            Result string or dictionary
        """
        # Your Python code here
        result = params.get('input', 'No input provided')
        return f"Processed: {result}"
  timeout: 30
  dependencies: []
  parameters:
    - name: input
      type: string
      description: Input value for processing
      required: true
`;
}

/**
 * Get tool template by type
 */
export function getToolTemplate(
  name: string,
  type: 'KustoTool' | 'LinkTool' | 'PythonTool',
  options?: {
    connector?: string;
    database?: string;
    template?: string;
  }
): string {
  switch (type) {
    case 'KustoTool':
      return getKustoToolTemplate(name, options?.connector, options?.database);
    case 'LinkTool':
      return getLinkToolTemplate(name, options?.template);
    case 'PythonTool':
      return getPythonToolTemplate(name);
    default:
      return getKustoToolTemplate(name);
  }
}
