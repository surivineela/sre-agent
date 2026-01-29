// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Charts;
using Agent.Framework;
using Microsoft.Extensions.Logging;
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

    public static string GenerateChartBase64String(ChartImageInput chartImageInput, Tuple<int, int, double>? sizeParameters = null)
    {
        if (sizeParameters == null)
        {
            sizeParameters = new Tuple<int, int, double>(600, 400, 1.0);
        }
        if (chartImageInput.TimeSeries == null || !chartImageInput.TimeSeries.Any())
            return string.Empty;

        // Create a new ScottPlot plot
        var plt = new Plot();

        // Group data by series name to create separate lines
        var groupedData = chartImageInput.TimeSeries
            .GroupBy(data => data.Name)
            .ToList();

        // Plot each series separately with unique colors
        int colorIndex = 0;
        foreach (var group in groupedData)
        {
            DateTime[] dts = group.Select(data => data.Timestamp).ToArray();
            double[] ys = group.Select(data => data.Value).ToArray();

            var scatter = plt.Add.Scatter(dts, ys);
            scatter.LegendText = group.Key; // Set legend text to series name
            scatter.LineWidth = 2;

            // Apply color from palette
            if (colorIndex < flatHexColors.Length)
            {
                scatter.Color = new Color(flatHexColors[colorIndex]);
            }
            colorIndex++;
        }

        plt.Axes.DateTimeTicksBottom();
        plt.ShowLegend();

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
        catch (Exception)
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
            catch (Exception)
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

    /// <summary>
    /// Determines the appropriate time format based on the date range
    /// </summary>
    public static string DetermineTimeFormat(DateTime earliestDate, DateTime latestDate)
    {
        var timeDelta = latestDate - earliestDate;

        if (timeDelta.TotalDays <= 0)
        {
            return "HH:mm:ss"; // Within a day
        }
        else if (timeDelta.TotalDays <= 7)
        {
            return "MM-dd HH:mm"; // Within a week
        }
        else if (timeDelta.TotalDays <= 30)
        {
            return "MM-dd HH"; // Between 7 and 30 days
        }
        else
        {
            return "yyyy-MM-dd HH"; // Above 30 days
        }
    }

    /// <summary>
    /// Posts chart data to the thread in a format the frontend can render
    /// </summary>
    public static async Task<string> PostChartDataAsync(
        Guid threadId,
        object chartData,
        string? description,
        IAgentOutboundCommunicationService outboundService,
        ILogger? logger = null)
    {
        try
        {
            // Serialize chart data to JSON
            var chartDataJson = JsonSerializer.Serialize(chartData);
            logger?.LogInternalInformation("Posting chart data to thread {ThreadId}", threadId);

            // Create the chart message format that the front-end will recognize
            var chartMessage = $"```chart-data\n{chartDataJson}\n```" + (string.IsNullOrEmpty(description) ? "" : $"\n{description}");

            Guid chartMessageId = Guid.NewGuid();

            // Save to database via the outbound service
            await outboundService.AppendAgentImageMessage(threadId, chartMessage, chartMessageId);

            // Stream the chart data directly to bypass tool call limitations
            await outboundService.AppendAgentStreamMessage(threadId, chartMessage, StreamMessageType.Chart, chartMessageId);

            return $"Successfully generated the chart data, description: {description}";
        }
        catch (Exception ex)
        {
            logger?.LogInternalError(ex, "Failed to post chart data");
            return $"ERROR: Chart data processing failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Generates HTML with an embedded chart image from TSV-formatted data.
    /// Useful for embedding charts in HTML content like ICM discussion entries.
    /// </summary>
    /// <param name="tsvData">TSV-formatted data. First row is headers, first column is X-axis (timestamp or category), other numeric columns are Y values.</param>
    /// <param name="chartTitle">Optional title for the chart.</param>
    /// <param name="chartType">Optional chart type: "timeseries" or "bar". Auto-detects if not specified.</param>
    /// <returns>HTML string with embedded base64 chart image, or null if chart generation fails.</returns>
    public static string? GenerateChartHtmlFromTsv(string tsvData, string? chartTitle = null, string? chartType = null)
    {
        if (string.IsNullOrWhiteSpace(tsvData))
        {
            return null;
        }

        // Parse TSV data
        var lines = tsvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return null; // Not enough data
        }

        var headers = lines[0].Split('\t');
        if (headers.Length < 2)
        {
            return null; // Need at least 2 columns
        }

        // Determine chart type based on first column if not specified
        // Map chart types:
        // - timeseries/line/area -> time series chart (area is rendered as line, ScottPlot fill not easily supported for static images)
        // - bar -> bar chart
        // - pie -> pie chart
        // Note: scatter is not supported (would require X/Y coordinate pairs)
        var chartTypeLower = chartType?.ToLowerInvariant();
        var useTimeSeries = chartTypeLower == "timeseries" || chartTypeLower == "line" || chartTypeLower == "area";
        var usePieChart = chartTypeLower == "pie";
        var useBarChart = chartTypeLower == "bar";

        if (!useTimeSeries && !usePieChart && !useBarChart)
        {
            // Auto-detect: if first data value looks like a date, use time series
            if (lines.Length > 1)
            {
                var cells = lines[1].Split('\t');
                var firstValue = cells.Length > 0 ? cells[0] : string.Empty;
                useTimeSeries = !string.IsNullOrEmpty(firstValue) &&
                    DateTime.TryParse(firstValue, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _);
                useBarChart = !useTimeSeries;
            }
        }

        string? base64Image = null;

        if (useTimeSeries)
        {
            base64Image = GenerateTimeSeriesChartFromTsv(lines, headers, chartTitle);
        }
        else if (usePieChart)
        {
            base64Image = GeneratePieChartFromTsv(lines, headers);
        }
        else
        {
            base64Image = GenerateBarChartFromTsv(lines, headers, chartTitle);
        }

        if (string.IsNullOrEmpty(base64Image))
        {
            return null;
        }

        // Build HTML with embedded chart
        var htmlBuilder = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(chartTitle))
        {
            htmlBuilder.Append("<h3>").Append(System.Web.HttpUtility.HtmlEncode(chartTitle)).Append("</h3>");
        }

        htmlBuilder.Append("<div style=\"margin: 10px 0;\">");
        htmlBuilder.Append("<img src=\"").Append(base64Image).Append("\" alt=\"Chart\" style=\"max-width: 100%; height: auto;\" />");
        htmlBuilder.Append("</div>");

        return htmlBuilder.ToString();
    }

    private static string? GenerateTimeSeriesChartFromTsv(string[] lines, string[] headers, string? chartTitle)
    {
        var timeSeriesData = new List<TimeSeriesData>();
        var numericColumnIndices = new List<int>();

        // Find numeric columns (skip first which is X-axis)
        for (int col = 1; col < headers.Length; col++)
        {
            for (int row = 1; row < Math.Min(lines.Length, 5); row++)
            {
                var cells = lines[row].Split('\t');
                if (col < cells.Length && double.TryParse(cells[col], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    numericColumnIndices.Add(col);
                    break;
                }
            }
        }

        if (numericColumnIndices.Count == 0)
        {
            return null;
        }

        // Parse data rows
        for (int row = 1; row < lines.Length; row++)
        {
            var cells = lines[row].Split('\t');
            if (cells.Length < 2) continue;

            if (!DateTime.TryParse(cells[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var timestamp))
            {
                continue;
            }

            foreach (var colIdx in numericColumnIndices)
            {
                if (colIdx < cells.Length && double.TryParse(cells[colIdx], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    timeSeriesData.Add(new TimeSeriesData
                    {
                        Name = headers[colIdx],
                        Timestamp = timestamp,
                        Value = value,
                        Unit = string.Empty
                    });
                }
            }
        }

        if (timeSeriesData.Count == 0)
        {
            return null;
        }

        var chartInput = new ChartImageInput
        {
            TimeSeries = timeSeriesData,
            Title = chartTitle
        };

        return GenerateChartBase64String(chartInput);
    }

    private static string? GenerateBarChartFromTsv(string[] lines, string[] headers, string? chartTitle)
    {
        var barData = new List<BarData>();

        // Find first numeric column
        int valueColumnIndex = -1;
        for (int col = 1; col < headers.Length; col++)
        {
            for (int row = 1; row < Math.Min(lines.Length, 5); row++)
            {
                var cells = lines[row].Split('\t');
                if (col < cells.Length && double.TryParse(cells[col], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    valueColumnIndex = col;
                    break;
                }
            }
            if (valueColumnIndex >= 0) break;
        }

        if (valueColumnIndex < 0)
        {
            return null;
        }

        // Parse data rows (limit for readability)
        const int MaxBarChartRows = 30;
        const int MaxCategoryLength = 25;
        for (int row = 1; row < Math.Min(lines.Length, MaxBarChartRows + 1); row++)
        {
            var cells = lines[row].Split('\t');
            if (cells.Length <= valueColumnIndex) continue;

            var category = cells[0];
            if (double.TryParse(cells[valueColumnIndex], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                barData.Add(new BarData
                {
                    Category = category.Length > MaxCategoryLength ? category.Substring(0, MaxCategoryLength - 3) + "..." : category,
                    Value = value
                });
            }
        }

        if (barData.Count == 0)
        {
            return null;
        }

        var barChartInput = new BarChartInput
        {
            Title = chartTitle ?? "Query Results",
            XAxisLabel = headers[0],
            YAxisLabel = headers[valueColumnIndex],
            Data = barData
        };

        return GenerateBarChartBase64String(barChartInput);
    }

    private static string? GeneratePieChartFromTsv(string[] lines, string[] headers)
    {
        var slices = new List<PieSlice>();

        // Find first numeric column for values
        int valueColumnIndex = -1;
        for (int col = 1; col < headers.Length; col++)
        {
            for (int row = 1; row < Math.Min(lines.Length, 5); row++)
            {
                var cells = lines[row].Split('\t');
                if (col < cells.Length && double.TryParse(cells[col], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    valueColumnIndex = col;
                    break;
                }
            }
            if (valueColumnIndex >= 0) break;
        }

        if (valueColumnIndex < 0)
        {
            return null;
        }

        // Parse data rows (limit for readability - pie charts shouldn't have too many slices)
        const int MaxPieSlices = 10;
        const int MaxLabelLength = 20;
        for (int row = 1; row < Math.Min(lines.Length, MaxPieSlices + 1); row++)
        {
            var cells = lines[row].Split('\t');
            if (cells.Length <= valueColumnIndex) continue;

            var label = cells[0];
            if (double.TryParse(cells[valueColumnIndex], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                slices.Add(new PieSlice
                {
                    Label = label.Length > MaxLabelLength ? label.Substring(0, MaxLabelLength - 3) + "..." : label,
                    Value = value
                });
            }
        }

        return slices.Any() ? GeneratePieChartBase64String(slices) : null;
    }

    /// <summary>
    /// Converts a chart-data JSON string to a base64-encoded PNG image.
    /// Supports pie, line, and bar chart types.
    /// </summary>
    /// <param name="chartDataJson">JSON with type, title, and data array</param>
    /// <returns>Base64 data URI (data:image/png;base64,...) or empty string if conversion fails</returns>
    public static string ConvertChartDataJsonToBase64(string chartDataJson)
    {
        if (string.IsNullOrWhiteSpace(chartDataJson))
        {
            return string.Empty;
        }

        try
        {
            var chartDoc = JsonDocument.Parse(chartDataJson);
            var root = chartDoc.RootElement;

            var chartType = root.GetProperty("type").GetString();
            var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : "Chart";

            return chartType switch
            {
                "pie" => GeneratePieChartFromJson(root),
                "line" => GenerateLineChartFromJson(root, title),
                "bar" => GenerateBarChartFromJson(root, title),
                _ => string.Empty
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GeneratePieChartFromJson(JsonElement root)
    {
        var data = root.GetProperty("data");
        var slices = new List<PieSlice>();

        foreach (var item in data.EnumerateArray())
        {
            var label = item.GetProperty("label").GetString() ?? "";
            var value = item.GetProperty("value").GetDouble();
            slices.Add(new PieSlice { Label = label, Value = value });
        }

        return slices.Any() ? GeneratePieChartBase64String(slices) : string.Empty;
    }

    private static string GenerateLineChartFromJson(JsonElement root, string? title)
    {
        var data = root.GetProperty("data");
        var yAxisLabel = root.TryGetProperty("yAxisLabel", out var yProp) ? yProp.GetString() : null;

        var timeSeries = new List<TimeSeriesData>();

        // Get series field names from first data item
        var firstItem = data.EnumerateArray().FirstOrDefault();
        if (firstItem.ValueKind == JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        var seriesNames = new List<string>();
        foreach (var prop in firstItem.EnumerateObject())
        {
            if (prop.Name != "name" && prop.Value.ValueKind == JsonValueKind.Number)
            {
                seriesNames.Add(prop.Name);
            }
        }

        foreach (var item in data.EnumerateArray())
        {
            var nameValue = item.GetProperty("name").GetString() ?? "";
            // Skip data points with unparseable timestamps to avoid chart corruption
            if (!DateTime.TryParse(nameValue, out var timestamp))
            {
                continue;
            }

            foreach (var seriesName in seriesNames)
            {
                if (item.TryGetProperty(seriesName, out var valueProp) &&
                    valueProp.ValueKind == JsonValueKind.Number)
                {
                    timeSeries.Add(new TimeSeriesData
                    {
                        Name = seriesName,
                        Timestamp = timestamp,
                        Value = valueProp.GetDouble(),
                        Unit = ""
                    });
                }
            }
        }

        if (!timeSeries.Any())
        {
            return string.Empty;
        }

        var chartInput = new ChartImageInput
        {
            TimeSeries = timeSeries,
            Title = title,
            YAxisLabel = yAxisLabel
        };

        return GenerateChartBase64String(chartInput);
    }

    private static string GenerateBarChartFromJson(JsonElement root, string? title)
    {
        var data = root.GetProperty("data");
        var xAxisLabel = root.TryGetProperty("xAxisLabel", out var xProp) ? xProp.GetString() : null;
        var yAxisLabel = root.TryGetProperty("yAxisLabel", out var yProp) ? yProp.GetString() : null;

        var barData = new List<BarData>();

        foreach (var item in data.EnumerateArray())
        {
            var category = item.GetProperty("name").GetString() ?? "";

            // Find the first numeric property that isn't "name"
            foreach (var prop in item.EnumerateObject())
            {
                if (prop.Name != "name" && prop.Value.ValueKind == JsonValueKind.Number)
                {
                    barData.Add(new BarData
                    {
                        Category = category,
                        Value = prop.Value.GetDouble()
                    });
                    break;
                }
            }
        }

        if (!barData.Any())
        {
            return string.Empty;
        }

        var chartInput = new BarChartInput
        {
            Title = title ?? "Bar Chart",
            XAxisLabel = xAxisLabel ?? "Category",
            YAxisLabel = yAxisLabel ?? "Value",
            Data = barData
        };

        return GenerateBarChartBase64String(chartInput);
    }

    /// <summary>
    /// Converts ```chart-data``` markdown blocks in content to base64 embedded images.
    /// Useful for contexts that only support HTML/images (e.g., ICM discussions).
    /// </summary>
    /// <param name="content">Content potentially containing chart-data blocks</param>
    /// <returns>Content with chart-data blocks replaced by HTML img tags</returns>
    public static string ConvertChartDataBlocksToBase64Images(string content)
    {
        if (string.IsNullOrEmpty(content) || !content.Contains("```chart-data"))
        {
            return content;
        }

        var chartDataRegex = new System.Text.RegularExpressions.Regex(
            @"```chart-data\s*\n([\s\S]*?)\n```",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        return chartDataRegex.Replace(content, match =>
        {
            var jsonContent = match.Groups[1].Value.Trim();
            var base64Image = ConvertChartDataJsonToBase64(jsonContent);

            // Validate the image is a proper data URI to prevent XSS
            if (string.IsNullOrEmpty(base64Image) || !base64Image.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return "<p><em>[Chart visualization not available]</em></p>";
            }

            // Extract title from JSON for alt text
            var title = "Chart";
            try
            {
                var chartDoc = JsonDocument.Parse(jsonContent);
                if (chartDoc.RootElement.TryGetProperty("title", out var titleProp))
                {
                    title = titleProp.GetString() ?? "Chart";
                }
            }
            catch { }

            return $"<p><strong>{System.Web.HttpUtility.HtmlEncode(title)}</strong></p><img src=\"{base64Image}\" alt=\"{System.Web.HttpUtility.HtmlEncode(title)}\" />";
        });
    }
}
