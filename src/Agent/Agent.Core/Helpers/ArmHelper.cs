// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Charts;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.CosmosDB.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.EventHubs;
using Azure.ResourceManager.ServiceBus;
using Azure.ResourceManager.ServiceBus.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Resources;
using Octokit;

namespace Agent.Core.Helpers;

public class OperationDetail
{
    public string OperationName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string Caller { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool IsSuccessful { get; set; }  // Indicates if the operation was successful 
}

public class ArmHelper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IArmClientFactory _armClientFactory;
    private readonly IAuthenticationService _authService;
    private readonly AzureSettings _azureSettings;

    // Crawler MI is used for production environment as current solution
    public ArmHelper(IHttpClientFactory httpClientFactory, IArmClientFactory armClientFactory, IAuthenticationService authService, AzureSettings azureSettings)
    {
        _httpClientFactory = httpClientFactory;
        _armClientFactory = armClientFactory;
        _authService = authService;
        _azureSettings = azureSettings;
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

            string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, requestUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
            HttpResponseMessage response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                string errorMessage = await response.Content.ReadAsStringAsync();
                return null;
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
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to fetch metrics: {response.ReasonPhrase}");
        }

        // Read the response content
        string content = await response.Content.ReadAsStringAsync();
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
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{appResourceId}/revisions/{revisionName}/restart?api-version=2024-04-01");
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

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
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
        // Construct the request URL to get the Detector URL
        // may need to add startTime and endTime query params
        var requestUrl = new Uri(new Uri("https://management.azure.com"), $"{resourceId}/detectors/{detectorId}");


        // Prepare the HTTP request
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
        // Send the request
        HttpResponseMessage response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to retrieve detector details. Status Code: {response.StatusCode}, Response: {responseBody}");
        }

        // Deserialize the response to extract the SKU and instance count
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
            LocalAuthEnabled: cosmosDBAccountResource.Data.DisableLocalAuth ?? false
            );
    }

    public async Task<EventHubStatus> FetchEventHubStatusAsync(string resourceId)
    {
        var eventHubsNamespaceResource = await GetEventHubAccountAsync(resourceId);
        return new EventHubStatus(
            ResourceId: resourceId,
            Name: eventHubsNamespaceResource.Data.Name,
            Location: eventHubsNamespaceResource.Data.Location,
            LocalAuthEnabled: eventHubsNamespaceResource.Data.DisableLocalAuth ?? false
            );
    }

    public async Task<ServiceBusStatus> FetchServiceBusStatusAsync(string resourceId)
    {
        var serviceBusNamespaceResource = await GetServiceBusAccountAsync(resourceId);
        return new ServiceBusStatus(
            ResourceId: resourceId,
            Name: serviceBusNamespaceResource.Data.Name,
            Location: serviceBusNamespaceResource.Data.Location,
            LocalAuthEnabled: serviceBusNamespaceResource.Data.DisableLocalAuth ?? false
            );
    }

    public async Task<SqlServerSettings> FetchSqlServerStatusAsync(string resourceId)
    {
        var sqlServerResource = await GetSqlServerAsync(resourceId);

        return new SqlServerSettings(
            ResourceId: resourceId,
            Name: sqlServerResource.Data.Name,
            Location: sqlServerResource.Data.Location,
            IsAzureADOnlyAuthenticationEnabled: sqlServerResource.Data.Administrators?.IsAzureADOnlyAuthenticationEnabled ?? false
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

        GenericArmResourceModel armRes = new GenericArmResourceModel(
            resourceData.Data.Id,
            resourceData.Data.Name,
            resourceData.Data.ResourceType,
            resourceData.Data.Location,
            resourceData.Data.Kind ?? string.Empty,
            properties,
            resourceData.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()) ?? new Dictionary<string, string>()
        );

        // Return the formatted JSON
        return JsonSerializer.Serialize(armRes, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<bool> PowerOnVirtualMachineAsync(string resourceId)
    {
        var armClient = _armClientFactory.GetArmClient();
        var virtualMachineResource = armClient.GetVirtualMachineResource(new ResourceIdentifier(resourceId));
        if (virtualMachineResource == null)
        {
            throw new ArgumentException($"Resource with ID {resourceId} is not a valid Virtual Machine resource.");
        }
        var startOperation = await virtualMachineResource.PowerOnAsync(WaitUntil.Completed);
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
        var cred = _authService.GetArmOperationCredential();
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
            var cred = _authService.GetArmOperationCredential();
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
                        return appIdFound ? appId.GetString()! : string.Empty ;
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

    public async Task<string> ExecuteAppInsightsQuery(string appInsightsAppId, string queryString)
    {
        try
        {            
            var endpoint = "https://api.applicationinsights.io/v1/apps/" + appInsightsAppId + "/query";
            
            var httpClient = _httpClientFactory.CreateClient();
            var cred = _authService.GetArmOperationCredential();
            var token = await cred.GetTokenAsync(new TokenRequestContext(new[] { "https://api.applicationinsights.io/.default" }), CancellationToken.None);

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            // Send the query
            var response = await httpClient.PostAsJsonAsync(endpoint, new { query = queryString });

            // Read and display the result
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                return string.Empty;
            }
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


            var cred = _authService.GetArmOperationCredential();

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
    



    public async Task<string> CheckConnectivity(string resourceId)
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

    public async Task<IDictionary<string, string>> FetchAppSetting(string resourceId, string appsettingKey)
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
    #region Private Methods

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

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

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
