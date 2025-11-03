// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ScottPlot;

namespace Agent.Plugins.Implementation
{
    public class MetricsAnalysisPlugin : IMetricsAnalysisPlugin
    {
        private readonly IChatCompletionService _chatCompletionService;
        private readonly ILogger<MetricsAnalysisPlugin> _logger;
        private readonly ISessionPoolService _sessionPoolService;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly string _tempDirectory;

        public Guid? ThreadId { get; set; }

        public MetricsAnalysisPlugin(
            IChatCompletionService chatCompletionService,
            ILogger<MetricsAnalysisPlugin> logger,
            ISessionPoolService sessionPoolService,
            IHostEnvironment hostEnvironment)
        {
            _chatCompletionService = chatCompletionService;
            _logger = logger;
            _sessionPoolService = sessionPoolService;
            _hostEnvironment = hostEnvironment;
            _tempDirectory = Path.Combine(Path.GetTempPath(), "MetricsAnalysis");
            Directory.CreateDirectory(_tempDirectory);
        }

        public async Task<DimensionFilter[]> GenerateFiltersAsync(
            string symptoms,
            string resourceDetails,
            string metricName,
            string[] dimensions)
        {
            _logger.LogInternalInformation($"Generating filters for metric '{metricName}' based on symptoms: {symptoms}, resource: {resourceDetails}");

            try
            {
                var dimensionsList = string.Join(", ", dimensions);

                var prompt = @$"You are an expert SRE analyzing system symptoms to determine relevant metric dimension filters.

**Task**: Based on the symptoms and resource details described, suggest dimension filters that would help narrow down the metric data to the most relevant scope.

**Symptoms**: {symptoms}

**Resource Details**: {resourceDetails}

**Metric Name**: {metricName}

**Available Dimensions**: {dimensionsList}

**Instructions**:
1. Carefully analyze the symptoms to identify key entities mentioned (e.g., region names, resource names, application names, environment names, etc.)
2. Match these entities to the available dimensions
3. For each relevant dimension, suggest specific values based on the symptoms
4. Only include dimensions that are explicitly mentioned or strongly implied in the symptoms
5. If no specific values can be determined from the symptoms, do not include that dimension

**Output Format**: Return ONLY a valid JSON array of dimension filters. Each filter should have ""Dimension"" and ""Value"" properties.

Example output format:
[
  {{""Dimension"": ""Region"", ""Value"": ""WestUS""}},
  {{""Dimension"": ""AppName"", ""Value"": ""my-application""}}
]

If no relevant filters can be determined from the symptoms, return an empty array: []

Return ONLY the JSON array, no additional text or explanation.";

                var chatHistory = new ChatHistory();
                chatHistory.AddUserMessage(prompt);

                var response = await _chatCompletionService.GetChatMessageContentAsync(chatHistory);
                var responseText = response.Content?.Trim() ?? "[]";

                // Remove markdown code blocks if present
                if (responseText.StartsWith("```json"))
                {
                    responseText = responseText.Substring(7);
                }
                else if (responseText.StartsWith("```"))
                {
                    responseText = responseText.Substring(3);
                }

                if (responseText.EndsWith("```"))
                {
                    responseText = responseText.Substring(0, responseText.Length - 3);
                }

                responseText = responseText.Trim();

                // Parse the JSON response
                var filters = JsonSerializer.Deserialize<DimensionFilter[]>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInternalInformation($"Generated {filters?.Length ?? 0} dimension filters");
                return filters ?? Array.Empty<DimensionFilter>();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error generating filters");
                // Return empty array on error rather than throwing
                return Array.Empty<DimensionFilter>();
            }
        }

        public async Task<MetricsAnalysisResult> AnalyzeMetricsAsync(
            string symptoms,
            TimeSeries[] timeSeries)
        {
            _logger.LogInternalInformation($"Starting metrics analysis for symptoms: {symptoms}");

            try
            {
                var metricsToCheck = timeSeries.Select(ts => ts.MetricName).ToArray();

                // Step 1: Serialize time series data
                var metricsJson = SerializeTimeSeries(timeSeries);

                // Step 2a: Direct LLM analysis
                var directAnalysis = await PerformDirectLLMAnalysisAsync(symptoms, metricsToCheck, metricsJson);

                // Step 2b: Statistical/ML analysis with Python
                var statisticalAnalysis = await PerformStatisticalAnalysisAsync(symptoms, metricsToCheck, metricsJson);

                // Step 2c: Visualization and multimodal analysis
                var visualizationAnalysis = await PerformVisualizationAnalysisAsync(symptoms, timeSeries);

                // Step 3: Combine results and generate summary
                var combined = await CombineResultsAndSummarizeAsync(
                    symptoms,
                    directAnalysis,
                    statisticalAnalysis,
                    visualizationAnalysis);

                var result = new MetricsAnalysisResult(
                    directAnalysis,
                    statisticalAnalysis,
                    visualizationAnalysis,
                    combined);

                _logger.LogInternalInformation("Metrics analysis completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error during metrics analysis");
                throw;
            }
        }

        private string SerializeTimeSeries(TimeSeries[] timeSeries)
        {
            // convert the letter case to camelCase when serializing
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            return JsonSerializer.Serialize(timeSeries, options);
        }

        private async Task<string> PerformDirectLLMAnalysisAsync(
            string symptoms,
            string[] metricsToCheck,
            string metricsJson)
        {
            _logger.LogInternalInformation("Performing direct LLM analysis");

            var prompt = $@"You are an expert metrics analyst. Analyze the following metrics data and provide insights.

**Symptoms**: {symptoms}

**Metrics to Analyze**: {string.Join(", ", metricsToCheck)}

**Raw Metrics Data**:
```json
{metricsJson}
```

Please provide general insights about the metrics patterns. Use bullets and be as concise as possible.";

            var response = await _chatCompletionService.GetChatMessageContentAsync(prompt);
            var content = response?.Content ?? "No analysis available";

            return content;
        }

        private async Task<string> PerformStatisticalAnalysisAsync(
            string symptoms,
            string[] metricsToCheck,
            string metricsJson)
        {
            _logger.LogInternalInformation("Performing statistical analysis with Python code generation");

            // Upload the metrics JSON file to the session
            var identifier = BuildIdentifier();
            var filename = "metrics_data.json";
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(metricsJson);

            try
            {
                await _sessionPoolService.UploadSessionFileAsync(identifier, filename, jsonBytes);
                _logger.LogInternalInformation($"Uploaded metrics data file to session: {filename}");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to upload metrics data file");
                throw;
            }

            var prompt = $@"You are an expert data scientist. Generate Python code to perform statistical and machine learning analysis on metrics data.

**Symptoms**: {symptoms}

**Metrics to Analyze**: {string.Join(", ", metricsToCheck)}

The metrics data is available in a file called '{filename}' in /mnt/data.

**Example data format**:
```json
[
  {{
    ""metricName"": ""CPU_Usage"",
    ""unit"": ""Percentage"",
    ""aggregation"": ""Max"",
    ""dimensions"": {{
      ""Instance"": ""AppServer1"",
      ""Region"": ""East US""
    }},
    ""dataPoints"": [
      {{""timestamp"": ""2024-01-15T10:00:00Z"", ""value"": 45.2}},
      {{""timestamp"": ""2024-01-15T10:05:00Z"", ""value"": 52.1}}
    ]
  }},
  {{
    ""metricName"": ""Memory_Usage"",
    ""unit"": ""Bytes"",
    ""aggregation"": ""Average"",
    ""dimensions"": {{
      ""Instance"": ""AppServer2"",
      ""Region"": ""East US 2""
    }},
    ""dataPoints"": [
      {{""timestamp"": ""2024-01-15T10:00:00Z"", ""value"": 1024.5}},
      {{""timestamp"": ""2024-01-15T10:05:00Z"", ""value"": 1128.3}}
    ]
  }}
]
```

Generate Python code that:
1. Loads and parses the JSON data from the file '/mnt/data/{filename}'
2. Detects anomalies using appropriate statistical and machine learning methods
3. Prints the statistical indicators and the anomalies count in a clear format. And the anomaly list should be concise (example line: `2025-10-22T09:27:00Z, 522.571`)

Requirements:
- Use only standard libraries (json, statistics, math) and numpy/pandas/scipy/scikit-learn if needed
- Handle missing or invalid data gracefully
- Output results as structured text
- Do NOT create any plots or visualizations

Return ONLY the Python code, no explanations.";

            var codeResponse = await _chatCompletionService.GetChatMessageContentAsync(prompt);
            var pythonCode = ExtractPythonCode(codeResponse?.Content ?? "");

            // Execute the Python code
            var executionOutput = await ExecutePythonCodeAsync(pythonCode);

            return executionOutput;
        }

        private async Task<string> PerformVisualizationAnalysisAsync(
            string symptoms,
            TimeSeries[] timeSeries)
        {
            _logger.LogInternalInformation("Performing visualization and multimodal analysis");

            var metricsToCheck = timeSeries.Select(ts => ts.MetricName).ToArray();

            // Analyze the chart with multimodal LLM (GPT-4o)
            var multimodalPrompt = $@"You are an expert metrics analyst. Analyze this metrics visualization chart.

**Symptoms**: {symptoms}

**Metrics Shown**: {string.Join(", ", metricsToCheck)}

Please identify:
1. Visual patterns and trends
2. Anomalies or spikes
3. Any concerning patterns related to the symptoms

ONLY state factual observations based on what you see in the chart. Don't provide any recommendations.";
            var messages = new ChatMessageContentItemCollection
            {
                new TextContent(multimodalPrompt)
            };

            foreach (var ts in timeSeries)
            {
                // Generate line chart using ScottPlot
                var chartPath = Path.Combine(_tempDirectory, $"metrics_chart_{Guid.NewGuid()}.png");
                var chartBytes = GenerateLineChart(ts, chartPath);
                messages.Add(new ImageContent(chartBytes, "image/png"));
            }

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(messages);

            var multimodalResponse = await _chatCompletionService.GetChatMessageContentAsync(chatHistory);
            var response = multimodalResponse?.Content ?? "No visual analysis available";

            return response;
        }

        private byte[] GenerateLineChart(
            TimeSeries series,
            string outputPath)
        {
            try
            {
                var plot = new Plot();

                // Create line plots for each time series
                if (series.DataPoints.Count > 0)
                {
                    var timestamps = series.DataPoints.Select(dp => dp.Timestamp.ToOADate()).ToArray();
                    var values = series.DataPoints.Select(dp => dp.Value).ToArray();

                    var scatter = plot.Add.Scatter(timestamps, values);
                    scatter.LegendText = series.FormatDimensions();
                    scatter.LineWidth = 2;
                }

                plot.Title(series.MetricName + (string.IsNullOrEmpty(series.Unit) ? string.Empty : $" ({series.Unit})"));
                plot.XLabel("Time");
                plot.YLabel("Value");
                plot.ShowLegend(Edge.Bottom);
                plot.Axes.DateTimeTicksBottom();

                plot.SavePng(outputPath, 1200, 800);
                _logger.LogInternalInformation($"Chart saved to {outputPath}");
                return plot.GetImageBytes(1200, 800, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error generating chart");
                throw;
            }
        }

        private async Task<string> CombineResultsAndSummarizeAsync(
            string symptoms,
            string directAnalysis,
            string statisticalAnalysis,
            string visualizationAnalysis)
        {
            _logger.LogInternalInformation("Combining results and generating summary");

            var prompt = $@"You are an expert metrics analyst. Combine the following three analysis results into a comprehensive summary.

<symptoms>
{symptoms}
</symptoms>

<direct_llm_analysis>
{directAnalysis}
</direct_llm_analysis>

<statistical_analysis>
{statisticalAnalysis}
</statistical_analysis>

<visualization_analysis>
{visualizationAnalysis}
</visualization_analysis>

Please provide:
1. A concise summary that synthesizes all three analyses
2. A list of factual observations that are supported by the data
3. If there are conflicting findings, note them clearly

Format your response as:

## Summary
[Your summary here]

## Factual Observations
- [Observation 1]
- [Observation 2]
...";

            var response = await _chatCompletionService.GetChatMessageContentAsync(prompt);
            var content = response?.Content ?? "";

            return content;
        }

        private async Task<string> ExecutePythonCodeAsync(string pythonCode)
        {
            try
            {
                var identifier = BuildIdentifier();
                var timeoutSeconds = 120; // 2 minutes timeout for statistical analysis

                var execResp = await _sessionPoolService.ExecutePythonInlineAsync(pythonCode, identifier, timeoutSeconds);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"ExitCode: {execResp.ExitCode?.ToString() ?? "(n/a)"}");
                if (!string.IsNullOrWhiteSpace(execResp.Stdout))
                {
                    sb.AppendLine("STDOUT:");
                    sb.AppendLine(execResp.Stdout);
                }
                if (!string.IsNullOrWhiteSpace(execResp.Stderr))
                {
                    sb.AppendLine("STDERR:");
                    sb.AppendLine(execResp.Stderr);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error executing Python code");
                return $"Error executing Python: {ex.Message}";
            }
        }

        /// <summary>
        /// Builds a stable session identifier for the code interpreter pool using the agent name and thread id.
        /// This ensures all python analysis code for the same (agent, thread) reuses the same underlying session when supported by the pool.
        /// </summary>
        private string BuildIdentifier()
        {
            var agentName = AgentNameHelper.GetAgentName(!_hostEnvironment.IsDevelopment());
            var threadId = ThreadId?.ToString() ?? Guid.NewGuid().ToString();
            return _sessionPoolService.BuildSessionIdentifier(agentName, threadId, false);
        }

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value.Substring(0, max) + "...<truncated>";

        private string ExtractPythonCode(string llmResponse)
        {
            // Extract code from markdown code blocks
            var codeBlockPattern = @"```python\s*(.*?)\s*```";
            var match = System.Text.RegularExpressions.Regex.Match(
                llmResponse,
                codeBlockPattern,
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // If no code block, try to find code without markers
            var altPattern = @"```\s*(.*?)\s*```";
            match = System.Text.RegularExpressions.Regex.Match(
                llmResponse,
                altPattern,
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // Return as-is if no code block found
            return llmResponse.Trim();
        }
    }
}
