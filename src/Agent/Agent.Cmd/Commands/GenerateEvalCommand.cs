// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Agent.Evals.Models;
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
            [FromKeyedServices("function-invocation-enabled")] IChatClient chatClient)
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

            return Directory.GetFiles(targetDirectory, "*.json").ToList();
        }

        private async Task<DeclarativeEvalConfiguration> GenerateEvalConfigFromLogsAsync(string testSuiteName, string description, string logDirectory, List<string> logFiles)
        {
            var logAnalysis = new StringBuilder();

            // Analyze all log files for comprehensive understanding
            var samplesToAnalyze = logFiles.Take(Math.Min(5, logFiles.Count));

            foreach (var logFile in samplesToAnalyze)
            {
                var logContent = await File.ReadAllTextAsync(logFile);
                var fileName = Path.GetFileName(logFile);

                // Parse and extract key information from the log
                var logSummary = ExtractLogSummary(logContent);
                logAnalysis.AppendLine($"=== Log File: {fileName} ===");
                logAnalysis.AppendLine(logSummary);
                logAnalysis.AppendLine();

                // Extract final agent responses for context
                var finalResponses = ExtractFinalResponses(logContent);
                if (!string.IsNullOrEmpty(finalResponses))
                {
                    logAnalysis.AppendLine("=== Final Agent Responses ===");
                    logAnalysis.AppendLine(finalResponses);
                    logAnalysis.AppendLine();
                }
            }

            var startMessages = ExtractStartMessages(logFiles);
            var templateYaml = await GetTemplateYamlAsync();

            var configPrompt = BuildSingleStepConfigGenerationPrompt(templateYaml, startMessages);
            var analysisPrompt = $"""
                Generate a complete declarative evaluation configuration for:
                - Test Suite Name: {testSuiteName}
                - Description: {description}
                - Log Directory: {logDirectory}

                ## Log Analysis:
                {logAnalysis}

                ## Extracted Start Messages:
                {string.Join("\n", startMessages.Select((msg, i) => $"{i + 1}. \"{msg}\""))}

                Based on this analysis, create a comprehensive YAML configuration that:
                1. Analyzes the logs holistically to identify distinct test scenarios
                2. Groups similar queries as start message variations within test cases
                3. Creates separate test cases for different scenarios if needed
                4. Generates appropriate ground truth and example responses for each scenario
                5. Uses the provided log directory for tool replay
                """;

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

        private string ExtractFinalResponses(string logContent)
        {
            try
            {
                var messages = JsonSerializer.Deserialize<JsonElement[]>(logContent, _serializerOptions);
                if (messages == null) return "";

                var finalResponses = new StringBuilder();

                // Find assistant messages that contain final results (look for completion indicators)
                var assistantMessages = messages.Where(m =>
                    m.TryGetProperty("role", out var role) && role.GetString() == "assistant")
                    .Where(m => m.TryGetProperty("contents", out var contents))
                    .Where(m =>
                    {
                        // Look for messages with completion indicators or final results
                        if (m.TryGetProperty("contents", out var contentsArray) && contentsArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var content in contentsArray.EnumerateArray())
                            {
                                if (content.TryGetProperty("text", out var text))
                                {
                                    var textContent = text.GetString();
                                    if (!string.IsNullOrEmpty(textContent) &&
                                        (textContent.Contains("CompletedSuccessfully") ||
                                         textContent.Contains("summary") ||
                                         textContent.Contains("Here is") ||
                                         textContent.Contains("| Key Vault") ||
                                         textContent.Contains("Last Updated") ||
                                         textContent.Length > 500)) // Long responses are often final results
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                        return false;
                    })
                    .TakeLast(2); // Get the last couple of final responses

                foreach (var msg in assistantMessages)
                {
                    if (msg.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var content in contents.EnumerateArray())
                        {
                            if (content.TryGetProperty("text", out var text))
                            {
                                finalResponses.AppendLine($"Final Response: {text.GetString()}");
                                finalResponses.AppendLine();
                            }
                        }
                    }
                }

                return finalResponses.ToString();
            }
            catch (Exception ex)
            {
                return $"Error extracting final responses: {ex.Message}";
            }
        }

        private string ExtractLogSummary(string logContent)
        {
            try
            {
                var messages = JsonSerializer.Deserialize<JsonElement[]>(logContent, _serializerOptions);
                if (messages == null)
                {
                    return "Error: Could not deserialize log content";
                }

                var summary = new StringBuilder();

                // Extract user's initial request
                var userMessages = messages.Where(m =>
                    m.TryGetProperty("role", out var role) && role.GetString() == "user")
                    .Take(2);

                foreach (var userMsg in userMessages)
                {
                    if (userMsg.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var content in contents.EnumerateArray())
                        {
                            if (content.TryGetProperty("text", out var text))
                            {
                                summary.AppendLine($"User Request: {text.GetString()}");
                            }
                        }
                    }
                }

                // Extract key assistant responses and tool calls
                var assistantMessages = messages.Where(m =>
                    m.TryGetProperty("role", out var role) && role.GetString() == "assistant")
                    .Take(5);

                foreach (var assistantMsg in assistantMessages)
                {
                    if (assistantMsg.TryGetProperty("contents", out var contents) && contents.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var content in contents.EnumerateArray())
                        {
                            if (content.TryGetProperty("$type", out var type))
                            {
                                if (type.GetString() == "functionCall" && content.TryGetProperty("name", out var funcName))
                                {
                                    summary.AppendLine($"Tool Call: {funcName.GetString()}");
                                }
                                else if (type.GetString() == "text" && content.TryGetProperty("text", out var text))
                                {
                                    var textContent = text.GetString();
                                    if (!string.IsNullOrEmpty(textContent) && textContent.Length > 50)
                                    {
                                        summary.AppendLine($"Agent Response: {textContent.Substring(0, Math.Min(200, textContent.Length))}...");
                                    }
                                }
                            }
                        }
                    }
                }

                return summary.ToString();
            }
            catch (Exception ex)
            {
                return $"Error parsing log: {ex.Message}";
            }
        }

        private void CreateDeclarativeEval(DeclarativeEvalConfiguration config)
        {
            _generatedConfig = config;
        }

        private string BuildSingleStepConfigGenerationPrompt(string templateYaml, List<string> startMessages)
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

                ## Start Message Examples Available:
                {string.Join("\n", startMessages.Select((msg, i) => $"{i + 1}. \"{msg}\""))}

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

        private List<string> ExtractStartMessages(List<string> logFiles)
        {
            var startMessages = new List<string>();

            foreach (var logFile in logFiles.Take(5)) // Sample up to 5 files
            {
                try
                {
                    var logContent = File.ReadAllText(logFile);
                    var messages = JsonSerializer.Deserialize<JsonElement[]>(logContent);

                    if (messages != null && messages.Length > 0)
                    {
                        // Look for the first user message
                        var firstUserMessage = messages.FirstOrDefault(m =>
                            m.TryGetProperty("role", out var role) && role.GetString() == "user");

                        if (firstUserMessage.ValueKind != JsonValueKind.Undefined &&
                            firstUserMessage.TryGetProperty("contents", out var contents) &&
                            contents.ValueKind == JsonValueKind.Array)
                        {
                            var contentsArray = contents.EnumerateArray().ToArray();
                            if (contentsArray.Length > 0 &&
                                contentsArray[0].TryGetProperty("text", out var textElement))
                            {
                                var text = textElement.GetString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    // Extract the user question part (after "User question goes below:")
                                    var userQuestionStart = text.IndexOf("User question goes below:");
                                    if (userQuestionStart >= 0)
                                    {
                                        var userQuestion = text.Substring(userQuestionStart + "User question goes below:".Length).Trim();
                                        if (!string.IsNullOrEmpty(userQuestion) && userQuestion.Length > 10)
                                        {
                                            startMessages.Add(userQuestion);
                                        }
                                    }
                                    else if (text.Length > 20)
                                    {
                                        // Fallback: use the full text if no specific pattern found
                                        startMessages.Add(text);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to extract start message from {logFile}: {ex.Message}");
                }
            }

            return startMessages.Distinct().Take(5).ToList();
        }

        private async Task<string> GetTemplateYamlAsync()
        {
            var templatePath = Path.Combine(GetDeclarativePath(), "ContainerAppsCpuMemory.yaml");
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
            var executableDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            var srcDir = executableDir;

            while (!string.IsNullOrEmpty(srcDir) && !Directory.Exists(Path.Combine(srcDir, "Agent")))
            {
                srcDir = Directory.GetParent(srcDir)?.FullName;
            }

            if (!string.IsNullOrEmpty(srcDir))
            {
                return Path.Combine(srcDir, "Agent", "Agent.Evals", "ToolReplayLogs");
            }

            throw new DirectoryNotFoundException("Could not find the Agent source directory");
        }

        private string GetDeclarativePath()
        {
            var executableDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            var srcDir = executableDir;

            while (!string.IsNullOrEmpty(srcDir) && !Directory.Exists(Path.Combine(srcDir, "Agent")))
            {
                srcDir = Directory.GetParent(srcDir)?.FullName;
            }

            if (!string.IsNullOrEmpty(srcDir))
            {
                return Path.Combine(srcDir, "Agent", "Agent.Evals", "Declarative");
            }

            throw new DirectoryNotFoundException("Could not find the Agent source directory");
        }
    }
}
