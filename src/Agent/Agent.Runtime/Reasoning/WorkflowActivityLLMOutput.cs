// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Simplified structured output class for LLM to generate.
/// Contains only the 3 core properties that LLM needs to populate.
/// This will be converted to WorkflowActivityAgentOutput programmatically.
/// </summary>
public sealed class WorkflowActivityLLMOutput : IWorkflowActivityOutput
{
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
}
