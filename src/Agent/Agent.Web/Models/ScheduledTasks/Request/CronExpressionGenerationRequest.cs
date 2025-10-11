// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Web.Models.ScheduledTasks.Request;

public class CronExpressionGenerationRequest
{
    /// <summary>
    /// Natural-language description of the desired schedule (e.g., "Every weekday at 9am").
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional timezone hint (e.g., "UTC", "America/Los_Angeles").
    /// </summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// Optional ISO 8601 start time that the schedule should respect.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Optional cron format preference ("standard" for 5-field, "extended" for 6-field expressions).
    /// </summary>
    public string? Format { get; set; }
}
