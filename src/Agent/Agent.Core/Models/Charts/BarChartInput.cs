// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Charts
{
    public class BarChartInput
    {
        public string Title { get; set; } = "";
        public string XAxisLabel { get; set; } = "";
        public string YAxisLabel { get; set; } = "";
        public List<BarData> Data { get; set; } = new List<BarData>();

        // New properties for grouped bar charts
        public bool UseManualBarData { get; set; } = false;
        public List<ScottPlotBarData> BarsData { get; set; } = new List<ScottPlotBarData>();
        public List<BarLegendItem> ManualLegendItems { get; set; } = new List<BarLegendItem>();
        public List<BarGroupLabel> XAxisTickLabels { get; set; } = new List<BarGroupLabel>();
        public bool HideGridLines { get; set; } = false;
        public double? BottomMargin { get; set; }
        public string LegendPosition { get; set; } = "UpperRight"; // Default, can be "UpperLeft", "None", etc.
    }
}
