// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Graph.Schema;
using Agent.Logging;
using Agent.Plugins.Interface;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Azure.Management.Monitor;
using Microsoft.Azure.Management.Monitor.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using Microsoft.Rest.Azure.OData;

namespace Agent.Plugins.Implementation
{
    public class AzureActivityLogsPlugin : IAzureActivityLogsPlugin
    {
        private readonly ILogger<AzureActivityLogsPlugin> _logger;
        private readonly IGraphDatabaseClient _graphDbClient;
        private readonly IAuthenticationService _authService;
        private readonly IChatClientProvider _chatClientProvider;
        private readonly IGraphDBPlugin _graphDBPlugin;
        private readonly IAgentOutboundCommunicationService _outboundService;

        public Guid? ThreadId { get; set; }

        public AzureActivityLogsPlugin(
            ILogger<AzureActivityLogsPlugin> logger,
            IGraphDatabaseClient graphDbClient,
            IAuthenticationService authService,
            IChatClientProvider chatClientProvider,
            IGraphDBPlugin graphDBPlugin,
            IAgentOutboundCommunicationService outboundService)
        {
            _logger = logger;
            _graphDbClient = graphDbClient;
            _authService = authService;
            _chatClientProvider = chatClientProvider;
            _graphDBPlugin = graphDBPlugin;
            _outboundService = outboundService;
        }

        public async Task<(List<Dictionary<string, object>> ActivityLogs, List<Node> Components)> FetchActivityLogsAndComponents(string resourceId, int hoursBack = 24, Guid? threadId = null)
        {
            _logger.LogInternalInformation($"[FetchActivityLogs] Invoked with resourceId: {resourceId}");

            if (string.IsNullOrWhiteSpace(resourceId))
            {
                throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
            }

            try
            {
                var _ = new ResourceIdentifier(resourceId);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Invalid Azure resource ID format: {ex.Message}",
                    nameof(resourceId));
            }

            if (threadId == null && ThreadId != null)
            {
                threadId = ThreadId;
            }

            const int maxRetries = 3;
            const int retryDelayMilliseconds = 1000; // 1 second

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Pull component summary via GraphDBPlugin
                    var components = await _graphDBPlugin.GetApplicationComponentsSummary(resourceId, 3);

                    if (components.Count == 0)
                    {
                        _logger.LogInternalWarning($"No components found for resourceId: {resourceId}");
                        throw new ArgumentException($"No components found for resourceId: {resourceId}. Was the correct resource ID provided? Alternatively, the Knowledge Graph may not have been built for this component.");
                    }

                    var resourceIds = components.Select(c => c.Id).ToList();
                    var rgs = ExtractUniqueResourceGroups(resourceIds);
                    _logger.LogInternalInformation($"Found {resourceIds.Count} related resources for activity log analysis");

                    var allActivityLogs = new List<Dictionary<string, object>>();

                    foreach (var id in rgs)
                    {
                        var logs = await FetchActivityLogsForResource(id.Replace("_", "/"), hoursBack);

                        // Retain only interesting write/deployment-ish operations when present
                        var distinctLogs = logs
                            .Where(l => l != null
                                        && l.ContainsKey("operationName")
                                        && l["operationName"]?.ToString()?.Contains("write", StringComparison.OrdinalIgnoreCase) == true)
                            .ToList();

                        var _ = logs.Select(l => l.TryGetValue("operationName", out var op) ? op?.ToString() : null)
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .Distinct()
                                    .ToList();

                        if (logs.Count > 0)
                        {
                            allActivityLogs.AddRange(logs);
                        }
                        _logger.LogInternalInformation($"Fetched {logs.Count} activity logs for resource {id}");
                    }

                    if (allActivityLogs.Count == 0)
                    {
                        throw new ArgumentException("No activity logs found for the specified resource and its dependencies in the last " + hoursBack + " hours.");
                    }

                    allActivityLogs = allActivityLogs
                        .OrderByDescending(log =>
                        {
                            if (log != null && log.TryGetValue("eventTimestamp", out var timestampValue) && timestampValue != null)
                            {
                                if (DateTimeOffset.TryParse(timestampValue.ToString(), out var timestamp))
                                {
                                    return timestamp;
                                }
                            }
                            return DateTimeOffset.MinValue;
                        })
                        .ToList();

                    _logger.LogInternalInformation($"Total activity logs collected: {allActivityLogs.Count}");
                    return (allActivityLogs, components);
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogInternalWarning($"[FetchAndSummarizeActivityLogs] Attempt {attempt} failed with error: {ex.Message}. Retrying in {retryDelayMilliseconds}ms...");
                        await Task.Delay(retryDelayMilliseconds);
                    }
                    else
                    {
                        _logger.LogInternalError($"[FetchAndSummarizeActivityLogs] All {maxRetries} attempts failed. Last error: {ex.Message}");
                        throw;
                    }
                }
            }

            throw new ArgumentException("Error: Unexpected execution path in fetching and summarizing activity logs");
        }

        public async Task<string> FetchAndSummarizeActivityLogs(string resourceId, int hoursBack = 24, Guid? threadId = null)
        {
            _logger.LogInternalInformation($"[FetchAndSummarizeActivityLogs] Invoked with resourceId: {resourceId}");
            (List<Dictionary<string, object>> allActivityLogs, List<Node> components) = await FetchActivityLogsAndComponents(resourceId, hoursBack, threadId);
            var logsJson = JsonSerializer.Serialize(allActivityLogs, new JsonSerializerOptions { WriteIndented = true });
            var summary = await SummarizeLogsWithLLM(logsJson, components?.ToString() ?? string.Empty);
            return summary;
        }

        public async Task<string> AnalyzeDeploymentFailures(string resourceId, int hoursBack = 24, Guid? threadId = null)
        {
            _logger.LogInternalInformation($"[AnalyzeDeploymentFailures] Invoked with resourceId: {resourceId}");

            try
            {
                var resourceIdentifier = new ResourceIdentifier(resourceId);
                var subscriptionId = resourceIdentifier.SubscriptionId;
                var resourceGroupName = resourceIdentifier.ResourceGroupName;

                var credential = await _authService.GetArmOperationCredential();
                var defaultToken = credential.GetToken(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None).Token;
                var defaultTokenCredentials = new TokenCredentials(defaultToken);
                var azureCredentials = new Microsoft.Azure.Management.ResourceManager.Fluent.Authentication.AzureCredentials(defaultTokenCredentials, defaultTokenCredentials, null, AzureEnvironment.AzureGlobalCloud);

                var restClient = RestClient.Configure()
                    .WithBaseUri("https://management.azure.com")
                    .WithCredentials(azureCredentials)
                    .Build();

                var monitorClient = new MonitorManagementClient(restClient)
                {
                    SubscriptionId = subscriptionId
                };

                var startTime = DateTime.UtcNow.AddHours(-hoursBack);
                var endTime = DateTime.UtcNow;

                // Focus specifically on deployment operations
                var filterString = $"eventTimestamp ge {startTime:yyyy-MM-ddTHH:mm:ssZ} and " +
                                   $"eventTimestamp le {endTime:yyyy-MM-ddTHH:mm:ssZ} and " +
                                   $"eventChannels eq 'Admin, Operation' and " +
                                   $"resourceGroupName eq '{resourceGroupName}' and " +
                                   $"operationName.value eq 'Microsoft.Resources/deployments/write'";

                var odataQuery = new ODataQuery<EventData>(filterString);

                IPage<EventData> eventsPage = await monitorClient.ActivityLogs.ListAsync(
                    odataQuery: odataQuery,
                    cancellationToken: default);

                var deploymentFailures = new List<Dictionary<string, object>>();

                do
                {
                    foreach (var eventData in eventsPage)
                    {
                        // Only include failed deployments
                        if (eventData?.Status?.Value?.Contains("Failed") == true)
                        {
                            var deployment = new Dictionary<string, object>
                            {
                                ["eventTimestamp"] = eventData?.EventTimestamp?.ToString("o") ?? string.Empty,
                                ["operationName"] = eventData?.OperationName?.Value ?? string.Empty,
                                ["caller"] = eventData?.Caller ?? string.Empty,
                                ["status"] = eventData?.Status?.Value ?? string.Empty,
                                ["correlationId"] = eventData?.CorrelationId ?? string.Empty,
                                ["level"] = eventData?.Level?.ToString() ?? string.Empty,
                                ["resourceId"] = eventData?.ResourceId ?? resourceId
                            };

                            // Include all properties for failed deployments
                            if (eventData?.Properties != null)
                            {
                                deployment["properties"] = JsonSerializer.Serialize(eventData.Properties);
                            }

                            deploymentFailures.Add(deployment);
                        }
                    }

                    if (!string.IsNullOrEmpty(eventsPage.NextPageLink))
                    {
                        eventsPage = await monitorClient.ActivityLogs.ListNextAsync(eventsPage.NextPageLink);
                    }
                    else
                    {
                        break;
                    }
                } while (true);

                if (!deploymentFailures.Any())
                {
                    return $"No deployment failures found in the last {hoursBack} hours for resource group '{resourceGroupName}'.";
                }

                var deploymentsJson = JsonSerializer.Serialize(deploymentFailures, new JsonSerializerOptions { WriteIndented = true });
                var analysis = await AnalyzeDeploymentFailuresWithLLM(deploymentsJson, resourceId);
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error analyzing deployment failures for {resourceId}");
                return $"Error analyzing deployment failures: {ex.Message}";
            }
        }

        public async Task<string> GetChangeHistory(string correlationId, string resourceId, Guid? threadId = null)
        {
            _logger.LogInternalInformation($"[GetChangeHistory] correlationId={correlationId}, resourceId={resourceId}");

            if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("Correlation ID cannot be empty.");
            if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("Resource ID cannot be empty.");

            try
            {
                var ri = new ResourceIdentifier(resourceId);
                var subscriptionId = ri.SubscriptionId ?? throw new ArgumentException("Invalid resourceId (no subscription).");
                var resourceGroupName = ri.ResourceGroupName ?? string.Empty;

                // 1) Activity Log (for context + anchor time)
                var changeHistory = await FetchChangeHistoryByCorrelationId(correlationId, subscriptionId, resourceGroupName);

                // Anchor time: first (oldest) event in the set or now if none
                var anchorUtc = changeHistory
                    .Select(e => DateTimeOffset.TryParse(e.TryGetValue("eventTimestamp", out var v) ? v?.ToString() : null, out var dto) ? dto.UtcDateTime : (DateTime?)null)
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .DefaultIfEmpty(DateTime.UtcNow)
                    .Min();

                // 2) Change Analysis (ARG) – primary path
                var diffs = await FetchDiffsFromARGByCorrelationIdAsync(correlationId, subscriptionId);

                // 3) Fallback – portal-style ±30 min window
                if (diffs.Count == 0)
                {
                    var rgOrIdHint = string.IsNullOrEmpty(resourceGroupName) ? resourceId : resourceGroupName;
                    diffs = await FetchDiffsFromARGByWindowAsync(anchorUtc, subscriptionId, rgOrIdHint);
                }

                // Optional: deployment & per-resource ops
                var deploymentDetails = await FetchDeploymentDetails(correlationId, subscriptionId, resourceGroupName);
                var resourceChanges = await FetchResourceChanges(correlationId, resourceId);

                // 4) Compose + analyze (LLM)
                var result = await AnalyzeChangeHistoryWithLLM_Extended(
                    changeHistory, deploymentDetails, resourceChanges, diffs, correlationId, resourceId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"[GetChangeHistory] failure for {correlationId}");
                return $"Error retrieving change history: {ex.Message}";
            }
        }

        public HashSet<string> ExtractUniqueResourceGroups(IEnumerable<string> resourceIds)
        {
            var resourceGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resourceId in resourceIds)
            {
                // Convert underscores back to slashes if they were replaced
                string normalizedId = resourceId.Replace("_", "/");

                // Split the path into segments
                string[] segments = normalizedId.Split('/');

                // Look for the subscription and resourceGroups in the segments
                for (int i = 0; i < segments.Length - 3; i++)
                {
                    if (string.Equals(segments[i], "subscriptions", StringComparison.OrdinalIgnoreCase) &&
                        i + 2 < segments.Length &&
                        string.Equals(segments[i + 2], "resourceGroups", StringComparison.OrdinalIgnoreCase) &&
                        i + 3 < segments.Length)
                    {
                        // Build the path: /subscriptions/{subId}/resourceGroups/{rgName}
                        string path = $"/subscriptions/{segments[i + 1]}/resourceGroups/{segments[i + 3]}";
                        resourceGroups.Add(path);
                        break; // move to next resource id
                    }
                }
            }

            return resourceGroups;
        }

        private async Task<List<Dictionary<string, object>>> FetchActivityLogsForResource(string resourceId, int hoursBack)
        {
            _logger.LogInternalInformation($"[FetchActivityLogsForResource] Fetching activity logs for: {resourceId}");

            try
            {
                var resourceIdentifier = new ResourceIdentifier(resourceId);
                var subscriptionId = resourceIdentifier.SubscriptionId;
                var resourceGroupName = resourceIdentifier.ResourceGroupName;

                var credential = await _authService.GetArmOperationCredential();
                var defaultToken = credential.GetToken(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None).Token;
                var defaultTokenCredentials = new TokenCredentials(defaultToken);
                var azureCredentials = new Microsoft.Azure.Management.ResourceManager.Fluent.Authentication.AzureCredentials(defaultTokenCredentials, defaultTokenCredentials, null, AzureEnvironment.AzureGlobalCloud);

                var restClient = RestClient.Configure()
                    .WithBaseUri("https://management.azure.com")
                    .WithCredentials(azureCredentials)
                    .Build();

                var monitorClient = new MonitorManagementClient(restClient)
                {
                    SubscriptionId = subscriptionId
                };

                var startTime = DateTime.UtcNow.AddHours(-hoursBack);
                var endTime = DateTime.UtcNow;

                var filterString = $"eventTimestamp ge {startTime:yyyy-MM-ddTHH:mm:ssZ} and " +
                                   $"eventTimestamp le {endTime:yyyy-MM-ddTHH:mm:ssZ} and " +
                                   $"eventChannels eq 'Admin, Operation' and " +
                                   $"resourceGroupName eq '{resourceGroupName}'";

                var odataQuery = new ODataQuery<EventData>(filterString);

                IPage<EventData> eventsPage = await monitorClient.ActivityLogs.ListAsync(
                    odataQuery: odataQuery,
                    cancellationToken: default);

                var logs = new List<Dictionary<string, object>>();

                do
                {
                    foreach (var eventData in eventsPage)
                    {
                        // Filter out GET operations and routine read activities
                        var operationName = eventData?.OperationName?.Value ?? string.Empty;
                        if (ShouldSkipOperation(operationName))
                        {
                            continue;
                        }

                        var log = new Dictionary<string, object>
                        {
                            ["eventTimestamp"] = eventData?.EventTimestamp?.ToString("o") ?? string.Empty,
                            ["operationName"] = operationName,
                            ["caller"] = eventData?.Caller ?? string.Empty,
                            ["status"] = eventData?.Status?.Value ?? string.Empty,
                            ["correlationId"] = eventData?.CorrelationId ?? string.Empty,
                            ["level"] = eventData?.Level?.ToString() ?? string.Empty
                        };

                        // Only add caller IP for failed operations or deployments
                        if (eventData?.HttpRequest != null &&
                            (eventData.Status?.Value?.Contains("Failed") == true ||
                             operationName.Contains("deployment", StringComparison.OrdinalIgnoreCase)))
                        {
                            log["callerIpAddress"] = eventData.HttpRequest.ClientIpAddress;
                        }

                        // Only add authorization for security-relevant operations
                        if (eventData?.Authorization != null && IsSecurityRelevantOperation(operationName))
                        {
                            log["authorizationAction"] = eventData.Authorization.Action;
                        }

                        // Only include essential properties for failed operations or deployments
                        if (eventData?.Properties != null &&
                            (eventData.Status?.Value?.Contains("Failed") == true ||
                             operationName.Contains("deployment", StringComparison.OrdinalIgnoreCase)))
                        {
                            var filteredProperties = FilterEssentialProperties(eventData.Properties);
                            if (filteredProperties.Any())
                            {
                                log["properties"] = JsonSerializer.Serialize(filteredProperties);
                            }
                        }

                        logs.Add(log);
                    }

                    if (!string.IsNullOrEmpty(eventsPage.NextPageLink))
                    {
                        eventsPage = await monitorClient.ActivityLogs.ListNextAsync(eventsPage.NextPageLink);
                    }
                    else
                    {
                        break;
                    }
                } while (true);

                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error fetching activity logs for {resourceId}");
                return new List<Dictionary<string, object>>();
            }
        }

        private bool ShouldSkipOperation(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return true;

            var operationLower = operationName.ToLowerInvariant();

            // Skip GET operations and other read-only activities
            var skipPatterns = new[]
            {
                "/read",
                "/get",
                "/list",
                "/query",
                "/search",
                "/browse",
                "/validate",
                "/check",
                "microsoft.insights/logs/read",
                "microsoft.insights/metrics/read",
                "microsoft.authorization/permissions/read",
                "microsoft.authorization/roleassignments/read",
                "microsoft.resources/subscriptions/read",
                "microsoft.resources/resourcegroups/read"
            };

            return skipPatterns.Any(pattern => operationLower.Contains(pattern));
        }

        private bool IsSecurityRelevantOperation(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return false;

            var operationLower = operationName.ToLowerInvariant();

            var securityPatterns = new[]
            {
                "roleassignment",
                "policy",
                "permission",
                "authentication",
                "authorization",
                "security",
                "delete",
                "microsoft.keyvault",
                "microsoft.authorization"
            };

            return securityPatterns.Any(pattern => operationLower.Contains(pattern));
        }

        private Dictionary<string, object> FilterEssentialProperties(IDictionary<string, string> properties)
        {
            var essentialProperties = new Dictionary<string, object>();

            // Only include properties that are useful for troubleshooting and change tracking
            var essentialKeys = new[]
            {
                "statusCode",
                "statusMessage",
                "errorCode",
                "errorMessage",
                "failureReason",
                "correlationId",
                "trackingId",
                "deploymentName",
                "templateHash",
                "resourceName",
                "provisioningState",
                "duration",
                "changeType",
                "changedProperties",
                "beforeSnapshot",
                "afterSnapshot",
                "configurationChanges",
                "resourceChanges",
                "deploymentParameters",
                "templateParameters",
                "outputResources",
                "modifiedBy",
                "changeDescription",
                "changeReason",
                "policyViolation",
                "complianceState"
            };

            foreach (var key in essentialKeys)
            {
                if (properties.ContainsKey(key) && properties[key] != null)
                {
                    essentialProperties[key] = properties[key];
                }
            }

            return essentialProperties;
        }

        private async Task<string> SummarizeLogsWithLLM(string logsJson, string gremlinOutput)
        {
            try
            {
                var prompt = @$"
You are a cloud operations analyst. I will provide you with Azure activity logs for a resource group of an app facing an issue. This might contain some noise since the resource group may have other resources. Provide a concise summary following instructions below:

Here's a gremlin output of resources we care about:

{gremlinOutput}

Please analyze these logs and provide a summary that includes:

1. A high-level overview of the activity
2. Key changes made to the resources (when and by whom), collapse same operation (e.g. role assignments) into same point
3. Patterns of activity (e.g., regular deployments, configuration changes)
4. Any potential issues or concerns
5. Recommendations based on the activity patterns

Each log entry contains:
- eventTimestamp: When the activity occurred
- operationName: What action was performed
- caller: The user or service principal that performed the action
- status: Success or failure of the operation
- correlationId: Unique identifier to track related activities, indicate this should be used for finding change diff
- properties: Additional details about the activity (only for failed operations)

Here are the logs in JSON format:

{logsJson}

Please provide a highly concise summary with sections for each of the above points. Focus on who made changes (mention the name), when they were made, and what kinds of changes were made. Identify patterns and potential issues. Remember the summary should be very concise and to the point. Respond in a **minimalist, structured format** with no fluff. Using fewer words per point while preserving clarity.";

                var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(prompt);
                return response.Text;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error summarizing logs with LLM");
                return $"Error summarizing logs: {ex.Message}";
            }
        }

        private async Task<string> AnalyzeDeploymentFailuresWithLLM(string deploymentsJson, string resourceId)
        {
            try
            {
                var prompt = @$"
You are an Azure deployment troubleshooting expert. I will provide you with failed Azure deployment logs. Analyze these failures and provide actionable insights.

Resource ID: {resourceId}

Analyze these deployment failures and provide:

1. **Root Cause Analysis**: What specifically caused each deployment to fail?
2. **Timeline**: When did these failures occur and any patterns?
3. **Error Details**: Extract and explain the key error messages and codes
4. **Resolution Steps**: Specific actions to fix these deployment issues
5. **Prevention**: How to prevent similar failures in the future

Each deployment failure contains:
- eventTimestamp: When the deployment failed
- operationName: The deployment operation
- caller: Who initiated the deployment
- status: Failure status
- correlationId: Unique identifier for tracking
- properties: Detailed error information and deployment context

Here are the failed deployments in JSON format:

{deploymentsJson}

Focus on actionable troubleshooting steps. Extract specific error codes, resource conflicts, permission issues, or template problems. Provide clear next steps for resolving each type of failure. Be concise but thorough in your analysis.";

                var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(prompt);
                return response.Text;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error analyzing deployment failures with LLM");
                return $"Error analyzing deployment failures: {ex.Message}";
            }
        }
        private async Task<List<Dictionary<string, object>>> FetchChangeHistoryByCorrelationId(
            string correlationId, string subscriptionId, string resourceGroupName)
        {
            try
            {
                // Build Track1 RestClient with ARM token
                var armCred = await _authService.GetArmOperationCredential();
                var token = armCred.GetToken(
                    new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                    CancellationToken.None).Token;

                var tokenCreds = new TokenCredentials(token);
                var azureCreds = new Microsoft.Azure.Management.ResourceManager.Fluent.Authentication.AzureCredentials(
                    tokenCreds, tokenCreds, null, AzureEnvironment.AzureGlobalCloud);

                var restClient = RestClient.Configure()
                    .WithBaseUri("https://management.azure.com")
                    .WithCredentials(azureCreds)
                    .Build();

                var monitorClient = new MonitorManagementClient(restClient)
                {
                    SubscriptionId = subscriptionId
                };

                var endTime = DateTimeOffset.UtcNow;
                var startTime = endTime.AddDays(-30);

                // Escape single quotes for OData
                string corr = correlationId.Replace("'", "''");

                // Narrow to time + correlationId
                const string isoZ = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
                var startUtc = startTime.UtcDateTime.ToString(isoZ, CultureInfo.InvariantCulture);
                var endUtc = endTime.UtcDateTime.ToString(isoZ, CultureInfo.InvariantCulture);

                var filterString =
                    $"eventTimestamp ge '{startUtc}' and " +
                    $"eventTimestamp le '{endUtc}' and " +
                    $"correlationId eq '{corr}'";

                var odataQuery = new ODataQuery<EventData>(filterString);

                var changeEvents = new List<Dictionary<string, object>>();

                IPage<EventData> page = await monitorClient.ActivityLogs.ListAsync(
                    odataQuery: odataQuery, cancellationToken: default);

                while (true)
                {
                    foreach (var e in page)
                    {
                        if (!string.Equals(e?.ResourceGroupName, resourceGroupName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var evt = new Dictionary<string, object>
                        {
                            ["eventTimestamp"] = e?.EventTimestamp?.ToString("o") ?? string.Empty,
                            ["operationName"] = e?.OperationName?.Value ?? string.Empty,
                            ["caller"] = e?.Caller ?? string.Empty,
                            ["status"] = e?.Status?.Value ?? string.Empty,
                            ["correlationId"] = e?.CorrelationId ?? string.Empty,
                            ["level"] = e?.Level?.ToString() ?? string.Empty,
                            ["resourceId"] = e?.ResourceId ?? string.Empty,
                            ["resourceGroup"] = e?.ResourceGroupName ?? string.Empty,
                            ["description"] = e?.Description ?? string.Empty,
                            ["eventName"] = e?.EventName?.Value ?? string.Empty,
                            ["category"] = e?.Category?.Value ?? string.Empty
                        };

                        if (e?.Properties != null && e.Properties.Count > 0)
                            evt["properties"] = JsonSerializer.Serialize(e.Properties);

                        if (e?.Authorization != null)
                            evt["authorization"] = new Dictionary<string, object>
                            {
                                ["action"] = e.Authorization.Action ?? string.Empty,
                                ["role"] = e.Authorization.Role ?? string.Empty,
                                ["scope"] = e.Authorization.Scope ?? string.Empty
                            };

                        var isFailed = (e?.Status?.Value ?? "").IndexOf("Failed", StringComparison.OrdinalIgnoreCase) >= 0;
                        var isDeploy = (e?.OperationName?.Value ?? "").IndexOf("deployment", StringComparison.OrdinalIgnoreCase) >= 0;
                        if ((isFailed || isDeploy) && e?.HttpRequest?.ClientIpAddress != null)
                            evt["callerIpAddress"] = e.HttpRequest.ClientIpAddress;

                        changeEvents.Add(evt);
                    }

                    if (string.IsNullOrEmpty(page?.NextPageLink)) break;
                    page = await monitorClient.ActivityLogs.ListNextAsync(page.NextPageLink);
                }

                return changeEvents
                    .OrderBy(e =>
                    {
                        if (e.TryGetValue("eventTimestamp", out var v) &&
                            DateTimeOffset.TryParse(v?.ToString(), out var dto))
                            return dto;
                        return DateTimeOffset.MinValue;
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error fetching change history by correlation ID {correlationId}");
                return new List<Dictionary<string, object>>();
            }
        }

        // Back-compat wrapper (if other code expects this name)
        private Task<List<Dictionary<string, object>>> FetchChangeHistoryForCorrelationId(string correlationId, string subscriptionId)
            => FetchChangeHistoryByCorrelationId(correlationId, subscriptionId, resourceGroupName: string.Empty);

        private async Task<List<ChangeRecord>> FetchDiffsFromARGByCorrelationIdAsync(string correlationId, string subscriptionId)
        {
            var armCred = await _authService.GetArmOperationCredential();
            var token = armCred.GetToken(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                CancellationToken.None).Token;

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var query = $@"
resourcechanges
| extend corr=tostring(properties.changeAttributes.correlationId),
         changeTime=todatetime(properties.changeAttributes.timestamp),
         targetResourceId=tostring(properties.targetResourceId),
         changeType=tostring(properties.changeType),
         changedBy=tostring(properties.changeAttributes.changedBy),
         clientType=tostring(properties.changeAttributes.clientType),
         prevSnap=tostring(properties.changeAttributes.previousResourceSnapshotId),
         newSnap=tostring(properties.changeAttributes.newResourceSnapshotId)
| where corr =~ '{correlationId}'
| project changeTime, targetResourceId, changeType, changedBy, clientType,
          changes=properties.changes, prevSnap, newSnap
| order by changeTime asc";

            var payload = new
            {
                subscriptions = new[] { subscriptionId },
                query,
                options = new { resultFormat = "objectArray" }
            };

            var resp = await http.PostAsync(
                "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();

            using var s = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);

            var results = new List<ChangeRecord>();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in data.EnumerateArray())
                {
                    results.Add(new ChangeRecord
                    {
                        ChangeTime = row.GetProperty("changeTime").GetDateTime(),
                        TargetResourceId = row.GetProperty("targetResourceId").GetString() ?? "",
                        ChangeType = row.GetProperty("changeType").GetString() ?? "",
                        ChangedBy = row.GetProperty("changedBy").GetString() ?? "",
                        ClientType = row.GetProperty("clientType").GetString() ?? "",
                        ChangesJson = row.GetProperty("changes").GetRawText(),
                        PreviousSnapshotId = row.GetProperty("prevSnap").GetString(),
                        NewSnapshotId = row.GetProperty("newSnap").GetString()
                    });
                }
            }

            return results;
        }

        private async Task<List<ChangeRecord>> FetchDiffsFromARGByWindowAsync(DateTime anchorUtc, string subscriptionId, string? resourceGroupHintOrResourceId)
        {
            var startUtc = anchorUtc.AddMinutes(-30).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
            var endUtc = anchorUtc.AddMinutes(+30).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

            var armCred = await _authService.GetArmOperationCredential();
            var token = armCred.GetToken(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None).Token;

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var nameHint = resourceGroupHintOrResourceId ?? string.Empty;

            var kql = $@"
let start_t = datetime({startUtc});
let end_t   = datetime({endUtc});
resourcechanges
| extend changeTime = todatetime(properties.changeAttributes.timestamp),
         targetResourceId = tostring(properties.targetResourceId)
| where changeTime between (start_t .. end_t)
| where isempty('{nameHint}') or targetResourceId has '{nameHint}'
| extend changeType = tostring(properties.changeType),
         changedBy = tostring(properties.changeAttributes.changedBy),
         clientType = tostring(properties.changeAttributes.clientType),
         prevSnap = tostring(properties.changeAttributes.previousResourceSnapshotId),
         newSnap = tostring(properties.changeAttributes.newResourceSnapshotId),
         changes = properties.changes
| project changeTime, targetResourceId, changeType, changedBy, clientType, changes, prevSnap, newSnap
| order by changeTime asc";

            var payload = new
            {
                subscriptions = new[] { subscriptionId },
                query = kql,
                options = new { resultFormat = "objectArray" }
            };

            var resp = await http.PostAsync(
                "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            resp.EnsureSuccessStatusCode();

            using var s = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);

            var list = new List<ChangeRecord>();
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var row in data.EnumerateArray())
            {
                list.Add(new ChangeRecord
                {
                    ChangeTime = row.GetProperty("changeTime").GetDateTime(),
                    TargetResourceId = row.GetProperty("targetResourceId").GetString() ?? "",
                    ChangeType = row.GetProperty("changeType").GetString() ?? "",
                    ChangedBy = row.GetProperty("changedBy").GetString() ?? "",
                    ClientType = row.GetProperty("clientType").GetString() ?? "",
                    ChangesJson = row.GetProperty("changes").GetRawText(),
                    PreviousSnapshotId = row.GetProperty("prevSnap").GetString(),
                    NewSnapshotId = row.GetProperty("newSnap").GetString()
                });
            }

            return list;
        }

        private static string FormatDiffsForPrompt(IEnumerable<ChangeRecord> diffs, int maxRows = 50)
        {
            var sb = new StringBuilder();
            int i = 0;
            foreach (var d in diffs.OrderBy(x => x.ChangeTime))
            {
                if (i++ >= maxRows) break;
                sb.AppendLine($"- [{d.ChangeTime:O}] {d.TargetResourceId} ({d.ChangeType}) by {d.ChangedBy} via {d.ClientType}");
                sb.AppendLine($"  changes: {d.ChangesJson}");
                if (!string.IsNullOrEmpty(d.PreviousSnapshotId) || !string.IsNullOrEmpty(d.NewSnapshotId))
                    sb.AppendLine($"  snapshots: prev={d.PreviousSnapshotId}, new={d.NewSnapshotId}");
            }
            return sb.ToString();
        }

        private async Task<string> AnalyzeChangeHistoryWithLLM_Extended(
            List<Dictionary<string, object>> changeHistory,
            Dictionary<string, object>? deploymentDetails,
            List<Dictionary<string, object>> resourceChanges,
            List<ChangeRecord> diffs,
            string correlationId,
            string resourceId)
        {
            var changeHistoryJson = JsonSerializer.Serialize(changeHistory, new JsonSerializerOptions { WriteIndented = true });
            var deploymentDetailsJson = deploymentDetails != null
                ? JsonSerializer.Serialize(deploymentDetails, new JsonSerializerOptions { WriteIndented = true })
                : "No deployment details available";
            var resourceChangesJson = JsonSerializer.Serialize(resourceChanges, new JsonSerializerOptions { WriteIndented = true });
            var diffsCompact = FormatDiffsForPrompt(diffs);

            var prompt = $@"
You are an Azure change analysis expert.

Correlation ID: {correlationId}
Resource ID: {resourceId}

Use **Change Analysis (property-level diffs)** as the source of truth.
Provide:
1) Change summary (what changed, when, and who)
2) Operation timeline (from Activity Log)
3) Property-level BEFORE → AFTER highlights (from diffs)
4) Impact analysis (what resources were affected)
5) Validation & issues
6) Rollback/mitigation pointers

=== DIFFS (from Change Analysis) ===
{diffsCompact}

=== ACTIVITY LOG (context) ===
{changeHistoryJson}

=== DEPLOYMENT DETAILS ===
{deploymentDetailsJson}

=== OTHER RESOURCE CHANGES (deployment ops) ===
{resourceChangesJson}

Respond in a concise, structured format with bullet points and short sentences.";
            var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(prompt);
            return response.Text;
        }

        private async Task<Dictionary<string, object>?> FetchDeploymentDetails(string correlationId, string subscriptionId, string resourceGroupName)
        {
            try
            {
                var resourceManagementClient = await CreateResourceClientAsync(subscriptionId);
                // List recent deployments and find one matching the correlation ID
                var deployments = await resourceManagementClient.Deployments.ListByResourceGroupAsync(resourceGroupName);

                foreach (var deployment in deployments)
                {
                    if (deployment?.Properties?.CorrelationId == correlationId)
                    {
                        return new Dictionary<string, object>
                        {
                            ["deploymentName"] = deployment.Name ?? string.Empty,
                            ["provisioningState"] = deployment.Properties?.ProvisioningState?.ToString() ?? string.Empty,
                            ["timestamp"] = deployment.Properties?.Timestamp?.ToString("o") ?? string.Empty,
                            ["mode"] = deployment.Properties?.Mode?.ToString() ?? string.Empty,
                            ["templateHash"] = deployment.Properties?.TemplateHash ?? string.Empty,
                            ["parameters"] = deployment.Properties?.Parameters != null ?
                                JsonSerializer.Serialize(deployment.Properties.Parameters) : string.Empty,
                            ["outputs"] = deployment.Properties?.Outputs != null ?
                                JsonSerializer.Serialize(deployment.Properties.Outputs) : string.Empty,
                            ["error"] = deployment.Properties?.Error != null ?
                                JsonSerializer.Serialize(deployment.Properties.Error) : string.Empty
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Could not fetch deployment details for correlation ID {correlationId}");
                return null;
            }
        }

        private async Task<List<Dictionary<string, object>>> FetchResourceChanges(string correlationId, string resourceId)
        {
            try
            {
                var ri = new ResourceIdentifier(resourceId);
                var subscriptionId = ri.SubscriptionId;
                var resourceGroupName = ri.ResourceGroupName;

                if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroupName))
                {
                    return new List<Dictionary<string, object>>();
                }

                var resourceClient = await CreateResourceClientAsync(subscriptionId);

                // Get recent deployments in the RG and find a correlationId match first
                IPage<DeploymentExtendedInner> page = await resourceClient.Deployments.ListByResourceGroupAsync(resourceGroupName);
                DeploymentExtendedInner? targetDeployment = null;

                while (true)
                {
                    foreach (var d in page)
                    {
                        if (string.Equals(d?.Properties?.CorrelationId, correlationId, StringComparison.OrdinalIgnoreCase))
                        {
                            targetDeployment = d;
                            break;
                        }
                    }
                    if (targetDeployment != null || string.IsNullOrEmpty(page?.NextPageLink)) break;
                    page = await resourceClient.Deployments.ListByResourceGroupNextAsync(page.NextPageLink);
                }

                if (targetDeployment != null && !string.IsNullOrEmpty(targetDeployment.Name))
                {
                    // Collect only operations that touched the provided resourceId
                    return await CollectChangesForDeploymentAsync(resourceClient, resourceGroupName, targetDeployment.Name, resourceId);
                }

                // Fallback: scan recent deployments for changes to this resource
                return await GetResourceChangesFromRecentDeployments(resourceClient, resourceGroupName, resourceId);
            }
            catch
            {
                return new List<Dictionary<string, object>>();
            }
        }

        private async Task<List<Dictionary<string, object>>> GetResourceChangesFromRecentDeployments(
            ResourceManagementClient resourceClient,
            string resourceGroupName,
            string resourceId)
        {
            var allChanges = new List<Dictionary<string, object>>();
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);

            IPage<DeploymentExtendedInner> page = await resourceClient.Deployments.ListByResourceGroupAsync(resourceGroupName);

            while (true)
            {
                foreach (var d in page)
                {
                    // Skip older deployments
                    var ts = d?.Properties?.Timestamp;
                    if (ts.HasValue && ts.Value < cutoff) continue;
                    if (string.IsNullOrEmpty(d?.Name)) continue;

                    var perDeployment = await CollectChangesForDeploymentAsync(resourceClient, resourceGroupName, d.Name, resourceId);
                    if (perDeployment.Count > 0)
                    {
                        allChanges.AddRange(perDeployment);
                    }
                }

                if (string.IsNullOrEmpty(page?.NextPageLink)) break;
                page = await resourceClient.Deployments.ListByResourceGroupNextAsync(page.NextPageLink);
            }

            // newest first
            allChanges = allChanges
                .OrderByDescending(c =>
                {
                    if (c.TryGetValue("timestamp", out var t) && DateTimeOffset.TryParse(t?.ToString(), out var dto)) return dto;
                    return DateTimeOffset.MinValue;
                })
                .ToList();

            return allChanges;
        }

        private static async Task<List<Dictionary<string, object>>> CollectChangesForDeploymentAsync(
            ResourceManagementClient client, string resourceGroupName, string deploymentName, string resourceId)
        {
            var changes = new List<Dictionary<string, object>>();
            IPage<DeploymentOperationInner> ops = await client.DeploymentOperations.ListAsync(resourceGroupName, deploymentName);

            while (true)
            {
                foreach (var op in ops)
                {
                    var tr = op?.Properties?.TargetResource;
                    var trId = tr?.Id ?? string.Empty;

                    if (!string.IsNullOrEmpty(resourceId) &&
                        !string.Equals(trId, resourceId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var change = new Dictionary<string, object>
                    {
                        ["deploymentName"] = deploymentName,
                        ["operationId"] = op?.OperationId ?? string.Empty,
                        ["timestamp"] = op?.Properties?.Timestamp?.ToString("o") ?? string.Empty,
                        ["provisioningOperation"] = op?.Properties?.ProvisioningOperation?.ToString() ?? string.Empty,
                        ["provisioningState"] = op?.Properties?.ProvisioningState ?? string.Empty,
                        ["resourceId"] = trId,
                        ["resourceType"] = tr?.ResourceType ?? string.Empty,
                        ["resourceName"] = tr?.ResourceName ?? string.Empty
                    };

                    if (op?.Properties?.StatusMessage != null)
                    {
                        change["statusMessage"] = op.Properties.StatusMessage;
                    }

                    changes.Add(change);
                }

                if (string.IsNullOrEmpty(ops?.NextPageLink)) break;
                ops = await client.DeploymentOperations.ListNextAsync(ops.NextPageLink);
            }

            return changes;
        }

        private async Task<ResourceManagementClient> CreateResourceClientAsync(string subscriptionId)
        {
            // Get ARM token via auth service
            var credential = await _authService.GetArmOperationCredential();
            var token = credential.GetToken(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                CancellationToken.None).Token;

            var tokenCreds = new TokenCredentials(token);

            var azureCreds = new Microsoft.Azure.Management.ResourceManager.Fluent.Authentication.AzureCredentials(
                tokenCreds, tokenCreds, tenantId: null, environment: AzureEnvironment.AzureGlobalCloud);

            var restClient = RestClient
                .Configure()
                .WithBaseUri("https://management.azure.com")
                .WithCredentials(azureCreds)
                .Build();

            var rm = new ResourceManagementClient(restClient)
            {
                SubscriptionId = subscriptionId
            };
            return rm;
        }

        public async Task<string> ShowChangeDiffViewer(string correlationId, string resourceId, string title, string description, Guid? threadId = null)
        {
            _logger.LogInternalInformation($"[ShowChangeDiffViewer] correlationId={correlationId}, resourceId={resourceId}");

            if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("Correlation ID cannot be empty.");
            if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("Resource ID cannot be empty.");

            if (threadId == null && ThreadId != null)
            {
                threadId = ThreadId;
            }

            if (threadId == null)
            {
                _logger.LogInternalWarning("ThreadId is null while showing change diff viewer.");
                return "ERROR: Context is null.";
            }

            try
            {
                var ri = new ResourceIdentifier(resourceId);
                var subscriptionId = ri.SubscriptionId ?? throw new ArgumentException("Invalid resourceId (no subscription).");

                // Fetch the detailed change diffs from ARG
                var diffs = await FetchDiffsFromARGByCorrelationIdAsync(correlationId, subscriptionId);

                if (diffs.Count == 0)
                {
                    // Fallback to window-based search
                    var anchorTime = DateTime.UtcNow;
                    var resourceGroupName = ri.ResourceGroupName ?? string.Empty;
                    var rgOrIdHint = string.IsNullOrEmpty(resourceGroupName) ? resourceId : resourceGroupName;
                    diffs = await FetchDiffsFromARGByWindowAsync(anchorTime, subscriptionId, rgOrIdHint);
                }

                if (diffs.Count == 0)
                {
                    return "No change diffs found for the specified correlation ID and resource.";
                }

                // Create the change diff data structure
                var changeDiffData = new ChangeDiffViewer
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Description = description,
                    CorrelationId = correlationId,
                    ResourceId = resourceId,
                    Changes = diffs.Select(d => new ChangeDiffItem
                    {
                        ChangeTime = d.ChangeTime.ToString("o"),
                        TargetResourceId = d.TargetResourceId,
                        ChangeType = d.ChangeType,
                        ChangedBy = d.ChangedBy,
                        ClientType = d.ClientType,
                        ChangesJson = d.ChangesJson,
                        PreviousSnapshotId = d.PreviousSnapshotId,
                        NewSnapshotId = d.NewSnapshotId
                    }).ToList()
                };

                var changeDiffJson = JsonSerializer.Serialize(changeDiffData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Create a special message that will be rendered as a diff viewer
                Guid messageId = Guid.NewGuid();

                // Create the change diff message format that the front-end will recognize
                var changeDiffMessage = $"```change-diff\n{changeDiffJson}\n```\n{description}";

                // Save to database via the outbound service (following ChartPluginV2 pattern)
                await _outboundService.AppendAgentImageMessage(ThreadId!.Value, changeDiffMessage, messageId);

                // Stream the chart data directly to bypass tool call limitations
                await _outboundService.AppendAgentStreamMessage(ThreadId.Value, changeDiffMessage, StreamMessageType.ChangeDiff, messageId);

                return $"Successfully generated change diff viewer for correlation ID {correlationId}. Description: {description}";
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"[ShowChangeDiffViewer] failure for {correlationId}");
                return $"Error showing change diff viewer: {ex.Message}";
            }
        }

        // ---------------- Types ----------------

        public class ChangeDiffViewer
        {
            public string Id { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public string CorrelationId { get; set; } = "";
            public string ResourceId { get; set; } = "";
            public List<ChangeDiffItem> Changes { get; set; } = new();
        }

        public class ChangeDiffItem
        {
            public string ChangeTime { get; set; } = "";
            public string TargetResourceId { get; set; } = "";
            public string ChangeType { get; set; } = "";
            public string ChangedBy { get; set; } = "";
            public string ClientType { get; set; } = "";
            public string ChangesJson { get; set; } = "[]";
            public string? PreviousSnapshotId { get; set; }
            public string? NewSnapshotId { get; set; }
        }

        public record ChangeRecord
        {
            public DateTime ChangeTime { get; init; }
            public string TargetResourceId { get; init; } = "";
            public string ChangeType { get; init; } = "";
            public string ChangedBy { get; init; } = "";
            public string ClientType { get; init; } = "";
            public string ChangesJson { get; init; } = "[]";
            public string? PreviousSnapshotId { get; init; }
            public string? NewSnapshotId { get; init; }
        }
    }
}
