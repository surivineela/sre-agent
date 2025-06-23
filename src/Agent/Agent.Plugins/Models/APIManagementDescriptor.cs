namespace Agent.Plugins.Models
{
    public sealed record APIManagementDescriptor(
        string ResourceId,
        string Name,
        string Type,
        string Location,
        string ResourceGroup,
        string? PublisherEmail = null,
        string? PublisherName = null,
        string? PublicIPAddresses = null,
        string? PrivateIPAddresses = null,
        string? VirtualNetworkType = null,
        string? PublicNetworkAccess = null,
        string? GatewayUri = null,
        string? GatewayRegionalUri = null,
        string? ManagementApiUri = null,
        string? DeveloperPortalUri = null,
        string? DeveloperPortalStatus = null,
        string? PortalUri = null,
        string? ScmUri = null,
        string? Certificates = null,
        string? EnableClientCertificate = null,
        string? CustomProperties = null,
        string? ProvisioningState = null,
        string? PlatformVersion = null,
        string? CreatedAtUtc = null,
        string? NatGatewayState = null,
        string? LegacyPortalStatus = null,
        string? HostNames = null,

        SkuDescriptor? SkuData = null,
        VNetConfigDescriptor? VNetConfig = null,
        SystemDataDescriptor? SystemData = null
    );

    public sealed record SystemDataDescriptor(
        string? CreatedAt,
        string? CreatedBy,
        string? CreatedByType,
        string? LastModifiedAt,
        string? LastModifiedBy,
        string? LastModifiedByType
    );

    public sealed record SkuDescriptor(
        string? SkuName,
        int? SkuCapacity
    );

    public sealed record VNetConfigDescriptor(
        string? SubnetName,
        string? SubnetResourceId,
        Guid? VnetId
    );
    public record APIMActivityLogEntry(
            string Timestamp,
            string Operation,
            string Event,
            string Status,
            string URI,
            string Caller
        );
}
