// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Charts;

public class TimeSeriesData

{
    public string Name { get; set; }
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; }
}
