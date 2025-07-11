using System.Collections.Generic;
using System.Linq;
using System.Text;
using Agent.Data.DatabaseClients.Attributes;
using Azure.ResourceManager;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public class APIManagementAzureBackendNode : ArmResourceNode
    {
        [GraphProperty("armResourceId")] public string? ArmResourceId { get; set; }
        [GraphProperty("connectedApis")] public string? ConnectedApis { get; set; }
        [GraphProperty("requestUri")] public string? RequestUri { get; set; }

        public APIManagementAzureBackendNode(string resourceType, string resourceId, string subscriptionId, string resourceGroupName, string resourceName, string location = null)
            : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
        {
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
            }
            else
            {
                ConnectedApis = string.Empty;
            }
        }

        private string BuildRemarks(APIManagementNode.BackendResourceInfo backendInfo)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ResourceUri: {backendInfo.ResourceUri}");
            sb.AppendLine($"ArmResourceId: {backendInfo.ArmResourceId}");
            
            if (backendInfo.Connections != null && backendInfo.Connections.Any())
            {
                sb.AppendLine("Connections:");
                foreach (var connection in backendInfo.Connections)
                {
                    sb.AppendLine($"  - Name: {connection.Name}, Level: {connection.Level}");
                }
            }
            else
            {
                sb.AppendLine("Connections: None");
            }
            
            return sb.ToString().TrimEnd();
        }
    }
}
