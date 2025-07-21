// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Newtonsoft.Json;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document model for ThreadEvaluateResult
/// </summary>
public record ThreadEvaluateResultDocument(
    string Id,
    string ThreadId,
    string ThreadTitle,
    TimeSpan Duration,
    int UserInteractionCount,
    int ToolCallCount,
    double ToolCallSuccessRate,
    int AzCliCallCount,
    double AzCliSuccessRate,
    int KubectlCallCount,
    double KubectlSuccessRate,
    string EvaluationSummary,
    double SATScore,
    string Category,
    int Resolved,
    int Satisfied,
    int Automatic,
    int Smooth,
    int Concise,
    int Adherence,
    string Priority,
    string PriorityReason,
    DateTime EvaluatedTimestamp,
    AgentTypeEnum AgentType
) : ICosmosDocument
{
    public string DocumentType => "ThreadEvaluationResult";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    // Conversion from domain model
    public static ThreadEvaluateResultDocument FromDomainModel(ThreadEvaluateResult evaluateResult) =>
        new ThreadEvaluateResultDocument(
            evaluateResult.Id.ToString(),
            evaluateResult.ThreadId.ToString(),
            evaluateResult.ThreadTitle,
            evaluateResult.Duration,
            evaluateResult.UserInteractionCount,
            evaluateResult.ToolCallCount,
            evaluateResult.ToolCallSuccessRate,
            evaluateResult.AzCliCallCount,
            evaluateResult.AzCliSuccessRate,
            evaluateResult.KubectlCallCount,
            evaluateResult.KubectlSuccessRate,
            evaluateResult.EvaluationSummary,
            evaluateResult.SATScore,
            evaluateResult.Category,
            evaluateResult.Resolved,
            evaluateResult.Satisfied,
            evaluateResult.Automatic,
            evaluateResult.Smooth,
            evaluateResult.Concise,
            evaluateResult.Adherence,
            evaluateResult.Priority,
            evaluateResult.PriorityReason,
            evaluateResult.EvaluatedTimestamp,
            evaluateResult.AgentType
        );

    // Conversion to domain model
    public ThreadEvaluateResult ToDomainModel() =>
        new ThreadEvaluateResult(
            Guid.Parse(Id),
            Guid.Parse(ThreadId),
            ThreadTitle,
            Duration,
            UserInteractionCount,
            ToolCallCount,
            ToolCallSuccessRate,
            AzCliCallCount,
            AzCliSuccessRate,
            KubectlCallCount,
            KubectlSuccessRate,
            EvaluationSummary,
            SATScore,
            Category,
            Resolved,
            Satisfied,
            Automatic,
            Smooth,
            Concise,
            Adherence,
            Priority,
            PriorityReason,
            EvaluatedTimestamp,
            AgentType
        );
}

/// <summary>
/// Custom JSON converter to handle both boolean and integer values for SAT score fields
/// </summary>
public class BoolToIntConverter : JsonConverter<int>
{
    public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer)
    {
        writer.WriteValue(value);
    }

    public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.Integer:
                return Convert.ToInt32(reader.Value);
            case JsonToken.Boolean:
                return (bool)reader.Value! ? 1 : 0;
            case JsonToken.String:
                var stringValue = reader.Value!.ToString()!;
                if (bool.TryParse(stringValue, out bool boolResult))
                {
                    return boolResult ? 1 : 0;
                }
                if (int.TryParse(stringValue, out int intResult))
                {
                    return intResult;
                }
                break;
        }

        // Default to 0 if we can't parse
        return 0;
    }
}
