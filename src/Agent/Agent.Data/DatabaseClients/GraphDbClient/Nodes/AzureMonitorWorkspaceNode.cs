// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public class AzureMonitorWorkspaceNode : ArmResourceNode
    {
        [GraphProperty("prometheusQueryEndpoint")]
        public string PrometheusQueryEndpoint { get; set; }

        public AzureMonitorWorkspaceNode(string resourceType,
            string resourceId,
            string subscriptionId,
            string resourceGroupName,
            string resourceName,
            string prometheusQueryEndpoint = null,
            string location = null) : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location: location)
        {
            PrometheusQueryEndpoint = prometheusQueryEndpoint;
        }

        public AzureMonitorWorkspaceNode(IDictionary<string, object> properties)
            : base(properties)
        {
        }
    }
}
