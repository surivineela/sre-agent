// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Agent.Plugins.Kusto;

/// <summary>
/// Helper to build optional display content from a KustoQueryResult without changing existing behavior.
/// Produces markdown table (from TSV) and/or chart-data JSON blocks the web client already renders.
/// </summary>
internal static class KustoDisplayFormatter
{
    /// <summary>
    /// Extracts the chart type from a KQL query's render operator.
    /// Maps Kusto render types to frontend chart types.
    /// </summary>
    /// <param name="query">The KQL query to analyze.</param>
    /// <returns>The chart type (line, bar, pie, area, table) or null if no render operator or unsupported type.</returns>
    /// <remarks>
    /// KQL Render Types Mapping:
    /// - timechart, linechart → line (time series line chart)
    /// - areachart, stackedareachart → area (rendered as line chart with fill, falls back to line)
    /// - barchart, columnchart → bar (categorical bar chart)
    /// - piechart → pie (pie/donut chart)
    /// - table → table (tabular display)
    /// - anomalychart → line (anomaly bands not supported, renders as line)
    /// - ladderchart → bar (state transitions rendered as bar)
    /// - scatterchart → null (scatter requires X/Y coordinates, not supported)
    /// - pivotchart → null (interactive pivot not supported)
    /// - card → null (single value display not supported as chart)
    /// </remarks>
    public static string? GetChartType(string query)
    {
        // Match "| render <type>" pattern
        var renderMatch = string.IsNullOrWhiteSpace(query) ? null : Regex.Match(query, @"\|\s*render\s+(\w+)", RegexOptions.IgnoreCase);
        if (renderMatch?.Success == true)
        {
            return renderMatch.Groups[1].Value.ToLowerInvariant() switch
            {
                // Time series charts
                "timechart" => "line",
                "linechart" => "line",
                "areachart" => "area",
                "stackedareachart" => "area",
                "anomalychart" => "line",  // Anomaly bands not supported, render as line

                // Categorical charts
                "barchart" => "bar",
                "columnchart" => "bar",
                "ladderchart" => "bar",    // State transitions rendered as bar

                // Other chart types
                "piechart" => "pie",
                "table" => "table",

                // Unsupported - return null, will show table instead
                "scatterchart" => null,    // Scatter requires X/Y coordinates, not supported
                "pivotchart" => null,      // Interactive pivot not supported
                "card" => null,            // Single value display not a chart
                _ => null
            };
        }

        return null;
    }

    public static ChatMessage BuildDisplayMessage(ChatMessage baseMessage, string tsv, KustoDisplayOptions options)
    {
        if ((!options.ShowTable && !options.ShowChart) || string.IsNullOrEmpty(tsv))
        {
            return baseMessage;
        }

        var sb = new StringBuilder();
        // Keep existing header/ADX link
        sb.Append(baseMessage.Text);

        var (headers, rows, totalRows) = ParseTsv(tsv, options.MaxTableRows);

        if (options.ShowTable && headers.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            var label = totalRows > 0 ? $"Preview table (showing first {rows.Count} of {totalRows} rows)" : "Preview table";
            sb.AppendLine($"<details><summary>{label}</summary>");
            sb.AppendLine();
            sb.AppendLine(BuildMarkdownTable(headers, rows));
            if (totalRows > rows.Count)
            {
                sb.AppendLine();
                sb.AppendLine("> Note: Result truncated for display. Open the ADX link above to view all rows.");
            }
            sb.AppendLine("</details>");
        }

        if (options.ShowChart && headers.Length > 1)
        {
            // Downsample to keep charts light
            var maxChartPoints = options.MaxChartPoints > 0 ? options.MaxChartPoints : 200;
            var chartRows = DownsampleRows(rows, maxChartPoints);
            var chartJson = BuildChartDataJson(headers, chartRows, options);
            if (!string.IsNullOrEmpty(chartJson))
            {
                sb.AppendLine();
                sb.AppendLine("```chart-data");
                sb.AppendLine(chartJson);
                sb.AppendLine("```");
            }
        }

        return new ChatMessage(ChatRole.Tool, sb.ToString());
    }

    private static (string[] headers, List<string[]> rows, int totalRows) ParseTsv(string tsv, int maxRows)
    {
        var lines = tsv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return (Array.Empty<string>(), new List<string[]>(), 0);
        }

        var headers = lines[0].Split('\t');
        var totalRows = Math.Max(0, lines.Length - 1);
        var cap = Math.Max(0, maxRows);
        var rows = new List<string[]>();
        for (var i = 1; i < lines.Length && rows.Count < cap; i++)
        {
            rows.Add(lines[i].Split('\t'));
        }
        return (headers, rows, totalRows);
    }

    private static string BuildMarkdownTable(string[] headers, List<string[]> rows)
    {
        var sb = new StringBuilder();
        // header
        sb.AppendLine("| " + string.Join(" | ", headers.Select(EscapeMd)) + " |");
        // separator
        sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");
        // rows
        foreach (var r in rows)
        {
            var cells = headers.Select((_, idx) => idx < r.Length ? EscapeMd(r[idx]) : string.Empty);
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
        }
        return sb.ToString();
    }

    private static string EscapeMd(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input
            .Replace("|", "\\|")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string BuildChartDataJson(string[] headers, List<string[]> rows, KustoDisplayOptions options)
    {
        // Basic heuristic:
        // - x field must exist
        // - one or more numeric series fields exist
        var xField = options.XField;
        List<string> series = options?.SeriesFields?.ToList<string>() ?? new List<string>();

        if (string.IsNullOrWhiteSpace(xField))
        {
            // Default to first column as category
            xField = headers.Length > 0 ? headers[0] : string.Empty;
        }

        if (series.Count == 0)
        {
            // Default to numeric columns after the first
            for (var i = 1; i < headers.Length; i++)
            {
                if (rows.Any(r => TryParseDouble(GetCell(r, i, headers.Length), out _)))
                {
                    series.Add(headers[i]);
                }
            }
        }

        if (series.Count == 0)
        {
            return string.Empty;
        }

        // Build a normalized data array
        var hIndex = headers.Select((h, idx) => (h, idx)).ToDictionary(t => t.h, t => t.idx, StringComparer.OrdinalIgnoreCase);

        // Determine chart type from options (default to "line")
        var chartType = !string.IsNullOrEmpty(options?.ChartType) ? options.ChartType : "line";

        // For pie charts, use "label" and "value" structure
        if (chartType == "pie" && series.Count > 0)
        {
            var pieData = rows.Select(r => new
            {
                label = GetCellByHeader(r, hIndex, xField),
                value = TryParseDouble(GetCellByHeader(r, hIndex, series[0]), out var d) ? d : 0
            }).ToList();

            var piePayload = new
            {
                type = "pie",
                title = options?.ChartTitle ?? "Kusto Result",
                data = pieData
            };

            return System.Text.Json.JsonSerializer.Serialize(piePayload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false
            });
        }

        // For line/bar/area charts, use "name" and series structure
        var dataObjects = new List<Dictionary<string, object?>>();
        foreach (var r in rows)
        {
            var obj = new Dictionary<string, object?>();
            obj["name"] = GetCellByHeader(r, hIndex, xField);
            foreach (var s in series)
            {
                var valStr = GetCellByHeader(r, hIndex, s);
                obj[s] = TryParseDouble(valStr, out var d) ? d : null;
            }
            dataObjects.Add(obj);
        }

        var payload = new
        {
            type = chartType,
            title = options?.ChartTitle ?? "Kusto Result",
            data = dataObjects,
            xAxisLabel = xField,
            yAxisLabel = series.Count == 1 ? series[0] : "Value"
        };

        return System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private static List<string[]> DownsampleRows(List<string[]> rows, int maxPoints)
    {
        return (rows.Count <= maxPoints || maxPoints <= 0)
            ? rows
            : Enumerable.Range(0, maxPoints)
            .Select(i => rows[(int)Math.Floor((double)i * rows.Count / maxPoints)])
            .ToList();
    }

    private static string GetCellByHeader(string[] row, Dictionary<string, int> hIndex, string header)
    {
        if (hIndex.TryGetValue(header, out var idx))
        {
            return GetCell(row, idx, hIndex.Count);
        }
        return string.Empty;
    }

    private static string GetCell(string[] row, int index, int columnCount)
    {
        return index >= 0 && index < row.Length ? row[index] : string.Empty;
    }

    private static bool TryParseDouble(string? s, out double d)
    {
        return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d);
    }
}
