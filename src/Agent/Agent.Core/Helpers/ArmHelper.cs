// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models;
using Agent.Core.Models.Charts;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Resources;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Agent.Core.Helpers;

public class ArmHelper
{
    private readonly ArmClient _armClient;
    private readonly TokenCredential _credential;
    private readonly HttpClient _httpClient;

    // Crawler MI is used for production environment as current solution
    public ArmHelper([FromKeyedServices("CrawlerArmClient")] ArmClient armClient, HttpClient httpClient, CrawlerSettings crawlerSettings)
    {
        var environment = Environment.GetEnvironmentVariable("Environment") ?? "Development";

        if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            ManagedIdentityId mi = ManagedIdentityId.FromUserAssignedClientId(crawlerSettings.IdentityClientId);
            ManagedIdentityCredentialOptions options = new ManagedIdentityCredentialOptions(mi);
            _credential = new ManagedIdentityCredential(options);
            _armClient = armClient;
        }
        else
        {
            _credential = new DefaultAzureCredential();
            _armClient = new ArmClient(_credential);
        }

        _httpClient = httpClient;
    }

    public async Task<List<AzureSubscription>> GetSubscriptionsAsync()
    {
        List<AzureSubscription> allSubs = [];
        if (_armClient == null) return allSubs;

        await foreach (SubscriptionResource subscription in _armClient.GetSubscriptions().GetAllAsync())
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
        string token = await GetAccessTokenAsync();
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, armUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _httpClient.SendAsync(request);

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

    public async Task<List<BasicAuthStatus>> CheckBasicAuth(List<string> resourceIds)
    {
        var output = new List<BasicAuthStatus>();
        if (resourceIds == null) return output;

        string token = await GetAccessTokenAsync();

        foreach (string resourceId in resourceIds)
        {
            var basicAuthResult = new BasicAuthStatus()
            {
                ResourceId = resourceId,
                Name = resourceId.Split('/').Last()
            };

            string basicAuthCheckUrl = $"https://management.azure.com{resourceId}/basicPublishingCredentialsPolicies?api-version=2021-02-01";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, basicAuthCheckUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request);
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

        string token = await GetAccessTokenAsync();

        List<Task<HttpResponseMessage>> tasks = new List<Task<HttpResponseMessage>>();
        if (appInViolation.FtpBasicAuthAllowed)
        {
            string ftpUrl = $"https://management.azure.com{appInViolation.ResourceId}/basicPublishingCredentialsPolicies/ftp?api-version=2021-02-01";
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
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            tasks.Add(_httpClient.SendAsync(request));
        }

        if (appInViolation.ScmBasicAuthAllowed)
        {
            string scmUrl = $"https://management.azure.com{appInViolation.ResourceId}/basicPublishingCredentialsPolicies/scm?api-version=2021-02-01";
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
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            tasks.Add(_httpClient.SendAsync(request));
        }

        if (tasks.Count == 0) return true;

        var result = await Task.WhenAll(tasks);
        return result.All(p => p.IsSuccessStatusCode);
    }

    public async Task<List<TimeSeriesData>> FetchMetricsAsync(string resourceId, List<Metric> metrics)
    {
        return await FetchMetricsAsync(resourceId, metrics, CancellationToken.None);
    }

    public async Task<List<TimeSeriesData>> FetchMetricsAsync(string resourceId, List<Metric> metrics, CancellationToken cancellationToken)
    {
        var timeSeriesData = new List<TimeSeriesData>();
        if (metrics == null) return timeSeriesData;

        string accessToken = await GetAccessTokenAsync();

        string metricNamesString = string.Join(",", metrics.Select(m => m.Name));
        string aggregationsString = string.Join(",", metrics.Select(m => m.Aggregation));
        string requestUri = $"https://management.azure.com{resourceId}/providers/microsoft.insights/metrics?api-version=2018-01-01&metricnames={metricNamesString}&aggregation={aggregationsString}&timespan=PT30M";

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Send the GET request
        HttpResponseMessage response = await _httpClient.SendAsync(request);
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

            foreach (var dataPoint in timeSeries[0]["data"])
            {
                var timestamp = DateTime.Parse(dataPoint["timeStamp"].ToString());
                var value = dataPoint[metricDefinition.Aggregation.ToLower()].Value<double>();

                timeSeriesData.Add(new TimeSeriesData
                {
                    Name = metricDefinition.Name,
                    Timestamp = timestamp,
                    Value = value,
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
        string accessToken = await GetAccessTokenAsync();
        // Prepare the HTTP request
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Send the request
        HttpResponseMessage response = await _httpClient.SendAsync(request);
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
        string requestUrl = $"https://management.azure.com{appServicePlanResourceId}?api-version=2021-02-01";
        string accessToken = await GetAccessTokenAsync();
        // Prepare the HTTP request
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Send the request
        HttpResponseMessage response = await _httpClient.SendAsync(request);

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
        string requestUrl = $"https://management.azure.com{appServicePlanResourceId}?api-version=2021-02-01";
        string accessToken = await GetAccessTokenAsync();
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<string> TakeMemoryDumpAsync(string appServiceResource)
    {
        try
        {
            string accessToken = await GetAccessTokenAsync();
            // Get the instances for the given App Service resource
            var instances = await GetAppServiceInstanceMachineNamesAsync(appServiceResource, accessToken);

            if (instances == null || instances.Length == 0)
            {
                return string.Empty;
            }

            var requestUrl = $"https://management.azure.com/{appServiceResource}/extensions/daas/sessions?api-version=2015-08-01";

            var payload = new
            {
                Mode = "Collect",
                Tool = "MemoryDump",
                Instances = instances
            };

            var content = new StringContent(JObject.FromObject(payload).ToString(), Encoding.UTF8, "application/json");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return String.Empty;
            }

            string sessionId = await response.Content.ReadAsStringAsync();
            return await WaitForDaaSSessionCompletionAsync(appServiceResource, accessToken, sessionId);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public async Task<bool> RestartWebAppAsync(string appResourceId)
    {
        string accessToken = await GetAccessTokenAsync();
        string requestUrl = $"https://management.azure.com{appResourceId}/restart?api-version=2024-04-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RestartContainerAppAsync(string appResourceId, string revisionName)
    {
        string accessToken = await GetAccessTokenAsync();
        string requestUrl = $"https://management.azure.com{appResourceId}/revisions/{revisionName}/restart?api-version=2024-04-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds)
    {
        var output = new List<TlsStatus>();
        if (resourceIds == null || resourceIds.Count == 0) return output;

        string token = await GetAccessTokenAsync();

        const int batchSize = 5;
        for (int i = 0; i < resourceIds.Count; i += batchSize)
        {
            // Take a slice of up to 5 resource IDs
            var chunk = resourceIds.Skip(i).Take(batchSize).ToList();

            // Create tasks for each resource in this chunk
            var tasks = chunk.Select(rid => FetchTlsStatusAsync(rid, token)).ToList();

            // Run them in parallel
            var results = await Task.WhenAll(tasks);

            // Add them to the output (filter out any null if the call failed)
            output.AddRange(results.Where(r => r != null));
        }

        return output;
    }

    public async Task<bool> CheckIfResourceExistsAsync(string resourceId)
    {
        try
        {
            var resource = await _armClient.GetGenericResource(new ResourceIdentifier(resourceId)).GetAsync();
            return resource != null;
        }
        catch(RequestFailedException ex) when (ex.Status == 404)
        {
            // Resource not found
            return false;
        }
    }

    public async Task<(bool, string)> UpdateMinimumTlsVersion(TlsStatus tlsStatus, string desiredTlsVersion)
    {
        if (tlsStatus == null || string.IsNullOrWhiteSpace(tlsStatus.ResourceId))
            throw new ArgumentException("Resource ID is required");

        string token = await GetAccessTokenAsync();

        string tlsUpdateUrl = $"https://management.azure.com{tlsStatus.ResourceId}/config/web?api-version=2022-03-01";

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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;

        var response = await _httpClient.SendAsync(request);

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


    private async Task<TlsStatus> FetchTlsStatusAsync(string resourceId, string token)
    {
        string tlsCheckUrl = $"https://management.azure.com{resourceId}/config/web?api-version=2022-03-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, tlsCheckUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
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

    #region Private Methods

    private async Task<string> GetAccessTokenAsync()
    {
        var tokenRequestContext = new TokenRequestContext(["https://management.azure.com/.default"]);
        AccessToken accessToken = await _credential.GetTokenAsync(tokenRequestContext, default);
        return accessToken.Token;
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

    private async Task<string[]> GetAppServiceInstanceMachineNamesAsync(string appServiceResource, string accessToken)
    {
        var requestUrl = $"https://management.azure.com{appServiceResource}/instances?api-version=2021-02-01";

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get instances: {response.ReasonPhrase}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);
        var machineNames = json["value"]?.Select(instance => instance["properties"]?["machineName"]?.ToString()).ToArray();

        return machineNames;
    }

    private async Task<string> WaitForDaaSSessionCompletionAsync(string appServiceResource, string accessToken, string sessionId)
    {
        sessionId = sessionId.Replace("\"", "");
        var requestUrl = $"https://management.azure.com/{appServiceResource}/extensions/daas/sessions/{sessionId}?api-version=2015-08-01";

        while (true)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.SendAsync(request);

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
