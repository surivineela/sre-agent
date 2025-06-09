---
mode: 'agent'
---

The below document is recommendations on improvements to the existing Agent.Graph.Crawler.ARM.PostgreSqlFlexServerCrawler
functionality that was just implemented in the previous task. The recommendations come from an agent did a deep dive
on the available functionality in the Azure.ResourceManager.PostgreSql package. The agent generating the recommendations
did not have any insight into this codebase besides the code for the PostgreSqlFlexServerCrawler.

Your task is to evaluate the recommendations based on knowledge about this codebase. Identify any issues with the recommendations,
and provide a new set of recommendations that take into account the existing codebase and the current state of the PostgreSqlFlexServerCrawler.
The new recommendations should be saved as a markdown file.

Priminary Note from the developer:

- The recommendations do add a number of child nodes, but that is OK. The recommendation from the team is that things that are not likely to change much should go in the knowledge graph, and things that are likely to change should be accessed by agents via tool calls.
- Edge relationships should re-use the existing relationships where possible, but new relationships can be added if they make a lot of sense. E.g. USES_DNS_ZONE to indicate a DNS zone. If there's a general relationship to add, e.g. USES, then add it with the idea that other edges will be able to leverage.
- Separate your document into an analysis section and a recommendation section. The recommendation section should be a set of instructions that will be used by agents to implement the changes, and should not include any analysis or discussion.

---

# PostgreSQL Flexible Server Knowledge Graph Enhancement Implementation Guide

This document provides a comprehensive implementation guide for enhancing the knowledge graph crawler to capture additional PostgreSQL Flexible Server data and relationships. The enhancements will significantly improve the depth and quality of insights available from your Azure infrastructure analysis.

## Overview

The current PostgreSQL Flexible Server crawler captures basic server properties but misses critical configuration details, security settings, operational data, and cross-resource relationships. This enhancement adds comprehensive coverage of all available data from the `Azure.ResourceManager.PostgreSql.FlexibleServers` package.

## 1. Enhanced Server-Level Properties

### Current Gap
The existing crawler only captures basic server properties like name, location, SKU, and high-level status. Many important server-level details are missing.

### Enhancement Required
Add the following properties to your `PostgreSqlFlexServerNode` class:

```csharp
// Enhanced server identification and versioning
public string MinorVersion { get; set; }                    // PostgreSQL minor version (e.g., "16.1")
public string AdministratorLogin { get; set; }              // Admin username
public string FullyQualifiedDomainName { get; set; }        // Server FQDN

// Restore and replication properties
public DateTimeOffset? PointInTimeUtc { get; set; }         // Point-in-time restore timestamp
public string SourceServerResourceId { get; set; }          // Source server for replicas/restores
public string ReplicationRole { get; set; }                 // Primary, AsyncReplica, GeoAsyncReplica
public int? ReplicaCapacity { get; set; }                   // Number of allowed replicas
public string CreateMode { get; set; }                      // Create, PointInTimeRestore, GeoRestore, Replica

// Enhanced storage properties
public string StorageType { get; set; }                     // Premium_LRS, PremiumV2_LRS
public bool? StorageAutoGrow { get; set; }                  // Auto-grow enabled
public string StorageTier { get; set; }                     // Storage performance tier
public int? StorageIops { get; set; }                       // IOPS for PremiumV2_LRS storage
public int? StorageThroughput { get; set; }                 // Throughput for PremiumV2_LRS storage

// Enhanced backup properties
public bool? GeoRedundantBackup { get; set; }               // Geo-redundant backup enabled
public DateTimeOffset? EarliestRestoreOn { get; set; }      // Earliest restore point

// High availability details
public string HighAvailabilityState { get; set; }           // NotEnabled, CreatingStandby, Healthy, etc.
public string StandbyAvailabilityZone { get; set; }         // Standby server availability zone

// Network details
public string PublicNetworkAccess { get; set; }             // Enabled, Disabled
public string DelegatedSubnetResourceId { get; set; }       // VNet subnet resource ID
public string PrivateDnsZoneArmResourceId { get; set; }     // Private DNS zone resource ID
```

### Implementation Code for Server Properties
Add this code to your server processing section in the PostgreSQL crawler:

```csharp
// Enhanced server properties extraction
try
{
    // Basic enhanced properties
    if (!string.IsNullOrEmpty(serverData.MinorVersion))
        postgreSqlNode.MinorVersion = serverData.MinorVersion;
    
    if (!string.IsNullOrEmpty(serverData.AdministratorLogin))
        postgreSqlNode.AdministratorLogin = serverData.AdministratorLogin;
    
    if (!string.IsNullOrEmpty(serverData.FullyQualifiedDomainName))
        postgreSqlNode.FullyQualifiedDomainName = serverData.FullyQualifiedDomainName;
    
    // Restore and replication properties
    if (serverData.PointInTimeUtc.HasValue)
        postgreSqlNode.PointInTimeUtc = serverData.PointInTimeUtc;
    
    if (serverData.SourceServerResourceId != null)
        postgreSqlNode.SourceServerResourceId = serverData.SourceServerResourceId.ToString();
    
    if (serverData.ReplicationRole.HasValue)
        postgreSqlNode.ReplicationRole = serverData.ReplicationRole.ToString();
    
    if (serverData.ReplicaCapacity.HasValue)
        postgreSqlNode.ReplicaCapacity = serverData.ReplicaCapacity;
    
    if (serverData.CreateMode.HasValue)
        postgreSqlNode.CreateMode = serverData.CreateMode.ToString();

    // Enhanced storage information
    if (serverData.Storage != null)
    {
        if (serverData.Storage.StorageType.HasValue)
            postgreSqlNode.StorageType = serverData.Storage.StorageType.ToString();
        
        if (serverData.Storage.AutoGrow.HasValue)
            postgreSqlNode.StorageAutoGrow = serverData.Storage.AutoGrow == StorageAutoGrow.Enabled;
        
        if (serverData.Storage.Tier.HasValue)
            postgreSqlNode.StorageTier = serverData.Storage.Tier.ToString();
        
        if (serverData.Storage.Iops.HasValue)
            postgreSqlNode.StorageIops = serverData.Storage.Iops;
        
        if (serverData.Storage.Throughput.HasValue)
            postgreSqlNode.StorageThroughput = serverData.Storage.Throughput;
    }

    // Enhanced backup information
    if (serverData.Backup != null)
    {
        if (serverData.Backup.GeoRedundantBackup.HasValue)
            postgreSqlNode.GeoRedundantBackup = serverData.Backup.GeoRedundantBackup == PostgreSqlFlexibleServerGeoRedundantBackupEnum.Enabled;
        
        if (serverData.Backup.EarliestRestoreOn.HasValue)
            postgreSqlNode.EarliestRestoreOn = serverData.Backup.EarliestRestoreOn;
    }

    // Enhanced high availability
    if (serverData.HighAvailability != null)
    {
        if (serverData.HighAvailability.State.HasValue)
            postgreSqlNode.HighAvailabilityState = serverData.HighAvailability.State.ToString();
        
        if (!string.IsNullOrEmpty(serverData.HighAvailability.StandbyAvailabilityZone))
            postgreSqlNode.StandbyAvailabilityZone = serverData.HighAvailability.StandbyAvailabilityZone;
    }

    // Enhanced network information
    if (serverData.Network != null)
    {
        if (serverData.Network.PublicNetworkAccess.HasValue)
            postgreSqlNode.PublicNetworkAccess = serverData.Network.PublicNetworkAccess.ToString();
        
        if (serverData.Network.DelegatedSubnetResourceId != null)
            postgreSqlNode.DelegatedSubnetResourceId = serverData.Network.DelegatedSubnetResourceId.ToString();
        
        if (serverData.Network.PrivateDnsZoneArmResourceId != null)
            postgreSqlNode.PrivateDnsZoneArmResourceId = serverData.Network.PrivateDnsZoneArmResourceId.ToString();
    }
}
catch (Exception ex)
{
    _logger.LogWarning($"Error extracting enhanced PostgreSQL server properties: {ex.Message}");
}
```

## 2. New Security and Authentication Nodes

### Authentication Configuration Node
Create a new node type to capture authentication settings:

```csharp
public class PostgreSqlFlexServerAuthConfigNode : GraphNode
{
    public string ServerResourceId { get; set; }
    public bool? ActiveDirectoryAuthEnabled { get; set; }
    public bool? PasswordAuthEnabled { get; set; }
    public string TenantId { get; set; }
}
```

### Data Encryption Node
Create a new node type for encryption configuration:

```csharp
public class PostgreSqlFlexServerDataEncryptionNode : GraphNode
{
    public string ServerResourceId { get; set; }
    public string KeyType { get; set; }                          // SystemManaged, AzureKeyVault
    public string PrimaryKeyUri { get; set; }                    // Key Vault URI
    public string PrimaryUserAssignedIdentityId { get; set; }    // Managed Identity resource ID
    public string GeoBackupKeyUri { get; set; }                  // Geo backup key URI
    public string GeoBackupUserAssignedIdentityId { get; set; }  // Geo backup managed identity
    public string PrimaryEncryptionKeyStatus { get; set; }       // Valid, Invalid, etc.
    public string GeoBackupEncryptionKeyStatus { get; set; }
}
```

### Maintenance Window Node
Create a new node type for maintenance window configuration:

```csharp
public class PostgreSqlFlexServerMaintenanceWindowNode : GraphNode
{
    public string ServerResourceId { get; set; }
    public string CustomWindow { get; set; }      // Enabled/Disabled
    public int? StartHour { get; set; }           // 0-23
    public int? StartMinute { get; set; }         // 0-59
    public int? DayOfWeek { get; set; }           // 0-6 (Sunday=0)
}
```

### Implementation Code for Security Nodes
Add this code after creating the main server node:

```csharp
// Authentication configuration
if (serverData.AuthConfig != null)
{
    var authConfigNode = new PostgreSqlFlexServerAuthConfigNode
    {
        Id = $"{postgreSqlNode.ResourceId}/authConfig",
        ServerResourceId = postgreSqlNode.ResourceId,
        ActiveDirectoryAuthEnabled = serverData.AuthConfig.ActiveDirectoryAuth == PostgreSqlFlexibleServerActiveDirectoryAuthEnum.Enabled,
        PasswordAuthEnabled = serverData.AuthConfig.PasswordAuth == PostgreSqlFlexibleServerPasswordAuthEnum.Enabled,
        TenantId = serverData.AuthConfig.TenantId?.ToString()
    };
    
    await _graphDbClient.AddOrUpdateNodeAsync(authConfigNode);
    var authEdge = new ArmResourceEdge(postgreSqlNode.GetNodeId(), authConfigNode.GetNodeId(), "HAS_AUTH_CONFIG");
    await _graphDbClient.AddOrUpdateEdgeAsync(authEdge);
}

// Data encryption configuration
if (serverData.DataEncryption != null)
{
    var dataEncryptionNode = new PostgreSqlFlexServerDataEncryptionNode
    {
        Id = $"{postgreSqlNode.ResourceId}/dataEncryption",
        ServerResourceId = postgreSqlNode.ResourceId,
        KeyType = serverData.DataEncryption.KeyType?.ToString(),
        PrimaryKeyUri = serverData.DataEncryption.PrimaryKeyUri?.ToString(),
        PrimaryUserAssignedIdentityId = serverData.DataEncryption.PrimaryUserAssignedIdentityId?.ToString(),
        GeoBackupKeyUri = serverData.DataEncryption.GeoBackupKeyUri?.ToString(),
        GeoBackupUserAssignedIdentityId = serverData.DataEncryption.GeoBackupUserAssignedIdentityId,
        PrimaryEncryptionKeyStatus = serverData.DataEncryption.PrimaryEncryptionKeyStatus?.ToString(),
        GeoBackupEncryptionKeyStatus = serverData.DataEncryption.GeoBackupEncryptionKeyStatus?.ToString()
    };
    
    await _graphDbClient.AddOrUpdateNodeAsync(dataEncryptionNode);
    var encryptionEdge = new ArmResourceEdge(postgreSqlNode.GetNodeId(), dataEncryptionNode.GetNodeId(), "HAS_DATA_ENCRYPTION");
    await _graphDbClient.AddOrUpdateEdgeAsync(encryptionEdge);
}

// Maintenance window configuration
if (serverData.MaintenanceWindow != null)
{
    var maintenanceNode = new PostgreSqlFlexServerMaintenanceWindowNode
    {
        Id = $"{postgreSqlNode.ResourceId}/maintenanceWindow",
        ServerResourceId = postgreSqlNode.ResourceId,
        CustomWindow = serverData.MaintenanceWindow.CustomWindow,
        StartHour = serverData.MaintenanceWindow.StartHour,
        StartMinute = serverData.MaintenanceWindow.StartMinute,
        DayOfWeek = serverData.MaintenanceWindow.DayOfWeek
    };
    
    await _graphDbClient.AddOrUpdateNodeAsync(maintenanceNode);
    var maintenanceEdge = new ArmResourceEdge(postgreSqlNode.GetNodeId(), maintenanceNode.GetNodeId(), "HAS_MAINTENANCE_WINDOW");
    await _graphDbClient.AddOrUpdateEdgeAsync(maintenanceEdge);
}
```

## 3. Child Resource Collection Crawling

### Server Configurations Node
Create a node to capture PostgreSQL server parameters:

```csharp
public class PostgreSqlFlexServerConfigurationNode : ArmResourceNode
{
    public string ConfigurationName { get; set; }
    public string Value { get; set; }
    public string Description { get; set; }
    public string DefaultValue { get; set; }
    public string DataType { get; set; }        // Boolean, Enumeration, Integer, Numeric
    public string AllowedValues { get; set; }
    public string Source { get; set; }          // system-default, user-override
    public bool? IsDynamicConfig { get; set; }
    public bool? IsReadOnly { get; set; }
    public bool? IsConfigPendingRestart { get; set; }
    public string Unit { get; set; }
    public string DocumentationLink { get; set; }
}
```

### Firewall Rules Node
Create a node to capture network access rules:

```csharp
public class PostgreSqlFlexServerFirewallRuleNode : ArmResourceNode
{
    public string StartIPAddress { get; set; }
    public string EndIPAddress { get; set; }
    public string RuleName { get; set; }
}
```

### Server Backups Node
Create a node to capture backup information:

```csharp
public class PostgreSqlFlexServerBackupNode : ArmResourceNode
{
    public string BackupType { get; set; }          // Full, Incremental
    public DateTimeOffset? CompletedOn { get; set; }
    public string Source { get; set; }
    public string BackupName { get; set; }
}
```

### Private Endpoint Connections Node
Create a node to capture private networking:

```csharp
public class PostgreSqlFlexServerPrivateEndpointConnectionNode : ArmResourceNode
{
    public string PrivateEndpointId { get; set; }
    public string ConnectionState { get; set; }
    public string Description { get; set; }
    public string ActionsRequired { get; set; }
    public string ConnectionName { get; set; }
}
```

### Active Directory Administrators Node
Create a node to capture AD admin assignments:

```csharp
public class PostgreSqlFlexServerActiveDirectoryAdminNode : ArmResourceNode
{
    public string ObjectId { get; set; }
    public string PrincipalType { get; set; }     // User, Group, ServicePrincipal
    public string PrincipalName { get; set; }
    public string TenantId { get; set; }
    public string AdminName { get; set; }
}
```

### Implementation Code for Child Resource Crawling
Add this code as separate methods that are called after the main server processing:

```csharp
// Method: CrawlServerConfigurations
private async IAsyncEnumerable<GraphNode> CrawlServerConfigurations(PostgreSqlFlexibleServerResource server, ArmResourceId armResourceId)
{
    try
    {
        var configurationCollection = server.GetPostgreSqlFlexibleServerConfigurations();
        await foreach (var configuration in configurationCollection.GetAllAsync())
        {
            var configData = configuration.Data;
            var configNode = new PostgreSqlFlexServerConfigurationNode
            {
                ResourceType = "Microsoft.DBforPostgreSQL/flexibleServers/configurations",
                ResourceId = configuration.Id,
                SubscriptionId = armResourceId.SubscriptionId,
                ResourceGroupName = armResourceId.ResourceGroupName,
                ResourceName = configData.Name,
                ConfigurationName = configData.Name,
                Value = configData.Value,
                Description = configData.Description,
                DefaultValue = configData.DefaultValue,
                DataType = configData.DataType?.ToString(),
                AllowedValues = configData.AllowedValues,
                Source = configData.Source,
                IsDynamicConfig = configData.IsDynamicConfig,
                IsReadOnly = configData.IsReadOnly,
                IsConfigPendingRestart = configData.IsConfigPendingRestart,
                Unit = configData.Unit,
                DocumentationLink = configData.DocumentationLink
            };

            await _graphDbClient.AddOrUpdateNodeAsync(configNode);
            var configEdge = new ArmResourceEdge(server.Id.ToString(), configNode.GetNodeId(), "HAS_CONFIGURATION");
            await _graphDbClient.AddOrUpdateEdgeAsync(configEdge);
            
            yield return configNode;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"Error crawling server configurations for {server.Data.Name}: {ex.Message}");
    }
}

// Method: CrawlFirewallRules
private async IAsyncEnumerable<GraphNode> CrawlFirewallRules(PostgreSqlFlexibleServerResource server, ArmResourceId armResourceId)
{
    try
    {
        var firewallRuleCollection = server.GetPostgreSqlFlexibleServerFirewallRules();
        await foreach (var firewallRule in firewallRuleCollection.GetAllAsync())
        {
            var ruleData = firewallRule.Data;
            var firewallNode = new PostgreSqlFlexServerFirewallRuleNode
            {
                ResourceType = "Microsoft.DBforPostgreSQL/flexibleServers/firewallRules",
                ResourceId = firewallRule.Id,
                SubscriptionId = armResourceId.SubscriptionId,
                ResourceGroupName = armResourceId.ResourceGroupName,
                ResourceName = ruleData.Name,
                RuleName = ruleData.Name,
                StartIPAddress = ruleData.StartIPAddress?.ToString(),
                EndIPAddress = ruleData.EndIPAddress?.ToString()
            };

            await _graphDbClient.AddOrUpdateNodeAsync(firewallNode);
            var firewallEdge = new ArmResourceEdge(server.Id.ToString(), firewallNode.GetNodeId(), "HAS_FIREWALL_RULE");
            await _graphDbClient.AddOrUpdateEdgeAsync(firewallEdge);
            
            yield return firewallNode;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"Error crawling firewall rules for {server.Data.Name}: {ex.Message}");
    }
}

// Method: CrawlServerBackups
private async IAsyncEnumerable<GraphNode> CrawlServerBackups(PostgreSqlFlexibleServerResource server, ArmResourceId armResourceId)
{
    try
    {
        var backupCollection = server.GetPostgreSqlFlexibleServerBackups();
        await foreach (var backup in backupCollection.GetAllAsync())
        {
            var backupData = backup.Data;
            var backupNode = new PostgreSqlFlexServerBackupNode
            {
                ResourceType = "Microsoft.DBforPostgreSQL/flexibleServers/backups",
                ResourceId = backup.Id,
                SubscriptionId = armResourceId.SubscriptionId,
                ResourceGroupName = armResourceId.ResourceGroupName,
                ResourceName = backupData.Name,
                BackupName = backupData.Name,
                BackupType = backupData.BackupType?.ToString(),
                CompletedOn = backupData.CompletedOn,
                Source = backupData.Source
            };

            await _graphDbClient.AddOrUpdateNodeAsync(backupNode);
            var backupEdge = new ArmResourceEdge(server.Id.ToString(), backupNode.GetNodeId(), "HAS_BACKUP");
            await _graphDbClient.AddOrUpdateEdgeAsync(backupEdge);
            
            yield return backupNode;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"Error crawling server backups for {server.Data.Name}: {ex.Message}");
    }
}

// Method: CrawlPrivateEndpointConnections
private async IAsyncEnumerable<GraphNode> CrawlPrivateEndpointConnections(PostgreSqlFlexibleServerResource server, ArmResourceId armResourceId)
{
    try
    {
        var privateEndpointConnections = server.GetPostgreSqlFlexibleServersPrivateEndpointConnections();
        await foreach (var connection in privateEndpointConnections.GetAllAsync())
        {
            var connectionData = connection.Data;
            var privateEndpointNode = new PostgreSqlFlexServerPrivateEndpointConnectionNode
            {
                ResourceType = "Microsoft.DBforPostgreSQL/flexibleServers/privateEndpointConnections",
                ResourceId = connection.Id,
                SubscriptionId = armResourceId.SubscriptionId,
                ResourceGroupName = armResourceId.ResourceGroupName,
                ResourceName = connectionData.Name,
                ConnectionName = connectionData.Name,
                PrivateEndpointId = connectionData.PrivateEndpoint?.Id?.ToString(),
                ConnectionState = connectionData.ConnectionState?.Status?.ToString(),
                Description = connectionData.ConnectionState?.Description,
                ActionsRequired = connectionData.ConnectionState?.ActionsRequired
            };

            await _graphDbClient.AddOrUpdateNodeAsync(privateEndpointNode);
            var privateEndpointEdge = new ArmResourceEdge(server.Id.ToString(), privateEndpointNode.GetNodeId(), "HAS_PRIVATE_ENDPOINT_CONNECTION");
            await _graphDbClient.AddOrUpdateEdgeAsync(privateEndpointEdge);
            
            yield return privateEndpointNode;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"Error crawling private endpoint connections for {server.Data.Name}: {ex.Message}");
    }
}

// Method: CrawlActiveDirectoryAdministrators
private async IAsyncEnumerable<GraphNode> CrawlActiveDirectoryAdministrators(PostgreSqlFlexibleServerResource server, ArmResourceId armResourceId)
{
    try
    {
        var activeDirectoryAdministrators = server.GetPostgreSqlFlexibleServerActiveDirectoryAdministrators();
        await foreach (var admin in activeDirectoryAdministrators.GetAllAsync())
        {
            var adminData = admin.Data;
            var adminNode = new PostgreSqlFlexServerActiveDirectoryAdminNode
            {
                ResourceType = "Microsoft.DBforPostgreSQL/flexibleServers/administrators",
                ResourceId = admin.Id,
                SubscriptionId = armResourceId.SubscriptionId,
                ResourceGroupName = armResourceId.ResourceGroupName,
                ResourceName = adminData.Name,
                AdminName = adminData.Name,
                ObjectId = adminData.ObjectId,
                PrincipalType = adminData.PrincipalType?.ToString(),
                PrincipalName = adminData.PrincipalName,
                TenantId = adminData.TenantId?.ToString()
            };

            await _graphDbClient.AddOrUpdateNodeAsync(adminNode);
            var adminEdge = new ArmResourceEdge(server.Id.ToString(), adminNode.GetNodeId(), "HAS_AD_ADMIN");
            await _graphDbClient.AddOrUpdateEdgeAsync(adminEdge);
            
            yield return adminNode;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"Error crawling AD administrators for {server.Data.Name}: {ex.Message}");
    }
}
```

### Call Child Resource Methods
Add these calls to your main server crawling method:

```csharp
// In your main PostgreSQL server crawling method, after creating the server node:
await foreach (var configNode in CrawlServerConfigurations(server, armResourceId))
    yield return configNode;

await foreach (var firewallNode in CrawlFirewallRules(server, armResourceId))
    yield return firewallNode;

await foreach (var backupNode in CrawlServerBackups(server, armResourceId))
    yield return backupNode;

await foreach (var privateEndpointNode in CrawlPrivateEndpointConnections(server, armResourceId))
    yield return privateEndpointNode;

await foreach (var adminNode in CrawlActiveDirectoryAdministrators(server, armResourceId))
    yield return adminNode;
```

## 4. Enhanced Database Information

### Enhanced Database Node Properties
Update your existing database node with additional properties:

```csharp
// Add these properties to your existing PostgreSqlFlexServerDatabaseNode
public string Charset { get; set; }        // Database character set (e.g., UTF8)
public string Collation { get; set; }      // Database collation (e.g., en_US.utf8)
public string DatabaseName { get; set; }   // Database name
```

### Enhanced Database Crawling Implementation
Update your database crawling code:

```csharp
// Enhanced database crawling
var databaseCollection = server.GetPostgreSqlFlexibleServerDatabases();
await foreach (var database in databaseCollection.GetAllAsync())
{
    var databaseData = database.Data;
    var databaseNode = new PostgreSqlFlexServerDatabaseNode
    {
        ResourceType = "Microsoft.DBforPostgreSQL/flexibleServers/databases",
        ResourceId = database.Id,
        SubscriptionId = armResourceId.SubscriptionId,
        ResourceGroupName = armResourceId.ResourceGroupName,
        ResourceName = databaseData.Name,
        DatabaseName = databaseData.Name,
        Charset = databaseData.Charset,
        Collation = databaseData.Collation
    };

    await _graphDbClient.AddOrUpdateNodeAsync(databaseNode);
    var edge = new ArmResourceEdge(server.Id.ToString(), databaseNode.GetNodeId(), "CONTAINS");
    await _graphDbClient.AddOrUpdateEdgeAsync(edge);

    yield return databaseNode;
}
```

## 5. Cross-Resource Relationships

### Managed Identity Relationships
Add code to create relationships with managed identities used for encryption:

```csharp
// Create relationships to managed identities used for data encryption
if (serverData.DataEncryption?.PrimaryUserAssignedIdentityId != null)
{
    var identityEdge = new ArmResourceEdge(
        postgreSqlNode.GetNodeId(), 
        serverData.DataEncryption.PrimaryUserAssignedIdentityId.ToString(), 
        "USES_MANAGED_IDENTITY");
    await _graphDbClient.AddOrUpdateEdgeAsync(identityEdge);
}

if (!string.IsNullOrEmpty(serverData.DataEncryption?.GeoBackupUserAssignedIdentityId))
{
    var geoIdentityEdge = new ArmResourceEdge(
        postgreSqlNode.GetNodeId(), 
        serverData.DataEncryption.GeoBackupUserAssignedIdentityId, 
        "USES_GEO_BACKUP_IDENTITY");
    await _graphDbClient.AddOrUpdateEdgeAsync(geoIdentityEdge);
}
```

### Virtual Network Relationships
Add code to create relationships with VNet resources:

```csharp
// Create relationships to VNet subnets
if (serverData.Network?.DelegatedSubnetResourceId != null)
{
    var subnetEdge = new ArmResourceEdge(
        postgreSqlNode.GetNodeId(), 
        serverData.Network.DelegatedSubnetResourceId.ToString(), 
        "CONNECTED_TO_SUBNET");
    await _graphDbClient.AddOrUpdateEdgeAsync(subnetEdge);
}

// Create relationships to Private DNS zones
if (serverData.Network?.PrivateDnsZoneArmResourceId != null)
{
    var dnsZoneEdge = new ArmResourceEdge(
        postgreSqlNode.GetNodeId(), 
        serverData.Network.PrivateDnsZoneArmResourceId.ToString(), 
        "USES_PRIVATE_DNS_ZONE");
    await _graphDbClient.AddOrUpdateEdgeAsync(dnsZoneEdge);
}
```

### Replica Relationships
Add code to create primary-replica relationships:

```csharp
// Create relationships between primary and replica servers
if (serverData.SourceServerResourceId != null && 
    serverData.ReplicationRole == PostgreSqlFlexibleServerReplicationRole.AsyncReplica)
{
    var replicaEdge = new ArmResourceEdge(
        serverData.SourceServerResourceId.ToString(), 
        postgreSqlNode.GetNodeId(), 
        "HAS_REPLICA");
    await _graphDbClient.AddOrUpdateEdgeAsync(replicaEdge);
}

// Create geo-replica relationships
if (serverData.SourceServerResourceId != null && 
    serverData.ReplicationRole == PostgreSqlFlexibleServerReplicationRole.GeoAsyncReplica)
{
    var geoReplicaEdge = new ArmResourceEdge(
        serverData.SourceServerResourceId.ToString(), 
        postgreSqlNode.GetNodeId(), 
        "HAS_GEO_REPLICA");
    await _graphDbClient.AddOrUpdateEdgeAsync(geoReplicaEdge);
}
```

### Key Vault Relationships
Add code to create relationships with Key Vault keys used for encryption:

```csharp
// Create relationships to Key Vault keys
if (serverData.DataEncryption?.PrimaryKeyUri != null)
{
    // Extract Key Vault resource ID from the key URI
    var keyUri = serverData.DataEncryption.PrimaryKeyUri.ToString();
    var keyVaultResourceId = ExtractKeyVaultResourceId(keyUri);
    if (!string.IsNullOrEmpty(keyVaultResourceId))
    {
        var keyVaultEdge = new ArmResourceEdge(
            postgreSqlNode.GetNodeId(), 
            keyVaultResourceId, 
            "USES_KEY_VAULT");
        await _graphDbClient.AddOrUpdateEdgeAsync(keyVaultEdge);
    }
}

// Helper method to extract Key Vault resource ID from key URI
private string ExtractKeyVaultResourceId(string keyUri)
{
    try
    {
        var uri = new Uri(keyUri);
        var keyVaultName = uri.Host.Split('.')[0];
        // You'll need to construct the full resource ID based on your subscription/resource group
        // This is a simplified example - you may need to enhance this logic
        return $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.KeyVault/vaults/{keyVaultName}";
    }
    catch
    {
        return null;
    }
}
```

## 6. Relationship Constants

Add these relationship constants to your constants file:

```csharp
public static class RelationshipTypes
{
    // Existing relationships
    public const string Contains = "CONTAINS";
    
    // New PostgreSQL-specific relationships
    public const string HasAuthConfig = "HAS_AUTH_CONFIG";
    public const string HasDataEncryption = "HAS_DATA_ENCRYPTION";
    public const string HasConfiguration = "HAS_CONFIGURATION";
    public const string HasFirewallRule = "HAS_FIREWALL_RULE";
    public const string HasBackup = "HAS_BACKUP";
    public const string HasPrivateEndpointConnection = "HAS_PRIVATE_ENDPOINT_CONNECTION";
    public const string HasMaintenanceWindow = "HAS_MAINTENANCE_WINDOW";
    public const string HasActiveDirectoryAdmin = "HAS_AD_ADMIN";
    
    // Cross-resource relationships
    public const string UsesManagedIdentity = "USES_MANAGED_IDENTITY";
    public const string UsesGeoBackupIdentity = "USES_GEO_BACKUP_IDENTITY";
    public const string ConnectedToSubnet = "CONNECTED_TO_SUBNET";
    public const string UsesPrivateDnsZone = "USES_PRIVATE_DNS_ZONE";
    public const string HasReplica = "HAS_REPLICA";
    public const string HasGeoReplica = "HAS_GEO_REPLICA";
    public const string UsesKeyVault = "USES_KEY_VAULT";
}
```

## 7. Error Handling and Logging Enhancements

### Enhanced Error Handling
Implement comprehensive error handling for all new crawling operations:

```csharp
// Enhanced error handling pattern for child resource crawling
private async IAsyncEnumerable<GraphNode> CrawlChildResourcesSafely<T>(
    Func<IAsyncEnumerable<T>> getCollectionFunc,
    Func<T, Task<GraphNode>> processItemFunc,
    string resourceTypeName)
{
    var processedCount = 0;
    var errorCount = 0;
    
    try
    {
        await foreach (var item in getCollectionFunc())
        {
            try
            {
                var node = await processItemFunc(item);
                processedCount++;
                yield return node;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogWarning($"Error processing {resourceTypeName} item: {ex.Message}");
                
                // Continue processing other items even if one fails
                if (errorCount > 10) // Stop if too many errors
                {
                    _logger.LogError($"Too many errors ({errorCount}) processing {resourceTypeName}, stopping");
                    break;
                }
            }
        }
        
        _logger.LogInformation($"Successfully processed {processedCount} {resourceTypeName} items with {errorCount} errors");
    }
    catch (Exception ex)
    {
        _logger.LogError($"Fatal error crawling {resourceTypeName}: {ex.Message}");
    }
}
```

## 8. Performance Considerations

### Parallel Processing for Child Resources
Implement parallel processing where appropriate:

```csharp
// Process child resources in parallel where safe to do so
var childResourceTasks = new List<Task<IAsyncEnumerable<GraphNode>>>
{
    Task.Run(() => CrawlServerConfigurations(server, armResourceId)),
    Task.Run(() => CrawlFirewallRules(server, armResourceId)),
    Task.Run(() => CrawlServerBackups(server, armResourceId)),
    Task.Run(() => CrawlPrivateEndpointConnections(server, armResourceId)),
    Task.Run(() => CrawlActiveDirectoryAdministrators(server, armResourceId))
};

// Process results as they complete
foreach (var task in childResourceTasks)
{
    var childResources = await task;
    await foreach (var node in childResources)
        yield return node;
}
```
