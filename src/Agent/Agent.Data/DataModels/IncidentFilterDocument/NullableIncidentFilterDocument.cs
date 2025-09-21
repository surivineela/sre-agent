// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

public record NullableIncidentFilterDocument : IncidentFilterDocumentPayload, IIncidentFilterDocument
{
    public NullableIncidentFilterDocument() : base()
    { }

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string DocumentType { get; } = IncidentFilterDocumentUtilities.GetDocumentTypeName(IncidentManagementType.None);
    public string PartitionKey => DocumentType;
}
