// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration;

public class AppSettings
{
    public string ManagedIdentityClientId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;

    [Required]
    public CoreSettings Core { get; set; } = new();
}

public class LoggingSettings
{
    public bool EnableFileLogging { get; set; } = false;
    public string LogFilePath { get; set; } = "logs/sreagent-.log";
    public int RetainedFileCountLimit { get; set; } = 7;
    public int FileSizeLimitMB { get; set; } = 10;
    public string RollingInterval { get; set; } = "Day"; // Day, Hour, Minute, etc.
}

public class TestSettings
{
    [Required]
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    public bool SkipResourceCleanupAfterTestRun { get; set; } = true;
}
