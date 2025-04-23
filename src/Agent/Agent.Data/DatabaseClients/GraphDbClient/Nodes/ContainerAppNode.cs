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

    [GraphProperty("clientCertificateMode")] public string? ClientCertificateMode { get; set; }
    [GraphProperty("allowInsecure")] public bool? AllowInsecure { get; set; }
    [GraphProperty("corsPolicyJson")] public string? CorsPolicyJson { get; set; }

    [GraphProperty("daprEnabled")]
    public bool? DaprEnabled { get; set; }

    [GraphProperty("daprAppId")]
    public string? DaprAppId { get; set; }

    [GraphProperty("daprAppPort")]
    public int? DaprAppPort { get; set; }

    [GraphProperty("daprLogLevel")]
    public string? DaprLogLevel { get; set; }
    public List<string> HostNames { get; set; } = [];

    public List<Container> Containers { get; set; } = [];

    public List<Container> InitContainers { get; set; } = [];

    [GraphProperty("minReplicas")]
    public int? MinReplicas { get; set; }

    [GraphProperty("maxReplicas")]
    public int? MaxReplicas { get; set; }

    [GraphProperty("environmentId")]
    public string? EnvironmentId { get; set; }

    [GraphProperty("activeRevisionMode")]
    public string? ActiveRevisionMode { get; set; }

    [GraphProperty("targetPort")]
    public int? TargetPort { get; set; }

    public List<TrafficConfiguration> Traffic { get; set; } = [];

    public List<Registry> Registries { get; set; } = [];

    public class Registry
    {
        public string? Server { get; set; }
        public string? Username { get; set; }
        public string? PasswordSecretRef { get; set; }
        public string? Identity { get; set; }
    }

    public class TrafficConfiguration
    {
        public string? RevisionName { get; set; }
        public int Weight { get; set; }
        public string? Label { get; set; }
        public bool LatestRevision { get; set; }
    }

    public class Container
    {
        public string Name { get; set; }
        public string Image { get; set; }
        public string? Cpu { get; set; }
        public string? Memory { get; set; }
    }

    public ContainerAppNode(IDictionary<string, object> properties)
        : base(properties)
    {
        SetNodeProperties(properties);
    }

    public ContainerAppNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        HostNames = [];
        Containers = [];
        InitContainers = [];
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

        if (Traffic != null && Traffic.Count > 0)
        {
            for (int i = 0; i < Traffic.Count; i++)
            {
                var trafficConfig = Traffic[i];
                props.Add($"traffic_{i}_revisionName", trafficConfig.RevisionName);
                props.Add($"traffic_{i}_weight", trafficConfig.Weight);
                props.Add($"traffic_{i}_label", trafficConfig.Label);
                props.Add($"traffic_{i}_latestRevision", trafficConfig.LatestRevision);
            }
        }

        if (Registries != null && Registries.Count > 0)
        {
            for (int i = 0; i < Registries.Count; i++)
            {
                var registry = Registries[i];
                props.Add($"registry_{i}_server", registry.Server);
                props.Add($"registry_{i}_username", registry.Username);
                props.Add($"registry_{i}_passwordSecretRef", registry.PasswordSecretRef);
                props.Add($"registry_{i}_identity", registry.Identity);
            }
        }

        return props;
    }

    private void SetNodeProperties(IDictionary<string, object> properties)
    {
        if (properties.TryGetValue("hostNames", out var hostnames) && hostnames is string)
        {
            HostNames = [.. hostnames.ToString().Split(',')];
        }

        Containers = properties.Keys
            .Where(k => k.StartsWith("container_"))
            .GroupBy(k => k.Substring(0, k.LastIndexOf('_')))
            .Select(g => new Container
            {
                Name = properties.TryGetValue(g.Key + "_name", out var value) ? value.ToString() : null,
                Image = properties.TryGetValue(g.Key + "_image", out var image) ? image.ToString() : null,
                Cpu = properties.TryGetValue(g.Key + "_cpu", out var cpu) ? cpu.ToString() : null,
                Memory = properties.TryGetValue(g.Key + "_memory", out var memory) ? memory.ToString() : null
            })
            .ToList();

        InitContainers = properties.Keys
            .Where(k => k.StartsWith("initContainer_"))
            .GroupBy(k => k.Substring(0, k.LastIndexOf('_')))
            .Select(g => new Container
            {
                Name = properties.TryGetValue(g.Key + "_name", out var value) ? value.ToString() : null,
                Image = properties.TryGetValue(g.Key + "_image", out var image) ? image.ToString() : null,
                Cpu = properties.TryGetValue(g.Key + "_cpu", out var cpu) ? cpu.ToString() : null,
                Memory = properties.TryGetValue(g.Key + "_memory", out var memory) ? memory.ToString() : null
            })
            .ToList();

        Traffic = properties.Keys
            .Where(k => k.StartsWith("traffic_"))
            .GroupBy(k => k.Substring(0, k.LastIndexOf('_')))
            .Select(g => new TrafficConfiguration
            {
                RevisionName = properties.TryGetValue(g.Key + "_revisionName", out var value) ? value.ToString() : null,
                Weight = properties.TryGetValue(g.Key + "_weight", out var weight) ? int.Parse(weight.ToString()) : 0,
                Label = properties.TryGetValue(g.Key + "_label", out var label) ? label.ToString() : null,
                LatestRevision = properties.TryGetValue(g.Key + "_latestRevision", out var latestRevision) && bool.TryParse(latestRevision.ToString(), out var result) && result
            })
            .ToList();

        Registries = properties.Keys
            .Where(k => k.StartsWith("registry_"))
            .GroupBy(k => k.Substring(0, k.LastIndexOf('_')))
            .Select(g => new Registry
            {
                Server = properties.TryGetValue(g.Key + "_server", out var server) ? server.ToString() : null,
                Username = properties.TryGetValue(g.Key + "_username", out var username) ? username.ToString() : null,
                PasswordSecretRef = properties.TryGetValue(g.Key + "_passwordSecretRef", out var password) ? password.ToString() : null,
                Identity = properties.TryGetValue(g.Key + "_identity", out var identity) ? identity.ToString() : null
            })
            .ToList();
    }
}
