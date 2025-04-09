// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient;

public class AppServiceNode : ArmResourceNode
{
    [GraphProperty("vnetId")]
    public string? VnetId { get; set; }

    [GraphProperty("minTlsVersion")]
    public string? MinTlsVersion { get; set; }

    [GraphProperty("numberOfWorkers")]
    public int? NumberOfWorkers { get; set; }

    [GraphProperty("autoHealEnabled")]
    public bool? AutoHealEnabled { get; set; }

    [GraphProperty("alwaysOn")]
    public bool? AlwaysOn { get; set; }

    [GraphProperty("healthCheckEnabled")]
    public bool? HealthCheckEnabled { get; set; }

    [GraphProperty("webSocketsEnabled")]
    public bool? WebSocketsEnabled { get; set; }

    // Additional properties

    [GraphProperty("kind")]
    public string? Kind { get; set; }

    [GraphProperty("stackVersion")]
    public string? StackVersion { get; set; }

    [GraphProperty("functionsHostVersion")]
    public string? FunctionsHostVersion { get; set; }

    [GraphProperty("usesAppInsights")]
    public bool? UsesAppInsights { get; set; }

    [GraphProperty("planType")]
    public string? PlanType { get; set; } // "ElasticPremium", "FlexConsumption", etc.

    public List<string> HostNames { get; set; } = new List<string>();

    [GraphProperty("workerRuntime")]
    public string? WorkerRuntime { get; set; } // Function worker runtime

    public class SlotSwapStatus
    {
        public DateTime TimestampUtc { get; set; }
        public string SourceSlotName { get; set; }
        public string DestinationSlotName { get; set; }
    }

    public AppServiceNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location) { }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();

        // Add hostnames
        if (HostNames != null && HostNames.Count > 0)
        {
            props.Add("hostNames", string.Join(",", HostNames));

            // Also add individual hostnames for easier querying
            for (int i = 0; i < HostNames.Count; i++)
            {
                // Add all hostnames as separate properties
                props.Add($"hostname_{i}", HostNames[i]);
            }

            // Flag if there are custom domains (non-azurewebsites.net domains)
            bool hasCustomDomains = HostNames.Any(h => !h.Contains(".azurewebsites.net", StringComparison.OrdinalIgnoreCase));
            if (hasCustomDomains)
            {
                props.Add("hasCustomDomains", true);
            }
        }

        return props;
    }
}
