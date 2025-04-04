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
        bool? healthCheckEnabled = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        VnetId = vnetId;
        MinTlsVersion = tlsVersion;
        NumberOfWorkers = numberOfWorkers;
        AutoHealEnabled = autoHealEnabled;
        AlwaysOn = alwaysOn;
        HealthCheckEnabled = healthCheckEnabled;
    }
    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();
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
        return props;
    }
}

