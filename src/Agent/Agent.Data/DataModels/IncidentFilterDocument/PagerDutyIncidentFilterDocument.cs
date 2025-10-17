// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

public record PagerDutyIncidentFilterDocument : PagerDutyIncidentFilterDocumentPayload, IIncidentFilterDocument
{
    public PagerDutyIncidentFilterDocument(
    ) : base()
    { }
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public bool IsDeleted { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string DocumentType { get; } = IncidentFilterDocumentUtilities.GetDocumentTypeName(IncidentManagementType.PagerDuty);
    public string PartitionKey => DocumentType;
}

public record PagerDutyIncidentFilterDocumentPayload : IncidentFilterDocumentPayload { }
