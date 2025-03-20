using Agent.Core.Models;

namespace Agent.Plugins
{
    public interface IChartPlugin
    {
        Task<string> PlotTimeSeriesDataAsync(
            string threadId,
            string title,
            string yAxisLabel,
            string yAxisMin,
            string yAxisMax,
            string dataPoints,
            string description);

        Task<string> PlotPieChartAsync(
            string threadId,
            string chartTitle,
            string dataPoints,
            string description);

        Task<string> PlotBarChartAsync(
            string threadId,
            string chartTitle,
            string xAxisLabel,
            string yAxisLabel,
            string dataPoints,
            string description);

        Task<string> PlotScatterAsync(
            string threadId,
            string chartTitle,
            string xAxisLabel,
            string yAxisLabel,
            string dataPoints,
            string description);
    }
}