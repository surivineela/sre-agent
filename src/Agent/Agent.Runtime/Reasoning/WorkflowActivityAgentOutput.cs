// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using Agent.Runtime.Workflow;
using Agent.Framework;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Complete structured output for Activity-type agents in workflow execution.
/// Implements both IAgentOutput (for compatibility) and IWorkflowActivityOutput (for workflow-specific properties).
/// </summary>
public sealed class WorkflowActivityAgentOutput : IAgentOutput, IWorkflowActivityOutput
{
    // === IAgentOutput Properties (for compatibility) ===

    [StreamableContent]
    [Description(
        """
        Presented to the user. Use this space to keep the user posted of your activity. It may be summary of your plan, tool results, or findings from your analysis. It should be concise, to the point, and relevant to the task you were assigned.
        This should focus on the specific analysis or task you performed within the workflow.
        """)]
    public required string NotifyUserMessage { get; set; }

    [Description(
        """
        Use this space to think step-by-step about the problem you're solving, formulate a plan, your current trajectory, reflecting on tool call outputs, and deciding next steps.
        This is for internal reasoning and will not be shown to the user.
        """)]
    public required string ReasoningScratchPad { get; set; }

    [Description(
        """
        Current state of execution. Your internal evaluation of where you're at. The allowed values are:
        - Processing: Still analyzing and processing the assigned task. More tool calls may be needed.
        - Completed: Task analysis completed successfully. Ready to proceed to next steps.
        - Failed: Unable to complete the assigned task due to errors or insufficient data.
        - RequiresInput: Additional input or clarification needed to complete the task.
        """)]
    public required string State { get; set; }

    [Description(
        """
        1-2 sentence explanation of why you are in that state.
        If the state is Failed, explain what went wrong and what prevented task completion.
        If the state is RequiresInput, specify what additional information is needed.
        If the state is Completed, briefly summarize what was accomplished.
        """)]
    public required string StateExplanation { get; set; }

    // === IWorkflowActivityOutput Properties (LLM generates these) ===

    [Description(
        """
        List of next agent names that should be executed based on your analysis results.
        This corresponds to the conditions defined in next_agent_mappings in your agent configuration.
        Leave empty if no further agents need to be executed in this workflow path.
        """)]
    public required List<string> NextSteps { get; set; } = [];

    [Description(
        """
        Detailed analysis results from executing your assigned task.
        This should contain the core findings, insights, or data that was discovered during your analysis.
        This information will be used by the orchestrator for final summarization and by subsequent agents.
        """)]
    public required string Analysis { get; set; }

    [Description(
        """
        Additional parameters discovered or refined during task execution in JSON format.
        These parameters will be merged with existing workflow parameters and made available to subsequent agents.
        Use JSON object format: {"key1": "value1", "key2": "value2"}
        Leave empty string if no new parameters were discovered.
        """)]
    public required string Parameters { get; set; } = "{}";

    // === Additional Properties (set programmatically) ===

    /// <summary>
    /// Workflow parameters parsed from the Parameters JSON string.
    /// This is populated programmatically after LLM generates the output.
    /// </summary>
    public Dictionary<string, string> ParsedParameters { get; private set; } = new();

    /// <summary>
    /// Workflow execution context and state information.
    /// Set programmatically by the workflow orchestrator.
    /// </summary>
    public WorkflowExecutionContext? ExecutionContext { get; set; }

    /// <summary>
    /// Timestamp when this output was generated.
    /// Set programmatically when the output is created.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Agent name that generated this output.
    /// Set programmatically by the framework.
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Parses the Parameters JSON string into ParsedParameters dictionary.
    /// Should be called after LLM generates the output.
    /// </summary>
    public void ParseParameters()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Parameters) || Parameters == "{}")
            {
                ParsedParameters = new Dictionary<string, string>();
                return;
            }

            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(Parameters);
            ParsedParameters = parsed ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            // If JSON parsing fails, treat as empty parameters
            ParsedParameters = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Validates that all required workflow properties are properly set.
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Analysis) &&
               NextSteps != null &&
               !string.IsNullOrWhiteSpace(Parameters);
    }

    /// <summary>
    /// Creates a WorkflowActivityAgentOutput from a WorkflowActivityLLMOutput.
    /// This is used to convert the simple LLM output to the full implementation.
    /// </summary>
    public static WorkflowActivityAgentOutput FromLLMOutput(
        WorkflowActivityLLMOutput llmOutput,
        string reasoningScratchPad,
        string notifyUserMessage,
        string state,
        string stateExplanation,
        string? agentName = null)
    {
        var output = new WorkflowActivityAgentOutput
        {
            // Copy from LLM output
            NextSteps = llmOutput.NextSteps,
            Analysis = llmOutput.Analysis,
            Parameters = llmOutput.Parameters,

            // Set from parameters
            ReasoningScratchPad = reasoningScratchPad,
            NotifyUserMessage = notifyUserMessage,
            State = state,
            StateExplanation = stateExplanation,
            AgentName = agentName,
            GeneratedAt = DateTime.UtcNow
        };

        // Parse parameters immediately
        output.ParseParameters();

        return output;
    }
}
