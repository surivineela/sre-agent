using System.Text;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Constants = Agent.Graph.Crawler.ARM.Constants;
using Agent.Plugins.Extensions;

namespace Agent.Plugins.Implementation
{
    public class APIManagementPlugin : IAPIManagementPlugin
    {
        private readonly IGraphDatabaseClient _databaseClient;
        private readonly ILogger<APIManagementPlugin> _logger;
        private readonly ArmHelper _armHelper;

        public APIManagementPlugin(
            IGraphDatabaseClient databaseClient,
            ILogger<APIManagementPlugin> logger,
            ArmHelper armHelper,
            IHttpClientFactory httpClientFactory
            )
        {
            _databaseClient = databaseClient;
            _logger = logger;
            _armHelper = armHelper;
        }
        public async Task<APIManagementDescriptor> GetAPIManagementInfoAsync(string resourceId)
        {
            _logger.LogInternalInformation($"[get_api_management_info] Invoked with resourceId: {resourceId}");

            try
            {
                // Build the normalized ID that Gremlin expects:
                string apiManagementResourceId = resourceId.ToLower().Replace("/", "_");

                string query = $@"
                    g.V()
                    .has('id', '{apiManagementResourceId}')
                    .project('properties')
                    .by(properties().group().by(key()).by(value()))
                    .select('properties')";

                var result = await _databaseClient.Query<Dictionary<string, object>>(query);

                if (result == null || !result.Any())
                {
                    _logger.LogInternalInformation("Could not retrieve API Management instance information for API Management Resource ID: " + resourceId);
                    return null;
                }

                // get the descriptors from the API Management Resource Node
                var apiManagementNode = new APIManagementNode(result.First());
                return apiManagementNode.ToDescriptor(verbose: true);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetAPIManagementInfoAsync with resourceId {resourceId}");
                return null;
            }
        }

        // Intentionally returning a string as the SRE agent frequently makes errors when trying to deserialize the descriptors manually
        public async Task<List<APIManagementDescriptor>> ListAPIManagementAsync(Guid subscriptionId)
        {
            _logger.LogInternalInformation($"[list_api_management_instances] Invoked with subscription {subscriptionId}");

            try
            {
                string query = $@"
                    g.V()
                    .has('subscriptionId', '{subscriptionId}')
                    .hasLabel('{Constants.ApiManagementType.ToLower()}')
                    .project('properties')
                    .by(properties().group().by(key()).by(value()))
                    .select('properties')";

                var result = await _databaseClient.Query<Dictionary<string, object>>(query);

                if (result == null || !result.Any())
                {
                    _logger.LogInternalInformation("No API Management Instances found for subscription {subscriptionId} in graph database.", subscriptionId);
                    return null;
                }

                // get the descriptors from the API Management Resource Node
                return result
                    .Select(apiManagementAppData => new APIManagementNode(apiManagementAppData))
                    .Select(a => a.ToDescriptor(verbose: false))
                    .ToList();               
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in ListAPIManagementAsync with subscription {subscriptionId}");
                return null;
            }
        }

        // Intentionally returning a string as the SRE agent frequently makes errors when trying to deserialize the descriptors manually
        public async Task<string> GetAPIMErrorLogsAsync(string apimInstanceResourceId, DateTime startTime , DateTime endTime, string statusCode, int top)
        {
            _logger.LogInternalInformation($"[GetAPIMErrorLogsAsync] Invoked with resourceId: {apimInstanceResourceId}, startTime: {startTime}, endTime: {endTime}, statusCode: {statusCode ?? "Any"}, top: {top}");

            var appInsightsResourceId = await GetAPIMConnectedAppInsights(apimInstanceResourceId);
            if (string.IsNullOrWhiteSpace(appInsightsResourceId))
            {
                _logger.LogInternalWarning($"No Application Insights resource found for API Management instance: {apimInstanceResourceId}");
                return $"Error: Could not find a connected Application Insights resource for {apimInstanceResourceId}";
            }

            string apimResourceName = apimInstanceResourceId.Split('/').Last();

            // Construct the KQL query
            var queryBuilder = new StringBuilder($@"
            let dataset = requests
                | where customDimensions['Service ID'] == '{apimResourceName}'
                | where success == false
                | where timestamp between (datetime('{startTime:O}') .. datetime('{endTime:O}'))");

            if (!string.IsNullOrWhiteSpace(statusCode))
            {
                queryBuilder.Append($@"
                | where resultCode == '{statusCode}'");
            }

            queryBuilder.Append($@"
                | top {top} by timestamp desc;
            dataset");

            // Resolve instrumentation key and App Insights App ID
            const string apiVersion = APIManagementHelper.Constants.AppInsightsApiVer;
            var appSettingsJson = await _armHelper.GetResourceByURL($"https://management.azure.com{appInsightsResourceId}?api-version={apiVersion}");
            var jsonObject = JObject.Parse(appSettingsJson);

            var instrumentationKey = jsonObject["properties"]?["InstrumentationKey"]?.ToString()
                                  ?? GetInstrumentationKey(jsonObject["properties"]?["ConnectionString"]?.ToString());
            if (string.IsNullOrWhiteSpace(instrumentationKey))
            {
                _logger.LogInternalWarning($"No Instrumentation Key found for Application Insights resource: {appInsightsResourceId}");
                return $"Error: Could not find Instrumentation Key for Application Insights resource {appInsightsResourceId}";
            }

            var subscriptionId = apimInstanceResourceId.Split('/')[2];
            var appInsightsAppId = await _armHelper.GetAppInsightsAppIdBySubscription(subscriptionId, instrumentationKey);

            // Execute the query
            var queryResult = await _armHelper.ExecuteAppInsightsQuery(appInsightsAppId, queryBuilder.ToString());
            return queryResult;
        }

        public async Task<string> GetAPIMActivityLogs(string apimResourceId, DateTime startTime, DateTime endTime)
        {
            _logger.LogInternalInformation($"[GetAPIMActivityLogsAsync] Invoked with resourceId: {apimResourceId}, startTime: {startTime}, endTime: {endTime}");

            string subscriptionId = apimResourceId.Split('/')[2];
            string apiVersion = APIManagementHelper.Constants.ActivityLogApiVer;

            string filter = $"eventTimestamp ge '{startTime:O}' and eventTimestamp le '{endTime:O}' and resourceUri eq '{apimResourceId}'";
            string requestUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Insights/eventtypes/management/values?api-version={apiVersion}&$filter={Uri.EscapeDataString(filter)}";

            try
            {
                _logger.LogInternalInformation($"GetAPIMActivityLogs: Fetching activity logs from URL: {requestUrl}");
                var appActivityLogs = await _armHelper.GetResourceByURL(requestUrl);

                if (string.IsNullOrWhiteSpace(appActivityLogs))
                {
                    _logger.LogInternalWarning("GetAPIMActivityLogs: Received empty response from ARM.");
                    return "No activity log entries found.";
                }

                var jsonAppActivityLogs = JObject.Parse(appActivityLogs);
                var activityLogEvents = jsonAppActivityLogs["value"] as JArray;
                if (activityLogEvents == null || !activityLogEvents.Any())
                {
                    _logger.LogInternalInformation("GetAPIMActivityLogs: No activity log entries found in response.");
                    return "No activity log entries found.";
                }

                var activityLogList = new List<APIMActivityLogEntry>();
                foreach (var activityEvent in activityLogEvents)
                {
                    var eventTimestamp = activityEvent["eventTimestamp"]?.ToString() ?? "N/A";
                    var operationName = activityEvent["operationName"]?["localizedValue"]?.ToString() ?? "N/A";
                    var eventName = activityEvent["eventName"]?["localizedValue"]?.ToString() ?? "N/A";
                    var eventStatus = activityEvent["status"]?["localizedValue"]?.ToString() ?? "N/A";
                    var requestUri = activityEvent["httpRequest"]?["uri"]?.ToString() ?? "N/A";
                    var eventCaller = activityEvent["caller"]?.ToString() ?? "N/A";

                    var logEntry = new APIMActivityLogEntry(
                        eventTimestamp,
                        operationName,
                        eventName,
                        eventStatus,
                        requestUri,
                        eventCaller
                    );
                    activityLogList.Add(logEntry);
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(activityLogList, Newtonsoft.Json.Formatting.Indented);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"GetAPIMActivityLogs: Exception occurred for resourceId: {apimResourceId}");
                return $"Error: Exception occurred while fetching activity logs: {ex.Message}";
            }
        }

        #region Api Gateway Logs Methods

        public async Task<string> GetAPIMFailureRateByApiOperation(string apiManagementResourceId, DateTime startTime, DateTime endTime)
        {
            _logger.LogInternalInformation($"[GetFailureRateByApiOperationAsync] Invoked for APIM '{apiManagementResourceId}', startTime '{startTime}', endTime '{endTime}'");

            try
            {
                // Build the time span string for the query
                string startIso = startTime.ToString("o");
                string endIso = endTime.ToString("o");
                string timeSpan = $"{startIso}/{endIso}";

                string queryString = $@"
                    ApiManagementGatewayLogs
                        | where TimeGenerated between(datetime('{startIso}') ..datetime('{endIso}'))
                        | summarize
                            TotalCount = count(),
                            FailedCount = countif(IsRequestSuccess == 0),
                            ResponseCode = arg_max(TimeGenerated, ResponseCode),
                            LastErrorReason = arg_max(TimeGenerated, LastErrorReason)
                            by ApiId, OperationId
                        | extend
                            FailureRatePercent = iif(TotalCount == 0, 0.0, todouble(FailedCount) / todouble(TotalCount) * 100)
                        | order by FailureRatePercent desc
                        | project
                            ApiId,
                            OperationId,
                            ResponseCode,
                            LastErrorReason,
                            TotalCount,
                            FailedCount,
                            FailureRatePercent
                ";

                // Send the query to Log Analytics and return the result
                string queryResult = await _armHelper.ExecuteLogAnalyticsQuery(apiManagementResourceId, queryString, timeSpan);
                return queryResult;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetFailureRateByApiOperationAsync for {apiManagementResourceId}");
                return null;
            }
        }

        public async Task<string> GetAPIMRecentFailedRequests(string apiManagementResourceId, TimeSpan lookback, int topN)
        {
            _logger.LogInternalInformation($"[GetRecentFailedRequestsAsync] Invoked for APIM '{apiManagementResourceId}', lookbackHour timespan '{lookback}', topN '{topN}'");

            try
            {
                // Determine the time window
                DateTime endTime = DateTime.UtcNow;
                DateTime startTime = endTime.Add(-lookback);
                string startIso = startTime.ToString("o");
                string endIso = endTime.ToString("o");
                string timeSpan = $"{startIso}/{endIso}";

                // Query retrieves the most recent failures with full details
                string queryString = $@"
                ApiManagementGatewayLogs
                    | where TimeGenerated between (datetime({startIso}) .. datetime({endIso}))
                    | where IsRequestSuccess == 0
                    | order by TimeGenerated desc
                    | take {topN}
                    | project
                        TimeGenerated,
                        CorrelationId,
                        ApiId,
                        OperationId,
                        Url,
                        Method,
                        CallerIpAddress,
                        ResponseCode,
                        LastErrorReason,
                        LastErrorMessage,
                        RequestSize,
                        ResponseSize,
                        RequestHeaders,
                        ResponseHeaders,
                        RequestBody,
                        ResponseBody
                ";

                // Execute the query and return the JSON result
                string queryResult = await _armHelper.ExecuteLogAnalyticsQuery(apiManagementResourceId, queryString, timeSpan);
                return queryResult;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetAPIMConnectedLogAnalytics for {apiManagementResourceId}");
                return null;
            }
        }

        #endregion

        #region APIM Connected Resource Helpers

        public async Task<string> GetAPIMConnectedAppInsights(string apiManagementResourceId)
        {
            string apiVersion = APIManagementHelper.Constants.LoggersApiVer;

            // Call to list all of the app insights resources connected to the API Management instance
            var requestUrl = $"https://management.azure.com{apiManagementResourceId}/loggers?api-version={apiVersion}";

            var connectedAppInsightsResources = await _armHelper.GetResourceByURL(requestUrl);
            JObject connectedAPIMAppInsights = JObject.Parse(connectedAppInsightsResources);

            // Extract subscriptionId from the API Management resourceId
            string[] apimParts = apiManagementResourceId.Split('/');
            string apimSubscriptionId = apimParts.Length > 2 ? apimParts[2] : string.Empty;
            if (string.IsNullOrEmpty(apimSubscriptionId))
            {
                _logger.LogInternalWarning($"Could not extract subscriptionId from API Management resourceId: {apiManagementResourceId}");
                return null;
            }

            var loggers = connectedAPIMAppInsights["value"];
            if (loggers == null)
            {
                return null;
            }

            foreach (var logger in loggers)
            {
                var loggerType = logger["properties"]?["loggerType"]?.ToString();
                var resourceId = logger["properties"]?["resourceId"]?.ToString();

                if (loggerType == Constants.ApplicationInsightsKind && !string.IsNullOrEmpty(resourceId))
                {
                    string[] resourceIdParts = resourceId.Split('/');
                    string resourceSubscriptionId = resourceIdParts.Length > 2 ? resourceIdParts[2] : string.Empty;

                    if (string.Equals(resourceSubscriptionId, apimSubscriptionId, StringComparison.OrdinalIgnoreCase))
                    {
                        return resourceId;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Generic API Management Helpers

        private string? GetInstrumentationKey(string? connectionString)
        {
            if (connectionString != null)
            {
                string[] keyValues = connectionString.Split(';');
                foreach (var keyValue in keyValues)
                {
                    if (keyValue.StartsWith("InstrumentationKey=", StringComparison.OrdinalIgnoreCase))
                    {
                        return keyValue.Substring("InstrumentationKey=".Length);
                    }
                }
            }
            return null;
        }
        #endregion

    }
}
