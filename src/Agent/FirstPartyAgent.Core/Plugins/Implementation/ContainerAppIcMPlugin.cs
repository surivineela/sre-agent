// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIcmAgent;
using Agent.Runtime.SubAgents;
using Agent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using System.Text.RegularExpressions;
using FirstPartyAgent.Models;

namespace FirstPartyAgent.Core.Plugins.Implementation;
public class ContainerAppIcMPlugin : IcmPlugin, IContainerAppIcMPlugin
{
    private readonly ILogger<ContainerAppIcMPlugin> _logger;
    private readonly IToolsRepository _toolsRepository;
    private readonly ITimePlugin _timePlugin;
    private readonly IContainerAppsPlugin _containerAppsPlugin;

    public ContainerAppIcMPlugin(
            IConfiguration config,
            ICMWorkflowClient icmAutomationClient,
            IChatClient chatClient,
            ILogger<ContainerAppIcMPlugin> logger,
            IToolsRepository toolsRepository,
            ITimePlugin timePlugin,
            IContainerAppsPlugin containerAppsPlugin) : base(config, icmAutomationClient, chatClient, logger)
    {
        _logger = logger;
        _toolsRepository = toolsRepository;
        _timePlugin = timePlugin;
        _containerAppsPlugin = containerAppsPlugin;
    }

    public async Task<string> GetInitialInvestigationReportAsync(string incidentId)
    {
        var incident = await GetIncidentInfo(incidentId);
        if (incident == null)
        {
            return "Incident not found.";
        }
        // TODO: fetcing discussion entries taking significant time.
        // var discussions = await GetDiscussionEntries(incidentId, queryFrom: DateTimeOffset.MinValue);
        var discussions = new List<DiscussionEntry>();
        if (discussions?.Count > 0)
        {
            discussions = [.. discussions.OrderByDescending(d => d.Date)]; // latest first
        }

        // Create a JSON string from the incident and discussions
        var json = new
        {
            details = incident,
            comments = discussions
        };

        var serializationOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull // Ignores null values
        };
        string incidentWithComments = JsonSerializer.Serialize(json, serializationOptions);

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartySubAgents), "ACA", nameof(ContainerAppIcmAgent), "ContainerAppIcmSummarizationPlan.txt");
        var summarizationPrompt = string.Empty;
        try
        {
            summarizationPrompt = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read summarizationPrompt from file at path: {Path}", path);
            throw;
        }

        var messages = new List<Microsoft.Extensions.AI.ChatMessage>();
        messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, summarizationPrompt));
        messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, incidentWithComments));

        
        var timePluginDefinition = new TimePluginDefinition(_timePlugin);
        var containerAppsPluginDefinition = new ContainerAppsPluginDefinition(_containerAppsPlugin);

        var toolSignatures = new List<string>
        {
            _toolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime),
            _toolsRepository.GetSignature(() => containerAppsPluginDefinition.GetSubscriptionDetail),
            _toolsRepository.GetSignature(() => containerAppsPluginDefinition.GetSubscriptionUsage),
        };
        var options = new ChatOptions
        {
            // Calling tools here is not working (ideally this can be light weight sub-agent after V2 implementation)
            // Tools = _toolsRepository.ResolveTools(toolSignatures),
            // ToolMode = ChatToolMode.Auto,
            Temperature = (float)0.2,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["response_format"] = "text"
            }
        };

        var response = await ChatClient.GetResponseAsync(messages, options);
        string summary = response.Text;

        _logger.LogInformation($"Created ICM summary for ICM ID {incidentId}");
        return summary;
    }

    public override async Task<Incident?> GetIncidentInfo(string incidentId)
    {
        Incident? incident = await _icmAutomationClient.GetIncidentAsync(incidentId);
        if (incident != null)
        {
            incident.DiscussionEntry = RemoveImageTags(incident.DiscussionEntry);
            incident.Summary = RemoveImageTags(incident.Summary);
        }
        return incident;
    }

    public override async Task<List<DiscussionEntry>?> GetDiscussionEntries(
        string incidentId,
        DateTimeOffset queryFrom)
    {
        List<DiscussionEntry> discussionEntries = await _icmAutomationClient.GetIncidentDiscussionEntriesAsync(incidentId, queryFrom);
        foreach (var discussionEntry in discussionEntries)
        {
            discussionEntry.Text = RemoveImageTags(discussionEntry.Text);
        }
        return discussionEntries;
    }

    private static string RemoveImageTags(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }
        // Regex to match <img ...> tags (case-insensitive)
        string pattern = @"<img[^>]*>";
        return Regex.Replace(html, pattern, string.Empty, RegexOptions.IgnoreCase);
    }
}
