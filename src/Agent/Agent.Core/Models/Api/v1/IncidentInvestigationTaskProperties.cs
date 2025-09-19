// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using NJ = Newtonsoft.Json;
using NJC = Newtonsoft.Json.Converters;
using SJ = System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

/*
JSON representation of the incident investigation task properties:
{
    "id": "00000000-0000-0000-0000-000000000000",
    "type": "incidentInvestigation",
    "status": "inProgress | completed | failed",
    "title": "500 errors reported for containerapp {{appname}}",
    "properties": {
        "initialInvestigation": {
            "status": "inProgress | complete",
            "gatheringContext": {
                "status": "inProgress | complete",
                "steps": [
                    {
                        "title": "Checking logs",
                        "status": "inProgress | complete",
                    }
                ]
            },
            "summary": "summary of the initial investigation"
        },
        "formingHypothesis": {
            "hypotheses": [
                {
                    "id": "00000000-0000-0000-0000-000000000000",
                    "title": "Hypothesis Title",
                    "description": "Hypothesis Description",
                    "status": "validated | invalidated | inconclusive",
                    "children": [
                        {
                            "id": "00000000-0000-0000-0000-000000000000",
                            "title": "Child Hypothesis Title",
                            "description": "Child Hypothesis Description",
                            "status": "validated | invalidated | inconclusive",
                            "children": [
                                {
                                    "title": "Child of Child Hypothesis Title",
                                }
                            ]
                        },
                        {
                            //...
                        },
                        {
                            //...
                        }
                    ]
                }
            ]
        },
        "conclusion": {
            "title": "conclusion title",
            "summary": "detailed summary"
        }
    }
}
*/

public sealed record IncidentInvestigationTaskProperties : AgentTaskProperties
{
    public required InitialInvestigationProperties InitialInvestigation { get; set; }
    public required FormingHypothesisProperties FormingHypothesis { get; set; }
    public required ConclusionProperties Conclusion { get; set; }
}

#region Initial Investigation

public sealed record InitialInvestigationProperties
{
    public required GatheringContextProperties GatheringContext { get; set; }
    public required string Summary { get; set; }

    public required string IncidentDescription { get; set; }

    public required string TimeFrame { get; set; }

    public required IList<string> AffectedResources { get; set; }

    public required string KeyFindings { get; set; }

    public required string Details { get; set; }

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public required InitialInvestigationStatus Status { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// List of tool names that were selected for the initial investigation.
    /// </summary>
    public List<string>? ToolNames { get; set; } = null;

    public override string ToString()
    {
        return $"<initial_investigation_summary>Summary={Summary}, Details={Details}, Steps={JsonSerializer.Serialize(GatheringContext.Steps)}</initial_investigation_summary>";
    }
}

public enum InitialInvestigationStatus
{
    NotStarted,
    InProgress,
    Complete
}

public sealed record GatheringContextProperties
{
    public IList<InitialInvestigationStep> Steps { get; set; } = new List<InitialInvestigationStep>();

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public required InitialInvestigationStatus Status { get; set; }
}

public sealed record InitialInvestigationStep
{
    public required string Title { get; set; } // title of the high-level initial investigation step, i.e. "Reviewing logs" or "Checking metrics"

    public required string Summary { get; set; }

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public required InitialInvestigationStatus Status { get; set; }

    /// <summary>
    /// Tool executions performed during this investigation step
    /// </summary>
    public List<ToolExecutionResult> ToolExecutions { get; set; } = [];
}

#endregion

#region Forming Hypothesis

public sealed record FormingHypothesisProperties
{
    public IList<HypothesisTreeItem> Hypotheses { get; set; } = new List<HypothesisTreeItem>();

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public required FormingHypothesisStatus Status { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public List<HypothesisTreeItem> GetAllHypotheses()
    {
        var allHypotheses = new List<HypothesisTreeItem>();

        foreach (var hypothesis in Hypotheses)
        {
            allHypotheses.Add(hypothesis);
            if (hypothesis.Children != null && hypothesis.Children.Count > 0)
            {
                allHypotheses.AddRange(hypothesis.GetAllDescendentHypotheses());
            }
        }

        return allHypotheses;
    }
}

public enum FormingHypothesisStatus
{
    NotStarted,
    InProgress,
    Complete
}

public enum HypothesisStatus
{
    Pending, // not started yet
    Validating, // validating the hypothesis
    Validated,
    Invalidated,
    Inconclusive
}

/// <summary>
/// Hypothesis properties for the overall agent task. Does not include steps.
/// </summary>
public sealed record HypothesisTreeItem
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required IList<HypothesisTreeItem> Children { get; set; }

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public required HypothesisStatus Status { get; set; }

    public string Reasoning { get; set; } = string.Empty;

    // HACK: temporarily storing steps here to avoid having to deal with separate cosmos items
    public required IList<HypothesisStep> Steps { get; set; }

    public required string ParentHypothesisDescription { get; set; } = string.Empty;

    public string StatusMessage { get; set; } = string.Empty;

    public HypothesisTreeItem Copy()
    {
        return this with { };
    }

    public List<HypothesisTreeItem> GetAllDescendentHypotheses()
    {
        if (Children.Count == 0)
        {
            return [];
        }

        var result = new List<HypothesisTreeItem>();

        foreach (var child in Children)
        {
            // Add the child itself
            result.Add(child);
            // Add all of the child's descendants recursively
            result.AddRange(child.GetAllDescendentHypotheses());
        }

        return result;
    }
}

/// <summary>
/// Expanded hypothesis properties for detailed view. Includes steps.
/// </summary>
public sealed record HypothesisDetails
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Reasoning { get; set; } = string.Empty;
    public required IEnumerable<HypothesisStep> Steps { get; set; }
    public required IEnumerable<HypothesisTreeItem> Children { get; set; }

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public required HypothesisStatus Status { get; set; }

    public required Guid AgentTaskId { get; set; }

    public required string ParentHypothesisDescription { get; set; } = string.Empty;

}

public sealed record HypothesisStep
{
    [Description("A brief summary of the step taken to investigate the hypothesis. This should be a single sentence that describes the step.")]
    public required string Summary { get; set; } // summary of the step taken to investigate the hypothesis

    [Description("The detailed content of the step taken to investigate the hypothesis. This should be a more detailed description of the step, including the reasoning behind the step. This should be in markdown format, using headings, bullet points, etc. to format the content in a readable way.")]
    public required string Details { get; set; } // details of the step taken to investigate the hypothesis, in markdown format

    /// <summary>
    /// Tool executions performed during this hypothesis validation step
    /// </summary>
    public List<ToolExecutionResult> ToolExecutions { get; set; } = [];
}

#endregion

#region Conclusion

public sealed record ConclusionProperties
{
    public required string Title { get; set; }
    public required string Summary { get; set; }
}

#endregion

#region Tool Execution Results

/// <summary>
/// Represents the result of a tool execution during investigation
/// </summary>
public sealed record ToolExecutionResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [NJ.JsonConverter(typeof(NJC.StringEnumConverter))]
    [SJ.JsonConverter(typeof(SJ.JsonStringEnumConverter))]
    public ToolExecutionType Type { get; set; }

    public string KustoQueryResults { get; set; } = string.Empty; // Full markdown string for Kusto results
    public DateTime ExecutedTimestamp { get; set; }

    // Structured data for specific tool types (avoids re-parsing JSON)
    public AzCliExecution? AzCliExecution { get; set; }
}

/// <summary>
/// Types of tools that can be executed during investigation
/// </summary>
public enum ToolExecutionType
{
    Kusto,
    AzCli,
}

#endregion
