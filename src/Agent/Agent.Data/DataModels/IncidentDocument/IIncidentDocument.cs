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
    string Status { get; }
    string IncidentType { get; }
    string Priority { get; }
    string Title { get; set; }
    string Description { get; set; }
    string ExtractedKnowledge { get; set; }
    string AIRootCause { get; set; }
    string GeneralSummary { get; set; }
}
