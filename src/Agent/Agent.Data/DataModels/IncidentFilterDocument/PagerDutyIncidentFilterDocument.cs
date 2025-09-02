using Agent.Core.Configuration;

namespace Agent.Data.DataModels;
public record PagerDutyIncidentFilterDocument: PagerDutyIncidentFilterDocumentPayload, IIncidentFilterDocument
{
    public PagerDutyIncidentFilterDocument(
    ) : base()
    { }
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public bool IsDeleted { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsEnabled { get; init; } = true;

    public string DocumentType { get; } = IncidentFilterDocumentUtilities.GetDocumentTypeName(IncidentManagementType.PagerDuty);

    public string PartitionKey => DocumentType;
}
public record PagerDutyIncidentFilterDocumentPayload : IncidentFilterDocumentPayload { }
