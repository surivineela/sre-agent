// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration;

public class AppSettings
{
    public string ApplicationName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;

    [Required]
    public CoreSettings Core { get; set; } = new();
}

public class LoggingSettings
{
    public bool LogGenAICalls { get; set; }
}

public class TestSettings
{
    [Required]
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    public bool SkipResourceCleanupAfterTestRun { get; set; } = true;
}