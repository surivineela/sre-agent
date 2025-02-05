// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using ScottPlot;
using static Agent.Core.Plugins.ChartPlugin;

namespace Agent.Core.Helpers;

public static class ChartHelper
{

    private static string[] flatHexColors = new string[]
    {
            "#45B7D1", // Summer Sky Blue
            "#F1C40F", // Sunflower Yellow
            "#FF6B6B", // Coral Pink
            "#4ECDC4", // Caribbean Green
            "#FFA07A", // Light Salmon
            "#F1C40F", // Sunflower Yellow
            "#96CEB4", // Sage Green
            "#7986CB", // Cornflower Blue
            "#9B59B6", // Amethyst Purple
            "#26A69A", // Persian Green
            "#5C6BC0"  // Indigo Blue
    };

    private static string GetTempImagePath()
    {
        string tempPath = Path.GetTempPath();
        string fileName = $"{Guid.NewGuid()}.png";
        return Path.Combine(tempPath, fileName);
    }

    public static string GenerateChartBase64String(ChartImageInput chartImageInput)
    {
        if (chartImageInput.TimeSeries == null || !chartImageInput.TimeSeries.Any())
            return string.Empty;

        // Extract data for plotting
        DateTime[] dts = chartImageInput.TimeSeries.Select(data => data.Timestamp).ToArray();
        double[] ys = chartImageInput.TimeSeries.Select(data => data.Value).ToArray();

        // Create a new ScottPlot plot
        var plt = new Plot();

        // Plot data
        plt.Add.Scatter(dts, ys);
        plt.Axes.DateTimeTicksBottom();

        // Set plot title and labels
        if (!string.IsNullOrWhiteSpace(chartImageInput.Title))
            plt.Title(chartImageInput.Title);

        if (!string.IsNullOrWhiteSpace(chartImageInput.YAxisLabel))
            plt.YLabel(chartImageInput.YAxisLabel);

        plt.XLabel("Time (UTC)");
        if (chartImageInput.YAxisMax.GetValueOrDefault(0.0) > 0.0)
        {
            plt.Axes.SetLimitsY(bottom: chartImageInput.YAxisMin.GetValueOrDefault(0.0), top: chartImageInput.YAxisMax.GetValueOrDefault(0.0));
        }

        var imageFile = GetTempImagePath();

        if (File.Exists(imageFile))
            File.Delete(imageFile);

        var savedImage = plt.SavePng(imageFile, 600, 400);
        string base64 = ConvertImageToBase64String(imageFile);
        File.Delete(imageFile);

        return base64;
    }

    public static string GeneratePieChartBase64String(List<PieSlice> slices)
    {
        if (slices.Count == 0)
            return string.Empty;

        // Create a new ScottPlot plot
        var plt = new ScottPlot.Plot();

        // Add pie slices
        var pie = plt.Add.Pie(slices);
        pie.ExplodeFraction = .1;
        plt.Add.Pie(slices);
        pie.ExplodeFraction = .1;
        pie.SliceLabelDistance = 1.4;

        plt.ShowLegend();

        // hide unnecessary plot components
        plt.Axes.Frameless();
        plt.HideGrid();

        // Save to a temporary file
        var imageFile = GetTempImagePath();
        if (File.Exists(imageFile))
            File.Delete(imageFile);

        var palette = ScottPlot.Palette.FromColors(flatHexColors);
        var colors = palette.GetColors(slices.Count);

        for (int i = 0; i < slices.Count; i++)
        {
            slices[i].FillColor = colors[i];
        }

        plt.SavePng(imageFile, 600, 400);

        // Convert to Base64
        string base64 = ConvertImageToBase64String(imageFile);

        // Clean up
        File.Delete(imageFile);

        return base64;
    }

    private static string ConvertImageToBase64String(string imagePath)
    {
        try
        {
            // Read the image file into a byte array
            byte[] imageBytes = File.ReadAllBytes(imagePath);

            // Convert the byte array to a Base64 string
            string base64String = Convert.ToBase64String(imageBytes);

            return $"data:image/png;base64,{base64String}";
        }
        catch (Exception ex)
        {
            // TODO : need to log exception
            return string.Empty;
        }
    }

    public static string GenerateBarChartBase64String(BarChartInput chartInput)
    {
        if (chartInput.Data == null || !chartInput.Data.Any())
            return string.Empty;

        var plt = new Plot();

        // Extract data for plotting
        double[] positions = Enumerable.Range(0, chartInput.Data.Count).Select(x => (double)x).ToArray();
        double[] values = chartInput.Data.Select(x => x.Value).ToArray();
        string[] labels = chartInput.Data.Select(x => x.Category).ToArray();

        // Create the bar plot with colors
        var bar = plt.Add.Bars(values);

        try
        {
            int minLength = Math.Min(bar.Bars.Count, labels.Length);

            for (int i = 0; i < minLength; i++)
            {
                if (bar.Bars[i] != null)
                {
                    bar.Bars[i].Label = labels[i] ?? string.Empty;

                    string colorHex = flatHexColors[i % flatHexColors.Length];
                    if (!string.IsNullOrEmpty(colorHex))
                    {
                        try
                        {
                            bar.Bars[i].FillColor = new Color(colorHex);
                        }
                        catch
                        {
                            // If color parsing fails, use a default color
                            bar.Bars[i].FillColor = new Color("#808080");
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            return $"Unexpected error: {e.Message}";
        }

        // Apply a color palette
        var palette = ScottPlot.Palette.FromColors(flatHexColors);
        var colors = palette.GetColors(values.Length);

        // Customize the plot
        plt.Title(chartInput.Title);
        plt.XLabel(chartInput.XAxisLabel);
        plt.YLabel(chartInput.YAxisLabel);

        // Save to temporary file
        var imageFile = GetTempImagePath();
        if (File.Exists(imageFile))
            File.Delete(imageFile);

        plt.SavePng(imageFile, 800, 600);
        string base64 = ConvertImageToBase64String(imageFile);
        File.Delete(imageFile);
        return base64;
    }

    public static string GenerateScatterPlotBase64String(ScatterChartInput chartInput)
    {
        if (chartInput.Points == null || !chartInput.Points.Any())
            return string.Empty;

        var plt = new Plot();

        // Extract data for plotting
        double[] xs = chartInput.Points.Select(p => p.X).ToArray();
        double[] ys = chartInput.Points.Select(p => p.Y).ToArray();

        // Create the scatter plot
        var scatter = plt.Add.Scatter(xs, ys);
        scatter.MarkerSize = 10;
        scatter.MarkerShape = MarkerShape.FilledCircle;

        // Apply colors from a palette
        var palette = ScottPlot.Palette.FromColors(flatHexColors);
        scatter.MarkerColor = palette.GetColor(0);  // Use first color for points

        // Add labels if provided
        for (int i = 0; i < chartInput.Points.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(chartInput.Points[i].Label))
            {
                plt.Add.Text(chartInput.Points[i].Label, xs[i], ys[i]);
            }
        }

        // Customize the plot
        plt.Title(chartInput.Title);
        plt.XLabel(chartInput.XAxisLabel);
        plt.YLabel(chartInput.YAxisLabel);

        // Save to temporary file
        var imageFile = GetTempImagePath();
        if (File.Exists(imageFile))
            File.Delete(imageFile);

        plt.SavePng(imageFile, 800, 600);
        string base64 = ConvertImageToBase64String(imageFile);
        File.Delete(imageFile);
        return base64;
    }
}

