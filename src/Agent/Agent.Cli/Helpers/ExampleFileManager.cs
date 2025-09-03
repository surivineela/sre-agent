using System.Reflection;

namespace Agent.Cli.Helpers;

/// <summary>
/// Manages the creation and copying of example files for the CLI.
/// </summary>
public static class ExampleFileManager
{
    /// <summary>
    /// Creates minimal example agent and tool files.
    /// </summary>
    public static async Task CopyExampleFilesAsync()
    {
        try
        {
            // Get the directory where the CLI is running from
            var executableDir = AppContext.BaseDirectory;
            var templatesDir = Path.Combine(executableDir, "templates");

            // If templates directory doesn't exist, create the examples inline
            if (!Directory.Exists(templatesDir))
            {
                await CreateMinimalExampleAgentAsync();
                await CreateMinimalExampleToolAsync();
                return;
            }

            // Copy example agent
            var exampleAgentSource = Path.Combine(templatesDir, "example_agent.yaml");
            var exampleAgentDest = Path.Combine("agents", "example_agent.yaml");
            if (File.Exists(exampleAgentSource))
            {
                await File.WriteAllTextAsync(exampleAgentDest, await File.ReadAllTextAsync(exampleAgentSource));
            }
            else
            {
                await CreateMinimalExampleAgentAsync();
            }

            // Copy example tool
            var exampleToolSource = Path.Combine(templatesDir, "example_tool.yaml");
            var exampleToolDest = Path.Combine("tools", "example_tool.yaml");
            if (File.Exists(exampleToolSource))
            {
                await File.WriteAllTextAsync(exampleToolDest, await File.ReadAllTextAsync(exampleToolSource));
            }
            else
            {
                await CreateMinimalExampleToolAsync();
            }
        }
        catch
        {
            // Fallback to minimal examples
            await CreateMinimalExampleAgentAsync();
            await CreateMinimalExampleToolAsync();
        }
    }

    /// <summary>
    /// Creates a minimal example agent file that matches AgentDescriptor structure.
    /// </summary>
    private static async Task CreateMinimalExampleAgentAsync()
    {
        var yaml = @"api_version: azuresre.ai/v1
kind: AgentConfiguration
metadata: {}
spec:
  name: example_agent
  system_prompt: |
    You are an example SRE agent designed to demonstrate the capabilities of the SREAgent system.
    You can help with basic incident management and provide guidance on SRE best practices.
    Always be helpful, professional, and focused on solving operational problems.
  handoff_description: Use this agent for general SRE tasks and as an example of agent configuration.
  handoffs:
    - meta_agent
  tools:
    - example_tool
  connectors: []
  allow_parallel_tool_calls: false
  max_reflection_count: 0
  critic_on_handoff: false
  custom_reflection_note: ''
  disable_document_retrieval: false
  enable_handoff_prompt_override: false
  temperature: 
  disable_common_prompts: false
  agent_type: Autonomous
  meta_data: {}
";

        var outputPath = Path.Combine("agents", "example_agent.yaml");
        Directory.CreateDirectory("agents");
        await File.WriteAllTextAsync(outputPath, yaml);
    }

    /// <summary>
    /// Creates a minimal example tool file that matches ToolDescriptor structure.
    /// </summary>
    private static async Task CreateMinimalExampleToolAsync()
    {
        var yaml = @"name: example_tool
type: KustoTool
connector: example_connector
description: An example tool that demonstrates how to create tools for SRE agents.
mode: query
database: example_database
query: |
  MyTable
  | where TimeGenerated > ago(1h)
  | summarize count() by OperationName
parameters:
  - name: timeRange
    type: string
    required: true
    description: Time range for the query
    map_to: args
    target: dictionary:args:string
";

        var outputPath = Path.Combine("tools", "example_tool.yaml");
        Directory.CreateDirectory("tools");
        await File.WriteAllTextAsync(outputPath, yaml);
    }

    /// <summary>
    /// Creates a sample agent that demonstrates using Kusto tools for impact analysis.
    /// </summary>
    private static async Task CreateKustoImpactAnalysisAgentAsync()
    {
        var yaml = @"api_version: azuresre.ai/v1
kind: AgentConfiguration
metadata: {}
spec:
  name: resource_impact_analyst
  system_prompt: |
  You are a Resource Impact Analysis Expert Agent specializing in comprehensive assessment of resource impact scenarios across subscriptions and tenants.

  ## YOUR ROLE AND CAPABILITIES
  
  **Primary Function**: Help users identify and analyze resource impacts using advanced Kusto queries and provide clear, actionable guidance.

  **CRITICAL DATA PRESENTATION RULES**:
  - **NEVER truncate data tables or use ellipses (""..."") when displaying tool results**
  - **ALWAYS show ALL rows returned by tools - every single affected resource must be visible**
  - **Complete data transparency is essential for impact assessment**

  ## RESPONSE BEHAVIOR
  
  - For general questions about resource impacts, provide clear guidance and ask which scenario needs analysis
  - For specific impact queries, use the CheckResourceImpact tool to provide comprehensive analysis
  - Always emphasize that immediate action is required when impacts are found
  - Continue until user's query is completely resolved

  ## WHEN DATA IS RETURNED FROM TOOLS
  
  **Format Results Properly**:
  1. Present results in clear table format showing ALL rows
  2. Group results by Scenario type and count affected resources
  3. Emphasize immediate action requirements
  4. Provide scenario-specific guidance and next steps
  5. NEVER truncate results - show every affected resource

  **Sample Response Format**:
  ```
  ## 📊 RESOURCE IMPACT DETECTED - IMMEDIATE ACTION REQUIRED

  Your subscription/tenant has **X** affected resources across multiple scenarios:

  ### Scenario 1 Resources:
  [TABLE WITH ALL ROWS]

  ### Scenario 2 Resources: 
  [TABLE WITH ALL ROWS]

  **Next Steps**: [Specific actions needed]
  ```

  ## TOOL USAGE
  - Use CheckResourceImpact when users provide subscription IDs or tenant IDs
  - Always include comprehensive impact data in responses
  - Cross-reference different scenarios for complete assessment

  Remember: Your expertise is in translating complex impact data into clear, actionable recommendations for users.
  handoff_description: Use this agent for comprehensive resource impact analysis using Kusto tools and data presentation.
  handoffs:
    - meta_agent
  tools:
    - CheckResourceImpact
  connectors: []
  allow_parallel_tool_calls: false
  max_reflection_count: 2
  critic_on_handoff: false
  custom_reflection_note: ''
  disable_document_retrieval: false
  enable_handoff_prompt_override: false
  temperature: 0.3
  disable_common_prompts: false
  agent_type: Autonomous
  meta_data: {}
";

        var outputPath = Path.Combine("agents", "resource_impact_analyst.yaml");
        Directory.CreateDirectory("agents");
        await File.WriteAllTextAsync(outputPath, yaml);
    }

    /// <summary>
    /// Creates additional advanced example files (called optionally during init).
    /// </summary>
    public static async Task CreateAdvancedExampleFilesAsync()
    {
        await CreateKustoImpactAnalysisAgentAsync();
    }
}
