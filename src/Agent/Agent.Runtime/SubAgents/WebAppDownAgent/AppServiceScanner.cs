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
using Agent.Runtime.SubAgents.ContainerAppsRemediation;
using Agent.Runtime.SubAgents.FunctionAppConnectivityAgent;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.WebAppDownAgent;
public class AppServiceScanner
{
    private readonly ILogger<AppServiceScanner> _logger;
    private readonly ScoreCardService _scoreCardService;
    private readonly IGraphDatabaseClient _graphDBClient;
    private readonly WebAppDownAgentFactory _webAppDownAgentFactory;
    private readonly ContainerAppsRemediationAgentFactory _containerAppsRemediationAgentFactory;
    private readonly FunctionAppConnectivityAgentFactory _functionAppConnectivityAgentFactory;
    private readonly List<object> _webAppsToMitigate;

    public AppServiceScanner(
        ScoreCardService scoreCardService,
        WebAppDownAgentFactory webAppDownAgentFactory,
        ContainerAppsRemediationAgentFactory containerAppsRemediationAgentFactory,
        FunctionAppConnectivityAgentFactory functionAppConnectivityAgentFactory,
        IGraphDatabaseClient graphDBClient,
        ILogger<AppServiceScanner> logger)
    {
        _logger = logger;
        _scoreCardService = scoreCardService;
        _graphDBClient = graphDBClient;
        _webAppsToMitigate = [];
        _webAppDownAgentFactory = webAppDownAgentFactory;
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

                // evaluates if score to see if node is high priority
                bool isHighPriority = await EvaluateScore(node);
                if (isHighPriority)
                {
                    _webAppsToMitigate.Add(node);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error updating score card for node");
            }
        }

        // var agentInput = new WebAppDownAgentInput()
        //_ = _webAppDownAgentFactory.StartOrchestration();
    }

    private async Task<bool> EvaluateScore(ArmResourceNode node)
    {
        return true;
    }

    private string GetResourceNodesToUpdateQuery()
    {
        // TODO: Update the query with resources that has AppHealthInfo
        // Function App vs Web App was causing issue. So removing it for now.
        return $@"g.V()
                .hasLabel(within(
                    '{Constants.ContainerAppType.ToLower()}',
                    '{Constants.AppServiceType.ToLower()}',
                    '{Constants.AzureRedisCacheType.ToLower()}',
                ))
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";
    }

    // Helper method to extract the first value from a property that might be an array
    private string GetFirstPropertyValue(Dictionary<string, object> properties, string key)
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

    private ArmResourceNode CreateArmResourceNodeFromDictionary(Dictionary<string, object> result)
    {
        try
        {
            // Get primary fields
            string id = result["id"]?.ToString();
            string name = result["name"]?.ToString();
            string type = result["type"]?.ToString();

            var properties = result["properties"] as Dictionary<string, object>;
            if (properties == null)
            {
                _logger.LogInternalWarning($"Properties is null for node {id}");
                return null;
            }

            // Extract values, handling arrays in property values
            string resourceId = GetFirstPropertyValue(properties, "resourceId") ?? id;
            string subscriptionId = GetFirstPropertyValue(properties, "subscriptionId");
            string resourceGroupName = GetFirstPropertyValue(properties, "resourceGroupName");
            string resourceName = GetFirstPropertyValue(properties, "resourceName") ?? name;
            string location = GetFirstPropertyValue(properties, "location");

            // Create the ArmResourceNode
            var armResourceNode = new ArmResourceNode(
                resourceType: type,
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
                    string value = GetFirstPropertyValue(properties, prop.Key);
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
