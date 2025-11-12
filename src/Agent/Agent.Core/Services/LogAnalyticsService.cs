// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Azure.Monitor.Query;

namespace Agent.Core.Services;

public class LogAnalyticsService : ILogAnalyticsService
{
    private readonly LogsQueryClient _client;

    public LogAnalyticsService(IAuthenticationService authenticationService)
    {
        var credentials = authenticationService.GetLogAnalyticsCredential();
        _client = new LogsQueryClient(credentials);
    }

    public async Task<IReadOnlyCollection<ContainerAppLogAnalyticsLog>> GetContainerAppSystemLogsAsync(
        string workspaceId,
        string containerAppName,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? revisionName = null,
        string? aggregateOver = "1h",
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(aggregateOver) && !IsValidAggregateOver(aggregateOver))
        {
            throw new ArgumentException($"Invalid aggregateOver value: {aggregateOver}. Only '1h' is supported.");
        }

        var query = new StringBuilder();
        query.AppendLine($"""
                          ContainerAppSystemLogs_CL
                          | where ContainerAppName_s == '{containerAppName}'
                          {(!string.IsNullOrEmpty(revisionName) ? $" | where RevisionName_s == '{revisionName}'" : string.Empty)}
                          | project TimeGenerated, Type = Type_s, Log = Log_s
                          {(!string.IsNullOrEmpty(aggregateOver) ? $"| summarize count() by bin(TimeGenerated, {aggregateOver}), Type, Log" : string.Empty)}
                          | order by TimeGenerated desc
                          | take 500
                          """);

        var response = await _client.QueryWorkspaceAsync<ContainerAppLogAnalyticsLog>(
            workspaceId,
            query.ToString(),
            new QueryTimeRange(startTime, endTime),
            cancellationToken: cancellationToken);

        return response.Value ?? [];
    }

    public async Task<IReadOnlyCollection<ContainerAppLogAnalyticsLog>> GetContainerAppApplicationLogsAsync(
        string workspaceId,
        string containerAppName,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? revisionName = null,
        string? aggregateOver = "1h",
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(aggregateOver) && !IsValidAggregateOver(aggregateOver))
        {
            throw new ArgumentException($"Invalid aggregateOver value: {aggregateOver}. Only '1h' is supported.");
        }

        var query = new StringBuilder();
        query.AppendLine($"""
                          ContainerAppConsoleLogs_CL
                          | where ContainerAppName_s == '{containerAppName}'
                          {(!string.IsNullOrEmpty(revisionName) ? $"| where RevisionName_s == '{revisionName}'" : string.Empty)}
                          | project TimeGenerated, Log = Log_s
                          {(!string.IsNullOrEmpty(aggregateOver) ? $"| summarize count() by bin(TimeGenerated, {aggregateOver}), Type, Log" : string.Empty)}
                          | order by TimeGenerated desc
                          | take 500
                          """);

        var response = await _client.QueryWorkspaceAsync<ContainerAppLogAnalyticsLog>(
            workspaceId,
            query.ToString(),
            new QueryTimeRange(startTime, endTime),
            cancellationToken: cancellationToken);

        return response.Value ?? [];
    }

    /// <summary>
    /// Validates the aggregateOver parameter for a KQL query.
    /// It only accepts "1h" as a valid value atm.
    /// </summary>
    private static bool IsValidAggregateOver(string? aggregateOver)
    {
        return aggregateOver == "1h";
    }

    public async Task<string> GetLatestImagePullingLogAsync(
        string workspaceId,
        string containerAppName,
        string revisionName,
        TimeSpan ago,
        CancellationToken cancellationToken = default)
    {
        var query = $"""
                     ContainerAppSystemLogs_CL
                     | where ContainerAppName_s == '{containerAppName}' and RevisionName_s == '{revisionName}'
                     | where Reason_s == 'ContainerTerminated'
                     | where Log_s has_any ('ImagePullBackOff', 'ErrImagePull','ImagePullFailure')
                     | top 1 by TimeGenerated desc
                     | project TimeGenerated, Type = Type_s, Log = Log_s
                     """;

        var response = await _client.QueryWorkspaceAsync<ContainerAppLogAnalyticsLog>(
            workspaceId,
            query,
            new QueryTimeRange(ago),
            cancellationToken: cancellationToken);

        if (response.Value is not null && response.Value.Count > 0)
        {
            return response.Value[0].Log;
        }

        return string.Empty;
    }

    public async Task<IReadOnlyCollection<string>> GetAllImagePullingLogsAsync(
        string workspaceId,
        string containerAppName,
        TimeSpan ago,
        CancellationToken cancellationToken = default)
    {
        var query = $"""
                        ContainerAppSystemLogs_CL
                        | where ContainerAppName_s == '{containerAppName}'
                        | where TimeGenerated > ago(1h)
                        | where Log_s has 'pull' or Log_s has 'image'
                        | project TimeGenerated, Log_s
                        | order by TimeGenerated desc
                    """;

        var response = await _client.QueryWorkspaceAsync<ContainerAppLogAnalyticsLog>(
            workspaceId,
            query,
            new QueryTimeRange(ago),
            cancellationToken: cancellationToken);

        return response.Value?.Select(l => l.Log).ToList() ?? [];
    }
}
