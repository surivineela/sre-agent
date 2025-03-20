namespace Agent.Plugins.Models
{
    public sealed record ContainerAppDescriptor(
        string ResourceId,
        string Name,
        string Kind,
        string Location,
        string WorkloadProfile,
        string State,
        string ResourceGroup,
        string Environment = "N/A",
        bool IsIngressEnabled = false,
        IReadOnlyList<RevisionInfo> Revisions = null);
    
    public sealed record RevisionInfo(
        string RevisionName,
        bool IsActive,
        int TrafficWeight);
}
