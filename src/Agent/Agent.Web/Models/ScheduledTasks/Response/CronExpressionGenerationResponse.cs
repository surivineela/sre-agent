// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Models.ScheduledTasks.Response;

public class CronExpressionGenerationResponse
{
    public string CronExpression { get; set; } = string.Empty;

    public string HumanReadableDescription { get; set; } = string.Empty;

    public string? Timezone { get; set; }

    public List<string> Assumptions { get; set; } = new();

    public List<string> Warnings { get; set; } = new();

    public List<string> Examples { get; set; } = new();
}
