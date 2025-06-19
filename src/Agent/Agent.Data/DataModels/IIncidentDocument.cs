// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

public interface IIncidentDocument : ICosmosDocument
{
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; set; }
    string ImpactedServiceId { get; set; }
    string ImpactedServiceName { get; set; }
    string Id { get; }
    string Status { get; set; }
    string IncidentType { get; set; }
    string Priority { get; set; }
    string Severity { get; set; }
    string Title { get; set; }
    string Description { get; set; }
    string ExtractedKnowledge { get; set; }
}
