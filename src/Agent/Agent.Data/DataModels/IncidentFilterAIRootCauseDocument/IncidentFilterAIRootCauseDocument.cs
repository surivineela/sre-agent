using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

/// <summary>
/// Represents a root cause category with its description
/// </summary>
public record RootCauseCategory
{
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public RootCauseCategory() { }

    public RootCauseCategory(string category, string description)
    {
        Category = category;
        Description = description;
    }
}

/// <summary>
/// Response model for AI root cause analysis
/// </summary>
public record AIRootCauseResponse
{
    public string RootCause { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public interface IIncidentFilterAIRootCauseDocument : ICosmosDocument
{
    public string FilterId { get; set; }
    public List<RootCauseCategory> RootCauses { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record IncidentFilterAIRootCausePayload
{
    public string Id { get; init; } = string.Empty;
    public string FilterId { get; set; } = string.Empty;
    public List<RootCauseCategory> RootCauses { get; set; } = new List<RootCauseCategory>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public IncidentFilterAIRootCausePayload()
    {
    }
}


public class IncidentFilterAIRootCauseUtilities
{
    public static string GetDocumentType(IncidentManagementType? type)
    {
        return $"IncidentFilterAIRootCause{type?.ToString()}";
    }
}
