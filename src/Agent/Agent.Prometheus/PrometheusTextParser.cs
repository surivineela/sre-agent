namespace Agent.Prometheus;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class MetricFamily
{
    public required string Name { get; set; }
    public string Help { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<Metric> Metrics { get; set; } = new List<Metric>();
}

public class Metric
{
    public required string Name { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
    public double Value { get; set; }
}

// This class is responsible for parsing Prometheus exposition format text into a list of MetricFamily objects.
public static partial class PrometheusTextParser
{
    public static List<MetricFamily> Parse(string expositionText)
    {
        var metricFamilies = new List<MetricFamily>();
        MetricFamily? currentFamily = null;

        var lines = expositionText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        int lineNumber = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                // Skip empty lines and comments
                continue;
            }

            if (line.StartsWith("# HELP "))
            {
                var rest = line.Remove(0, "# HELP ".Length); // Remove "# HELP "
                var parts = rest.Split(' ', 2);
                currentFamily = new MetricFamily
                {
                    Name = parts[0],
                    Help = parts[1]
                };
                metricFamilies.Add(currentFamily);
            }
            else if (line.StartsWith("# TYPE "))
            {
                var rest = line.Remove(0, "# TYPE ".Length); // Remove "# TYPE "
                var parts = rest.Split(' ', 2);
                if (currentFamily != null && currentFamily.Name == parts[0])
                {
                    currentFamily.Type = parts[1];
                }
            }
            else
            {
                var match = MetricLineRegex().Match(line);
                if (match.Success)
                {
                    var metricName = match.Groups[1].Value;
                    var labels = match.Groups[2].Value;
                    var value = double.Parse(match.Groups[3].Value);

                    var metric = new Metric { Name = metricName, Value = value };

                    if (!string.IsNullOrEmpty(labels))
                    {
                        var labelPairs = LabelsMatcherRegex().Matches(labels.Trim('{', '}'));

                        if (labelPairs.Count == 0)
                        {
                            throw new FormatException($"Invalid label format: {metricName} at line {lineNumber}");
                        }

                        foreach (Match m in labelPairs)
                        {
                            if (m.Groups.Count != 3)
                            {
                                throw new FormatException($"Invalid label format: {metricName} at line {lineNumber}");
                            }
                            var labelKey = m.Groups[1].Value;
                            var labelValue = m.Groups[2].Value;
                            metric.Labels[labelKey] = labelValue;
                        }
                    }

                    currentFamily?.Metrics.Add(metric);
                }
            }
            ++lineNumber;
        }

        return metricFamilies;
    }

    [GeneratedRegex(@"^(\w+)(\{.*?\})?\s+([\d\.eE+-]+)$")]
    private static partial Regex MetricLineRegex();
    [GeneratedRegex(@"(\w+)\s*=\s*""((?:[^""\\]|\\.|,)*?)""")]
    private static partial Regex LabelsMatcherRegex();
}
