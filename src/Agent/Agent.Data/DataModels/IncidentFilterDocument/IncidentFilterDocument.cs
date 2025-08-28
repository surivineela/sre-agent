// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

public interface IIncidentFilterDocument : ICosmosDocument
{
    bool IsDeleted { get; init; } // Flag to indicate if the filter is deleted. This is used for soft delete.
    DateTime UpdatedAt { get; init; }
    public bool IsEnabled { get; init; }
    public string HandlingAgent { get; init; }
}

public record IncidentFilterDocumentPayload
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ImpactedService { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string IncidentType { get; init; } = string.Empty;
    public string AlertId { get; init; } = string.Empty;
    public string TitleContains { get; init; } = string.Empty;
    public string AgentMode { get; init; } = string.Empty;
    public string HandlingAgent { get; init; } = string.Empty;
    public string OwningTeamId { get; init; } = string.Empty;
    public int MaxAutomatedInvestigationAttempts { get; init; } = 3;

    public IncidentFilterDocumentPayload()
    {
    }
}

public class IncidentFilterDocumentUtilities
{
    public static string GetDocumentTypeName(IncidentManagementType? type)
    {
        return $"IncidentFilter{type?.ToString()}";
    }
}
