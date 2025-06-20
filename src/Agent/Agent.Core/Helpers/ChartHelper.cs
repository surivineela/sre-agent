// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Charts;
using ScottPlot;

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

    // Expose flatHexColors for use in other classes
    public static string[] GetFlatHexColors() => flatHexColors;

    private static string GetTempImagePath()
    {
        string tempPath = Path.GetTempPath();
        string fileName = $"{Guid.NewGuid()}.png";
        return Path.Combine(tempPath, fileName);
    }

    public static string GenerateChartBase64String(ChartImageInput chartImageInput, Tuple<int, int, double> sizeParameters = null)
    {
        if (sizeParameters == null)
        {
            sizeParameters = new Tuple<int, int, double>(600, 400, 1.0);
        }
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

        plt.ScaleFactor = sizeParameters.Item3;

        var savedImage = plt.SavePng(imageFile, sizeParameters.Item1, sizeParameters.Item2);
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
        Plot plt; // Declare plt here to be accessible for common operations if moved outside

        if (chartInput.UseManualBarData)
        {
            if (chartInput.BarsData == null || !chartInput.BarsData.Any())
                return string.Empty;

            plt = new Plot(); // Initialize for this branch
            var scottPlotBars = new List<ScottPlot.Bar>();

            foreach (var barData in chartInput.BarsData)
            {
                var scottBar = new ScottPlot.Bar
                {
                    Position = barData.Position,
                    Value = barData.Value,
                    FillColor = !string.IsNullOrEmpty(barData.FillColorHex) ? new Color(barData.FillColorHex) : new Color("#808080") // Default color
                };
                if (barData.Error.HasValue)
                {
                    // ScottPlot.Bar doesn't have a direct 'Error' property in the same way as the example.
                    // Error bars are typically added as a separate plot type (e.g., ErrorBar series) or drawn manually.
                    // For simplicity, we'll omit error bars if direct property isn't available on ScottPlot.Bar.
                    // If error bars are crucial, this part needs to be implemented by adding an ErrorBar series
                    // that corresponds to these bars.
                }
                scottPlotBars.Add(scottBar);
            }
            plt.Add.Bars(scottPlotBars.ToArray());

            if (chartInput.ManualLegendItems != null && chartInput.ManualLegendItems.Any())
            {
                plt.Legend.IsVisible = true;
                if (Enum.TryParse<Alignment>(chartInput.LegendPosition, true, out var legendAlignment))
                {
                    plt.Legend.Alignment = legendAlignment;
                }
                else
                {
                    plt.Legend.Alignment = Alignment.UpperRight; // Default if parsing fails
                }

                foreach (var legendItem in chartInput.ManualLegendItems) // Corrected loop
                {
                    plt.Legend.ManualItems.Add(new ScottPlot.LegendItem
                    {
                        LabelText = legendItem.LabelText,
                        FillColor = !string.IsNullOrEmpty(legendItem.FillColorHex) ? new Color(legendItem.FillColorHex) : Colors.Transparent
                    });
                }
            }
            else
            {
                plt.Legend.IsVisible = false;
            }


            if (chartInput.XAxisTickLabels != null && chartInput.XAxisTickLabels.Any())
            {
                var ticks = chartInput.XAxisTickLabels.Select(label => new ScottPlot.Tick(label.Position, label.Label)).ToArray();
                plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
                plt.Axes.Bottom.MajorTickStyle.Length = 0; // As per example
            }
            
            if (chartInput.HideGridLines)
            {
                plt.HideGrid();
            }

            if (chartInput.BottomMargin.HasValue)
            {
                plt.Axes.Margins(bottom: chartInput.BottomMargin.Value);
            }
            else
            {
                 plt.Axes.Margins(bottom: 0); 
            }

            // Common finalization for this branch
            plt.Title(chartInput.Title);
            plt.XLabel(chartInput.XAxisLabel);
            plt.YLabel(chartInput.YAxisLabel);

            var imageFileManual = GetTempImagePath();
            if (File.Exists(imageFileManual))
                File.Delete(imageFileManual);

            plt.SavePng(imageFileManual, 800, 600); 
            string base64Manual = ConvertImageToBase64String(imageFileManual);
            File.Delete(imageFileManual);
            return base64Manual;
        }
        else
        {
            // Existing logic for simple bar charts
            if (chartInput.Data == null || !chartInput.Data.Any())
                return string.Empty;

            plt = new Plot(); // Initialize for this branch

            double[] positions = Enumerable.Range(0, chartInput.Data.Count).Select(x => (double)x).ToArray();
            double[] values = chartInput.Data.Select(x => x.Value).ToArray();
            string[] labels = chartInput.Data.Select(x => x.Category).ToArray();

            var barPlot = plt.Add.Bars(values);
            
            plt.Axes.SetLimitsX(-0.5, positions.Length - 0.5);
            
            var ticks = new List<ScottPlot.Tick>();
            for (int i = 0; i < positions.Length; i++)
            {
                ticks.Add(new ScottPlot.Tick(positions[i], labels[i]));
            }
            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
            plt.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleCenter;
            
            try
            {
                if (barPlot.Bars != null && barPlot.Bars.Any()) 
                {
                    for (int i = 0; i < barPlot.Bars.Count; i++)
                    {
                        if (i < values.Length) 
                        {
                            string colorHex = flatHexColors[i % flatHexColors.Length];
                            if (!string.IsNullOrEmpty(colorHex))
                            {
                                try
                                {
                                    barPlot.Bars[i].FillColor = new Color(colorHex);
                                }
                                catch
                                {
                                    barPlot.Bars[i].FillColor = new Color("#808080"); 
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Consider logging this server-side.
            }

            // Common finalization for this branch
            plt.Title(chartInput.Title);
            plt.XLabel(chartInput.XAxisLabel);
            plt.YLabel(chartInput.YAxisLabel);

            var imageFileSimple = GetTempImagePath();
            if (File.Exists(imageFileSimple))
                File.Delete(imageFileSimple);

            plt.SavePng(imageFileSimple, 800, 600);
            string base64Simple = ConvertImageToBase64String(imageFileSimple);
            File.Delete(imageFileSimple);
            return base64Simple;
        }
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
        scatter.MarkerSize = 10; // Default marker size
        scatter.MarkerShape = MarkerShape.FilledCircle; // Default marker shape

        // Apply colors from a palette - using the first color from flatHexColors for all points for simplicity
        // If different colors per point or series are needed, this logic would need to be more complex,
        // potentially requiring color information in ScatterPoint or grouping.
        if (flatHexColors.Any())
        {
            scatter.MarkerColor = new Color(flatHexColors[0]); 
        }
        else
        {
            scatter.MarkerColor = Colors.Blue; // Fallback color
        }

        // Add labels if provided
        for (int i = 0; i < chartInput.Points.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(chartInput.Points[i].Label))
            {
                // Add text labels near the points. Adjust offset as needed.
                var text = plt.Add.Text(chartInput.Points[i].Label, xs[i], ys[i]);
                text.LabelFontColor = Colors.Black;
                text.LabelFontSize = 10;
                // Optional: Offset the label slightly so it doesn't overlap the point
                // text.OffsetX = 5;
                // text.OffsetY = 5;
            }
        }

        // Customize the plot
        if (!string.IsNullOrWhiteSpace(chartInput.Title))
            plt.Title(chartInput.Title);
        
        if (!string.IsNullOrWhiteSpace(chartInput.XAxisLabel))
            plt.XLabel(chartInput.XAxisLabel);

        if (!string.IsNullOrWhiteSpace(chartInput.YAxisLabel))
            plt.YLabel(chartInput.YAxisLabel);

        // Save to temporary file
        var imageFile = GetTempImagePath();
        if (File.Exists(imageFile))
            File.Delete(imageFile);

        // Consider making size configurable, e.g., via ScatterChartInput
        plt.SavePng(imageFile, 800, 600); 
        string base64 = ConvertImageToBase64String(imageFile);
        File.Delete(imageFile);
        return base64;
    }
}

