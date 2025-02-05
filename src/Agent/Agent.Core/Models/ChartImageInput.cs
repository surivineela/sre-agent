// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;

public class ChartImageInput
{
    public List<TimeSeriesData>? TimeSeries { get; set; }
    public string? Title { get; set; }
    public string? YAxisLabel { get; set; }
    public double? YAxisMin { get; set; }
    public double? YAxisMax { get; set; }
}
