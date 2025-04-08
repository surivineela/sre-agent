// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
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

        var armNode = (ArmResourceNode)node;
        _logger.LogDebug($"Crawling Container App {armNode.ResourceId}");

        // Get the properties for the node to update
        var properties = node.GetNodeProperties();

        var resourceIdentifier = new ResourceIdentifier(armNode.ResourceId);
        var resp = await _armClient.GetGenericResource(resourceIdentifier).GetAsync();
        if (resp == null || !resp.Value.HasData)
        {
            _logger.LogWarning($"Failed to get resource details for: {armNode.ResourceId}");
            yield break;
        }

        var resource = resp.Value;
        var jsonObj = JsonSerializer.Deserialize<JsonElement>(resource.Data.Properties);

        if (resource.Data.Kind != null)
        {
            properties["kind"] = resource.Data.Kind ?? "ContainerApp";
        }

        if (jsonObj.TryGetProperty("provisioningState", out JsonElement provisioningState) && 
            provisioningState.ValueKind == JsonValueKind.String)
        {
            properties["provisioningState"] = provisioningState.GetString();
        }

        if (jsonObj.TryGetProperty("managedEnvironmentId", out JsonElement managedEnvironmentId) && 
            managedEnvironmentId.ValueKind == JsonValueKind.String)
        {
            properties["managedEnvironmentId"] = managedEnvironmentId.GetString();
        }

        if (jsonObj.TryGetProperty("workloadProfileName", out JsonElement workloadProfileName) && 
            workloadProfileName.ValueKind == JsonValueKind.String)
        {
            properties["workloadProfileName"] = workloadProfileName.GetString();
        }

        if (jsonObj.TryGetProperty("configuration", out JsonElement configuration) && 
            configuration.TryGetProperty("ingress", out JsonElement ingress))
        {
            if (ingress.TryGetProperty("fqdn", out JsonElement fqdn) && 
                fqdn.ValueKind == JsonValueKind.String)
            {
                properties["fqdn"] = fqdn.GetString();
            }

            if (ingress.TryGetProperty("external", out JsonElement external) && 
                external.ValueKind == JsonValueKind.True || 
                external.ValueKind == JsonValueKind.False)
            {
                properties["ingressExternal"] = external.GetBoolean();
            }
        }

        if (jsonObj.TryGetProperty("latestRevisionName", out JsonElement latestRevisionName) && 
            latestRevisionName.ValueKind == JsonValueKind.String)
        {
            properties["latestRevisionName"] = latestRevisionName.GetString();
        }

        await _graphDbClient.AddOrUpdateNodeAsync(node);

        if (jsonObj.TryGetProperty("template", out JsonElement template) &&
            template.TryGetProperty("containers", out JsonElement containers) &&
            containers.ValueKind == JsonValueKind.Array)
        {
            foreach (var container in containers.EnumerateArray())
            {
                // Check environment variables
                if (container.TryGetProperty("env", out JsonElement env) &&
                    env.ValueKind == JsonValueKind.Array)
                {
                    foreach (var envVar in env.EnumerateArray())
                    {
                        if (envVar.TryGetProperty("name", out JsonElement nameElement) &&
                            envVar.TryGetProperty("value", out JsonElement valueElement) &&
                            nameElement.ValueKind == JsonValueKind.String &&
                            valueElement.ValueKind == JsonValueKind.String)
                        {
                            var name = nameElement.GetString();
                            var value = valueElement.GetString();
                            if (string.IsNullOrEmpty(value)) continue;

                            if (name == "REDIS_HOST" && !string.IsNullOrEmpty(value))
                            {
                                await foreach (var resourceNode in ProcessRedisHost(armNode, name, value, "env"))
                                {
                                    yield return resourceNode;
                                }
                            }
                            else
                            {
                                await foreach (var resourceNode in ProcessConnectionString(armNode, name, value, "env"))
                                {
                                    yield return resourceNode;
                                }
                            }
                        }
                        // Check secretRef
                        else if (envVar.TryGetProperty("name", out nameElement) &&
                                 envVar.TryGetProperty("secretRef", out JsonElement secretRef) &&
                                 secretRef.TryGetProperty("name", out JsonElement secretName))
                        {
                            var envName = nameElement.GetString();
                            var secretNameValue = secretName.GetString();

                            // Look up the secret value in the secrets section
                            if (template.TryGetProperty("secrets", out JsonElement secrets) &&
                                secrets.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var secret in secrets.EnumerateArray())
                                {
                                    if (secret.TryGetProperty("name", out JsonElement sName) &&
                                        secret.TryGetProperty("value", out JsonElement sValue) &&
                                        sName.GetString() == secretNameValue)
                                    {
                                        var secretValue = sValue.GetString();
                                        if (!string.IsNullOrEmpty(secretValue))
                                        {
                                            await foreach (var resourceNode in ProcessConnectionString(armNode, envName, secretValue, "secret"))
                                            {
                                                yield return resourceNode;
                                            }
                                        }
                                    }
                                }
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

