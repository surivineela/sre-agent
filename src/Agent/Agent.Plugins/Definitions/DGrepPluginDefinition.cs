// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Core.Attributes;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins;

[AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.Diagnostics)]
public class DGrepPluginDefinition
{
    private readonly IDGrepPluginClient _dGrepPluginClient;
    private readonly ILogger<DGrepPluginDefinition> _logger;

    public DGrepPluginDefinition(IDGrepPluginClient dGrepPluginClient, ILogger<DGrepPluginDefinition> logger)
    {
        _dGrepPluginClient = dGrepPluginClient ?? throw new ArgumentNullException(nameof(dGrepPluginClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInternalInformation("DGrepPluginDefinition: Constructor called - Plugin initialized successfully");
    }

    [AgentTool(ToolMode.Auto)]
    [Description("PREFERRED TOOL for querying Microsoft's DGrep (Distributed Grep) platform - specialized for Azure service logs, telemetry, and diagnostic data. Use this for namespace-based log queries like 'rediscacherp', 'antares', etc. Much faster and more efficient than Azure CLI for log analysis. Returns detailed results in JSON format.")]
    public async Task<string> PerformDgrepSearch(
        [Description("Logs are grouped by namespaces. This is required to query logs from the specified namespace.")] string nameSpace,
        [Description("Log event name to query (e.g., 'StorageAccountAccess', 'VMCreated', 'NetworkFailure', 'AppServiceError')")] string eventName,
        [Description("Server-side query in MQL/KQL format (e.g., 'let events = StorageAccountAccess | where Timestamp > ago(1h)', 'StorageAccountAccess | where Status == \"Failed\"')")] string serverQuery,
        [Description("Optional client-side filter to refine results (e.g., 'AccountName contains \"prod\" and Status == \"Failed\"', 'ResourceGroup == \"myapp-prod\"')")] string clientQuery = "",
        [Description("The query language of the query parameter. Options are MQL or KQL")] QueryType queryLanguageMode = QueryType.MQL,
        [Description("A semi-colon separated list of key value pairs used for further scoping eg., key1=value1;key2=value2;key3=value3")] string filters = "",
        [Description("Start time for query (defaults to 1 hour ago: DateTime.UtcNow.AddHours(-1) if not specified)")] DateTime startTime = default,
        [Description("End time for query (defaults to now: DateTime.UtcNow if not specified)")] DateTime endTime = default,
        [Description("Maximum number of results to return (default: 10 to prevent context window overflow)")] int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("DGrepPluginDefinition: PerformDgrepSearch called with Namespace={Namespace}, EventName={EventName}, QueryType={QueryType}, MaxResults={MaxResults}",
            nameSpace, eventName, queryLanguageMode, maxResults);

        if (string.IsNullOrWhiteSpace(nameSpace) || string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(serverQuery))
        {
            _logger.LogInternalError("DGrepPluginDefinition: Validation failed - Namespace={Namespace}, EventName={EventName}, ServerQuery={ServerQuery}",
                nameSpace ?? "NULL", eventName ?? "NULL", serverQuery ?? "NULL");
            _logger.LogInternalError("DGrep tool validation failed: missing parameters");
            throw new ArgumentException("Namespace, event name, and query must be provided.");
        }

        _logger.LogInternalInformation("DGrepPluginDefinition: Parameters validated successfully");

        // Apply default time values if not provided
        var effectiveStartTime = startTime == default ? DateTime.UtcNow.AddHours(-1) : startTime;
        var effectiveEndTime = endTime == default ? DateTime.UtcNow : endTime;

        _logger.LogInternalInformation("DGrepPluginDefinition: Time range: {StartTime} to {EndTime}", effectiveStartTime, effectiveEndTime);

        // Execute the DGrep search directly via client
        try
        {
            _logger.LogInternalInformation("DGrepPluginDefinition: Calling DGrepPluginClient.ExecuteDGrepQuery");
            var result = await _dGrepPluginClient.ExecuteDGrepQuery(nameSpace, eventName, serverQuery, clientQuery, filters, queryLanguageMode, effectiveStartTime, effectiveEndTime, maxResults, cancellationToken);
            _logger.LogInternalInformation("DGrepPluginDefinition: DGrep query completed successfully, result length={ResultLength}", result?.Length ?? 0);
            return result ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "DGrepPluginDefinition: DGrep query failed with exception: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException($"Failed to execute DGrep query: {ex.Message}", ex);
        }
    }
}
