// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Web;
using Agent.Core.Configuration;
using Agent.Core.Exceptions;
using Agent.Core.Helpers.ArmModels;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Charts;
using Agent.Core.Services;
using Agent.Logging;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.CosmosDB.Models;
using Azure.ResourceManager.EventHubs;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ServiceBus;
using Azure.ResourceManager.ServiceBus.Models;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Resources;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Agent.Core.Helpers;

public class OperationDetail
{
    public string OperationName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string Caller { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? StatusMessage { get; set; } // Add new property for the full status message JSON
    public bool IsSuccessful { get; set; }  // Indicates if the operation was successful
}

public class ArmHelper
{
    private readonly ILogger<ArmHelper> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IArmClientFactory _armClientFactory;
    private readonly IAuthenticationService _authService;
    private readonly AzureSettings _azureSettings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IChatClient _chatClient;

    private readonly ICrawlerTriggerService _crawlerTriggerService;

    public static readonly ImmutableArray<string> AllowedReadVerbs = [
        "get",
        "list",
        "show",
        "query",
    ];

    public static readonly ImmutableArray<string> BlockedSubCommands = [
        "keyvault"
    ];

    public static readonly string AllowedReadVerbString = string.Join(", ", AllowedReadVerbs);

    /// <summary>
    /// Special command patterns that should be treated as read-only regardless of verbs
    /// These patterns are checked before the general verb-based logic
    /// </summary>
    public static readonly ImmutableArray<string> ReadOnlyCommandPatterns = [
        "az monitor log-analytics query",
    ];

    /// <summary>
    /// Special command patterns that should be treated as write commands regardless of verbs
    /// These patterns are checked before the general verb-based logic
    /// </summary>
    public static readonly ImmutableArray<string> WriteCommandPatterns = [
    ];

    public static readonly ImmutableArray<string> AllowedWriteVerbs = [
        "add",
        "create",
        "register", // for RPs and Features
        "unregister",
        "scale",
        "set",
        "stop",
        "update",
        "upgrade",
        "deploy",        // `az deployment group create` etc.
        "redeploy",      // VM redeployment
        "attach",        // attach/detach disks, policies, etc.
        "detach",
        "enable",        // enable/disable features, add-ons
        "disable",
        "import",        // storage, key-vault, etc.
        "export",
        "backup",        // key-vault, AKS cluster snapshots, …
        "restore",
        "move",          // resource moves across RGs/subs
        "rename",        // supported on a few resources
        "install",       // extension install/upgrade flows
        "uninstall",
        "purge",         // key-vault, app-config, log-analytics
        "invoke",        // run-command, function invoke
        "commit",        // ACR tasks, app-service slots
        "reimage",
        "failover-group",
        // Configuration / updates
        "update", "set", "patch", "apply-patches", "assess-patches",
        "upgrade", "deploy", "redeploy", "reapply", "commit",
        // Scale & size
        "scale", "resize",

        // Start/stop style actions
        "start", "stop", "restart", "deallocate",
        // Access & identity
        "assign", "grant", "revoke",
        // Networking & recovery
        "failover", "reset", "repair", "flush",
        // Promotion / traffic-shift
        "swap", "promote",
        // Misc utility
        "sync",
        "query",  // some RPs treat query as a POST that writes logs
        "restart", // left here for clarity even though in “start/stop” bucket
    ];

    public static readonly string AllowedWriteVerbString = string.Join(", ", AllowedWriteVerbs);

    public static readonly ImmutableArray<string> BlockedDeleteVerbs = [
        "delete",
        "remove",
    ];

    public static readonly ImmutableArray<string> WriteVerbs = [.. AllowedWriteVerbs, .. BlockedDeleteVerbs];

    // Crawler MI is used for production environment as current solution
    public ArmHelper(
        ILogger<ArmHelper> logger,
        IHttpClientFactory httpClientFactory,
        IArmClientFactory armClientFactory,
        IAuthenticationService authService,
        AzureSettings azureSettings,
        IHostEnvironment hostEnvironment,
        ICrawlerTriggerService crawlerTriggerService,
        IChatClient chatClient)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _armClientFactory = armClientFactory;
        _authService = authService;
        _azureSettings = azureSettings;
        _hostEnvironment = hostEnvironment;
        _crawlerTriggerService = crawlerTriggerService;
        _chatClient = chatClient;
    }

    public async Task<List<AzureSubscription>> GetSubscriptionsAsync()
    {
        List<AzureSubscription> allSubs = [];

        var armClient = await _armClientFactory.GetArmOperationClient();
        await foreach (SubscriptionResource subscription in armClient.GetSubscriptions().GetAllAsync())
        {
            allSubs.Add(new AzureSubscription(subscription.Data.SubscriptionId, subscription.Data.DisplayName, []));
        }

        return allSubs;
    }

    public async Task<List<string>> GetAllResourceUriAsync(string subscriptionId)
    {
        List<string> resourceUrls = [];
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return resourceUrls;

        string armUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/resources?api-version=2021-04-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, armUrl);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {

            string responseBody = await response.Content.ReadAsStringAsync();
            JObject jsonObj = JObject.Parse(responseBody);

            JArray? values = jsonObj["value"] as JArray;
            if (values == null)
            {
                return resourceUrls;
            }

            foreach (JObject value in values)
            {
                string? id = value["id"]?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    resourceUrls.Add(id);
                }
            }

            return resourceUrls;
        }
        else
        {
            return resourceUrls;
        }
    }

    public async Task<string> CreateAutoScaleSetting(
        string subscriptionId,
        string resourceGroupName,
        string autoScaleSettingName,
        string location,
        string resourceId,
        int minCount,
        int maxCount,
        int targetCount,
        string profileName = "DefaultProfile",
        string metricName = "CpuPercentage",
        string operatorProperty = "GreaterThan",
        double threshold = 70.0,
        string timeAggregation = "Average",
        string statistic = "Average",
        string timeGrain = "PT1M",
        string timeWindow = "PT5M",
        string scaleDirection = "Increase",
        string scaleType = "ChangeCount",
        string scaleValue = "1",
        string cooldown = "PT5M")
    {
        try
        {
            var requestUrl = new Uri(new Uri("https://management.azure.com"),
                $"subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/Microsoft.Insights/autoscalesettings/{autoScaleSettingName}?api-version=2022-10-01");

            var requestBody = new
            {
                location = location,
                properties = new
                {
                    profiles = new[]
                    {
                    new
                    {
                        name = profileName, // Use the customizable name parameter
                        capacity = new
                        {
                            minimum = minCount.ToString(),
                            maximum = maxCount.ToString(),
                            @default = targetCount.ToString()
                        },
                        rules = new[]
                        {
                            new
                            {
                                metricTrigger = new
                                {
                                    metricName = metricName,
                                    metricResourceUri = resourceId,
                                    operatorProperty = operatorProperty,
                                    threshold = threshold,
                                    timeAggregation = timeAggregation,
                                    statistic = statistic,
                                    timeGrain = timeGrain,
                                    timeWindow = timeWindow
                                },
                                scaleAction = new
                                {
                                    direction = scaleDirection,
                                    type = scaleType,
                                    value = scaleValue,
                                    cooldown = cooldown
                                }
                            }
                        }
                    }
                }
                }
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Put, requestUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                if (CheckForUnauthorizedAccess(response))
                {
                    throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
                }
                throw new Exception("Failed to create auto-scale setting: " + await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while creating auto-scale setting", ex);
        }
    }

    public async Task<List<BasicAuthStatus>> CheckBasicAuth(List<string> resourceIds)
    {
        var output = new List<BasicAuthStatus>();
        if (resourceIds == null) return output;

        foreach (string resourceId in resourceIds)
        {
            var basicAuthResult = new BasicAuthStatus()
            {
                ResourceId = resourceId,
                Name = resourceId.Split('/').Last()
            };

            var basicAuthCheckUrl = new Uri(new Uri("https://management.azure.com"), $"{resourceId}/basicPublishingCredentialsPolicies?api-version=2021-02-01");
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, basicAuthCheckUrl);

            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                string responseJson = await response.Content.ReadAsStringAsync();
                var jsonObject = JObject.Parse(responseJson);

                var valueArray = jsonObject["value"] as JArray;

                if (valueArray != null)
                {
                    var ftp = valueArray.FirstOrDefault(item => (item["name"]?.ToString() ?? string.Empty).Equals("ftp"));
                    var scm = valueArray.FirstOrDefault(item => (item["name"]?.ToString() ?? string.Empty).Equals("scm"));

                    if (ftp != null)
                    {
                        bool allow = bool.Parse(ftp["properties"]?["allow"]?.ToString() ?? "false");
                        basicAuthResult.FtpBasicAuthAllowed = allow;
                        basicAuthResult.Location = ftp["location"]?.ToString();
                    }

                    if (scm != null)
                    {
                        bool allow = bool.Parse(scm["properties"]?["allow"]?.ToString() ?? "false");
                        basicAuthResult.ScmBasicAuthAllowed = allow;
                        basicAuthResult.Location = scm["location"]?.ToString();
                    }
                }

                output.Add(basicAuthResult);
            }
        }

        return output;
    }

    public async Task<bool> DisableBasicAuth(BasicAuthStatus appInViolation)
    {
        if (appInViolation == null || string.IsNullOrWhiteSpace(appInViolation.ResourceId)) return false;

        List<Task<HttpResponseMessage>> tasks = new List<Task<HttpResponseMessage>>();
        if (appInViolation.FtpBasicAuthAllowed)
        {
            var ftpUrl = new Uri(new Uri("https://management.azure.com"), $"{appInViolation.ResourceId}/basicPublishingCredentialsPolicies/ftp?api-version=2021-02-01");
            var requestBody = new
            {
                id = $"{appInViolation.ResourceId}/basicPublishingCredentialsPolicies/ftp",
                name = "ftp",
                type = "Microsoft.Web/sites/basicPublishingCredentialsPolicies",
                location = appInViolation.Location,
                properties = new
                {
                    allow = false
                }
            };
            string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, ftpUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            tasks.Add(httpClient.SendAsync(request));
        }

        if (appInViolation.ScmBasicAuthAllowed)
        {
            var scmUrl = new Uri(new Uri("https://management.azure.com"), $"{appInViolation.ResourceId}/basicPublishingCredentialsPolicies/scm?api-version=2021-02-01");
            var requestBody = new
            {
                id = $"{appInViolation.ResourceId}/basicPublishingCredentialsPolicies/scm",
                name = "scm",
                type = "Microsoft.Web/sites/basicPublishingCredentialsPolicies",
                location = appInViolation.Location,
                properties = new
                {
                    allow = false
                }
            };

            string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, scmUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            tasks.Add(httpClient.SendAsync(request));
        }

        if (tasks.Count == 0) return true;

        var result = await Task.WhenAll(tasks);
        return result.All(p => p.IsSuccessStatusCode);
    }

    public async Task<List<TimeSeriesData>> FetchMetricsAsync(string resourceId, List<Metric> metrics, string filter = "")
    {
        return await FetchMetricsAsync(resourceId, metrics, filter, CancellationToken.None);
    }

    public async Task<List<TimeSeriesData>> FetchMetricsAsync(string resourceId, List<Metric> metrics, string filter, CancellationToken cancellationToken)
    {
        var timeSeriesData = new List<TimeSeriesData>();
        if (metrics == null) return timeSeriesData;

        string metricNamesString = string.Join(",", metrics.Select(m => m.Name));
        string aggregationsString = string.Join(",", metrics.Select(m => m.Aggregation));
        string filterParam = string.IsNullOrEmpty(filter) ? string.Empty : $"&{filter}";

        var requestUri = new Uri(new Uri("https://management.azure.com"), $"{resourceId}/providers/microsoft.insights/metrics?api-version=2018-01-01&metricnames={metricNamesString}&aggregation={aggregationsString}&timespan=PT30M{filterParam}");

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

        // Send the GET request
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

        // Read the response content
        string content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            if (CheckForUnauthorizedAccess(response))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
            }
            throw new Exception($"Failed to fetch metrics: {content}");
        }

        JObject metricsJson = JObject.Parse(content);

        // Extract time series data with proper null checking
        var valueToken = metricsJson["value"];
        if (valueToken == null) return timeSeriesData;

        foreach (var metric in valueToken)
        {
            if (metric == null) continue;

            // Safe navigation for nested properties
            var nameToken = metric["name"]?["value"];
            if (nameToken == null) continue;

            string metricName = nameToken.ToString();
            var timeSeries = metric["timeseries"];

            // Find matching metric definition
            var metricDefinition = metrics.FirstOrDefault(m => m.Name == metricName);
            if (metricDefinition == null) continue;

            if (timeSeries == null || !timeSeries.Any()) continue;

            // Check if first timeseries element exists and has data
            var firstTimeSeries = timeSeries[0];
            if (firstTimeSeries == null) continue;

            var dataToken = firstTimeSeries["data"];
            if (dataToken == null) continue;

            foreach (var dataPoint in dataToken)
            {
                if (dataPoint == null) continue;

                var timestampToken = dataPoint["timeStamp"];
                if (timestampToken == null) continue;

                if (!DateTime.TryParse(timestampToken.ToString(), out DateTime timestamp))
                    continue;

                var aggregationKey = metricDefinition.Aggregation?.ToLower();
                if (string.IsNullOrEmpty(aggregationKey)) continue;

                var value = dataPoint[aggregationKey]?.Value<double>();

                timeSeriesData.Add(new TimeSeriesData
                {
                    Name = metricDefinition.Name,
                    Timestamp = timestamp,
                    Value = value ?? 0f,
                    Unit = metricDefinition.Unit
                });
            }
        }

        return timeSeriesData;
    }

    public async Task<string> GetAppServicePlanNameAsync(string appServiceResourceId)
    {
        // Construct the request URL to get the App Service details
        string requestUrl = $"https://management.azure.com/{appServiceResourceId}?api-version=2021-02-01";
        // Prepare the HTTP request
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        // Send the request
        HttpResponseMessage response = await httpClient.SendAsync(request);
        string jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            if (CheckForUnauthorizedAccess(response))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to App Service {appServiceResourceId}");
            }
            throw new Exception($"Failed to fetch App service details. Status Code : {response.StatusCode}, Error Response : {jsonResponse}");
        }

        // Deserialize the response to extract the App Service Plan name
        using JsonDocument jsonDocument = JsonDocument.Parse(jsonResponse);
        JsonElement properties = jsonDocument.RootElement.GetProperty("properties");
        string? appServicePlanId = properties.GetProperty("serverFarmId").GetString();
        if (string.IsNullOrEmpty(appServicePlanId))
        {
            throw new Exception("App Service Plan ID (serverFarmId) is missing in the response.");
        }
        return appServicePlanId;
    }

    public async Task<AppPlanSku> GetCurrentSkuAsync(string appServicePlanResourceId)
    {
        // Construct the request URL to get the App Service Plan details
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appServicePlanResourceId}?api-version=2021-02-01");

        // Prepare the HTTP request
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        // Send the request
        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            if (CheckForUnauthorizedAccess(response))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to App Service Plan {appServicePlanResourceId}");
            }
            string responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to retrieve App Service Plan details. Status Code: {response.StatusCode}, Response: {responseBody}");
        }

        // Deserialize the response to extract the SKU and instance count
        string jsonResponse = await response.Content.ReadAsStringAsync();
        using JsonDocument jsonDocument = JsonDocument.Parse(jsonResponse);
        JsonElement skuElement = jsonDocument.RootElement.GetProperty("sku");
        string sku = skuElement.GetProperty("name").GetString() ?? string.Empty;
        string location = jsonDocument.RootElement.GetProperty("location").GetString() ?? string.Empty;
        int instanceCount = jsonDocument.RootElement.GetProperty("properties").GetProperty("numberOfWorkers").GetInt32();

        return new AppPlanSku()
        {
            Location = location,
            Capacity = instanceCount,
            Family = GetFamilyFromSku(sku),
            Name = sku,
            Size = sku,
            Tier = GetTierFromSku(sku)
        };
    }

    public static AppPlanSku GetNextSku(AppPlanSku currentSku)
    {
        // Define the SKU progression
        var skuProgression = new[] { "F1", "D1", "B1", "B2", "B3", "S1", "S2", "S3", "P1", "P1v2", "P2v2", "P3v2", "P0v3", "P1v3", "P2v3", "P3v3" };

        // Find the index of the current SKU
        int currentIndex = Array.IndexOf(skuProgression, currentSku.Size);

        if (currentIndex == -1 || currentIndex == skuProgression.Length - 1)
        {
            // Current SKU not found or it's already the highest SKU
            return currentSku;
        }

        string nextSku = skuProgression[currentIndex + 1];

        // Return the next SKU
        return new AppPlanSku()
        {
            Location = currentSku.Location,
            Capacity = currentSku.Capacity,
            Family = GetFamilyFromSku(nextSku),
            Name = nextSku,
            Size = nextSku,
            Tier = GetTierFromSku(nextSku)
        };
    }

    public async Task<bool> ScaleUpAppServicePlanByNameAsync(string appServicePlanResourceId, AppPlanSku targetSku)
    {
        string appServicePlanName = await GetAppServicePlanNameAsync(appServicePlanResourceId);
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appServicePlanName}?api-version=2024-11-01");

        var requestBody = new
        {
            kind = "app",
            location = targetSku.Location,
            properties = new { },
            sku = new
            {
                name = targetSku.Name,
                tier = targetSku.Tier,
                size = targetSku.Size,
                family = targetSku.Family,
                capacity = targetSku.Capacity
            }
        };

        string jsonBody = JsonSerializer.Serialize(requestBody);
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, requestUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        var cred = await _authService.GetArmOperationCredential();
        var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

        HttpResponseMessage response = await httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            if (CheckForUnauthorizedAccess(response))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to App Service Plan {appServicePlanResourceId}");
            }

            string errorMessage = $"Failed to scale up App Service Plan. Status Code: {response.StatusCode}, Response: {responseContent}";
            _logger.LogInternalError(errorMessage);
            throw new Exception(errorMessage);
        }

        _logger.LogInternalInformation($"ScaleUpAppServicePlanByNameAsync response: {responseContent}");
        return response.IsSuccessStatusCode;
    }

    public async Task<string> ProfileAndGetCPUReport(string appServiceResource)
    {
        try
        {
            // Step 1: Get the instances for the given App Service resource
            var instances = await GetAppServiceInstanceMachineNamesAsync(appServiceResource);

            if (instances == null || instances.Length == 0)
            {
                return string.Empty;
            }

            var requestUrl = $"https://management.azure.com{appServiceResource}/extensions/daas/sessions?api-version=2015-08-01";
            var payload = new
            {
                Mode = "CollectAndAnalyze",
                Tool = "Profiler with CPU Stacks",
                Instances = new[] { instances[0] }
            };

            // Step 2: Send the request to start the DaaS session and obtain a session ID.
            var content = new StringContent(JObject.FromObject(payload).ToString(), Encoding.UTF8, "application/json");
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = content;
            using var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            httpClient.Timeout = TimeSpan.FromMinutes(10);
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = $"Failed to get DaaS SessionId for ResourceId: {appServiceResource} with error message: {await response.Content.ReadAsStringAsync()}";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }

            string sessionId = await response.Content.ReadAsStringAsync();

            // Step 3: Wait for the DaaS session to complete and retrieve the report data path.
            var result = await WaitForDaaSSessionCompletionWithRetriesAsync(appServiceResource, sessionId);
            var activeInstances = result["ActiveInstances"];
            if (activeInstances == null || !activeInstances.HasValues)
            {
                string errorMessage = $"No active instances found for ResourceId: {appServiceResource}. The App Service may not have any running instances.";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }

            var firstInstance = activeInstances[0];
            var logs = firstInstance?["Logs"];
            if (logs == null || !logs.HasValues)
            {
                string errorMessage = $"No logs found for ResourceId: {appServiceResource}. The profiling session may not have generated any logs.";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }

            var reports = logs[0]?["Reports"];
            if (reports == null || !reports.HasValues)
            {
                string errorMessage = $"No reports found for ResourceId: {appServiceResource}. The profiling session may not have generated any reports.";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }

            var firstReport = reports[0];
            if (firstReport?["PartialPath"] == null)
            {
                string errorMessage = $"Failed to get CPU analysis for ResourceId: {appServiceResource}. PartialPath is missing from the report.";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }

            if (firstInstance?["Name"] == null)
            {
                string errorMessage = $"Failed to get CPU analysis for ResourceId: {appServiceResource}. Instance name is missing.";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }

            string partialPath = firstReport["PartialPath"]?.ToString() ?? "";
            string instance = firstInstance["Name"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(partialPath) || string.IsNullOrEmpty(instance))
            {
                string errorMessage = $"Failed to get CPU analysis for ResourceId: {appServiceResource}. PartialPath or Instance is null or empty.";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }

            // Step 4: Get the path to the raw data.
            partialPath = Path.Combine("C:\\home\\", partialPath);
            string? parentFolder = Path.GetDirectoryName(partialPath);
            string reportDataPath = Path.Combine(parentFolder ?? string.Empty, instance, "reportdata");
            reportDataPath = reportDataPath.Replace("\\", "/");

            string hostName = await GetKuduHostNameAsync(appServiceResource);
            string reportRequestUrl = $"https://{hostName}/api/vfs/{reportDataPath}/";
            HttpRequestMessage reportRequest = new HttpRequestMessage(HttpMethod.Get, reportRequestUrl);
            var cred = await _authService.GetArmOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://api.applicationinsights.io/.default" }), CancellationToken.None);
            reportRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            var reportResponse = await httpClient.SendAsync(reportRequest);

            if (!reportResponse.IsSuccessStatusCode)
            {
                string errorMessage = $"Failed to retrieve report data from: {reportRequestUrl} with status code: {reportResponse.StatusCode} and error message: {await reportResponse.Content.ReadAsStringAsync()} for appServiceResource: {appServiceResource} and sessionId: {sessionId}";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }

            // Step 5: Get the CPU stack file from the report data.
            string reportContent = await reportResponse.Content.ReadAsStringAsync();
            JArray reportFiles = JArray.Parse(reportContent);
            var cpuStackFile = reportFiles.FirstOrDefault(file =>
            {
                var nameToken = file?["name"];
                var name = nameToken?.ToString();
                return !string.IsNullOrEmpty(name)
                    && name.Contains("cpuStacks", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("Jmc", StringComparison.OrdinalIgnoreCase);
            });

            if (cpuStackFile == null)
            {
                string errorMessage = $"No CPU stack file found in the report data for appServiceResource: {appServiceResource} and sessionId: {sessionId} - this is because the overall CPU utilization < 2%";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }
            // Step 6: Get the contents of the CPU Stack file.
            string urlOfCpuStack = cpuStackFile["href"]?.ToString() ?? throw new InvalidOperationException("CPU stack file 'href' property is missing.");
            HttpRequestMessage cpuStackRequest = new HttpRequestMessage(HttpMethod.Get, urlOfCpuStack);
            cred = await _authService.GetArmOperationCredential();
            token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://api.applicationinsights.io/.default" }), CancellationToken.None);
            cpuStackRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            var cpuStackDataResponse = await httpClient.SendAsync(cpuStackRequest);
            if (!cpuStackDataResponse.IsSuccessStatusCode)
            {
                string errorMessage = $"Failed to get CPU Stack Data for appServiceResource: {appServiceResource} and sessionId: {sessionId}";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage);
            }

            string cpuStackDataContent = await cpuStackDataResponse.Content.ReadAsStringAsync();
            return cpuStackDataContent;
        }

        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            string errorMessage = CreateTimeoutErrorMessage("HTTP timeout", appServiceResource, ex.Message);
            _logger.LogInternalError(errorMessage);
            throw new Exception(errorMessage, ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            string errorMessage = CreateTimeoutErrorMessage("HTTP request timeout", appServiceResource, ex.Message);
            _logger.LogInternalError(errorMessage);
            throw new Exception(errorMessage, ex);
        }
        catch (Exception e)
        {
            string errorMessage = $"Failed to Get CPU Analysis for: {appServiceResource} with exception: {e.Message}";
            _logger.LogInternalError(errorMessage);
            throw;
        }
    }

    public async Task<string> TakeMemoryDumpAsync(string appServiceResource)
    {
        try
        {
            // Get the instances for the given App Service resource
            var instances = await GetAppServiceInstanceMachineNamesAsync(appServiceResource);

            if (instances == null || instances.Length == 0)
            {
                return string.Empty;
            }

            var requestUrl = $"https://management.azure.com{appServiceResource}/extensions/daas/sessions?api-version=2015-08-01";
            var payload = new
            {
                Mode = "Collect",
                Tool = "MemoryDump",
                Instances = instances
            };

            var content = new StringContent(JObject.FromObject(payload).ToString(), Encoding.UTF8, "application/json");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = content;

            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return String.Empty;
            }

            string sessionId = await response.Content.ReadAsStringAsync();
            return await WaitForDaaSSessionCompletionAsync(appServiceResource, sessionId);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public async Task<HttpResponseMessage> RestartWebAppAsync(string appResourceId)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appResourceId}/restart?api-version=2024-04-01");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        HttpResponseMessage response = await httpClient.SendAsync(request);
        return response;
    }

    public async Task<bool> RestartContainerAppAsync(string appResourceId, string revisionName)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appResourceId}/revisions/{revisionName}/restart?api-version=2025-01-01");
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        HttpResponseMessage response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    // Use the generic method for all specific cases:
    public async Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchTlsStatusAsync);
    }

    public async Task<List<StorageAccountLocalAuthSettings>> GetStorageSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchStorageAccountStatusAsync);
    }

    public async Task<List<CosmosDbLocalAuthStatus>> GetCosmosDbSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchCosmosDbStatusAsync);
    }

    public async Task<List<EventHubLocalAuthStatus>> GetEventHubSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchEventHubStatusAsync);
    }

    public async Task<List<ServiceBusLocalAuthStatus>> GetServiceBusSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchServiceBusStatusAsync);
    }

    public async Task<List<SqlServerLocalAuthStatus>> GetAzureSqlServerSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchSqlServerStatusAsync);
    }

    public async Task<List<AppServiceLocalAuthStatus>> GetAppServiceSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchAppServiceStatusAsync);
    }

    public async Task<List<KubernetesLocalAuthStatus>> GetKubernetesSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchKubernetesStatusAsync);
    }

    /// <summary>
    /// Checks if a given string is a valid resource identifier.
    /// </summary>
    /// <param name="resourceId">The string to be validated as a resource identifier.</param>
    /// <returns>Returns true if the string is a valid resource identifier, otherwise false.</returns>
    /// <remarks>Note that this doesn't check for existence - just structure/shape</remarks>
    public bool IsWellFormattedResourceId(string resourceId)
    {
        return ResourceIdentifier.TryParse(resourceId, out var resourceIdentifier);
    }

    public async Task<bool> CheckIfResourceExistsAsync(string resourceId)
    {
        try
        {
            var armClient = await _armClientFactory.GetArmOperationClient();
            var resource = await armClient.GetGenericResource(new ResourceIdentifier(resourceId)).GetAsync();
            return resource != null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Resource not found
            return false;
        }
    }

    public async Task<(bool, string)> UpdateMinimumTlsVersion(TlsStatus tlsStatus, string desiredTlsVersion)
    {
        if (tlsStatus == null || string.IsNullOrWhiteSpace(tlsStatus.ResourceId))
            throw new ArgumentException("Resource ID is required");

        var tlsUpdateUrl = new Uri(new Uri("https://management.azure.com"), $"{tlsStatus.ResourceId}/config/web?api-version=2022-03-01");

        var requestBody = new
        {
            id = $"{tlsStatus.ResourceId}/config/web",
            name = "web",
            type = "Microsoft.Web/sites/config",
            location = tlsStatus.Location,
            properties = new
            {
                minTlsVersion = desiredTlsVersion
            }
        };

        string jsonBody = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, tlsUpdateUrl);
        request.Content = content;

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        var response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            return (true, string.Empty);
        }
        else
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            return (false, $"Http status code: {response.StatusCode}, body: {responseBody}");
        }
    }

    public async Task<StorageAccountResource> GetStorageAccountAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var storageAccount = armClient.GetStorageAccountResource(new ResourceIdentifier(resourceId));
        return await storageAccount.GetAsync();
    }

    public async Task<CosmosDBAccountResource> GetCosmosDbAccountAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var cosmosDBAccountResource = armClient.GetCosmosDBAccountResource(new ResourceIdentifier(resourceId));
        return await cosmosDBAccountResource.GetAsync();
    }

    public async Task<EventHubsNamespaceResource> GetEventHubAccountAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var eventHubsNamespaceResource = armClient.GetEventHubsNamespaceResource(new ResourceIdentifier(resourceId));
        return await eventHubsNamespaceResource.GetAsync();
    }

    public async Task<ServiceBusNamespaceResource> GetServiceBusAccountAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var serviceBusNamespaceResource = armClient.GetServiceBusNamespaceResource(new ResourceIdentifier(resourceId));
        return await serviceBusNamespaceResource.GetAsync();
    }

    public async Task<SqlServerResource> GetSqlServerAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var sqlServerResource = armClient.GetSqlServerResource(new ResourceIdentifier(resourceId));
        return await sqlServerResource.GetAsync();
    }

    public async Task<WebSiteResource> GetAppServiceAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var webSiteResource = armClient.GetWebSiteResource(new ResourceIdentifier(resourceId));
        return await webSiteResource.GetAsync();
    }

    public async Task<ContainerServiceManagedClusterResource> GetKubernetesClusterAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var kubernetesClusterResource = armClient.GetContainerServiceManagedClusterResource(new ResourceIdentifier(resourceId));
        return await kubernetesClusterResource.GetAsync();
    }

    public async Task SetStorageAccountSharedKeySupportAsync(string resourceId, FeatureState featureState)
    {
        var storageAccountResource = await GetStorageAccountAsync(resourceId);
        var storageAccountPatch = new StorageAccountPatch()
        {
            AllowSharedKeyAccess = featureState == FeatureState.Enabled ? true : false
        };
        await storageAccountResource.UpdateAsync(storageAccountPatch);

        // re-crawl for WRITE operations
        _crawlerTriggerService.TriggerArmCrawl(resourceId);
    }

    public async Task SetStorageAccountContainerPublicAccess(string resourceId, FeatureState featureState)
    {
        var storageAccountResource = await GetStorageAccountAsync(resourceId);
        var storageAccountPatch = new StorageAccountPatch()
        {
            AllowBlobPublicAccess = featureState == FeatureState.Enabled ? true : false
        };
        await storageAccountResource.UpdateAsync(storageAccountPatch);

        // re-crawl for WRITE operations
        _crawlerTriggerService.TriggerArmCrawl(resourceId);
    }

    public async Task SetSqlServerEntraAuthSupport(string resourceId, FeatureState featureState)
    {
        var sqlServer = await GetSqlServerAsync(resourceId);
        var sqlServerAdOnlyAuthResult = await sqlServer.GetSqlServerAzureADOnlyAuthenticationAsync(AuthenticationName.Default);
        sqlServerAdOnlyAuthResult.Value.Data.IsAzureADOnlyAuthenticationEnabled = (featureState == FeatureState.Enabled);
        await sqlServerAdOnlyAuthResult.Value.UpdateAsync(WaitUntil.Completed, sqlServerAdOnlyAuthResult.Value.Data);

        // re-crawl for WRITE operations
        _crawlerTriggerService.TriggerArmCrawl(resourceId);
    }

    /// <summary>
    /// Gets the detector response for a resource with specified start time, enforcing a maximum time range of 3 days.
    /// The end time is always set to current time minus 15 minutes.
    /// </summary>
    /// <param name="resourceId">The Azure resource ID for which to get detector data</param>
    /// <param name="detectorId">The ID of the detector to query</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago if not specified)</param>
    /// <param name="endTime">Optional end time parameter (ignored - always uses current time minus 15 minutes)</param>
    /// <returns>The detector response as a JSON string</returns>
    /// <exception cref="ArgumentException">Thrown when the time range exceeds 3 days</exception>
    public async Task<string> GetDetectorResponseWithTime(string resourceId, string detectorId, DateTime? startTime = null, DateTime? endTime = null)
    {
        startTime ??= DateTime.UtcNow.AddHours(-2);
        endTime = DateTime.UtcNow.AddMinutes(-15);

        if (startTime > endTime)
        {
            throw new ArgumentException("Start time must be before end time");
        }

        TimeSpan maxDuration = TimeSpan.FromDays(3);
        TimeSpan actualDuration = endTime.Value - startTime.Value;

        if (actualDuration > maxDuration)
        {
            throw new ArgumentException($"Time range cannot exceed 3 days. Requested: {actualDuration.TotalDays:F1} days");
        }

        string formattedStartTime = startTime.Value.ToString("yyyy-MM-dd HH:mm");
        string formattedEndTime = endTime.Value.ToString("yyyy-MM-dd HH:mm");

        var requestUrl = new Uri(new Uri("https://management.azure.com"),
            $"{resourceId}/detectors/{detectorId}?startTime={Uri.EscapeDataString(formattedStartTime)}&endTime={Uri.EscapeDataString(formattedEndTime)}&api-version=2015-08-01");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to retrieve detector details. Status Code: {response.StatusCode}, Response: {responseBody}");
        }

        string jsonResponse = await response.Content.ReadAsStringAsync();
        return jsonResponse;
    }

    /// <summary>
    /// Gets the analysis response for a resource with specified start time, enforcing a maximum time range of 3 days.
    /// The end time is always set to current time minus 15 minutes.
    /// </summary>
    /// <param name="resourceId">The Azure resource ID for which to get analysis data</param>
    /// <param name="detectorId">The ID of the analysis to query</param>
    /// <param name="startTime">Optional start time for the query (defaults to 1 hour ago if not specified)</param>
    /// <param name="endTime">Optional end time parameter (ignored - always uses current time minus 15 minutes)</param>
    /// <returns>The analysis response as a JSON string</returns>
    /// <exception cref="ArgumentException">Thrown when the time range exceeds 3 days</exception>
    public async Task<string> GetAnalysisWithTime(string resourceId, string detectorId, DateTime? startTime = null, DateTime? endTime = null)
    {
        startTime ??= DateTime.UtcNow.AddHours(-1);
        endTime = DateTime.UtcNow.AddMinutes(-15);

        if (startTime > endTime)
        {
            throw new ArgumentException("Start time must be before end time");
        }

        TimeSpan maxDuration = TimeSpan.FromDays(3);
        TimeSpan actualDuration = endTime.Value - startTime.Value;

        if (actualDuration > maxDuration)
        {
            throw new ArgumentException($"Time range cannot exceed 3 days. Requested: {actualDuration.TotalDays:F1} days");
        }

        // First get the analysis response
        string analysisResponse = await GetDetectorResponseWithTime(resourceId, detectorId, startTime, endTime);

        try
        {
            // Parse the JSON response to extract detector IDs
            using JsonDocument document = JsonDocument.Parse(analysisResponse);
            var root = document.RootElement;

            // Create a list to store all detector responses
            List<string> allDetectorResponses = new List<string> { analysisResponse };

            // Check if properties exists in the response
            if (root.TryGetProperty("properties", out JsonElement properties))
            {
                // Check if dataset exists in properties
                if (properties.TryGetProperty("dataset", out JsonElement dataset) &&
                    dataset.ValueKind == JsonValueKind.Array)
                {
                    // Iterate through each item in the dataset array
                    foreach (JsonElement datasetItem in dataset.EnumerateArray())
                    {
                        // Look for renderingProperties which contains detectorIds
                        if (datasetItem.TryGetProperty("renderingProperties", out JsonElement renderingProps))
                        {
                            if (renderingProps.TryGetProperty("detectorIds", out JsonElement detectorIdsElement) &&
                                detectorIdsElement.ValueKind == JsonValueKind.Array)
                            {
                                // Extract each detector ID and make a call to GetDetectorResponseWithTime
                                foreach (JsonElement detectorIdElement in detectorIdsElement.EnumerateArray())
                                {
                                    string? subDetectorId = detectorIdElement.GetString();
                                    if (!string.IsNullOrEmpty(subDetectorId))
                                    {
                                        try
                                        {
                                            // Call GetDetectorResponseWithTime for this detector ID
                                            string detectorResponse = await GetDetectorResponseWithTime(resourceId, subDetectorId, startTime, endTime);
                                            allDetectorResponses.Add(detectorResponse);
                                        }
                                        catch (Exception ex)
                                        {
                                            // Log the error but continue with other detector IDs
                                            Console.WriteLine($"Failed to get detector response for {subDetectorId}: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Deserialize each response and add to a list
            var combinedResponses = new List<JsonElement>();
            foreach (var response in allDetectorResponses)
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(response);
                    combinedResponses.Add(doc.RootElement.Clone());
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Failed to parse detector response: {ex.Message}. Skipping this response.");
                }
            }

            // Serialize the list into a JSON array
            return JsonSerializer.Serialize(combinedResponses);
        }
        catch (JsonException ex)
        {
            // If JSON parsing fails, just return the original response
            return $"Failed to parse detector response: {ex.Message}. Original response: {analysisResponse}";
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to process analysis response: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sends a POST request to synchronize a Function App's host.
    /// This can be used to detect host runtime errors.
    /// </summary>
    /// <param name="resourceId">The Azure resource ID of the Function App</param>
    /// <returns>The JSON response as a string. If the host has runtime errors, this will contain error details.</returns>
    public async Task<string> SyncFunctionAppHost(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required");

        var requestUrl = new Uri(new Uri("https://management.azure.com"),
            $"{resourceId}/host/default/sync?api-version=2022-03-01");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        HttpResponseMessage response = await httpClient.SendAsync(request);

        // Always return the content, even for error status codes
        // This is important because we're looking for specific error messages
        string jsonResponse = await response.Content.ReadAsStringAsync();
        return jsonResponse;
    }

    public async Task<bool> UpdateAutoHeal(string resourceId, bool autoHealEnabled, AutoHealRules autoHealRules)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required");

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        string armUrl = $"https://management.azure.com{resourceId}/config/web?api-version=2024-04-01";

        var requestBody = new
        {
            properties = new
            {
                autoHealEnabled = autoHealEnabled,
                autoHealRules = autoHealRules
            }
        };

        string jsonBody = JsonSerializer.Serialize(requestBody);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, armUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            // re-crawl for WRITE operations
            _crawlerTriggerService.TriggerArmCrawl(resourceId);
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateNumberOfWorkersAppService(string resourceId, int numberOfWorkers)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required");

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        string armUrl = $"https://management.azure.com{resourceId}/config/web?api-version=2024-04-01";

        var requestBody = new
        {
            properties = new
            {
                numberOfWorkers = numberOfWorkers
            }
        };

        string jsonBody = JsonSerializer.Serialize(requestBody);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, armUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            // re-crawl for WRITE operations
            _crawlerTriggerService.TriggerArmCrawl(resourceId);
        }

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAlwaysOn(string resourceId, bool alwaysOn)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required");

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        string armUrl = $"https://management.azure.com{resourceId}/config/web?api-version=2024-04-01";

        var requestBody = new
        {
            properties = new
            {
                alwaysOn = alwaysOn
            }
        };

        string jsonBody = JsonSerializer.Serialize(requestBody);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, armUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            // re-crawl for WRITE operations
            _crawlerTriggerService.TriggerArmCrawl(resourceId);
        }

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateHealthcheck(string resourceId, string healthCheckPath)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required");

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        string armUrl = $"https://management.azure.com{resourceId}/config/web?api-version=2024-04-01";

        var requestBody = new
        {
            properties = new
            {
                healthCheckPath = healthCheckPath
            }
        };

        string jsonBody = JsonSerializer.Serialize(requestBody);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, armUrl)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            // re-crawl for WRITE operations
            _crawlerTriggerService.TriggerArmCrawl(resourceId);
        }

        return response.IsSuccessStatusCode;
    }

    public async Task<Models.StorageAccountLocalAuthSettings> FetchStorageAccountStatusAsync(string resourceId)
    {
        var storageAccount = await GetStorageAccountAsync(resourceId);
        return new Models.StorageAccountLocalAuthSettings(
            ResourceId: resourceId,
            Name: storageAccount.Data.Name,
            Location: storageAccount.Data.Location,
            StorageKeyEnabled: storageAccount.Data.AllowSharedKeyAccess ?? false,
            PublicContainersEnabled: storageAccount.Data.AllowBlobPublicAccess ?? false
            );
    }

    public async Task<CosmosDbLocalAuthStatus> FetchCosmosDbStatusAsync(string resourceId)
    {
        var cosmosDBAccountResource = await GetCosmosDbAccountAsync(resourceId);
        return new CosmosDbLocalAuthStatus(
            ResourceId: resourceId,
            Name: cosmosDBAccountResource.Data.Name,
            Location: cosmosDBAccountResource.Data.Location,
            IsLocalAuthEnabled: cosmosDBAccountResource.Data.DisableLocalAuth ?? false
            );
    }

    public async Task<EventHubLocalAuthStatus> FetchEventHubStatusAsync(string resourceId)
    {
        var eventHubsNamespaceResource = await GetEventHubAccountAsync(resourceId);
        return new EventHubLocalAuthStatus(
            ResourceId: resourceId,
            Name: eventHubsNamespaceResource.Data.Name,
            Location: eventHubsNamespaceResource.Data.Location,
            IsLocalAuthDisabled: eventHubsNamespaceResource.Data.DisableLocalAuth ?? false
            );
    }

    public async Task<ServiceBusLocalAuthStatus> FetchServiceBusStatusAsync(string resourceId)
    {
        var serviceBusNamespaceResource = await GetServiceBusAccountAsync(resourceId);
        return new ServiceBusLocalAuthStatus(
            ResourceId: resourceId,
            Name: serviceBusNamespaceResource.Data.Name,
            Location: serviceBusNamespaceResource.Data.Location,
            IsLocalAuthDisabled: serviceBusNamespaceResource.Data.DisableLocalAuth ?? false
            );
    }

    public async Task<SqlServerLocalAuthStatus> FetchSqlServerStatusAsync(string resourceId)
    {
        var sqlServerResource = await GetSqlServerAsync(resourceId);

        return new SqlServerLocalAuthStatus(
            ResourceId: resourceId,
            Name: sqlServerResource.Data.Name,
            Location: sqlServerResource.Data.Location,
            IsAzureADOnlyAuthenticationEnabled: sqlServerResource.Data.Administrators?.IsAzureADOnlyAuthenticationEnabled ?? false,
            IsEntraAdminSet: sqlServerResource.Data.Administrators?.AdministratorType == SqlAdministratorType.ActiveDirectory
            );
    }

    public async Task<AppServiceLocalAuthStatus> FetchAppServiceStatusAsync(string resourceId)
    {
        var webSiteResource = await GetAppServiceAsync(resourceId);
        var scmPublishingCredentialsPolicy = await webSiteResource.GetScmSiteBasicPublishingCredentialsPolicy().GetAsync();
        var ftpPublishingCredentialsPolicy = await webSiteResource.GetWebSiteFtpPublishingCredentialsPolicy().GetAsync();

        return new AppServiceLocalAuthStatus(
            ResourceId: resourceId,
            Name: webSiteResource.Data.Name,
            Location: webSiteResource.Data.Location,
            FTPBasicAuthEnabled: ftpPublishingCredentialsPolicy.Value.Data.Allow ?? true,
            SCMBasicAuthEnabled: scmPublishingCredentialsPolicy.Value.Data.Allow ?? true
            );
    }

    public async Task<KubernetesLocalAuthStatus> FetchKubernetesStatusAsync(string resourceId)
    {
        var kubernetesClusterResource = await GetKubernetesClusterAsync(resourceId);
        return new KubernetesLocalAuthStatus(
            ResourceId: resourceId,
            Name: kubernetesClusterResource.Data.Name,
            Location: kubernetesClusterResource.Data.Location,
            DisableLocalAccounts: kubernetesClusterResource.Data.DisableLocalAccounts ?? false
        );
    }

    public async Task SetWebSiteFtpAuthenticationSupport(string resourceId, FeatureState featureState)
    {
        var webSiteResource = await GetAppServiceAsync(resourceId);
        var ftpPublishingCredentialsPolicy = await webSiteResource.GetWebSiteFtpPublishingCredentialsPolicy().GetAsync();
        ftpPublishingCredentialsPolicy.Value.Data.Allow = (featureState == FeatureState.Enabled);
        await ftpPublishingCredentialsPolicy.Value.CreateOrUpdateAsync(WaitUntil.Completed, ftpPublishingCredentialsPolicy.Value.Data);

        // re-crawl for WRITE operations
        _crawlerTriggerService.TriggerArmCrawl(resourceId);
    }

    public async Task SetWebSiteScmAuthenticationSupport(string resourceId, FeatureState featureState)
    {

        var webSiteResource = await GetAppServiceAsync(resourceId);
        var scmPublishingCredentialsPolicy = await webSiteResource.GetScmSiteBasicPublishingCredentialsPolicy().GetAsync();
        scmPublishingCredentialsPolicy.Value.Data.Allow = (featureState == FeatureState.Enabled);
        await scmPublishingCredentialsPolicy.Value.CreateOrUpdateAsync(WaitUntil.Completed, scmPublishingCredentialsPolicy.Value.Data);

        // re-crawl for WRITE operations
        _crawlerTriggerService.TriggerArmCrawl(resourceId);
    }

    public async Task SetCosmosDbLocalAuthSupport(string resourceId, FeatureState featureState)
    {
        var cosmosDBAccountResource = await GetCosmosDbAccountAsync(resourceId);
        var cosmosDbPatch = new CosmosDBAccountPatch();
        bool updateCosmosDb = false;

        if (featureState == FeatureState.Disabled && cosmosDBAccountResource.Data.DisableLocalAuth != false)
        {
            cosmosDbPatch.DisableLocalAuth = false;
            updateCosmosDb = true;
        }
        else if (featureState == FeatureState.Enabled && cosmosDBAccountResource.Data.DisableLocalAuth != true)
        {
            cosmosDbPatch.DisableLocalAuth = true;
            updateCosmosDb = true;
        }

        if (updateCosmosDb)
        {
            await cosmosDBAccountResource.UpdateAsync(WaitUntil.Completed, cosmosDbPatch);
            // re-crawl for WRITE operations
            _crawlerTriggerService.TriggerArmCrawl(resourceId);
        }
    }

    public async Task SetEventHubLocalAuthSupport(string resourceId, FeatureState featureState)
    {
        var eventHubResource = await GetEventHubAccountAsync(resourceId);

        eventHubResource.Data.DisableLocalAuth = (featureState == FeatureState.Disabled);
        await eventHubResource.UpdateAsync(eventHubResource.Data);

        // re-crawl for WRITE operations
        _crawlerTriggerService.TriggerArmCrawl(resourceId);
    }

    public async Task SetServiceBusLocalAuthSupport(string resourceId, FeatureState featureState)
    {
        var serviceBusNamespaceResource = await GetServiceBusAccountAsync(resourceId);
        var serviceBusNamespacePatch = new ServiceBusNamespacePatch(serviceBusNamespaceResource.Data.Location);

        serviceBusNamespacePatch.DisableLocalAuth = (featureState == FeatureState.Disabled);

        await serviceBusNamespaceResource.UpdateAsync(serviceBusNamespacePatch);

        // re-crawl for WRITE operations
        _crawlerTriggerService.TriggerArmCrawl(resourceId);
    }

    public async Task<VirtualMachineResource> GetVirtualMachineResourceAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var virtualMachineResource = armClient.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        if (virtualMachineResource == null)
        {
            throw new ArgumentException($"Resource with ID {resourceId} is not a valid Virtual Machine resource.");
        }
        var virtualMachineResourceResponse = await virtualMachineResource.GetAsync();
        return virtualMachineResourceResponse.Value;
    }

    public async Task<string> GetArmResourceAsJsonAsync(string resourceId)
    {
        // Validate resource ID format before attempting to use it
        if (!IsWellFormattedResourceId(resourceId))
        {
            return $"{{\"error\":{{\"code\":\"InvalidResourceId\",\"message\":\"The provided resource ID '{resourceId}' is not in the correct format. Azure resource IDs should start with /subscriptions/ and follow the pattern /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/{{resourceProviderNamespace}}/{{resourceType}}/{{resourceName}}\"}}}}";
        }

        try
        {
            var armClient = await _armClientFactory.GetArmOperationClient();
            var resource = armClient.GetGenericResource(new ResourceIdentifier(resourceId));
            var resourceDataResponse = await resource.GetAsync();

            var resourceData = resourceDataResponse.Value;
            var properties = JsonSerializer.Deserialize<object>(resourceData.Data.Properties.ToString());

            var identity = resourceData.Data.Identity;
            List<GenericArmResourceIdentityModel> managedIdentities = [];
            if (identity != null)
            {
                if (identity.PrincipalId != null)
                {
                    managedIdentities.Add(new GenericArmResourceIdentityModel(IdentityType.SystemAssignedManagedIdentity.ToString(), identity.PrincipalId.Value));
                }

                if (identity.UserAssignedIdentities != null)
                {
                    managedIdentities.AddRange(identity.UserAssignedIdentities.Values
                        .Where(userAssignedIdentity => userAssignedIdentity.PrincipalId != null)
                        .Select(userAssignedIdentity => new GenericArmResourceIdentityModel(IdentityType.UserAssignedManagedIdentity.ToString(), userAssignedIdentity.PrincipalId ?? Guid.Empty)));
                }
            }

            GenericArmResourceModel armRes = new GenericArmResourceModel(
                id: resourceId,
                name: resourceData.Data.Name,
                type: resourceData.Data.ResourceType,
                kind: resourceData.Data.Kind ?? string.Empty,
                location: resourceData.Data.Location,
                properties: properties ?? new object(),
                tags: resourceData.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()) ?? new Dictionary<string, string>(),
                IdentityModels: managedIdentities
            );

            // Return the formatted JSON
            return JsonSerializer.Serialize(armRes, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            return $"{{\"error\":{{\"code\":\"InternalError\",\"message\":\"An error occurred while retrieving the resource: {ex.Message}\"}}}}";

        }
    }

    public async Task<bool> PowerOnVirtualMachineAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var virtualMachineResource = armClient.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        if (virtualMachineResource == null)
        {
            throw new ArgumentException($"Resource with ID {resourceId} is not a valid Virtual Machine resource.");
        }
        var startOperation = await virtualMachineResource.PowerOnAsync(WaitUntil.Completed);
        if (startOperation.HasCompleted)
        {
            // re-crawl for WRITE operations
            _crawlerTriggerService.TriggerArmCrawl(resourceId);
        }
        return startOperation.HasCompleted;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnosticsAsync(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        var virtualMachineResource = armClient.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        if (virtualMachineResource == null)
        {
            throw new ArgumentException($"Resource with ID {resourceId} is not a valid Virtual Machine resource.");
        }

        var bootDiagnosticsDataResult = await virtualMachineResource.RetrieveBootDiagnosticsDataAsync(10);

        var bootDiagnosticLogs = new Dictionary<string, string>();

        if (bootDiagnosticsDataResult.Value.ConsoleScreenshotBlobUri != null || bootDiagnosticsDataResult.Value.SerialConsoleLogBlobUri != null)
        {
            var httpClient = _httpClientFactory.CreateClient(nameof(GetVirtualMachineBootDiagnosticsAsync));

            // Intentionally not summarizing console screenshot image it is bmp and causes token limit to exceed.
            // The correct way is to convert it to png/jpeg and then summarize it.
            // Returning only the SerialConsoleLog text content for now.

            if (bootDiagnosticsDataResult.Value.SerialConsoleLogBlobUri != null)
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, bootDiagnosticsDataResult.Value.SerialConsoleLogBlobUri);
                var serialCosoleLogResponse = await httpClient.SendAsync(request);
                if (serialCosoleLogResponse.IsSuccessStatusCode)
                {
                    // Read the log response
                    var logContent = await serialCosoleLogResponse.Content.ReadAsStringAsync();
                    bootDiagnosticLogs.Add("SerialConsoleLog", logContent);
                }
            }
        }

        return bootDiagnosticLogs;
    }

    public async Task<string> GetAppSettings(string resourceId)
    {
        var requestUrl = $"https://management.azure.com{resourceId}/config/appSettings/list?api-version=2022-03-01";
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        HttpResponseMessage responseMessage = await httpClient.SendAsync(request);

        if (!responseMessage.IsSuccessStatusCode)
        {
            _logger.LogInternalWarning($"Failed to fetch app settings for {resourceId}: {responseMessage.ReasonPhrase}");
            if (CheckForUnauthorizedAccess(responseMessage))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
            }

            throw new Exception($"Failed to retrieve app settings for resource {resourceId}");
        }

        var appSettings = await responseMessage.Content.ReadAsStringAsync();

        return appSettings;
    }

    public async Task<string> GetAppInsightsAppIdBySubscription(string subscriptionId, string instrumentationKey)
    {

        try
        {
            var requestUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/microsoft.insights/components?api-version=2018-05-01-preview";
            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpResponseMessage responseMessage = await httpClient.SendAsync(request);

            if (responseMessage.IsSuccessStatusCode)
            {
                var content = await responseMessage.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                foreach (var component in root.GetProperty("value").EnumerateArray())
                {
                    if (component.TryGetProperty("properties", out var properties) &&
                        properties.TryGetProperty("InstrumentationKey", out var key) &&
                        key.GetString() == instrumentationKey)
                    {
                        var appIdFound = properties.TryGetProperty("AppId", out var appId);
                        return appIdFound ? appId.GetString()! : string.Empty;
                    }
                }

                return string.Empty; // Return empty if no match found
            }
            else
            {
                if (CheckForUnauthorizedAccess(responseMessage))
                {
                    throw new ToolExecutionUnauthorizedException($"Unauthorized access to subscription {subscriptionId}");
                }

                // Handle unsuccessful response
                var errorContent = await responseMessage.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to get App Insights resource ID. Response: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An error occurred while getting the App Insights resource ID.", ex);
        }
    }

    public async Task<GenericArmResourceModel?> GetAppInsightsResourceByInstrumentationKeyAsync(string subscriptionId, string instrumentationKey)
    {
        var requestUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/microsoft.insights/components?api-version=2018-05-01-preview";
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);

        foreach (var component in jsonDoc.RootElement.GetProperty("value").EnumerateArray())
        {
            if (component.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty("InstrumentationKey", out var key) &&
                key.GetString() == instrumentationKey)
            {
                var resourceId = component.GetProperty("id").GetString();
                var resourceName = component.GetProperty("name").GetString();
                var location = component.GetProperty("location").GetString();
                var kind = component.GetProperty("kind").GetString();

                if (string.IsNullOrEmpty(resourceId) || string.IsNullOrEmpty(resourceName) || string.IsNullOrEmpty(location) || string.IsNullOrEmpty(kind))
                {
                    _logger.LogInternalError("App Insights resource lookup failed: one or more required fields (id, name, location, or kind) are missing or empty.");
                    return null;
                }
                
                var type = component.TryGetProperty("type", out var typ) ? typ.GetString() : null;
                var tags = component.TryGetProperty("tags", out var tagElement) && tagElement.ValueKind == JsonValueKind.Object
                    ? tagElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString())
                    : new Dictionary<string, string>();

                return new GenericArmResourceModel(
                    id: resourceId,
                    name: resourceName,
                    type: type ?? "microsoft.insights/components",
                    kind: kind ?? string.Empty,
                    location: location,
                    properties: properties,
                    tags: tags,
                    IdentityModels: new List<GenericArmResourceIdentityModel>()
                );
            }
        }
        return null;
    }

    public async Task<string> ExecuteLogAnalyticsQuery(string resourceId, string queryString, string timeSpan)
    {
        try
        {
            var requestUrl = $"https://management.azure.com{resourceId}/providers/microsoft.insights/diagnosticSettings?api-version=2021-05-01-preview";
            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpResponseMessage responseMessage = await httpClient.SendAsync(request);

            if (!responseMessage.IsSuccessStatusCode)
            {
                if (CheckForUnauthorizedAccess(responseMessage))
                {
                    throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
                }

                // Handle unsuccessful response
                var errorContent = await responseMessage.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to retrieve diagnostic settings. Response: {errorContent}");
            }
            var content = await responseMessage.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            foreach (var component in root.GetProperty("value").EnumerateArray())
            {
                if (component.TryGetProperty("properties", out var properties) &&
                    properties.TryGetProperty("workspaceId", out var workSpaceId) &&
                    properties.TryGetProperty("logs", out var logsArray))
                {
                    foreach (var logsEntry in logsArray.EnumerateArray())
                    {
                        if (logsEntry.TryGetProperty("category", out var categoryElement))
                        {
                            var category = categoryElement.GetString();

                            if (category == "AppServiceHTTPLogs" ||
                                category == "AppServiceConsoleLogs" ||
                                category == "AppServicePlatformLogs" ||
                                category == "GatewayLogs")
                            {
                                var endpoint = "https://api.loganalytics.io/v1" + workSpaceId.GetString()! + "/query?timespan=" + timeSpan;

                                return await ExecuteAppInsightsQueryInternal(endpoint, queryString);
                            }
                        }
                    }
                }
            }

            return string.Empty; // Return empty if no match found
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An error occurred while querying Log Analytics.", ex);
        }
    }

    public async Task<string> ExecuteAppInsightsQuery(string appInsightsAppId, string queryString)
    {
        try
        {
            var endpoint = "https://api.applicationinsights.io/v1/apps/" + appInsightsAppId + "/query";

            return await ExecuteAppInsightsQueryInternal(endpoint, queryString);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An error occurred while querying Application Insights.", ex);
        }
    }

    private async Task<string> ExecuteAppInsightsQueryInternal(string url, string queryString)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var cred = await _authService.GetArmOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://api.applicationinsights.io/.default" }), CancellationToken.None);

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            // Send the query
            var response = await httpClient.PostAsJsonAsync(url, new { query = queryString });

            // Read and display the result
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                _logger.LogInternalWarning($"Failed to query App Insights");
                if (CheckForUnauthorizedAccess(response))
                {
                    throw new ToolExecutionUnauthorizedException($"Unauthorized access to App Insights resource {url}");
                }

                var message = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"FAILED! Querying {url} Failed: Status {response.StatusCode}, Message: {message}");
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<string>> GetHostNamesOfAppServices(string resourceId)
    {
        try
        {
            var armClient = await _armClientFactory.GetArmOperationClient();
            ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
            WebSiteResource webApp = await armClient.GetWebSiteResource(resourceIdentifier).GetAsync();
            var appData = webApp.Data;
            var hostNames = appData.EnabledHostNames.Where(h => !h.Contains(".scm."));
            return hostNames.ToList();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> SwapAppServiceSlotsAsync(string resourceId, bool preserveVNetValue, string sourceSlotName, string targetSlotName)
    {
        try
        {
            // Construct the request URL for swapping slots
            string requestUrl = $"https://management.azure.com{resourceId}/slots/{sourceSlotName}/slotsswap?api-version=2022-03-01";

            // Prepare the request body
            var requestBody = new
            {
                targetSlot = targetSlotName,
                preserveVNet = preserveVNetValue
            };

            // Serialize the request body to JSON
            string jsonBody = JsonSerializer.Serialize(requestBody);

            // Create the HTTP request
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            // Create and send the HTTP request
            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            HttpResponseMessage response = await httpClient.SendAsync(request);

            // Check the response status code
            if (response.IsSuccessStatusCode)
            {
                // If the swap is successful, trigger a re-crawl for WRITE operations
                _crawlerTriggerService.TriggerArmCrawl(resourceId);

                return true; // Swap was successful
            }
            else
            {
                if (CheckForUnauthorizedAccess(response))
                {
                    throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
                }
                string responseBody = await response.Content.ReadAsStringAsync();
                return false; // Swap failed
            }
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred during the swap operation", ex);
        }
    }

    public async Task<(List<OperationDetail> Deployments, List<OperationDetail> Swaps)> GetDeploymentActivity(string subId, string rg, string resourceId, string? st = null, string? et = null)
    {
        try
        {
            // Set default values for start time and end time if not provided
            if (string.IsNullOrEmpty(st))
            {
                st = DateTime.UtcNow.AddHours(-3).ToString("yyyy-MM-ddTHH:mm:ssZ"); // 3 hours ago
            }

            if (string.IsNullOrEmpty(et))
            {
                et = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"); // Current time
            }

            string filter = $"$filter=eventTimestamp ge '{st}' and eventTimestamp le '{et}' and eventChannels eq 'Admin, Operation' and resourceGroupName eq '{rg}' and resourceId eq '{resourceId}' and levels eq 'Informational'";
            string requestUrl = $"https://management.azure.com/subscriptions/{subId}/providers/microsoft.insights/eventtypes/management/values?api-version=2017-03-01-preview&{filter}";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            HttpResponseMessage response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                if (CheckForUnauthorizedAccess(response))
                {

                    throw new ToolExecutionUnauthorizedException($"Unauthorized access to subscription {subId}");
                }

                throw new Exception($"Failed to retrieve deployment activity: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
            }

            var content = await response.Content.ReadAsStringAsync();

            // Parse the response
            JObject jsonResponse = JObject.Parse(content);
            var events = jsonResponse["value"]?.Children<JObject>() ?? throw new Exception("No events found in the response.");

            // Extract deployment and swap details
            var deployments = ExtractOperationDetails(events, "deploy");
            var swaps = ExtractOperationDetails(events, "slotsSwap");

            return (deployments, swaps);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred during the deployment activity retrieval", ex);
        }
    }

    private List<OperationDetail> ExtractOperationDetails(IEnumerable<JObject> events, string operationFilter)
    {
        var operationDetails = new List<OperationDetail>();

        foreach (var evt in events)
        {
            var operationName = evt["operationName"]?["value"]?.ToString();
            if (operationName?.Contains(operationFilter) == true)
            {
                var status = evt["properties"]?["statusCode"]?.ToString() ?? string.Empty;
                var isSuccessful = status.Contains("Accepted", StringComparison.OrdinalIgnoreCase);

                var detail = new OperationDetail
                {
                    OperationName = operationName ?? string.Empty,
                    Status = status,
                    Timestamp = DateTime.TryParse(evt["eventTimestamp"]?.ToString(), out var timestamp) ? (DateTime?)timestamp : null,
                    ResourceId = evt["resourceId"]?.ToString() ?? string.Empty,
                    Caller = evt["caller"]?.ToString() ?? string.Empty,
                    ErrorMessage = isSuccessful ? null : evt["properties"]?["statusCode"]?.ToString(),
                    IsSuccessful = isSuccessful
                };

                operationDetails.Add(detail);
            }
        }

        return operationDetails;
    }

    public async Task<List<OperationDetail>> GetCriticalErrorActivityLogs(string subId, string rg, string resourceId, string? st = null, string? et = null)
    {
        try
        {
            // Set default values for start time and end time if not provided
            if (string.IsNullOrEmpty(st))
            {
                st = DateTime.UtcNow.AddHours(-3).ToString("yyyy-MM-ddTHH:mm:ssZ"); // 3 hours ago
            }

            if (string.IsNullOrEmpty(et))
            {
                et = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"); // Current time
            }

            // Filter for critical, error, and warning levels
            string filter = $"$filter=eventTimestamp ge '{st}' and eventTimestamp le '{et}' and eventChannels eq 'Admin, Operation' and resourceGroupName eq '{rg}' and resourceId eq '{resourceId}' and levels eq 'Critical,Error,Warning'";
            string requestUrl = $"https://management.azure.com/subscriptions/{subId}/providers/microsoft.insights/eventtypes/management/values?api-version=2017-03-01-preview&{filter}";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            HttpResponseMessage response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retrieve activity logs: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
            }

            var content = await response.Content.ReadAsStringAsync();

            // Parse the response
            JObject jsonResponse = JObject.Parse(content);
            var events = jsonResponse["value"]?.Children<JObject>();

            // Extract error details
            var errorDetails = new List<OperationDetail>();

            if (events != null)
            {
                foreach (var evt in events)
                {
                    var operationName = evt["operationName"]?["value"]?.ToString();
                    var status = evt["properties"]?["statusCode"]?.ToString() ?? string.Empty;
                    var message = evt["properties"]?["message"]?.ToString() ?? string.Empty;
                    var statusMessage = evt["properties"]?["statusMessage"]?.ToString();
                    var isSuccessful = status.Contains("Succeeded", StringComparison.OrdinalIgnoreCase);

                    var detail = new OperationDetail
                    {
                        OperationName = operationName ?? string.Empty,
                        Status = status,
                        Timestamp = DateTime.TryParse(evt["eventTimestamp"]?.ToString(), out var timestamp) ? (DateTime?)timestamp : null,
                        ResourceId = evt["resourceId"]?.ToString() ?? string.Empty,
                        Caller = evt["caller"]?.ToString() ?? string.Empty,
                        ErrorMessage = message,
                        StatusMessage = statusMessage,
                        IsSuccessful = isSuccessful
                    };

                    errorDetails.Add(detail);
                }
            }

            return errorDetails;
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred during the activity logs retrieval", ex);
        }
    }

    public async Task<string> CheckConnectivityToAzureWebJobsStorage(string resourceId, string providerType = "BlobStorage")
    {
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

        httpClient.BaseAddress = new Uri("https://management.azure.com");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, resourceId + "/connectivityCheck?api-version=2022-03-01");

        string payload = $@"{{
            ""properties"": {{
                ""ProviderType"": ""{providerType}"",
                ""Credentials"": {{
                    ""CredentialType"": ""CredentialReference"",
                    ""CredentialReference"": {{
                        ""ReferenceType"": ""AppSetting"",
                        ""ReferenceName"": ""AzureWebJobsStorage""
                    }}
                }},
                ""ResourceMetadata"": {{}}
            }}
        }}";
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var res = await httpClient.SendAsync(request);
        if (res.IsSuccessStatusCode)
        {
            var connectivityCheckResult = await res.Content.ReadAsStringAsync();
            return connectivityCheckResult;
        }
        else
        {
            if (CheckForUnauthorizedAccess(res))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
            }

            throw new Exception($"Connectivity check failed: {res.Content}");
        }
    }

    public async Task<string> CheckTcpConnectivityAsync(string resourceId, string host, int port)
    {
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

        httpClient.BaseAddress = new Uri("https://management.azure.com");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, resourceId + "/tcpPingCheck?api-version=2022-03-01");

        if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            host = host.Substring("https://".Length);
        }

        string payload = $@"{{
            ""properties"": {{
                ""Host"": ""{host}"",
                ""Port"": ""{port}""
            }}
        }}";
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var res = await httpClient.SendAsync(request);
        if (res.IsSuccessStatusCode)
        {
            var tcpConnectivityCheckResult = await res.Content.ReadAsStringAsync();
            return tcpConnectivityCheckResult;
        }
        else
        {
            if (CheckForUnauthorizedAccess(res))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
            }

            throw new Exception($"TCP ping check failed: {res.Content}");
        }
    }

    public async Task<IReadOnlyCollection<ArmWrapper<ArmRevisionReplica>>> GetRevisionReplicas(string revisionId)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{revisionId}/replicas?api-version=2024-03-01");
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get instances: {response.ReasonPhrase}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var replicas = JsonSerializer.Deserialize<ArmListWrapper<ArmRevisionReplica>>(content);
        return replicas?.Value ?? [];
    }

    public async Task<IReadOnlyCollection<ArmWrapper<ArmRevisionReplica>>> GetRevisionInstances(string containerAppId, string revisionName)
    {
        var revisionId = $"{containerAppId}/revisions/{revisionName}";
        return await GetRevisionReplicas(revisionId);
    }

    public async Task<string> CheckDnsResolution(string resourceId, string destinationUrl)
    {
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

        httpClient.BaseAddress = new Uri("https://management.azure.com");

        if (destinationUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            destinationUrl = destinationUrl.Substring("https://".Length);
        }

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, resourceId + "/dnsCheck?api-version=2022-03-01");

        string payload = $@"{{
           ""properties"": {{
               ""dnsName"": ""{destinationUrl}""
           }}
       }}";
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var res = await httpClient.SendAsync(request);
        if (res.IsSuccessStatusCode)
        {
            var dnsResolutionCheckResult = await res.Content.ReadAsStringAsync();
            return dnsResolutionCheckResult;
        }
        else
        {
            if (CheckForUnauthorizedAccess(res))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
            }

            throw new Exception($"Dns Resolution check failed: {res.Content}");
        }
    }

    public async Task<IDictionary<string, string>> GetAppSetting(string resourceId, string appsettingKey)
    {
        var appSettingKv = new Dictionary<string, string>();
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        httpClient.BaseAddress = new Uri("https://management.azure.com");

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, resourceId + "/config/appsettings/list?api-version=2024-04-01");
        using var res = await httpClient.SendAsync(request);

        if (res.IsSuccessStatusCode)
        {
            string responseJson = await res.Content.ReadAsStringAsync();

            // Parse JSON with null checking
            var jsonObject = JObject.Parse(responseJson);
            var propertiesToken = jsonObject["properties"];

            if (propertiesToken != null)
            {
                var value = propertiesToken[appsettingKey];
                if (value != null)
                {
                    appSettingKv[appsettingKey] = value.ToString();
                }
            }

            return appSettingKv;
        }
        else
        {
            if (CheckForUnauthorizedAccess(res))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
            }

            // Read the error content safely
            string errorContent = string.Empty;
            try
            {
                errorContent = await res.Content.ReadAsStringAsync();
            }
            catch
            {
                errorContent = "Unable to read error response content";
            }

            throw new Exception($"Failed to retrieve app setting {appsettingKey} for resource {resourceId}: {errorContent}");
        }
    }

    public async Task<bool> UpdateAppSettingsAsync(string resourceId, IDictionary<string, string> appSettings)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || appSettings == null || appSettings.Count == 0)
            throw new ArgumentException("Resource ID and app settings are required");

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        httpClient.BaseAddress = new Uri("https://management.azure.com");

        // Fetch existing app settings
        var existingAppSettingsResponse = await httpClient.PostAsync(resourceId + "/config/appsettings/list?api-version=2024-04-01", null);
        if (!existingAppSettingsResponse.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch existing app settings. Status Code: {existingAppSettingsResponse.StatusCode}");

        var existingAppSettingsJson = await existingAppSettingsResponse.Content.ReadAsStringAsync();
        var existingAppSettings = JsonSerializer.Deserialize<Dictionary<string, string>>(JObject.Parse(existingAppSettingsJson)["properties"]?.ToString() ?? "{}") ?? [];

        // Merge new app settings with existing ones
        foreach (var kvp in appSettings)
        {
            if (existingAppSettings.ContainsKey(kvp.Key))
            {
                var archivedKey = $"Archived{kvp.Key}";
                existingAppSettings[archivedKey] = existingAppSettings[kvp.Key];
            }

            existingAppSettings[kvp.Key] = kvp.Value;
        }

        // Prepare the request body
        var requestBody = new
        {
            properties = existingAppSettings
        };
        string jsonBody = JsonSerializer.Serialize(requestBody);

        // Send the update request
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, resourceId + "/config/appsettings?api-version=2024-04-01")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            // Trigger a re-crawl for WRITE operations
            _crawlerTriggerService.TriggerArmCrawl(resourceId);
        }
        else
        {
            if (CheckForUnauthorizedAccess(response))
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
            }

            throw new Exception($"Updating app settings failed: {response.Content}");
        }

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ListKeysAndUpdateAppSettingsAsync(string storageResourceId, string appServiceResourceId, string appSettingKey)
    {
        if (string.IsNullOrWhiteSpace(storageResourceId))
            throw new ArgumentException("Storage Resource ID is required");
        if (string.IsNullOrWhiteSpace(appServiceResourceId))
            throw new ArgumentException("App Service Resource ID is required");
        if (string.IsNullOrWhiteSpace(appSettingKey))
            throw new ArgumentException("App Setting Key is required");

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        string requestUrl = $"https://management.azure.com{storageResourceId}/listKeys?api-version=2023-05-01";

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        using HttpResponseMessage response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = string.Empty;
            try
            {
                errorContent = await response.Content.ReadAsStringAsync();
            }
            catch
            {
                errorContent = "Unable to read error response content";
            }

            throw new Exception($"Failed to retrieve keys. Status Code: {response.StatusCode}, Response: {errorContent}");
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        using var jsonResponse = JsonDocument.Parse(responseContent);

        // Get the storage account name from the resource ID
        var resourceIdParts = storageResourceId.Split('/');
        if (resourceIdParts.Length == 0)
        {
            throw new Exception("Invalid storage resource ID format");
        }
        string storageAccountName = resourceIdParts.Last();

        // Get the first key with proper null checking
        if (!jsonResponse.RootElement.TryGetProperty("keys", out JsonElement keysElement))
        {
            throw new Exception("No 'keys' property found in response");
        }

        var keysArray = keysElement.EnumerateArray();
        var firstKey = keysArray.FirstOrDefault();

        if (firstKey.ValueKind == JsonValueKind.Undefined)
        {
            throw new Exception("No storage keys found in response");
        }

        if (!firstKey.TryGetProperty("value", out JsonElement valueElement))
        {
            throw new Exception("No 'value' property found in first key");
        }

        string? key = valueElement.GetString();
        if (string.IsNullOrEmpty(key))
        {
            throw new Exception("No valid storage key found");
        }

        // Construct the connection string
        string connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={key};EndpointSuffix=core.windows.net";

        // Update the app setting with the connection string
        var appSettings = new Dictionary<string, string>
        {
            { appSettingKey, connectionString }
        };

        return await UpdateAppSettingsAsync(appServiceResourceId, appSettings);
    }

    public async Task<bool> ConfigureAppSettingsForManagedIdentityStorage(string resourceId, string storageAccountName)
    {
        var appSettings = new Dictionary<string, string>
        {
            { "AzureWebJobsStorage__accountName", storageAccountName },
            { "AzureWebJobsStorage__blobServiceUri", $"https://{storageAccountName}.blob.core.windows.net" },
            { "AzureWebJobsStorage__queueServiceUri", $"https://{storageAccountName}.queue.core.windows.net" },
            { "AzureWebJobsStorage__tableServiceUri", $"https://{storageAccountName}.table.core.windows.net" }
        };

        return await UpdateAppSettingsAsync(resourceId, appSettings);
    }

    public static int GetNumberOfCoresFromSku(string sku)
    {
        // Map SKU to tier, e.g., "P1v2" -> "PremiumV2"
        // You can add more mappings as needed
        return sku switch
        {
            "F1" => 1,
            "D1" => 1,
            "B1" => 1,
            "B2" => 2,
            "B3" => 4,
            "S1" => 1,
            "S2" => 2,
            "S3" => 4,
            "P1v2" => 1,
            "P2v2" => 2,
            "P3v2" => 4,
            "P0v3" => 1,
            "P1v3" => 2,
            "P2v3" => 4,
            "P3v3" => 8,
            "P4mv3" => 16,
            "P5mv3" => 32,
            "I1v2" => 2,
            "I2v2" => 4,
            "I3v2" => 8,
            "I4v2" => 16,
            "I5v2" => 32,
            "I6v2" => 64,
            _ => throw new ArgumentException("SKU is invalid", nameof(sku))
        };
    }

    public async Task<int> GetNumberOfWorkers(string resourceId)
    {
        var armClient = await _armClientFactory.GetArmOperationClient();
        ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
        WebSiteResource webApp = await armClient.GetWebSiteResource(resourceIdentifier).GetAsync();
        var numWorkers = webApp.Data.SiteConfig.NumberOfWorkers ?? 1;
        return numWorkers;
    }

    public async Task<WebSiteResource> GetWebSiteResourceAsync(string resourceId)
    {
        try
        {
            // Get ResourceIdentifier from the provided resourceId
            ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
            var armClient = await _armClientFactory.GetArmOperationClient();
            WebSiteResource webApp = await armClient.GetWebSiteResource(resourceIdentifier).GetAsync();
            return webApp;
        }

        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve Web App details. {ex}", ex);
        }
    }

    public async Task<string> GetKuduHostNameAsync(string resourceId)
    {
        try
        {
            // Retrieve Web App using the provided resourceId
            var site = await GetWebSiteResourceAsync(resourceId);
            // Kudu host URL (this will be used for profiling purposes)
            return site.Data.EnabledHostNames.First(h => h.Contains(".scm."));
        }

        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to retrieve Kudu host information.", ex);
        }
    }

    public async Task<string> GetOperatingSystemAsync(string resourceId)
    {
        try
        {
            // Retrieve the Web App and determine its OS
            var site = await GetWebSiteResourceAsync(resourceId);
            return site.Data.Kind.Contains("linux", StringComparison.OrdinalIgnoreCase) ? "Linux" : "Windows";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to determine the operating system type.", ex);
        }
    }

    public async Task<int> GetDefaultProcessIdForWebAppAsync(string resourceId, string os, string hostName)
    {
        string url = $"https://{hostName}/api/processes";
        using HttpClient httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        if (os == "Linux")
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var processes = JsonSerializer.Deserialize<JsonArray>(jsonResponse);

            if (processes is null)
                throw new InvalidOperationException("No processes returned.");

            foreach (var processElement in processes)
            {
                if (processElement is JsonObject processObj)
                {
                    bool isDefault = false;
                    int processId = 0;

                    if (processObj.TryGetPropertyValue("isDefault", out var isDefaultNode)
                        && isDefaultNode is JsonValue isDefaultValue
                        && isDefaultValue.TryGetValue<bool>(out var defaultBool)
                        && defaultBool)
                    {
                        isDefault = true;
                    }

                    if (processObj.TryGetPropertyValue("pid", out var idNode)
                        && idNode is JsonValue idValue
                        && idValue.TryGetValue<int>(out var pid))
                    {
                        processId = pid;
                    }

                    if (isDefault)
                        return processId;
                }
            }

            throw new InvalidOperationException("Default process not found.");
        }

        else if (os == "Windows")
        {
            var processesUrl = $"https://{hostName}/api/processes";

            var response = await httpClient.GetAsync(processesUrl);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var processes = JsonSerializer.Deserialize<JsonArray>(jsonResponse);

            if (processes is null)
                throw new InvalidOperationException("No processes returned.");

            foreach (var processElement in processes.Where(p =>
                p is JsonObject obj &&
                obj.TryGetPropertyValue("name", out var nameNode) &&
                nameNode is JsonValue nameValue &&
                nameValue.TryGetValue<string>(out var name) &&
                string.Equals(name, "w3wp", StringComparison.OrdinalIgnoreCase)))
            {
                if (processElement is JsonObject processObj)
                {
                    int processId = -1;

                    if (processObj.TryGetPropertyValue("id", out var idNode)
                        && idNode is JsonValue idValue
                        && idValue.TryGetValue<int>(out var pid))
                    {
                        processId = pid;
                        var processUrl = $"https://{hostName}/api/processes/{pid}";
                        var processResponse = await httpClient.GetAsync(processUrl);
                        processResponse.EnsureSuccessStatusCode();
                        var processInfo = JsonSerializer.Deserialize<JsonObject>(await processResponse.Content.ReadAsStringAsync());
                        if (processInfo is not null && processInfo.TryGetPropertyValue("name", out var node))
                        {
                            if (!processInfo.TryGetPropertyValue("is_scm_site", out var _))
                            {
                                return processId;
                            }
                        }
                    }
                }
            }

            throw new InvalidOperationException("Default process not found.");
        }

        else
        {
            throw new InvalidOperationException("Unsupported OS type.");
        }
    }

    public async Task<JObject> WaitForDaaSSessionCompletionWithRetriesAsync(string appServiceResource, string sessionId)
    {
        int retryCount = 0;
        sessionId = sessionId.Replace("\"", "");
        var requestUrl = $"https://management.azure.com/{appServiceResource}/extensions/daas/sessions/{sessionId}?api-version=2015-08-01";

        var cred = await _authService.GetArmOperationCredential();
        var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), default);
        using var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        httpClient.Timeout = TimeSpan.FromMinutes(5); // Set a longer timeout for session completion check

        while (true)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            try
            {
                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get session details: {response.ReasonPhrase}");
                }

                var content = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(content);
                var status = json.ContainsKey("Status") ? json["Status"]?.ToString() : null;

                if (status == "Complete")
                {
                    return json;
                }

                else
                {
                    ++retryCount;
                    if (retryCount >= 30) // 5 minutes max
                    {
                        throw new Exception($"DaaS Session did not complete within the expected time for: {appServiceResource} for SessionId: {sessionId}.");
                    }

                    // Delay for 10 seconds before checking again
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                string errorMessage = $"HTTP timeout occurred while checking DaaS session status for: {appServiceResource} and sessionId: {sessionId}. This timeout is likely due to insufficient computational resources on the App Service. Consider scaling up the App Service Plan to a higher SKU tier (e.g., Standard, Premium, or Premium v2/v3) to provide more CPU and memory resources for the profiling operation. Exception: {ex.Message}";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage, ex);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                string errorMessage = $"HTTP request timeout occurred while checking DaaS session status for: {appServiceResource} and sessionId: {sessionId}. This timeout is likely due to insufficient computational resources on the App Service. Consider scaling up the App Service Plan to a higher SKU tier (e.g., Standard, Premium, or Premium v2/v3) to provide more CPU and memory resources for the profiling operation. Exception: {ex.Message}";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage, ex);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error checking session status for ResourceId: {appServiceResource} and sessionId: {sessionId} - {ex.Message}";
                _logger.LogInternalError(errorMessage);
                throw;
            }
        }
    }

    public async Task<bool> UploadFileToKudu(string hostName, string filePath, string workingDirectory)
    {
        string? zipOutputPath = null;
        
        try
        {
            // Precondition Checks.
            if (string.IsNullOrWhiteSpace(hostName))
                throw new ArgumentException("Host name cannot be null or empty.", nameof(hostName));
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new ArgumentException("File path cannot be null or does not exist.", nameof(filePath));
            if (string.IsNullOrWhiteSpace(workingDirectory))
                throw new ArgumentException("Working directory cannot be null or empty.", nameof(workingDirectory));

            // Create temporary zip file path
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            zipOutputPath = Path.Combine(Path.GetTempPath(), $"{fileName}_{Guid.NewGuid():N}.zip");

            // Get authentication token
            var cred = await _authService.GetArmOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), default);

            using HttpClient httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            // Create the zip file containing the specified file
            using (var zipStream = new FileStream(zipOutputPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
            }

            // Read the zip file bytes
            var zipBytes = await File.ReadAllBytesAsync(zipOutputPath);

            // Normalize working directory path for Kudu
            var normalizedWorkingDirectory = workingDirectory.Replace('\\', '/').TrimStart('/');
            var kuduZipUrl = $"https://{hostName}/api/zip/{normalizedWorkingDirectory}/";

            // Upload the zip file to Kudu
            using (var zipContent = new ByteArrayContent(zipBytes))
            using (var request = new HttpRequestMessage(HttpMethod.Put, kuduZipUrl))
            {
                zipContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                request.Content = zipContent;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

                var uploadResponse = await httpClient.SendAsync(request);
                if (!uploadResponse.IsSuccessStatusCode)
                {
                    var errorContent = await uploadResponse.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to upload zip file. Status: {uploadResponse.StatusCode}, Response: {errorContent}");
                }
            }

            _logger.LogInternalInformation($"[UploadFileToKudu] File uploaded successfully to {normalizedWorkingDirectory}");
            return true; 
        }
        catch (HttpRequestException ex)
        {
            _logger.LogInternalError($"[UploadFileToKudu] HTTP request failed during file upload: {ex.Message}");
            throw new InvalidOperationException("Failed to upload file to Kudu.", ex);
        }

        catch (Exception ex)
        {
            _logger.LogInternalError($"[UploadFileToKudu] Unexpected error during file upload: {ex.Message}");
            throw;
        }
        finally
        {
            // Clean up: Delete the temporary zip file
            if (!string.IsNullOrEmpty(zipOutputPath) && File.Exists(zipOutputPath))
            {
                try
                {
                    File.Delete(zipOutputPath);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"[UploadFileToKudu] Failed to delete temporary zip file {zipOutputPath}: {ex.Message}");
                }
            }
        }
    }

    public async Task<string> ExecuteKuduCommandAsync(string hostName, string command, string workingDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be null or empty.", nameof(command));

            using HttpClient httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            httpClient.Timeout = TimeSpan.FromMinutes(5); // Set a longer timeout for command execution
            var url = $"https://{hostName}/api/command";
            var commandDetails = "{\"command\": \"" + command + "\", \"dir\": \"" + workingDirectory + "\"}";
            var commandPayload = new StringContent(commandDetails, Encoding.UTF8, "application/json");
            var postResponse = await httpClient.PostAsync(url, commandPayload);
            postResponse.EnsureSuccessStatusCode();

            var output = await postResponse.Content.ReadAsStringAsync();
            Console.WriteLine("[KuduManager] Command Output:\n" + output);
            return output;
        }

        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"[KuduManager] HTTP request failed during command execution: {ex.Message} for command: {command} in {workingDirectory}");
            throw new InvalidOperationException("Failed to execute command on Kudu.", ex);
        }

        catch (Exception ex)
        {
            Console.Error.WriteLine($"[KuduManager] Unexpected error during command execution: {ex.Message}");
            throw;
        }
    }

    private record GetAuthTokenResponseProperties([property: JsonPropertyName("token")] string Token);
    private record GetAuthTokenResponse([property: JsonPropertyName("properties")] GetAuthTokenResponseProperties Properties);
    public async Task<string> GetProxyApiTokenAsync(string subscriptionId, string resourceGroup, string appName)
    {
        try
        {
            // Add the token to the HttpClient's Authorization header
            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

            var uriBuilder = new UriBuilder($"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.App/containerApps/{appName}/getAuthToken");
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Add("api-version", "2024-02-02-preview");
            uriBuilder.Query = query.ToString();

            var response = await httpClient.PostAsync(uriBuilder.Uri, null);
            response.EnsureSuccessStatusCode();
            var resp = await response.Content.ReadFromJsonAsync<GetAuthTokenResponse>();
            if (resp == null || resp.Properties == null || string.IsNullOrEmpty(resp.Properties.Token))
            {
                throw new Exception("Failed to Proxy get API token");
            }

            return resp.Properties.Token;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<CliExecutionResult> RunAzCliCommandsAsync(string command)
    {
        _logger.LogInternalInformation($"[RunAzCliCommandsAsync] command: {command}");
        // Trim any leading/trailing whitespace
        command = command.Trim();

        // Validate command format
        var validationSummary = ValidateCommand(command);
        if (validationSummary != null)
        {
            _logger.LogInternalError($"[RunAzCliCommandsAsync] Validation failed: {validationSummary}");
            return new CliExecutionResult
            {
                ErrorType = CliErrorType.ValidationError,
                Output = validationSummary,
            };
        }

        // Execute the command
        try
        {
            var cred = await _authService.GetArmOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext([Constants.DefaultOboTokenScope]), default);
            var cliExecution = new AzCliExecution(_logger, command, accessToken: token.Token, isDevelopment: _hostEnvironment.IsDevelopment());
            var result = await cliExecution.ExecuteAsync();

            if (IsWriteCommand(command))
            {
                _crawlerTriggerService.TriggerArmCrawl(result);
            }

            return new CliExecutionResult
            {
                ErrorType = CliErrorType.None,
                Output = result,
            };
        }
        catch (Exception ex)
        {
            var executionResult = await CliExecutionHelper.ParseCliExecutionResult(_chatClient, ex.Message);
            return executionResult;
        }
    }

    public async Task<string> GetResourceByURL(string requestUrl)
    {
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        HttpResponseMessage responseMessage = await httpClient.SendAsync(request);

        var result = await responseMessage.Content.ReadAsStringAsync();
        return result;
    }

    /// <summary>
    /// Gets Event Grid subscriptions for a specified resource.
    /// </summary>
    /// <param name="resourceId">The resource ID to get Event Grid subscriptions for (e.g., a storage account resource ID)</param>
    /// <param name="apiVersion">The API version to use, defaults to 2024-12-15-preview</param>
    /// <param name="top">The maximum number of subscriptions to return, defaults to 20</param>
    /// <returns>The JSON response containing Event Grid subscriptions</returns>
    public async Task<string> GetEventGridSubscriptionsAsync(string resourceId, string apiVersion = "2024-12-15-preview", int top = 20)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required", nameof(resourceId));

        // Validate resource ID format
        if (!IsWellFormattedResourceId(resourceId))
            throw new ArgumentException($"Invalid resource ID format: {resourceId}", nameof(resourceId));

        // Check if the resource exists before attempting to get subscriptions
        bool resourceExists = await CheckIfResourceExistsAsync(resourceId);
        if (!resourceExists)
        {
            _logger.LogInternalWarning($"Resource not found when retrieving Event Grid subscriptions: {resourceId}");
            return $"{{\"error\":{{\"code\":\"ResourceNotFound\",\"message\":\"The Resource was not found. Please verify the resource exists before retrieving Event Grid subscriptions.\"}}}}";
        }

        // Construct the URL for Event Grid subscriptions
        // Format: {resourceId}/providers/Microsoft.EventGrid/eventSubscriptions?api-version={apiVersion}&$top={top}
        string requestUrl = $"https://management.azure.com{resourceId}/providers/Microsoft.EventGrid/eventSubscriptions?api-version={apiVersion}&$top={top}";

        try
        {
            // Use the existing GetResourceByURL method to make the API call
            string result = await GetResourceByURL(requestUrl);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Failed to get Event Grid subscriptions for resource {resourceId}: {ex.Message}");
            throw;
        }
    }

    #region Parsing Methods

    public static string? TryParseFirstSubdomainFromHttpsUrl(string? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value.StartsWith("https://"))
        {
            return value.Substring("https://".Length).Split('.').FirstOrDefault();
        }

        return null;
    }

    public static string? TryParseSynapseWorkspaceFromEndpoint(string? value)
    {
        if (value == null)
        {
            return null;
        }

        // value is an endpoint. It can be either <workspaceName>-ondemand.sql.azuresynapse.net or  <workspaceName>.sql.azuresynapse.net or any sql endpoint
        return value.Split('.').FirstOrDefault()?.Split('-').FirstOrDefault();
    }

    public static string? TryParseStorageAccountFromNameOrEndpoint(string? value)
    {
        if (value == null)
        {
            return null;
        }

        // value is an endpoint. It could be either https://<name>.<type>.core.windows.net/ or https://<name>-secondary.<type>.core.windows.net/
        if (value.StartsWith("https://"))
        {
            return value!.Substring("https://".Length).Split('.').FirstOrDefault()?.Split('-').FirstOrDefault();
        }
        else
        {
            // value is the storage account name
            return value;
        }
    }

    #endregion

    #region Private Methods

    private static string CreateTimeoutErrorMessage(string timeoutType, string appServiceResource, string exceptionMessage)
    {
        return $"{timeoutType} occurred while getting CPU analysis for: {appServiceResource}. This timeout is likely due to insufficient computational resources on the App Service. Consider scaling up the App Service Plan to a higher SKU tier (e.g., Standard, Premium, or Premium v2/v3) to provide more CPU and memory resources for the profiling operation. Exception: {exceptionMessage}";
    }

    private async Task<List<T>> GetResourceSettings<T>(
    List<string> resourceIds,
    Func<string, Task<T>> fetchStatusFunc)
    where T : class
    {
        var output = new List<T>();
        if (resourceIds == null || resourceIds.Count == 0) return output;

        const int batchSize = 5;
        for (int i = 0; i < resourceIds.Count; i += batchSize)
        {
            // Take a slice of up to 5 resource IDs
            var chunk = resourceIds.Skip(i).Take(batchSize).ToList();

            // Create tasks for each resource in this chunk
            var tasks = chunk.Select(rid => fetchStatusFunc(rid)).ToList();

            // Run them in parallel
            var results = await Task.WhenAll(tasks);

            // Add them to the output (filter out any null if the call failed)
            output.AddRange(results.Where(r => r != null));
        }

        return output;
    }

    private async Task<TlsStatus> FetchTlsStatusAsync(string resourceId)
    {
        var tlsCheckUrl = new Uri(new Uri("https://management.azure.com"), $"{resourceId}/config/web?api-version=2022-03-01");
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, tlsCheckUrl);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInternalWarning($"Failed to fetch TLS status for {resourceId}: {response.ReasonPhrase}");
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceId}");
            }

            throw new ToolExecutionException($"Failed to fetch TLS status for {resourceId}: {response.ReasonPhrase}");
        }

        string responseJson = await response.Content.ReadAsStringAsync();
        var jsonObject = JObject.Parse(responseJson);

        var properties = jsonObject["properties"];
        var minimumTlsVersion = properties != null
            ? properties["minTlsVersion"]?.ToString()
            : null;

        // Ensure location is never null (use empty string as fallback)
        var location = jsonObject["location"]?.ToString() ?? string.Empty;

        var tlsStatus = new TlsStatus(
            ResourceId: resourceId,
            Name: resourceId.Split('/').Last(),
            Location: location,
            MinimumTlsVersion: minimumTlsVersion);

        return tlsStatus;
    }

    private static string GetFamilyFromSku(string sku)
    {
        // Map SKU to family, e.g., "P1v2" -> "Pv2"
        // You can add more mappings as needed
        return sku switch
        {
            "F1" => "F",
            "D1" => "D",
            "B1" => "B",
            "B2" => "B",
            "B3" => "B",
            "S1" => "S",
            "S2" => "S",
            "S3" => "S",
            "P1" => "Pv2",
            "P1v2" => "Pv2",
            "P2v2" => "Pv2",
            "P3v2" => "Pv2",
            "P0v3" => "Pv3",
            "P1v3" => "Pv3",
            "P2v3" => "Pv3",
            "P3v3" => "Pv3",
            _ => throw new ArgumentException("Unknown SKU")
        };
    }

    private static string GetTierFromSku(string sku)
    {
        // Map SKU to tier, e.g., "P1v2" -> "PremiumV2"
        // You can add more mappings as needed
        return sku switch
        {
            "F1" => "Free",
            "D1" => "Shared",
            "B1" => "Basic",
            "B2" => "Basic",
            "B3" => "Basic",
            "S1" => "Standard",
            "S2" => "Standard",
            "S3" => "Standard",
            "P1" => "PremiumV2",
            "P1v2" => "PremiumV2",
            "P2v2" => "PremiumV2",
            "P3v2" => "PremiumV2",
            "P0v3" => "PremiumV3",
            "P1v3" => "PremiumV3",
            "P2v3" => "PremiumV3",
            "P3v3" => "PremiumV3",
            _ => throw new ArgumentException("Unknown SKU")
        };
    }

    private async Task<string[]> GetAppServiceInstanceMachineNamesAsync(string appServiceResource)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appServiceResource}/instances?api-version=2021-02-01");

        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get instances: {response.ReasonPhrase}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);
        var machineNames = json["value"]?.Select(instance => instance["properties"]?["machineName"]?.ToString()).ToArray();
        if (machineNames == null)
        {
            throw new InvalidDataException("No machine names found in the response.");
        }

        return [.. machineNames.Where(m => !string.IsNullOrEmpty(m)).Select(m => m!)];
    }

    private async Task<string> WaitForDaaSSessionCompletionAsync(string appServiceResource, string sessionId)
    {
        sessionId = sessionId.Replace("\"", "");
        var requestUrl = $"https://management.azure.com/{appServiceResource}/extensions/daas/sessions/{sessionId}?api-version=2015-08-01";

        while (true)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get session details: {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var status = json["Status"]?.ToString();

            if (status == "Complete")
            {
                var relativePath = json["ActiveInstances"]?[0]?["Logs"]?[0]?["RelativePath"]?.ToString();
                return relativePath ?? string.Empty;
            }

            // Delay for 10 seconds before checking again
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private string? ValidateCommand(string command)
    {
        // Check if the command starts with "az"
        if (!command.StartsWith("az ", StringComparison.OrdinalIgnoreCase))
        {
            return "[Validation Failed]: Command must start with 'az'.";
        }

        // try determine the verb by finding the last command before the parameters
        var commandParts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var verb = string.Empty;
        foreach (var cmd in commandParts)
        {
            if (cmd.StartsWith("-") || cmd.StartsWith("--"))
            {
                break;
            }
            verb = cmd;
        }

        // Define flags that are allowed to contain dangerous characters in their quoted values
        var whitelistedFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--analytics-query",
            "--query"
        };

        // Check for dangerous characters that could indicate command injection
        var dangerousPatterns = new string[]
        {
                ";",        // Command separator
                "&&",       // Command chaining
                "||",       // Command chaining
                "|",        // Pipe (could be dangerous)
                ">",        // Output redirection
                "<",        // Input redirection
                "`",        // Command substitution
                "$(",       // Command substitution
                "\\",       // Escape character
                "\n",       // Newline
                "\r"        // Carriage return
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (command.Contains(pattern))
            {
                // Check if the dangerous pattern is within a whitelisted flag's quoted value
                if (IsDangerousPatternInWhitelistedFlag(command, pattern, whitelistedFlags))
                {
                    continue; // Allow this pattern as it's in a whitelisted flag's quoted value
                }

                return $"[Validation Failed]: Command contains potentially dangerous character(s): {pattern}";
            }
        }

        return null; // No validation errors
    }

    private bool IsDangerousPatternInWhitelistedFlag(string command, string pattern, HashSet<string> whitelistedFlags)
    {
        // Find all occurrences of the dangerous pattern
        int patternIndex = 0;
        while ((patternIndex = command.IndexOf(pattern, patternIndex, StringComparison.Ordinal)) != -1)
        {
            // Check if this pattern occurrence is within a whitelisted flag's quoted value
            if (!IsPatternInWhitelistedFlagValue(command, patternIndex, whitelistedFlags))
            {
                return false; // Found a pattern that's not in a whitelisted flag's value
            }
            patternIndex += pattern.Length;
        }
        return true; // All pattern occurrences are in whitelisted flag values
    }

    private bool IsPatternInWhitelistedFlagValue(string command, int patternIndex, HashSet<string> whitelistedFlags)
    {
        // Look backwards from the pattern to find the nearest flag
        var beforePattern = command.Substring(0, patternIndex);

        // Find the last occurrence of each whitelisted flag before the pattern
        string? matchedFlag = null;
        int flagIndex = -1;

        foreach (var flag in whitelistedFlags)
        {
            int lastFlagIndex = beforePattern.LastIndexOf(flag, StringComparison.OrdinalIgnoreCase);
            if (lastFlagIndex > flagIndex)
            {
                flagIndex = lastFlagIndex;
                matchedFlag = flag;
            }
        }

        if (matchedFlag == null || flagIndex == -1)
        {
            return false; // No whitelisted flag found before the pattern
        }

        // Check if there's a quoted value after the flag that contains the pattern
        var afterFlag = command.Substring(flagIndex + matchedFlag.Length);

        // Skip whitespace after flag
        int valueStart = 0;
        while (valueStart < afterFlag.Length && char.IsWhiteSpace(afterFlag[valueStart]))
        {
            valueStart++;
        }

        if (valueStart >= afterFlag.Length)
        {
            return false; // No value after flag
        }

        // Check if the value starts with a quote
        char quoteChar = afterFlag[valueStart];
        if (quoteChar != '"' && quoteChar != '\'')
        {
            return false; // Value is not quoted
        }

        // Find the closing quote
        int closingQuoteIndex = afterFlag.IndexOf(quoteChar, valueStart + 1);
        if (closingQuoteIndex == -1)
        {
            return false; // No closing quote found
        }

        // Check if the pattern is within the quoted value
        int quotedValueStart = flagIndex + matchedFlag.Length + valueStart + 1; // +1 to skip opening quote
        int quotedValueEnd = flagIndex + matchedFlag.Length + closingQuoteIndex;

        return patternIndex >= quotedValueStart && patternIndex < quotedValueEnd;
    }

    private bool CheckForUnauthorizedAccess(HttpResponseMessage response)
    {
        return (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden);
    }

    public static bool IsReadOnlyCommand(string command)
    {
        var commandLower = command.ToLower().Trim();

        // First, check special read-only command patterns (whitelist)
        foreach (var pattern in ReadOnlyCommandPatterns)
        {
            if (commandLower.StartsWith(pattern.ToLower(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Then, check special write command patterns (to exclude them from read-only)
        foreach (var pattern in WriteCommandPatterns)
        {
            if (commandLower.StartsWith(pattern.ToLower(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Finally, fall back to the general verb-based logic
        return AllowedReadVerbs.Any(verb => commandLower.Contains($" {verb} ") || commandLower.Contains($" {verb}"));
    }

    public static bool IsWriteCommand(string command)
    {
        var commandLower = command.ToLower().Trim();

        // First, check special write command patterns (whitelist)
        foreach (var pattern in WriteCommandPatterns)
        {
            if (commandLower.StartsWith(pattern.ToLower(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Then, check special read-only command patterns (to exclude them from write)
        foreach (var pattern in ReadOnlyCommandPatterns)
        {
            if (commandLower.StartsWith(pattern.ToLower(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Finally, fall back to the general verb-based logic
        return WriteVerbs.Any(verb => commandLower.Contains($" {verb} ") || commandLower.Contains($" {verb}"));
    }

    public static bool IsDeleteCommand(string command)
    {
        var deleteVerbs = new[] { "delete", "remove" };
        var commandLower = command.ToLower();

        // Check if command contains delete verbs as primary action
        return BlockedDeleteVerbs.Any(verb => commandLower.Contains($" {verb} ") || commandLower.Contains($" {verb}"));
    }

    public static bool IsBlockedSubCommand(string command)
    {
        var commandLower = command.ToLower().Trim();

        // Check if command contains blocked subcommands
        return BlockedSubCommands.Any(subCommand => commandLower.Contains($" {subCommand} ") || commandLower.Contains($" {subCommand}"));
    }

    public static bool IsAksCommandInvokeCommand(string command)
    {
        var commandLower = command.ToLower().Trim();

        // Check if command contains "aks command invoke"
        return commandLower.Contains("aks command invoke");
    }

    public static string GetCommandDescription(string command)
    {
        // Extract a user-friendly description from the command
        if (command.Contains("create"))
            return "Creating new Azure resource";
        if (command.Contains("update"))
            return "Updating Azure resource";
        if (command.Contains("set"))
            return "Setting resource configuration";
        if (command.Contains("scale"))
            return "Scaling Azure resource";
        if (command.Contains("start"))
            return "Starting Azure resource";
        if (command.Contains("stop"))
            return "Stopping Azure resource";
        if (command.Contains("restart"))
            return "Restarting Azure resource";

        // Extract the main verb and resource type if possible
        var parts = command.Split(' ');
        if (parts.Length >= 3)
        {
            return $"Executing {parts[1]} {parts[2]}";
        }

        return "Executing Azure CLI write command";
    }

    public static string? ExtractResourceGroupNameFromId(string resourceId)
    {
        var resourceIdentifier = new ResourceIdentifier(resourceId);
        return resourceIdentifier.ResourceGroupName;
    }

    #endregion
}
