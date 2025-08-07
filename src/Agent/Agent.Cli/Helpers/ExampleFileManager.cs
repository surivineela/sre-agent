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
        var yaml = @"name: example_agent

system_prompt: |
  You are an example SRE agent designed to demonstrate the capabilities of the SREAgent system.
  You can help with basic incident management and provide guidance on SRE best practices.
  Always be helpful, professional, and focused on solving operational problems.

handoff_description: Use this agent for general SRE tasks and as an example of agent configuration.

handoffs:
  - meta_agent

tools:
  - example_tool

max_reflection_count: 0
allow_parallel_tool_calls: false
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
}
