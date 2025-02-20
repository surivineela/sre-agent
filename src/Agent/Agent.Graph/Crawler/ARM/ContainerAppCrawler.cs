using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Azure.Core;

namespace Agent.Graph.Crawler.ARM
{
    public class ContainerAppCrawler : IArmResourceCrawler
    {
        private readonly ILogger<ContainerAppCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly ArmClient _armClient;

        public ContainerAppCrawler(ILogger<ContainerAppCrawler> logger, IGraphDatabaseManager dbManager)
        {
            _logger = logger;
            _dbManager = dbManager;
            _armClient = new ArmClient(new DefaultAzureCredential());
        }

        public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            _logger.LogInformation($"Crawling Container App {node.ResourceId}");

            await _dbManager.AddOrUpdateNodeAsync(
                node.GetNodeLabel(),
                node.GetNodeId(),
                node.GetResourceType(),
                node.GetNodeProperties());

            var resourceIdentifier = new ResourceIdentifier(node.ResourceId);
            var resource = _armClient.GetGenericResource(resourceIdentifier);
            if (resource == null || !resource.HasData)
            {
                _logger.LogWarning($"Failed to get resource details for: {node.ResourceId}");
                yield break;
            }

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

                                await foreach (var resourceNode in ProcessConnectionString(node, name, value, "env"))
                                {
                                    yield return resourceNode;
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
                var sqlNode = await sqlHelper.GetSqlResourceFromConnectionStringAsync(_dbManager, node, value);
                if (sqlNode != null)
                {
                    var properties = sqlNode.GetNodeProperties();
                    properties["authType"] = value.Contains("Authentication=Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase) 
                        ? "managedIdentity" 
                        : "connectionString";
                    properties["source"] = $"containerApp:{sourceType}:{name}";

                    await _dbManager.AddOrUpdateNodeAsync(
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
                var redisNode = await redisHelper.GetRedisResourceFromConnectionStringAsync(_dbManager, node, value);
                if (redisNode != null)
                {
                    var properties = redisNode.GetNodeProperties();
                    properties["authType"] = value.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase) 
                        ? "managedIdentity" 
                        : "connectionString";
                    properties["source"] = $"containerApp:{sourceType}:{name}";

                    await _dbManager.AddOrUpdateNodeAsync(
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

        private bool IsRedisConnectionString(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            
            return value.Contains(".redis.cache.windows.net", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("ssl=true", StringComparison.OrdinalIgnoreCase) && 
                   (value.Contains(",abortConnect=false", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("password=", StringComparison.OrdinalIgnoreCase));
        }
    }
}
