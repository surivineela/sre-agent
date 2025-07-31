// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Runtime.Models.ExtendedAgents;

namespace Agent.Web.Models.ExtendedAgents.Response;

public record ExtendedAgentErrorResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("error_code")] string ErrorCode,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("details")] ExtendedAgentErrorDetails? Details
)
{
    public ExtendedAgentErrorResponse() : this("error", "", "", DateTime.UtcNow, null) { }

    public static ExtendedAgentErrorResponse FromRuntime(ExtendedAgentError runtimeModel)
    {
        return new ExtendedAgentErrorResponse(
            Status: runtimeModel.Status,
            ErrorCode: runtimeModel.ErrorCode,
            Message: runtimeModel.Message,
            Timestamp: runtimeModel.Timestamp,
            Details: runtimeModel.Details != null ? ExtendedAgentErrorDetails.FromRuntime(runtimeModel.Details) : null
        );
    }
}

public record ExtendedAgentErrorDetails(
    [property: JsonPropertyName("errors")] List<ExtendedAgentErrorField>? Errors
)
{
    public static ExtendedAgentErrorDetails FromRuntime(ErrorDetails runtimeModel)
    {
        return new ExtendedAgentErrorDetails(
            Errors: runtimeModel.Errors?.Select(ExtendedAgentErrorField.FromRuntimeErrorField).ToList()
        );
    }
}

public record ExtendedAgentErrorField(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("message")] string Message
)
{
    public static ExtendedAgentErrorField FromRuntime(ExtendedAgentErrorField runtimeModel)
    {
        return new ExtendedAgentErrorField(
            Field: runtimeModel.Field,
            Message: runtimeModel.Message
        );
    }

    public static ExtendedAgentErrorField FromRuntimeErrorField(ErrorField runtimeModel)
    {
        return new ExtendedAgentErrorField(
            Field: runtimeModel.Field,
            Message: runtimeModel.Message
        );
    }
}
