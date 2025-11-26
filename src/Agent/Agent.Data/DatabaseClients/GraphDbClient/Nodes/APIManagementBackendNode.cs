// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public class APIManagementBackendNode : ArmResourceNode
{
    [GraphProperty("armResourceId")] public string? ArmResourceId { get; set; }
    [GraphProperty("connectedApis")] public string? ConnectedApis { get; set; } // This is what shows up in the UI as "Connected APIs"
    [GraphJsonProperty("apiConnectionInfo")] public List<APIConnectionInfo>? ApiConnectionInfo { get; set; } // Internal use for structured connection details
    [GraphProperty("apimBackendEndpoint")] public string? APIMBackendEndpoint { get; set; }

    public APIManagementBackendNode(string resourceType, string resourceId, string subscriptionId, string resourceGroupName, string resourceName, string? location = null)
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

        ArmResourceId = backendInfo.ArmResourceId ?? string.Empty;
        APIMBackendEndpoint = backendInfo.ResourceUri ?? string.Empty;

        if (backendInfo.Connections != null && backendInfo.Connections.Any())
        {
            ConnectedApis = string.Join(", ", backendInfo.Connections.Select(u => u.Name.Split(':')[0]).Distinct());

            ApiConnectionInfo = backendInfo.Connections.Select(c => new APIConnectionInfo
            {
                Name = c.Name,
                Level = c.Level.ToString()
            }).ToList();
        }
    }
}
