// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient;

public class PostgreSqlFlexServerNode : ArmResourceNode
{
    [GraphProperty("serverVersion")]
    public string? ServerVersion { get; set; }

    [GraphProperty("skuTier")]
    public string? SkuTier { get; set; }

    [GraphProperty("skuName")]
    public string? SkuName { get; set; }

    [GraphProperty("storageSize")]
    public int? StorageSize { get; set; }

    [GraphProperty("backupRetentionDays")]
    public int? BackupRetentionDays { get; set; }

    [GraphProperty("highAvailabilityEnabled")]
    public bool? HighAvailabilityEnabled { get; set; }

    [GraphProperty("publicNetworkAccess")]
    public string? PublicNetworkAccess { get; set; }

    [GraphProperty("sslEnforcement")]
    public string? SslEnforcement { get; set; }

    [GraphProperty("minimalTlsVersion")]
    public string? MinimalTlsVersion { get; set; }

    [GraphProperty("provisioningState")]
    public string? ProvisioningState { get; set; }

    [GraphProperty("fullyQualifiedDomainName")]
    public string? FullyQualifiedDomainName { get; set; }

    [GraphProperty("availabilityZone")]
    public string? AvailabilityZone { get; set; }

    [GraphProperty("maintenanceWindow")]
    public string? MaintenanceWindow { get; set; }

    // Enhanced server properties
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

    public PostgreSqlFlexServerNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location) { }
}
