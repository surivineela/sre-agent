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

            // NoContent (204) means the item was not found - this is a success case
            if (statusCode == HttpStatusCode.NoContent)
            {
                return (true, $"Tool '{toolName}' does not exist (already deleted or never created)");
            }

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

                return (false, errorMessage);
            }

            return (true, $"Tool '{toolName}' deleted successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete tool: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ApplyExtendedToolAsync(ExtendedToolV2 tool, bool dryRun = false)
    {
        try
        {
            var toolName = tool.Metadata?.Name;
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return (false, "Tool name is required in metadata.");
            }

            // Serialize spec to JSON node with camelCase properties
            var propertiesNode = JsonSerializer.SerializeToNode(tool.Spec, _camelCaseJsonOptions);

            // Build the API request envelope in camelCase
            var requestBody = new
            {
                name = toolName,
                type = ResourceModel.ResourceKind.ExtendedAgentToolV2,
                tags = tool.Metadata?.Tags ?? new List<string>(),
                owner = tool.Metadata?.Owner,
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

    public async Task<(bool Success, string Message)> ApplyExtendedAgentAsync(ExtendedAgentV2 agent, bool dryRun = false)
    {
        try
        {
            var agentName = agent.Metadata?.Name;
            if (string.IsNullOrWhiteSpace(agentName))
            {
                return (false, "Agent name is required in metadata.");
            }

            // Serialize spec to JSON node with camelCase properties
            var propertiesNode = JsonSerializer.SerializeToNode(agent.Spec, _camelCaseJsonOptions);

            // Build the API request envelope in camelCase
            var requestBody = new
            {
                name = agent.Metadata?.Name ?? agentName,
                type = ResourceModel.ResourceKind.ExtendedAgentV2,
                tags = agent.Metadata?.Tags ?? new List<string>(),
                owner = agent.Metadata?.Owner,
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

            // Call the overload that takes ExtendedAgentV2
            return await ApplyExtendedAgentAsync(agent, dryRun);
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to apply agent: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteExtendedAgentAsync(string agentName, bool dryRun = false)
    {
        try
        {
            // Use V2 API endpoint: DELETE /api/v2/extendedAgent/agents/{agentName}
            var relativeUrl = $"api/v2/extendedAgent/agents/{Uri.EscapeDataString(agentName)}";
            if (dryRun)
            {
                relativeUrl += "?dryRun=true";
            }

            var (content, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(HttpMethod.Delete, relativeUrl);

            // NoContent (204) means the item was not found - this is a success case
            if (statusCode == HttpStatusCode.NoContent)
            {
                return (true, $"Agent '{agentName}' does not exist (already deleted or never created)");
            }

            if (errorMessage != null)
            {
                return (false, errorMessage);
            }

            return (true, $"✅ Agent '{agentName}' deleted successfully!");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete agent: {ex.Message}");
        }
    }

    #endregion

    #region Extended API for Common Prompts

    public async Task<(List<CommonPromptV2> Result, string? Error)> ListCommonPromptsAsync(string? search = null)
    {
        var resultList = new List<CommonPromptV2>();

        try
        {
            var relativeUrl = "api/v2/extendedAgent/commonprompts";
            if (!string.IsNullOrWhiteSpace(search))
            {
                relativeUrl += $"?search={Uri.EscapeDataString(search)}";
            }

            var (promptsEnvelope, statusCode, errorMessage) = await MakeHttpRequestAsync<ApiCollectionEnvelope<CommonPromptSpecV2>>(HttpMethod.Get, relativeUrl);

            if (errorMessage != null)
            {
                return (resultList, errorMessage);
            }

            if (promptsEnvelope?.Value == null || promptsEnvelope.Value.Count == 0)
            {
                return (resultList, null);
            }

            // Convert envelopes to CommonPromptV2
            foreach (var envelope in promptsEnvelope.Value)
            {
                if (envelope.Properties == null)
                {
                    continue;
                }

                // Construct CommonPromptV2 from envelope
                var commonPrompt = new CommonPromptV2
                {
                    Metadata = new ResourceMetadataModel
                    {
                        Name = envelope.Name,
                        Owner = envelope.Owner,
                        Tags = envelope.Tags ?? new List<string>()
                    },
                    Spec = envelope.Properties
                };

                resultList.Add(commonPrompt);
            }

            return (resultList, null);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ListCommonPrompts failed: {ex.Message}");
            return (resultList, $"Failed to list common prompts: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteCommonPromptAsync(string promptName, bool dryRun = false)
    {
        try
        {
            // Use V2 API endpoint: DELETE /api/v2/extendedAgent/commonprompts/{promptName}
            var relativeUrl = $"api/v2/extendedAgent/commonprompts/{Uri.EscapeDataString(promptName)}";
            if (dryRun)
            {
                relativeUrl += "?dryRun=true";
            }

            var (content, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(HttpMethod.Delete, relativeUrl);

            // NoContent (204) means the item was not found - this is a success case
            if (statusCode == HttpStatusCode.NoContent)
            {
                return (true, $"Common prompt '{promptName}' does not exist (already deleted or never created)");
            }

            if (errorMessage != null)
            {
                // Check specific status codes for better error messages
                if (statusCode == HttpStatusCode.Conflict)
                {
                    // Parse conflict response to show dependent resources
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
                            return (false, $"Cannot delete common prompt '{promptName}': it is used by the following agents: {agentList}");
                        }

                        if (conflictData.TryGetProperty("message", out var messageElement))
                        {
                            return (false, messageElement.GetString() ?? $"Conflict deleting common prompt '{promptName}'");
                        }
                    }
                    catch
                    {
                        // Fall back to generic conflict message if parsing fails
                    }

                    return (false, $"Cannot delete common prompt '{promptName}': it is being used by agents");
                }

                return (false, errorMessage);
            }

            return (true, $"Common prompt '{promptName}' deleted successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete common prompt: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ApplyCommonPromptAsync(CommonPromptV2 prompt, bool dryRun = false)
    {
        try
        {
            var promptName = prompt.Metadata.Name;
            if (string.IsNullOrWhiteSpace(promptName))
            {
                return (false, "Common prompt name is required in metadata.");
            }

            // Serialize spec to JSON node with camelCase properties
            var propertiesNode = JsonSerializer.SerializeToNode(prompt.Spec, _camelCaseJsonOptions);

            // Build the API request envelope in camelCase
            var requestBody = new
            {
                name = prompt.Metadata.Name,
                type = ResourceModel.ResourceKind.CommonPromptV2,
                tags = prompt.Metadata.Tags ?? new List<string>(),
                owner = prompt.Metadata.Owner,
                properties = propertiesNode
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, _camelCaseJsonOptions);

            // Use V2 API endpoint: PUT /api/v2/extendedAgent/commonprompts/{promptName}
            var dryRunQuery = dryRun ? "?dryRun=true" : "";
            var relativeUrl = $"api/v2/extendedAgent/commonprompts/{Uri.EscapeDataString(promptName)}{dryRunQuery}";

            var (responseContent, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(HttpMethod.Put, relativeUrl, jsonContent);

            if (errorMessage == null)
            {
                var message = dryRun
                    ? $"✅ Common prompt '{promptName}' validated successfully (dry run)"
                    : $"✅ Common prompt '{promptName}' applied successfully!";
                return (true, message);
            }
            else
            {
                return (false, $"❌ Failed to apply common prompt: {statusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to apply common prompt: {ex.Message}");
        }
    }

    #endregion

    #region Extended API for Skills

    public async Task<(List<ExtendedSkillV2> Result, string? Error)> ListExtendedSkillsAsync(string? search = null)
    {
        var resultList = new List<ExtendedSkillV2>();

        try
        {
            var relativeUrl = "api/v2/extendedAgent/skills";
            if (!string.IsNullOrWhiteSpace(search))
            {
                relativeUrl += $"?search={Uri.EscapeDataString(search)}";
            }

            var (skillsEnvelope, statusCode, errorMessage) = await MakeHttpRequestAsync<ApiCollectionEnvelope<SkillSpecV2>>(HttpMethod.Get, relativeUrl);

            if (errorMessage != null)
            {
                return (resultList, errorMessage);
            }

            if (skillsEnvelope?.Value == null || skillsEnvelope.Value.Count == 0)
            {
                return (resultList, null);
            }

            // Convert envelopes to ExtendedSkillV2
            foreach (var envelope in skillsEnvelope.Value)
            {
                if (envelope.Properties == null)
                {
                    continue;
                }

                // Construct ExtendedSkillV2 from envelope
                var extendedSkill = new ExtendedSkillV2
                {
                    Metadata = new ResourceMetadataModel
                    {
                        Name = envelope.Name,
                        Owner = envelope.Owner,
                        Tags = envelope.Tags ?? new List<string>()
                    },
                    Spec = envelope.Properties
                };

                resultList.Add(extendedSkill);
            }

            return (resultList, null);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ListExtendedSkills failed: {ex.Message}");
            return (resultList, $"Failed to list skills: {ex.Message}");
        }
    }

    public async Task<(ExtendedSkillV2? Result, string? Error)> GetExtendedSkillAsync(string skillName)
    {
        try
        {
            var relativeUrl = $"api/v2/extendedAgent/skills/{Uri.EscapeDataString(skillName)}";

            var (skillEnvelope, statusCode, errorMessage) = await MakeHttpRequestAsync<ApiEnvelope<SkillSpecV2>>(HttpMethod.Get, relativeUrl);

            // NotFound (404) means the skill doesn't exist
            if (statusCode == HttpStatusCode.NotFound)
            {
                return (null, $"Skill '{skillName}' not found");
            }

            if (errorMessage != null)
            {
                return (null, errorMessage);
            }

            if (skillEnvelope?.Properties == null)
            {
                return (null, "Invalid response from server");
            }

            // Convert envelope to ExtendedSkillV2
            var skill = new ExtendedSkillV2
            {
                Metadata = new ResourceMetadataModel
                {
                    Name = skillEnvelope.Name,
                    Owner = skillEnvelope.Owner,
                    Tags = skillEnvelope.Tags ?? new List<string>()
                },
                Spec = skillEnvelope.Properties
            };

            return (skill, null);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"GetExtendedSkill failed: {ex.Message}");
            return (null, $"Failed to get skill: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteExtendedSkillAsync(string skillName, bool dryRun = false)
    {
        try
        {
            // Use V2 API endpoint: DELETE /api/v2/extendedAgent/skills/{skillName}
            var relativeUrl = $"api/v2/extendedAgent/skills/{Uri.EscapeDataString(skillName)}";
            if (dryRun)
            {
                relativeUrl += "?dryRun=true";
            }

            var (content, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(HttpMethod.Delete, relativeUrl);

            // NoContent (204) means the item was not found - this is a success case
            if (statusCode == HttpStatusCode.NoContent)
            {
                return (true, $"Skill '{skillName}' does not exist (already deleted or never created)");
            }

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
                            return (false, $"Cannot delete skill '{skillName}': it is used by the following agents: {agentList}");
                        }

                        if (conflictData.TryGetProperty("message", out var messageElement))
                        {
                            return (false, messageElement.GetString() ?? $"Conflict deleting skill '{skillName}'");
                        }
                    }
                    catch
                    {
                        // Fall back to generic conflict message if parsing fails
                    }

                    return (false, $"Cannot delete skill '{skillName}': it is being used by agents");
                }

                return (false, errorMessage);
            }

            return (true, $"Skill '{skillName}' deleted successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete skill: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ApplyExtendedSkillAsync(ExtendedSkillV2 skill, bool dryRun = false)
    {
        try
        {
            var skillName = skill.Metadata?.Name;
            if (string.IsNullOrWhiteSpace(skillName))
            {
                return (false, "Skill name is required in metadata.");
            }

            // Serialize spec to JSON node with camelCase properties
            var propertiesNode = JsonSerializer.SerializeToNode(skill.Spec, _camelCaseJsonOptions);

            // Build the API request envelope in camelCase
            var requestBody = new
            {
                name = skillName,
                type = ResourceModel.ResourceKind.SkillV2,
                tags = skill.Metadata?.Tags ?? new List<string>(),
                owner = skill.Metadata?.Owner,
                properties = propertiesNode
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, _camelCaseJsonOptions);

            // Use V2 API endpoint: PUT /api/v2/extendedAgent/skills/{skillName}
            var dryRunQuery = dryRun ? "?dryRun=true" : "";
            var relativeUrl = $"api/v2/extendedAgent/skills/{Uri.EscapeDataString(skillName)}{dryRunQuery}";

            var (responseContent, statusCode, errorMessage) = await MakeHttpRequestAsync<string>(HttpMethod.Put, relativeUrl, jsonContent);

            if (errorMessage == null)
            {
                var message = dryRun
                    ? $"✅ Skill '{skillName}' validated successfully (dry run)"
                    : $"✅ Skill '{skillName}' applied successfully!";
                return (true, message);
            }
            else
            {
                return (false, $"❌ Failed to apply skill: {statusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to apply skill: {ex.Message}");
        }
    }

    #endregion
}

