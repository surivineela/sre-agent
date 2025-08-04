// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public sealed record IncidentInvestigationTaskInputData : AgentTaskInputData
{
    /// <summary>
    /// Detailed description of the incident being investigated.
    /// </summary>
    public required string IncidentDescription { get; set; }
}
