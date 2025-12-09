// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Agent.Cli.Helpers;
using Agent.Cli.Models;

namespace Agent.Cli.Services;

public partial class ApiService : IDisposable
{
    #region Extended API for Tools

    public async Task<(List<ExtendedToolV2> Result, string? Error)> ListExtendedToolsAsync(string? search = null)
    {
        var resultList = new List<ExtendedToolV2>();

        try
        {
            var relativeUrl = "api/v2/extendedAgent/tools";
            if (!string.IsNullOrWhiteSpace(search))
            {
                relativeUrl += $"?search={Uri.EscapeDataString(search)}";
            }

            var (toolsEnvelope, statusCode, errorMessage) = await MakeHttpRequestAsync<ApiCollectionEnvelope<ToolSpecV2>>(HttpMethod.Get, relativeUrl);

            if (errorMessage != null)
            {
                return (resultList, errorMessage);
            }

            if (toolsEnvelope?.Value == null || toolsEnvelope.Value.Count == 0)
            {
                return (resultList, null);
            }

            // Convert envelopes to ExtendedToolV2
            foreach (var envelope in toolsEnvelope.Value)
            {
                if (envelope.Properties == null)
                {
                    continue;
                }

                // Construct ExtendedToolV2 from envelope
                var extendedTool = new ExtendedToolV2
                {
                    Metadata = new ResourceMetadataModel
                    {
                        Name = envelope.Name,
                        Owner = envelope.Owner,
                        Tags = envelope.Tags ?? new List<string>()
                    },
                    Spec = envelope.Properties
                };

                resultList.Add(extendedTool);
            }

            return (resultList, null);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ListExtendedTools failed: {ex.Message}");
            return (resultList, $"Failed to list tools: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteExtendedToolAsync(string toolName, bool dryRun = false)
    {
        try
        {
            // Use V2 API endpoint: DELETE /api/v2/extendedAgent/tools/{toolName}
            var relativeUrl = $"api/v2/extendedAgent/tools/{Uri.EscapeDataString(toolName)}";
            if (dryRun)
            {
                relativeUrl += "?dryRun=true";
            }

            var (content, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(HttpMethod.Delete, relativeUrl);

            if (errorMessage != null)
            {
                // Check specific status codes for better error messages
                if (statusCode == HttpStatusCode.Conflict)
                {
                    // Parse conflict response to show dependent agents
                    try
                    {
                        var conflictData = JsonSerializer.Deserialize<JsonElement>(content ?? "{}");
                        if (conflictData.TryGetProperty("dependentAgents", out var dependentAgentsElement))
                        {
                            var dependentAgents = dependentAgentsElement.EnumerateArray()
                                .Select(x => x.GetString())
                                .Where(x => !string.IsNullOrEmpty(x))
                                .ToList();

                            var agentList = dependentAgents.Count != 0 ? string.Join(", ", dependentAgents) : "unknown agents";
                            return (false, $"Cannot delete tool '{toolName}': it is used by the following agents: {agentList}");
                        }

                        if (conflictData.TryGetProperty("message", out var messageElement))
                        {
                            return (false, messageElement.GetString() ?? $"Conflict deleting tool '{toolName}'");
                        }
                    }
                    catch
                    {
                        // Fall back to generic conflict message if parsing fails
                    }

                    return (false, $"Cannot delete tool '{toolName}': it is being used by agents");
                }
                else if (statusCode == HttpStatusCode.NotFound)
                {
                    return (false, $"Tool '{toolName}' not found");
                }

                return (false, errorMessage);
            }

            return (true, $"Tool '{toolName}' deleted successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete tool: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ApplyExtendedToolAsync(string toolName, bool dryRun = false)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            // Find tool YAML file using flexible search
            var toolFilePath = ExtendedToolHelper.FindToolFile(toolName);
            if (toolFilePath == null)
            {
                return (false, $"Tool file not found for '{toolName}'. Searched in tools directory and subdirectories for '{toolName}.yaml'");
            }

            // Check tool version before attempting to apply
            var detectedVersion = ExtendedToolHelper.DetectVersion(toolFilePath);
            if (detectedVersion != YamlApiVersion.V2)
            {
                var versionName = detectedVersion == YamlApiVersion.V1 ? "V1" : "unknown";
                return (false, $"❌ Tool '{toolName}' is using {versionName} format and must be migrated to V2 before applying.\n" +
                              $"   Run: srectl tool migrate --name {toolName}\n" +
                              $"   Or:  srectl tool migrate --all");
            }

            // Read and parse the YAML file as ExtendedToolV2
            var tool = await ExtendedToolV2.LoadYamlAsync(toolFilePath);
            if (tool == null)
            {
                return (false, $"Failed to parse tool YAML file: {toolFilePath}");
            }

            // Serialize spec to JSON node with camelCase properties
            var propertiesNode = JsonSerializer.SerializeToNode(tool.Spec, _camelCaseJsonOptions);

            // Build the API request envelope in camelCase
            var requestBody = new
            {
                name = tool.Metadata.Name,
                type = "ExtendedAgentTool",
                tags = tool.Metadata.Tags ?? new List<string>(),
                owner = tool.Metadata.Owner,
                properties = propertiesNode
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, _camelCaseJsonOptions);

            // Use V2 API endpoint: PUT /api/v2/extendedAgent/tools/{toolName}
            var dryRunQuery = dryRun ? "?dryRun=true" : "";
            var relativeUrl = $"api/v2/extendedAgent/tools/{Uri.EscapeDataString(toolName)}{dryRunQuery}";

            var (responseContent, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(HttpMethod.Put, relativeUrl, jsonContent);

            if (errorMessage == null)
            {
                var message = dryRun
                    ? $"✅ Tool '{toolName}' validated successfully (dry run)"
                    : $"✅ Tool '{toolName}' applied successfully!";
                return (true, message);
            }
            else
            {
                return (false, $"❌ Failed to apply tool: {statusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to apply tool: {ex.Message}");
        }
    }

    #endregion

    #region Extended API for Agents

    public async Task<(List<ExtendedAgentV2> Result, string? Error)> ListExtendedAgentsAsync(string? search = null)
    {
        var resultList = new List<ExtendedAgentV2>();

        try
        {
            var relativeUrl = "api/v2/extendedAgent/agents";
            if (!string.IsNullOrWhiteSpace(search))
            {
                relativeUrl += $"?search={Uri.EscapeDataString(search)}";
            }

            var (agentsEnvelope, statusCode, errorMessage) = await MakeHttpRequestAsync<ApiCollectionEnvelope<ExtendedAgentSpecV2>>(HttpMethod.Get, relativeUrl);

            if (errorMessage != null)
            {
                return (resultList, errorMessage);
            }

            if (agentsEnvelope?.Value == null || agentsEnvelope.Value.Count == 0)
            {
                return (resultList, null);
            }

            // Convert envelopes to ExtendedAgentV2
            foreach (var envelope in agentsEnvelope.Value)
            {
                if (envelope.Properties == null)
                {
                    continue;
                }

                // Construct ExtendedAgentV2 from envelope
                var extendedAgent = new ExtendedAgentV2
                {
                    Metadata = new ResourceMetadataModel
                    {
                        Name = envelope.Name,
                        Owner = envelope.Owner,
                        Tags = envelope.Tags ?? new List<string>()
                    },
                    Spec = envelope.Properties
                };

                resultList.Add(extendedAgent);
            }

            return (resultList, null);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ListExtendedAgents failed: {ex.Message}");
            return (resultList, $"Failed to list agents: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ApplyExtendedAgentAsync(string agentName, bool dryRun = false)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            // Find agent YAML file using flexible search
            var agentFilePath = ExtendedAgentHelper.FindAgentFile(agentName);
            if (agentFilePath == null)
            {
                return (false, $"Agent file not found for '{agentName}'. Searched in agents directory and subdirectories for '{agentName}.yaml'");
            }

            // Read the agent YAML file
            var agentYamlContent = await File.ReadAllTextAsync(agentFilePath);

            // Detect YAML version
            var detectedVersion = ExtendedAgentHelper.DetectVersion(agentYamlContent);
            if (detectedVersion == null)
            {
                return (false, $"❌ Unsupported or invalid YAML version. Expected api_version: '{YamlApiVersion.V1}' or '{YamlApiVersion.V2}'");
            }

            // Load YAML and convert to V2 if necessary
            ExtendedAgentV2 agent;
            if (detectedVersion == YamlApiVersion.V1)
            {
                // V1: Deserialize to V1 model and convert to V2
                var v1Agent = ExtendedAgentV1.ParseYaml(agentYamlContent);
                if (v1Agent == null)
                {
                    return (false, "Failed to parse V1 agent YAML");
                }

                agent = Converters.ExtendedAgentConverter.ConvertToV2(v1Agent);

                ConsoleUI.WriteInfo($"⚠️  Warning: Agent YAML is using V1 format.", ConsoleColor.Yellow);
                ConsoleUI.WriteInfo($"   Consider migrating to V2 format by running:", ConsoleColor.Yellow);
                ConsoleUI.WriteInfo($"   'srectl agent sync' to download the latest V2 format from the server.", ConsoleColor.Yellow);
                Console.WriteLine();
            }
            else if (detectedVersion == YamlApiVersion.V2)
            {
                // V2: Deserialize directly
                agent = ExtendedAgentV2.ParseYaml(agentYamlContent);
                if (agent == null)
                {
                    return (false, "Failed to parse V2 agent YAML");
                }
            }
            else
            {
                // This should never happen due to the null check above, but satisfies the compiler
                return (false, $"Unsupported YAML version: {detectedVersion}");
            }

            // Serialize spec to JSON node with camelCase properties
            var propertiesNode = JsonSerializer.SerializeToNode(agent.Spec, _camelCaseJsonOptions);

            // Build the API request envelope in camelCase
            var requestBody = new
            {
                name = agent.Metadata.Name,
                type = "ExtendedAgent",
                tags = agent.Metadata.Tags ?? new List<string>(),
                owner = agent.Metadata.Owner,
                properties = propertiesNode
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, _camelCaseJsonOptions);

            // Use V2 API endpoint: PUT /api/v2/extendedAgent/agents/{agentName}
            var dryRunQuery = dryRun ? "?dryRun=true" : "";
            var relativeUrl = $"api/v2/extendedAgent/agents/{Uri.EscapeDataString(agentName)}{dryRunQuery}";

            var (responseContent, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(HttpMethod.Put, relativeUrl, jsonContent);

            if (errorMessage == null)
            {
                var message = dryRun
                    ? $"✅ Agent '{agentName}' validated successfully (dry run)"
                    : $"✅ Agent '{agentName}' applied successfully!";
                return (true, message);
            }
            else
            {
                return (false, $"❌ Failed to apply agent: {statusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to apply agent: {ex.Message}");
        }
    }

    #endregion
}

