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
using System.Text.RegularExpressions;
using FirstPartyAgent.Models;
using Agent.Core.Interfaces;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace FirstPartyAgent.Core.Plugins.Implementation;
public class ContainerAppIcMPlugin : IcmPlugin, IContainerAppIcMPlugin
{
    private readonly ILogger<ContainerAppIcMPlugin> _logger;
    private readonly IChatClient _chatClient;
    private readonly ITimePlugin _timePlugin;
    private readonly IManagedClusterPlugin _managedClusterPlugin;
    private readonly IManagedEnvironmentPlugin _managedEnvironmentPlugin;
    private readonly IContainerAppsPlugin _containerAppsPlugin;

    public ContainerAppIcMPlugin(
            IConfiguration config,
            ICMWorkflowClient icmAutomationClient,
            IChatClient chatClient,
            ITimePlugin timePlugin,
            IManagedClusterPlugin managedClusterPlugin,
            IManagedEnvironmentPlugin managedEnvironmentPlugin,
            IContainerAppsPlugin containerAppsPlugin,
            ILogger<ContainerAppIcMPlugin> logger)
        : base(config, icmAutomationClient, logger)
    {
        _logger = logger;
        _chatClient = chatClient;
        _timePlugin = timePlugin;
        _managedClusterPlugin = managedClusterPlugin;
        _managedEnvironmentPlugin = managedEnvironmentPlugin;
        _containerAppsPlugin = containerAppsPlugin;
    }

    public (DateTime StartDate, DateTime EndDate) GetIssueInvestigationTimeRange(DateTime? issueFirstOccurence, DateTime? issueLastOccurene, DateTime? reportedIssueObservedOnTime)
    {
        if(issueFirstOccurence == null && issueLastOccurene == null && reportedIssueObservedOnTime == null)
        {
            throw new ArgumentException("At least one of the issueFirstOccurence, issueLastOccurene or reportedIssueObservedOnTime should be provided.");
        }

        var now = DateTime.UtcNow;

        // If no endDate, set to now
        DateTime endDate = issueLastOccurene
            ?? (reportedIssueObservedOnTime.HasValue ? reportedIssueObservedOnTime.Value.AddDays(2) : now);

        // If no startDate, set to now-10d
        DateTime startDate = issueFirstOccurence
            ?? (reportedIssueObservedOnTime.HasValue ? reportedIssueObservedOnTime.Value.AddDays(-2) : now.AddDays(-10));

        // Ensure the start date is not after the end date
        if (startDate > endDate)
        {
            startDate = endDate.AddDays(-10);
        }

        // If the range is greater than 1 month, adjust startDate to be 1 month before endDate
        if ((endDate - startDate).TotalDays > 30)
        {
            startDate = endDate.AddMonths(-1);
        }

        // If end date is older than 4 months, throw error
        if ((now - endDate).TotalDays > 120)
        {
            throw new ArgumentException("Issue end date is older than 4 months. Please specify correct dates as we can't investigate it.");
        }

        _logger.LogDebug($"Calculated investigation time range: StartDate={startDate}, EndDate={endDate}");
        return (startDate, endDate);
    }

    public async Task<string> GetInitialInvestigationReportAsync(string incidentId)
    {
        var stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
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
        stopwatch1.Stop();
        _logger.LogInformation($"Fetched ICM incident details for ICM ID {incidentId} total time took in fetching: {(int)stopwatch1.ElapsedMilliseconds}");

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

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartySubAgents), "ACA", "Common", "ContainerAppIcmSummarizationPlan.txt");
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

        var timePluginDefinition = new TimePluginDefinition(_timePlugin);
        var managedClusterPluginDefinition = new ManagedClusterPluginDefinition(_managedClusterPlugin);
        var managedEnvironmentPluginDefinition = new ManagedEnvironmentPluginDefinition(_managedEnvironmentPlugin);
        var containerAppsPluginDefinition = new ContainerAppsPluginDefinition(_containerAppsPlugin);

        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(managedClusterPluginDefinition.GetASIPageForManagedCluster),
            AIFunctionFactory.Create(managedEnvironmentPluginDefinition.GetASIPageForManagedEnvironment),
            AIFunctionFactory.Create(managedEnvironmentPluginDefinition.GetManagedEnvironmentInfo),
            AIFunctionFactory.Create(timePluginDefinition.GetCurrentUtcTime),
            AIFunctionFactory.Create(containerAppsPluginDefinition.GetSubscriptionUsage),
            AIFunctionFactory.Create(containerAppsPluginDefinition.GetSubscriptionDetail),
        };
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, summarizationPrompt),
            new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, incidentWithComments)
        };

        var options = new ChatOptions
        {
            Temperature = (float)0.2,
            // Don't enable: model may not respond when tools are added. For having goal of summary to minimize context, omitting tools is not going to impact; Metagent will run them if needed.
            // Not able to understand the root cause of why after adding it is causing problem in some cases.
            // Tools = tools,
            // ToolMode = ChatToolMode.Auto,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["response_format"] = "text"
            }
        };


        var stopwatch2 = System.Diagnostics.Stopwatch.StartNew();
        var response = await _chatClient.GetResponseAsync(messages, options);

        string summary = response.Text.ToString();
        stopwatch2.Stop();

        _logger.LogInformation($"Created ICM summary for ICM ID {incidentId} total time took in summarization: {(int)stopwatch2.ElapsedMilliseconds}");
        return summary;
    }

    public override async Task<Incident?> GetIncidentInfo(string incidentId)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Incident? incident = await _icmAutomationClient.GetIncidentAsync(incidentId);
        if (incident != null)
        {
            incident.DiscussionEntry = RemoveImageTags(incident.DiscussionEntry);
            incident.Summary = RemoveImageTags(incident.Summary);
        }
        stopwatch.Stop();
        _logger.LogInformation($"Fetched raw ICM incident details for ICM ID {incidentId} total time took in fetching: {(int)stopwatch.ElapsedMilliseconds}");
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

    public async Task WasAgentHelpfulInDebuggingIssueAsync(string incidentId, bool? wasHelpful, bool? isResolutionCorrect)
    {
        if (wasHelpful != null)
        {
            await AddTag(incidentId, wasHelpful == true ? "AgentHelpful:true" : "AgentHelpful:false");
        }
        if (isResolutionCorrect != null)
        {
            await AddTag(incidentId, isResolutionCorrect == true ? "AgentResolutionCorrect:true" : "AgentResolutionCorrect:false");
        }
    }
}
