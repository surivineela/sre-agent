// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Kusto;

/// <summary>
/// Optional display flags for rendering Kusto results in the chat UI.
/// Passing this is opt-in; existing behavior remains unchanged otherwise.
/// </summary>
public class KustoDisplayOptions
{
    public bool ShowTable { get; set; } = false;
    public bool ShowChart { get; set; } = false;

    /// <summary>
    /// Chart type to render (line, bar, pie, scatter, table).
    /// Derived from "| render XXX" in the query, or null if unsupported/not specified.
    /// </summary>
    public string? ChartType { get; set; }

    public int MaxTableRows { get; set; } = 50;
    public int MaxChartPoints { get; set; } = 200;

    public string? ChartTitle { get; set; }
    public string? XField { get; set; }
    public IReadOnlyList<string>? SeriesFields { get; set; }
}
