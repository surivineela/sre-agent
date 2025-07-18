// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


namespace Agent.Core.Models.Charts;

public class HeatmapPoint
{
    public required string X { get; set; }
    public required string Y { get; set; }
    public required double Value { get; set; }
}
