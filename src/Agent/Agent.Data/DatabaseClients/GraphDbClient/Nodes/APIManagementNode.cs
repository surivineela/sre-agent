using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Data.DatabaseClients.Attributes;
using Azure;
using Azure.ResourceManager.ApiManagement;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public class APIManagementNode : ArmResourceNode
    {
        // General properties
        [GraphProperty("publisherEmail")] public string? PublisherEmail { get; set; }
        [GraphProperty("publisherName")] public string? PublisherName { get; set; }
        [GraphProperty("hostnames")] public string? HostNames { get; set; }
        [GraphProperty("virtualNetworkType")] public string? VirtualNetworkType { get; set; }
        [GraphProperty("publicIPAddresses")] public string? PublicIPAddresses { get; set; }
        [GraphProperty("privateIPAddresses")] public string? PrivateIPAddresses { get; set; }
        [GraphProperty("publicNetworkAccess")] public string? PublicNetworkAccess { get; set; }
        [GraphProperty("gatewayUri")] public string? GatewayUri { get; set; }
        [GraphProperty("gatewayRegionalUri")] public string? GatewayRegionalUri { get; set; }
        [GraphProperty("managementApiUri")] public string? ManagementApiUri { get; set; }
        [GraphProperty("developerPortalUri")] public string? DeveloperPortalUri { get; set; }
        [GraphProperty("developerPortalStatus")] public string? DeveloperPortalStatus { get; set; }
        [GraphProperty("portalUri")] public string? PortalUri { get; set; }
        [GraphProperty("scmUri")] public string? ScmUri { get; set; }
        [GraphProperty("certificates")] public string? Certificates { get; set; }
        [GraphProperty("enableClientCertificate")] public string? EnableClientCertificate { get; set; }
        [GraphProperty("customProperties")] public string? CustomProperties { get; set; }
        [GraphProperty("provisioningState")] public string? ProvisioningState { get; set; }
        [GraphProperty("platformVersion")] public string? PlatformVersion { get; set; }
        [GraphProperty("createdAtUtc")] public string? CreatedAtUtc { get; set; }
        [GraphProperty("natGatewayState")] public string? NatGatewayState { get; set; }
        [GraphProperty("legacyPortalStatus")] public string? LegacyPortalStatus { get; set; }

        // SystemData properties (flattened)
        [GraphProperty("createdOn")] public string? CreatedOn { get; set; }
        [GraphProperty("createdBy")] public string? CreatedBy { get; set; }
        [GraphProperty("createdByType")] public string? CreatedByType { get; set; }
        [GraphProperty("lastModifiedOn")] public string? LastModifiedOn { get; set; }
        [GraphProperty("lastModifiedBy")] public string? LastModifiedBy { get; set; }
        [GraphProperty("lastModifiedByType")] public string? LastModifiedByType { get; set; }

        // Sku Properties (flattened)
        [GraphProperty("skuName")] public string? SkuName { get; set; }
        [GraphProperty("skuCapacity")] public int? SkuCapacity { get; set; }

        // VirtualNetworkConfiguration properties
        [GraphProperty("subnetName")] public string? SubnetName { get; set; }
        [GraphProperty("subnetResourceId")] public string? SubnetResourceId { get; set; }
        [GraphProperty("vnetId")] public Guid? VnetId { get; set; }

        public class BackendConnection
        {
            // API name or API:Operation name
            public string Name { get; set; } = string.Empty;
            public PolicyLevel Level { get; set; } = PolicyLevel.ApiLevel;
        }
        public enum PolicyLevel
        {
            ApiLevel,
            OperationLevel
        }

        public class BackendResourceInfo
        {
            public string? ArmResourceId { get; set; } // Only valid for Azure backends
            public string? ResourceUri { get; set; }
            public List<BackendConnection> Connections { get; set; } = new List<BackendConnection>();
        }

        public Dictionary<string, BackendResourceInfo>? BackendResourceMap { get; set; }

        public APIManagementNode(IDictionary<string, object> properties)
            : base(properties)
        {
            if (properties.TryGetValue("appHealthInfo", out var appHealthInfoObj) && appHealthInfoObj is string appHealthInfoJson)
            {
                AppHealthInfo = JsonSerializer.Deserialize<AppHealthInfo>(appHealthInfoJson);
            }
        }

        public APIManagementNode(
                string resourceType,
                string resourceId,
                string subscriptionId,
                string resourceGroupName,
                string resourceName,
                string location = null)
                : base(resourceType,
                      resourceId,
                      subscriptionId,
                      resourceGroupName,
                      resourceName,
                      location)
        {
        }

        public void PopulateFromApiManagementServiceData(ApiManagementServiceData apimInstance)
        {
            PublisherEmail = apimInstance.PublisherEmail;
            PublisherName = apimInstance.PublisherName;

            // System Data (flattened)
            CreatedOn = apimInstance.SystemData?.CreatedOn?.ToString();
            CreatedBy = apimInstance.SystemData?.CreatedBy?.ToString();
            CreatedByType = apimInstance.SystemData?.CreatedByType?.ToString();
            LastModifiedOn = apimInstance.SystemData?.LastModifiedOn?.ToString();
            LastModifiedBy = apimInstance.SystemData?.LastModifiedBy?.ToString();
            LastModifiedByType = apimInstance.SystemData?.LastModifiedByType?.ToString();

            // SKU (flattened)
            SkuName = apimInstance.Sku?.Name.ToString();
            SkuCapacity = apimInstance.Sku?.Capacity;

            // VNet Config (flattened)
            SubnetName = apimInstance.VirtualNetworkConfiguration?.Subnetname?.ToString();
            SubnetResourceId = apimInstance.VirtualNetworkConfiguration?.SubnetResourceId?.ToString();
            VnetId = apimInstance.VirtualNetworkConfiguration?.VnetId;

            // Hostnames
            var hostnames = apimInstance.HostnameConfigurations?
                .Select(h => h.HostName?.ToString())
                .Where(hn => !string.IsNullOrWhiteSpace(hn))
                .ToList() ?? new List<string>();

            HostNames = JsonSerializer.Serialize(hostnames);

            ResourceType = apimInstance.ResourceType.ToString().ToLower();

            // Networking and General
            VirtualNetworkType = apimInstance.VirtualNetworkType?.ToString();

            PublicIPAddresses = apimInstance.PublicIPAddresses != null && apimInstance.PublicIPAddresses.Count > 0
                ? string.Join(",", apimInstance.PublicIPAddresses.Select(ip => ip.ToString()))
                : string.Empty;

            PrivateIPAddresses = apimInstance.PrivateIPAddresses != null && apimInstance.PrivateIPAddresses.Count > 0
                ? string.Join(",", apimInstance.PrivateIPAddresses.Select(ip => ip.ToString()))
                : string.Empty;

            PublicNetworkAccess = apimInstance.PublicNetworkAccess?.ToString();

            // URIs
            GatewayUri = apimInstance.GatewayUri?.ToString();
            GatewayRegionalUri = apimInstance.GatewayRegionalUri?.ToString();
            ManagementApiUri = apimInstance.ManagementApiUri?.ToString();
            DeveloperPortalUri = apimInstance.DeveloperPortalUri?.ToString();
            DeveloperPortalStatus = apimInstance.DeveloperPortalStatus?.ToString();
            PortalUri = apimInstance.PortalUri?.ToString();
            ScmUri = apimInstance.ScmUri?.ToString();

            // Misc
            Certificates = apimInstance.Certificates != null && apimInstance.Certificates.Count > 0
                ? JsonSerializer.Serialize(apimInstance.Certificates)
                : string.Empty;

            EnableClientCertificate = apimInstance.EnableClientCertificate?.ToString();
            NatGatewayState = apimInstance.NatGatewayState?.ToString();
            LegacyPortalStatus = apimInstance.LegacyPortalStatus?.ToString();

            CustomProperties = apimInstance.CustomProperties.Values != null && apimInstance.CustomProperties.Values.Count > 0
                ? JsonSerializer.Serialize(apimInstance.CustomProperties.Values)
                : string.Empty;

            ProvisioningState = apimInstance.ProvisioningState?.ToString();
            PlatformVersion = apimInstance.PlatformVersion?.ToString();
            CreatedAtUtc = apimInstance.CreatedAtUtc?.ToString("o");
        }

        public void PopulateFromApiManagementServiceResource(ApiManagementServiceResource apimResource)
        {
            // Populate standard properties from Data
            PopulateFromApiManagementServiceData(apimResource.Data);

            // Extract backend connections from policies
            var connectionMap = BuildBackendUsageMapFromPolicies(apimResource);
            var backendResourceMap = BuildBackendResourceMap(apimResource, connectionMap);

            BackendResourceMap = backendResourceMap;
        }

        private Dictionary<string, List<BackendConnection>> BuildBackendUsageMapFromPolicies(ApiManagementServiceResource apimResource)
        {
            var connectionMap = new Dictionary<string, List<BackendConnection>>();
            
            foreach (ApiResource api in apimResource.GetApis().GetAll())
            {
                string apiName = api.Data.Name;
                CollectApiLevelBackendConnections(api, apiName, connectionMap);
                CollectOperationLevelBackendConnections(api, apiName, connectionMap);
            }
            
            return connectionMap;
        }

        private void CollectApiLevelBackendConnections(ApiResource api, string apiName, Dictionary<string, List<BackendConnection>> connectionMap)
        {
            Pageable<ApiPolicyResource> policies = api.GetApiPolicies().GetAll();
            foreach (ApiPolicyResource policy in policies)
            {
                ExtractBackendConnectionsFromPolicy(policy.Data.Value, apiName, PolicyLevel.ApiLevel, connectionMap);
            }
        }

        private void CollectOperationLevelBackendConnections(ApiResource api, string apiName, Dictionary<string, List<BackendConnection>> connectionMap)
        {
            foreach (ApiOperationResource op in api.GetApiOperations().GetAll())
            {
                string operationName = op.Data.Name;

                Pageable<ApiOperationPolicyResource> policies = op.GetApiOperationPolicies().GetAll();
                foreach (ApiOperationPolicyResource policy in policies)
                {
                    ExtractBackendConnectionsFromPolicy(policy.Data.Value, $"{apiName}:{operationName}", PolicyLevel.OperationLevel, connectionMap);
                }
            }
        }

        private void ExtractBackendConnectionsFromPolicy(string policyXml, string name, PolicyLevel level, Dictionary<string, List<BackendConnection>> connectionMap)
        {
            var matches = Regex.Matches(policyXml,
                @"<set-backend-service[^>]*(?:backend-id|base-url)\s*=\s*""([^""]+)""",
                RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                string backendId = match.Groups[1].Value;
                if (!connectionMap.ContainsKey(backendId))
                    connectionMap[backendId] = new List<BackendConnection>();
                connectionMap[backendId].Add(new BackendConnection { Name = name, Level = level });
            }
        }

        private Dictionary<string, BackendResourceInfo> BuildBackendResourceMap(
            ApiManagementServiceResource apimResource, 
            Dictionary<string, List<BackendConnection>> connectionMap)
        {
            var backendResourceMap = new Dictionary<string, BackendResourceInfo>();
            
            foreach (var backend in apimResource.GetApiManagementBackends().GetAll())
            {
                var resourceUri = backend.Data.ResourceUri?.ToString() ?? string.Empty;
                var backendId = backend.Data.Id?.ToString() ?? string.Empty;
                var backendType = backend.Data.ResourceType.ToString();
                var backendName = backend.Data.Name?.ToString() ?? string.Empty;
                var isAzureType = backendType.StartsWith("microsoft.", StringComparison.OrdinalIgnoreCase);

                // Only add to backendResourceMap if used in connectionMap
                if (connectionMap.ContainsKey(backendName))
                {
                    backendResourceMap[backendName] = new BackendResourceInfo
                    {
                        ResourceUri = resourceUri,
                        ArmResourceId = isAzureType ? backendId : null,
                        Connections = connectionMap[backendName]
                    };
                }
            }
            
            return backendResourceMap;
        }
    }
}
