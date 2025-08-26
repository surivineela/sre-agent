// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Models;

public class AppCorrelateDataSetParseResult
{
    public bool IsValid { get; set; } = true;

    public string? ErrorMessage { get; set; }

    public List<AppCorrelateDataSet> DataSets { get; set; } = new List<AppCorrelateDataSet>();
}
