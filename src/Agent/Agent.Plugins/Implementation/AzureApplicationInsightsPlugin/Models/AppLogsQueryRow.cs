// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Models;

public class AppLogsQueryRow<T>
{
    public required T Data { get; set; }
    public Dictionary<string, object?> OtherColumns { get; set; } = new Dictionary<string, object?>();
}
