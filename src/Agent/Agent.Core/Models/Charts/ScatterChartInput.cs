// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Charts
{
    public class ScatterChartInput
    {
        public string Title { get; set; } = "";
        public string XAxisLabel { get; set; } = "";
        public string YAxisLabel { get; set; } = "";
        public List<ScatterPoint> Points { get; set; } = new List<ScatterPoint>();
    }
}
