// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

public interface IIncidentDocument : ICosmosDocument
{
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; set; }
    string Title { get; set; }
    string Description { get; set; }
    string ExtractedKnowledge { get; set; }
}
