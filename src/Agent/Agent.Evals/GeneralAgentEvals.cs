// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Evals;

[TestClass]
public class GeneralAgentEvals
{
    private static TestHost? _testHost;

    private static async ValueTask<TestHost> GetTestHostAsync()
    {
        _testHost ??= await TestHelpers.InitializeTestHost();
        return _testHost;
    }

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> GeneralTestCases => LoadTestCasesFromFiles().Select(i => new object[] { i });

    private static GeneralTestCase[] LoadTestCasesFromFiles()
    {
        // Check for environment variables to filter tests, mainly used for local test with fast testing for specific case
        var targetFolderEnv = Environment.GetEnvironmentVariable("TEST_FOLDER");
        var targetFile = Environment.GetEnvironmentVariable("TEST_FILE");

        // Built-in scenario folders under the default Data directory (used ONLY when TEST_FOLDER not supplied)
        var builtInDataFolders = new[] { "AksScenarios", "AzCliCommandScenarios" };

        // New semantics (per request):
        // 1. Allow TEST_FOLDER to specify multiple folders separated by commas.
        //    e.g. TEST_FOLDER=Data/Stable,Data/Unstable,/abs/path/Custom
        // 2. If TEST_FOLDER is specified, DO NOT merge or fallback to built-in folders; only use what is specified.
        // 3. Items in TEST_FOLDER are treated literally as folder specifiers (absolute or relative to AppContext.BaseDirectory).
        //    We no longer treat plain names specially by auto-matching built-ins. To reference a built-in folder when using
        //    TEST_FOLDER, provide its relative path (e.g. "Data/HandOff") or absolute path. (Assumption documented.)

        var folderSpecs = new List<(string Spec, string ResolvedPath)>();
        if (string.IsNullOrWhiteSpace(targetFolderEnv))
        {
            // Use built-ins (legacy default behavior)
            foreach (var builtIn in builtInDataFolders)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Data", builtIn);
                folderSpecs.Add((builtIn, path));
            }
        }
        else
        {
            var rawSpecs = targetFolderEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var spec in rawSpecs)
            {
                string resolvedPath;
                if (Path.IsPathRooted(spec))
                {
                    resolvedPath = spec;
                }
                else
                {
                    // If spec is a simple folder name (no directory separator), attempt built-in style first for backward compat
                    if (!spec.Contains(Path.DirectorySeparatorChar) && !spec.Contains('/') && !spec.Contains('\\'))
                    {
                        var builtInCandidate = Path.Combine(AppContext.BaseDirectory, "Data", spec);
                        if (Directory.Exists(builtInCandidate))
                        {
                            resolvedPath = builtInCandidate;
                        }
                        else
                        {
                            // Fall back to treating as relative path to base directory
                            resolvedPath = Path.Combine(AppContext.BaseDirectory, spec);
                        }
                    }
                    else
                    {
                        // Path-like spec (contains separator) => make it relative to base directory
                        resolvedPath = Path.Combine(AppContext.BaseDirectory, spec);
                    }
                }
                folderSpecs.Add((spec, resolvedPath));
            }
        }

        var allTestCases = new List<GeneralTestCase>();
        var allAvailableFiles = new List<string>();
        var missingFolders = new List<string>();
        foreach (var (spec, path) in folderSpecs)
        {
            if (!Directory.Exists(path))
            {
                missingFolders.Add($"{spec} (resolved: {path})");
                continue;
            }

            var data = ModelGenerationDataLoader.LoadChatMessagesFromJsonFiles(path);

            // Collect all available files for error reporting
            allAvailableFiles.AddRange(data.Keys.Select(key => $"{spec}/{key}"));

            // If a specific file is requested, filter the data
            if (!string.IsNullOrEmpty(targetFile))
            {
                var filteredData = data.Where(kvp => kvp.Key.Contains(targetFile, StringComparison.OrdinalIgnoreCase)).ToList();
                if (filteredData.Count > 0)
                {
                    data = filteredData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                else
                {
                    // Skip this folder if no matching files found, don't throw exception yet
                    continue;
                }
            }

            var testCases = data.Select(kvp => GeneralTestCase.FromModelGenerationContent(kvp.Value, $"{spec}_{kvp.Key}"))
                .ToArray();
            allTestCases.AddRange(testCases);
        }

        // If a specific file was requested but no test cases were found across all folders, throw exception
        if (!string.IsNullOrEmpty(targetFile) && allTestCases.Count == 0)
        {
            var availableFiles = string.Join(", ", allAvailableFiles);
            throw new ArgumentException($"File '{targetFile}' not found in any folder. Available files: {availableFiles}");
        }

        // If TEST_FOLDER was specified and resulted in zero discovered tests, provide an aggregated error
        if (!string.IsNullOrWhiteSpace(targetFolderEnv) && allTestCases.Count == 0)
        {
            var missingMsg = missingFolders.Count > 0 ? $" Missing/NotFound: {string.Join(", ", missingFolders)}." : string.Empty;
            throw new ArgumentException($"No tests discovered from TEST_FOLDER='{targetFolderEnv}'.{missingMsg}");
        }

        return allTestCases.ToArray();
    }

    [TestMethod]
    [DynamicData(nameof(GeneralTestCases))]
    public async Task GeneralAgentTests(GeneralTestCase testCase)
    {
        // initialize test host
        var testHost = await GetTestHostAsync();

        var agent = testHost.AgentFactory.GetAgent(testCase.AgentName);
        Assert.IsNotNull(agent, $"Agent '{testCase.AgentName}' not found");

        List<ChatMessage> modelInput = [
            new ChatMessage(ChatRole.System, agent.Instructions.ToString()),
            .. testCase.ModelInput,
        ];

        var chatClient = agent.GetChatClient(testHost.RunConfig);
        var chatOptions = agent.GetChatOptions(testHost);
        // Enforce temperature override for GPT-5 family models
        var clientMetadata = chatClient.GetService<ChatClientMetadata>();
        if (clientMetadata?.DefaultModelId?.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) == true)
        {
            chatOptions.Temperature = 1;
            TestContext.WriteLine($"Overriding temperature to 1 for model '{clientMetadata.DefaultModelId}' (GPT-5 family)");
        }

        ChatResponse response;
        if (agent.HasStructuredOutput)
        {
            (response, _) = await chatClient.GetResponseAsync(modelInput, agent.OutputType, chatOptions);
        }
        else
        {
            response = await chatClient.GetResponseAsync(modelInput, chatOptions);
        }

        // Check and handle handoff state without tool call - similar to ReasoningLoop
        response = await HandleHandoffCorrectionIfNeeded(agent, modelInput, response, chatClient, chatOptions);

        // Validate based on expected output type
        switch (testCase.ExpectedOutputType)
        {
            case ExpectedOutputType.ToolCall:
                ValidateToolCallOutput(response, testCase);
                break;
            case ExpectedOutputType.FinalResponse:
                ValidateFinalResponseOutput(response, testCase);
                break;
            case ExpectedOutputType.Mixed:
                ValidateMixedOutput(response, testCase);
                break;
            case ExpectedOutputType.Handoff:
                ValidateHandoffOutput(response, testCase);
                break;
            default:
                Assert.Fail($"Unknown expected output type: {testCase.ExpectedOutputType}");
                break;
        }
    }

    [TestMethod]
    [DynamicData(nameof(GeneralTestCases))]
    public async Task GeneralAgentTests_DetailedComparison(GeneralTestCase testCase)
    {
        TestContext.WriteLine($"=== STARTING TEST CASE ===");
        TestContext.WriteLine($"Test Case: {testCase.TestCaseName}");
        TestContext.WriteLine($"============================");

        var testHost = await GetTestHostAsync();

        var agent = testHost.AgentFactory.GetAgent(testCase.AgentName);
        Assert.IsNotNull(agent, $"Agent '{testCase.AgentName}' not found");

        TestContext.WriteLine($"Testing agent: {agent.Name}");
        var instructionsText = agent.Instructions?.ToString();
        var agentInstructions = instructionsText is not null
            ? string.Concat(instructionsText.AsSpan(0, Math.Min(200, instructionsText.Length)), "...")
            : "null";
        TestContext.WriteLine($"Agent instructions: {agentInstructions}");

        // Get chat client and options similar to RunSingleTurnAsync
        var chatClient = agent.GetChatClient(testHost.RunConfig);
        var chatOptions = agent.GetChatOptions(testHost);
        var clientMetadata = chatClient.GetService<ChatClientMetadata>();

        // Enforce temperature override for GPT-5 family models
        if (clientMetadata?.DefaultModelId?.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) == true)
        {
            chatOptions.Temperature = 1;
            TestContext.WriteLine($"Overriding temperature to 1 for model '{clientMetadata.DefaultModelId}' (GPT-5 family)");
        }

        TestContext.WriteLine($"Chat options configured:");
        TestContext.WriteLine($"  - Tools count: {chatOptions.Tools?.Count ?? 0}");
        TestContext.WriteLine($"  - Tool mode: {chatOptions.ToolMode}");
        TestContext.WriteLine($"  - Temperature: {chatOptions.Temperature}");
        TestContext.WriteLine($"  - DefaultModelId: {clientMetadata?.DefaultModelId}");
        TestContext.WriteLine($"  - ProviderName: {clientMetadata?.ProviderName}");
        TestContext.WriteLine($"  - Allow multiple tool calls: {chatOptions.AllowMultipleToolCalls}");

        // Fix Bug 1: Override system prompt with agent's actual instructions
        // Remove the system message from test data if it exists and use agent's actual instructions
        var modelInput = testCase.ModelInput.ToList();

        // Remove existing system message from test data
        if (modelInput.Count > 0 && modelInput[0].Role == ChatRole.System)
        {
            modelInput.RemoveAt(0);
            TestContext.WriteLine("Removed system prompt from test data to use agent's actual instructions");
        }

        // Prepend the agent's actual system instructions
        modelInput.Insert(0, new ChatMessage(ChatRole.System, instructionsText));
        TestContext.WriteLine("Using agent's actual system instructions for evaluation");

        // Skill tools can be added dynamically at runtime based on activated skills.
        // The eval harness needs to simulate that behavior by looking at the last
        // "These are your currently active skills:" reminder in the conversation.
        TryAddDynamicToolsFromActiveSkills(testHost, agent, modelInput, chatOptions);

        // Log available tools
        if (chatOptions.Tools?.Count > 0)
        {
            TestContext.WriteLine("Available tools:");
            foreach (var tool in chatOptions.Tools)
            {
                if (tool is AIFunction func)
                {
                    TestContext.WriteLine($"  - {func.Name}");
                }
            }
            TestContext.WriteLine("");
        }

        TestContext.WriteLine($"\n=== EXPECTED MODEL OUTPUT ===");
        foreach (var expectedMsg in testCase.ExpectedOutput)
        {
            TestContext.WriteLine($"Expected [{expectedMsg.Role}]:");
            if (!string.IsNullOrEmpty(expectedMsg.Text))
            {
                TestContext.WriteLine($"  Text: {expectedMsg.Text}");
            }

            if (expectedMsg.Contents?.Count > 0)
            {
                foreach (var content in expectedMsg.Contents)
                {
                    if (content is FunctionCallContent funcCall)
                    {
                        TestContext.WriteLine($"  Expected function call: {funcCall.Name}");
                        TestContext.WriteLine($"  Arguments: {funcCall.GetSerializedArguments()}");
                    }
                }
            }
        }

        // Call the LLM with the same setup as RunSingleTurnAsync
        ChatResponse response;

        var reasoningStreamer = new ReasoningStreamContentHandler(displayModelOutput: default);

        try
        {
            if (agent.HasStructuredOutput)
            {
                var jsonStreamer = new JsonStreamContentHandler(agent.OutputType, displayModelOutput: default);
                TestContext.WriteLine("Agent has structured output - calling with structured output support");
                (response, _) = await chatClient.GetResponseAsync(
                    messages: modelInput,
                    outputType: agent.OutputType,
                    options: chatOptions,
                    outputJsonStreamHandler: jsonStreamer,
                    reasoningStreamHandler: reasoningStreamer);
            }
            else
            {
                var textStreamer = new TextStreamContentHandler(displayModelOutput: default);
                TestContext.WriteLine("Agent using regular chat completion");
                response = await chatClient.GetResponseAsync(
                    messages: modelInput,
                    options: chatOptions,
                    outputTextStreamHandler: textStreamer,
                    reasoningStreamHandler: reasoningStreamer);
            }

            // Check and handle handoff state without tool call - similar to ReasoningLoop
            response = await HandleHandoffCorrectionIfNeeded(agent, modelInput, response, chatClient, chatOptions, TestContext);
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"ERROR during LLM call: {ex.Message}");
            TestContext.WriteLine($"Exception type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                TestContext.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            throw;
        }

        // Log actual output
        TestContext.WriteLine($"\n=== ACTUAL MODEL OUTPUT ===");
        for (var i = 0; i < response.Messages.Count; i++)
        {
            var msg = response.Messages[i];
            TestContext.WriteLine($"Response message {i + 1} [{msg.Role}]:");

            if (!string.IsNullOrEmpty(msg.Text))
            {
                TestContext.WriteLine($"  Text: {msg.Text}");
            }

            if (msg.Contents?.Count > 0)
            {
                TestContext.WriteLine($"  Contents count: {msg.Contents.Count}");
                foreach (var content in msg.Contents)
                {
                    TestContext.WriteLine($"    - {content.GetType().Name}");
                    if (content is FunctionCallContent funcCall)
                    {
                        TestContext.WriteLine($"      Function: {funcCall.Name}");
                        TestContext.WriteLine($"      Call ID: {funcCall.CallId}");
                        TestContext.WriteLine($"      Arguments: {funcCall.GetSerializedArguments()}");
                    }
                    else if (content is FunctionResultContent funcResult)
                    {
                        TestContext.WriteLine($"      Call ID: {funcResult.CallId}");
                        var resultText = funcResult.Result?.ToString();
                        if (!string.IsNullOrEmpty(resultText))
                        {
                            var displayResult = resultText.Length > 200 ? resultText.Substring(0, 200) + "..." : resultText;
                            TestContext.WriteLine($"      Result: {displayResult}");
                        }
                    }
                }
            }
        }

        TestContext.WriteLine($"Response modelId: {response.ModelId}");

        // Compare with expected output
        TestContext.WriteLine($"\n=== COMPARISON ===");

        if (testCase.ExpectedOutput.Length != response.Messages.Count)
        {
            TestContext.WriteLine($"⚠️  Message count mismatch - Expected: {testCase.ExpectedOutput.Length}, Actual: {response.Messages.Count}");
        }
        else
        {
            TestContext.WriteLine($"✅ Message count matches: {response.Messages.Count}");
        }

        // Compare function calls
        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? [])
            .ToList();

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? [])
            .ToList();

        TestContext.WriteLine($"Expected function calls: {expectedFunctionCalls.Count}");
        TestContext.WriteLine($"Actual function calls: {actualFunctionCalls.Count}");

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains('-'));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            TestContext.WriteLine("✅ No function calls found and expected options include '-' (no call acceptable)");
            // Skip function call validation since no calls are expected and none were made
        }
        else
        {
            for (var i = 0; i < Math.Max(expectedFunctionCalls.Count, actualFunctionCalls.Count); i++)
            {
                if (i < expectedFunctionCalls.Count && i < actualFunctionCalls.Count)
                {
                    var expected = expectedFunctionCalls[i];
                    var actual = actualFunctionCalls[i];

                    bool functionsMatch;
                    string expectedDisplayName;

                    // Check if the expected function name contains multiple acceptable options (pipe-separated)
                    if (expected.Name.Contains('|'))
                    {
                        var acceptableFunctionNames = expected.Name.Split('|', StringSplitOptions.RemoveEmptyEntries)
                            .Select(name => name.Trim())
                            .Where(name => name != "-") // Exclude "-" from actual function name matching
                            .ToList();

                        functionsMatch = acceptableFunctionNames.Contains(actual.Name);
                        expectedDisplayName = GetExpectedFunctionDisplayName(expected.Name);
                    }
                    else
                    {
                        functionsMatch = expected.Name == actual.Name;
                        expectedDisplayName = expected.Name;
                    }

                    TestContext.WriteLine($"Function call {i + 1}: {(functionsMatch ? "✅" : "❌")} Expected: {expectedDisplayName}, Actual: {actual.Name}");

                    // Assert that function names match
                    Assert.IsTrue(functionsMatch,
                        $"Function call {i + 1}: Expected '{expectedDisplayName}' but got '{actual.Name}'");

                    if (functionsMatch
                        && !IsHandoffToolCall(actual.Name) // can't compare input args for handoff as it is free flow text
                        && !expected.Name.Contains('|')) // don't verify arguments if multiple function calls are acceptable
                    {
                        // Compare arguments using smart comparison
                        var argsMatch = TestHelpers.AreArgumentsEquivalent(expected.Arguments ?? new object(), actual.Arguments ?? new object());
                        TestContext.WriteLine($"  Arguments match: {(argsMatch ? "✅" : "❌")}");

                        // If smart comparison fails, show detailed comparison for debugging
                        if (!argsMatch)
                        {
                            TestContext.WriteLine($"  Expected args: {expected.GetSerializedArguments()}");
                            TestContext.WriteLine($"  Actual args: {actual.GetSerializedArguments()}");
                        }

                        // Assert that arguments match
                        Assert.IsTrue(argsMatch,
                            $"Function call {i + 1} arguments mismatch. Expected: {expected.GetSerializedArguments()}, Actual: {actual.GetSerializedArguments()}");
                    }
                    else if (expected.Name.Contains('|'))
                    {
                        TestContext.WriteLine($"  Arguments verification skipped (multiple function calls acceptable)");
                    }
                }
                else if (i < expectedFunctionCalls.Count)
                {
                    var expected = expectedFunctionCalls[i];
                    var expectedDisplayName = GetExpectedFunctionDisplayName(expected.Name);

                    // Check if "-" is acceptable for this expected function call
                    if (expected.Name.Contains('-'))
                    {
                        TestContext.WriteLine($"Function call {i + 1}: ✅ Expected: {expectedDisplayName}, Actual: (none) - no call is acceptable");
                    }
                    else
                    {
                        TestContext.WriteLine($"Function call {i + 1}: ❌ Expected: {expectedDisplayName}, Actual: (missing)");
                        Assert.Fail($"Function call {i + 1}: Expected '{expectedDisplayName}' but no corresponding function call was found");
                    }
                }
                else
                {
                    TestContext.WriteLine($"Function call {i + 1}: ❌ Expected: (missing), Actual: {actualFunctionCalls[i].Name}");
                    Assert.Fail($"Function call {i + 1}: Unexpected function call '{actualFunctionCalls[i].Name}' was found but none was expected");
                }
            }
        }

        // Fix Bug 2: Add proper assertions based on expected output type
        TestContext.WriteLine($"\n=== RUNNING ASSERTIONS ===");
        try
        {
            switch (testCase.ExpectedOutputType)
            {
                case ExpectedOutputType.ToolCall:
                    ValidateToolCallOutputWithAssertions(response, testCase, TestContext);
                    break;
                case ExpectedOutputType.FinalResponse:
                    await ValidateFinalResponseOutputWithLLM(response, testCase, TestContext, chatClient);
                    break;
                case ExpectedOutputType.Mixed:
                    ValidateMixedOutputWithAssertions(response, testCase, TestContext);
                    break;
                case ExpectedOutputType.Handoff:
                    ValidateHandoffOutputWithAssertions(response, testCase, TestContext);
                    break;
                default:
                    Assert.Fail($"Unknown expected output type: {testCase.ExpectedOutputType}");
                    break;
            }
            TestContext.WriteLine("✅ All assertions passed!");
        }
        catch (AssertFailedException ex)
        {
            TestContext.WriteLine($"❌ Assertion failed: {ex.Message}");
            throw;
        }

        TestContext.WriteLine($"\n=== TEST COMPLETED ===");
        TestContext.WriteLine("Use the console output above to analyze the differences between expected and actual behavior.");
    }

    private void TryAddDynamicToolsFromActiveSkills(
        TestHost testHost,
        Agent<AgentContext> agent,
        IReadOnlyList<ChatMessage> modelInput,
        ChatOptions chatOptions)
    {
        var activeSkillNames = ExtractActiveSkillNamesFromLastReminder(modelInput);
        if (activeSkillNames.Count == 0)
        {
            return;
        }

        TestContext.WriteLine($"Active skills detected from chat input: {string.Join(", ", activeSkillNames)}");

        chatOptions.Tools ??= [];

        var existingToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in chatOptions.Tools)
        {
            if (tool is AIFunction f)
            {
                existingToolNames.Add(f.Name);
            }
        }

        var addedToolNames = new List<string>();
        foreach (var skillName in activeSkillNames)
        {
            ISkill? skill;
            try
            {
                skill = testHost.RunConfig.SkillRegistry.GetSkillByName(skillName, includeSystemSkills: agent.AddSystemSkills);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Failed to resolve active skill '{skillName}': {ex.Message}");
                continue;
            }

            if (skill is null)
            {
                TestContext.WriteLine($"Active skill '{skillName}' not found in registry; skipping skill tools.");
                continue;
            }

            foreach (var toolName in skill.Tools)
            {
                if (existingToolNames.Contains(toolName))
                {
                    continue;
                }

                try
                {
                    var tool = testHost.ToolFactory.GetTool(toolName, agent);
                    chatOptions.Tools.Add(tool);
                    existingToolNames.Add(tool.Name);
                    addedToolNames.Add(tool.Name);
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"Tool '{toolName}' listed by active skill '{skill.Name}' was not found in ToolFactory; skipping. Error: {ex.Message}");
                }
            }
        }

        if (addedToolNames.Count > 0)
        {
            // If tools were previously empty, AllowMultipleToolCalls is expected to be null.
            // Once tools exist, keep it at false for parity with the eval harness defaults.
            chatOptions.AllowMultipleToolCalls ??= false;
            TestContext.WriteLine($"Added {addedToolNames.Count} dynamic tool(s) from active skills: {string.Join(", ", addedToolNames)}");
            TestContext.WriteLine($"Total tools count is now: {chatOptions.Tools.Count}");
        }
    }

    private static List<string> ExtractActiveSkillNamesFromLastReminder(IReadOnlyList<ChatMessage> messages)
    {
        const string marker = "These are your currently active skills:";
        const string noSkillsMarker = "You currently have no active skills";

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var text = messages[i].Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (text.Contains(noSkillsMarker, StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            if (!text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lines = text.Split('\n');
            var startIndex = -1;
            for (var li = 0; li < lines.Length; li++)
            {
                if (lines[li].Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    startIndex = li + 1;
                    break;
                }
            }

            if (startIndex < 0 || startIndex >= lines.Length)
            {
                return [];
            }

            var skillNames = new List<string>();
            for (var li = startIndex; li < lines.Length; li++)
            {
                var line = lines[li].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("</system-reminder>", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (!line.StartsWith("- ", StringComparison.Ordinal))
                {
                    // Stop once we leave the bullet list.
                    if (skillNames.Count > 0)
                    {
                        break;
                    }
                    continue;
                }

                var afterDash = line[2..].Trim();
                if (afterDash.Length == 0)
                {
                    continue;
                }

                var colonIndex = afterDash.IndexOf(':');
                var skillName = colonIndex >= 0 ? afterDash[..colonIndex] : afterDash;
                skillName = skillName.Trim();

                if (skillName.Length > 0)
                {
                    skillNames.Add(skillName);
                }
            }

            return skillNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Fallback: if the runtime-generated skills reminder isn't present in the chat history,
        // infer "active" skills from the skills the agent already loaded via read_skill_file.
        return ExtractActiveSkillNamesFromReadSkillFileToolCalls(messages);
    }

    private static List<string> ExtractActiveSkillNamesFromReadSkillFileToolCalls(IReadOnlyList<ChatMessage> messages)
    {
        var skillNames = new List<string>();

        foreach (var message in messages)
        {
            if (message.Contents is null || message.Contents.Count == 0)
            {
                continue;
            }

            foreach (var functionCall in message.Contents.OfType<FunctionCallContent>())
            {
                if (!functionCall.Name.Equals("read_skill_file", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var argsJson = functionCall.GetSerializedArguments();
                if (string.IsNullOrWhiteSpace(argsJson))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(argsJson);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    string? skillName = null;

                    if (doc.RootElement.TryGetProperty("skill_name", out var skillNameProp) &&
                        skillNameProp.ValueKind == JsonValueKind.String)
                    {
                        skillName = skillNameProp.GetString();
                    }
                    else if (doc.RootElement.TryGetProperty("skillName", out var skillNameCamelProp) &&
                             skillNameCamelProp.ValueKind == JsonValueKind.String)
                    {
                        skillName = skillNameCamelProp.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(skillName))
                    {
                        skillNames.Add(skillName.Trim());
                    }
                }
                catch
                {
                    // Ignore malformed arguments; fallback best-effort only.
                }
            }
        }

        return skillNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsHandoffToolCall(string toolName)
    {
        return toolName.StartsWith("transfer_to_", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("handoffback", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateToolCallOutputWithAssertions(ChatResponse response, GeneralTestCase testCase, TestContext testContext)
    {
        testContext.WriteLine("Validating tool call output with assertions...");

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? [])
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? [])
            .ToList();

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains('-'));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            testContext.WriteLine("✅ No function calls found and '-' option allows this");
            return;
        }

        Assert.AreEqual(expectedFunctionCalls.Count, actualFunctionCalls.Count,
            $"Expected {expectedFunctionCalls.Count} function calls but got {actualFunctionCalls.Count}");

        for (var i = 0; i < expectedFunctionCalls.Count; i++)
        {
            var expectedCall = expectedFunctionCalls[i];
            var actualCall = actualFunctionCalls[i];

            ValidateFunctionCallName(expectedCall.Name, actualCall.Name, i, testContext, isHandoff: false);
        }
        testContext.WriteLine("Tool call validation passed!");
    }

    private static async Task ValidateFinalResponseOutputWithLLM(ChatResponse response, GeneralTestCase testCase, TestContext testContext, IChatClient chatClient)
    {
        testContext.WriteLine("Validating final response output with LLM evaluation...");

        // Ensure no function calls are present
        var functionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        Assert.AreEqual(0, functionCalls.Count, "Expected no function calls in final response");

        // Ensure there's actual text content
        var hasTextContent = response.Messages.Any(m => !string.IsNullOrEmpty(m.Text) ||
                                                         m.Contents?.OfType<TextContent>().Any() == true);
        Assert.IsTrue(hasTextContent, "Expected text content in final response");

        // Use LLM to evaluate semantic similarity
        var actualText = string.Join("\n", response.Messages.Select(m => m.Text ?? "").Where(t => !string.IsNullOrEmpty(t)));
        var expectedText = string.Join("\n", testCase.ExpectedOutput.Select(m => m.Text ?? "").Where(t => !string.IsNullOrEmpty(t)));

        if (!string.IsNullOrEmpty(expectedText) && !string.IsNullOrEmpty(actualText))
        {
            var evaluationPrompt = $@"
You are an AI assistant that evaluates whether two responses are semantically similar and convey the same meaning.

Expected Response:
{expectedText}

Actual Response:
{actualText}

Task: Determine if the actual response conveys the same meaning and intent as the expected response. Consider:
1. Key information and facts are preserved
2. Overall tone and intent match
3. Important details are not missing
4. The response addresses the same concerns

Respond with only 'SIMILAR' if they convey the same meaning, or 'DIFFERENT' if they don't, followed by a brief explanation.";

            try
            {
                var evaluationMessages = new List<ChatMessage>
                {
                    new(ChatRole.User, evaluationPrompt)
                };
                var evaluationResponse = await chatClient.GetResponseAsync(evaluationMessages);
                var evaluation = evaluationResponse.Messages.LastOrDefault()?.Text ?? "";

                testContext.WriteLine($"LLM Evaluation: {evaluation}");

                if (evaluation.StartsWith("DIFFERENT", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail($"LLM evaluation indicates responses are semantically different. Evaluation: {evaluation}");
                }
                else if (evaluation.StartsWith("SIMILAR", StringComparison.OrdinalIgnoreCase))
                {
                    testContext.WriteLine("✅ LLM evaluation indicates responses are semantically similar");
                }
                else
                {
                    testContext.WriteLine($"⚠️ Ambiguous LLM evaluation, proceeding with caution: {evaluation}");
                }
            }
            catch (Exception ex)
            {
                testContext.WriteLine($"⚠️ LLM evaluation failed: {ex.Message}. Proceeding without semantic validation.");
            }
        }
        testContext.WriteLine("Final response validation completed!");
    }

    private static void ValidateMixedOutputWithAssertions(ChatResponse response, GeneralTestCase testCase, TestContext testContext)
    {
        testContext.WriteLine("Validating mixed output with assertions...");

        Assert.IsNotNull(response, "Response should not be null");
        Assert.IsTrue(response.Messages.Count > 0, "Response should contain at least one message");

        // Basic structure validation - ensure we have the expected number of messages
        Assert.AreEqual(testCase.ExpectedOutput.Length, response.Messages.Count,
            "Message count should match expected output");

        testContext.WriteLine("Mixed output validation passed!");
    }

    private static void ValidateHandoffOutputWithAssertions(ChatResponse response, GeneralTestCase testCase, TestContext testContext)
    {
        testContext.WriteLine("Validating handoff output with assertions...");

        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains("-"));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            testContext.WriteLine("✅ No handoff function calls found and '-' option allows this");
            return;
        }

        Assert.AreEqual(expectedFunctionCalls.Count, actualFunctionCalls.Count,
            $"Expected {expectedFunctionCalls.Count} function calls but got {actualFunctionCalls.Count}");

        for (var i = 0; i < expectedFunctionCalls.Count; i++)
        {
            var expectedCall = expectedFunctionCalls[i];
            var actualCall = actualFunctionCalls[i];

            ValidateFunctionCallName(expectedCall.Name, actualCall.Name, i, testContext, isHandoff: true);
        }
        testContext.WriteLine("Handoff validation passed!");
    }

    private static void ValidateToolCallOutput(ChatResponse response, GeneralTestCase testCase)
    {
        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains("-"));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            // No function calls found and "-" option allows this
            return;
        }

        Assert.AreEqual(expectedFunctionCalls.Count, actualFunctionCalls.Count,
            $"Expected {expectedFunctionCalls.Count} function calls but got {actualFunctionCalls.Count}");

        for (var i = 0; i < expectedFunctionCalls.Count; i++)
        {
            var expectedCall = expectedFunctionCalls[i];
            var actualCall = actualFunctionCalls[i];

            ValidateFunctionCallName(expectedCall.Name, actualCall.Name, i, isHandoff: false);
        }
    }

    private static void ValidateFinalResponseOutput(ChatResponse response, GeneralTestCase testCase)
    {
        // Ensure no function calls are present
        var functionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        Assert.AreEqual(0, functionCalls.Count, "Expected no function calls in final response");

        // Ensure there's actual text content
        var hasTextContent = response.Messages.Any(m => !string.IsNullOrEmpty(m.Text) ||
                                                         m.Contents?.OfType<TextContent>().Any() == true);
        Assert.IsTrue(hasTextContent, "Expected text content in final response");
    }

    private static void ValidateMixedOutput(ChatResponse response, GeneralTestCase testCase)
    {
        // For mixed output, we just validate that the response is not null and has content
        // More specific validation can be added based on the expected output structure
        Assert.IsNotNull(response, "Response should not be null");
        Assert.IsTrue(response.Messages.Count > 0, "Response should contain at least one message");

        // Basic structure validation - ensure we have the expected number of messages
        Assert.AreEqual(testCase.ExpectedOutput.Length, response.Messages.Count,
            "Message count should match expected output");
    }

    private static void ValidateHandoffOutput(ChatResponse response, GeneralTestCase testCase)
    {
        var actualFunctionCalls = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        var expectedFunctionCalls = testCase.ExpectedOutput
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
            .ToList();

        // Check if any expected function calls have "-" option (meaning no function call is acceptable)
        var hasNoCallOption = expectedFunctionCalls.Any(fc => fc.Name.Contains("-"));

        if (hasNoCallOption && actualFunctionCalls.Count == 0)
        {
            // No handoff function calls found and "-" option allows this
            return;
        }

        Assert.AreEqual(expectedFunctionCalls.Count, actualFunctionCalls.Count,
            $"Expected {expectedFunctionCalls.Count} function calls but got {actualFunctionCalls.Count}");

        for (var i = 0; i < expectedFunctionCalls.Count; i++)
        {
            var expectedCall = expectedFunctionCalls[i];
            var actualCall = actualFunctionCalls[i];

            ValidateFunctionCallName(expectedCall.Name, actualCall.Name, i, isHandoff: true);
        }
    }

    /// <summary>
    /// Validates that a function call matches one of the expected function names (supports pipe-separated multiple options)
    /// Special case: "-" in the expected options means "no function call is acceptable"
    /// </summary>
    /// <param name="expectedFunctionName">Expected function name, can be pipe-separated for multiple options. Use "-" to indicate no function call is acceptable.</param>
    /// <param name="actualFunctionName">Actual function name from the response</param>
    /// <param name="callIndex">Index of the function call for error messages</param>
    /// <param name="testContext">Test context for logging (optional)</param>
    /// <param name="isHandoff">Whether this is a handoff function call (adds transfer_to_ validation)</param>
    private static void ValidateFunctionCallName(string expectedFunctionName, string actualFunctionName, int callIndex, TestContext? testContext = null, bool isHandoff = false)
    {
        if (expectedFunctionName.Contains('|'))
        {
            var acceptableFunctionNames = expectedFunctionName.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .ToList();

            // Special case: if "-" is one of the options, it means no function call is acceptable
            var noFunctionCallAcceptable = acceptableFunctionNames.Contains("-");
            var actualFunctionNames = acceptableFunctionNames.Where(name => name != "-").ToList();

            var isValidCall = actualFunctionNames.Contains(actualFunctionName);
            var callType = isHandoff ? "Handoff function call" : "Function call";

            Assert.IsTrue(isValidCall,
                $"{callType} {callIndex + 1}: Expected one of [{string.Join(", ", acceptableFunctionNames)}] but got {actualFunctionName}");

            if (isHandoff && !noFunctionCallAcceptable)
            {
                // For handoffs, we expect the function name to start with "transfer_to_" unless "-" is acceptable
                Assert.IsTrue(actualFunctionName.StartsWith("transfer_to_"),
                    $"Expected handoff function to start with 'transfer_to_' but got {actualFunctionName}");
            }

            testContext?.WriteLine($"✅ {callType} {callIndex + 1}: {actualFunctionName} matches one of the acceptable options: [{string.Join(", ", acceptableFunctionNames)}]");
        }
        else
        {
            var callType = isHandoff ? "Handoff function call" : "Function call";
            Assert.AreEqual(expectedFunctionName, actualFunctionName,
                $"{callType} {callIndex + 1}: Expected {expectedFunctionName} but got {actualFunctionName}");

            if (isHandoff)
            {
                // For handoffs, we expect the function name to start with "transfer_to_"
                Assert.IsTrue(actualFunctionName.StartsWith("transfer_to_"),
                    $"Expected handoff function to start with 'transfer_to_' but got {actualFunctionName}");
            }
        }
    }

    /// <summary>
    /// Gets a display-friendly name for expected function calls (handles pipe-separated options)
    /// </summary>
    private static string GetExpectedFunctionDisplayName(string expectedFunctionName)
    {
        return expectedFunctionName.Contains('|')
            ? $"[{string.Join(" | ", expectedFunctionName.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(name => name.Trim()))}]"
            : expectedFunctionName;
    }

    /// <summary>
    /// Checks if agent has structured output with handoff state but no tool call, and handles correction similar to ReasoningLoop
    /// </summary>
    /// <param name="agent">The agent being tested</param>
    /// <param name="modelInput">The current model input (will be modified if correction is needed)</param>
    /// <param name="response">The current response from the LLM</param>
    /// <param name="chatClient">The chat client for making additional calls</param>
    /// <param name="chatOptions">The chat options to use</param>
    /// <param name="testContext">Test context for logging (optional)</param>
    /// <returns>The corrected response if handoff correction was applied, otherwise the original response</returns>
    private static async Task<ChatResponse> HandleHandoffCorrectionIfNeeded(
        Agent<AgentContext> agent,
        List<ChatMessage> modelInput,
        ChatResponse response,
        IChatClient chatClient,
        ChatOptions chatOptions,
        TestContext? testContext = null)
    {
        // Only check if agent has structured output and there's a response
        if (!agent.HasStructuredOutput || response.Messages.Count == 0)
        {
            return response;
        }

        var lastMessage = response.Messages.Last();
        var hasToolCall = lastMessage.Contents?.OfType<FunctionCallContent>().Any() == true;

        // Only proceed if there's no tool call but there's text content
        if (hasToolCall || string.IsNullOrEmpty(lastMessage.Text))
        {
            return response;
        }

        try
        {
            testContext?.WriteLine($"Response before handoff check: {lastMessage.Text}");
            testContext?.WriteLine($"Checking structured output for handoff state without tool call...");
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(lastMessage.Text);

            if (jsonElement.TryGetProperty("state", out var stateProperty))
            {
                var state = stateProperty.GetString();
                testContext?.WriteLine($"Agent state: {state}");

                // Check if agent indicated handoff but didn't make tool call
                if (state == "HandOff_OutOfScope" || state == "HandOff_Continue")
                {
                    testContext?.WriteLine($"Agent indicated handoff state '{state}' but made no tool call. Adding prompt for second attempt, similar to ReasoningLoop logic.");

                    // Add the agent's response to the conversation
                    modelInput.Add(lastMessage);

                    // Add user prompt asking agent to make the proper tool call
                    var promptMessage = new ChatMessage(ChatRole.User,
                        $"You mentioned the request is in state {state}, but did not actually perform any handoffs (transfer_to_* or HandOffBack). " +
                        "Reflect if any more processing work is required. If yes, set the state to Processing and continue taking actions in your scope. " +
                        "Otherwise if you are actually done, then call the right handoff tool.");

                    if (state == "HandOff_Continue")
                    {
                        promptMessage = new ChatMessage(ChatRole.User,
                        "You mentioned the request is in state HandOff_Continue, but did not actually perform any handoffs (transfer_to_*). " +
                        "Reflect if any more processing work is required *based on your responsibility*. If yes, set the state to Processing and continue taking actions in your scope. " +
                        "Otherwise if you are actually done, then call the right handoff tool.");
                    }
                    else
                    {
                        promptMessage = new ChatMessage(ChatRole.User,
                        "You mentioned the request is in state HandOff_OutOfScope, but did not actually perform any handoffs (transfer_to_* or HandOffBack). " +
                        "Reflect if any more processing work is required *based on your responsibility*. If yes, set the state to Processing and continue taking actions in your scope. " +
                        "Otherwise if you are actually done, then call the right handoff tool.");
                    }

                    modelInput.Add(promptMessage);

                    testContext?.WriteLine($"\n=== CALLING LLM AGAIN (Handoff Correction) ===");
                    testContext?.WriteLine($"Added user prompt: {promptMessage.Text}");

                    // Make second LLM call
                    ChatResponse correctedResponse;
                    if (agent.HasStructuredOutput)
                    {
                        (correctedResponse, _) = await chatClient.GetResponseAsync(modelInput, agent.OutputType, chatOptions);
                    }
                    else
                    {
                        correctedResponse = await chatClient.GetResponseAsync(modelInput, chatOptions);
                    }
                    return correctedResponse;
                }
            }
        }
        catch (JsonException)
        {
            // If we can't parse JSON, just continue with original response
            testContext?.WriteLine("Could not parse structured output as JSON, continuing with original response");
        }

        return response;
    }

    #region Test Case Classes

    public sealed record GeneralTestCase(
        string AgentName,
        List<ChatMessage> ModelInput,
        ChatMessage[] ExpectedOutput,
        ExpectedOutputType ExpectedOutputType,
        string TestCaseName = "")
    {
        public static GeneralTestCase FromModelGenerationContent(ModelGenerationContent content)
        {
            return FromModelGenerationContent(content, "");
        }

        public static GeneralTestCase FromModelGenerationContent(ModelGenerationContent content, string testCaseName)
        {
            var chatHistory = content.ModelInput.ToList();
            if (chatHistory[0].Role == ChatRole.System)
            {
                chatHistory.RemoveRange(0, 1); // Remove system message if it exists
            }

            var expectedOutputType = DetermineExpectedOutputType(content.ModelOutput);

            return new GeneralTestCase(
                AgentName: content.AgentName,
                ModelInput: chatHistory,
                ExpectedOutput: content.ModelOutput,
                ExpectedOutputType: expectedOutputType,
                TestCaseName: testCaseName);
        }

        private static ExpectedOutputType DetermineExpectedOutputType(ChatMessage[] modelOutput)
        {
            var functionCalls = modelOutput
                .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? Enumerable.Empty<FunctionCallContent>())
                .ToList();

            var hasTextContent = modelOutput.Any(m => !string.IsNullOrEmpty(m.Text) ||
                                                     m.Contents?.OfType<TextContent>().Any() == true);

            // Check if it's a handoff case
            if (functionCalls.Any(fc => fc.Name.StartsWith("transfer_to_")))
            {
                return ExpectedOutputType.Handoff;
            }

            // Check other cases
            if (functionCalls.Count != 0 && hasTextContent)
            {
                return ExpectedOutputType.Mixed;
            }
            else if (functionCalls.Count != 0)
            {
                return ExpectedOutputType.ToolCall;
            }
            else
            {
                return ExpectedOutputType.FinalResponse;
            }
        }
    }

    public enum ExpectedOutputType
    {
        ToolCall,
        FinalResponse,
        Mixed,
        Handoff
    }

    #endregion
}
