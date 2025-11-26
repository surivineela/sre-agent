// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Plugins.Models;

namespace Agent.Plugins.Extensions;

public static class APIManagementNodeExtensions
{
    public static APIManagementDescriptor ToDescriptor(this APIManagementNode apiManagementNode, bool verbose = true)
    {
        if (!verbose)
        {
            return new APIManagementDescriptor(
                ResourceId: apiManagementNode.ResourceId ?? string.Empty,
                Name: apiManagementNode.ResourceName ?? string.Empty,
                Type: apiManagementNode.ResourceType ?? string.Empty,
                Location: apiManagementNode.Location ?? string.Empty,
                ResourceGroup: apiManagementNode.ResourceGroupName ?? string.Empty
            );
        }

        // Build backend info if present
        List<APIManagementBackendDescriptor>? backendDescriptors = null;
        if (apiManagementNode.BackendResourceMap != null && apiManagementNode.BackendResourceMap.Any())
        {
            backendDescriptors = apiManagementNode.BackendResourceMap.Select(kvp =>
                new APIManagementBackendDescriptor(
                    BackendName: kvp.Key,
                    ResourceUri: kvp.Value.ResourceUri,
                    BackendResourceId: kvp.Value.BackendResourceId,
                    ArmResourceId: kvp.Value.ArmResourceId,
                    ConnectedAPIInfo: kvp.Value.Connections?.Select(c => new APIManagementBackendConnectionDescriptor(c.Name, c.Level.ToString())).ToList() ?? new List<APIManagementBackendConnectionDescriptor>())
            ).ToList();
        }

        // Build API info if present
        List<APIManagementApiInfoDescriptor>? apiInfoDescriptors = null;
        if (apiManagementNode.ApiInfoMap != null && apiManagementNode.ApiInfoMap.Any())
        {
            apiInfoDescriptors = apiManagementNode.ApiInfoMap.Select(kvp =>
                new APIManagementApiInfoDescriptor(
                    ApiName: kvp.Key,
                    DisplayName: kvp.Value.DisplayName,
                    Description: kvp.Value.Description,
                    Path: kvp.Value.Path,
                    Operations: kvp.Value.Operations?.Select(op =>
                        new APIManagementApiOperationInfo(
                            DisplayName: op.DisplayName,
                            Method: op.Method,
                            Description: op.Description
                        )).ToList() ?? new List<APIManagementApiOperationInfo>(),
                    Dependencies: kvp.Value.ApiDependencies?.Select(dep =>
                        new APIManagementApiDependencyInfo(
                            BackendResourceIdentifier: dep.BackendResourceIdentifier,
                            BackendResourceType: dep.BackendResourceType
                        )).ToList() ?? new List<APIManagementApiDependencyInfo>()
                )
            ).ToList();
        }

        return new APIManagementDescriptor(
            ResourceId: apiManagementNode.ResourceId ?? string.Empty,
            Name: apiManagementNode.ResourceName ?? string.Empty,
            Type: apiManagementNode.ResourceType ?? string.Empty,
            Location: apiManagementNode.Location ?? string.Empty,
            ResourceGroup: apiManagementNode.ResourceGroupName ?? string.Empty,

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
                : null,

            AppHealthInfo: apiManagementNode.AppHealthInfo,
            Backends: backendDescriptors,
            ApiInfo: apiInfoDescriptors
        );
    }
}
