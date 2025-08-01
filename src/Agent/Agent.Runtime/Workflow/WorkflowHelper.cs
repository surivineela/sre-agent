// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Runtime.Reasoning;

namespace Agent.Runtime.Workflow;

/// <summary>
/// Helper methods for workflow operations.
/// </summary>
public static class WorkflowHelper
{
    /// <summary>
    /// Merges parameters from multiple workflow activity outputs.
    /// </summary>
    public static Dictionary<string, string> MergeParameters(
        Dictionary<string, string> baseParameters,
        params WorkflowActivityAgentOutput[] outputs)
    {
        var merged = new Dictionary<string, string>(baseParameters);
        
        foreach (var output in outputs)
        {
            foreach (var kvp in output.ParsedParameters)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }
        
        return merged;
    }

    /// <summary>
    /// Converts a dictionary to JSON string safely.
    /// </summary>
    public static string DictionaryToJsonString(Dictionary<string, string> dictionary)
    {
        try
        {
            return JsonSerializer.Serialize(dictionary);
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// Parses JSON string to dictionary safely.
    /// </summary>
    public static Dictionary<string, string> JsonStringToDictionary(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Validates workflow activity state string.
    /// </summary>
    public static bool IsValidWorkflowState(string state)
    {
        return Enum.TryParse<WorkflowActivityState>(state, ignoreCase: true, out _);
    }

    /// <summary>
    /// Gets the WorkflowActivityState enum from string.
    /// </summary>
    public static WorkflowActivityState? ParseWorkflowState(string state)
    {
        if (Enum.TryParse<WorkflowActivityState>(state, ignoreCase: true, out var result))
            return result;
        
        return null;
    }

    /// <summary>
    /// Creates a new workflow execution context.
    /// </summary>
    public static WorkflowExecutionContext CreateExecutionContext(
        string workflowId,
        string? orchestratorAgent = null,
        int maxAgentCount = 10)
    {
        return new WorkflowExecutionContext
        {
            WorkflowId = workflowId,
            StepNumber = 1,
            ExecutedAgentCount = 0,
            MaxAgentCount = maxAgentCount,
            OrchestratorAgent = orchestratorAgent,
            StartedAt = DateTime.UtcNow,
            AccumulatedParameters = new WorkflowParameters()
        };
    }
}
