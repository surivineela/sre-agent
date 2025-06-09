using System.Text;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

using AgentCoreConstants = Agent.Core.Constants;
using Constants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Plugins.Implementation
{
    public class APIManagementPlugin : IAPIManagementPlugin
    {
        private readonly IGraphDatabaseClient _databaseClient;
        private readonly ILogger<APIManagementPlugin> _logger;
        private readonly ArmHelper _armHelper;
        private readonly IHttpClientFactory _httpClientFactory;

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
            _httpClientFactory = httpClientFactory;
        }

        public async Task<APIManagementDescriptor> GetAPIManagementInfoAsync(string resourceId)
        {
            _logger.LogInternalInformation($"[get_api_management_info] Invoked with resourceId: {resourceId}");

            try
            {
                // Build the normalized ID that Gremlin expects:
                string apiManagementResourceId = resourceId.ToLower().Replace("/", "_");

                string query = $@"
                g.V().has('id', '{apiManagementResourceId}')
                     .hasLabel('{Constants.ApiManagementType.ToLower()}')
                     .project('id', 'name', 'type', 'properties')
                       .by(id())
                       .by(coalesce(values('resourceName'), constant('')))
                       .by(label())
                       .by(valueMap())";

                // Use the helper to run the query and map results:
                var allDescriptors = await GetDescriptorsFromGremlinAsync(query);

                if (allDescriptors.Count == 0)
                {
                    _logger.LogInternalWarning($"API Management Instance with ID '{resourceId}' not found in graph database.");
                    return null;
                }

                return allDescriptors[0];
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetAPIManagementInfoAsync with resourceId {resourceId}");
                return null;
            }
        }

        public async Task<IReadOnlyList<APIManagementDescriptor>> ListAPIManagementAsync(Guid subscriptionId)
        {
            _logger.LogInternalInformation($"[list_api_management_instances] Invoked with subscription {subscriptionId}");

            try
            {
                string query = $@"
                g.V().has('subscriptionId', '{subscriptionId}')
                     .hasLabel('{Constants.ApiManagementType.ToLower()}')
                     .project('id', 'name', 'type', 'properties')
                       .by(id())
                       .by(coalesce(values('resourceName'), constant('')))
                       .by(label())
                       .by(valueMap())";

                // restoreIdSlashes: true because in the listing we convert "_" back to "/"
                var descriptors = await GetDescriptorsFromGremlinAsync(query);
                RestoreIdSlashes(descriptors);

                if (descriptors.Count == 0)
                {
                    _logger.LogInternalInformation($"No API Management instances found for subscription {subscriptionId} in graph database.");
                }

                return descriptors;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in ListAPIManagementAsync with subscription {subscriptionId}");
                return new List<APIManagementDescriptor>();
            }
        }

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
            const string apiVersion = "2018-05-01-preview";
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

        #region APIM Connected Resource Helpers

        public async Task<string> GetAPIMConnectedAppInsights(string apiManagementResourceId)
        {
            string apiVersion = "2020-06-01-preview";

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

        private async Task<List<APIManagementDescriptor>> GetDescriptorsFromGremlinAsync(string gremlinQuery)
        {
            var descriptors = new List<APIManagementDescriptor>();

            var rawResults = await _databaseClient.Query(gremlinQuery);
            if (rawResults == null || !rawResults.Any())
            {
                return descriptors; // empty
            }

            foreach (var apiManagementInstance in rawResults)
            {
                var properties = apiManagementInstance["properties"];

                string id = apiManagementInstance["id"]?.ToString() ?? "";

                string name = apiManagementInstance["name"]?.ToString() ?? "";
                string type = apiManagementInstance["type"]?.ToString() ?? "Unknown";

                // Extract “resourceGroupName” and “location” from the valueMap in “properties”
                string resourceGroup = GetFirstPropertyValue(properties, "resourceGroupName");
                string location = GetFirstPropertyValue(properties, "location");

                var descriptor = new APIManagementDescriptor(
                    ResourceId: id,
                    Name: name,
                    Type: type,
                    ResourceGroup: resourceGroup,
                    Location: location
                );

                descriptors.Add(descriptor);
            }

            return descriptors;
        }

        private string GetFirstPropertyValue(dynamic properties, string propertyName)
        {
            if (properties == null || !((IDictionary<string, object>)properties).ContainsKey(propertyName))
            {
                return string.Empty;
            }

            var values = properties[propertyName];
            if (values is IEnumerable<object> enumerable && enumerable.Any())
            {
                return enumerable.First().ToString();
            }

            return string.Empty;
        }

        private void RestoreIdSlashes(List<APIManagementDescriptor> descriptors)
        {
            for (int i = 0; i < descriptors.Count; i++)
            {
                var apimDescriptor = descriptors[i];
                if (apimDescriptor.ResourceId.Contains("_"))
                {
                    descriptors[i] = apimDescriptor with { ResourceId = apimDescriptor.ResourceId.Replace("_", "/") };
                }
            }
        }

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
