using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins.Models;

namespace Agent.Plugins.Extensions
{
    public static class APIManagementNodeExtensions
    {
        public static APIManagementDescriptor ToDescriptor(this APIManagementNode apiManagementNode, bool verbose = true)

        {
            if (!verbose)
            {
                return new APIManagementDescriptor(
                    ResourceId: apiManagementNode.ResourceId,
                    Name: apiManagementNode.ResourceName,
                    Type: apiManagementNode.ResourceType,
                    Location: apiManagementNode.Location,
                    ResourceGroup: apiManagementNode.ResourceGroupName
                );
            }

            return new APIManagementDescriptor(
                ResourceId: apiManagementNode.ResourceId,
                Name: apiManagementNode.ResourceName,
                Type: apiManagementNode.ResourceType,
                Location: apiManagementNode.Location,
                ResourceGroup: apiManagementNode.ResourceGroupName,

                PublisherEmail: apiManagementNode.PublisherEmail,
                PublisherName: apiManagementNode.PublisherName,

                SkuData: apiManagementNode.SkuName != null || apiManagementNode.SkuCapacity != null
                    ? new SkuDescriptor(
                        SkuName: apiManagementNode.SkuName,
                        SkuCapacity: apiManagementNode.SkuCapacity
                    )
                    : null,

                VNetConfig: apiManagementNode.SubnetName != null || apiManagementNode.SubnetResourceId != null || apiManagementNode.VnetId != null
                    ? new VNetConfigDescriptor(
                        SubnetName: apiManagementNode.SubnetName,
                        SubnetResourceId: apiManagementNode.SubnetResourceId,
                        VnetId: apiManagementNode.VnetId
                    )
                    : null,

                HostNames: apiManagementNode.HostNames,

                PublicIPAddresses: apiManagementNode.PublicIPAddresses,
                PrivateIPAddresses: apiManagementNode.PrivateIPAddresses,
                VirtualNetworkType: apiManagementNode.VirtualNetworkType,
                PublicNetworkAccess: apiManagementNode.PublicNetworkAccess,

                GatewayUri: apiManagementNode.GatewayUri,
                GatewayRegionalUri: apiManagementNode.GatewayRegionalUri,
                ManagementApiUri: apiManagementNode.ManagementApiUri,
                DeveloperPortalUri: apiManagementNode.DeveloperPortalUri,
                DeveloperPortalStatus: apiManagementNode.DeveloperPortalStatus,
                PortalUri: apiManagementNode.PortalUri,
                ScmUri: apiManagementNode.ScmUri,

                Certificates: apiManagementNode.Certificates,
                EnableClientCertificate: apiManagementNode.EnableClientCertificate,
                CustomProperties: apiManagementNode.CustomProperties,
                LegacyPortalStatus: apiManagementNode.LegacyPortalStatus,
                NatGatewayState: apiManagementNode.NatGatewayState,

                ProvisioningState: apiManagementNode.ProvisioningState,
                PlatformVersion: apiManagementNode.PlatformVersion,
                CreatedAtUtc: apiManagementNode.CreatedAtUtc,

                SystemData: apiManagementNode.CreatedOn != null || apiManagementNode.LastModifiedOn != null
                    ? new SystemDataDescriptor(
                        CreatedOn: apiManagementNode.CreatedOn,
                        CreatedBy: apiManagementNode.CreatedBy,
                        CreatedByType: apiManagementNode.CreatedByType,
                        LastModifiedOn: apiManagementNode.LastModifiedOn,
                        LastModifiedBy: apiManagementNode.LastModifiedBy,
                        LastModifiedByType: apiManagementNode.LastModifiedByType
                    )
                    : null
            );
        }
    }
}
