// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Charts
{
    public class ScottPlotBarData
    {
        public double Position { get; set; }
        public double Value { get; set; }
        public required string FillColorHex { get; set; }
        public double? Error { get; set; }
    }
}
