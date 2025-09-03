using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Agent.Framework;

namespace Agent.Core.Helpers.ExtendedAgents;

/// <summary>
/// Shared helper class for parsing agent YAML files in both Kubernetes-style and flat formats.
/// This class provides unified logic that can be used by both client-side and server-side code.
/// </summary>
public static class AgentYamlParser
{
    /// <summary>
    /// Parses agent YAML content and returns the agent descriptor.
    /// Handles both Kubernetes-style format (with kind and spec) and flat format.
    /// </summary>
    /// <param name="yaml">The YAML content to parse</param>
    /// <returns>The parsed agent descriptor</returns>
    /// <exception cref="InvalidOperationException">Thrown when YAML cannot be parsed</exception>
    public static IAgentDescriptor? ParseAgentYaml(string yaml)
    {
        try
        {
            // Parse YAML using the same approach as the server
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlObject = deserializer.Deserialize(new StringReader(yaml));
            
            if (yamlObject == null)
            {
                throw new InvalidOperationException("YAML content is empty or invalid");
            }

            // Convert to dictionary to access properties
            var yamlDict = yamlObject as Dictionary<string, object> ?? 
                          throw new InvalidOperationException("YAML must be a valid object structure");

            // Check for kind field to determine format
            if (yamlDict.TryGetValue("kind", out var kindObj) && kindObj?.ToString() == "AgentConfiguration")
            {
                // Handle Kubernetes-style format (matches server logic)
                if (yamlDict.TryGetValue("spec", out var specObj) && specObj is Dictionary<string, object> spec)
                {
                    // Extract the spec directly as it contains the agent properties
                    var serializer = new SerializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .Build();
                    
                    var agentYaml = serializer.Serialize(spec);
                    
                    // Parse using AgentFactory
                    return AgentFactory<object>.LoadAgentFromYaml(agentYaml);
                }
                else
                {
                    throw new InvalidOperationException("AgentConfiguration must have spec structure");
                }
            }
            else
            {
                // Fallback: try parsing as flat format (direct agent descriptor)
                return AgentFactory<object>.LoadAgentFromYaml(yaml);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse YAML: '{yaml}' into AgentDescriptor: {ex.Message} ", ex);
        }
    }

    /// <summary>
    /// Extracts tool names from agent YAML content.
    /// Handles both Kubernetes-style format (spec.agent.tools) and flat format (tools).
    /// </summary>
    /// <param name="yaml">The YAML content</param>
    /// <returns>List of tool names referenced by the agent</returns>
    public static List<string> ExtractToolNames(string yaml)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yaml);
            if (yamlDict == null) return new List<string>();

            // Check for Kubernetes-style format
            if (yamlDict.TryGetValue("kind", out var kindObj) && kindObj?.ToString() == "AgentConfiguration")
            {
                // Extract from spec.tools (tools are directly in spec)
                if (yamlDict.TryGetValue("spec", out var specObj) && 
                    specObj is Dictionary<string, object> spec &&
                    spec.TryGetValue("tools", out var toolsObj) && 
                    toolsObj is List<object> toolsList)
                {
                    return toolsList.Cast<string>().ToList();
                }
            }
            else
            {
                // Extract from flat format (root level tools)
                if (yamlDict.TryGetValue("tools", out var toolsObj) && toolsObj is List<object> toolsList)
                {
                    return toolsList.Cast<string>().ToList();
                }
            }

            return new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Determines if the YAML content is in Kubernetes-style format.
    /// </summary>
    /// <param name="yaml">The YAML content to check</param>
    /// <returns>True if the YAML has Kubernetes-style structure with kind and spec</returns>
    public static bool IsKubernetesFormat(string yaml)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yaml);
            
            return yamlDict != null && 
                   yamlDict.TryGetValue("kind", out var kindObj) && 
                   kindObj?.ToString() == "AgentConfiguration" &&
                   yamlDict.ContainsKey("spec");
        }
        catch
        {
            return false;
        }
    }
}
