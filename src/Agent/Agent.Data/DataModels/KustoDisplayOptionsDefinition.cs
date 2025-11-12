// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Agent.Data.DataModels;

/// <summary>
/// Serializable representation of Kusto display preferences for YAML tools.
/// </summary>
public class KustoDisplayOptionsDefinition
{
    [YamlMember(Alias = "show_table")]
    public bool ShowTable { get; set; }

    [YamlMember(Alias = "show_chart")]
    public bool ShowChart { get; set; }

    [YamlMember(Alias = "max_table_rows")]
    public int? MaxTableRows { get; set; }

    [YamlMember(Alias = "max_chart_points")]
    public int? MaxChartPoints { get; set; }

    [YamlMember(Alias = "chart_title")]
    public string? ChartTitle { get; set; }

    [YamlMember(Alias = "x_field")]
    public string? XField { get; set; }

    [YamlMember(Alias = "series_fields")]
    public List<string>? SeriesFields { get; set; }

    public void Validate()
    {
        if (MaxTableRows is < 0)
        {
            throw new ArgumentException("display_options.max_table_rows cannot be negative.");
        }

        if (MaxChartPoints is < 0)
        {
            throw new ArgumentException("display_options.max_chart_points cannot be negative.");
        }
    }
}
