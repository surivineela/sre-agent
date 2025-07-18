// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Charts;

public class TimeSeriesData
{
    public required string Name { get; set; }
    public required DateTime Timestamp { get; set; }
    public required double Value { get; set; }
    public required string Unit { get; set; }
}
