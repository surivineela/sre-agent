// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using FirstPartyAgent.AgentPrompts;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Models.Attributes;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FirstPartyAgent.Core.Helpers
{
    // Simple class to read YAML agent configuration
    public class YamlAgentConfig
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "system_prompt")]
        public string SystemPrompt { get; set; } = string.Empty;

        [YamlMember(Alias = "handoff_description")]
        public string? HandoffDescription { get; set; }

        [YamlMember(Alias = "tools")]
        public List<string>? Tools { get; set; }

        [YamlMember(Alias = "handoffs")]
        public List<string>? Handoffs { get; set; }

        [YamlMember(Alias = "common_prompts")]
        public List<string>? CommonPrompts { get; set; }

        [YamlMember(Alias = "max_reflection_count")]
        public int? MaxReflectionCount { get; set; }
    }

    public static class AgentFinder
    {
        private static Dictionary<string, List<string>> AgentPluginsConfig = new Dictionary<string, List<string>>()
        {
            { "None", new List<string>(){ "AzureSearchPlugin" } },
            { "DevOpsAgent", new List<string>() { "KustoPlugin", "TimePlugin", "HttpRequestPlugin", "AzureDevOpsPlugin" } },
            { "ControlPlane", new List<string>(){ "ControlPlanePlugin", "ICMChartPlugin", "IcmPlugin", "KustoPlugin", "TimePlugin", "WebAppPlugin" } },
            { "Sev2", new List<string>(){ "KustoPlugin", "IcmPlugin", "GenevaActionsPlugin", "ICMChartPlugin", "WebAppPlugin", "AzureAlertingPlugin", "TimePlugin", "HttpRequestPlugin" } },
            { "TestModeAgent", new List<string>(){ "KustoPlugin", "IcmPlugin", "GenevaActionsPlugin", "ICMChartPlugin", "WebAppPlugin", "AzureAlertingPlugin", "TimePlugin", "HttpRequestPlugin" } },
            { "ICMAgent", new List<string>(){ "KustoPlugin", "IcmPlugin", "GenevaActionsPlugin", "ICMChartPlugin", "AzureAlertingPlugin", "AzureSearchPlugin", "ApplensDetectorPlugin" } },
            { "MFP", new List<string>(){ "IcmPlugin", "GenevaActionsPlugin", "KustoPlugin", "TeamsPlugin" } },
            { "GithubIssueTagger", new List<string>() { "GitHubIssuePlugin", "AzureSearchPlugin" } },
            { "ICMSummarizer", new List<string>(){ "IcmPlugin" } },
            { "ColdStart", new List<string>(){ "ColdStartPlugin", "TeamsChartPlugin" } },
            { "ACIToLegionMigration", new List<string>(){ "ATLPlugin" } },
            { "ICMCorrelationAgent", new List<string>(){ "KustoPlugin", "IcmPlugin", "AzureAlertingPlugin", "HandoffToAgentPlugin" }  },
            { "ICMTriagerAgent", new List<string>(){ "KustoPlugin", "IcmPlugin", "AzureAlertingPlugin", "HandoffToAgentPlugin" } },
            { "EmergingIssue", new List<string>() { "KustoPlugin", "IcmPlugin", "EmergingIssuePlugin", "AzureAlertingPlugin" } },
            { "EmergingIssueManager" , new List<string>() { "EmergingIssueManagerPlugin", "AzureAlertingPlugin" } }
        };

        public static Dictionary<string, List<string>> AgentDataParsingConfig = new Dictionary<string, List<string>>()
        {
            { "ControlPlane", new List<string>(){ "IncidentId" } },
            { "Hotsite", new List<string>(){ "IncidentId" } },
            { "Sev2", new List<string>(){ "IncidentId" } },
            { "ICMAgent", new List<string>(){ "IncidentId" } },
            { "MFP", new List<string>(){ "IncidentId" } },
            { "GithubIssueTagger", new List<string>(){ "IssueId", "CommentId" } },
            { "ICMCorrelationAgent", new List<string>() {"IncidentId" } },
            { "ICMTriagerAgent", new List<string>() {"IncidentId" } },
            { "EmergingIssue", new List<string>(){ "IncidentId" } },
            { "EmergingIssueManager", new List<string>(){ "IncidentId" } }
        };

        public static List<string> ListAgentModes()
        {
            var allowedAgentModes = new List<string>() { "None", "ColdStart", "DevOpsAgent", "ControlPlane", "ICMAgent", "Sev2", "ICMSummarizer", "ICMCorrelationAgent", "ICMTriagerAgent", "EmergingIssue", "EmergingIssueManager", "ACIToLegionMigration" };
            return Enum.GetNames(typeof(AgentMode)).Where(x => allowedAgentModes.Contains(x)).ToList();
        }

        public static List<string> GetAgentPlugins(string agentMode)
        {
            if (AgentPluginsConfig.TryGetValue(agentMode, out var plugins))
            {
                return plugins;
            }
            return new List<string>();
        }

        public static List<AgentPromptModel> GetAgentPrompts(AgentMode mode)
        {
            var results = new List<AgentPromptModel>();

            // First try to load from YAML file
            var yamlResult = TryLoadAgentFromYaml(mode);
            if (yamlResult != null)
            {
                results.Add(yamlResult);
                return results;
            }

            // Fallback to reflection-based approach for C# classes
            var types = Assembly.GetExecutingAssembly().GetTypes()
                        .Where(t => t.Namespace == "FirstPartyAgent.AgentPrompts"
                                    && t.IsClass
                                    && t.IsAbstract
                                    && t.IsSealed); // static classes are both abstract and sealed

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<AgentPromptAttribute>();
                // Use equality check instead of HasFlag
                if (attr != null && attr.AgentMode == mode)
                {
                    // Retrieve the public static field "SystemMessage".
                    var field = type.GetField("SystemMessage", BindingFlags.Public | BindingFlags.Static);
                    if (field != null)
                    {
                        var systemMessage = field.GetValue(null) as string;
                        results.Add(new AgentPromptModel(type.Name, attr.Description, systemMessage));
                    }
                }
            }

            return results;
        }

        private static AgentPromptModel? TryLoadAgentFromYaml(AgentMode mode)
        {
            try
            {
                var agentName = $"{mode}Agent";
                var yamlFileName = $"{agentName}.yaml";

                // Try multiple possible paths for the YAML file
                var possiblePaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "AgentsV2", "ACA-FirstParty", yamlFileName),
                    Path.Combine(AppContext.BaseDirectory, "..", "Agent.Runtime", "AgentsV2", "ACA-FirstParty", yamlFileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "AgentsV2", "ACA-FirstParty", yamlFileName)
                };

                string yamlPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        yamlPath = path;
                        break;
                    }
                }

                if (yamlPath != null)
                {
                    var yamlContent = File.ReadAllText(yamlPath);
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .Build();

                    var yamlAgent = deserializer.Deserialize<YamlAgentConfig>(yamlContent);

                    // Use the agent name from YAML if available, otherwise construct from mode
                    var displayName = !string.IsNullOrEmpty(yamlAgent.Name) ? yamlAgent.Name : agentName;

                    // Provide a default handoff description if not specified
                    var handoffDescription = yamlAgent.HandoffDescription ?? GetDefaultHandoffDescription(mode);

                    return new AgentPromptModel(
                        displayName,
                        handoffDescription,
                        yamlAgent.SystemPrompt
                    );
                }
            }
            catch (Exception ex)
            {
                // Log error and continue with fallback approach
                Console.WriteLine($"Error reading YAML file for {mode} agent: {ex.Message}");
            }

            return null;
        }

        private static string GetDefaultHandoffDescription(AgentMode mode)
        {
            return mode switch
            {
                AgentMode.ColdStart => "This is the SRE Agent that helps with Functions Consumption Cold Start regressions and troubleshooting.",
                AgentMode.DevOpsAgent => "This agent helps with Azure DevOps operations and troubleshooting.",
                AgentMode.ControlPlane => "This agent helps with control plane operations and incident management.",
                AgentMode.ICMAgent => "This agent helps with ICM incident management and troubleshooting.",
                AgentMode.Sev2 => "This agent helps with Sev2 incident management and resolution.",
                AgentMode.ICMSummarizer => "This agent helps with summarizing ICM incidents.",
                AgentMode.ICMCorrelationAgent => "This agent helps with correlating ICM incidents.",
                AgentMode.ICMTriagerAgent => "This agent helps with triaging ICM incidents.",
                AgentMode.EmergingIssue => "This agent helps with emerging issue detection and management.",
                AgentMode.EmergingIssueManager => "This agent helps with managing emerging issues.",
                AgentMode.ACIToLegionMigration => "This agent helps with ACI to Legion migration tasks.",
                _ => $"This is the {mode} agent that provides specialized assistance."
            };
        }
    }

}

