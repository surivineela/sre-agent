// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Models.Attributes;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Reflection;

namespace FirstPartyAgent.Core.Helpers
{
    public static class AgentFinder
    {
        private static Dictionary<string, List<string>> AgentPluginsConfig = new Dictionary<string, List<string>>()
        {
            { "None", new List<string>(){ "KustoPlugin", "TimePlugin", "HttpRequestPlugin" } },
            { "Sev2", new List<string>(){ "KustoPlugin", "IcmPlugin", "GenevaActionsPlugin", "ICMChartPlugin", "WebAppPlugin", "AzureAlertingPlugin", "TimePlugin", "HttpRequestPlugin" } },
            { "ICMAgent", new List<string>(){ "KustoPlugin", "IcmPlugin", "GenevaActionsPlugin", "ICMChartPlugin", "AzureAlertingPlugin" } },
            { "MFP", new List<string>(){ "IcmPlugin", "GenevaActionsPlugin", "KustoPlugin", "TeamsPlugin" } },
            { "GithubIssueTagger", new List<string>() { "GitHubIssuePlugin", "AzureSearchPlugin" } },
            { "ICMSummarizer", new List<string>(){ "IcmPlugin" } }
        };

        public static Dictionary<string, List<string>> AgentDataParsingConfig = new Dictionary<string, List<string>>()
        {
            { "Hotsite", new List<string>(){ "IncidentId" } },
            { "Sev2", new List<string>(){ "IncidentId" } },
            { "ICMAgent", new List<string>(){ "IncidentId" } },
            { "MFP", new List<string>(){ "IncidentId" } },
            { "GithubIssueTagger", new List<string>(){ "IssueId", "CommentId" } }
        };

        public static List<string> ListAgentModes()
        {
            var allowedAgentModes = new List<string>() { "ICMAgent", "Sev2", "ICMSummarizer" };
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

            // Get all static classes in the specified namespace.
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
    }

}

