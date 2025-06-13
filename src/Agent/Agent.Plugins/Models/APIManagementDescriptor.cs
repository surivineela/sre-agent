namespace Agent.Plugins.Models
{
    public sealed record APIManagementDescriptor(
        string ResourceId,
        string Name,
        string Type,
        string Location,
        string ResourceGroup);

    public record APIMActivityLogEntry(
            string Timestamp,
            string Operation,
            string Event,
            string Status,
            string URI,
            string Caller
        );
}
