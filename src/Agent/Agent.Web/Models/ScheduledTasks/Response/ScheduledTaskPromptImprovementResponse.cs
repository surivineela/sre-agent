// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace Agent.Web.Models.ScheduledTasks.Response;

public class ScheduledTaskPromptImprovementResponse
{
    public string ImprovedPrompt { get; set; } = string.Empty;

    public List<string> Warnings { get; set; } = new();

    public List<string> Suggestions { get; set; } = new();

    public List<string> FollowUpQuestions { get; set; } = new();
}
