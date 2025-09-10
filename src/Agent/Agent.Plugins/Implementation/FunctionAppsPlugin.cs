// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Extensions.Logging;
using Agent.Core.Helpers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Agent.Core.Models;
using CoreConstants = Agent.Core.Constants;

namespace Agent.Plugins.Implementation;
public class FunctionAppsPlugin : IFunctionAppsPlugin
{
    private readonly IGraphDatabaseClient _databaseClient;
    private readonly ILogger<FunctionAppsPlugin> _logger;
    private readonly ArmHelper _armHelper;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string AzureManagementBatchUrl = "https://management.azure.com/batch?api-version=2020-06-01";
    private const int FunctionTriggerTimeoutSeconds = 300; // 5 minutes

    public FunctionAppsPlugin(
        IGraphDatabaseClient graphDatabaseClient,
        ILogger<FunctionAppsPlugin> logger,
        ArmHelper armHelper,
        IHttpClientFactory httpClientFactory)
    {
        _databaseClient = graphDatabaseClient;
        _logger = logger;
        _armHelper = armHelper;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<FunctionAppDescriptor?> GetFunctionAppInfoAsync(string resourceId)
    {
        _logger.LogInternalInformation($"[get_function_app_info] Invoked with resourceId: {resourceId}");

        try
        {
            string functionAppResourceId = resourceId.ToLower().Replace("/", "_");
            string query = $@"
                g.V().has('id', '{functionAppResourceId}').has('isDeleted', false)
                .hasLabel('{Constants.AppServiceType.ToLower()}')
                .has('kind', containing('{Constants.FunctionAppKind}'))
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";

            var result = await _databaseClient.Query(query);

            if (result == null || !result.Any())
            {
                _logger.LogInternalWarning($"Function App with ID '{resourceId}' not found in graph database.");
                return null;
            }

            var functionApp = result.First();
            var properties = functionApp["properties"];

            string name = functionApp["name"]?.ToString() ?? "";
            string kind = GetFirstPropertyValue(properties, "kind") ?? Constants.FunctionAppKind;
            string location = GetFirstPropertyValue(properties, "location");
            string sku = GetFirstPropertyValue(properties, "sku") ?? "Unknown";
            string state = GetFirstPropertyValue(properties, "state") ?? "Unknown";
            string resourceGroup = GetFirstPropertyValue(properties, "resourceGroupName");

            string vnetIdValue = GetFirstPropertyValue(properties, "vnetId");
            string? vnetId = string.IsNullOrEmpty(vnetIdValue) ? null : vnetIdValue;

            string stackVersionValue = GetFirstPropertyValue(properties, "stackVersion");
            string? stackVersion = string.IsNullOrEmpty(stackVersionValue) ? null : stackVersionValue;

            string planTypeValue = GetFirstPropertyValue(properties, "planType");
            string? planType = string.IsNullOrEmpty(planTypeValue) ? null : planTypeValue;

            string minTlsVersionValue = GetFirstPropertyValue(properties, "minTlsVersion");
            string? minTlsVersion = string.IsNullOrEmpty(minTlsVersionValue) ? null : minTlsVersionValue;

            bool? webSocketEnabled = TryParseBool(GetFirstPropertyValue(properties, "webSocketEnabled"));
            int? numberOfWorkers = TryParseInt(GetFirstPropertyValue(properties, "numberOfWorkers"));
            bool? autoHealEnabled = TryParseBool(GetFirstPropertyValue(properties, "autoHealEnabled"));
            bool? alwaysOn = TryParseBool(GetFirstPropertyValue(properties, "alwaysOn"));
            bool? healthCheckEnabled = TryParseBool(GetFirstPropertyValue(properties, "healthCheckEnabled"));

            return new FunctionAppDescriptor(
                ResourceId: resourceId,
                Name: name,
                Kind: kind,
                Location: location,
                Sku: sku,
                State: state,
                ResourceGroup: resourceGroup,
                VnetId: vnetId,
                StackVersion: stackVersion,
                PlanType: planType,
                MinTlsVersion: minTlsVersion,
                WebSocketEnabled: webSocketEnabled,
                NumberOfWorkers: numberOfWorkers,
                AutoHealEnabled: autoHealEnabled,
                AlwaysOn: alwaysOn,
                HealthCheckEnabled: healthCheckEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in GetFunctionAppInfoAsync with resourceId {resourceId}");
            return null;
        }
    }

    public async Task<IReadOnlyList<FunctionAppDescriptor>> ListFunctionAppsAsync(Guid subscriptionId)
    {
        _logger.LogInternalInformation($"[list_function_app_instances] Invoked with subscription {subscriptionId}");

        var functionApps = new List<FunctionAppDescriptor>();

        try
        {
            string query = $@"
                g.V().has('isDeleted', false)
                .has('subscriptionId', '{subscriptionId}')
                .hasLabel('{Constants.AppServiceType.ToLower()}')
                .has('kind', containing('{Constants.FunctionAppKind}'))
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";

            var result = await _databaseClient.Query(query);

            if (result == null || !result.Any())
            {
                _logger.LogInternalInformation($"No function apps found for subscription {subscriptionId} in graph database.");
                return functionApps;
            }

            foreach (var functionApp in result)
            {
                var properties = functionApp["properties"];

                string id = functionApp["id"].ToString();
                string resourceId = id.Replace("_", "/");

                string name = functionApp["name"]?.ToString() ?? "";
                string kind = GetFirstPropertyValue(properties, "kind") ?? Constants.FunctionAppKind;
                string location = GetFirstPropertyValue(properties, "location");
                string sku = GetFirstPropertyValue(properties, "sku") ?? "Unknown";
                string state = GetFirstPropertyValue(properties, "state") ?? "Unknown";
                string resourceGroup = GetFirstPropertyValue(properties, "resourceGroupName");

                // list function only contains the basic info.
                var functionAppDescriptor = new FunctionAppDescriptor(
                    ResourceId: resourceId,
                    Name: name,
                    Kind: kind,
                    Location: location,
                    Sku: sku,
                    State: state,
                    ResourceGroup: resourceGroup,
                    VnetId: null,
                    StackVersion: null,
                    PlanType: null,
                    MinTlsVersion: null,
                    WebSocketEnabled: null,
                    NumberOfWorkers: null,
                    AutoHealEnabled: null,
                    AlwaysOn: null,
                    HealthCheckEnabled: null);

                functionApps.Add(functionAppDescriptor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in ListFunctionAppsAsync with subscription {subscriptionId}");
            return new List<FunctionAppDescriptor>();
        }

        return functionApps;
    }

    private string GetFirstPropertyValue(dynamic properties, string propertyName)
    {
        if (properties == null)
        {
            return string.Empty;
        }

        var dict = properties as IDictionary<string, object>;
        if (dict == null || !dict.ContainsKey(propertyName))
        {
            return string.Empty;
        }

        var values = dict[propertyName];
        if (values is IEnumerable<object> enumerable)
        {
            var firstValue = enumerable.Cast<object>().FirstOrDefault();
            return firstValue?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private bool? TryParseBool(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        return null;
    }

    private int? TryParseInt(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (int.TryParse(value, out int result))
        {
            return result;
        }

        return null;
    }

    public async Task<List<string>> GetFunctionAppDeploymentSlotsAsync(string resourceId)
    {
        _logger.LogInternalInformation($"[get_function_app_deployment_slots] Invoked with resourceId: {resourceId}");

        try
        {
            // First, get the Function App information to check its SKU
            var functionAppInfo = await GetFunctionAppInfoAsync(resourceId);
            if (functionAppInfo == null)
            {
                _logger.LogInternalWarning($"Function App with ID '{resourceId}' not found.");
                return new List<string>();
            }

            // Check if the SKU supports deployment slots
            if (!SupportsDeploymentSlots(functionAppInfo.Sku))
            {
                _logger.LogInternalInformation($"Function App '{functionAppInfo.Name}' has SKU '{functionAppInfo.Sku}' which does not support deployment slots.");
                return new List<string>();
            }

            // Get deployment slots using ArmHelper
            var slotResourceIds = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);
            
            _logger.LogInternalInformation($"Found {slotResourceIds.Count} deployment slots for Function App '{functionAppInfo.Name}'.");
            return slotResourceIds;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in GetFunctionAppDeploymentSlotsAsync with resourceId {resourceId}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Determines if the given SKU supports deployment slots
    /// </summary>
    /// <param name="sku">The SKU name (e.g., Standard, Premium, etc.)</param>
    /// <returns>True if deployment slots are supported, false otherwise</returns>
    private static bool SupportsDeploymentSlots(string sku)
    {
        if (string.IsNullOrEmpty(sku))
            return false;

        // Convert to upper case for case-insensitive comparison
        string skuUpper = sku.ToUpperInvariant();

        // SKUs that support deployment slots:
        // - Standard (S1, S2, S3)
        // - Premium (P1, P2, P3, P1v2, P2v2, P3v2, P1v3, P2v3, P3v3, etc.)
        // - Premium V2 (P1V2, P2V2, P3V2)
        // - Premium V3 (P1V3, P2V3, P3V3)
        // - Isolated (I1, I2, I3, I1v2, I2v2, I3v2)
        
        // SKUs that do NOT support deployment slots:
        // - Free (F1)
        // - Shared (D1)
        // - Basic (B1, B2, B3)
        // - Consumption (Y1, Consumption)
        // - Flex Consumption (FC)

        return skuUpper.StartsWith("STANDARD") ||
               skuUpper.StartsWith("PREMIUM") ||
               skuUpper.StartsWith("P1") ||
               skuUpper.StartsWith("P2") ||
               skuUpper.StartsWith("P3") ||
               skuUpper.StartsWith("S1") ||
               skuUpper.StartsWith("S2") ||
               skuUpper.StartsWith("S3") ||
               skuUpper.StartsWith("ISOLATED") ||
               skuUpper.StartsWith("I1") ||
               skuUpper.StartsWith("I2") ||
               skuUpper.StartsWith("I3");
    }

    public async Task<FunctionTriggerResponse> TriggerTimerFunctionAsync(
        string functionAppResourceId, 
        string functionName)
    {
        _logger.LogInternalInformation($"[trigger_timer_function] Invoked with functionAppResourceId: {functionAppResourceId}, functionName: {functionName}");
        
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(functionAppResourceId))
            {
                return new FunctionTriggerResponse(false, null, null, "Function app resource ID cannot be empty", null);
            }

            if (string.IsNullOrWhiteSpace(functionName))
            {
                return new FunctionTriggerResponse(false, null, null, "Function name cannot be empty", null);
            }

            // Get function app information
            var functionAppInfo = await GetFunctionAppInfoAsync(functionAppResourceId);
            if (functionAppInfo == null)
            {
                return new FunctionTriggerResponse(false, null, null, $"Function app with ID '{functionAppResourceId}' not found", null);
            }

            var functionAppName = functionAppInfo.Name;
            
            // Get master key
            _logger.LogInternalInformation($"Retrieving master key from Azure");
            var masterKey = await RetrieveMasterKeyAsync(functionAppResourceId);
            
            if (string.IsNullOrWhiteSpace(masterKey))
            {
                return new FunctionTriggerResponse(false, null, null, "Failed to retrieve master key for function app", null);
            }

            // Validate that the function is a TimerTrigger
            var isTimerTrigger = await ValidateTimerTriggerFunctionAsync(functionAppName, functionName, masterKey);
            if (!isTimerTrigger)
            {
                return new FunctionTriggerResponse(false, null, null, $"Function '{functionName}' is not a TimerTrigger function. This method only supports TimerTrigger functions.", null);
            }

            // Construct the function trigger URL
            var triggerUrl = $"https://{functionAppName}.azurewebsites.net/admin/functions/{functionName}";
            
            _logger.LogInternalInformation($"Triggering TimerTrigger function at URL: {triggerUrl}");

            // Prepare the HTTP request
            using var httpClient = _httpClientFactory.CreateClient(CoreConstants.HttpClientForArmOperation);
            httpClient.Timeout = TimeSpan.FromSeconds(FunctionTriggerTimeoutSeconds);
            
            using var request = new HttpRequestMessage(HttpMethod.Post, triggerUrl);
            request.Headers.Add("x-functions-key", masterKey);
            
            // TimerTrigger functions don't require payload, send empty JSON object
            var jsonContent = "{}";
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Send the request
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInternalInformation($"Successfully triggered function '{functionName}' in app '{functionAppName}'");
                return new FunctionTriggerResponse(
                    true, 
                    response.StatusCode.ToString(), 
                    responseContent, 
                    null, 
                    stopwatch.Elapsed);
            }
            else
            {
                _logger.LogInternalWarning($"Failed to trigger function. Status: {response.StatusCode}, Response: {responseContent}");
                return new FunctionTriggerResponse(
                    false, 
                    response.StatusCode.ToString(), 
                    responseContent, 
                    $"Function trigger failed with status {response.StatusCode}", 
                    stopwatch.Elapsed);
            }
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            var errorMessage = $"Function trigger timed out after {FunctionTriggerTimeoutSeconds} seconds";
            _logger.LogInternalError(errorMessage);
            return new FunctionTriggerResponse(false, null, null, errorMessage, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogInternalError(ex, $"Error triggering function '{functionName}' in app '{functionAppResourceId}'");
            return new FunctionTriggerResponse(false, null, null, $"Error: {ex.Message}", stopwatch.Elapsed);
        }
    }

    private async Task<string?> RetrieveMasterKeyAsync(string functionAppResourceId)
    {
        try
        {
            // Parse resource ID to extract subscription, resource group, and app name
            var resourceIdParts = functionAppResourceId.Split('/');
            if (resourceIdParts.Length < 9)
            {
                _logger.LogInternalError($"Invalid function app resource ID format: {functionAppResourceId}");
                return null;
            }

            var subscriptionId = resourceIdParts[2];
            var resourceGroup = resourceIdParts[4];
            var functionAppName = resourceIdParts[8];

            // Prepare the batch request
            var listKeysUrl = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{functionAppName}/host/default/listKeys?api-version=2022-03-01";
            
            var batchRequest = new
            {
                requests = new[]
                {
                    new
                    {
                        httpMethod = "POST",
                        name = Guid.NewGuid().ToString(),
                        requestHeaderDetails = new { commandName = "WebsitesExtension.getAppKeys" },
                        url = listKeysUrl
                    }
                }
            };

            // Use HttpClient to make the batch request
            using var httpClient = _httpClientFactory.CreateClient(CoreConstants.HttpClientForArmOperation);
            
            using var request = new HttpRequestMessage(HttpMethod.Post, AzureManagementBatchUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(batchRequest), Encoding.UTF8, "application/json")
            };
            
            var httpResponse = await httpClient.SendAsync(request);
            
            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogInternalWarning($"Failed to retrieve master key. Status: {httpResponse.StatusCode}");
                return null;
            }
            
            var responseContent = await httpResponse.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<FunctionKeyResponse>(responseContent);

            if (response?.Responses?.FirstOrDefault()?.Content?.MasterKey != null)
            {
                _logger.LogInternalInformation("Successfully retrieved master key for function app");
                return response.Responses.First().Content!.MasterKey!;
            }

            _logger.LogInternalWarning("Failed to retrieve master key from batch API response");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error retrieving master key for function app {functionAppResourceId}");
            return null;
        }
    }

    private async Task<bool> ValidateTimerTriggerFunctionAsync(string functionAppName, string functionName, string masterKey)
    {
        try
        {
            // Get function metadata using admin API
            var functionMetadataUrl = $"https://{functionAppName}.azurewebsites.net/admin/functions/{functionName}";
            
            using var httpClient = _httpClientFactory.CreateClient(CoreConstants.HttpClientForArmOperation);
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            using var request = new HttpRequestMessage(HttpMethod.Get, functionMetadataUrl);
            request.Headers.Add("x-functions-key", masterKey);
            
            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInternalWarning($"Failed to retrieve function metadata for '{functionName}'. Status: {response.StatusCode}");
                return false;
            }
            
            var metadataContent = await response.Content.ReadAsStringAsync();
            
            // Check if the function has a TimerTrigger binding
            // The metadata should contain binding information including trigger type
            var isTimerTrigger = metadataContent.Contains("\"type\":\"timerTrigger\"", StringComparison.OrdinalIgnoreCase) ||
                               metadataContent.Contains("timerTrigger", StringComparison.OrdinalIgnoreCase);
                               
            _logger.LogInternalInformation($"Function '{functionName}' TimerTrigger validation result: {isTimerTrigger}");
            return isTimerTrigger;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error validating TimerTrigger function '{functionName}' in app '{functionAppName}'");
            return false;
        }
    }
}
