// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

/// <summary>
/// Represents a todo item in the manage_todo_list tool.
/// </summary>
public class WorkspaceTodoItem
{
    /// <summary>
    /// Unique identifier for the todo item.
    /// </summary>
    [Description("Unique identifier for the todo. Use sequential numbers starting from 1.")]
    public int Id { get; set; }

    /// <summary>
    /// Concise action-oriented label for the todo.
    /// </summary>
    [Description("Concise action-oriented todo label (3-7 words). Displayed in UI.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed context, requirements, or implementation notes.
    /// </summary>
    [Description("Detailed context, requirements, or implementation notes. Include file paths, specific methods, or acceptance criteria.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Status of the todo item: "not-started", "in-progress", or "completed".
    /// </summary>
    [Description("not-started: Not begun | in-progress: Currently working (max 1) | completed: Fully finished with no blockers")]
    public string Status { get; set; } = "not-started";
}
