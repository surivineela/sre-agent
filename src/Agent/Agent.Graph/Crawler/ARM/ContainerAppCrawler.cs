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

    public ContainerAppCrawler(ILogger<ContainerAppCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient)
        : base(logger, graphDbClient, armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
    }

    public override async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        _logger.LogDebug($"Crawling Container App {node.ResourceId}");

        await _graphDbClient.AddOrUpdateNodeAsync(
            node.GetNodeLabel(),
            node.GetNodeId(),
            node.GetResourceType(),
            node.GetNodeProperties());

        var resourceIdentifier = new ResourceIdentifier(node.ResourceId);
        var resp = await _armClient.GetGenericResource(resourceIdentifier).GetAsync();
        if (resp == null || !resp.Value.HasData)
        {
            _logger.LogWarning($"Failed to get resource details for: {node.ResourceId}");
            yield break;
        }

        var resource = resp.Value;
        var jsonObj = JsonSerializer.Deserialize<JsonElement>(resource.Data.Properties);

        // Check template.containers array
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
                                await foreach (var resourceNode in ProcessRedisHost(node, name, value, "env"))
                                {
                                    yield return resourceNode;
                                }
                            }
                            else
                            {
                                await foreach (var resourceNode in ProcessConnectionString(node, name, value, "env"))
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
                                            await foreach (var resourceNode in ProcessConnectionString(node, envName, secretValue, "secret"))
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

                await _graphDbClient.AddOrUpdateNodeAsync(
                    redisNode.GetNodeLabel(),
                    redisNode.GetNodeId(),
                    redisNode.GetResourceType(),
                    properties);

                await _graphDbClient.AddOrUpdateEdgeAsync(
                node.GetNodeId(),
                redisNode.GetNodeId(),
                Constants.Relationships.UsesRedis,
                new Dictionary<string, object>
                {
                    ["updateTs"] = DateTime.UtcNow.Ticks,
                    ["connectionType"] = sourceType,
                    ["envVarName"] = name
                });

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
        if (IsSqlConnectionString(value))
        {
            var sqlHelper = new SqlConnectionStringHelper(_logger, _armClient);
            var sqlNode = await sqlHelper.GetSqlResourceFromConnectionStringAsync(_graphDbClient, node, value);
            if (sqlNode != null)
            {
                var properties = sqlNode.GetNodeProperties();
                properties["authType"] = value.Contains("Authentication=Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase)
                    ? "managedIdentity"
                    : "connectionString";
                properties["source"] = $"containerApp:{sourceType}:{name}";

                await _graphDbClient.AddOrUpdateNodeAsync(
                    sqlNode.GetNodeLabel(),
                    sqlNode.GetNodeId(),
                    sqlNode.GetResourceType(),
                    properties);

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

                await _graphDbClient.AddOrUpdateNodeAsync(
                    redisNode.GetNodeLabel(),
                    redisNode.GetNodeId(),
                    redisNode.GetResourceType(),
                    properties);

                yield return redisNode;
            }
        }
    }

    private bool IsSqlConnectionString(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        return value.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase);
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
