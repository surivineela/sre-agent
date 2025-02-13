// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Charts;

public class ChartImageInput
{
    public List<TimeSeriesData>? TimeSeries { get; set; }
    public string? Title { get; set; }
    public string? YAxisLabel { get; set; }
    public double? YAxisMin { get; set; }
    public double? YAxisMax { get; set; }
}
