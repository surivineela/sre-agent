// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models;

public class ContainerAppLogAnalyticsLog
{
    public string TimeGenerated { get; init; } = string.Empty;

    public string Log { get; init; } = string.Empty;
}
