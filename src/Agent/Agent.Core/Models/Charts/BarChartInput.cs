namespace Agent.Core.Models.Charts
{
    public class BarChartInput
    {
        public string Title { get; set; } = "";
        public string XAxisLabel { get; set; } = "";
        public string YAxisLabel { get; set; } = "";
        public List<BarData> Data { get; set; } = new List<BarData>();
    }
}