// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

public record AzMonitorIncidentFilterDocument : AzMonitorIncidentFilterDocumentPayload, IIncidentFilterDocument
{
    public AzMonitorIncidentFilterDocument() : base()
    { }
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public bool IsDeleted { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string DocumentType { get; } = IncidentFilterDocumentUtilities.GetDocumentTypeName(IncidentManagementType.AzMonitor);
    public string PartitionKey => DocumentType;
}

public record AzMonitorIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    public AzMonitorIncidentFilterDocumentPayload() : base()
    { }
    public string TargetResourceType { get; set; } = string.Empty;
    public string TargetResource { get; set; } = string.Empty;
}
