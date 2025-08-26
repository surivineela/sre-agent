using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using Agent.Runtime.Helpers;
using Agent.Core.Helpers;

namespace Agent.Runtime.SubAgents.WebAppDownAgent;
public class AppServiceScanner
{
    private readonly ILogger<AppServiceScanner> _logger;
    private readonly ScoreCardService _scoreCardService;
    private readonly IGraphDatabaseClient _graphDBClient;
    private readonly List<object> _webAppsToMitigate;

    public AppServiceScanner(
        ScoreCardService scoreCardService,
        IGraphDatabaseClient graphDBClient,
        ILogger<AppServiceScanner> logger)
    {
        _logger = logger;
        _scoreCardService = scoreCardService;
        _graphDBClient = graphDBClient;
        _webAppsToMitigate = [];
    }

    public async Task Scan(CancellationToken cancellationToken)
    {
        string nodesToUpdateQuery = GetResourceNodesToUpdateQuery();

        var queryResults = await _graphDBClient.Query(nodesToUpdateQuery);


        foreach (var result in queryResults)
        {
            if (result == null)
            {
                _logger.LogInternalWarning("Null result encountered, skipping");
                continue;
            }

            try
            {
                var node = CreateArmResourceNodeFromDictionary(result);
                if (node == null)
                {
                    _logger.LogInternalWarning($"Could not create ArmResourceNode from result");
                    continue;
                }
                _webAppsToMitigate.Add(node);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error updating score card for node");
            }
        }

        // var agentInput = new WebAppDownAgentInput()
        //_ = _webAppDownAgentFactory.StartOrchestration();
    }

    private string GetResourceNodesToUpdateQuery()
    {
        // TODO: Update the query with resources that has AppHealthInfo
        // Function App vs Web App was causing issue. So removing it for now.
        return $@"g.V().has('isDeleted', false)
                .hasLabel(within(
                    '{Constants.ContainerAppType.ToLower()}',
                    '{Constants.AppServiceType.ToLower()}',
                    '{Constants.AzureRedisCacheType.ToLower()}',
                    '{Constants.ApiManagementType.ToLower()}',
                ))
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";
    }

    // Helper method to extract the first value from a property that might be an array
    private string? GetFirstPropertyValue(Dictionary<string, object> properties, string key)
    {
        if (properties == null || !properties.ContainsKey(key) || properties[key] == null)
            return null;

        var value = properties[key];

        // If it's an array/list, get the first item
        if (value is IEnumerable enumerable && !(value is string))
        {
            foreach (var item in enumerable)
            {
                return item?.ToString();
            }
        }

        // Otherwise, just return the value as string
        return value.ToString();
    }

    private ArmResourceNode? CreateArmResourceNodeFromDictionary(Dictionary<string, object> result)
    {
        try
        {
            // Get primary fields
            string id = result["id"]?.ToString() ?? string.Empty;
            string name = result["name"]?.ToString() ?? string.Empty;
            string type = result["type"]?.ToString() ?? string.Empty;
            string kind = result["kind"]?.ToString() ?? string.Empty;

            var properties = result["properties"] as Dictionary<string, object>;
            if (properties == null)
            {
                _logger.LogInternalWarning($"Properties is null for node {id}");
                return null;
            }

            // Extract values, handling arrays in property values
            string resourceId = GetFirstPropertyValue(properties, "resourceId") ?? throw new Exception("Failed to get property resourceId");
            string subscriptionId = GetFirstPropertyValue(properties, "subscriptionId") ?? throw new Exception("Failed to get property subscriptionId");
            string resourceGroupName = GetFirstPropertyValue(properties, "resourceGroupName") ?? throw new Exception("Failed to get property resourceGroupName");
            string resourceName = GetFirstPropertyValue(properties, "resourceName") ?? name;
            string location = GetFirstPropertyValue(properties, "location") ?? throw new Exception("Failed to get property location");

            // Create the ArmResourceNode
            var armResourceNode = new ArmResourceNode(
                resourceType: type,
                resourceKind: ResourceKindHelper.getResourceKind(type, kind),
                resourceId: resourceId,
                subscriptionId: subscriptionId,
                resourceGroupName: resourceGroupName,
                resourceName: resourceName,
                location: location
            );

            // Add any additional properties
            foreach (var prop in properties)
            {
                if (!armResourceNode.GetNodeProperties().ContainsKey(prop.Key))
                {
                    string? value = GetFirstPropertyValue(properties, prop.Key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        armResourceNode.GetNodeProperties()[prop.Key] = value;
                    }
                }
            }

            return armResourceNode;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error converting dictionary to ArmResourceNode");
            return null;
        }
    }
}
