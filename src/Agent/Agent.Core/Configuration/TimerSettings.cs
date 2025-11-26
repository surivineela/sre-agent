// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration;

public class TimerSettings
{
    [Required]
    public int BackgroundCrawlIntervalInMinutes { get; set; } = 30;

    // This provides a way to disable the timer for testing/developing purposes
    public bool Disabled { get; set; } = false;
}
