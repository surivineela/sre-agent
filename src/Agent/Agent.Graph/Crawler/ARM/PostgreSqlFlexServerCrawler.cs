// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class PostgreSqlFlexServerCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<PostgreSqlFlexServerCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;

    public PostgreSqlFlexServerCrawler(ILogger<PostgreSqlFlexServerCrawler> logger, IGraphDatabaseClient dbManager, ArmClient armClient)
        : base(logger, dbManager, armClient, false)
    {
        _logger = logger;
        _graphDbClient = dbManager;
    }
    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }
        var postgreSqlNode = (PostgreSqlFlexServerNode)node;
        _logger.LogInternalInformation($"Crawling PostgreSQL Flexible Server {postgreSqlNode.ResourceId}");

        var armResourceId = new ResourceIdentifier(postgreSqlNode.ResourceId);
        var databaseList = new List<ArmResourceNode>();
        var crossResourceNodes = new List<ArmResourceNode>();

        try
        {
            // Get the PostgreSQL flexible server using typed ARM resource calls
            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                armResourceId.SubscriptionId,
                armResourceId.ResourceGroupName);
            var resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);

            var pgServer = await resourceGroup.GetPostgreSqlFlexibleServerAsync(armResourceId.Name);

            if (pgServer?.Value != null)
            {
                var server = pgServer.Value;
                var serverData = server.Data;                // Populate properties from the typed ARM resource
                try
                {
                    if (serverData.Version != null)
                    {
                        postgreSqlNode.ServerVersion = serverData.Version.ToString();
                    }

                    if (serverData.State != null)
                    {
                        postgreSqlNode.ProvisioningState = serverData.State.ToString();
                    }

                    if (!string.IsNullOrEmpty(serverData.FullyQualifiedDomainName))
                    {
                        postgreSqlNode.FullyQualifiedDomainName = serverData.FullyQualifiedDomainName;
                    }

                    if (!string.IsNullOrEmpty(serverData.AvailabilityZone))
                    {
                        postgreSqlNode.AvailabilityZone = serverData.AvailabilityZone;
                    }

                    // Enhanced basic properties
                    if (!string.IsNullOrEmpty(serverData.AdministratorLogin))
                    {
                        postgreSqlNode.AdministratorLogin = serverData.AdministratorLogin;
                    }

                    // SKU information
                    if (serverData.Sku != null)
                    {
                        postgreSqlNode.SkuTier = serverData.Sku.Tier.ToString();
                        postgreSqlNode.SkuName = serverData.Sku.Name;
                    }

                    // Enhanced storage information
                    if (serverData.Storage != null)
                    {
                        if (serverData.Storage.StorageSizeInGB.HasValue)
                        {
                            postgreSqlNode.StorageSize = serverData.Storage.StorageSizeInGB;
                        }

                        if (serverData.Storage.AutoGrow.HasValue)
                        {
                            postgreSqlNode.StorageAutoGrow = serverData.Storage.AutoGrow == Azure.ResourceManager.PostgreSql.FlexibleServers.Models.StorageAutoGrow.Enabled;
                        }

                        if (serverData.Storage.Tier.HasValue)
                        {
                            postgreSqlNode.StorageTier = serverData.Storage.Tier.ToString();
                        }

                        if (serverData.Storage.StorageType.HasValue)
                        {
                            postgreSqlNode.StorageType = serverData.Storage.StorageType.ToString();
                        }

                        if (serverData.Storage.Iops.HasValue)
                        {
                            postgreSqlNode.StorageIops = serverData.Storage.Iops;
                        }

                        if (serverData.Storage.Throughput.HasValue)
                        {
                            postgreSqlNode.StorageThroughput = serverData.Storage.Throughput;
                        }
                    }

                    // Enhanced backup information
                    if (serverData.Backup != null)
                    {
                        if (serverData.Backup.BackupRetentionDays.HasValue)
                        {
                            postgreSqlNode.BackupRetentionDays = serverData.Backup.BackupRetentionDays;
                        }

                        if (serverData.Backup.GeoRedundantBackup.HasValue)
                        {
                            postgreSqlNode.GeoRedundantBackup = serverData.Backup.GeoRedundantBackup == Azure.ResourceManager.PostgreSql.FlexibleServers.Models.PostgreSqlFlexibleServerGeoRedundantBackupEnum.Enabled;
                        }

                        if (serverData.Backup.EarliestRestoreOn.HasValue)
                        {
                            postgreSqlNode.EarliestRestoreOn = serverData.Backup.EarliestRestoreOn.Value.DateTime;
                        }
                    }

                    // Enhanced high availability
                    if (serverData.HighAvailability != null)
                    {
                        if (serverData.HighAvailability.Mode.HasValue)
                        {
                            postgreSqlNode.HighAvailabilityEnabled = serverData.HighAvailability.Mode.ToString() != "Disabled";
                        }

                        if (serverData.HighAvailability.State.HasValue)
                        {
                            postgreSqlNode.HighAvailabilityState = serverData.HighAvailability.State.ToString();
                        }

                        if (!string.IsNullOrEmpty(serverData.HighAvailability.StandbyAvailabilityZone))
                        {
                            postgreSqlNode.StandbyAvailabilityZone = serverData.HighAvailability.StandbyAvailabilityZone;
                        }
                    }

                    // Enhanced network information
                    if (serverData.Network != null)
                    {
                        if (serverData.Network.PublicNetworkAccess.HasValue)
                        {
                            postgreSqlNode.PublicNetworkAccess = serverData.Network.PublicNetworkAccess.ToString();
                        }

                        if (serverData.Network.DelegatedSubnetResourceId is not null)
                        {
                            postgreSqlNode.DelegatedSubnetResourceId = serverData.Network.DelegatedSubnetResourceId.ToString();
                        }

                        if (serverData.Network.PrivateDnsZoneArmResourceId is not null)
                        {
                            postgreSqlNode.PrivateDnsZoneArmResourceId = serverData.Network.PrivateDnsZoneArmResourceId.ToString();
                        }
                    }

                    // Authentication configuration
                    if (serverData.AuthConfig != null)
                    {
                        if (serverData.AuthConfig.ActiveDirectoryAuth.HasValue)
                        {
                            postgreSqlNode.AuthConfigActiveDirectoryAuthEnabled = serverData.AuthConfig.ActiveDirectoryAuth == Azure.ResourceManager.PostgreSql.FlexibleServers.Models.PostgreSqlFlexibleServerActiveDirectoryAuthEnum.Enabled;
                        }

                        if (serverData.AuthConfig.PasswordAuth.HasValue)
                        {
                            postgreSqlNode.AuthConfigPasswordAuthEnabled = serverData.AuthConfig.PasswordAuth == Azure.ResourceManager.PostgreSql.FlexibleServers.Models.PostgreSqlFlexibleServerPasswordAuthEnum.Enabled;
                        }
                    }                    // Data encryption (simplified - may need property name adjustment based on SDK)
                    if (serverData.DataEncryption != null)
                    {
                        // Note: Property names may vary in different SDK versions
                        try
                        {
                            var encryptionTypeProp = serverData.DataEncryption.GetType().GetProperty("Type") ??
                                                   serverData.DataEncryption.GetType().GetProperty("DataEncryptionType");
                            if (encryptionTypeProp != null)
                            {
                                var encryptionTypeValue = encryptionTypeProp.GetValue(serverData.DataEncryption);
                                if (encryptionTypeValue != null)
                                {
                                    postgreSqlNode.DataEncryptionType = encryptionTypeValue.ToString();
                                }
                            }

                            if (serverData.DataEncryption.PrimaryKeyUri != null)
                            {
                                postgreSqlNode.DataEncryptionKeyUri = serverData.DataEncryption.PrimaryKeyUri.ToString();
                            }
                        }
                        catch (Exception encEx)
                        {
                            _logger.LogInternalInformation($"Error accessing data encryption properties: {encEx.Message}");
                        }
                    }

                    // Maintenance window
                    if (serverData.MaintenanceWindow != null)
                    {
                        if (!string.IsNullOrEmpty(serverData.MaintenanceWindow.CustomWindow))
                        {
                            postgreSqlNode.MaintenanceWindowCustom = serverData.MaintenanceWindow.CustomWindow;
                        }

                        if (serverData.MaintenanceWindow.StartHour.HasValue)
                        {
                            postgreSqlNode.MaintenanceWindowStartHour = serverData.MaintenanceWindow.StartHour;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalInformation($"Error parsing PostgreSQL server properties: {ex.Message}");
                }

                await _graphDbClient.AddOrUpdateNodeAsync(postgreSqlNode);                // Create cross-resource relationships and nodes
                try
                {
                    // Create subnet node and relationship if delegated to VNet
                    if (!string.IsNullOrEmpty(postgreSqlNode.DelegatedSubnetResourceId))
                    {
                        var subnetResourceId = new ResourceIdentifier(postgreSqlNode.DelegatedSubnetResourceId);
                        var subnetNode = new ArmResourceNode(
                            resourceType: subnetResourceId.ResourceType,
                            resourceId: postgreSqlNode.DelegatedSubnetResourceId,
                            subscriptionId: subnetResourceId.SubscriptionId!,
                            resourceGroupName: subnetResourceId.ResourceGroupName!,
                            resourceName: subnetResourceId.Name);

                        await _graphDbClient.AddOrUpdateNodeAsync(subnetNode);

                        var subnetEdge = new ArmResourceEdge(
                            postgreSqlNode.GetNodeId(),
                            subnetNode.GetNodeId(),
                            Constants.Relationships.DelegatedTo);
                        await _graphDbClient.AddOrUpdateEdgeAsync(subnetEdge);

                        _logger.LogInternalInformation($"Created subnet node and relationship from PostgreSQL server {postgreSqlNode.ResourceId} to subnet {postgreSqlNode.DelegatedSubnetResourceId}");
                        crossResourceNodes.Add(subnetNode);

                        // Also create VNet node if we have subnet
                        var vnetResourceId = subnetResourceId.Parent!;
                        var vnetNode = new ArmResourceNode(
                            resourceType: vnetResourceId.ResourceType,
                            resourceId: vnetResourceId.ToString(),
                            subscriptionId: vnetResourceId.SubscriptionId!,
                            resourceGroupName: vnetResourceId.ResourceGroupName!,
                            resourceName: vnetResourceId.Name);

                        await _graphDbClient.AddOrUpdateNodeAsync(vnetNode);
                        crossResourceNodes.Add(vnetNode);
                        _logger.LogInternalInformation($"Created VNet node {vnetResourceId}");
                    }

                    // Create Private DNS zone node and relationship
                    if (!string.IsNullOrEmpty(postgreSqlNode.PrivateDnsZoneArmResourceId))
                    {
                        var dnsResourceId = new ResourceIdentifier(postgreSqlNode.PrivateDnsZoneArmResourceId);
                        var dnsNode = new ArmResourceNode(
                            resourceType: dnsResourceId.ResourceType,
                            resourceId: postgreSqlNode.PrivateDnsZoneArmResourceId,
                            subscriptionId: dnsResourceId.SubscriptionId!,
                            resourceGroupName: dnsResourceId.ResourceGroupName!,
                            resourceName: dnsResourceId.Name);

                        await _graphDbClient.AddOrUpdateNodeAsync(dnsNode);

                        var dnsEdge = new ArmResourceEdge(
                            postgreSqlNode.GetNodeId(),
                            dnsNode.GetNodeId(),
                            Constants.Relationships.UsesDnsZone);
                        await _graphDbClient.AddOrUpdateEdgeAsync(dnsEdge);

                        _logger.LogInternalInformation($"Created DNS zone node and relationship from PostgreSQL server {postgreSqlNode.ResourceId} to DNS zone {postgreSqlNode.PrivateDnsZoneArmResourceId}");
                        crossResourceNodes.Add(dnsNode);
                    }

                    // Create Key Vault node and relationship (if using customer-managed keys)
                    if (!string.IsNullOrEmpty(postgreSqlNode.DataEncryptionKeyUri))
                    {
                        // Extract Key Vault resource ID from key URI
                        var keyVaultMatch = System.Text.RegularExpressions.Regex.Match(
                            postgreSqlNode.DataEncryptionKeyUri,
                            @"https://([^\.]+)\.vault\.azure\.net");

                        if (keyVaultMatch.Success)
                        {
                            var keyVaultName = keyVaultMatch.Groups[1].Value;
                            var keyVaultResourceId = $"/subscriptions/{postgreSqlNode.SubscriptionId}/resourceGroups/{postgreSqlNode.ResourceGroupName}/providers/Microsoft.KeyVault/vaults/{keyVaultName}";
                            var kvResourceId = new ResourceIdentifier(keyVaultResourceId);

                            var keyVaultNode = new ArmResourceNode(
                                resourceType: kvResourceId.ResourceType,
                                resourceId: keyVaultResourceId,
                                subscriptionId: kvResourceId.SubscriptionId!,
                                resourceGroupName: kvResourceId.ResourceGroupName!,
                                resourceName: kvResourceId.Name);

                            await _graphDbClient.AddOrUpdateNodeAsync(keyVaultNode);

                            var keyVaultEdge = new ArmResourceEdge(
                                postgreSqlNode.GetNodeId(),
                                keyVaultNode.GetNodeId(),
                                Constants.Relationships.Uses);
                            await _graphDbClient.AddOrUpdateEdgeAsync(keyVaultEdge);

                            _logger.LogInternalInformation($"Created Key Vault node and relationship from PostgreSQL server {postgreSqlNode.ResourceId} to Key Vault {keyVaultResourceId}");
                            crossResourceNodes.Add(keyVaultNode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalInformation($"Error creating cross-resource relationships: {ex.Message}");
                }
                // Crawl databases within the PostgreSQL server using typed ARM calls
                try
                {
                    var databaseCollection = server.GetPostgreSqlFlexibleServerDatabases();
                    foreach (var database in databaseCollection)
                    {
                        var databaseData = database.Data;
                        var databaseNode = new ArmResourceNode(
                            resourceType: "Microsoft.DBforPostgreSQL/flexibleServers/databases",
                            resourceId: database.Id!,
                            subscriptionId: armResourceId.SubscriptionId!,
                            resourceGroupName: armResourceId.ResourceGroupName!,
                            resourceName: databaseData.Name);

                        // Add enhanced database-specific properties
                        var properties = databaseNode.GetNodeProperties();
                        if (!string.IsNullOrEmpty(databaseData.Charset))
                        {
                            properties["charset"] = databaseData.Charset;
                        }

                        if (!string.IsNullOrEmpty(databaseData.Collation))
                        {
                            properties["collation"] = databaseData.Collation;
                        }

                        await _graphDbClient.AddOrUpdateNodeAsync(databaseNode);

                        var edge = new ArmResourceEdge(postgreSqlNode.GetNodeId(), databaseNode.GetNodeId(), Constants.Relationships.Contains);
                        await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                        _logger.LogInternalInformation($"Linked PostgreSQL Server {postgreSqlNode.ResourceId} with Database {databaseData.Name}");

                        databaseList.Add(databaseNode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalInformation($"Error crawling PostgreSQL databases: {ex.Message}");
                }

                // Crawl firewall rules within the PostgreSQL server
                try
                {
                    var firewallRuleCollection = server.GetPostgreSqlFlexibleServerFirewallRules();
                    await foreach (var rule in firewallRuleCollection.GetAllAsync())
                    {
                        var ruleData = rule.Data;
                        var ruleNode = new ArmResourceNode(
                            resourceType: "Microsoft.DBforPostgreSQL/flexibleServers/firewallRules",
                            resourceId: rule.Id!,
                            subscriptionId: armResourceId.SubscriptionId!,
                            resourceGroupName: armResourceId.ResourceGroupName!,
                            resourceName: ruleData.Name);

                        var properties = ruleNode.GetNodeProperties();
                        properties["startIpAddress"] = ruleData.StartIPAddress;
                        properties["endIpAddress"] = ruleData.EndIPAddress;

                        await _graphDbClient.AddOrUpdateNodeAsync(ruleNode);
                        var edge = new ArmResourceEdge(postgreSqlNode.GetNodeId(), ruleNode.GetNodeId(), Constants.Relationships.Contains);
                        await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                        _logger.LogInternalInformation($"Linked PostgreSQL Server {postgreSqlNode.ResourceId} with Firewall Rule {ruleData.Name}");
                        databaseList.Add(ruleNode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalInformation($"Error crawling PostgreSQL firewall rules: {ex.Message}");
                }
            }
            else
            {
                _logger.LogInternalWarning($"PostgreSQL server {armResourceId.Name} not found in resource group {armResourceId.ResourceGroupName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error crawling PostgreSQL Flexible Server {postgreSqlNode.ResourceId}: {ex.Message}");
        }        // Return databases and cross-resource nodes outside of try-catch to avoid yield issues
        foreach (var db in databaseList)
        {
            yield return db;
        }

        // Also yield cross-resource nodes so other crawlers can discover them
        foreach (var crossResourceNode in crossResourceNodes)
        {
            yield return crossResourceNode;
        }
    }
}
