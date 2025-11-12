// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Plugins.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins.Implementation.CdbSDKDiagnosePlugin;

public class CdbSDKDiagnosePlugin : ICdbSDKDiagnosePlugin
{
    private readonly IAppInsightsPlugin _appInsightsPlugin;

    public CdbSDKDiagnosePlugin(IAppInsightsPlugin appInsightsPlugin)
    {
        _appInsightsPlugin = appInsightsPlugin;
    }

    public string SDKAnalyze(string error)
    {
        try
        {
            // Check if input contains multiple diagnostic entries (markdown format)
            if (error.Contains("# Cosmos DB SDK Diagnostic Logs Found"))
            {
                return AnalyzeMultipleDiagnostics(error);
            }

            // Single diagnostic analysis (existing logic)
            var cleanedJson = CleanJsonString(error);

            if (string.IsNullOrEmpty(cleanedJson))
            {
                return JsonConvert.SerializeObject(new { error = "No valid JSON found in input" });
            }

            var analysis = new DiagnosticsAnalysis(traceKusto: false);
            var result = analysis.Analyze(cleanedJson);

            if (result.Error != null)
            {
                return JsonConvert.SerializeObject(new { error = result.Error });
            }

            if (result.RCA != null)
            {
                return JsonConvert.SerializeObject(result.RCA);
            }

            return JsonConvert.SerializeObject(new { message = "No RCA produced" });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                error = $"SDKAnalyze failed: {ex.Message}",
                details = ex.ToString()
            });
        }
    }

    private string AnalyzeMultipleDiagnostics(string markdownContent)
    {
        try
        {
            var diagnosticJsonList = new List<string>();

            // Extract JSON blocks from markdown
            var jsonBlockPattern = @"```json\s*(.*?)\s*```";
            var matches = System.Text.RegularExpressions.Regex.Matches(
                markdownContent,
                jsonBlockPattern,
                System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var jsonContent = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(jsonContent))
                    {
                        diagnosticJsonList.Add(jsonContent);
                    }
                }
            }

            if (diagnosticJsonList.Count == 0)
            {
                return JsonConvert.SerializeObject(new { error = "No diagnostic JSON found in markdown content" });
            }

            // Limit to top 20 most relevant diagnostics
            // The diagnostics are already sorted by timestamp then score in ExtractCosmosDbDiagnosticJson
            var diagnosticsToAnalyze = diagnosticJsonList.Take(20).ToList();
            var totalDiagnostics = diagnosticJsonList.Count;

            var results = new List<object>();
            var analysis = new DiagnosticsAnalysis(traceKusto: false);

            for (int i = 0; i < diagnosticsToAnalyze.Count; i++)
            {
                try
                {
                    var cleanedJson = CleanJsonString(diagnosticsToAnalyze[i]);
                    if (string.IsNullOrEmpty(cleanedJson))
                    {
                        results.Add(new
                        {
                            entryNumber = i + 1,
                            error = "Invalid JSON format"
                        });
                        continue;
                    }

                    var result = analysis.Analyze(cleanedJson);

                    if (result.Error != null)
                    {
                        results.Add(new
                        {
                            entryNumber = i + 1,
                            error = result.Error
                        });
                    }
                    else if (result.RCA != null)
                    {
                        results.Add(new
                        {
                            entryNumber = i + 1,
                            rca = result.RCA
                        });
                    }
                    else
                    {
                        results.Add(new
                        {
                            entryNumber = i + 1,
                            message = "No RCA produced"
                        });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        entryNumber = i + 1,
                        error = $"Analysis failed: {ex.Message}"
                    });
                }
            }

            return JsonConvert.SerializeObject(new
            {
                totalDiagnosticsFound = totalDiagnostics,
                analyzedCount = diagnosticsToAnalyze.Count,
                message = totalDiagnostics > 5 ? $"Analyzed top 5 most relevant diagnostics out of {totalDiagnostics} found" : null,
                analyses = results
            }, Formatting.Indented);
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                error = $"Failed to analyze multiple diagnostics: {ex.Message}",
                details = ex.ToString()
            });
        }
    }

    private string CleanJsonString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        try
        {
            // First, try to parse it as-is (it might be valid JSON already)
            var parsed = JToken.Parse(input);
            return parsed.ToString(Formatting.None);
        }
        catch
        {
            // If parsing fails, try to clean it
            try
            {
                // Remove escaped quotes and unescape the string
                var unescaped = System.Text.RegularExpressions.Regex.Unescape(input);

                // Try to parse the unescaped version
                var parsed = JToken.Parse(unescaped);
                return parsed.ToString(Formatting.None);
            }
            catch
            {
                // Try to extract JSON from within the string
                try
                {
                    // Look for the first { and last } to extract just the JSON portion
                    int firstBrace = input.IndexOf('{');
                    if (firstBrace == -1)
                        return string.Empty;

                    // Find the matching closing brace
                    int braceCount = 0;
                    int lastBrace = -1;

                    for (int i = firstBrace; i < input.Length; i++)
                    {
                        if (input[i] == '{')
                            braceCount++;
                        else if (input[i] == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                lastBrace = i;
                                break;
                            }
                        }
                    }

                    if (lastBrace != -1)
                    {
                        var extracted = input.Substring(firstBrace, lastBrace - firstBrace + 1);
                        var unescaped = System.Text.RegularExpressions.Regex.Unescape(extracted);

                        // Validate it's valid JSON
                        var parsed = JToken.Parse(unescaped);
                        return parsed.ToString(Formatting.None);
                    }
                }
                catch
                {
                    // Last resort: return empty
                }
            }
        }

        return string.Empty;
    }

    public async Task<string> FetchCosmosDbSdkError(string appInsightsResourceId, string? timeSpan = "PT6H")
    {
        try
        {
            // Optimized query - only project needed columns to reduce data transfer
            var tracesQuery = @"
                traces
                | where timestamp > ago(6h)
                | where message contains ""Summary""
                | order by timestamp desc
                | sample 20
                | project timestamp, message";

            var tracesResult = await _appInsightsPlugin.QueryAppInsightsByResourceId(
                appInsightsResourceId,
                tracesQuery,
                timeSpan,
                formatAsTsv: false);

            // Parse and extract diagnostic JSON strings
            var extractedDiagnostics = ExtractCosmosDbDiagnosticJson(tracesResult);

            if (!string.IsNullOrEmpty(extractedDiagnostics))
            {
                return extractedDiagnostics;
            }

            // If no diagnostics found, return raw traces
            var combinedResult = new
            {
                summary = "Cosmos DB SDK telemetry from Application Insights",
                timeRange = timeSpan ?? "PT6H",
                diagnosticsFound = false,
                message = "No detailed Cosmos DB diagnostic JSON found. Showing raw traces.",
                traces = tracesResult
            };

            return JsonConvert.SerializeObject(combinedResult, Formatting.Indented);
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                error = $"Failed to fetch Cosmos DB SDK errors from Application Insights: {ex.Message}",
                details = ex.ToString()
            });
        }
    }

    public async Task<string> DiagnoseCosmosDbSdkErrors(string appInsightsResourceId, string? timeSpan = "PT6H")
    {
        try
        {
            // Step 1: Fetch Cosmos DB SDK errors from Application Insights
            var fetchResult = await FetchCosmosDbSdkError(appInsightsResourceId, timeSpan);

            if (string.IsNullOrEmpty(fetchResult))
            {
                return JsonConvert.SerializeObject(new
                {
                    error = "No data fetched from Application Insights",
                    step = "fetch"
                }, Formatting.Indented);
            }

            // Check if fetch returned an error
            try
            {
                var parsedFetch = JToken.Parse(fetchResult);
                var errorToken = parsedFetch["error"];
                if (errorToken != null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        error = "Failed to fetch diagnostics from Application Insights",
                        details = errorToken.ToString(),
                        step = "fetch"
                    }, Formatting.Indented);
                }
            }
            catch
            {
                // Not JSON, might be markdown - proceed with analysis
            }

            Console.WriteLine(fetchResult);

            // Step 2: Analyze the fetched diagnostics
            var analysisResult = SDKAnalyze(fetchResult);

            if (string.IsNullOrEmpty(analysisResult))
            {
                return JsonConvert.SerializeObject(new
                {
                    error = "Analysis produced no results",
                    step = "analyze",
                    fetchedData = fetchResult
                }, Formatting.Indented);
            }

            // Step 3: Combine results into comprehensive output
            try
            {
                var parsedAnalysis = JToken.Parse(analysisResult);

                // Check if analysis returned an error
                var analysisErrorToken = parsedAnalysis["error"];
                if (analysisErrorToken != null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        warning = "Analysis completed with errors",
                        analysisError = analysisErrorToken.ToString(),
                        step = "analyze",
                        rawFetchResult = fetchResult
                    }, Formatting.Indented);
                }

                // Success - return the complete diagnosis
                var result = new
                {
                    status = "success",
                    message = "End-to-end Cosmos DB SDK diagnostics completed successfully",
                    appInsightsResourceId = appInsightsResourceId,
                    timeRange = timeSpan ?? "PT6H",
                    analysis = parsedAnalysis
                };

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                // If we can't parse the analysis result, return it as-is with context
                return JsonConvert.SerializeObject(new
                {
                    status = "partial_success",
                    message = "Diagnostics fetched and analyzed, but result format is non-standard",
                    appInsightsResourceId = appInsightsResourceId,
                    timeRange = timeSpan ?? "PT6H",
                    analysisResult = analysisResult,
                    parseError = ex.Message
                }, Formatting.Indented);
            }
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                error = $"End-to-end diagnosis failed: {ex.Message}",
                details = ex.ToString(),
                step = "overall"
            }, Formatting.Indented);
        }
    }

    private string ExtractCosmosDbDiagnosticJson(params string[] queryResults)
    {
        var diagnosticJsonList = new List<(string json, int score, DateTime? timestamp)>();
        var sb = new StringBuilder();

        foreach (var result in queryResults)
        {
            if (string.IsNullOrEmpty(result))
                continue;

            try
            {
                // Try to parse as JSON
                var parsed = JToken.Parse(result);

                // Check if this is an Application Insights response with tables
                if (parsed["tables"] is JArray tables)
                {
                    foreach (var table in tables)
                    {
                        var columns = table["columns"] as JArray;
                        var rows = table["rows"] as JArray;

                        if (columns == null || rows == null)
                            continue;

                        // Find the index of the "message" and "timestamp" columns
                        int messageIndex = -1;
                        int timestampIndex = -1;
                        for (int i = 0; i < columns.Count; i++)
                        {
                            var columnName = columns[i]["name"]?.ToString();
                            if (columnName == "message")
                            {
                                messageIndex = i;
                            }
                            else if (columnName == "timestamp")
                            {
                                timestampIndex = i;
                            }
                        }

                        if (messageIndex == -1)
                            continue;

                        // Process each row
                        foreach (var row in rows)
                        {
                            if (row is JArray rowArray && rowArray.Count > messageIndex)
                            {
                                var message = rowArray[messageIndex]?.ToString();
                                DateTime? timestamp = null;

                                // Extract timestamp if available
                                if (timestampIndex != -1 && rowArray.Count > timestampIndex)
                                {
                                    var timestampStr = rowArray[timestampIndex]?.ToString();
                                    if (!string.IsNullOrEmpty(timestampStr) && DateTime.TryParse(timestampStr, out var parsedTimestamp))
                                    {
                                        timestamp = parsedTimestamp;
                                    }
                                }

                                if (!string.IsNullOrEmpty(message) && message.Contains("\"Summary\""))
                                {
                                    // Extract JSON from the message
                                    var extracted = ExtractJsonFromMessage(message);
                                    if (!string.IsNullOrEmpty(extracted))
                                    {
                                        // Score the diagnostic based on richness
                                        int score = ScoreDiagnostic(extracted);
                                        if (score > 0)
                                        {
                                            diagnosticJsonList.Add((extracted, score, timestamp));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Try the old extraction method for other formats
                    if (parsed is JArray array)
                    {
                        foreach (var item in array)
                        {
                            ExtractDiagnosticsFromItem(item, diagnosticJsonList);
                        }
                    }
                    else if (parsed is JObject obj)
                    {
                        ExtractDiagnosticsFromItem(obj, diagnosticJsonList);
                    }
                }
            }
            catch
            {
                // If not JSON, try to find JSON strings in the text
                var jsonMatches = System.Text.RegularExpressions.Regex.Matches(
                    result,
                    @"\{[^\}]*""Summary""[^\}]*\{[^\}]*""DirectCalls""[^}]*\}[^}]*\}",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                foreach (System.Text.RegularExpressions.Match match in jsonMatches)
                {
                    var cleaned = CleanJsonString(match.Value);
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        int score = ScoreDiagnostic(cleaned);
                        if (score > 0)
                        {
                            diagnosticJsonList.Add((cleaned, score, null));
                        }
                    }
                }
            }
        }

        if (diagnosticJsonList.Count > 0)
        {
            // Sort by timestamp (most recent first), then by score as secondary sort
            var sortedDiagnostics = diagnosticJsonList
                .OrderByDescending(d => d.timestamp ?? DateTime.MinValue)
                .ThenByDescending(d => d.score)
                .Select(d => d.json)
                .Distinct()
                .ToList();

            sb.AppendLine("# Cosmos DB SDK Diagnostic Logs Found");
            sb.AppendLine($"Total diagnostic entries: {sortedDiagnostics.Count}");
            sb.AppendLine();

            for (int i = 0; i < sortedDiagnostics.Count; i++)
            {
                sb.AppendLine($"## Diagnostic Entry {i + 1}");
                sb.AppendLine("```json");

                try
                {
                    var prettified = JToken.Parse(sortedDiagnostics[i]).ToString(Formatting.Indented);
                    sb.AppendLine(prettified);
                }
                catch
                {
                    sb.AppendLine(sortedDiagnostics[i]);
                }

                sb.AppendLine("```");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        return string.Empty;
    }

    private int ScoreDiagnostic(string jsonString)
    {
        try
        {
            var parsed = JToken.Parse(jsonString);
            int score = 0;

            // Check for empty or minimal Summary
            var summary = parsed["Summary"];
            if (summary != null)
            {
                // Empty summary or only has empty objects = low value
                if (summary.Type == JTokenType.Object && !summary.HasValues)
                {
                    return 0; // Skip diagnostics with empty Summary
                }

                // Has DirectCalls = good
                var directCalls = summary["DirectCalls"];
                if (directCalls != null && directCalls.HasValues)
                {
                    score += 50;
                }

                // Has GatewayCalls = good
                var gatewayCalls = summary["GatewayCalls"];
                if (gatewayCalls != null && gatewayCalls.HasValues)
                {
                    score += 30;
                }
            }
            else
            {
                return 0; // No Summary at all = skip
            }

            // Check for Client Configuration
            var data = parsed["data"];
            if (data != null)
            {
                if (data["Client Configuration"] != null)
                {
                    score += 40;
                }

                // Check for System Info
                if (data["System Info"] != null)
                {
                    score += 30;
                }
            }

            // Check for children (request pipeline details)
            var children = parsed["children"];
            if (children != null && children.HasValues)
            {
                score += 20;
            }

            // Check for detailed operation name
            var name = parsed["name"]?.ToString();
            if (!string.IsNullOrEmpty(name) &&
                name != "Account Read" && // Generic operation = less valuable
                (name.Contains("ItemAsync") || name.Contains("Create") || name.Contains("Query")))
            {
                score += 10;
            }

            return score;
        }
        catch
        {
            return 0; // Invalid JSON = skip
        }
    }

    private string ExtractJsonFromMessage(string message)
    {
        try
        {
            // Look for JSON with "Summary" field anywhere in the message
            // Find the first occurrence of {"Summary"
            int summaryStart = message.IndexOf("{\"Summary\"", StringComparison.Ordinal);
            if (summaryStart == -1)
            {
                // Try with escaped quotes
                summaryStart = message.IndexOf("{\\\"Summary\\\"", StringComparison.Ordinal);
            }

            if (summaryStart != -1)
            {
                // Start from the opening brace
                int firstBrace = message.LastIndexOf('{', summaryStart);
                if (firstBrace != -1)
                {
                    // Find the matching closing brace
                    int braceCount = 0;
                    int lastBrace = -1;

                    for (int i = firstBrace; i < message.Length; i++)
                    {
                        if (message[i] == '{')
                            braceCount++;
                        else if (message[i] == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                lastBrace = i;
                                break;
                            }
                        }
                    }

                    if (lastBrace != -1)
                    {
                        var extracted = message.Substring(firstBrace, lastBrace - firstBrace + 1);
                        var cleaned = CleanJsonString(extracted);
                        if (!string.IsNullOrEmpty(cleaned))
                        {
                            // Validate it has Summary
                            if (cleaned.Contains("\"Summary\""))
                            {
                                return cleaned;
                            }
                        }
                    }
                }
            }

            // Fallback: Try generic patterns for any JSON with Diagnostics
            var patterns = new[]
            {
                @"Diagnostics:\s*(\{.*\})$",
                @"(\{.*""Summary"".*\})"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    message,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                if (match.Success && match.Groups.Count > 1)
                {
                    var jsonStr = match.Groups[1].Value;
                    var cleaned = CleanJsonString(jsonStr);
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        return cleaned;
                    }
                }
            }
        }
        catch
        {
            // Ignore extraction errors
        }

        return string.Empty;
    }

    private void ExtractDiagnosticsFromItem(JToken item, List<(string json, int score, DateTime? timestamp)> diagnosticJsonList)
    {
        try
        {
            // Try to extract timestamp from the item
            DateTime? timestamp = null;
            var timestampToken = item["timestamp"];
            if (timestampToken != null)
            {
                var timestampStr = timestampToken.ToString();
                if (!string.IsNullOrEmpty(timestampStr) && DateTime.TryParse(timestampStr, out var parsedTimestamp))
                {
                    timestamp = parsedTimestamp;
                }
            }

            // Check if the item itself is a diagnostic JSON (has Summary, name, etc.)
            if (item["Summary"] != null || item["Client Configuration"] != null || item["name"] != null)
            {
                var jsonStr = item.ToString(Formatting.None);
                int score = ScoreDiagnostic(jsonStr);
                if (score > 0)
                {
                    diagnosticJsonList.Add((jsonStr, score, timestamp));
                }
                return;
            }

            // Look for diagnostic JSON in common fields
            var fieldsToCheck = new[] { "message", "cosmosJson", "diagnosticData", "data", "details" };

            foreach (var field in fieldsToCheck)
            {
                var value = item[field];
                if (value != null && value.Type == JTokenType.String)
                {
                    var stringValue = value.ToString();

                    // Check if it looks like a Cosmos DB diagnostic JSON by looking for Summary
                    if (stringValue.Contains("\"Summary\""))
                    {
                        // Clean the JSON string before adding
                        var cleaned = CleanJsonString(stringValue);
                        if (!string.IsNullOrEmpty(cleaned))
                        {
                            int score = ScoreDiagnostic(cleaned);
                            if (score > 0)
                            {
                                diagnosticJsonList.Add((cleaned, score, timestamp));
                            }
                        }
                    }
                }
                else if (value != null && value.Type == JTokenType.Object)
                {
                    // Check nested object
                    if (value["Summary"] != null || value["Client Configuration"] != null)
                    {
                        var jsonStr = value.ToString(Formatting.None);
                        int score = ScoreDiagnostic(jsonStr);
                        if (score > 0)
                        {
                            diagnosticJsonList.Add((jsonStr, score, timestamp));
                        }
                    }
                }
            }

            // Check customDimensions
            var customDims = item["customDimensions"];
            if (customDims != null && customDims.Type == JTokenType.Object)
            {
                foreach (var prop in ((JObject)customDims).Properties())
                {
                    if (prop.Value.Type == JTokenType.String)
                    {
                        var stringValue = prop.Value.ToString();
                        if (stringValue.Contains("\"Summary\""))
                        {
                            // Clean the JSON string before adding
                            var cleaned = CleanJsonString(stringValue);
                            if (!string.IsNullOrEmpty(cleaned))
                            {
                                int score = ScoreDiagnostic(cleaned);
                                if (score > 0)
                                {
                                    diagnosticJsonList.Add((cleaned, score, timestamp));
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Skip items that can't be processed
        }
    }
}
