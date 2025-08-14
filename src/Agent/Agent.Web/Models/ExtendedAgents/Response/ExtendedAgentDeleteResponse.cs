// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Web.Models.ExtendedAgents.Response;

public record ExtendedAgentDeleteResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("resource_name")] string ResourceName,
    [property: JsonPropertyName("resource_type")] string ResourceType,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp
)
{
    public ExtendedAgentDeleteResponse() : this("success", "", "", "", DateTime.UtcNow) { }
}

public record ExtendedAgentConflictResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("error_code")] string ErrorCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("resource_name")] string ResourceName,
    [property: JsonPropertyName("resource_type")] string ResourceType,
    [property: JsonPropertyName("conflict_reason")] string ConflictReason,
    [property: JsonPropertyName("dependent_agents")] List<string> DependentAgents,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp
)
{
    public ExtendedAgentConflictResponse() : this("conflict", "", "", "", "", "", [], DateTime.UtcNow) { }
}
