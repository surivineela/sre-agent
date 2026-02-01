// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework.TaskTool;

/// <summary>
/// Defines the types of subagents that can be spawned by the Task tool.
/// </summary>
public enum SubAgentType
{
    /// <summary>
    /// Code exploration agent - traces execution paths, maps architecture layers,
    /// understands patterns and abstractions, and documents dependencies.
    /// Use for understanding how features work before making changes.
    /// </summary>
    Explore,

    /// <summary>
    /// Architecture design agent - analyzes existing codebase patterns and conventions,
    /// then provides comprehensive implementation blueprints with specific files to
    /// create/modify, component designs, data flows, and build sequences.
    /// Use for planning new features or significant changes.
    /// </summary>
    Plan,

    /// <summary>
    /// Code review agent - reviews code for bugs, logic errors, security vulnerabilities,
    /// code quality issues, and adherence to project conventions.
    /// Uses confidence-based filtering to report only high-priority issues.
    /// </summary>
    CodeReview,

    /// <summary>
    /// Kusto query agent - executes KQL queries against Azure Data Explorer clusters
    /// and analyzes/summarizes the results. Can explore table schemas, sample data,
    /// and interpret query results to provide actionable insights.
    /// </summary>
    KustoQuery,

    /// <summary>
    /// Command execution specialist for running bash commands efficiently and safely.
    /// Use for git operations, command execution, and other terminal tasks.
    /// </summary>
    Bash
}
