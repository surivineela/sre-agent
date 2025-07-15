using System.Text;
using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Agent.Plugins.Extensions;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Azure;
using Azure.Core;
using Azure.ResourceManager.ApiManagement;
using Azure.ResourceManager.ApiManagement.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using static Agent.Plugins.Helpers.APIManagementHelper;
using Constants = Agent.Graph.Crawler.ARM.Constants;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Agent.Plugins.Implementation
{
    public class APIManagementPlugin : IAPIManagementPlugin
    {
        private readonly IGraphDatabaseClient _databaseClient;
        private readonly ILogger<APIManagementPlugin> _logger;
        private readonly ArmHelper _armHelper;
        private readonly IArmClientFactory _armClientFactory;
        

        public APIManagementPlugin(
            IGraphDatabaseClient databaseClient,
            ILogger<APIManagementPlugin> logger,
            ArmHelper armHelper,
            IHttpClientFactory httpClientFactory,
            IArmClientFactory armClientFactory
            )
        {
            _databaseClient = databaseClient;
            _logger = logger;
            _armHelper = armHelper;
            _armClientFactory = armClientFactory;
        }

        public Guid? ThreadId { get; set; }

        public async Task<APIManagementDescriptor?> GetAPIManagementInfoAsync(string resourceId)
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
                    return [];
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
                return [];
            }
        }

        public async Task<string> GetAPIMErrorLogsAsync(string apimInstanceResourceId, DateTime startTime, DateTime endTime, string statusCode, int top)
        {
            _logger.LogInternalInformation($"[GetAPIMErrorLogsAsync] Invoked with resourceId: {apimInstanceResourceId}, startTime: {startTime}, endTime: {endTime}, statusCode: {statusCode ?? "Any"}, top: {top}");

            string startIso = startTime.ToString("o");
            string endIso = endTime.ToString("o");
            string timeSpan = $"{startIso}/{endIso}";

            var queryString = new StringBuilder($@"
                ApiManagementGatewayLogs
                | where _ResourceId == '{apimInstanceResourceId.ToLower()}'
                | where TimeGenerated between(datetime('{startIso}') .. datetime('{endIso}'))
                | where IsRequestSuccess == 0");

            if (!string.IsNullOrWhiteSpace(statusCode))
            {
                queryString.Append($@"
                | where ResponseCode == '{statusCode}'");
            }

            // Now add top and project
            queryString.Append($@"
                | top {top} by TimeGenerated desc
                | project
                    TimeGenerated,
                    BackendId,
                    ApiId,
                    OperationId,
                    Url,
                    Method,
                    ResponseCode,
                    LastErrorReason,
                    LastErrorMessage,
                    BackendUrl,
                    BackendTime,
                    BackendResponseBody,
                    BackendResponseCode,
                    Category
                ");


            return await _armHelper.ExecuteLogAnalyticsQuery(apimInstanceResourceId, queryString.ToString(), timeSpan);
        }

        #region Azure Activity Log Methods

        public async Task<string> GetAPIMActivityLogsAsync(string apimResourceId, DateTime startTime, DateTime endTime)
        {
            _logger.LogInternalInformation($"[GetAPIMActivityLogsAsync] Invoked with resourceId: {apimResourceId}, startTime: {startTime}, endTime: {endTime}");

            string subscriptionId = apimResourceId.Split('/')[2];
            string apiVersion = APIManagementHelper.Constants.ActivityLogApiVer;
            string managementAzureBaseUrl = APIManagementHelper.Constants.ManagementAzureBaseUrl;

            string filter = $"eventTimestamp ge '{startTime:O}' and eventTimestamp le '{endTime:O}' and resourceUri eq '{apimResourceId}'";
            string requestUrl = $"{managementAzureBaseUrl}/subscriptions/{subscriptionId}/providers/Microsoft.Insights/eventtypes/management/values?api-version={apiVersion}&$filter={Uri.EscapeDataString(filter)}";

            try
            {
                _logger.LogInternalInformation($"GetAPIMActivityLogsAsync: Fetching activity logs from URL: {requestUrl}");
                var appActivityLogs = await _armHelper.GetResourceByURL(requestUrl);

                if (string.IsNullOrWhiteSpace(appActivityLogs))
                {
                    _logger.LogInternalWarning("GetAPIMActivityLogsAsync: Received empty response from ARM.");
                    return "No activity log entries found.";
                }

                var jsonAppActivityLogs = JObject.Parse(appActivityLogs);
                var activityLogEvents = jsonAppActivityLogs["value"] as JArray;
                if (activityLogEvents == null || !activityLogEvents.Any())
                {
                    _logger.LogInternalInformation("GetAPIMActivityLogsAsync: No activity log entries found in response.");
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
                _logger.LogInternalError(ex, $"GetAPIMActivityLogsAsync: Exception occurred for resourceId: {apimResourceId}");
                return $"Error: Exception occurred while fetching activity logs: {ex.Message}";
            }
        }

        #endregion

        #region Api Gateway Logs Methods

        public async Task<string> GetAPIMFailureRateByApiOperationAsync(string apiManagementResourceId, DateTime startTime, DateTime endTime)
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
                        | where _ResourceId == '{apiManagementResourceId.ToLower()}'
                        | where TimeGenerated between(datetime('{startIso}') ..datetime('{endIso}'))
                        | summarize
                            TotalCount = count(),
                            FailedCount = countif(IsRequestSuccess == 0),
                            ResponseCode = arg_max(TimeGenerated, ResponseCode),
                            LastErrorReason = arg_max(TimeGenerated, LastErrorReason)
                            by BackendId, ApiId, OperationId
                        | extend
                            FailureRatePercent = iif(TotalCount == 0, 0.0, todouble(FailedCount) / todouble(TotalCount) * 100)
                        | order by FailureRatePercent desc
                        | project
                            BackendId,
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
                return string.Empty;
            }
        }

        public async Task<string> GetAPIMRecentFailedRequestsAsync(string apiManagementResourceId, TimeSpan lookback, int topN)
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
                    | where _ResourceId == '{apiManagementResourceId}'
                    | where TimeGenerated between (datetime({startIso}) .. datetime({endIso}))
                    | where IsRequestSuccess == 0
                    | order by TimeGenerated desc
                    | take {topN}
                    | project
                        TimeGenerated,
                        CorrelationId,
                        BackendId,
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
                return string.Empty;
            }
        }

        #endregion

        #region APIM APIs and Operations Methods

        public async Task<List<APIManagementApiDescriptor>> GetAPIMApisAsync(string apiManagementResourceId, string workspaceName)
        {
            string apiVersion = APIManagementHelper.Constants.APIMAPIVersion;
            string managementAzureBaseUrl = APIManagementHelper.Constants.ManagementAzureBaseUrl;

            string workspaceSegment = string.IsNullOrWhiteSpace(workspaceName) ? string.Empty : $"/workspaces/{workspaceName}";
            var requestUrl = $"{managementAzureBaseUrl}{apiManagementResourceId}{workspaceSegment}/apis?api-version={apiVersion}";

            try
            {
                var res = await _armHelper.GetResourceByURL(requestUrl);

                using var doc = JsonDocument.Parse(res);
                if (!doc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    _logger.LogInternalError($"GetAPIMApisAsync: 'value' property not found in response for resourceId {apiManagementResourceId}.");
                    return null;
                }

                return JsonSerializer.Deserialize<List<APIManagementApiDescriptor>>(valueElement.GetRawText()) ?? new List<APIManagementApiDescriptor>();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"GetAPIMApisAsync: Exception occurred while fetching APIs for resourceId {apiManagementResourceId}: {ex.Message}");
                return null;
            }
        }

        public async Task<APIManagementApiDescriptor> GetAPIDetailsByNameAsync(string apiManagementResourceId, string apiName, string workspaceName)
        {
            string normalizedAPIName = apiName.ToLower().Replace(" ", "-");
            string workspaceSegment = string.IsNullOrWhiteSpace(workspaceName) ? string.Empty : $"/workspaces/{workspaceName}";

            string apiVersion = APIManagementHelper.Constants.APIMAPIVersion;
            string managementAzureBaseUrl = APIManagementHelper.Constants.ManagementAzureBaseUrl;

            var requestUrl = $"{managementAzureBaseUrl}{apiManagementResourceId}{workspaceSegment}/apis/{normalizedAPIName}?api-version={apiVersion}";

            try
            {
                var res = await _armHelper.GetResourceByURL(requestUrl);
                return JsonSerializer.Deserialize<APIManagementApiDescriptor>(res);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"GetAPIDetailsByNameAsync: Exception occurred while fetching API '{apiName}' for resourceId {apiManagementResourceId}: {ex.Message}");
                return null;
            }
        }

        public async Task<ApiPolicyResource> GetPoliciesByApiAsync(string apiManagementResourceId, string apiName)
        {
            _logger.LogInternalInformation($"[GetPoliciesByApiAsync] Invoked with resourceId: {apiManagementResourceId}, apiName: {apiName}");

            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                var subResource = new ResourceIdentifier(apiManagementResourceId);
                string subscriptionId = subResource.SubscriptionId;
                string resourceGroupName = subResource.ResourceGroupName;
                string serviceName = subResource.Name;
                string apiId = apiName.ToLower().Replace(" ", "-");

                ResourceIdentifier apiResourceId = ApiResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, serviceName, apiId);
                ApiResource api = armClient.GetApiResource(apiResourceId);

                // Get the collection of this ApiPolicyResource
                ApiPolicyCollection collection = api.GetApiPolicies();

                PolicyName policyId = PolicyName.Policy;
                NullableResponse<ApiPolicyResource> response = await collection.GetIfExistsAsync(policyId);
                if (response.Value == null)
                {
                    _logger.LogInternalError($"No policies found for API: {apiName} with resourceId: {apiManagementResourceId}");
                }
                ApiPolicyResource result = response.HasValue ? response.Value : null;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetPoliciesByApiAsync with resourceId: {apiManagementResourceId}, apiName: {apiName}");
                return null;
            }
        }

        public async Task<ApiOperationPolicyResource> GetPoliciesByOperationAsync(string apiManagementResourceId, string apiName, string operationId)
        {
            _logger.LogInternalInformation($"[GetPoliciesByOperationAsync] Invoked with resourceId: {apiManagementResourceId}, apiName: {apiName}, operationId: {operationId}");

            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                var subResource = new ResourceIdentifier(apiManagementResourceId);
                string subscriptionId = subResource.SubscriptionId;
                string resourceGroupName = subResource.ResourceGroupName;
                string serviceName = subResource.Name;
                string normalizedApiId = apiName.ToLower().Replace(" ", "-");
                string normalizedOperationId = operationId.ToLower().Replace(" ", "-");

                ResourceIdentifier apiResourceId = ApiResource.CreateResourceIdentifier(
                    subscriptionId, resourceGroupName, serviceName, normalizedApiId);
                ApiResource api = armClient.GetApiResource(apiResourceId);

                ResourceIdentifier operationResourceId = ApiOperationResource.CreateResourceIdentifier(
                    subscriptionId, resourceGroupName, serviceName, normalizedApiId, normalizedOperationId);
                ApiOperationResource operation = armClient.GetApiOperationResource(operationResourceId);

                // Get the collection of this ApiOperationPolicyResource
                ApiOperationPolicyCollection collection = operation.GetApiOperationPolicies();

                PolicyName policyId = PolicyName.Policy;
                NullableResponse<ApiOperationPolicyResource> response = await collection.GetIfExistsAsync(policyId);
                if (response.Value == null)
                {
                    _logger.LogInternalError($"No policies found for operation: {operationId} in API: {apiName} with resourceId: {apiManagementResourceId}");
                }
                ApiOperationPolicyResource result = response.HasValue ? response.Value : null;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetPoliciesByOperationAsync with resourceId: {apiManagementResourceId}, apiName: {apiName}, operationId: {operationId}");
                return null;
            }
        }

        public async Task<ApiManagementPolicyResource> GetGlobalApimPolicyAsync(string apiManagementResourceId)
        {
            _logger.LogInternalInformation($"[GetGlobalApimPolicies] Invoked with resourceId: {apiManagementResourceId}");

            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                // Get the API Management service resource
                ApiManagementServiceResource apimService = armClient.GetApiManagementServiceResource(new ResourceIdentifier(apiManagementResourceId));

                // Get the collection of policies at the API Management service level
                ApiManagementPolicyCollection policyCollection = apimService.GetApiManagementPolicies();

                // Get the global policy (if it exists)
                PolicyName policyId = PolicyName.Policy;
                NullableResponse<ApiManagementPolicyResource> response = await policyCollection.GetIfExistsAsync(policyId);

                if (response.Value == null)
                {
                    _logger.LogInternalError($"No global policies found for API Management service: {apiManagementResourceId}");
                }

                return response.HasValue ? response.Value : null;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetAllApimPolicies with resourceId: {apiManagementResourceId}");
                return null;
            }
        }

        public async Task<List<APIManagementApiOperationSummary>> GetAPIOperationsByApiAsync(string apiManagementResourceId, string apiName, string workspaceName)
        {
            string normalizedAPIName = apiName.ToLower().Replace(" ", "-");
            string workspaceSegment = string.IsNullOrWhiteSpace(workspaceName) ? string.Empty : $"/workspaces/{workspaceName}";

            string apiVersion = APIManagementHelper.Constants.APIMAPIVersion;
            string managementAzureBaseUrl = APIManagementHelper.Constants.ManagementAzureBaseUrl;

            var requestUrl = $"{managementAzureBaseUrl}{apiManagementResourceId}{workspaceSegment}/apis/{normalizedAPIName}/operations?api-version={apiVersion}"; 
            try
            {
                var res = await _armHelper.GetResourceByURL(requestUrl);
                using var doc = JsonDocument.Parse(res);

                if (!doc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    _logger.LogInternalError(null, $"GetAPIOperationsByApiAsync: 'value' property not found in response for resourceId {apiManagementResourceId}, API {apiName}.");
                    return null;
                }

                return JsonSerializer.Deserialize<List<APIManagementApiOperationSummary>>(valueElement.GetRawText()) ?? new List<APIManagementApiOperationSummary>();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"GetAPIOperationsByApiAsync: Exception occurred while fetching operations for resourceId {apiManagementResourceId}, API {apiName}: {ex.Message}");
                return null;
            }
        }

        public async Task<APIManagementApiOperationDescriptor> GetAPIOperationDetailedInfoAsync(string apiManagementResourceId, string apiName, string operationName, string workspaceName)
        {
            string normalizedAPIName = apiName.ToLower().Replace(" ", "-");
            string normalizedOperationName = operationName.ToLower().Replace(" ", "-");
            string workspaceSegment = string.IsNullOrWhiteSpace(workspaceName) ? string.Empty : $"/workspaces/{workspaceName}";

            string apiVersion = APIManagementHelper.Constants.APIMAPIVersion;
            string managementAzureBaseUrl = APIManagementHelper.Constants.ManagementAzureBaseUrl;

            var requestUrl = $"{managementAzureBaseUrl}{apiManagementResourceId}{workspaceSegment}/apis/{normalizedAPIName}/operations/{normalizedOperationName}?api-version={apiVersion}";
            try
            {
                var res = await _armHelper.GetResourceByURL(requestUrl);
                return JsonSerializer.Deserialize<APIManagementApiOperationDescriptor>(res) ?? null;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"GetAPIOperationDetailedInfoAsync: Exception occurred while fetching operation '{operationName}' for resourceId {apiManagementResourceId}, API {apiName}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region VNet, Subnet, and NSGS

        public async Task<string> CheckForVirtualNetworkIssuesAsync(string apimResourceId, DateTime issueStartTime, DateTime issueEndTime)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== API Management Virtual Network Diagnostic Report ===");
            sb.AppendLine($"Resource ID: {apimResourceId}");
            sb.AppendLine($"Issue Timeframe: {issueStartTime:u} - {issueEndTime:u}");
            sb.AppendLine($"Diagnostic Run At: {DateTime.UtcNow:u}");
            sb.AppendLine("--------------------------------------------------------");

            try
            {
                var apimProps = await GetApimNodePropertiesAsync(apimResourceId);

                if (apimProps == null)
                {
                    sb.AppendLine("❌ Could not find API Management resource in graph DB.");
                    sb.AppendLine("--------------------------------------------------------");
                    sb.AppendLine("End of diagnostic report.");
                    return sb.ToString();
                }

                if (string.IsNullOrWhiteSpace(apimProps.SubnetResourceId))
                {
                    sb.AppendLine("❌ This API Management instance is not connected to any virtual network subnet.");
                    sb.AppendLine("--------------------------------------------------------");
                    sb.AppendLine("End of diagnostic report.");
                    return sb.ToString();
                }

                SubnetResourceInfo subnetResourceInfo = apimProps.SubnetResourceId;

                string subscriptionId = subnetResourceInfo.SubscriptionId;
                string subnetName = subnetResourceInfo.SubnetName;
                string vnetName = subnetResourceInfo.VnetName;
                string vnetType = apimProps?.VirtualNetworkType;

                sb.AppendLine($"✅ Subnet found: {subnetName}");
                sb.AppendLine($"✅ VNet Name: {vnetName}");
                sb.AppendLine($"ℹ️  Virtual Network Type: {vnetType}");

                var customRulesSummary = await GetNSGRulesForApiManagementAsync(apimResourceId, true);

                if (customRulesSummary.Any())
                {
                    sb.AppendLine("--- Custom NSG Rules ---");
                    foreach (var rule in customRulesSummary)
                    {
                        sb.AppendLine($"Rule: {rule.Name} | {rule.Direction} | {rule.Access} | {rule.Protocol} | {rule.Source} → {rule.Destination} | Port: ??? | Priority: {rule.Priority}");
                    }
                }
                else
                {
                    sb.AppendLine("✅ No custom NSG rules found.");
                }

                sb.AppendLine($"\n--- NSG Activity Log: ({issueStartTime:u} to {issueEndTime:u}) ---");
                var nsgLogSummary = await GetNSGActivityLogsAsync(apimResourceId, topNAzureLogs:40, maxFindings:10);
                sb.AppendLine(nsgLogSummary);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"[CheckForVirtualNetworkIssuesAsync] Failed for resource: {apimResourceId}");
                sb.AppendLine($"\n❌ Error during diagnosis: {ex.Message}");
            }

            sb.AppendLine("\n--------------------------------------------------------");
            sb.AppendLine("End of diagnostic report.");
            return sb.ToString();
        }

        public async Task<VirtualNetworkDetails?> GetVNetConfigurationForApiManagementAsync(string apimResourceId)
        {
            _logger.LogInternalInformation($"[GetVNetConfigurationForApiManagementAsync] Invoked with resourceId: {apimResourceId}");

            try
            {
                var apimProps = await GetApimNodePropertiesAsync(apimResourceId);
                if (apimProps == null)
                {
                    _logger.LogInternalInformation("❌ Could not find API Management resource in the graph database.");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(apimProps.SubnetResourceId))
                {
                    _logger.LogInternalInformation("❌ subnetResourceId not found for the APIM resource.");
                    return null;
                }

                SubnetResourceInfo subnetResourceInfo = apimProps.SubnetResourceId;

                string subscriptionId = subnetResourceInfo.SubscriptionId;
                string resourceGroupName = subnetResourceInfo.ResourceGroupName;
                string vnetName = subnetResourceInfo.VnetName;

                string apiVersion = APIManagementHelper.Constants.VirtualNetworkAPIVer;
                string managementAzureBaseUrl = APIManagementHelper.Constants.ManagementAzureBaseUrl;

                string requestUrl = $"{managementAzureBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{vnetName}?api-version={apiVersion}";

                var response = await _armHelper.GetResourceByURL(requestUrl);
                if (string.IsNullOrWhiteSpace(response))
                {
                    _logger.LogInternalInformation("❌ No response received from Azure ARM when retrieving VNet config.");
                    return null;
                }

                var json = JObject.Parse(response);
                var properties = json["properties"];

                var vnetDetails = new VirtualNetworkDetails
                {
                    Name = json["name"]?.ToString() ?? "",
                    Location = json["location"]?.ToString() ?? "",
                    AddressPrefixes = properties?["addressSpace"]?["addressPrefixes"]?.Select(p => p.ToString()).ToList() ?? new(),
                    DnsServers = properties?["dhcpOptions"]?["dnsServers"]?.Select(p => p.ToString()).ToList() ?? new()
                };

                if (properties?["subnets"] is JArray subnetsArray)
                {
                    foreach (var subnet in subnetsArray)
                    {
                        vnetDetails.Subnets.Add(new VirtualSubnetDetails
                        {
                            Name = subnet["name"]?.ToString() ?? "",
                            AddressPrefix = subnet["properties"]?["addressPrefix"]?.ToString() ?? "",
                            NetworkSecurityGroupId = subnet["properties"]?["networkSecurityGroup"]?["id"]?.ToString(),
                            PrivateEndpointPolicies = subnet["properties"]?["privateEndpointNetworkPolicies"]?.ToString(),
                            ServiceEndpoints = subnet["properties"]?["serviceEndpoints"]?.Select(e => e["service"]?.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new()
                        });
                    }
                }

                return vnetDetails;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"GetVNetConfigurationForApiManagementAsync: Exception occurred for resourceId: {apimResourceId}");
                return null;
            }
        }

        public async Task<List<NSGRuleDetails>> GetNSGRulesForApiManagementAsync(string apimResourceId, bool getCustomOnly = false)
        {
            _logger.LogInternalInformation($"[GetNSGRulesForApiManagementAsync] Invoked with resourceId: {apimResourceId}, getCustomOnly: {getCustomOnly}");

            var rulesList = new List<NSGRuleDetails>();

            try
            {
                string formattedResourceId = apimResourceId.ToLower().Replace("/", "_");

                string query = $@"
                    g.V('{formattedResourceId}')
                     .values('subnetResourceId');";

                var resultSubnets = await _databaseClient.Query(query);

                if (resultSubnets == null || !resultSubnets.Any())
                {
                    _logger.LogInternalInformation("No subnets found for API Management resource.");
                    return rulesList;
                }

                var subnetResourceId = resultSubnets.FirstOrDefault()?.ToString();
                var armClient = await _armClientFactory.GetArmOperationClient();
                var subnet = armClient.GetSubnetResource(new ResourceIdentifier(subnetResourceId));
                var subnetData = await subnet.GetAsync();
                var subnetDataNSGGroup = subnetData.Value.Data.NetworkSecurityGroup;

                if (string.IsNullOrWhiteSpace(subnetDataNSGGroup.Id))
                {
                    _logger.LogInternalInformation($"NSG ID is null for subnet: {subnetResourceId}");
                    return rulesList;
                }

                string nsgId = subnetDataNSGGroup.Id!;
                var nsg = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgId));
                var nsgData = await nsg.GetAsync();

                var rawContent = nsgData.GetRawResponse().Content.ToString();
                var json = JObject.Parse(rawContent);
                var securityRules = json["properties"]?["securityRules"] as JArray;

                if (securityRules == null || !securityRules.Any())
                    return rulesList;

                foreach (var rule in securityRules)
                {
                    var name = rule["name"]?.ToString() ?? "Unnamed Rule";

                    if (getCustomOnly && !IsCustomNSGRule(name))
                        continue;
                    
                    rulesList.Add(new NSGRuleDetails
                    {
                        NSGId = nsgId,
                        Name = name,
                        Direction = rule["properties"]?["direction"]?.ToString() ?? "",
                        Access = rule["properties"]?["access"]?.ToString() ?? "",
                        Protocol = rule["properties"]?["protocol"]?.ToString() ?? "",
                        Priority = int.TryParse(rule["properties"]?["priority"]?.ToString(), out var p) ? p : 0,
                        Source = rule["properties"]?["sourceAddressPrefix"]?.ToString() ?? "",
                        Destination = rule["properties"]?["destinationAddressPrefix"]?.ToString() ?? "",
                        Type = getCustomOnly ? "Custom" : (name.StartsWith("NRMS", StringComparison.OrdinalIgnoreCase) ? "System" : "Custom")
                    });
                }

                return rulesList;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetNSGRulesForApiManagementAsync with resourceId: {apimResourceId}");
                return rulesList;
            }
        }

        public async Task<string> GetNSGActivityLogsAsync(string apimResourceId, int topNAzureLogs, int maxFindings)
        {
            _logger.LogInternalInformation($"[GetNSGActivityLogsAsync] Invoked with APIM resourceId: {apimResourceId}, top: {topNAzureLogs}");

            string subscriptionId = apimResourceId.Split('/')[2];
            string apiVersion = APIManagementHelper.Constants.ActivityLogApiVer;
            string resourceType = Constants.NetworkSecurityGroupType;
            string managementAzureBaseUrl = APIManagementHelper.Constants.ManagementAzureBaseUrl;

            // Azure Activity Logs API only supports querying data from the past 90 days.
            // Adding a start time filter to ensure compliance and avoid BadRequest errors.
            var startTime = DateTime.UtcNow.AddDays(-90).ToString("o"); 
            string filter = $"eventTimestamp ge {startTime} and resourceType eq '{resourceType}'";
            string orderByDesc = "$orderby=eventTimestamp desc";
            string topN = $"$top={topNAzureLogs}";
            string requestUrl = $"{managementAzureBaseUrl}/subscriptions/{subscriptionId}/providers/Microsoft.Insights/eventtypes/management/values?api-version={apiVersion}&$filter={Uri.EscapeDataString(filter)}&{orderByDesc}&{topN}";

            try
            {
                _logger.LogInternalInformation($"GetNSGActivityLogsAsync: Fetching activity logs from URL: {requestUrl}");
                var response = await _armHelper.GetResourceByURL(requestUrl);

                if (string.IsNullOrWhiteSpace(response))
                {
                    _logger.LogInternalWarning("GetNSGActivityLogsAsync: Received empty response from ARM.");
                    return "No activity log entries found.";
                }

                var jsonLogs = JObject.Parse(response);
                var activityLogEvents = jsonLogs["value"] as JArray;
                if (activityLogEvents == null || !activityLogEvents.Any())
                {
                    _logger.LogInternalInformation("GetNSGActivityLogsAsync: No activity log entries found in response.");
                    return "No activity log entries found.";
                }

                var sb = new StringBuilder();
                int findingsCount = 0;

                foreach (var activityEvent in activityLogEvents)
                {
                    if (findingsCount >= maxFindings)
                        break;

                    try
                    {
                        string action = activityEvent["operationName"]?["value"]?.ToString();
                        string eventName = activityEvent["eventName"]?["value"]?.ToString();
                        string caller = activityEvent["caller"]?.ToString();
                        string timestampStr = activityEvent["eventTimestamp"]?.ToString();
                        string status = activityEvent["status"]?["localizedValue"]?.ToString();
                        string resourceUri = activityEvent["httpRequest"]?["uri"]?.ToString();

                        DateTime.TryParse(timestampStr, out var timestamp);

                        bool isRuleChange = action?.Contains(APIManagementHelper.Constants.SecurityRuleAction, StringComparison.OrdinalIgnoreCase) ?? false;
                        bool isNSGLevelChange =
                            (action?.Contains(APIManagementHelper.Constants.NSGWriteAction, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (action?.Contains(APIManagementHelper.Constants.SecurityRuleActionTitle, StringComparison.OrdinalIgnoreCase) ?? false);

                        if (isRuleChange || isNSGLevelChange)
                        {
                            findingsCount++;

                            sb.AppendLine($"\n🔧 Operation: {action}");
                            sb.AppendLine($"- Timestamp: {timestamp:u}");
                            sb.AppendLine($"- Event: {eventName}");
                            sb.AppendLine($"- Status: {status}");
                            sb.AppendLine($"- Resource URI: {resourceUri}");
                            sb.AppendLine($"- Caller: {caller}");

                            sb.AppendLine(isRuleChange
                                ? "⚠️  NSG rule modification detected."
                                : "⚠️  Entire NSG configuration was modified.");
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"⚠️  Failed to parse a log entry: {ex.Message}");
                    }
                }

                if (findingsCount == 0)
                    sb.AppendLine("✅ No NSG or policy changes found.");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"GetNSGActivityLogsAsync: Exception occurred for resourceId: {apimResourceId}");
                return $"Error: Exception occurred while fetching NSG activity logs: {ex.Message}";
            }
        }

        public async Task<bool> APIMRemoveNSGRuleAsync(string nsgResourceId, string ruleName)
        {
            _logger.LogInternalInformation($"[APIMRemoveNSGRuleAsync] Invoked with NSG resourceId: {nsgResourceId}, ruleName: {ruleName}");
            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                var nsgResource = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgResourceId));
                await nsgResource.GetAsync();

                // Get the security rules collection
                SecurityRuleCollection securityRules = nsgResource.GetSecurityRules();
                if (securityRules == null)
                {
                    _logger.LogInternalInformation($"SecurityRuleCollection is null for NSG resource: {nsgResourceId}");
                    return false;
                }

                try
                {
                    // Check if the rule exists
                    var existingRule = await securityRules.GetAsync(ruleName);
                    // Delete the rule
                    _logger.LogInternalInformation($"Removing security rule '{ruleName}' from NSG {nsgResourceId}");
                    var armOperation = await existingRule.Value.DeleteAsync(WaitUntil.Completed);

                    _logger.LogInternalInformation(armOperation.HasCompleted
                        ? $"Successfully removed security rule '{ruleName}' from NSG {nsgResourceId}"
                        : $"Failed to remove security rule '{ruleName}' from NSG {nsgResourceId}"
                        );

                    return armOperation.HasCompleted;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    // Rule doesn't exist, nothing to remove
                    _logger.LogInternalInformation($"Security rule '{ruleName}' not found in NSG {nsgResourceId}, nothing to remove");
                    return true;
                }
                ;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in APIMRemoveNSGRuleAsync for {nsgResourceId} with rule {ruleName}");
                return false;
            }
        }

        public async Task<bool> APIMModifyNSGRuleAsync(string nsgResourceId, string ruleName, string? priority = null, string? access = null, string? direction = null, string? protocol = null, string? sourcePortRange = null, string? destinationPortRange = null, string? sourceAddressPrefix = null, string? destinationAddressPrefix = null, string? description = null)
        {
            _logger.LogInternalInformation($"[APIMModifyNSGRuleAsync] Invoked with NSG resourceId: {nsgResourceId}, ruleName: {ruleName}");

            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                var nsgResource = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgResourceId));
                await nsgResource.GetAsync();

                var securityRules = nsgResource.GetSecurityRules();
                if (securityRules == null)
                {
                    _logger.LogInternalInformation($"SecurityRuleCollection is null for NSG resource: {nsgResourceId}");
                    return false;
                }

                try
                {
                    var existingRuleResponse = await securityRules.GetAsync(ruleName);
                    var existingRule = existingRuleResponse.Value;
                    var data = existingRule.Data;

                    // Update only the provided parameters
                    if (!string.IsNullOrEmpty(priority)) data.Priority = int.Parse(priority);
                    if (!string.IsNullOrEmpty(access)) data.Access = access;
                    if (!string.IsNullOrEmpty(direction)) data.Direction = direction;
                    if (!string.IsNullOrEmpty(protocol)) data.Protocol = protocol;
                    if (!string.IsNullOrEmpty(sourcePortRange)) data.SourcePortRange = sourcePortRange;
                    if (!string.IsNullOrEmpty(destinationPortRange)) data.DestinationPortRange = destinationPortRange;
                    if (!string.IsNullOrEmpty(sourceAddressPrefix)) data.SourceAddressPrefix = sourceAddressPrefix;
                    if (!string.IsNullOrEmpty(destinationAddressPrefix)) data.DestinationAddressPrefix = destinationAddressPrefix;
                    if (!string.IsNullOrEmpty(description)) data.Description = description;

                    _logger.LogInternalInformation($"Modifying security rule '{ruleName}' in NSG {nsgResourceId}");
                    var operation = await securityRules.CreateOrUpdateAsync(WaitUntil.Completed, ruleName, data);

                    _logger.LogInternalInformation(operation.HasCompleted
                        ? $"Successfully modified security rule '{ruleName}' in NSG {nsgResourceId}"
                        : $"Failed to modify security rule '{ruleName}' in NSG {nsgResourceId}");

                    return operation.HasCompleted;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogInternalInformation($"Security rule '{ruleName}' not found in NSG {nsgResourceId}, cannot modify");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in APIMModifyNSGRuleAsync for {nsgResourceId} with rule {ruleName}");
                return false;
            }
        }

        #endregion

        #region APIM Latency Trends

        public async Task<string> GetApiManagementGatewayLatencyTrendAsync(string apimResourceId, DateTime startTime, DateTime endTime)
        {
            var metrics = await GetLatencyMetricsAsync(apimResourceId, startTime, endTime, Constants.GatewayRequestsDuration, Constants.Gateway);

            if (!metrics.HasData)
            {
                return "No latency data available for the specified period.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== API Management Gateway Latency Diagnostic Report ===");
            sb.AppendLine($"Resource ID: {apimResourceId}");
            sb.AppendLine($"Timeframe: {startTime:u} - {endTime:u}");
            sb.AppendLine($"Diagnostic Run At: {DateTime.UtcNow:u}");
            sb.AppendLine("--------------------------------------------------");

            // Summary
            sb.AppendLine("\n[Summary]");
            sb.AppendLine($"- Average Gateway Request Duration: {metrics.OverallAvg:F2} ms");
            sb.AppendLine($"- Maximum Gateway Request Duration: {metrics.OverallMax:F2} ms");
            sb.AppendLine($"- Number of Spikes (>{APIManagementHelper.Constants.SpikeMultiplier}x average): {metrics.SpikeCount}");
            if (metrics.SpikeCount > 0)
                sb.AppendLine($"⚠️  High latency detected. Spikes above {APIManagementHelper.Constants.SpikeMultiplier}x the average may indicate performance or backend issues.");
            else
                sb.AppendLine("✅ No significant latency spikes detected.");

            // Trend Details
            sb.AppendLine("\n[Gateway Latency Trend Details]");
            sb.AppendLine("Timestamp                 | Avg (ms) | Max (ms)");
            sb.AppendLine("--------------------------|----------|---------");
            foreach (var p in metrics.LatencyPoints)
            {
                sb.AppendLine($"{p.Time:u} | {p.Avg,8:F2} | {p.Max,8:F2}");
            }

            // Spike Analysis
            sb.AppendLine("\n[Spike Analysis]");
            if (metrics.SpikePoints.Any())
            {
                foreach (var p in metrics.SpikePoints)
                {
                    sb.AppendLine($"⚠️  Spike at {p.Time:u}: Max Gateway Latency = {p.Max:F2} ms");
                }
                // If spikes, check backend latency
                sb.AppendLine("\n[Backend Latency Analysis]");
                sb.AppendLine(await GetApiManagementBackendLatencyTrendAsync(apimResourceId, startTime, endTime));
            }
            else
            {
                sb.AppendLine("No latency spikes above relative threshold detected.");
            }

            // Recommendations
            sb.AppendLine("\n[Recommendations]");
            sb.AppendLine("- Consider scaling up the instance.");

            sb.AppendLine("\n--------------------------------------------------");
            sb.AppendLine("End of diagnostic report.");
            return sb.ToString();
        }

        public async Task<string> GetApiManagementBackendLatencyTrendAsync(string apimResourceId, DateTime startTime, DateTime endTime)
        {
            var metrics = await GetLatencyMetricsAsync(apimResourceId, startTime, endTime, Constants.BackendRequestsDuration, Constants.Backend);

            if (!metrics.HasData)
            {
                return "No backend latency data available for the specified period.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("--- Backend Latency Diagnostic Report ---");
            sb.AppendLine($"- Average Backend Request Duration: {metrics.OverallAvg:F2} ms");
            sb.AppendLine($"- Maximum Backend Request Duration: {metrics.OverallMax:F2} ms");
            sb.AppendLine($"- Number of Spikes (>{APIManagementHelper.Constants.SpikeMultiplier}x average): {metrics.SpikeCount}");
            if (metrics.SpikeCount > 0)
                sb.AppendLine($"⚠️  High backend latency detected. Spikes above {APIManagementHelper.Constants.SpikeMultiplier}x the average may indicate backend performance issues.");
            else
                sb.AppendLine("✅ No significant backend latency spikes detected.");

            sb.AppendLine("\n[Backend Latency Trend Details]");
            sb.AppendLine("Timestamp                 | Avg (ms) | Max (ms)");
            sb.AppendLine("--------------------------|----------|---------");
            foreach (var p in metrics.LatencyPoints)
            {
                sb.AppendLine($"{p.Time:u} | {p.Avg,8:F2} | {p.Max,8:F2}");
            }

            sb.AppendLine("\n[Backend Spike Analysis]");
            if (metrics.SpikePoints.Any())
            {
                foreach (var p in metrics.SpikePoints)
                {
                    sb.AppendLine($"⚠️  Backend spike at {p.Time:u}: Max Backend Latency = {p.Max:F2} ms");
                }
            }
            else
            {
                sb.AppendLine("No backend latency spikes above relative threshold detected.");
            }

            sb.AppendLine("------------------------------------------");
            return sb.ToString();
        }

        private async Task<LatencyMetricsData> GetLatencyMetricsAsync(string apimResourceId, DateTime startTime, DateTime endTime, string metricName, string logType)
        {
            string apiVersion = APIManagementHelper.Constants.MetricsInsightsApiVer;
            string interval = APIManagementHelper.Constants.AppInsightsTimeInterval;
            string aggregations = APIManagementHelper.Constants.LatencyAggregations;
            string managementAzureBaseUrl = APIManagementHelper.Constants.ManagementAzureBaseUrl;
            string timespan = $"{startTime:o}/{endTime:o}";

            string requestUrl = $"{managementAzureBaseUrl}{apimResourceId}/providers/microsoft.insights/metrics" +
                                $"?api-version={apiVersion}" +
                                $"&metricnames={metricName}" +
                                $"&timespan={timespan}" +
                                $"&interval={interval}" +
                                $"&aggregation={aggregations}";

            var metricsResponse = await _armHelper.GetResourceByURL(requestUrl);
            var metrics = JObject.Parse(metricsResponse);

            var data = metrics["value"]?[0]?["timeseries"]?[0]?["data"];
            if (data == null || !data.Any())
            {
                _logger.LogInternalWarning($"No {logType} latency data found for apimResourceId: {apimResourceId} in the specified time range.");
                return new LatencyMetricsData();
            }

            var metricsData = new LatencyMetricsData();
            foreach (var point in data)
            {
                var avg = point["average"]?.Value<double?>();
                var max = point["maximum"]?.Value<double?>();
                var time = point["timeStamp"]?.Value<DateTime?>();
                if (time.HasValue)
                    metricsData.LatencyPoints.Add(new LatencyDataPoint(time.Value, avg, max));
            }

            // Calculate summary statistics
            var avgValues = metricsData.LatencyPoints.Where(p => p.Avg.HasValue).Select(p => p.Avg.Value).ToList();
            var maxValues = metricsData.LatencyPoints.Where(p => p.Max.HasValue).Select(p => p.Max.Value).ToList();
            metricsData.OverallAvg = avgValues.Any() ? avgValues.Average() : 0.0;
            metricsData.OverallMax = maxValues.Any() ? maxValues.Max() : 0.0;

            IdentifySpikePoints(metricsData);

            return metricsData;
        }

        private void IdentifySpikePoints(LatencyMetricsData metricsData)
        {
            if (metricsData.OverallAvg <= 0)
                return;

            double threshold = metricsData.OverallAvg * APIManagementHelper.Constants.SpikeMultiplier;

            metricsData.SpikePoints = metricsData.LatencyPoints
                .Where(p => p.Max.HasValue && p.Max.Value >= threshold)
                .ToList();

            metricsData.SpikeCount = metricsData.SpikePoints.Count;
        }

        public async Task<string> ScaleAPIMInstanceAsync(string apimResourceId, int newUnitCount)
        {
            _logger.LogInternalInformation($"[ScaleApiManagementInstanceAsync] Invoked with resourceId: {apimResourceId}, newUnitCount: {newUnitCount}");

            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                var apimServiceResource = armClient.GetApiManagementServiceResource(new ResourceIdentifier(apimResourceId));

                // Get the current configuration
                var apim = await apimServiceResource.GetAsync();
                var currentData = apim.Value.Data;
                int oldUnitCount = currentData.Sku.Capacity;
                string skuName = currentData.Sku.Name.ToString();

                _logger.LogInternalInformation($"Current SKU: {skuName}, Current Unit Count: {oldUnitCount}, Requested Unit Count: {newUnitCount}");

                if (oldUnitCount == newUnitCount)
                {
                    return $"No scaling needed. API Management instance is already at {oldUnitCount} units.";
                }

                // Prepare the patch object
                var patch = new ApiManagementServicePatch
                {
                    Sku = new ApiManagementServiceSkuProperties(currentData.Sku.Name, newUnitCount)
                };

                // Update the APIM instance using PATCH
                var updateOp = await apimServiceResource.UpdateAsync(WaitUntil.Completed, patch);
                var updated = await apimServiceResource.GetAsync();
                int afterUnitCount = updated.Value.Data.Sku.Capacity;

                _logger.LogInternalInformation($"Scaling operation completed. Unit count after scaling: {afterUnitCount}");

                if (afterUnitCount == newUnitCount)
                {
                    return $"Successfully scaled API Management instance from {oldUnitCount} to {afterUnitCount} units.";
                }
                else
                {
                    return $"Scaling operation completed, but unit count is {afterUnitCount} (expected {newUnitCount}).";
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in ScaleApiManagementInstanceAsync for {apimResourceId} with newUnitCount {newUnitCount}");
                return $"Failed to scale API Management instance: {ex.Message}";
            }
        }

        #endregion

        #region APIM Connected Resource Helpers

        public async Task<string> GetAPIMConnectedAppInsightsAsync(string apiManagementResourceId)
        {
            string apiVersion = APIManagementHelper.Constants.LoggersApiVer;
            string managementAzureBaseUrl = APIManagementHelper.Constants.ManagementAzureBaseUrl;

            // Call to list all of the app insights resources connected to the API Management instance
            var requestUrl = $"{managementAzureBaseUrl}{apiManagementResourceId}/loggers?api-version={apiVersion}";

            var connectedAppInsightsResources = await _armHelper.GetResourceByURL(requestUrl);
            JObject connectedAPIMAppInsights = JObject.Parse(connectedAppInsightsResources);

            // Extract subscriptionId from the API Management resourceId
            string[] apimParts = apiManagementResourceId.Split('/');
            string apimSubscriptionId = apimParts.Length > 2 ? apimParts[2] : string.Empty;
            if (string.IsNullOrEmpty(apimSubscriptionId))
            {
                _logger.LogInternalWarning($"Could not extract subscriptionId from API Management resourceId: {apiManagementResourceId}");
                return string.Empty;
            }

            var loggers = connectedAPIMAppInsights["value"];
            if (loggers == null)
            {
                return string.Empty;
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

            return string.Empty;
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

        private async Task<APIManagementNode?> GetApimNodePropertiesAsync(string apimResourceId)
        {
            string apimKey = apimResourceId.ToLower().Replace("/", "_");
            string query = $"g.V('{apimKey}').valueMap()";

            var results = await _databaseClient.Query(query);

            return new APIManagementNode(results.First());
        }

        private static bool IsCustomNSGRule(string ruleName)
        {
            return !ruleName.StartsWith(APIManagementHelper.Constants.NRMSRulePrefix, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

    }
}
