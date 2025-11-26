// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using static Agent.Data.DatabaseClients.GraphDbClient.Nodes.APICenterNode;
using ApimConstants = Agent.Graph.Helpers.APIManagementGraphHelper.Constants;

namespace Agent.Graph.Crawler.ARM;

public class APICenterCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<APICenterCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly AzureResourceGraphClient _graphClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public APICenterCrawler(ILogger<APICenterCrawler> logger, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, ArmClient armClient, IHttpClientFactory httpClientFactory)
    : base(logger, graphDbClient, armClient, false)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _graphClient = graphClient;
        _httpClientFactory = httpClientFactory;
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var apiCenterNode = (APICenterNode)node;
        _logger.LogInternalInformation($"Crawling API Center {apiCenterNode.ResourceId}");

        // Fetch and process resource links
        if (!await FetchAndProcessResourceLinks(apiCenterNode))
        {
            yield break;
        }

        await ProcessApisByWorkspace(apiCenterNode);

        await _graphDbClient.AddOrUpdateNodeAsync(apiCenterNode);

        await CreateEdgesToApiManagementInstances(apiCenterNode);
    }

    private async Task CreateEdgesToApiManagementInstances(APICenterNode apiCenterNode)
    {
        if (apiCenterNode.ApiConnectionInfo != null && apiCenterNode.ApiConnectionInfo.Any())
        {
            // Group connections by unique ApimSourceId
            var groupedConnections = apiCenterNode.ApiConnectionInfo
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value.ApimSourceId))
                .GroupBy(kvp => kvp.Value.ApimSourceId);

            foreach (var group in groupedConnections)
            {
                var apimSourceId = group.Key!;
                _logger.LogInternalInformation($"Creating edge from API Center to APIM: {apimSourceId}");

                var apimNodeIdQuery = apimSourceId.ToLower().Replace("/", "_");
                var query = $@"
                        g.V()
                        .has('id', '{apimNodeIdQuery}')
                        .project('properties')
                        .by(properties().group().by(key()).by(value()))
                        .select('properties')";

                var result = await _graphDbClient.Query<Dictionary<string, object>>(query);

                if (result != null && result.Any())
                {
                    var apimNode = new APIManagementNode(result.First());

                    // Initialize ApiInfoMap if it doesn't exist
                    apimNode.ApiInfoMap ??= new Dictionary<string, APIManagementNode.ApiInfo>();

                    // Process each connection in the group
                    foreach (var connection in group)
                    {
                        _logger.LogInternalInformation($"Processing connection: {connection.Key}, ApiSourceId: {connection.Value.ApiSourceId}, Title: {connection.Value.Title}");

                        // Find matching API in APIM by comparing titles
                        var apicApiTitle = connection.Value.Title;
                        if (string.IsNullOrEmpty(apicApiTitle))
                        {
                            _logger.LogInternalWarning($"No title found for API connection: {connection.Key}");
                            continue;
                        }

                        // Find the API in APIM that matches this title
                        var matchingApimApi = apimNode.ApiInfoMap?
                            .FirstOrDefault(kvp => string.Equals(kvp.Value.DisplayName, apicApiTitle, StringComparison.OrdinalIgnoreCase));

                        if (matchingApimApi.HasValue && matchingApimApi.Value.Key != null)
                        {
                            var apiName = matchingApimApi.Value.Key;

                            // Initialize ApiDependencies dictionary if needed
                            if (apimNode.ApiInfoMap![apiName].ApiDependencies == null)
                            {
                                apimNode.ApiInfoMap[apiName].ApiDependencies = new List<APIManagementNode.ApiDependency>();
                            }

                            // Add the APIC dependency information
                            var targetLinks = apiCenterNode.ResourceLinks?
                                .Where(link => link.Source?.Identifier == connection.Key);

                            if (targetLinks != null && targetLinks.Any())
                            {
                                foreach (var targetLink in targetLinks)
                                {
                                    // This is where we store the APIC dependency information into the APIM node
                                    apimNode.ApiInfoMap[apiName].ApiDependencies!.Add(new APIManagementNode.ApiDependency
                                    {
                                        BackendResourceIdentifier = targetLink?.Target?.Identifier?.ToString(),
                                        BackendResourceType = targetLink?.Target?.Type?.ToString()
                                    });

                                    _logger.LogInternalInformation($"Added APIC dependency for API '{apiName}': {targetLink?.Target?.Identifier}");
                                }

                                _logger.LogInternalInformation($"Matched APIC API '{apicApiTitle}' to APIM API '{apiName}'");
                            }
                        }
                        else
                        {
                            _logger.LogInternalWarning($"Could not find matching APIM API for APIC API title: {apicApiTitle}");
                        }
                    }

                    // Update the APIM node in the database with the new APIC dependency information
                    await _graphDbClient.AddOrUpdateNodeAsync(apimNode);

                    // Add APIC -> APIM Edge for visual representation
                    var sanitizedApicCenterNodeId = await _graphDbClient.GetNodeId(apiCenterNode.GetNodeId());
                    var apimNodeId = await _graphDbClient.GetNodeId(apimSourceId);

                    var apicToApimEdge = new ArmResourceEdge(sanitizedApicCenterNodeId, apimNodeId, Constants.Relationships.Linked);
                    apicToApimEdge.AddOrUpdateEdgeProperty(Constants.ConnectionType, Constants.APICenter);
                    _logger.LogInternalInformation($"Adding or updating edge from API Center to APIM: {sanitizedApicCenterNodeId} -> {apimNodeId}");
                    await _graphDbClient.AddOrUpdateEdgeAsync(apicToApimEdge);
                }
            }
        }
    }

    private async Task<bool> FetchAndProcessResourceLinks(APICenterNode apiCenterNode)
    {
        var getApicResourceLinksUrl = $"{ApimConstants.ManagementAzureBaseUrl}{apiCenterNode.ResourceId}{ApimConstants.ApicDefaultWorkspaceSegment}/links?api-version={ApimConstants.ApicApiVersion}";
        var resourceLinksJson = await GetArmResourceByUrl(getApicResourceLinksUrl);

        if (string.IsNullOrEmpty(resourceLinksJson))
        {
            return false;
        }

        apiCenterNode.PopulateFromApiCenterResourceLinks(resourceLinksJson);

        if (apiCenterNode.ApiConnectionInfo == null || !apiCenterNode.ApiConnectionInfo.Any())
        {
            return false;
        }

        return true;
    }

    private async Task ProcessApisByWorkspace(APICenterNode apiCenterNode)
    {
        // Group connections by workspace
        var connectionsByWorkspace = apiCenterNode.ApiConnectionInfo!
            .GroupBy(conn => conn.Value.Workspace)
            .Where(group => !string.IsNullOrEmpty(group.Key));

        // Iterate through each workspace group
        foreach (var workspaceGroup in connectionsByWorkspace)
        {
            var workspace = workspaceGroup.Key!;
            await ProcessWorkspaceApis(apiCenterNode, workspace, workspaceGroup.ToList());

            await ProcessWorkspaceApiSources(apiCenterNode, workspace, workspaceGroup.ToList());

        }
    }

    private async Task ProcessWorkspaceApiSources(APICenterNode apiCenterNode, string workspace, IEnumerable<KeyValuePair<string, ApiConnectionEntity>> workspaceConnections)
    {
        var getApicApiSourceByWorkspaceUrl = $"{ApimConstants.ManagementAzureBaseUrl}{apiCenterNode.ResourceId}/workspaces/{workspace}/apiSources?api-version={ApimConstants.ApicApiVersion}";
        var apiSourcesJson = await GetArmResourceByUrl(getApicApiSourceByWorkspaceUrl);
        if (string.IsNullOrEmpty(apiSourcesJson))
        {
            _logger.LogInternalWarning($"No API sources found for workspace: {workspace}");
            return;
        }
        try
        {
            ProcessApiSourcesJsonResponse(apiSourcesJson, workspaceConnections);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Failed to parse API sources JSON for workspace {workspace}: {ex.Message}");
        }
    }

    private async Task ProcessWorkspaceApis(APICenterNode apiCenterNode, string workspace, IEnumerable<KeyValuePair<string, ApiConnectionEntity>> workspaceConnections)
    {
        var getApicApiByWorkspaceUrl = $"{ApimConstants.ManagementAzureBaseUrl}{apiCenterNode.ResourceId}/workspaces/{workspace}/apis?api-version={ApimConstants.ApicApiVersion}";
        var apisJson = await GetArmResourceByUrl(getApicApiByWorkspaceUrl);

        if (string.IsNullOrEmpty(apisJson))
        {
            _logger.LogInternalWarning($"No APIs found for workspace: {workspace}");
            return;
        }

        try
        {
            ProcessApisJsonResponse(apisJson, workspaceConnections);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Failed to parse APIs JSON for workspace {workspace}: {ex.Message}");
        }
    }

    private void ProcessApiSourcesJsonResponse(string apiSourcesJson, IEnumerable<KeyValuePair<string, ApiConnectionEntity>> workspaceConnections)
    {
        var apiSourcesResponse = JsonSerializer.Deserialize<JsonElement>(apiSourcesJson);
        if (apiSourcesResponse.TryGetProperty("value", out JsonElement apiSourcesArray) && apiSourcesArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement apiSourceElement in apiSourcesArray.EnumerateArray())
            {
                UpdateConnectionPropertiesFromApiSourceElement(apiSourceElement, workspaceConnections);
            }
        }
    }

    private void ProcessApisJsonResponse(string apisJson, IEnumerable<KeyValuePair<string, ApiConnectionEntity>> workspaceConnections)
    {
        var apisResponse = JsonSerializer.Deserialize<JsonElement>(apisJson);
        if (apisResponse.TryGetProperty("value", out JsonElement apisArray) && apisArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement apiElement in apisArray.EnumerateArray())
            {
                UpdateConnectionPropertiesFromApiElement(apiElement, workspaceConnections);
            }
        }
    }

    private void UpdateConnectionPropertiesFromApiSourceElement(JsonElement apiSourceElement, IEnumerable<KeyValuePair<string, ApiConnectionEntity>> workspaceConnections)
    {
        if (apiSourceElement.TryGetProperty("id", out JsonElement idElement) &&
            apiSourceElement.TryGetProperty("properties", out JsonElement propertiesElement))
        {
            var apiSourceId = idElement.GetString() ?? string.Empty;

            // Check if this apiSource is of type "Apim"
            if (propertiesElement.TryGetProperty("apiSourceType", out JsonElement apiSourceTypeElement) &&
                string.Equals(apiSourceTypeElement.GetString(), "Apim", StringComparison.OrdinalIgnoreCase))
            {
                // Get the APIM resource ID
                string? apimResourceId = null;
                if (propertiesElement.TryGetProperty("azureApiManagementSource", out JsonElement azureApimSourceElement) &&
                    azureApimSourceElement.TryGetProperty("resourceId", out JsonElement resourceIdElement))
                {
                    apimResourceId = resourceIdElement.GetString();
                }

                if (!string.IsNullOrEmpty(apimResourceId))
                {
                    // Find all connections that reference this apiSource
                    foreach (var connection in workspaceConnections)
                    {
                        ApiConnectionEntity entity = connection.Value;

                        // If the connection references this apiSource
                        if (string.Equals(entity.ApiSourceId, apiSourceId, StringComparison.OrdinalIgnoreCase))
                        {
                            // Update the APIM resource ID
                            entity.ApimSourceId = apimResourceId;
                            _logger.LogInternalInformation($"Updated API connection: {entity.Name}, ApiSourceId: {entity.ApiSourceId}, ApimSourceId: {entity.ApimSourceId}");
                        }
                    }
                }
            }
        }
    }

    private void UpdateConnectionPropertiesFromApiElement(JsonElement apiElement, IEnumerable<KeyValuePair<string, ApiConnectionEntity>> workspaceConnections)
    {
        if (apiElement.TryGetProperty("id", out JsonElement idElement) &&
            apiElement.TryGetProperty("properties", out JsonElement propertiesElement))
        {
            var apiId = idElement.GetString() ?? string.Empty;

            // Find matching connections in ApiConnectionInfo by comparing resource IDs
            foreach (var connection in workspaceConnections)
            {
                var identifier = connection.Key;
                ApiConnectionEntity entity = connection.Value;

                var match = Regex.Match(apiId, @"/workspaces/[^/]+/apis/[^/]+$", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var normalizedApiId = match.Value;

                    if (string.Equals(normalizedApiId, identifier, StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateEntityProperties(entity, propertiesElement);
                    }
                }
            }
        }
    }

    private void UpdateEntityProperties(ApiConnectionEntity entity, JsonElement propertiesElement)
    {
        if (propertiesElement.TryGetProperty("title", out JsonElement titleElement))
        {
            entity.Title = titleElement.GetString();
        }

        if (propertiesElement.TryGetProperty("apiSourceId", out JsonElement apiSourceIdElement))
        {
            entity.ApiSourceId = apiSourceIdElement.GetString();
        }

        _logger.LogInternalInformation($"Updated API connection: {entity.Name}, Title: {entity.Title}, ApiSourceId: {entity.ApiSourceId}");
    }

    public async Task<string> GetArmResourceByUrl(string requestUrl)
    {
        _logger.LogInternalInformation($"Retrieving ARM resource with URL: {requestUrl}");

        var httpClient = _httpClientFactory.CreateClient(ApimConstants.ArmOperation);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        HttpResponseMessage responseMessage = await httpClient.SendAsync(request);
        if (responseMessage == null || !responseMessage.IsSuccessStatusCode)
        {
            _logger.LogInternalError($"Failed to retrieve ARM resource. Status Code: {responseMessage?.StatusCode}");
            return string.Empty;
        }

        return await responseMessage.Content.ReadAsStringAsync();
    }
}
