using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public class ContainerAppNode : ArmResourceNode
{
    [GraphProperty("provisioningState")]
    public string? ProvisioningState { get; set; }
    [GraphProperty("runningStatus")]
    public string? RunningStatus { get; set; }
    [GraphProperty("workloadProfileName")]
    public string? WorkloadProfileName { get; set; }
    [GraphProperty("external")]
    public bool? External { get; set; }
    [GraphProperty("transport")]
    public string? Transport { get; set; }
    public List<string> HostNames { get; set; }
    public List<Container> Containers { get; set; }
    public List<Container> InitContainers { get; set; }
    [GraphProperty("minReplicas")]
    public int? MinReplicas { get; set; }
    [GraphProperty("maxReplicas")]
    public int? MaxReplicas { get; set; }

    public class Container
    {
        public string Name { get; set; }
        public string Image { get; set; }
        public string? Cpu { get; set; }
        public string? Memory { get; set; }
    }

    public ContainerAppNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        HostNames = new List<string>();
        Containers = new List<Container>();
        InitContainers = new List<Container>();
    }

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

            // Flag if there are custom domains
            bool hasCustomDomains = HostNames.Any(h => !h.EndsWith(".azurecontainerapps.io", StringComparison.OrdinalIgnoreCase));
            if (hasCustomDomains)
            {
                props.Add("hasCustomDomains", true);
            }
        }

        // Add containers
        if (Containers != null && Containers.Count > 0)
        {
            for (int i = 0; i < Containers.Count; i++)
            {
                var container = Containers[i];
                props.Add($"container_{i}_name", container.Name);
                props.Add($"container_{i}_image", container.Image);
                props.Add($"container_{i}_cpu", container.Cpu);
                props.Add($"container_{i}_memory", container.Memory);
            }
        }

        // Add init containers
        if (InitContainers != null && InitContainers.Count > 0)
        {
            for (int i = 0; i < InitContainers.Count; i++)
            {
                var container = InitContainers[i];
                props.Add($"initContainer_{i}_name", container.Name);
                props.Add($"initContainer_{i}_image", container.Image);
                props.Add($"initContainer_{i}_cpu", container.Cpu);
                props.Add($"initContainer_{i}_memory", container.Memory);
            }
        }

        return props;
    }
}
