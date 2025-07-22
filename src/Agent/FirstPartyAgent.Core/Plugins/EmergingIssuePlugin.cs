using System.ComponentModel;
using System.Text;
using System.Text.Json;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace FirstPartyAgent.Core.Plugins;

/// <summary>
/// Plugin for detecting emerging issues by comparing ICM content with emerging issue configurations
/// </summary>
public class EmergingIssuePlugin
{
    private readonly Kernel _kernel;
    private readonly ILogger<EmergingIssuePlugin> _logger;
    private readonly IEmergingIssueConfigService _emergingIssueConfigService;
    private readonly AlertHandlerService _alertHandlerService; private readonly Dictionary<string, EmergingIssueDetectionPromptConfig> _alertIdToDetectionConfig;

    public EmergingIssuePlugin(
        Kernel kernel,
        ILogger<EmergingIssuePlugin> logger,
        IEmergingIssueConfigService emergingIssueConfigService,
        AlertHandlerService alertHandlerService)
    {
        _kernel = kernel;
        _logger = logger;
        _emergingIssueConfigService = emergingIssueConfigService ?? throw new ArgumentNullException(nameof(emergingIssueConfigService));
        _alertHandlerService = alertHandlerService ?? throw new ArgumentNullException(nameof(alertHandlerService));
        _alertIdToDetectionConfig = LoadEmergingIssueDetectionPromptConfigs();
    }    /// <summary>
         /// Loads custom system prompts for emerging issue detection from configuration files
         /// </summary>
    private Dictionary<string, EmergingIssueDetectionPromptConfig> LoadEmergingIssueDetectionPromptConfigs()
    {
        var dict = new Dictionary<string, EmergingIssueDetectionPromptConfig>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Try looking for the file with the new name in the FirstPartyAgent.Core directory
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmergingIssueDetectionPromptConfigs.json");

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var options = JsonSerializer.Deserialize<List<EmergingIssueDetectionPromptConfig>>(json);
                if (options != null)
                {
                    foreach (var opt in options)
                    {
                        // Add by AlertId (direct match)
                        if (!string.IsNullOrWhiteSpace(opt.AlertId) && !string.IsNullOrWhiteSpace(opt.SystemPrompt))
                        {
                            dict[opt.AlertId] = opt;
                        }

                        // Add by Tags (for tag-based matching)
                        if (opt.Tags != null && opt.Tags.Count > 0 && !string.IsNullOrWhiteSpace(opt.SystemPrompt))
                        {
                            foreach (var tag in opt.Tags)
                            {
                                if (!string.IsNullOrWhiteSpace(tag))
                                {
                                    // Use tag: prefix to distinguish tag-based entries from AlertId-based entries
                                    dict[$"tag:{tag.ToLowerInvariant()}"] = opt;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load EmergingIssueDetectionPromptConfigs.json: {Message}", ex.Message);
        }
        return dict;
    }

    /// <summary>
    /// Detects if the ICM content matches any known emerging issue patterns
    /// </summary>
    /// <param name="icmContent">The summarized content of an ICM</param>
    /// <param name="issueMetadata">Metadata about the site</param>
    /// <returns>A detection result with matching details if any emerging issue is detected</returns>
    [KernelFunction("detect_emerging_issue"), Description("Detect if an ICM is related to a known emerging issue")]
    public async Task<EmergingIssueDetectionResult> DetectEmergingIssue(
        [Description("The summarized content of an ICM")] string icmContent,
        [Description("Metadata about the issue (e.g., issueMetadata, service etc.)")] string issueMetadata,
        [Description("ALERT_ID")] string alertId)
    {
        try
        {
            _logger.LogInformation("Detecting emerging issues for ICM content: {Length} chars", icmContent.Length);

            var emergingIssueConfigs = await GetEmergingIssueConfigs();
            if (emergingIssueConfigs.Count == 0)
            {
                _logger.LogWarning("No emerging issue configurations found");
                return new EmergingIssueDetectionResult
                {
                    IsEmergingIssue = false,
                    MatchedEmergingIssue = null,
                    MatchConfidence = 0,
                    AnalysisExplanation = "No emerging issue configurations found to compare against."
                };
            }

            var bestMatch = await FindBestMatchingEmergingIssue(icmContent, issueMetadata, emergingIssueConfigs, alertId);
            return bestMatch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting emerging issues: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets all emerging issue configurations from the service
    /// </summary>
    private async Task<List<EmergingIssueConfigWrapper>> GetEmergingIssueConfigs()
    {
        var result = new List<EmergingIssueConfigWrapper>();

        try
        {
            if (!_emergingIssueConfigService.IsEnabled())
            {
                _logger.LogWarning("EmergingIssueConfigService is not enabled");
                return result;
            }

            // Get all emerging issues from the service
            var emergingIssues = await _emergingIssueConfigService.ListEmergingIssues();

            foreach (var issue in emergingIssues)
            {
                try
                {
                    var configWrapper = ParseEmergingIssueConfig(issue);
                    if (configWrapper != null)
                    {
                        result.Add(configWrapper);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing emerging issue config for incident {IncidentId}: {Message}",
                        issue.IncidentId, ex.Message);
                }
            }

            _logger.LogInformation("Found {Count} emerging issue configurations from service", result.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving emerging issue configs: {Message}", ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Parses an emerging issue configuration from the Models.EmergingIssueConfig
    /// </summary>
    private EmergingIssueConfigWrapper? ParseEmergingIssueConfig(EmergingIssueConfig emergingIssueConfig)
    {
        if (emergingIssueConfig == null || string.IsNullOrEmpty(emergingIssueConfig.Content))
        {
            return null;
        }

        var content = emergingIssueConfig.Content;
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var configWrapper = new EmergingIssueConfigWrapper
        {
            OriginalConfig = emergingIssueConfig,
            Content = content
        };

        // Extract main sections from markdown content stored in Content property
        var currentSection = "";
        var sectionContent = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                // Save previous section
                if (!string.IsNullOrEmpty(currentSection) && sectionContent.Length > 0)
                {
                    configWrapper.Sections[currentSection] = sectionContent.ToString().Trim();
                    sectionContent.Clear();
                }

                // Start new section
                currentSection = line.Substring(3).Trim();
            }
            else if (line.StartsWith("# "))
            {
                // Title (use the one from config if available)
                if (string.IsNullOrEmpty(emergingIssueConfig.Title))
                {
                    configWrapper.Title = line.Substring(2).Trim();
                }
                else
                {
                    configWrapper.Title = emergingIssueConfig.Title;
                }
            }
            else if (!string.IsNullOrEmpty(currentSection))
            {
                // Append to current section
                sectionContent.AppendLine(line);
            }
        }

        // Save last section
        if (!string.IsNullOrEmpty(currentSection) && sectionContent.Length > 0)
        {
            configWrapper.Sections[currentSection] = sectionContent.ToString().Trim();
        }

        // Extract condition specifically
        if (configWrapper.Sections.TryGetValue("Condition", out var condition))
        {
            configWrapper.Condition = condition;
        }

        // If we didn't find a title in the markdown, use the one from the config
        if (string.IsNullOrEmpty(configWrapper.Title))
        {
            configWrapper.Title = emergingIssueConfig.Title;
        }

        // Set the IncidentId from the config
        configWrapper.IncidentId = emergingIssueConfig.IncidentId;

        return configWrapper;
    }

    /// <summary>
    /// Finds the best matching emerging issue for the given ICM content
    /// </summary>
    private async Task<EmergingIssueDetectionResult> FindBestMatchingEmergingIssue(
        string icmContent,
        string issueMetadata,
        List<EmergingIssueConfigWrapper> emergingIssueConfigs,
        string alertId)
    {
        var result = new EmergingIssueDetectionResult
        {
            IsEmergingIssue = false,
            MatchConfidence = 0
        };        // First try to find a custom system prompt by AlertId
        string? systemPrompt = null;
        string metadataLabel = "Site Metadata:"; // Default value
                                                 // Direct AlertId match
        if (!string.IsNullOrWhiteSpace(alertId) && _alertIdToDetectionConfig.TryGetValue(alertId, out var customConfigByAlertId))
        {
            systemPrompt = customConfigByAlertId.SystemPrompt;
            if (!string.IsNullOrWhiteSpace(customConfigByAlertId.MetadataLabel))
            {
                metadataLabel = customConfigByAlertId.MetadataLabel;
            }
            _logger.LogInformation("Found custom system prompt by direct AlertId match for {AlertId}", alertId);
        }
        // If no direct match, try to find by Tags from ICMAlertConfig
        else if (!string.IsNullOrWhiteSpace(alertId))
        {
            try
            {
                // Get the ICMAlertConfig to check its Tags
                var alertConfig = await _alertHandlerService.GetICMAlertConfigAsync(alertId);
                if (alertConfig != null && alertConfig.Tags != null && alertConfig.Tags.Count > 0)
                {
                    // Try to find matching tags
                    foreach (var tag in alertConfig.Tags)
                    {
                        string tagKey = $"tag:{tag.ToLowerInvariant()}";
                        if (_alertIdToDetectionConfig.TryGetValue(tagKey, out var customConfigByTag))
                        {
                            systemPrompt = customConfigByTag.SystemPrompt;
                            if (!string.IsNullOrWhiteSpace(customConfigByTag.MetadataLabel))
                            {
                                metadataLabel = customConfigByTag.MetadataLabel;
                            }
                            _logger.LogInformation("Found custom system prompt by Tag match for {Tag} from AlertId {AlertId}", tag, alertId);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting ICMAlertConfig for tag matching with AlertId {AlertId}: {Message}", alertId, ex.Message);
            }
        }

        // If no custom prompt found, use the default
        if (systemPrompt == null)
        {
            systemPrompt = @"You are an expert system analyzing incidents.
You are comparing a current incident with known emerging issues to determine if they are related.
Use your knowledge of Azure Functions, runtime errors, and cloud systems to evaluate the similarity.
Consider error messages, version numbers, affected services, and problem patterns in your analysis.

## Condition Matching Analysis
Perform a detailed condition matching analysis by breaking down the emerging issue condition into specific elements and checking each one against the current incident:
1. Identify individual conditions in the emerging issue (conditions may be separated by periods, semicolons, or phrases like ""when"" or ""if"")
2. For each condition, evaluate whether it matches the current incident
3. Assign a match status to each condition: ""Yes"" (clearly matches), ""No"" (clearly doesn't match), or ""Unknown"" (insufficient information)
4. Consider all information available about the incident, including error messages, host version, extensions, etc.

Rate the similarity on a scale of 0-100 where:
- 0: Not related at all
- 25: Slightly similar but likely different issues
- 50: Moderately similar with some common elements
- 75: Highly similar and likely the same issue
- 100: Definitely the same issue";
            _logger.LogInformation("Using default system prompt for alertId {AlertId}", alertId ?? "null");
        }

        // Process each emerging issue configuration separately
        double highestScore = 0;
        EmergingIssueConfigWrapper? bestMatchConfig = null;
        string bestMatchAnalysis = string.Empty;

        foreach (var config in emergingIssueConfigs)
        {            // Prepare the user prompt for this particular emerging issue configuration
            var userPrompt = $@"
Current Incident:
{icmContent}

{metadataLabel}
{issueMetadata}

Emerging Issue (ICM {config.IncidentId}):
Condition: {config.Condition}
Root Cause: {config.Sections.GetValueOrDefault("Root Cause Analysis", "Not specified")}

Your task is to analyze if the current incident is related to this emerging issue by checking each part of the condition.

IMPORTANT INSTRUCTIONS:
1. Break down the emerging issue condition into SEPARATE, DISTINCT conditions. Each sentence or requirement should be treated as a separate condition.
2. For EACH distinct condition, determine if it matches the current incident (Yes/No/Unknown).
3. Provide a clear explanation for why each condition matches or doesn't match.
4. Create a comprehensive analysis showing ALL conditions, even if some don't match.

Provide your response in the following JSON format:
{{
    ""similarity_score"": <number 0-100>,
    ""reasons"": [""reason1"", ""reason2"", ...],
    ""confidence"": <number 0-100>,
    ""condition_matches"": [
        {{
            ""condition"": ""specific condition text"",
            ""match_status"": ""Yes|No|Unknown"",
            ""explanation"": ""brief explanation of why it matches or not""
        }},
        ...
    ]
}}

Make sure to identify and list EVERY distinct condition from the emerging issue condition. This is critical as your analysis will be displayed as a table to users and all conditions must be shown.";

            // Use ChatCompletionService from the kernel
            try
            {
                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(systemPrompt);
                chatHistory.AddUserMessage(userPrompt);

                // Configure execution settings
                var promptExecutionSettings = new AzureOpenAIPromptExecutionSettings()
                {
                    Temperature = 0
                };

                // Get model response
                var chatCompletionResult = await chatCompletionService.GetChatMessageContentAsync(
                    chatHistory,
                    executionSettings: promptExecutionSettings,
                    kernel: _kernel);

                var analysisText = chatCompletionResult.Content ?? string.Empty;

                // Parse JSON response
                try
                {
                    // Extract the JSON part from the response
                    var jsonStartIndex = analysisText.IndexOf('{');
                    var jsonEndIndex = analysisText.LastIndexOf('}');
                    if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
                    {
                        var jsonText = analysisText.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                        var analysisResult = System.Text.Json.JsonSerializer.Deserialize<EmergingIssueAnalysisResult>(jsonText);

                        if (analysisResult != null && analysisResult.similarity_score > highestScore)
                        {
                            highestScore = analysisResult.similarity_score;
                            bestMatchConfig = config;

                            // Format detailed analysis from the result
                            var detailedAnalysis = new StringBuilder();
                            detailedAnalysis.AppendLine($"Similarity Score: {analysisResult.similarity_score}");
                            detailedAnalysis.AppendLine($"Confidence: {analysisResult.confidence}");

                            detailedAnalysis.AppendLine("\n## Match Reasons:");
                            foreach (var reason in analysisResult.reasons)
                            {
                                detailedAnalysis.AppendLine($"- {reason}");
                            }

                            // Make sure we have condition matches to display
                            if (analysisResult.condition_matches == null || analysisResult.condition_matches.Count == 0)
                            {
                                detailedAnalysis.AppendLine("\n## Condition Match Analysis:");
                                detailedAnalysis.AppendLine("\nNo condition matches were provided in the analysis.");
                            }
                            else
                            {
                                // Format condition matches as a markdown table for better readability
                                detailedAnalysis.AppendLine("\n## Condition Match Analysis:");
                                detailedAnalysis.AppendLine("\n| Condition | Match Status | Explanation |");
                                detailedAnalysis.AppendLine("|-----------|--------------|-------------|");

                                foreach (var match in analysisResult.condition_matches)
                                {
                                    // Use emojis for visual cues
                                    string statusEmoji = match.match_status switch
                                    {
                                        "Yes" => "✅",
                                        "No" => "❌",
                                        _ => "❓"
                                    };

                                    // Escape any pipe characters in the text to maintain table formatting
                                    var condition = match.condition?.Replace("|", "\\|") ?? "Unknown condition";
                                    var explanation = match.explanation?.Replace("|", "\\|") ?? "No explanation provided";

                                    detailedAnalysis.AppendLine($"| {condition} | {statusEmoji} {match.match_status} | {explanation} |");
                                }
                            }

                            bestMatchAnalysis = detailedAnalysis.ToString();
                        }
                    }
                }
                catch (Exception jsonEx)
                {
                    _logger.LogError(jsonEx, "Error parsing JSON analysis result: {Message}", jsonEx.Message);

                    // In case of parsing error, log the full response for debugging
                    _logger.LogDebug("Full response from LLM: {ResponseText}", analysisText);

                    // Fallback to basic extraction if JSON parsing fails
                    var similarity = ExtractNumericValue(analysisText, "similarity_score");
                    if (similarity > highestScore)
                    {
                        highestScore = similarity;
                        bestMatchConfig = config;
                        bestMatchAnalysis = $"Analysis (JSON parsing failed): {analysisText}";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while analyzing emerging issue match: {Message}", ex.Message);
            }
        }

        // Set result based on best match
        result.IsEmergingIssue = highestScore >= 80;
        result.MatchConfidence = highestScore / 100.0;
        result.MatchedEmergingIssue = bestMatchConfig?.OriginalConfig;
        result.AnalysisExplanation = bestMatchAnalysis;

        return result;
    }

    /// <summary>
    /// Extracts a numeric value from a string based on a key
    /// </summary>
    private double ExtractNumericValue(string text, string key)
    {
        var pattern = $"\"{key}\"\\s*:\\s*(\\d+)";
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern);
        if (match.Success && match.Groups.Count > 1)
        {
            if (double.TryParse(match.Groups[1].Value, out var value))
            {
                return value;
            }
        }
        return 0;
    }

    /// <summary>
    /// Result of emerging issue detection
    /// </summary>
    public class EmergingIssueDetectionResult
    {
        public bool IsEmergingIssue { get; set; }
        public double MatchConfidence { get; set; }
        public EmergingIssueConfig? MatchedEmergingIssue { get; set; }
        public string? AnalysisExplanation { get; set; }
    }

    /// <summary>
    /// JSON structure for the analysis result from the LLM
    /// </summary>
    public class EmergingIssueAnalysisResult
    {
        public double similarity_score { get; set; }
        public List<string> reasons { get; set; } = [];
        public double confidence { get; set; }
        public List<ConditionMatch> condition_matches { get; set; } = [];
    }

    /// <summary>
    /// Represents a single condition match in the analysis
    /// </summary>
    public class ConditionMatch
    {
        public string condition { get; set; } = string.Empty;
        public string match_status { get; set; } = string.Empty;
        public string explanation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Wrapper for EmergingIssueConfig to parse and access markdown content
    /// </summary>
    public class EmergingIssueConfigWrapper
    {
        public EmergingIssueConfig? OriginalConfig { get; set; }
        public string IncidentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public Dictionary<string, string> Sections { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Configuration for customized system prompts used in emerging issue detection for specific alerts
    /// </summary>
    public class EmergingIssueDetectionPromptConfig
    {
        /// <summary>
        /// The ID of the alert that this system prompt applies to
        /// </summary>
        public string AlertId { get; set; } = string.Empty;

        /// <summary>
        /// The system prompt to use when detecting emerging issues for this alert type
        /// </summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Optional tags to match against ICMAlertConfig Tags for finding prompt templates
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// The label to use for the metadata section in prompts (e.g., "Site Metadata:", "Service Metadata:", "Component Metadata:")
        /// Defaults to "Site Metadata:" if not specified
        /// </summary>
        public string MetadataLabel { get; set; } = "Site Metadata:";
    }
}
