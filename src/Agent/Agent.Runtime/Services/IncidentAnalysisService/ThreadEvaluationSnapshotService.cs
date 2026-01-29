// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Data;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Runtime.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Agent.Runtime.Services;

/// <summary>
/// Service for ingesting thread evaluation snapshots for incident threads.
/// Called by ThreadEvaluator after evaluating incident-related threads.
/// Queries App Insights for the latest snapshot and enriches it with evaluation scores.
/// </summary>
public class ThreadEvaluationSnapshotService : IThreadEvaluationSnapshotService
{
    private readonly CustomerLogger _appInsightsLogger;
    private readonly ILogger<ThreadEvaluationSnapshotService> _logger;
    private readonly ArmHelper _armHelper;
    private readonly CoreSettings _coreSettings;

    public ThreadEvaluationSnapshotService(
        CustomerLogger appInsightsLogger,
        ILogger<ThreadEvaluationSnapshotService> logger,
        ArmHelper armHelper,
        CoreSettings coreSettings)
    {
        _appInsightsLogger = appInsightsLogger ?? throw new ArgumentNullException(nameof(appInsightsLogger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _armHelper = armHelper ?? throw new ArgumentNullException(nameof(armHelper));
        _coreSettings = coreSettings ?? throw new ArgumentNullException(nameof(coreSettings));
    }

    /// <inheritdoc />
    public async Task IngestIntentMetSnapshotAsync(
        Guid threadId,
        string incidentId,
        int intentMetScore,
        string evaluationSummary,
        CancellationToken cancellationToken = default)
    {
        // Query App Insights for the latest snapshot for this incident
        var latestSnapshot = await GetLatestSnapshotFromAppInsightsAsync(incidentId, cancellationToken);

        if (latestSnapshot == null)
        {
            _logger.LogInternalWarning(
                "[ThreadEvaluationSnapshotService] No existing snapshot found in App Insights for incident {IncidentId}. Skipping IntentMet ingestion.",
                incidentId);
            return;
        }

        // Build the enriched snapshot with IntentMet fields
        var enrichedSnapshot = EnrichSnapshotWithIntentMet(latestSnapshot, threadId, intentMetScore, evaluationSummary);

        // Ingest the enriched snapshot to App Insights
        IngestSnapshot(enrichedSnapshot);

        _logger.LogInternalInformation(
            "[ThreadEvaluationSnapshotService] Successfully ingested IntentMet snapshot for incident {IncidentId} with score {Score}",
            incidentId, intentMetScore);
    }

    private async Task<Dictionary<string, string>?> GetLatestSnapshotFromAppInsightsAsync(
        string incidentId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Query for the latest snapshot with all fields
            var query = $@"
                customEvents
                | where timestamp > ago(60d) and name == ""IncidentActivitySnapshot""
                | where tostring(customDimensions.IncidentId) == ""{incidentId}""
                | extend
                    ResponsePlanId = tostring(customDimensions.ResponsePlanId),
                    IncidentId = tostring(customDimensions.IncidentId),
                    IncidentTitle = tostring(customDimensions.IncidentTitle),
                    ResponsePlanCreatedOn = tostring(customDimensions.ResponsePlanCreatedOn),
                    ResponsePlanUpdatedOn = tostring(customDimensions.ResponsePlanUpdatedOn),
                    IncidentCreatedOn = tostring(customDimensions.IncidentCreatedOn),
                    IncidentUpdatedOn = tostring(customDimensions.IncidentUpdatedOn),
                    IncidentHandledOn = tostring(customDimensions.IncidentHandledOn),
                    IncidentStatus = tostring(customDimensions.IncidentStatus),
                    IncidentSeverity = tostring(customDimensions.IncidentSeverity),
                    IncidentMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent),
                    IncidentAssistedByAgent = tostring(customDimensions.IncidentAssistedByAgent),
                    IncidentMitigatedOn = tostring(customDimensions.IncidentMitigatedOn),
                    IncidentRootCauseCategory = tostring(customDimensions.IncidentRootCauseCategory),
                    IncidentRootCauseDescription = tostring(customDimensions.IncidentRootCauseDescription),
                    IncidentSummary = tostring(customDimensions.IncidentSummary),
                    IncidentImpactedService = tostring(customDimensions.IncidentImpactedService),
                    AgentAutonomyLevel = tostring(customDimensions.AgentAutonomyLevel),
                    ResponsePlanCustom = tostring(customDimensions.ResponsePlanCustom),
                    IncidentPlatform = tostring(customDimensions.IncidentPlatform),
                    MinutesUntilIncidentMitigation = tostring(customDimensions.MinutesUntilIncidentMitigation)
                | order by timestamp desc
                | take 1
                | project
                    ResponsePlanId, IncidentId, IncidentTitle,
                    ResponsePlanCreatedOn, ResponsePlanUpdatedOn,
                    IncidentCreatedOn, IncidentUpdatedOn, IncidentHandledOn,
                    IncidentStatus, IncidentSeverity,
                    IncidentMitigatedByAgent, IncidentAssistedByAgent,
                    IncidentMitigatedOn, IncidentRootCauseCategory, IncidentRootCauseDescription,
                    IncidentSummary, IncidentImpactedService,
                    AgentAutonomyLevel, ResponsePlanCustom,
                    IncidentPlatform, MinutesUntilIncidentMitigation";

            var dataTable = await QueryAppInsights(query);

            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                return null;
            }

            var row = dataTable.Rows[0];
            var snapshot = new Dictionary<string, string>
            {
                { "ResponsePlanId", row["ResponsePlanId"]?.ToString() ?? string.Empty },
                { "IncidentId", row["IncidentId"]?.ToString() ?? string.Empty },
                { "IncidentTitle", row["IncidentTitle"]?.ToString() ?? string.Empty },
                { "ResponsePlanCreatedOn", row["ResponsePlanCreatedOn"]?.ToString() ?? string.Empty },
                { "ResponsePlanUpdatedOn", row["ResponsePlanUpdatedOn"]?.ToString() ?? string.Empty },
                { "IncidentCreatedOn", row["IncidentCreatedOn"]?.ToString() ?? string.Empty },
                { "IncidentUpdatedOn", row["IncidentUpdatedOn"]?.ToString() ?? string.Empty },
                { "IncidentHandledOn", row["IncidentHandledOn"]?.ToString() ?? string.Empty },
                { "IncidentStatus", row["IncidentStatus"]?.ToString() ?? string.Empty },
                { "IncidentSeverity", row["IncidentSeverity"]?.ToString() ?? string.Empty },
                { "IncidentMitigatedByAgent", row["IncidentMitigatedByAgent"]?.ToString() ?? string.Empty },
                { "IncidentAssistedByAgent", row["IncidentAssistedByAgent"]?.ToString() ?? string.Empty },
                { "IncidentMitigatedOn", row["IncidentMitigatedOn"]?.ToString() ?? string.Empty },
                { "IncidentRootCauseCategory", row["IncidentRootCauseCategory"]?.ToString() ?? string.Empty },
                { "IncidentRootCauseDescription", row["IncidentRootCauseDescription"]?.ToString() ?? string.Empty },
                { "IncidentSummary", row["IncidentSummary"]?.ToString() ?? string.Empty },
                { "IncidentImpactedService", row["IncidentImpactedService"]?.ToString() ?? string.Empty },
                { "AgentAutonomyLevel", row["AgentAutonomyLevel"]?.ToString() ?? string.Empty },
                { "ResponsePlanCustom", row["ResponsePlanCustom"]?.ToString() ?? string.Empty },
                { "IncidentPlatform", row["IncidentPlatform"]?.ToString() ?? string.Empty },
                { "MinutesUntilIncidentMitigation", row["MinutesUntilIncidentMitigation"]?.ToString() ?? string.Empty }
            };

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex,
                "[ThreadEvaluationSnapshotService] Error querying App Insights for incident {IncidentId}: {Message}",
                incidentId, ex.Message);
            return null;
        }
    }

    private async Task<DataTable?> QueryAppInsights(string query)
    {
        var connectionString = Environment.GetEnvironmentVariable("AppSettings__Core__Azure__ApplicationInsights__ConnectionString")
            ?? _coreSettings.Azure.AppInsights.ConnectionString;

        var applicationId = GetApplicationId(connectionString);

        if (string.IsNullOrWhiteSpace(applicationId))
        {
            _logger.LogInternalWarning("[ThreadEvaluationSnapshotService] ApplicationId not found in connection string");
            return null;
        }

        var results = await _armHelper.QueryAppInsightsByAppId(applicationId, query);
        var dataSet = JsonConvert.DeserializeObject<DataTableResponseObjectCollection>(results);

        if (dataSet?.Tables == null || !dataSet.Tables.Any())
        {
            return null;
        }

        var dt = dataSet.Tables.FirstOrDefault();
        if (dt == null)
        {
            return null;
        }

        // Set all columns to dynamic type for proper parsing
        foreach (var column in dt.Columns)
        {
            column.Type = "dynamic";
        }

        return Agent.Core.Helpers.DataTableExtensions.ToDataTable(dt);
    }

    private static string GetApplicationId(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return string.Empty;
        }

        // Parse the connection string to extract the ApplicationId
        // Format: InstrumentationKey=xxx;IngestionEndpoint=xxx;LiveEndpoint=xxx;ApplicationId=xxx
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2 && keyValue[0].Trim().Equals("ApplicationId", StringComparison.OrdinalIgnoreCase))
            {
                return keyValue[1].Trim();
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, string> EnrichSnapshotWithIntentMet(
        Dictionary<string, string> existingSnapshot,
        Guid threadId,
        int intentMetScore,
        string evaluationSummary)
    {
        // Create a new snapshot with all existing fields plus IntentMet fields
        var enrichedSnapshot = new Dictionary<string, string>(existingSnapshot)
        {
            // Add/update IntentMet Score fields
            ["ThreadId"] = threadId.ToString(),
            ["IntentMetScore"] = intentMetScore.ToString(),
            ["IntentMetSummary"] = evaluationSummary ?? string.Empty
        };

        return enrichedSnapshot;
    }

    private void IngestSnapshot(Dictionary<string, string> payload)
    {
        try
        {
            _appInsightsLogger.LogCustomEvent("IncidentActivitySnapshot", payload);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[ThreadEvaluationSnapshotService] Ingesting snapshot to App Insights failed");
            throw;
        }
    }
}
