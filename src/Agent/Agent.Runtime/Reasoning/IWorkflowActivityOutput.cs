// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Core interface for workflow activity agent output that LLM should generate.
/// Contains only the essential properties that LLM needs to populate.
/// </summary>
public interface IWorkflowActivityOutput
{
    /// <summary>
    /// List of next agent names that should be executed based on analysis results.
    /// This corresponds to the conditions defined in next_agent_mappings.
    /// </summary>
    [Description(
        """
        List of next agent names that should be executed based on your analysis results.
        This corresponds to the conditions defined in next_agent_mappings in your agent configuration.
        Leave empty if no further agents need to be executed in this workflow path.
        """)]
    public List<string> NextSteps { get; set; }

    /// <summary>
    /// Detailed analysis results from executing the assigned task.
    /// </summary>
    [Description(
        """
        Detailed analysis results from executing your assigned task.
        This should contain the core findings, insights, or data that was discovered during your analysis.
        This information will be used by the orchestrator for final summarization and by subsequent agents.
        """)]
    public string Analysis { get; set; }

    /// <summary>
    /// Additional parameters discovered during task execution in JSON format.
    /// </summary>
    [Description(
        """
        Additional parameters discovered or refined during task execution in JSON format.
        These parameters will be merged with existing workflow parameters and made available to subsequent agents.
        Use JSON object format: {"key1": "value1", "key2": "value2"}
        Leave empty string if no new parameters were discovered.
        """)]
    public string Parameters { get; set; }
}
