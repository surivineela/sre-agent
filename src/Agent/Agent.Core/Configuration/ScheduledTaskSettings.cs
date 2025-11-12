// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration;

public class ScheduledTaskSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether scheduled tasks are enabled.
    /// When enabled, scheduled task instructions will be included in agent prompts.
    /// </summary>
    public bool Enabled { get; set; } = false;
}
