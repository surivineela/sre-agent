// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for working with ExtendedAgentV2 objects.
/// </summary>
public static class ExtendedAgentHelper
{
    /// <summary>
    /// Formats a list of ExtendedAgentV2 objects into a console-friendly string representation.
    /// </summary>
    /// <param name="agents">The list of agents to format.</param>
    /// <returns>A formatted string suitable for console output.</returns>
    public static string FormatAgentList(List<ExtendedAgentV2> agents)
    {
        if (agents == null || agents.Count == 0)
        {
            return "\nNo agents found on the server.";
        }

        var agentList = new List<string>();

        foreach (var agent in agents)
        {
            var agentOutput = ConsoleUI.CaptureOutput(() =>
            {
                Console.WriteLine();
                ConsoleUI.WriteBullet(agent.Metadata.Name ?? "Unknown", ConsoleColor.White, 0);

                if (!string.IsNullOrEmpty(agent.Spec.HandoffDescription))
                {
                    ConsoleUI.WriteKeyValue("  Description", agent.Spec.HandoffDescription, 13, ConsoleColor.Gray, ConsoleColor.White);
                }

                if (!string.IsNullOrEmpty(agent.Spec.Instructions))
                {
                    // Truncate instructions if too long for display
                    var displayPrompt = agent.Spec.Instructions.Length > 100
                        ? agent.Spec.Instructions.Substring(0, 100) + "..."
                        : agent.Spec.Instructions;
                    ConsoleUI.WriteKeyValue("  Instructions", displayPrompt, 13, ConsoleColor.Gray, ConsoleColor.White);
                }

                // Get tools
                if (agent.Spec.Tools != null && agent.Spec.Tools.Count > 0)
                {
                    ConsoleUI.WriteKeyValue("  Tools", string.Join(", ", agent.Spec.Tools), 13, ConsoleColor.Gray, ConsoleColor.White);
                }

                // Get handoffs
                if (agent.Spec.Handoffs != null && agent.Spec.Handoffs.Count > 0)
                {
                    ConsoleUI.WriteKeyValue("  Handoffs", string.Join(", ", agent.Spec.Handoffs), 13, ConsoleColor.Gray, ConsoleColor.White);
                }
            });
            agentList.Add(agentOutput);
        }

        agentList.Add($"\nTotal: {agents.Count} agent(s)");

        return string.Join("\n", agentList);
    }

    /// <summary>
    /// Finds an agent YAML file by searching recursively under the agents directory.
    /// Supports flexible folder organization.
    /// </summary>
    /// <param name="agentName">The name of the agent to find</param>
    /// <returns>The full path to the agent YAML file, or null if not found</returns>
    public static string? FindAgentFile(string agentName)
    {
        const string agentsDir = "agents";
        if (!Directory.Exists(agentsDir))
        {
            return null;
        }

        // First, try the legacy structure: agents/{agentName}/{agentName}.yaml
        var legacyPath = Path.Combine(agentsDir, agentName, $"{agentName}.yaml");
        if (File.Exists(legacyPath))
        {
            return legacyPath;
        }

        // Then try the flat structure: agents/{agentName}.yaml
        var flatPath = Path.Combine(agentsDir, $"{agentName}.yaml");
        if (File.Exists(flatPath))
        {
            return flatPath;
        }

        // Finally, search recursively for any YAML file with the matching agent name
        var yamlFiles = Directory.GetFiles(agentsDir, "*.yaml", SearchOption.AllDirectories);

        foreach (var file in yamlFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Equals(agentName, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>
    /// Detects the YAML API version of an agent file.
    /// Returns: YamlApiVersion.V1 ("agent.platform.ai/v1") with Kind "AgentConfiguration",
    ///          YamlApiVersion.V2 ("azuresre.ai/v2") with Kind "ExtendedAgent",
    ///          or null if unsupported/invalid version
    /// </summary>
    public static YamlApiVersion? DetectVersion(string yamlContent)
    {
        try
        {
            // Try to deserialize as ResourceModel to get Kind and ApiVersion
            var deserializer = ResourceModel.GetDeserializerBuilder().Build();

            var resourceModel = deserializer.Deserialize<ResourceModel>(yamlContent);

            if (resourceModel == null)
            {
                return null;
            }

            // Check ApiVersion and Kind combination
            // V2: api_version: "azuresre.ai/v2" + kind: "ExtendedAgent"
            if (string.Equals(resourceModel.Kind, "ExtendedAgent", StringComparison.OrdinalIgnoreCase))
            {
                var version = YamlApiVersion.Parse(resourceModel.ApiVersion);
                return version;
            }

            // V1: api_version: "agent.platform.ai/v1" + kind: "AgentConfiguration"
            if (string.Equals(resourceModel.Kind, "AgentConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                var version = YamlApiVersion.Parse(resourceModel.ApiVersion);
                return version;
            }

            // Not a recognized agent format
            return null;
        }
        catch
        {
            // If parsing fails, return null (invalid)
            return null;
        }
    }
}
