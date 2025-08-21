using Agent.Core.Configuration;

namespace Agent.Data.DataModels;
public record ServiceNowIncidentFilterDocument : ServiceNowIncidentFilterDocumentPayload, IIncidentFilterDocument
{
    public ServiceNowIncidentFilterDocument(
    ) : base()
    { }

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public bool IsDeleted { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsEnabled { get; init; } = true;

    public string DocumentType { get; } = IncidentFilterDocumentUtilities.GetDocumentTypeName(IncidentManagementType.ServiceNow);

    public string PartitionKey => DocumentType;
}
public record ServiceNowIncidentFilterDocumentPayload : IncidentFilterDocumentPayload {
    public ServiceNowIncidentFilterDocumentPayload() : base() { }
}
