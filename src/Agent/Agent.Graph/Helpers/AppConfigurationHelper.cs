// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Azure.Core;
using Azure.Data.AppConfiguration;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Helpers;

public class AppConfigurationHelper
{
    private readonly ILogger _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IAuthenticationService _authenticationService;
    private readonly SqlConnectionStringHelper _sqlHelper;
    private readonly PostgreSqlConnectionStringHelper _postgreSqlHelper;
    private readonly RedisConnectionStringHelper _redisHelper;

    public AppConfigurationHelper(
        ILogger logger,
        IGraphDatabaseClient graphDbClient,
        IAuthenticationService authenticationService,
        ArmClient armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _authenticationService = authenticationService;
        _sqlHelper = new SqlConnectionStringHelper(logger, armClient, graphDbClient);
        _postgreSqlHelper = new PostgreSqlConnectionStringHelper(logger, armClient, graphDbClient);
        _redisHelper = new RedisConnectionStringHelper(logger, armClient);
    }

    /// <summary>
    /// Processes Azure App Configuration to find connected resources and add connections to the graph
    /// </summary>
    /// <param name="sourceNode">The source node that references the App Configuration</param>
    /// <param name="appConfigurationUrl">The URL of the App Configuration (e.g., https://myconfig.azconfig.io)</param>
    /// <returns>Connected resource nodes found through configuration values</returns>
    public async IAsyncEnumerable<GraphNode> ProcessAppConfigurationConnections(
        IList<GraphNode> sourceNodes,
        string appConfigurationUrl)
    {
        _logger.LogInternalInformation($"Processing App Configuration connections for {appConfigurationUrl}");

        ConfigurationClient? configClient = null;
        List<ConfigurationSetting> allSettings = new List<ConfigurationSetting>();
        TokenCredential? credential = null;

        try
        {
            // Get credential using authentication service
            credential = _authenticationService.GetCrawlerCredential();
            configClient = new ConfigurationClient(new Uri(appConfigurationUrl), credential);

            // Retrieve all configuration settings
            var configSettings = configClient.GetConfigurationSettingsAsync(new SettingSelector());

            await foreach (var setting in configSettings)
            {
                allSettings.Add(setting);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error accessing App Configuration {appConfigurationUrl}: {ex.Message}");
            yield break;
        }

        // Process configuration values outside of the main try block
        foreach (var setting in allSettings)
        {
            var configValue = setting.Value;
            var configKey = setting.Key;

            if (setting is SecretReferenceConfigurationSetting kvSetting)
            {
                try
                {
                    configValue = await ResolveKeyVaultSecret(kvSetting.SecretId, credential);
                    _logger.LogDebug($"Resolved Key Vault secret for configuration key: {configKey}");
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Failed to resolve Key Vault secret for {configKey}: {ex.Message}");
                    continue;
                }
            }
            else
            {
                _logger.LogDebug($"Resolved configuration key: {configKey}");
            }

            if (string.IsNullOrEmpty(configValue))
            {
                continue;
            }

            HashSet<string> uniqueNodes = new HashSet<string>();
            foreach (var sourceNode in sourceNodes)
            {
                await foreach (var resourceNode in ProcessConfigurationValue(sourceNode, configKey, configValue))
                {
                    if (uniqueNodes.Add(resourceNode.GetNodeId()))
                    {
                        yield return resourceNode;
                    }
                }
            }
        }
    }

    private async IAsyncEnumerable<ArmResourceNode> ProcessConfigurationValue(
        GraphNode sourceNode,
        string configKey,
        string configValue)
    {
        // Check for SQL connection strings
        if (_sqlHelper.IsSqlConnectionString(configValue))
        {
            var sqlNode = await _sqlHelper.GetSqlResourceFromConnectionStringAsync(
                sourceNode, configValue, "appConfiguration", configKey);
            if (sqlNode != null)
            {
                // Add the detected node to the graph
                await _graphDbClient.AddOrUpdateNodeAsync(sqlNode);

                // Create edge from source to SQL node
                var edge = new ArmResourceEdge(sourceNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.References);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                _logger.LogDebug($"Found SQL connection from App Configuration key: {configKey}");
                yield return sqlNode;
            }
        }
        // Check for PostgreSQL connection strings
        else if (_postgreSqlHelper.IsPostgreSqlConnectionString(configValue, configKey))
        {
            var postgreSqlNode = await _postgreSqlHelper.GetPostgreSqlResourceFromConnectionStringAsync(
                sourceNode, configValue, "appConfiguration", configKey);
            if (postgreSqlNode != null)
            {
                // Add the detected node to the graph
                await _graphDbClient.AddOrUpdateNodeAsync(postgreSqlNode);

                // Create edge from source to PostgreSQL node
                var edge = new ArmResourceEdge(sourceNode.GetNodeId(), postgreSqlNode.GetNodeId(), Constants.Relationships.References);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                _logger.LogDebug($"Found PostgreSQL connection from App Configuration key: {configKey}");
                yield return postgreSqlNode;
            }
        }
        // Check for Redis connection strings
        else if (RedisConnectionStringHelper.IsRedisConnectionString(configValue))
        {
            var redisNode = await _redisHelper.GetRedisResourceFromConnectionStringAsync(
                _graphDbClient, (ArmResourceNode)sourceNode, configValue);
            if (redisNode != null)
            {
                // Add the detected node to the graph
                await _graphDbClient.AddOrUpdateNodeAsync(redisNode);

                // Create edge from source to Redis node
                var edge = new ArmResourceEdge(sourceNode.GetNodeId(), redisNode.GetNodeId(), Constants.Relationships.References);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                _logger.LogDebug($"Found Redis connection from App Configuration key: {configKey}");
                yield return redisNode;
            }
        }
    }

    private async Task<string> ResolveKeyVaultSecret(Uri secretUri, TokenCredential credential)
    {
        try
        {
            var vaultUri = $"{secretUri.Scheme}://{secretUri.Host}";
            var secretName = secretUri.Segments[2].TrimEnd('/');

            var secretClient = new SecretClient(new Uri(vaultUri), credential);
            var secret = await secretClient.GetSecretAsync(secretName);

            return secret.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to resolve Key Vault secret: {ex.Message}");
            throw;
        }
    }
}
