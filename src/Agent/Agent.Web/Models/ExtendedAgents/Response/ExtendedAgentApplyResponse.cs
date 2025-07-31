// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Web.Models.ExtendedAgents.Response;

public record ExtendedAgentApplyResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("operation_id")] string OperationId,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("details")] ExtendedAgentApplyResponseDetails? Details
)
{
    public static ExtendedAgentApplyResponse FromRuntime(Runtime.Models.ExtendedAgents.ExtendedAgentApply runtimeModel)
    {
        return new ExtendedAgentApplyResponse(
            Status: runtimeModel.Status.ToString(),
            Message: runtimeModel.Message,
            OperationId: runtimeModel.OperationId,
            Timestamp: runtimeModel.Timestamp,
            Details: runtimeModel.Details != null ? ExtendedAgentApplyResponseDetails.FromRuntime(runtimeModel.Details) : null
        );
    }
}

public record ExtendedAgentApplyResponseDetails(
    [property: JsonPropertyName("agent_name")] string? AgentName,
    [property: JsonPropertyName("tools_count")] int ToolsCount,
    [property: JsonPropertyName("estimated_completion_time")] DateTime? EstimatedCompletionTime
)
{
    public static ExtendedAgentApplyResponseDetails FromRuntime(Runtime.Models.ExtendedAgents.ExtendedAgentApplyDetails runtimeModel)
    {
        return new ExtendedAgentApplyResponseDetails(
            AgentName: runtimeModel.AgentName,
            ToolsCount: runtimeModel.ToolsCount,
            EstimatedCompletionTime: runtimeModel.EstimatedCompletionTime
        );
    }
}
