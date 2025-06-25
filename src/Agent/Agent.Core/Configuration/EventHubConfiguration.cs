namespace Agent.Core.Configuration
{
    public class EventHubConfiguration
    {
        public string FullyQualifiedNamespace { get; set; } = string.Empty;
        public string EventHubName { get; set; } = string.Empty;
        public string FirstPartyAppClientId { get; set; } = string.Empty;
        public string FirstPartyAppTenantId { get; set; } = string.Empty;
        public string CertificatePath { get; set; } = string.Empty;
    }
}
