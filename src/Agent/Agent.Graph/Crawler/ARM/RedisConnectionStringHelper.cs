// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Agent.Graph.Crawler.ARM;

public class RedisConnectionStringHelper
{
    private readonly ILogger _logger;
    private readonly ArmClient _armClient;

    public RedisConnectionStringHelper(ILogger logger, ArmClient armClient)
    {
        _logger = logger;
        _armClient = armClient;
    }

    public async Task<ArmResourceNode> GetRedisResourceFromConnectionStringAsync(
        IGraphDatabaseClient graphDbClient,
        ArmResourceNode workloadNode,
        string connectionString)
    {
        try
        {
            var options = ConfigurationOptions.Parse(connectionString);
            var endpoint = options.EndPoints.First();
            string hostName = endpoint.ToString();
            if (hostName.StartsWith("Unspecified/", StringComparison.OrdinalIgnoreCase))
            {
                hostName = hostName.Substring("Unspecified/".Length);
            }

            var serverName = hostName;
            int portIndex = serverName.LastIndexOf(':');
            if (portIndex >= 0)
            {
                serverName = serverName.Substring(0, portIndex);
            }

            const string redisSuffix = ".redis.cache.windows.net";
            if (serverName.Contains(redisSuffix, StringComparison.OrdinalIgnoreCase))
            {
                serverName = serverName.Substring(0, serverName.Length - redisSuffix.Length);
            }

            _logger.LogDebug($"Parsed Redis server name: {serverName}");

            var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier("/subscriptions/" + workloadNode.SubscriptionId));
            await foreach (var cache in subscription.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Cache/redis'"))
            {
                if (cache.Data.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase))
                {
                    var redisResourceId = cache.Data.Id.ToString();
                    var redisNode = new ArmResourceNode(
                        resourceType: "Microsoft.Cache/redis",
                        resourceId: redisResourceId,
                        subscriptionId: workloadNode.SubscriptionId,
                        resourceGroupName: ExtractResourceGroupName(cache.Data.Id),
                        resourceName: cache.Data.Name);

                    await graphDbClient.AddOrUpdateNodeAsync(redisNode);

                    _logger.LogDebug($"Found Redis cache {redisResourceId}");
                    return redisNode;
                }
            }

            _logger.LogInternalWarning($"Redis cache with name {serverName} was not found in the subscription.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error processing Redis connection string: {ex.Message}");
            return null;
        }
    }

    private string ExtractResourceGroupName(string resourceId)
    {
        var segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
            {
                return segments[i + 1];
            }
        }
        return string.Empty;
    }
}
