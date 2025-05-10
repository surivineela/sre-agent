// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Web;
using Agent.Core.Configuration;
using Agent.Core.Helpers.ArmModels;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Charts;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Compute;
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
using Newtonsoft.Json.Linq;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IArmClientFactory _armClientFactory;
    private readonly IAuthenticationService _authService;

    // Crawler MI is used for production environment as current solution
    public ArmHelper(IHttpClientFactory httpClientFactory, IArmClientFactory armClientFactory, IAuthenticationService authService, AzureSettings azureSettings)
    {
        _httpClientFactory = httpClientFactory;
        _armClientFactory = armClientFactory;
        _authService = authService;
    }

    public async Task<List<AzureSubscription>> GetSubscriptionsAsync()
    {
        List<AzureSubscription> allSubs = [];

        var armClient = _armClientFactory.GetArmClient();
        await foreach (SubscriptionResource subscription in armClient.GetSubscriptions().GetAllAsync())
        {
            allSubs.Add(new AzureSubscription(subscription.Data.SubscriptionId, subscription.Data.DisplayName, null));
        }

        return allSubs;
    }

    public async Task<List<string>> GetAllResourceUriAsync(string subscriptionId)
    {
        List<string> resourceUrls = new List<string>();
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return resourceUrls;

        string armUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/resources?api-version=2021-04-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, armUrl);

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {

            string responseBody = await response.Content.ReadAsStringAsync();
            JObject jsonObj = JObject.Parse(responseBody);
            JArray values = (JArray)jsonObj["value"];

            List<string> ids = new List<string>();
            foreach (JObject value in values)
            {
                string id = value["id"].ToString();
                resourceUrls.Add(id);
            }

            return resourceUrls;
        }
        else
        {
            //throw new Exception($"Failed to retrieve resources. Status code: {response.StatusCode}");
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

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return null;
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

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        // Send the GET request
        HttpResponseMessage response = await httpClient.SendAsync(request);
        // Read the response content
        string content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to fetch metrics: {content}");
        }

        JObject metricsJson = JObject.Parse(content);

        // Extract time series data
        foreach (var metric in metricsJson["value"])
        {
            string metricName = metric["name"]["value"].ToString();
            var timeSeries = metric["timeseries"];
            var metricDefinition = metrics.First(m => m.Name == metricName);

            if (timeSeries == null || timeSeries.Count() == 0) continue;

            foreach (var dataPoint in timeSeries[0]["data"])
            {
                var timestamp = DateTime.Parse(dataPoint["timeStamp"].ToString());
                var value = dataPoint[metricDefinition.Aggregation.ToLower()]?.Value<double>();

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

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        // Send the request
        HttpResponseMessage response = await httpClient.SendAsync(request);
        string jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to fetch App service details. Status Code : {response.StatusCode}, Error Response : {jsonResponse}");
        }

        // Deserialize the response to extract the App Service Plan name
        using JsonDocument jsonDocument = JsonDocument.Parse(jsonResponse);
        JsonElement properties = jsonDocument.RootElement.GetProperty("properties");
        string appServicePlanId = properties.GetProperty("serverFarmId").GetString();

        return appServicePlanId;
    }

    public async Task<AppPlanSku> GetCurrentSkuAsync(string appServicePlanResourceId)
    {
        // Construct the request URL to get the App Service Plan details
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appServicePlanResourceId}?api-version=2021-02-01");

        // Prepare the HTTP request
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        // Send the request
        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
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
        if (currentSku == null) return null;

        // Define the SKU progression
        var skuProgression = new[] { "F1", "D1", "B1", "B2", "B3", "S1", "S2", "S3", "P1v2", "P2v2", "P3v2", "P0v3", "P1v3", "P2v3", "P3v3" };

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
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appServicePlanResourceId}?api-version=2021-02-01");
        var requestBody = new
        {
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

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        HttpResponseMessage response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
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

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

    public async Task<bool> RestartWebAppAsync(string appResourceId)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appResourceId}/restart?api-version=2024-04-01");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        HttpResponseMessage response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RestartContainerAppAsync(string appResourceId, string revisionName)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appResourceId}/revisions/{revisionName}/restart?api-version=2025-01-01");
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        HttpResponseMessage response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    // Use the generic method for all specific cases:
    public async Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchTlsStatusAsync);
    }

    public async Task<List<Models.StorageAccountStatus>> GetStorageSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchStorageAccountStatusAsync);
    }

    public async Task<List<CosmosDbStatus>> GetCosmosDbSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchCosmosDbStatusAsync);
    }

    public async Task<List<EventHubStatus>> GetEventHubSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchEventHubStatusAsync);
    }

    public async Task<List<ServiceBusStatus>> GetServiceBusSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchServiceBusStatusAsync);
    }

    public async Task<List<SqlServerSettings>> GetAzureSqlServerSettings(List<string> resourceIds)
    {
        return await GetResourceSettings(resourceIds, FetchSqlServerStatusAsync);
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
            var armClient = _armClientFactory.GetArmClient();
            var resource = await armClient.GetGenericResource(new ResourceIdentifier(resourceId)).GetAsync();
            return resource != null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Resource not found
            return false;
        }
    }

    public async Task<(bool, string)> UpdateMinimumTlsVersion(ApprovalContext approval, TlsStatus tlsStatus, string desiredTlsVersion)
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

        var httpClient = _httpClientFactory.CreateClient();
        var cred = await _authService.GetArmWriteOperationCredential(approval);
        if (cred == null)
        {
            throw new InvalidOperationException("The action is not approved");
        }

        var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

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
        var armClient = _armClientFactory.GetArmClient();
        var storageAccount = armClient.GetStorageAccountResource(new ResourceIdentifier(resourceId));
        return await storageAccount.GetAsync();
    }

    public async Task<CosmosDBAccountResource> GetCosmosDbAccountAsync(string resourceId)
    {
        var armClient = _armClientFactory.GetArmClient();
        var cosmosDBAccountResource = armClient.GetCosmosDBAccountResource(new ResourceIdentifier(resourceId));
        return await cosmosDBAccountResource.GetAsync();
    }

    public async Task<EventHubsNamespaceResource> GetEventHubAccountAsync(string resourceId)
    {
        var armClient = _armClientFactory.GetArmClient();
        var eventHubsNamespaceResource = armClient.GetEventHubsNamespaceResource(new ResourceIdentifier(resourceId));
        return await eventHubsNamespaceResource.GetAsync();
    }

    public async Task<ServiceBusNamespaceResource> GetServiceBusAccountAsync(string resourceId)
    {
        var armClient = _armClientFactory.GetArmClient();
        var serviceBusNamespaceResource = armClient.GetServiceBusNamespaceResource(new ResourceIdentifier(resourceId));
        return await serviceBusNamespaceResource.GetAsync();
    }

    public async Task<SqlServerResource> GetSqlServerAsync(string resourceId)
    {
        var armClient = _armClientFactory.GetArmClient();
        var sqlServerResource = armClient.GetSqlServerResource(new ResourceIdentifier(resourceId));
        return await sqlServerResource.GetAsync();
    }

    public async Task SetStorageAccountSharedKeySupportAsync(string resourceId, FeatureState featureState)
    {
        var storageAccountResource = await GetStorageAccountAsync(resourceId);
        var storageAccountPatch = new StorageAccountPatch()
        {
            AllowSharedKeyAccess = featureState == FeatureState.Enabled ? true : false
        };
        await storageAccountResource.UpdateAsync(storageAccountPatch);
    }

    public async Task SetStorageAccountContainerPublicAccess(string resourceId, FeatureState featureState)
    {
        var storageAccountResource = await GetStorageAccountAsync(resourceId);
        var storageAccountPatch = new StorageAccountPatch()
        {
            AllowBlobPublicAccess = featureState == FeatureState.Enabled ? true : false
        };
        await storageAccountResource.UpdateAsync(storageAccountPatch);
    }

    public async Task SetSqlServerEntraAuthSupport(string resourceId, FeatureState featureState)
    {
        var sqlServer = await GetSqlServerAsync(resourceId);
        var sqlServerAdOnlyAuthResult = await sqlServer.GetSqlServerAzureADOnlyAuthenticationAsync(AuthenticationName.Default);
        sqlServerAdOnlyAuthResult.Value.Data.IsAzureADOnlyAuthenticationEnabled = (featureState == FeatureState.Enabled);
        await sqlServerAdOnlyAuthResult.Value.UpdateAsync(WaitUntil.Completed, sqlServerAdOnlyAuthResult.Value.Data);
    }

    public async Task<string> GetDetectorResponse(string resourceId, string detectorId)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{resourceId}/detectors/{detectorId}");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.UserAgent.ParseAdd("SREAgent");

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

        string formattedStartTime = startTime.Value.ToString("yyyy-MM-dd HH:mm");
        string formattedEndTime = endTime.Value.ToString("yyyy-MM-dd HH:mm");

        var requestUrl = new Uri(new Uri("https://management.azure.com"),
            $"{resourceId}/detectors/{detectorId}?startTime={Uri.EscapeDataString(formattedStartTime)}&endTime={Uri.EscapeDataString(formattedEndTime)}&api-version=2015-08-01");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        try
        {
            var cred = _authService.GetArmOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.UserAgent.ParseAdd("SREAgent");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to retrieve authentication token.", ex);
        }

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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
                                    string subDetectorId = detectorIdElement.GetString();
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

        try
        {
            var cred = _authService.GetArmOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.UserAgent.ParseAdd("SREAgent");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to retrieve authentication token.", ex);
        }

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateNumberOfWorkersAppService(string resourceId, int numberOfWorkers)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required");

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAlwaysOn(string resourceId, bool alwaysOn)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required");

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateHealthcheck(string resourceId, string healthCheckPath)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required");

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

        return response.IsSuccessStatusCode;
    }

    public async Task<Models.StorageAccountStatus> FetchStorageAccountStatusAsync(string resourceId)
    {
        var storageAccount = await GetStorageAccountAsync(resourceId);
        return new Models.StorageAccountStatus(
            ResourceId: resourceId,
            Name: storageAccount.Data.Name,
            Location: storageAccount.Data.Location,
            StorageKeyEnabled: storageAccount.Data.AllowSharedKeyAccess ?? false,
            PublicContainersEnabled: storageAccount.Data.AllowBlobPublicAccess ?? false
            );
    }

    public async Task<CosmosDbStatus> FetchCosmosDbStatusAsync(string resourceId)
    {
        var cosmosDBAccountResource = await GetCosmosDbAccountAsync(resourceId);
        return new CosmosDbStatus(
            ResourceId: resourceId,
            Name: cosmosDBAccountResource.Data.Name,
            Location: cosmosDBAccountResource.Data.Location,
            IsLocalAuthEnabled: cosmosDBAccountResource.Data.DisableLocalAuth ?? false
            );
    }

    public async Task<EventHubStatus> FetchEventHubStatusAsync(string resourceId)
    {
        var eventHubsNamespaceResource = await GetEventHubAccountAsync(resourceId);
        return new EventHubStatus(
            ResourceId: resourceId,
            Name: eventHubsNamespaceResource.Data.Name,
            Location: eventHubsNamespaceResource.Data.Location,
            IsLocalAuthDisabled: eventHubsNamespaceResource.Data.DisableLocalAuth ?? false
            );
    }

    public async Task<ServiceBusStatus> FetchServiceBusStatusAsync(string resourceId)
    {
        var serviceBusNamespaceResource = await GetServiceBusAccountAsync(resourceId);
        return new ServiceBusStatus(
            ResourceId: resourceId,
            Name: serviceBusNamespaceResource.Data.Name,
            Location: serviceBusNamespaceResource.Data.Location,
            IsLocalAuthDisabled: serviceBusNamespaceResource.Data.DisableLocalAuth ?? false
            );
    }

    public async Task<SqlServerSettings> FetchSqlServerStatusAsync(string resourceId)
    {
        var sqlServerResource = await GetSqlServerAsync(resourceId);

        return new SqlServerSettings(
            ResourceId: resourceId,
            Name: sqlServerResource.Data.Name,
            Location: sqlServerResource.Data.Location,
            IsAzureADOnlyAuthenticationEnabled: sqlServerResource.Data.Administrators?.IsAzureADOnlyAuthenticationEnabled ?? false,
            IsEntraAdminSet: sqlServerResource.Data.Administrators?.AdministratorType == SqlAdministratorType.ActiveDirectory
            );
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
        }
    }

    public async Task SetEventHubLocalAuthSupport(string resourceId, FeatureState featureState)
    {
        var eventHubResource = await GetEventHubAccountAsync(resourceId);

        eventHubResource.Data.DisableLocalAuth = (featureState == FeatureState.Disabled);
        await eventHubResource.UpdateAsync(eventHubResource.Data);
    }

    public async Task SetServiceBusLocalAuthSupport(string resourceId, FeatureState featureState)
    {
        var serviceBusNamespaceResource = await GetServiceBusAccountAsync(resourceId);
        var serviceBusNamespacePatch = new ServiceBusNamespacePatch(serviceBusNamespaceResource.Data.Location);

        serviceBusNamespacePatch.DisableLocalAuth = (featureState == FeatureState.Disabled);

        await serviceBusNamespaceResource.UpdateAsync(serviceBusNamespacePatch);
    }

    public async Task<VirtualMachineResource> GetVirtualMachineResourceAsync(string resourceId)
    {
        var armClient = _armClientFactory.GetArmClient();
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
        var armClient = _armClientFactory.GetArmClient();
        var resource = armClient.GetGenericResource(new ResourceIdentifier(resourceId));
        var resourceDataResponse = await resource.GetAsync();
        var resourceData = resourceDataResponse.Value;
        var properties = JsonSerializer.Deserialize<object>(resourceData.Data.Properties.ToString());

        var identity = resourceData.Data.Identity;
        var managedIdentities = new List<GenericArmResourceIdentityModel>();
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
                    .Select(userAssignedIdentity => new GenericArmResourceIdentityModel(IdentityType.UserAssignedManagedIdentity.ToString(), userAssignedIdentity.PrincipalId.Value)));
            }
        }

        GenericArmResourceModel armRes = new GenericArmResourceModel(
            id: resourceData.Data.Id,
            name: resourceData.Data.Name,
            type: resourceData.Data.ResourceType,
            kind: resourceData.Data.Kind ?? string.Empty,
            location: resourceData.Data.Location,
            properties: properties,
            tags: resourceData.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()) ?? new Dictionary<string, string>(),
            IdentityModels: managedIdentities
        );

        // Return the formatted JSON
        return JsonSerializer.Serialize(armRes, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<bool> PowerOnVirtualMachineAsync(ApprovalContext approval, string resourceId)
    {
        var cred = await _authService.GetArmWriteOperationCredential(approval);
        if (cred == null)
        {
            throw new InvalidOperationException("The action is not approved");
        }

        var armClient = _armClientFactory.GetArmClient(cred);
        var virtualMachineResource = armClient.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        if (virtualMachineResource == null)
        {
            throw new ArgumentException($"Resource with ID {resourceId} is not a valid Virtual Machine resource.");
        }
        var startOperation = await virtualMachineResource.PowerOnAsync(WaitUntil.Completed);

        if (cred is IDisposable disposable)
            disposable.Dispose();

        return startOperation.HasCompleted;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnosticsAsync(string resourceId)
    {
        var armClient = _armClientFactory.GetArmClient();
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
        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        var cred = _authService.GetArmReadOperationCredential();
        var token = await cred.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]), CancellationToken.None);

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.Token}");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        HttpResponseMessage responseMessage = await httpClient.SendAsync(request);

        var appSettings = await responseMessage.Content.ReadAsStringAsync();

        return appSettings;
    }

    public async Task<string> GetAppInsightsAppIdBySubscription(string subscriptionId, string instrumentationKey)
    {
        try
        {
            var requestUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/microsoft.insights/components?api-version=2018-05-01-preview";
            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
            var cred = _authService.GetArmReadOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);

            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.Token}");
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

    public async Task<string> ExecuteLogAnalyticsQuery(string resourceId, string queryString, string timeSpan)
    {
        try
        {
            var requestUrl = $"https://management.azure.com{resourceId}/providers/microsoft.insights/diagnosticSettings?api-version=2021-05-01-preview";
            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
            var cred = _authService.GetArmReadOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);

            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.Token}");
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpResponseMessage responseMessage = await httpClient.SendAsync(request);

            if (!responseMessage.IsSuccessStatusCode)
            {
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
                                category == "AppServicePlatformLogs")
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
            var cred = _authService.GetArmReadOperationCredential();
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
                var message = await response.Content.ReadAsStringAsync();
                return $"FAILED! Querying {url} Failed: Status {response.StatusCode}, Message: {message}";
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
            var credential = _authService.GetArmReadOperationCredential();
            var armClient = new ArmClient(credential);
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


            var cred = _authService.GetArmReadOperationCredential();

            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);


            // Create the HTTP request
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };


            // Attach the token to the request
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            // Create and send the HTTP request
            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
            HttpResponseMessage response = await httpClient.SendAsync(request);

            // Check the response status code
            if (response.IsSuccessStatusCode)
            {
                return true; // Swap was successful
            }
            else
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                return false; // Swap failed
            }
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred during the swap operation", ex);
        }
    }

    public async Task<(List<OperationDetail> Deployments, List<OperationDetail> Swaps)> GetDeploymentActivity(string subId, string rg, string resourceId, string st = null, string et = null)
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

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
            HttpResponseMessage response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retrieve deployment activity: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
            }

            var content = await response.Content.ReadAsStringAsync();

            // Parse the response
            JObject jsonResponse = JObject.Parse(content);
            var events = jsonResponse["value"]?.Children<JObject>();

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

    public async Task<List<OperationDetail>> GetCriticalErrorActivityLogs(string subId, string rg, string resourceId, string st = null, string et = null)
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

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

            return errorDetails;
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred during the activity logs retrieval", ex);
        }
    }

    public async Task<string> CheckConnectivityToAzureWebJobsStorage(string resourceId)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));

        httpClient.BaseAddress = new Uri("https://management.azure.com");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, resourceId + "/connectivityCheck?api-version=2022-03-01");

        string payload = @"{
            ""properties"": {
                ""ProviderType"": ""BlobStorage"",
                ""Credentials"": {
                    ""CredentialType"": ""CredentialReference"",
                    ""CredentialReference"": {
                        ""ReferenceType"": ""AppSetting"",
                        ""ReferenceName"": ""AzureWebJobsStorage""
                    }
                },
                ""ResourceMetadata"": {}
            }
        }";
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var res = await httpClient.SendAsync(request);
        if (res.IsSuccessStatusCode)
        {
            var connectivityCheckResult = await res.Content.ReadAsStringAsync();
            return connectivityCheckResult;
        }

        return "Connectivity check failed.";
    }

    public async Task<string> CheckTcpConnectivityAsync(string resourceId, string host, int port)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));

        httpClient.BaseAddress = new Uri("https://management.azure.com");

        httpClient.BaseAddress = new Uri("https://management.azure.com");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, resourceId + "/tcpPingCheck?api-version=2022-03-01");

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

        return "TCP ping check failed.";
    }

    public async Task<IReadOnlyCollection<ArmWrapper<ArmRevisionReplica>>> GetRevisionReplicas(string revisionId)
    {
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{revisionId}/replicas?api-version=2024-03-01");
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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

    public async Task<string> CheckDnsResolution(string resourceId, string desinationUrl)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));

        httpClient.BaseAddress = new Uri("https://management.azure.com");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, resourceId + "/dnsCheck?api-version=2022-03-01");

        string payload = $@"{{
            ""properties"": {{
                ""dnsName"": ""{desinationUrl}""
            }}
        }}";
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var res = await httpClient.SendAsync(request);
        if (res.IsSuccessStatusCode)
        {
            var dnsResolutionCheckResult = await res.Content.ReadAsStringAsync();
            return dnsResolutionCheckResult;
        }

        return "Dns Resolution check failed.";
    }

    public async Task<IDictionary<string, string>> GetAppSetting(string resourceId, string appsettingKey)
    {
        var appSettingKv = new Dictionary<string, string>();

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));

        httpClient.BaseAddress = new Uri("https://management.azure.com");

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, resourceId + "/config/appsettings/list?api-version=2024-04-01");

        var res = await httpClient.SendAsync(request);
        if (res.IsSuccessStatusCode)
        {
            string responseJson = await res.Content.ReadAsStringAsync();
            var appSettingsJobject = JObject.Parse(responseJson)["properties"];
            var value = appSettingsJobject[appsettingKey];
            if (value != null)
            {
                appSettingKv[appsettingKey] = value.ToString();
            }
        }

        return appSettingKv;
    }
    public async Task<bool> UpdateAppSettingsAsync(string resourceId, IDictionary<string, string> appSettings)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || appSettings == null || appSettings.Count == 0)
            throw new ArgumentException("Resource ID and app settings are required");

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        httpClient.BaseAddress = new Uri("https://management.azure.com");

        // Fetch existing app settings
        var existingAppSettingsResponse = await httpClient.PostAsync(resourceId + "/config/appsettings/list?api-version=2024-04-01", null);
        if (!existingAppSettingsResponse.IsSuccessStatusCode)
            throw new Exception($"Failed to fetch existing app settings. Status Code: {existingAppSettingsResponse.StatusCode}");

        var existingAppSettingsJson = await existingAppSettingsResponse.Content.ReadAsStringAsync();
        var existingAppSettings = JsonSerializer.Deserialize<Dictionary<string, string>>(JObject.Parse(existingAppSettingsJson)["properties"]?.ToString() ?? "{}");

        // Merge new app settings with existing ones
        foreach (var kvp in appSettings)
        {
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

        return response.IsSuccessStatusCode;
    }

    public async Task<Dictionary<string, string>> ListKeysForStorageAsync(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required");

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        string requestUrl = $"https://management.azure.com{resourceId}/listKeys?api-version=2023-05-01";

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to retrieve keys. Status Code: {response.StatusCode}, Response: {errorContent}");
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonDocument.Parse(responseContent);

        var keys = jsonResponse.RootElement.GetProperty("keys")
            .EnumerateArray()
            .ToDictionary(
                key => key.GetProperty("keyName").GetString(),
                key => key.GetProperty("value").GetString()
            );

        return keys;
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
        var credential = _authService.GetArmReadOperationCredential();
        var armClient = new ArmClient(credential);
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
            var armClient = _armClientFactory.GetArmClient();
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
            return site.Data.EnabledHostNames.FirstOrDefault(h => h.Contains(".scm."));
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
        using HttpClient httpClient = await GetAuthenticatedHttpClient();

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

                    if (processObj.TryGetPropertyValue("id", out var idNode)
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

            foreach (var processElement in processes)
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
                            if (node.GetValue<string>().Contains("w3wp") && !processInfo.TryGetPropertyValue("is_scm_site", out var _))
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

    public async Task<string> ExecuteKuduCommandAsync(string hostName, string command, string workingDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be null or empty.", nameof(command));

            using HttpClient httpClient = await GetAuthenticatedHttpClient();
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
            Console.Error.WriteLine($"[KuduManager] HTTP request failed during command execution: {ex.Message}");
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
            var cred = _authService.GetArmOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);

            // Add the token to the HttpClient's Authorization header
            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));

            var uriBuilder = new UriBuilder($"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.App/containerApps/{appName}/getAuthToken");
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Add("api-version", "2024-02-02-preview");
            uriBuilder.Query = query.ToString();

            var client = new HttpClient(); // _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
            var response = await client.PostAsync(uriBuilder.Uri, null);
            response.EnsureSuccessStatusCode();
            var resp = await response.Content.ReadFromJsonAsync<GetAuthTokenResponse>();
            if (resp == null || resp.Properties == null || string.IsNullOrEmpty(resp.Properties.Token))
            {
                throw new Exception("Failed to Proxy get API token");
            }

            return resp.Properties.Token;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #region Private Methods

    internal async Task<HttpClient> GetAuthenticatedHttpClient()
    {
        try
        {
            // Retrieve authentication token using DefaultAzureCredential
            var cred = _authService.GetArmOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None);

            // Add the token to the HttpClient's Authorization header
            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
            httpClient.Timeout = TimeSpan.FromMinutes(10);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SRE Agent");
            return httpClient;
        }

        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to obtain a Bearer token.", ex);
        }
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

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync();
        var jsonObject = JObject.Parse(responseJson);

        var properties = jsonObject["properties"];
        var minimumTlsVersion = properties != null
            ? properties["minTlsVersion"]?.ToString()
            : null;

        var tlsStatus = new TlsStatus(
            ResourceId: resourceId,
            Name: resourceId.Split('/').Last(),
            Location: jsonObject["location"]?.ToString(),
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

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get instances: {response.ReasonPhrase}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);
        var machineNames = json["value"]?.Select(instance => instance["properties"]?["machineName"]?.ToString()).ToArray();

        return machineNames;
    }

    private async Task<string> WaitForDaaSSessionCompletionAsync(string appServiceResource, string sessionId)
    {
        sessionId = sessionId.Replace("\"", "");
        var requestUrl = $"https://management.azure.com/{appServiceResource}/extensions/daas/sessions/{sessionId}?api-version=2015-08-01";

        while (true)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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
                return relativePath;
            }

            // Delay for 10 seconds before checking again
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    #endregion
}
