// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Helpers.JmesPath;
using Agent.Core.Interfaces;
using Agent.Core.JsonConverters;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Plugin for accessing and manipulating stored tool outputs
/// </summary>
public class ToolOutputRetrieverPlugin : IToolOutputRetrieverPlugin
{
    private readonly IToolOutputStorage _toolOutputStorage;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly ILogger<ToolOutputRetrieverPlugin> _logger;
    private readonly int _maxOutputChars;

    public ToolOutputRetrieverPlugin(
        IToolOutputStorage toolOutputStorage,
        IChatClientProvider chatClientProvider,
        ILogger<ToolOutputRetrieverPlugin> logger,
        IOptions<ToolOutputSettings> toolOutputSettings)
    {
        _toolOutputStorage = toolOutputStorage;
        _chatClientProvider = chatClientProvider;
        _logger = logger;
        _maxOutputChars = toolOutputSettings.Value.MaxOutputChars;
    }

    /// <inheritdoc/>
    public async Task<string> RetrieveToolOutputAsync(ToolOutputRetrieverOptions options)
    {
        try
        {
            _logger.LogInternalInformation(
                "RetrieveToolOutput called with fileKey={FileKey}, operation={Operation}",
                options.FileKey, options.Operation);

            // Validate and get file path
            var filePath = _toolOutputStorage.EnsureFileExist(options.FileKey);
            if (string.IsNullOrEmpty(filePath))
            {
                return $"Error: File with key '{options.FileKey}' not found in storage.";
            }

            // Route to appropriate operation handler
            return options.Operation?.ToLowerInvariant() switch
            {
                "read_by_line" => await ReadByLineAsync(filePath, options.LineStart, options.LineEnd),
                "read_by_offset" => await ReadByOffsetAsync(filePath, options.OffsetStart, options.OffsetEnd),
                "summarize" => await SummarizeAsync(filePath, options.SummaryPrompt),
                "filter_structured" => await FilterStructuredAsync(filePath, options.JmesPath),
                "search_regex" => await SearchRegexAsync(filePath, options.RegexPattern, options.RegexFlags, options.RegexMaxMatches ?? 100),
                _ => $"Error: Unknown operation '{options.Operation}'. Supported operations: read_by_line, read_by_offset, summarize, filter_structured, search_regex"
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in RetrieveToolOutput for fileKey={FileKey}, operation={Operation}", options.FileKey, options.Operation);
            return $"Error: {ex.Message}";
        }
    }

    private string DetermineContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            _ => "text"
        };
    }

    private async Task<string> ReadByLineAsync(string filePath, int? lineStart, int? lineEnd)
    {
        if (!lineStart.HasValue || lineStart.Value < 1)
        {
            return "Error: lineStart is required and must be >= 1 for read_by_line operation.";
        }

        // Default lineEnd to 50 lines from start if not specified
        lineEnd ??= lineStart.Value + 49; // +49 because we want 50 lines total (inclusive)

        try
        {
            var startIndex = lineStart.Value - 1; // Convert to 0-based
            var endIndex = lineEnd.Value - 1; // Convert to 0-based
            var currentLineNumber = 0;
            var selectedLines = new List<string>();

            await foreach (var line in File.ReadLinesAsync(filePath))
            {
                // Check if we've reached the start of the range
                if (currentLineNumber >= startIndex)
                {
                    selectedLines.Add(line);

                    // Break when we've reached the end of the range
                    if (currentLineNumber >= endIndex)
                    {
                        break;
                    }
                }

                currentLineNumber++;
            }

            // Validate that we found any lines
            if (selectedLines.Count == 0)
            {
                return startIndex == 0
                    ? "Error: File is empty."
                    : $"Error: lineStart ({lineStart.Value}) exceeds total lines in file.";
            }

            var content = string.Join(Environment.NewLine, selectedLines);

            var result = new StringBuilder();
            result.AppendLine($"lineStart: {lineStart.Value}");
            result.AppendLine($"lineEnd: {Math.Min(currentLineNumber + 1, lineEnd.Value)}");
            result.AppendLine("Content:");
            result.AppendLine(content);

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error reading file by line: {FilePath}", filePath);
            return $"Error reading file: {ex.Message}";
        }
    }

    private async Task<string> ReadByOffsetAsync(string filePath, long? offsetStart, long? offsetEnd)
    {
        if (!offsetStart.HasValue || offsetStart.Value < 0)
        {
            return "Error: offsetStart is required and must be >= 0 for read_by_offset operation.";
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Length;

            var start = offsetStart.Value;
            // Default offsetEnd to offsetStart + _maxOutputChars, capped at file size
            var end = offsetEnd.HasValue ? Math.Min(offsetEnd.Value, fileSize) : Math.Min(start + _maxOutputChars, fileSize);

            if (start >= fileSize)
            {
                return $"Error: offsetStart ({start}) exceeds file size ({fileSize} bytes).";
            }

            if (start > end)
            {
                return $"Error: offsetStart ({start}) is greater than offsetEnd ({end}).";
            }

            var bytesToRead = (int)(end - start);
            var buffer = new byte[bytesToRead];

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                stream.Seek(start, SeekOrigin.Begin);
                var bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead);

                var content = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                var result = new StringBuilder();
                result.AppendLine($"offsetStart: {start}");
                result.AppendLine($"offsetEnd: {start + bytesRead}");
                result.AppendLine("Content:");
                result.AppendLine(content);

                return result.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error reading file by offset: {FilePath}", filePath);
            return $"Error reading file: {ex.Message}";
        }
    }

    private async Task<string> SummarizeAsync(
        string filePath,
        string? summaryPrompt)
    {
        if (string.IsNullOrWhiteSpace(summaryPrompt))
        {
            return "Error: summaryPrompt is required for summarize operation.";
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath);

            // Use LLM to summarize
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, $"{summaryPrompt} Content: {content}")
            };

            var response = await _chatClientProvider.LargeContextModel.GetResponseAsync(messages);
            return response.Messages.Last().Text ?? "No summary generated.";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error summarizing file: {FilePath}", filePath);
            return $"Error summarizing file: {ex.Message}";
        }
    }

    private async Task<string> FilterStructuredAsync(string filePath, string? jmesPath)
    {
        if (string.IsNullOrWhiteSpace(jmesPath))
        {
            return "Error: jmesPath is required for filter_structured operation.";
        }

        try
        {
            var contentType = DetermineContentType(filePath);
            var content = await File.ReadAllTextAsync(filePath);
            JsonElement jsonData;

            // Convert to JSON if necessary
            if (contentType == "yaml")
            {
                jsonData = YamlJsonConverter.ConvertYamlToJsonElement(content);
            }
            else if (contentType == "json")
            {
                jsonData = JsonDocument.Parse(content).RootElement;
            }
            else
            {
                return "Error: filter_structured operation requires JSON or YAML file format.";
            }

            // Apply JMESPath query
            var result = JmesPath.Query(jmesPath, jsonData);

            // Convert result back to original format
            string output;
            if (contentType == "yaml")
            {
                output = YamlJsonConverter.ConvertJsonElementToYaml(result);
            }
            else
            {
                output = JsonSerializer.Serialize(result);
            }

            return output;
        }
        catch (JsonException ex)
        {
            _logger.LogInternalError(ex, "Error parsing file as JSON/YAML: {FilePath}", filePath);
            return $"Error parsing file: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error filtering structured data: {FilePath}", filePath);
            return $"Error filtering data: {ex.Message}";
        }
    }

    private async Task<string> SearchRegexAsync(string filePath, string? regexPattern, string? regexFlags, int maxMatches)
    {
        if (string.IsNullOrWhiteSpace(regexPattern))
        {
            return "Error: regexPattern is required for search_regex operation.";
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath);

            // Parse regex flags
            var options = RegexOptions.None;
            if (!string.IsNullOrEmpty(regexFlags))
            {
                if (regexFlags.Contains('i')) options |= RegexOptions.IgnoreCase;
                if (regexFlags.Contains('m')) options |= RegexOptions.Multiline;
                if (regexFlags.Contains('s')) options |= RegexOptions.Singleline;
            }

            var regex = new Regex(regexPattern, options);
            var matches = regex.Matches(content);

            if (matches.Count == 0)
            {
                return $"No matches found for pattern: {regexPattern}";
            }

            var result = new StringBuilder();
            result.AppendLine($"Total matches: {matches.Count}");
            result.AppendLine();

            var actualMatchesShown = 0;
            for (int i = 0; i < Math.Min(matches.Count, maxMatches); i++)
            {
                var match = matches[i];
                var lineNumber = GetLineNumber(content, match.Index);
                var columnNumber = GetColumnNumber(content, match.Index);

                result.AppendLine($"**Match {i + 1}** (Line {lineNumber}, Column {columnNumber}, Offset {match.Index}):");
                result.AppendLine();
                actualMatchesShown = i + 1;
            }

            if (matches.Count > maxMatches)
            {
                result.AppendLine($"... Showing {actualMatchesShown} of {matches.Count} matches (limited by regexMaxMatches={maxMatches})");
            }

            return result.ToString();
        }
        catch (ArgumentException ex)
        {
            _logger.LogInternalError(ex, "Invalid regex pattern: {Pattern}", regexPattern);
            return $"Error: Invalid regex pattern - {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error searching with regex: {FilePath}", filePath);
            return $"Error searching file: {ex.Message}";
        }
    }

    private int GetLineNumber(string content, int index)
    {
        var lineNumber = 1;
        for (int i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                lineNumber++;
            }
        }
        return lineNumber;
    }

    private int GetColumnNumber(string content, int index)
    {
        var column = 1;
        for (int i = index - 1; i >= 0; i--)
        {
            if (content[i] == '\n')
            {
                break;
            }
            column++;
        }
        return column;
    }
}
