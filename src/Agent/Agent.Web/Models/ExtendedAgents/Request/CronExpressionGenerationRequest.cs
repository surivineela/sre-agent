// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Web.Models.ExtendedAgents.Request;

public class CronExpressionGenerationRequest
{
    /// <summary>
    /// Natural language description of the desired schedule (e.g. "Every weekday at 9am").
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional timezone hint (e.g. "UTC", "America/Los_Angeles").
    /// </summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// Optional starting point or additional context (ISO 8601).
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Optional preferred cron format ("standard" for 5-part, "extended" for 6+ fields).
    /// Defaults to standard if not provided.
    /// </summary>
    public string? Format { get; set; }
}
