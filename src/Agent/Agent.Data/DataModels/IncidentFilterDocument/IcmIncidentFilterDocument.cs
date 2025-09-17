// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

public record IcmIncidentFilterDocument : IcmIncidentFilterDocumentPayload, IIncidentFilterDocument
{
    public IcmIncidentFilterDocument(
    ) : base()
    { }
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string DocumentType { get; } = IncidentFilterDocumentUtilities.GetDocumentTypeName(IncidentManagementType.Icm);
    public string PartitionKey => DocumentType;
}

public record IcmIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    public IcmIncidentFilterDocumentPayload() : base()
    { }
    public string MonitorId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}
