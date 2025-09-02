using Agent.Core.Configuration;

namespace Agent.Data.DataModels;
public record IcmIncidentFilterDocument : IcmIncidentFilterDocumentPayload, IIncidentFilterDocument
{

    public IcmIncidentFilterDocument(
    ) : base()
    {}

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsEnabled { get; init; } = true;

    public string DocumentType { get; } = IncidentFilterDocumentUtilities.GetDocumentTypeName(IncidentManagementType.Icm);

    public string PartitionKey => DocumentType;
}

public record IcmIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    public IcmIncidentFilterDocumentPayload():base()
    {}
    public string MonitorId { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
}
