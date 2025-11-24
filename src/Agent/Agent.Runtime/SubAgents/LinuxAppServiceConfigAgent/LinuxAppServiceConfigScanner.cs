// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services.LinuxAppService.Validators;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ArmConstants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Runtime.SubAgents.LinuxAppServiceConfigAgent;

/// <summary>
/// Scanner that identifies and handles Linux App Service configuration issues.
/// This scanner operates independently and is designed to be extensible for additional configuration checks.
/// </summary>
public class LinuxAppServiceConfigScanner(
    IChatClientProvider chatClientProvider,
    IThreadRepository threadRepository,
    ILogger<LinuxAppServiceConfigScanner> logger,
    IAgentInboundCommunicationService agentInboundCommunicationService,
    IGraphDatabaseClient graphDatabaseClient,
    ArmHelper armHelper,
    IEnumerable<ILinuxAppServiceConfigValidator> validators)
{
    private readonly ILogger<LinuxAppServiceConfigScanner> _logger = logger;
    private readonly IAgentInboundCommunicationService _agentInboundCommunicationService = agentInboundCommunicationService;
    private readonly IGraphDatabaseClient _graphDatabaseClient = graphDatabaseClient;
    private readonly ArmHelper _armHelper = armHelper;
    private readonly IThreadRepository _repository = threadRepository;
    private readonly IChatClient _chatClient = chatClientProvider.SmallFastModel;
    private readonly IEnumerable<ILinuxAppServiceConfigValidator> _validators = validators;

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Starting Linux App Service configuration scan");

        try
        {
            // Get all configuration issues across supported types
            var issues = await GetConfigurationIssuesAsync(cancellationToken);

            // Group issues by type for organized handling
            var groupedIssues = issues.GroupBy(issue => issue.Type).ToList();

            foreach (var group in groupedIssues)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInternalInformation("Cancellation requested, stopping Linux App Service config scanner.");
                    return;
                }

                if (group.Any())
                {
                    try
                    {
                        await HandleIssueGroupAsync(group);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "Error handling issue group {issueType}", group.Key);
                    }
                }
            }

            _logger.LogInternalInformation("Completed Linux App Service configuration scan");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Linux App Service configuration scan was canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during Linux App Service configuration scan");
        }
    }

    private async Task HandleIssueGroupAsync(IGrouping<LinuxAppServiceConfigIssueType, LinuxAppServiceConfigIssue> group)
    {
        var agentName = nameof(LinuxAppServiceConfigScanner);

        // Create the thread with issue type information
        (var thread, var agentContext) = await _agentInboundCommunicationService.CreateAgentThread(
            $"{agentName} detected {group.Key} issue",
            "",
            AgentTypeEnum.Meta,
            ThreadSource.BestPractices
        );

        _logger.LogInternalInformation($"Created thread for {group.Key} issues with {group.Count()} resources");

        // Build the structured message that includes the resource and issue information
        var issueDataLines = new List<string>
        {
            "<ISSUES_SUMMARY>",
            "SiteName | Location | ResourceId | Issue Details | Recommendation"
        };
        foreach (var issue in group)
        {
            issueDataLines.Add($"{issue.SiteName}|{issue.Location}|{issue.ResourceId}|{issue.Details}|{issue.Recommendation}");
        }
        issueDataLines.Add("</ISSUES_SUMMARY>");

        var userMessage = $"Detected '{group.Key}' for the following apps:" +
                          $"\n\n{string.Join("\n", issueDataLines)}";

        var message = new ThreadMessage(
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage?.Id ?? new Guid(),
            Message: userMessage,
            UserId: "",
            DisplayName: "",
            Timestamp: DateTime.UtcNow);

        await _agentInboundCommunicationService.ProcessUserMessageAsync(message);
    }

    private async Task<List<LinuxAppServiceConfigIssue>> GetConfigurationIssuesAsync(CancellationToken cancellationToken)
    {
        var resourcesToAnalyze = await GetResourcesToAnalyzeAsync();

        if (resourcesToAnalyze.Count == 0)
        {
            _logger.LogInternalInformation($"No Linux App Services found to analyze");
            return [];
        }

        _logger.LogInternalInformation($"Analyzing {resourcesToAnalyze.Count} Linux App Services");

        var issues = new List<LinuxAppServiceConfigIssue>();

        foreach (var resourceId in resourcesToAnalyze)
        {
            cancellationToken.ThrowIfCancellationRequested();

            issues.AddRange(await GetAllConfigIssueForResource(resourceId, cancellationToken));
        }

        return issues;
    }

    private async Task<List<LinuxAppServiceConfigIssue>> GetAllConfigIssueForResource(string resourceId, CancellationToken cancellationToken)
    {
        var issues = new List<LinuxAppServiceConfigIssue>();
        var siteConfig = await _armHelper.GetLinuxAppServiceConfigurationAsync(resourceId, cancellationToken);

        try
        {
            foreach (var validator in _validators)
            {
                var result = await validator.ValidateAsync(siteConfig);

                if (result != null)
                {
                    issues.Add(result);
                }
            }

            return issues;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error retrieving or validating configuration for resource {ResourceId}", resourceId);
            // Continue with other resources instead of failing the entire operation
        }

        return issues;
    }

    private async Task<List<string>> GetResourcesToAnalyzeAsync()
    {
        const string ignoreFieldName = "notification_ignoreuntil";

        // Query for Linux App Services specifically (filter by kind)
        var query = $"""
        g.V().has('resourceType', '{ArmConstants.AppServiceType.ToLower()}')
          .has('isDeleted', false)
          .has('kind', within('app,linux', 'app,container,linux'))
          .project('resourceId', '{ignoreFieldName}')
          .by(values('resourceId'))
          .by(
            coalesce(
              outE('{ArmConstants.Relationships.HasIgnoreConfig}').inV().has('isDeleted', false).values('{ignoreFieldName}'),
              constant('')
            )
          )
        """;

        var queryResults = await _graphDatabaseClient.Query(query);
        var resources = queryResults
            .Select(x => new
            {
                ResourceId = (string)x["resourceId"],
                IgnoreUntil = x[ignoreFieldName] == string.Empty ? null : DateTimeOffset.Parse(x[ignoreFieldName].ToString())
            })
            .OrderBy(x => x.ResourceId.Split("/").Last()).ToList();

        var now = DateTimeOffset.Now;
        var resourcesToAnalyze = new List<string>();
        DateTimeOffset? maxIgnoreUntil = null;

        foreach (var resource in resources)
        {
            if (resource.IgnoreUntil == null || resource.IgnoreUntil <= now)
            {
                resourcesToAnalyze.Add(resource.ResourceId);
            }
            else if (maxIgnoreUntil == null || resource.IgnoreUntil > maxIgnoreUntil)
            {
                maxIgnoreUntil = resource.IgnoreUntil;
            }
        }

        var ignoredCount = resources.Count - resourcesToAnalyze.Count;
        if (ignoredCount > 0)
        {
            _logger.LogInternalInformation(
                $"Skipping validation of {ignoredCount} resources as they are tagged to be ignored until {maxIgnoreUntil}"
            );
        }

        return resourcesToAnalyze;
    }
}
