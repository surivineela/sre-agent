using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;

namespace Agent.Data.DataModels;


public interface IIncidentFilterAIRootCauseDocument: ICosmosDocument
{
    public string FilterId { get; set; }
    public List<string> RootCauses { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record IncidentFilterAIRootCausePayload
{
    public string Id { get; init; } = string.Empty;
    public string FilterId { get; set; } = string.Empty;
    public List<string> RootCauses { get; set; } = new List<string>();
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
