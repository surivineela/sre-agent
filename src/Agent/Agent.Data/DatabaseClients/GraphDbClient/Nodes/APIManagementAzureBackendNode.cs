using System.Collections.Generic;
using System.Linq;
using System.Text;
using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public class APIManagementAzureBackendNode : ArmResourceNode
    {
        [GraphProperty("armResourceId")] public string? ArmResourceId { get; set; }
        [GraphProperty("connectedApis")] public string? ConnectedApis { get; set; } // This is what shows up in the UI as "Connected APIs"
        [GraphProperty("requestUri")] public string? RequestUri { get; set; }

        [GraphJsonProperty("apiConnectionInfo")] public List<APIConnectionInfo>? ApiConnectionInfo { get; set; } // Internal use for structured connection details

        public APIManagementAzureBackendNode(string resourceType, string resourceId, string subscriptionId, string resourceGroupName, string resourceName, string location = null)
            : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
        {
        }

        public class APIConnectionInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Level { get; set; } = string.Empty;
        }

        // Accepts BackendResourceInfo and populates properties, including requestUri and remarks
        public void PopulateAPIMBackendResource(APIManagementNode.BackendResourceInfo backendInfo)
        {
            ArgumentNullException.ThrowIfNull(backendInfo, nameof(backendInfo));
            ArgumentNullException.ThrowIfNullOrEmpty(backendInfo.ArmResourceId, nameof(backendInfo.ArmResourceId));

            ArmResourceId = backendInfo.ArmResourceId;
            RequestUri = backendInfo.ResourceUri ?? string.Empty;

            if (backendInfo.Connections != null && backendInfo.Connections.Any())
            {
                ConnectedApis = string.Join(",", backendInfo.Connections.Select(u => u.Name.Split(':')[0]).Distinct());

                ApiConnectionInfo = backendInfo.Connections.Select(c => new APIConnectionInfo
                {
                    Name = c.Name,
                    Level = c.Level.ToString()
                }).ToList();
            }

        }
    }
}
