// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Helpers;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class ContainerAppCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<ContainerAppCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly SqlConnectionStringHelper _sqlHelper;
    private readonly PostgreSqlConnectionStringHelper _postgreSqlHelper;
    private readonly AzureResourceGraphClient _graphClient;


    public ContainerAppCrawler(ILogger<ContainerAppCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient, AzureResourceGraphClient graphClient)
        : base(logger, graphDbClient, armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _sqlHelper = new SqlConnectionStringHelper(logger, armClient, graphDbClient);
        _postgreSqlHelper = new PostgreSqlConnectionStringHelper(logger, armClient, graphDbClient);
        _graphClient = graphClient;
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var cappNode = (ContainerAppNode)node;
        _logger.LogInternalInformation($"Crawling Container App {cappNode.ResourceId}");

        var rgResourceId = ResourceGroupResource.CreateResourceIdentifier(cappNode.SubscriptionId, cappNode.ResourceGroupName);
        var rgResource = _armClient.GetResourceGroupResource(rgResourceId);
        if (rgResource == null)
        {
            _logger.LogInternalWarning($"Failed to get container app: {cappNode.ResourceId}");
            yield break;
        }

        var cappResp = await rgResource.GetContainerAppAsync(cappNode.ResourceName);
        if (cappResp == null || !cappResp.Value.HasData)
        {
            _logger.LogInternalWarning($"Failed to get container app: {cappNode.ResourceId}");
            yield break;
        }

        var capp = cappResp.Value.Data;
        this.UpdateContainerAppNode(cappNode, capp);

        await _graphDbClient.AddOrUpdateNodeAsync(cappNode);

        var secrets = cappResp.Value.GetSecretsAsync();
        await foreach (var nextNode in this.ProcessSecrets(capp, cappNode, secrets))
        {
            yield return nextNode;
        }

        var revisions = cappResp.Value.GetContainerAppRevisions().GetAllAsync();
        await foreach (var nextNode in this.ProcessRevisions(cappNode, revisions))
        {
            yield return nextNode;
        }
    }

    private async IAsyncEnumerable<GraphNode> ProcessRevisions(ArmResourceNode cappNode,
        IAsyncEnumerable<ContainerAppRevisionResource> revisions)
    {
        await foreach (var revision in revisions)
        {
            var revisionNode = new ContainerAppRevisionNode
            {
                SubscriptionId = cappNode.SubscriptionId,
                ResourceGroupName = cappNode.ResourceGroupName,
                ResourceId = revision.Id.ToString().ToLower(),
                ResourceType = revision.Data.ResourceType.ToString().ToLower(),
                ResourceName = revision.Data.Name,
                Location = cappNode.Location,
                Name = revision.Data.Name,
                AppName = cappNode.ResourceName,
                Fqdn = revision.Data.Fqdn,
                Labels = string.Join(", ", revision.Data.Labels),
                Template = JsonSerializer.Serialize(revision.Data.Template),
                Replicas = revision.Data.Replicas,
                CreatedOn = revision.Data.CreatedOn?.ToString("u"),
                HealthState = revision.Data.HealthState?.ToString(),
                IsActive = revision.Data.IsActive,
                ProvisioningState = revision.Data.ProvisioningState?.ToString(),
                ProvisioningError = revision.Data.ProvisioningError,
                RunningState = revision.Data.RunningState?.ToString(),
                TrafficWeight = revision.Data.TrafficWeight,
                LastActiveOn = revision.Data.LastActiveOn?.ToString(),
            };
            var edge = new ArmResourceEdge(cappNode.GetNodeId(), revisionNode.GetNodeId(), Constants.Relationships.RevisionOf);

            await _graphDbClient.AddOrUpdateNodeAsync(revisionNode);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            yield return revisionNode;
        }
    }

    private async IAsyncEnumerable<GraphNode> ProcessSecrets(ContainerAppData capp, ArmResourceNode cappNode, IAsyncEnumerable<ContainerAppSecret> appSecrets)
    {
        var kvSecrets = appSecrets.Where(s => s.KeyVaultUri != null);
        HashSet<string> kvNames = new();
        await foreach (var secret in kvSecrets)
        {
            var kvName = KeyVaultHelper.ExtractKeyVaultName(secret.KeyVaultUri);
            if (string.IsNullOrEmpty(kvName))
            {
                continue;
            }

            kvNames.Add(kvName);
        }

        var queryResults = await _graphClient.Query([], $"resources | where name in~ ('{string.Join("','", kvNames)}') and type =~ '{Constants.KeyVaultType}' | project id, subscriptionId, resourceGroup, name, location");
        if (queryResults != null && queryResults.Count > 0)
        {
            var jsonResults = JsonSerializer.Deserialize<JsonElement>(queryResults.Data);
            foreach (var result in jsonResults.EnumerateArray())
            {
                var kvNode = new ArmResourceNode(
                    resourceId: result.GetProperty("id").GetString()!,
                    subscriptionId: result.GetProperty("subscriptionId").GetString()!,
                    resourceGroupName: result.GetProperty("resourceGroup").GetString()!,
                    resourceName: result.GetProperty("name").GetString()!,
                    resourceType: Constants.KeyVaultType,
                    location: result.GetProperty("location").GetString()!
                );
                await _graphDbClient.AddOrUpdateNodeAsync(kvNode);

                var edge = new ArmResourceEdge(cappNode.GetNodeId(), kvNode.GetNodeId(), Constants.Relationships.References);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                yield return kvNode;
            }
        }

        var secrets = await appSecrets.ToDictionaryAsync(secret => secret.Name, secret => secret.Value);

        if (capp.Template == null || capp.Template.Containers.Count == 0)
        {
            yield break;
        }

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

            // Process PostgreSQL environment variables for this container
            var postgreSqlEnvNode = await container.TryProcessPostgreSqlEnvironmentVariablesAsync(
                cappNode, _postgreSqlHelper, "containerApp:environmentVariables", _logger);
            if (postgreSqlEnvNode != null)
            {
                yield return postgreSqlEnvNode;
            }
        }
    }

    private void UpdateContainerAppNode(ContainerAppNode cappNode, ContainerAppData capp)
    {
        cappNode.ProvisioningState = capp.ProvisioningState.ToString();
        cappNode.RunningStatus = capp.RunningStatus.ToString();
        cappNode.WorkloadProfileName = capp.WorkloadProfileName;
        cappNode.EnvironmentId = capp.EnvironmentId.ToString();

        // ingress properties
        cappNode.External = capp.Configuration?.Ingress?.External;
        cappNode.Transport = capp.Configuration?.Ingress?.Transport.ToString();
        cappNode.TargetPort = capp.Configuration?.Ingress?.TargetPort;
        cappNode.ActiveRevisionMode = capp.Configuration?.ActiveRevisionsMode.ToString();

        cappNode.ClientCertificateMode = capp.Configuration?.Ingress?.ClientCertificateMode?.ToString();
        cappNode.AllowInsecure = capp.Configuration?.Ingress?.AllowInsecure;
        cappNode.CorsPolicyJson = capp.Configuration?.Ingress?.CorsPolicy is null ? null : JsonSerializer.Serialize(capp.Configuration?.Ingress.CorsPolicy);
        if (!string.IsNullOrEmpty(capp.Configuration?.Ingress?.Fqdn))
        {
            cappNode.HostNames.Add(capp.Configuration.Ingress.Fqdn);
        }
        if (capp.Configuration?.Ingress?.CustomDomains?.Count > 0)
        {
            foreach (var customDomain in capp.Configuration.Ingress.CustomDomains)
            {
                if (!string.IsNullOrEmpty(customDomain.Name))
                {
                    cappNode.HostNames.Add(customDomain.Name);
                }
            }
        }

        // dapr properties
        var dapr = capp.Configuration?.Dapr;
        cappNode.DaprEnabled = dapr?.IsEnabled;
        cappNode.DaprAppId = dapr?.AppId;
        cappNode.DaprAppPort = dapr?.AppPort;
        cappNode.DaprLogLevel = dapr?.LogLevel?.ToString();

        // containers properties
        if (capp.Template?.Containers.Count > 0)
        {
            cappNode.Containers = [];
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
            cappNode.InitContainers = [];
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

        if (capp.Configuration?.Ingress?.Traffic?.Count > 0)
        {
            cappNode.Traffic = [];
            foreach (var traffic in capp.Configuration.Ingress.Traffic)
            {
                var trafficNode = new ContainerAppNode.TrafficConfiguration
                {
                    RevisionName = traffic.RevisionName,
                    Weight = traffic.Weight ?? 0,
                    Label = traffic.Label,
                    LatestRevision = traffic.IsLatestRevision ?? false
                };
                cappNode.Traffic.Add(trafficNode);
            }
        }

        if (capp.Configuration?.Registries?.Count > 0)
        {
            cappNode.Registries = [];
            foreach (var registry in capp.Configuration.Registries)
            {
                var registryNode = new ContainerAppNode.Registry
                {
                    Server = registry.Server,
                    Username = registry.Username,
                    PasswordSecretRef = registry.PasswordSecretRef,
                    Identity = registry.Identity,
                };
                cappNode.Registries.Add(registryNode);
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
                    resourceGroupName: ArmHelper.ExtractResourceGroupNameFromId(cache.Data.Id!)!,
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
        // Look for PostgreSQL connection strings
        else if (_postgreSqlHelper.IsPostgreSqlConnectionString(value, name))
        {
            var postgreSqlNode = await _postgreSqlHelper.GetPostgreSqlResourceFromConnectionStringAsync(node, value, $"containerApp:{sourceType}", name);
            if (postgreSqlNode != null)
            {
                var properties = postgreSqlNode.GetNodeProperties();
                properties["authType"] = value.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase)
                    ? "managedIdentity"
                    : "connectionString";
                properties["source"] = $"containerApp:{sourceType}:{name}";

                await _graphDbClient.AddOrUpdateNodeAsync(postgreSqlNode);

                yield return postgreSqlNode;
            }
        }
        // Look for Redis connection strings
        else if (RedisConnectionStringHelper.IsRedisConnectionString(value))
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
}

