// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Web.Models.ScheduledTasks.Request;

public class ScheduledTaskPromptImprovementRequest
{
    /// <summary>
    /// The existing agent instructions to improve.
    /// </summary>
    [Required]
    public string Prompt { get; set; } = string.Empty;
}
