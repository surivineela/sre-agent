// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

public interface IIncidentFilterDocument : ICosmosDocument
{
    // Provide default implementation for static abstract member from ICosmosDocument
    // This allows IIncidentFilterDocument to be used as a generic type argument
    static string ICosmosDocument.ContainerName => AgentDataConfiguration.ThreadContainerName;

    bool IsDeleted { get; set; } // Flag to indicate if the filter is deleted. This is used for soft delete.
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
    public bool IsEnabled { get; set; }
    public string HandlingAgent { get; set; }
}

public record IncidentFilterDocumentPayload
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImpactedService { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string AlertId { get; set; } = string.Empty;
    public string TitleContains { get; set; } = string.Empty;
    public string AgentMode { get; set; } = string.Empty;
    public string HandlingAgent { get; set; } = string.Empty;
    public string OwningTeamId { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of automated investigation attempts for recurring alerts before requesting user input.
    /// When an alert fires repeatedly and automated RCA fails to find a definitive root cause,
    /// the agent will ask the user for additional context after this many attempts.
    /// </summary>
    public int MaxAutomatedInvestigationAttempts { get; set; } = 3;
    public bool DeepInvestigationEnabled { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;



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

    public static IncidentManagementType? GetIncidentManagementTypeFromDocumentType(string documentType)
    {
        if (documentType.StartsWith("IncidentFilter"))
        {
            var typeString = documentType.Substring("IncidentFilter".Length);
            if (Enum.TryParse<IncidentManagementType>(typeString, out var type))
            {
                return type;
            }
        }
        return null;
    }
}
