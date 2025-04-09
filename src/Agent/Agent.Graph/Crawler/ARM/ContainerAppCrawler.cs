// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class ContainerAppCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<ContainerAppCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly SqlConnectionStringHelper _sqlHelper;


    public ContainerAppCrawler(ILogger<ContainerAppCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient)
        : base(logger, graphDbClient, armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _sqlHelper = new SqlConnectionStringHelper(logger, armClient, graphDbClient);
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var cappNode = (ContainerAppNode)node;
        _logger.LogDebug($"Crawling Container App {cappNode.ResourceId}");

        var rgResourceId = ResourceGroupResource.CreateResourceIdentifier(cappNode.SubscriptionId, cappNode.ResourceGroupName);
        var rgResource = _armClient.GetResourceGroupResource(rgResourceId);
        if (rgResource == null)
        {
            _logger.LogWarning($"Failed to get container app: {cappNode.ResourceId}");
            yield break;
        }

        var cappResp = await rgResource.GetContainerAppAsync(cappNode.ResourceName);
        if (cappResp == null || !cappResp.Value.HasData)
        {
            _logger.LogWarning($"Failed to get container app: {cappNode.ResourceId}");
            yield break;
        }

        var capp = cappResp.Value.Data;
        cappNode.ProvisioningState = capp.ProvisioningState.ToString();
        cappNode.RunningStatus = capp.RunningStatus.ToString();
        cappNode.WorkloadProfileName = capp.WorkloadProfileName;

        // ingress properties
        cappNode.External = capp.Configuration?.Ingress?.External;
        cappNode.Transport = capp.Configuration?.Ingress?.Transport.ToString();
        if (!string.IsNullOrEmpty(capp.Configuration?.Ingress?.Fqdn))
        {
            cappNode.HostNames.Add(capp.Configuration.Ingress.Fqdn);
        }
        if (capp.Configuration.Ingress.CustomDomains.Count > 0)
        {
            foreach (var customDomain in capp.Configuration.Ingress.CustomDomains)
            {
                if (!string.IsNullOrEmpty(customDomain.Name))
                {
                    cappNode.HostNames.Add(customDomain.Name);
                }
            }
        }

        // containers properties
        if (capp.Template?.Containers.Count > 0)
        {
            cappNode.Containers = new List<ContainerAppNode.Container>();
            foreach (var container in capp.Template.Containers)
            {
                var containerNode = new ContainerAppNode.Container
                {
                    Name = container.Name,
                    Image = container.Image,
                    Cpu = container.Resources?.Cpu.ToString(),
                    Memory = container.Resources?.Memory.ToString()
                };
                cappNode.Containers.Add(containerNode);
            }
        }
        if (capp.Template?.InitContainers.Count > 0)
        {
            cappNode.InitContainers = new List<ContainerAppNode.Container>();
            foreach (var container in capp.Template.InitContainers)
            {
                var containerNode = new ContainerAppNode.Container
                {
                    Name = container.Name,
                    Image = container.Image,
                    Cpu = container.Resources?.Cpu.ToString(),
                    Memory = container.Resources?.Memory.ToString()
                };
                cappNode.InitContainers.Add(containerNode);
            }
        }

        // scale properties
        if (capp.Template?.Scale != null)
        {
            cappNode.MinReplicas = capp.Template.Scale.MinReplicas ?? 1;
            cappNode.MaxReplicas = capp.Template.Scale.MaxReplicas;
        }

        await _graphDbClient.AddOrUpdateNodeAsync(cappNode);

        Dictionary<string, string> secrets = new Dictionary<string, string>();
        var cappSecrets = cappResp.Value.GetSecretsAsync();
        await foreach (var secret in cappSecrets)
        {
            secrets.Add(secret.Name, secret.Value);
        }

        if (capp.Template?.Containers.Count > 0)
        {
            foreach (var container in capp.Template.Containers)
            {
                foreach (var env in container.Env)
                {
                    if (env.SecretRef != null)
                    {
                        if (secrets.ContainsKey(env.SecretRef))
                        {
                            var secretValue = secrets[env.SecretRef];
                            if (string.IsNullOrEmpty(secretValue)) continue;

                            await foreach (var resourceNode in ProcessConnectionString(cappNode, env.Name, secretValue, "secret"))
                            {
                                yield return resourceNode;
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(env.Value))
                    {
                        if (env.Name.Equals("REDIS_HOST", StringComparison.OrdinalIgnoreCase))
                        {
                            await foreach (var resourceNode in ProcessRedisHost(cappNode, env.Name, env.Value, "env"))
                            {
                                yield return resourceNode;
                            }
                        }
                        else
                        {
                            await foreach (var resourceNode in ProcessConnectionString(cappNode, env.Name, env.Value, "env"))
                            {
                                yield return resourceNode;
                            }
                        }
                    }
                }
            }
        }
    }

    private async IAsyncEnumerable<ArmResourceNode> ProcessRedisHost(
        ArmResourceNode node,
        string name,
        string hostName,
        string sourceType)
    {
        // Strip any port if present
        int portIndex = hostName.LastIndexOf(':');
        if (portIndex >= 0)
        {
            hostName = hostName.Substring(0, portIndex);
        }

        // Strip the redis suffix if present
        const string redisSuffix = ".redis.cache.windows.net";
        if (hostName.EndsWith(redisSuffix, StringComparison.OrdinalIgnoreCase))
        {
            hostName = hostName.Substring(0, hostName.Length - redisSuffix.Length);
        }

        _logger.LogDebug($"Processing Redis host: {hostName}");

        // Use the RedisConnectionStringHelper to find the Redis resource
        var redisHelper = new RedisConnectionStringHelper(_logger, _armClient);
        var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier("/subscriptions/" + node.SubscriptionId));

        await foreach (var cache in subscription.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Cache/redis'"))
        {
            if (cache.Data.Name.Equals(hostName, StringComparison.OrdinalIgnoreCase))
            {
                var redisResourceId = cache.Data.Id.ToString();
                var redisNode = new ArmResourceNode(
                    resourceType: "Microsoft.Cache/redis",
                    resourceId: redisResourceId,
                    subscriptionId: node.SubscriptionId,
                    resourceGroupName: ExtractResourceGroupName(cache.Data.Id),
                    resourceName: cache.Data.Name);

                var properties = redisNode.GetNodeProperties();
                properties["authType"] = "hostName";
                properties["source"] = $"containerApp:{sourceType}:{name}";

                await _graphDbClient.AddOrUpdateNodeAsync(redisNode);

                var edge = new ArmResourceEdge(node.GetNodeId(), redisNode.GetNodeId(), Constants.Relationships.UsesRedis);
                edge.AdditionalProperties["connectionType"] = sourceType;
                edge.AdditionalProperties["envVarName"] = name;

                await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                _logger.LogDebug($"Found Redis cache {redisResourceId} from host name");
                yield return redisNode;
            }
        }
    }

    private async IAsyncEnumerable<ArmResourceNode> ProcessConnectionString(
        ArmResourceNode node,
        string name,
        string value,
        string sourceType)
    {
        // Look for SQL connection strings
        if (_sqlHelper.IsSqlConnectionString(value))
        {
            var sqlNode = await _sqlHelper.GetSqlResourceFromConnectionStringAsync(node, value, $"containerApp:{sourceType}", name);
            if (sqlNode != null)
            {
                yield return sqlNode;
            }
        }
        // Look for Redis connection strings
        else if (IsRedisConnectionString(value))
        {
            var redisHelper = new RedisConnectionStringHelper(_logger, _armClient);
            var redisNode = await redisHelper.GetRedisResourceFromConnectionStringAsync(_graphDbClient, node, value);
            if (redisNode != null)
            {
                var properties = redisNode.GetNodeProperties();
                properties["authType"] = value.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase)
                    ? "managedIdentity"
                    : "connectionString";
                properties["source"] = $"containerApp:{sourceType}:{name}";

                await _graphDbClient.AddOrUpdateNodeAsync(redisNode);

                yield return redisNode;
            }
        }
    }

    private string ExtractResourceGroupName(ResourceIdentifier resourceId)
    {
        var segments = resourceId.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
            {
                return segments[i + 1];
            }
        }
        return string.Empty;
    }
    private bool IsRedisConnectionString(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        return value.Contains(".redis.cache.windows.net", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("ssl=true", StringComparison.OrdinalIgnoreCase) &&
               (value.Contains(",abortConnect=false", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("password=", StringComparison.OrdinalIgnoreCase));
    }
}

