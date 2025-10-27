// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Agent.Core;
using Agent.Evals.Models;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cmd
{
    /// <summary>
    /// Generates declarative evaluation YAML files from tool replay logs using LLM assistance.
    /// </summary>
    public class GenerateEvalCommand
    {
        private const string JsonFilePattern = "*.json";
        private const string TemplateFileName = "ContainerAppsCpuMemory.yaml";
        private const string AgentDirectoryName = "Agent";
        private const string ToolReplayLogsPath = "Agent.Evals/ToolReplayLogs";
        private const string DeclarativePath = "Agent.Evals/Declarative";
        private const int MinimumTextLength = 50;
        private const int MinimumUserQuestionLength = 10;

        private readonly ILogger<GenerateEvalCommand> _logger;
        private readonly IChatClient _chatClient;
        private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        private DeclarativeEvalConfiguration? _generatedConfig;

        public GenerateEvalCommand(
            ILogger<GenerateEvalCommand> logger,
            [FromKeyedServices(Constants.FunctionInvocationChatClient)] IChatClient chatClient)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public void GenerateEval(CommandLineApplication command)
        {
            command.Description = "Generate declarative evaluation YAML from tool replay logs";
            command.HelpOption("-?|-h|--help");

            var logDirectoryArg = command.Argument("logDirectory", "Directory name under ToolReplayLogs containing the replay logs");
            var outputFileOption = command.Option("-o|--output", "Output YAML file path (default: Declarative/<logDirectory>.yaml)", CommandOptionType.SingleValue);
            var testSuiteNameOption = command.Option("-n|--name", "Test suite name (default: logDirectory)", CommandOptionType.SingleValue);
            var descriptionOption = command.Option("-d|--description", "Test suite description", CommandOptionType.SingleValue);

            command.OnExecute(async () =>
            {
                if (string.IsNullOrEmpty(logDirectoryArg.Value))
                {
                    Console.WriteLine("Error: Log directory must be provided.");
                    return 1;
                }

                var logDirectory = logDirectoryArg.Value;
                var testSuiteName = testSuiteNameOption.HasValue() ? testSuiteNameOption.Value() : logDirectory;
                var description = descriptionOption.HasValue() ? descriptionOption.Value() : $"Evaluates agent's ability to handle scenarios from {logDirectory} logs";

                var outputFile = outputFileOption.HasValue()
                    ? outputFileOption.Value()
                    : GetDefaultOutputPath(logDirectory);

                await GenerateEvalFromLogsAsync(logDirectory, outputFile, testSuiteName, description);
                return 0;
            });
        }

        private async Task GenerateEvalFromLogsAsync(string logDirectory, string outputFile, string testSuiteName, string description)
        {
            try
            {
                var logFiles = GetLogFiles(logDirectory);
                if (!logFiles.Any())
                {
                    Console.WriteLine($"Error: No log files found in directory '{logDirectory}'");
                    return;
                }

                Console.WriteLine($"Found {logFiles.Count} log files in '{logDirectory}'");
                Console.WriteLine("Generating declarative evaluation configuration from logs...");

                var evalConfig = await GenerateEvalConfigFromLogsAsync(testSuiteName, description, logDirectory, logFiles);

                Console.WriteLine("Serializing to YAML...");

                var yamlContent = SerializeToYaml(evalConfig);

                await SaveYamlAsync(outputFile, yamlContent);

                Console.WriteLine($"✅ Generated evaluation saved to: {outputFile}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate evaluation from logs");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private List<string> GetLogFiles(string logDirectory)
        {
            var logsBasePath = GetToolReplayLogsPath();
            var targetDirectory = Path.Combine(logsBasePath, logDirectory);

            if (!Directory.Exists(targetDirectory))
            {
                throw new DirectoryNotFoundException($"Directory not found: {targetDirectory}");
            }

            return Directory.GetFiles(targetDirectory, JsonFilePattern).ToList();
        }

        private async Task<DeclarativeEvalConfiguration> GenerateEvalConfigFromLogsAsync(string testSuiteName, string description, string logDirectory, List<string> logFiles)
        {
            var logAnalysis = new StringBuilder();

            // Analyze all log files for comprehensive understanding
            foreach (var logFile in logFiles)
            {
                await ProcessLogFileAsync(logFile, logAnalysis);
            }

            var templateYaml = await GetTemplateYamlAsync();

            var configPrompt = BuildSingleStepConfigGenerationPrompt(templateYaml);
            var analysisPrompt = BuildAnalysisPrompt(testSuiteName, description, logDirectory, logAnalysis.ToString());

            Console.WriteLine(analysisPrompt);

            return await GenerateConfigurationAsync(configPrompt, analysisPrompt);
        }

        private async Task ProcessLogFileAsync(string logFile, StringBuilder logAnalysis)
        {
            var fileName = Path.GetFileName(logFile);
            try
            {
                var logContent = await File.ReadAllTextAsync(logFile);

                logAnalysis.AppendLine($"=== Log File: {fileName} ===");

                // Check if this is an OTEL format file
                if (IsOtelFormat(logContent))
                {
                    Console.WriteLine($"Detected OTEL format in {fileName}");

                    // For OTEL format, extract summary differently
                    var otelSummary = ExtractOtelLogSummary(logContent);
                    logAnalysis.AppendLine(otelSummary);
                }
                else
                {
                    // Parse and extract key information from the log (original chat format)
                    var logSummary = ExtractLogSummary(logContent);
                    logAnalysis.AppendLine(logSummary);
                }

                logAnalysis.AppendLine();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to process log file '{fileName}': {ex.Message}", ex);
            }
        }

        private static string BuildAnalysisPrompt(string testSuiteName, string description, string logDirectory, string logAnalysis)
        {
            return $"""
                Generate a complete declarative evaluation configuration for:
                - Test Suite Name: {testSuiteName}
                - Description: {description}
                - Log Directory: {logDirectory}

                ## Log Analysis:
                {logAnalysis}

                Based on this analysis, create a comprehensive YAML configuration that:
                1. Analyzes the logs holistically to identify distinct test scenarios
                2. Groups similar queries as start message variations within test cases
                3. Creates separate test cases for different scenarios if needed
                4. Generates appropriate ground truth and example responses for each scenario
                5. Uses the provided log directory for tool replay
                """;
        }

        private async Task<DeclarativeEvalConfiguration> GenerateConfigurationAsync(string configPrompt, string analysisPrompt)
        {
            var tools = new List<AITool> { AIFunctionFactory.Create(CreateDeclarativeEval) };
            var options = new ChatOptions { ToolMode = ChatToolMode.RequireAny, Tools = tools };

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, configPrompt),
                new ChatMessage(ChatRole.User, analysisPrompt)
            };

            await _chatClient.GetResponseAsync(messages, options);

            if (_generatedConfig == null)
            {
                throw new InvalidOperationException("LLM failed to generate a valid configuration");
            }

            return _generatedConfig;
        }

        private string ExtractLogSummary(string logContent)
        {
            var messages = JsonSerializer.Deserialize<JsonElement[]>(logContent, _serializerOptions);
            if (messages == null)
            {
                return "Error: Could not deserialize log content";
            }

            var summary = new StringBuilder();
            ExtractUserRequests(messages, summary);
            ExtractAgentResponses(messages, summary);
            return summary.ToString();
        }

        private void ExtractUserRequests(JsonElement[] messages, StringBuilder summary)
        {
            var userMessages = messages.Where(m =>
                m.TryGetProperty("role", out var role) && role.GetString() == "user");

            foreach (var userMsg in userMessages)
            {
                if (userMsg.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array)
                {
                    foreach (var content in contents.EnumerateArray())
                    {
                        if (content.TryGetProperty("text", out var text))
                        {
                            var fullText = text.GetString();
                            if (!string.IsNullOrEmpty(fullText))
                            {
                                var userRequest = ExtractUserQuestionFromText(fullText);
                                summary.AppendLine($"User Request: {userRequest}");
                            }
                        }
                    }
                }
            }
        }

        private string ExtractUserQuestionFromText(string fullText)
        {
            var userQuestionStart = fullText.IndexOf(Agent.Framework.Markers.UserQuestionMarker);
            if (userQuestionStart >= 0)
            {
                var userQuestion = fullText.Substring(userQuestionStart + Agent.Framework.Markers.UserQuestionMarker.Length).Trim();
                if (!string.IsNullOrEmpty(userQuestion) && userQuestion.Length > MinimumUserQuestionLength)
                {
                    return userQuestion;
                }
            }

            // Fallback: use the full text if no specific pattern found
            return fullText;
        }

        private void ExtractAgentResponses(JsonElement[] messages, StringBuilder summary)
        {
            var assistantMessages = messages.Where(m =>
                m.TryGetProperty("role", out var role) && role.GetString() == "assistant");

            foreach (var assistantMsg in assistantMessages)
            {
                if (assistantMsg.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array)
                {
                    foreach (var content in contents.EnumerateArray())
                    {
                        if (content.TryGetProperty("$type", out var type) && type.GetString() == "text" &&
                            content.TryGetProperty("text", out var text))
                        {
                            var textContent = text.GetString();
                            if (!string.IsNullOrEmpty(textContent) && textContent.Length > MinimumTextLength)
                            {
                                var agentResponse = ExtractAgentResponseFromText(textContent);
                                if (!string.IsNullOrEmpty(agentResponse))
                                {
                                    summary.AppendLine($"Agent Response: {agentResponse}");
                                }
                            }
                        }
                    }
                }
            }
        }

        private string ExtractAgentResponseFromText(string textContent)
        {
            try
            {
                var responseJson = JsonSerializer.Deserialize<JsonElement>(textContent);
                if (responseJson.TryGetProperty("notifyUserMessage", out var notifyUserMessage))
                {
                    return notifyUserMessage.GetString() ?? "";
                }

                // If no notifyUserMessage but has state, it might be an intermediate response
                if (responseJson.TryGetProperty("state", out var state))
                {
                    return textContent;
                }

                return "";
            }
            catch (JsonException)
            {
                // Not JSON, use the full text
                return textContent;
            }
        }

        private void CreateDeclarativeEval(DeclarativeEvalConfiguration config)
        {
            _generatedConfig = config;
        }

        private string BuildSingleStepConfigGenerationPrompt(string templateYaml)
        {
            return $"""
                You are an expert at generating declarative evaluation configurations for AI agent testing.

                Your task is to analyze log files holistically and generate a complete configuration object that can handle multiple test scenarios intelligently.

                ## Template Example Structure:
                ```yaml
                {templateYaml}
                ```

                ## Key Requirements:
                1. **Holistic Analysis**: Analyze all logs to understand the full scope of scenarios
                2. **Intelligent Grouping**:
                   - Group similar queries as variations within the same test case
                   - Create separate test cases only for fundamentally different scenarios
                   - Each test case should have 3-5 start message variations
                3. **Smart Test Case Creation**:
                   - If logs show variations of the same core question → Single test case with multiple start messages
                   - If logs show completely different problem types → Multiple test cases
                   - Look for patterns in user requests, agent responses, and outcomes

                ## Configuration Guidelines:
                1. Use placeholder database: "PLACEHOLDER_DATABASE_NAME"
                2. Include standard plugins: "ContainerAppPluginDefinition", "DiagnosticsPluginDefinition", "ArmPluginDefinition", "NSGRulePluginDefinition", "PagerDutyIncidentPluginDefinition", "ChartPluginDefinition"
                3. Set appropriate timeout (2m for complex scenarios, 30s for simple ones)
                4. Configure toolReplay with the provided log directory name
                5. Set equivalence and groundedness minimum scores to 4
                6. Set defaultReply to "Please do your best to figure it out."

                ## Ground Truth Generation:
                - Extract ACTUAL RESULTS and FINAL OUTCOMES from logs
                - Focus on specific data points, metrics, resource names, error details
                - Create testable facts about "what the world looked like"
                - Structure as numbered points for clarity

                ## Example Response Generation:
                - Create comprehensive final responses showing all discovered data
                - Include specific technical details from the logs
                - Show the complete resolution or diagnostic findings
                - Format as a realistic agent response

                Call the CreateDeclarativeEval function with the complete configuration object.
                """;
        }
        private string SerializeToYaml(DeclarativeEvalConfiguration config)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitEmptyCollections)
                .Build();

            return serializer.Serialize(config);
        }

        private async Task<string> GetTemplateYamlAsync()
        {
            var templatePath = Path.Combine(GetDeclarativePath(), TemplateFileName);
            if (File.Exists(templatePath))
            {
                return await File.ReadAllTextAsync(templatePath);
            }
            return "# Template not found - using basic structure";
        }

        private async Task SaveYamlAsync(string outputFile, string yamlContent)
        {
            var directory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputFile, yamlContent);
        }

        private string GetDefaultOutputPath(string logDirectory)
        {
            return Path.Combine(GetDeclarativePath(), $"{logDirectory}.yaml");
        }

        private string GetToolReplayLogsPath()
        {
            return Path.Combine(FindAgentSourceDirectory(), AgentDirectoryName, "Agent.Evals", "ToolReplayLogs");
        }

        private string GetDeclarativePath()
        {
            return Path.Combine(FindAgentSourceDirectory(), AgentDirectoryName, "Agent.Evals", "Declarative");
        }

        private string FindAgentSourceDirectory()
        {
            var executableDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            var srcDir = executableDir;

            while (!string.IsNullOrEmpty(srcDir) && !Directory.Exists(Path.Combine(srcDir, AgentDirectoryName)))
            {
                srcDir = Directory.GetParent(srcDir)?.FullName;
            }

            if (!string.IsNullOrEmpty(srcDir))
            {
                return srcDir;
            }

            throw new DirectoryNotFoundException("Could not find the Agent source directory");
        }

        private bool IsOtelFormat(string logContent)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(logContent, _serializerOptions);

            // Check if it's an array and has OTEL trace structure
            if (json.ValueKind == JsonValueKind.Array)
            {
                var array = json.EnumerateArray().ToArray();
                if (array.Length > 0)
                {
                    var firstElement = array[0];
                    // OTEL format should have traceId, spans, etc.
                    return firstElement.TryGetProperty("traceId", out _) &&
                           firstElement.TryGetProperty("spans", out _);
                }
            }

            return false;
        }

        private string ExtractOtelLogSummary(string logContent)
        {
            var traces = JsonSerializer.Deserialize<JsonElement[]>(logContent, _serializerOptions);
            if (traces == null || traces.Length == 0)
            {
                return "Error: Could not deserialize OTEL log content";
            }

            var summary = new StringBuilder();

            foreach (var trace in traces) // Analyze all traces
            {
                if (trace.TryGetProperty("traceId", out var traceId))
                {
                    summary.AppendLine($"Trace ID: {traceId.GetString()}");
                }

                if (trace.TryGetProperty("totalDuration", out var duration))
                {
                    summary.AppendLine($"Total Duration: {duration.GetInt64()}ms");
                }

                if (trace.TryGetProperty("spans", out var spans) && spans.ValueKind == JsonValueKind.Array)
                {
                    var spanArray = spans.EnumerateArray().ToArray();
                    summary.AppendLine($"Span Count: {spanArray.Length}");

                    // Sort spans by start time to get chronological order
                    var sortedSpans = spanArray
                        .Where(span => span.TryGetProperty("startTime", out _))
                        .OrderBy(span => span.GetProperty("startTime").GetInt64())
                        .ToArray();

                    // Process each span in chronological order
                    foreach (var span in sortedSpans)
                    {
                        if (span.TryGetProperty("operationName", out var operationName))
                        {
                            var operation = operationName.GetString();

                            // Extract user messages from user.message spans
                            if (operation == "user.message" &&
                                span.TryGetProperty("attributes", out var attributes) &&
                                attributes.TryGetProperty("message.content", out var messageContent))
                            {
                                var userRequest = messageContent.GetString();
                                if (!string.IsNullOrEmpty(userRequest))
                                {
                                    summary.AppendLine($"User Request: {userRequest}");
                                }
                            }
                            // Extract agent responses from model.output in other spans
                            else if (span.TryGetProperty("attributes", out var attrs) &&
                                     attrs.TryGetProperty("model.output", out var modelOutput))
                            {
                                var agentResponse = ExtractAgentResponseFromModelOutput(modelOutput.GetString() ?? "");
                                if (!string.IsNullOrEmpty(agentResponse))
                                {
                                    summary.AppendLine($"Agent Response: {agentResponse}");
                                }
                            }
                        }
                    }
                }

                summary.AppendLine();
            }

            return summary.ToString();
        }

        private string ExtractAgentResponseFromModelOutput(string modelOutput)
        {
            var messages = JsonSerializer.Deserialize<JsonElement[]>(modelOutput);
            if (messages == null) return "";

            foreach (var message in messages)
            {
                if (message.TryGetProperty("role", out var role) && role.GetString() == "assistant" &&
                    message.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array)
                {
                    foreach (var content in contents.EnumerateArray())
                    {
                        if (content.TryGetProperty("value", out var value) &&
                            value.TryGetProperty("text", out var text))
                        {
                            var textContent = text.GetString() ?? "";
                            if (!string.IsNullOrEmpty(textContent))
                            {
                                // Try to parse as JSON to extract notifyUserMessage
                                try
                                {
                                    var responseJson = JsonSerializer.Deserialize<JsonElement>(textContent);
                                    if (responseJson.TryGetProperty("notifyUserMessage", out var notifyUserMessage))
                                    {
                                        var userMessage = notifyUserMessage.GetString();
                                        if (!string.IsNullOrEmpty(userMessage))
                                        {
                                            return userMessage;
                                        }
                                    }
                                }
                                catch (JsonException)
                                {
                                    // If not JSON, use the full text
                                    if (textContent.Length > MinimumTextLength)
                                    {
                                        return textContent;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return "";
        }
    }
}
