---
mode: 'agent'
---

Implement the following improvements to the PostgreSqlFlexServerCrawler.
Ensure the Azure.ResourceManager.PostgreSql package supports all the new features and properties.
Look for ways to improve the implemention that is recommended in the context of the codebase.

### 1. Enhanced Server-Level Properties

Add missing properties to the existing `PostgreSqlFlexServerNode` class:

```csharp
// Add to PostgreSqlFlexServerNode.cs
[GraphProperty("administratorLogin")]
public string? AdministratorLogin { get; set; }

[GraphProperty("storageAutoGrow")]
public bool? StorageAutoGrow { get; set; }

[GraphProperty("storageTier")]
public string? StorageTier { get; set; }

[GraphProperty("storageType")]
public string? StorageType { get; set; }

[GraphProperty("storageIops")]
public int? StorageIops { get; set; }

[GraphProperty("storageThroughput")]
public int? StorageThroughput { get; set; }

[GraphProperty("geoRedundantBackup")]
public bool? GeoRedundantBackup { get; set; }

[GraphProperty("earliestRestoreOn")]
public DateTime? EarliestRestoreOn { get; set; }

[GraphProperty("highAvailabilityState")]
public string? HighAvailabilityState { get; set; }

[GraphProperty("standbyAvailabilityZone")]
public string? StandbyAvailabilityZone { get; set; }

[GraphProperty("delegatedSubnetResourceId")]
public string? DelegatedSubnetResourceId { get; set; }

[GraphProperty("privateDnsZoneArmResourceId")]
public string? PrivateDnsZoneArmResourceId { get; set; }

[GraphProperty("authConfigActiveDirectoryAuthEnabled")]
public bool? AuthConfigActiveDirectoryAuthEnabled { get; set; }

[GraphProperty("authConfigPasswordAuthEnabled")]
public bool? AuthConfigPasswordAuthEnabled { get; set; }

[GraphProperty("dataEncryptionType")]
public string? DataEncryptionType { get; set; }

[GraphProperty("dataEncryptionKeyUri")]
public string? DataEncryptionKeyUri { get; set; }

[GraphProperty("maintenanceWindowCustom")]
public string? MaintenanceWindowCustom { get; set; }

[GraphProperty("maintenanceWindowStartHour")]
public int? MaintenanceWindowStartHour { get; set; }
```

### 2. Enhanced Crawler Implementation

Update the `PostgreSqlFlexServerCrawler.Crawl` method to populate the new properties:

```csharp
// Add enhanced property extraction to existing try-catch block in PostgreSqlFlexServerCrawler.cs
if (!string.IsNullOrEmpty(serverData.AdministratorLogin))
    postgreSqlNode.AdministratorLogin = serverData.AdministratorLogin;

// Enhanced storage information
if (serverData.Storage != null)
{
    if (serverData.Storage.AutoGrow.HasValue)
        postgreSqlNode.StorageAutoGrow = serverData.Storage.AutoGrow;
    if (serverData.Storage.Tier.HasValue)
        postgreSqlNode.StorageTier = serverData.Storage.Tier.ToString();
    if (serverData.Storage.StorageType.HasValue)
        postgreSqlNode.StorageType = serverData.Storage.StorageType.ToString();
    if (serverData.Storage.Iops.HasValue)
        postgreSqlNode.StorageIops = serverData.Storage.Iops;
    if (serverData.Storage.Throughput.HasValue)
        postgreSqlNode.StorageThroughput = serverData.Storage.Throughput;
}

// Enhanced backup information
if (serverData.Backup != null)
{
    if (serverData.Backup.GeoRedundantBackup.HasValue)
        postgreSqlNode.GeoRedundantBackup = serverData.Backup.GeoRedundantBackup;
    if (serverData.Backup.EarliestRestoreOn.HasValue)
        postgreSqlNode.EarliestRestoreOn = serverData.Backup.EarliestRestoreOn.Value.DateTime;
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
    if (serverData.Network.DelegatedSubnetResourceId != null)
        postgreSqlNode.DelegatedSubnetResourceId = serverData.Network.DelegatedSubnetResourceId.ToString();
    if (serverData.Network.PrivateDnsZoneArmResourceId != null)
        postgreSqlNode.PrivateDnsZoneArmResourceId = serverData.Network.PrivateDnsZoneArmResourceId.ToString();
}

// Authentication configuration
if (serverData.AuthConfig != null)
{
    if (serverData.AuthConfig.ActiveDirectoryAuth.HasValue)
        postgreSqlNode.AuthConfigActiveDirectoryAuthEnabled = serverData.AuthConfig.ActiveDirectoryAuth == PostgreSqlFlexibleServerActiveDirectoryAuthEnum.Enabled;
    if (serverData.AuthConfig.PasswordAuth.HasValue)
        postgreSqlNode.AuthConfigPasswordAuthEnabled = serverData.AuthConfig.PasswordAuth == PostgreSqlFlexibleServerPasswordAuthEnum.Enabled;
}

// Data encryption
if (serverData.DataEncryption != null)
{
    if (serverData.DataEncryption.Type.HasValue)
        postgreSqlNode.DataEncryptionType = serverData.DataEncryption.Type.ToString();
    if (!string.IsNullOrEmpty(serverData.DataEncryption.PrimaryKeyUri))
        postgreSqlNode.DataEncryptionKeyUri = serverData.DataEncryption.PrimaryKeyUri;
}

// Maintenance window
if (serverData.MaintenanceWindow != null)
{
    if (!string.IsNullOrEmpty(serverData.MaintenanceWindow.CustomWindow))
        postgreSqlNode.MaintenanceWindowCustom = serverData.MaintenanceWindow.CustomWindow;
    if (serverData.MaintenanceWindow.StartHour.HasValue)
        postgreSqlNode.MaintenanceWindowStartHour = serverData.MaintenanceWindow.StartHour;
}
```

### 3. Firewall Rules as Child Nodes

Add firewall rule crawling as these represent distinct security entities:

```csharp
// Add after database crawling in PostgreSqlFlexServerCrawler.Crawl method
try
{
    var firewallRuleCollection = server.GetPostgreSqlFlexibleServerFirewallRules();
    await foreach (var rule in firewallRuleCollection.GetAllAsync())
    {
        var ruleData = rule.Data;
        var ruleNode = new ArmResourceNode(
            resourceType: "Microsoft.DBforPostgreSQL/flexibleServers/firewallRules",
            resourceId: rule.Id,
            subscriptionId: armResourceId.SubscriptionId,
            resourceGroupName: armResourceId.ResourceGroupName,
            resourceName: ruleData.Name);

        var properties = ruleNode.GetNodeProperties();
        properties["startIpAddress"] = ruleData.StartIPAddress;
        properties["endIpAddress"] = ruleData.EndIPAddress;

        await _graphDbClient.AddOrUpdateNodeAsync(ruleNode);
        var edge = new ArmResourceEdge(postgreSqlNode.GetNodeId(), ruleNode.GetNodeId(), Constants.Relationships.Contains);
        await _graphDbClient.AddOrUpdateEdgeAsync(edge);
        
        databaseList.Add(ruleNode);
    }
}
catch (Exception ex)
{
    _logger.LogInternalInformation($"Error crawling PostgreSQL firewall rules: {ex.Message}");
}
```

### 4. Cross-Resource Relationships

Add new relationship constants and implement cross-resource connections:

```csharp
// Add to Constants.cs Relationships class
public const string DelegatedTo = "DELEGATED_TO";
public const string UsesDnsZone = "USES_DNS_ZONE";
public const string UsesKeyVault = "USES_KEY_VAULT";
```

```csharp
// Add after server node creation in PostgreSqlFlexServerCrawler.Crawl method
// Create relationships to VNet subnets
if (!string.IsNullOrEmpty(postgreSqlNode.DelegatedSubnetResourceId))
{
    var subnetEdge = new NonCrawledEdge(
        postgreSqlNode.GetNodeId(), 
        postgreSqlNode.DelegatedSubnetResourceId, 
        Constants.Relationships.DelegatedTo);
    await _graphDbClient.AddOrUpdateEdgeAsync(subnetEdge);
}

// Create relationships to Private DNS zones
if (!string.IsNullOrEmpty(postgreSqlNode.PrivateDnsZoneArmResourceId))
{
    var dnsEdge = new NonCrawledEdge(
        postgreSqlNode.GetNodeId(), 
        postgreSqlNode.PrivateDnsZoneArmResourceId, 
        Constants.Relationships.UsesDnsZone);
    await _graphDbClient.AddOrUpdateEdgeAsync(dnsEdge);
}

// Create relationships to Key Vault (if using customer-managed keys)
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
        
        var keyVaultEdge = new NonCrawledEdge(
            postgreSqlNode.GetNodeId(), 
            keyVaultResourceId, 
            Constants.Relationships.UsesKeyVault);
        await _graphDbClient.AddOrUpdateEdgeAsync(keyVaultEdge);
    }
}
```

### 5. Enhanced Database Properties

Update database crawling to include charset and collation:

```csharp
// Replace existing database crawling section in PostgreSqlFlexServerCrawler.Crawl method
var databaseCollection = server.GetPostgreSqlFlexibleServerDatabases();
await foreach (var database in databaseCollection.GetAllAsync())
{
    var databaseData = database.Data;
    var databaseNode = new ArmResourceNode(
        resourceType: "Microsoft.DBforPostgreSQL/flexibleServers/databases",
        resourceId: database.Id,
        subscriptionId: armResourceId.SubscriptionId,
        resourceGroupName: armResourceId.ResourceGroupName,
        resourceName: databaseData.Name);

    // Add database-specific properties
    var properties = databaseNode.GetNodeProperties();
    if (!string.IsNullOrEmpty(databaseData.Charset))
        properties["charset"] = databaseData.Charset;
    if (!string.IsNullOrEmpty(databaseData.Collation))
        properties["collation"] = databaseData.Collation;

    await _graphDbClient.AddOrUpdateNodeAsync(databaseNode);
    var edge = new ArmResourceEdge(postgreSqlNode.GetNodeId(), databaseNode.GetNodeId(), Constants.Relationships.Contains);
    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
    
    databaseList.Add(databaseNode);
}
```

