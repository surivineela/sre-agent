// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Agent.Data.DatabaseClients.GraphDbClient;

public class AppServiceNode : ArmResourceNode
{
    public string? VnetId { get; set; }
    public string? MinTlsVersion { get; set; }
    public int? NumberOfWorkers { get; set; }
    public bool? AutoHealEnabled { get; set; }
    public bool? AlwaysOn { get; set; }
    public bool? HealthCheckEnabled { get; set; }
    public bool? WebSocketsEnabled { get; set; }

    // Additional properties
    public string? Kind { get; set; }
    public string? StackVersion { get; set; }
    public string? FunctionsHostVersion { get; set; }
    public bool? UsesAppInsights { get; set; }
    public string? PlanType { get; set; } // "ElasticPremium", "FlexConsumption", etc.
    public List<string> HostNames { get; set; } = new List<string>();

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
        string location = null,
        string? vnetId = null,
        string? tlsVersion = null,
        int? numberOfWorkers = null,
        bool? autoHealEnabled = null,
        bool? alwaysOn = null,
        bool? healthCheckEnabled = null,
        string? kind = null,
        string? stackVersion = null,
        string? functionsHostVersion = null,
        bool? usesAppInsights = null,
        DateTime? lastDeploymentTime = null,
        string? planType = null,
        SlotSwapStatus? slotSwapStatus = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        // Initialize base properties
        VnetId = vnetId;
        MinTlsVersion = tlsVersion;
        NumberOfWorkers = numberOfWorkers;
        AutoHealEnabled = autoHealEnabled;
        AlwaysOn = alwaysOn;
        HealthCheckEnabled = healthCheckEnabled;

        // Initialize additional properties
        Kind = kind;
        StackVersion = stackVersion;
        FunctionsHostVersion = functionsHostVersion;
        UsesAppInsights = usesAppInsights;
        PlanType = planType;
        HostNames = new List<string>();
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();

        // Add base properties
        if (VnetId != null)
        {
            props.Add("vnetId", VnetId);
        }

        if (MinTlsVersion != null)
        {
            props.Add("minTlsVersion", MinTlsVersion);
        }

        if (NumberOfWorkers != null)
        {
            props.Add("numberOfWorkers", NumberOfWorkers);
        }

        if (AutoHealEnabled != null)
        {
            props.Add("autoHealEnabled", AutoHealEnabled);
        }

        if (AlwaysOn != null)
        {
            props.Add("alwaysOn", AlwaysOn);
        }

        if (HealthCheckEnabled != null)
        {
            props.Add("healthCheckEnabled", HealthCheckEnabled);
        }

        // Add additional properties
        if (Kind != null)
        {
            props.Add("kind", Kind);
        }

        if (StackVersion != null)
        {
            props.Add("stackVersion", StackVersion);
        }

        if (FunctionsHostVersion != null)
        {
            props.Add("functionsHostVersion", FunctionsHostVersion);
        }

        if (UsesAppInsights != null)
        {
            props.Add("usesAppInsights", UsesAppInsights);
        }

        if (PlanType != null)
        {
            props.Add("planType", PlanType);
        }

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
