using System.Diagnostics;
using System.Reflection;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Evals.Models;
using Agent.Framework;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Interface;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.Mocks.FunctionCalling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Evals;

[TestClass]
public class DeclarativeEvalRunner
{
    public TestContext TestContext { get; set; }
    private ChatConfiguration? _chatConfiguration;
    private IChatClient _agentStateAssessmentClient = null!;
    private IHost? _host;
    private string? _llmDeploymentName;
    private ThreadManagementService _threadManager = null!;
    private IThreadRepository _threadRepo = null!;
    private ReplayToolFactory<AgentContext> _replayToolFactory = null!;
    private DeclarativeEvalConfiguration _currentEvalConfig = null!;

    // MS Test does not support per class concurrency limits.
    // Running all the declarative tests in parallel at max concurrency will just cause timeouts due to LLM throttling
    private static SemaphoreSlim _testSemaphore = new SemaphoreSlim(4, 4);

    [TestInitialize]
    public async Task TestInitialize()
    {
        await _testSemaphore.WaitAsync();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        _testSemaphore.Release();

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    // Dynamic test data for all declarative tests
    public static IEnumerable<object[]> DeclarativeTestData()
    {
        var declarativeFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Declarative");

        if (!Directory.Exists(declarativeFolder))
        {
            yield break;
        }

        var yamlFiles = Directory.GetFiles(declarativeFolder, "*.yaml");

        foreach (var yamlFilePath in yamlFiles)
        {
            var config = LoadEvalConfiguration(yamlFilePath);
            var testSuiteName = config.TestSuite.Name;

            // Use relative path from Declarative folder
            var relativePath = Path.GetFileName(yamlFilePath);

            for (int i = 0; i < config.TestSuite.TestCases.Count; i++)
            {
                var testCase = config.TestSuite.TestCases[i];
                var testCaseName = testCase.Name;

                for (int j = 0; j < testCase.StartMessages.Count; j++)
                {
                    var userMessage = testCase.StartMessages[j];
                    var testDisplayName = $"{testSuiteName}_{testCaseName}_{j + 1}";
                    yield return new object[] { testDisplayName, userMessage, relativePath, testCaseName };
                }
            }
        }
    }

    [TestMethod]
    public void DeclarativeTest_ParseYaml()
    {
        var declarativeFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Declarative");
        var yamlFiles = Directory.GetFiles(declarativeFolder, "*.yaml");

        foreach (var yamlFilePath in yamlFiles)
        {
            try
            {
                var config = LoadEvalConfiguration(yamlFilePath);
                Console.WriteLine($"Successfully loaded {Path.GetFileName(yamlFilePath)}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse YAML file {yamlFilePath}.", ex);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(DeclarativeTestData), DynamicDataSourceType.Method)]
    public async Task DeclarativeTest(string testDisplayName, string userMessage, string relativeYamlPath, string testCaseName)
    {
        await RunDeclarativeEvalCore(relativeYamlPath, testDisplayName, userMessage, testCaseName);
    }

    private async Task RunDeclarativeEvalCore(string relativeYamlPath, string testDisplayName, string userMessage, string testCaseName)
    {
        // Convert relative path back to absolute path
        var yamlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Declarative", relativeYamlPath);
        _currentEvalConfig = LoadEvalConfiguration(yamlFilePath);

        var testCase = _currentEvalConfig.TestSuite.TestCases.First(tc => tc.Name == testCaseName);
        var evaluation = MergeEvaluation(testCase.Evaluation, _currentEvalConfig.TestSuite.Evaluation);

        var validationError = ValidateEvaluation(evaluation, testCaseName, relativeYamlPath);
        if (validationError != null)
        {
            Assert.Fail(validationError);
            return;
        }

        await SetupTestHost(_currentEvalConfig);

        var timeout = ParseTimeout(_currentEvalConfig.TestSuite.Configuration.Timeout);
        var tokenSource = new CancellationTokenSource();
        if (!Debugger.IsAttached)
        {
            tokenSource.CancelAfter(timeout);
        }

        await LoadToolReplayLogs(_currentEvalConfig);
        SetupSkipReplayFunctions(_currentEvalConfig);
        SetupFuzzyMatchFunctions(_currentEvalConfig);

        var groundedContext = evaluation.GroundedContext;
        var exampleResponse = evaluation.ExampleResponse;

        TestContext.WriteLine($"Test: {testDisplayName}");
        TestContext.WriteLine($"Message: {userMessage}");

        var startMessageRequest = new CreateMessageRequest(userMessage, "testUser", "Test User");
        var thread = await _threadManager.CreateUserInitiatedThread(new CreateThreadRequest(startMessageRequest));

        List<ChatMessage>? fullConversation = null;
        var autoReplyHelper = new AutoReplyHelper(_agentStateAssessmentClient)
        {
            DefaultReply = evaluation.AutoReply.DefaultReply,
            GroundedContext = groundedContext
        };

        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));

                (_, fullConversation) = await _threadRepo.WaitForAgentResponse(thread, tokenSource.Token);
                var agentContext = (await _threadRepo.GetAgentContextsForThreadAsync(thread.Id)).Single(); // I dont know how to handle multiple contexts yet
                _replayToolFactory.CheckForReplayFailures();

                if (agentContext.ContextState == ContextStateEnum.PendingApproval)
                {
                    throw new Exception("Declarative evals do not have handling for approvals yet");
                }

                // If the agent is still processing, we just wait and check again.
                if (agentContext.ContextState == ContextStateEnum.Processing)
                {
                    continue;
                }

                // The agent wasn't processing, so a reply might be necessary, such as asking the user a clarifying question.
                var reply = await autoReplyHelper.AssessAndGetReply(fullConversation);

                if (reply != null)
                {
                    // send the reply and then resume waiting.
                    await _threadManager.CreateMessage(thread.Id, new CreateMessageRequest(reply, "testUser", "Test User"));                    
                    continue;
                }

                if (autoReplyHelper.AssessedState == AutoReplyHelper.AssessedAgentState.Complete)
                {
                    break;
                }

                Assert.Inconclusive($"⚠️ Eval framework had trouble determining the next step. This code probably needs an update. The agent context state is {agentContext.ContextState}, the autoreply assessed state is: {autoReplyHelper.AssessedState}. ");
            }

            var combinedAgentResponse = fullConversation.CombineAgentResponses();
            var evalResult = await combinedAgentResponse.EvaluateAsync(TestContext, _chatConfiguration, fullConversation, groundedContext, exampleResponse, _llmDeploymentName);

            TestContext.WriteLine(string.Empty);
            if (fullConversation != null)
                TestContext.WriteMessages(fullConversation);

            var assertions = evaluation.Assertions;
            var equivalenceScore = evalResult?.Equivalence?.Value ?? 0;
            var groundednessScore = evalResult?.Groundedness?.Value ?? 0;

            Assert.IsTrue(equivalenceScore >= assertions?.Equivalence?.MinimumScore,
                $"Low equivalence score: {equivalenceScore}, {evalResult?.Equivalence?.Reason}");
            Assert.IsTrue(groundednessScore >= assertions?.Groundedness?.MinimumScore,
                $"Low groundedness score: {groundednessScore}, {evalResult?.Groundedness?.Reason}");
        }
        catch (TaskCanceledException tce)
        {
            TestContext.WriteLine(string.Empty);
            if (fullConversation != null)
                TestContext.WriteMessages(fullConversation);

            throw new Exception("Evaluation did not complete within the specified timeout.", tce);
        }
        catch (ReplayFailureException fe)
        {
            TestContext.WriteLine(string.Empty);
            if (fullConversation != null)
                TestContext.WriteMessages(fullConversation);

            Assert.Inconclusive($"The agent made a tool call that we could not replay from the logs, which invalidates this test run: {fe.Message}");
        }
        catch (Exception)
        {
            TestContext.WriteLine(string.Empty);
            if (fullConversation != null)
                TestContext.WriteMessages(fullConversation);

            throw;
        }
    }

    private static EvalEvaluation? MergeEvaluation(EvalEvaluation? caseEval, EvalEvaluation? suiteEval)
    {
        if (caseEval == null && suiteEval == null) return null;

        if (suiteEval == null) return caseEval;
        if (caseEval == null) return suiteEval;

        // Deep merge for assertions
        var mergedAssertions = new EvalAssertions();
        if (caseEval.Assertions != null || suiteEval.Assertions != null)
        {
            mergedAssertions.Equivalence = new EvalScoreAssertion
            {
                MinimumScore = caseEval.Assertions?.Equivalence?.MinimumScore ?? suiteEval.Assertions?.Equivalence?.MinimumScore
            };
            mergedAssertions.Groundedness = new EvalScoreAssertion
            {
                MinimumScore = caseEval.Assertions?.Groundedness?.MinimumScore ?? suiteEval.Assertions?.Groundedness?.MinimumScore
            };
        }

        // Deep merge for auto-reply
        var mergedAutoReply = new EvalAutoReply();
        if (caseEval.AutoReply != null || suiteEval.AutoReply != null)
        {
            mergedAutoReply.DefaultReply = caseEval.AutoReply?.DefaultReply ?? suiteEval.AutoReply?.DefaultReply;
            mergedAutoReply.AssessmentBreakCondition = caseEval.AutoReply?.AssessmentBreakCondition ?? suiteEval.AutoReply?.AssessmentBreakCondition;
        }

        return new EvalEvaluation
        {
            GroundedContext = caseEval.GroundedContext ?? suiteEval.GroundedContext,
            ExampleResponse = caseEval.ExampleResponse ?? suiteEval.ExampleResponse,
            Assertions = mergedAssertions,
            AutoReply = mergedAutoReply
        };
    }

    private static string? ValidateEvaluation(EvalEvaluation? evaluation, string testCaseName, string relativeYamlPath)
    {
        if (evaluation == null)
            return $"Evaluation is null for test case {testCaseName} in {relativeYamlPath}";

        var missingProperties = new List<string>();

        if (evaluation.GroundedContext == null)
            missingProperties.Add("GroundedContext");

        if (evaluation.ExampleResponse == null)
            missingProperties.Add("ExampleResponse");

        if (evaluation.Assertions == null)
            missingProperties.Add("Assertions");
        else
        {
            if (evaluation.Assertions.Equivalence?.MinimumScore == null)
                missingProperties.Add("Assertions.Equivalence.MinimumScore");
            if (evaluation.Assertions.Groundedness?.MinimumScore == null)
                missingProperties.Add("Assertions.Groundedness.MinimumScore");
        }

        if (evaluation.AutoReply == null)
            missingProperties.Add("AutoReply");
        else if (evaluation.AutoReply.DefaultReply == null)
            missingProperties.Add("AutoReply.DefaultReply");

        if (missingProperties.Any())
            return $"Missing evaluation properties for test case {testCaseName} in {relativeYamlPath}: {string.Join(", ", missingProperties)}";

        return null;
    }

    private static DeclarativeEvalConfiguration LoadEvalConfiguration(string yamlFilePath)
    {
        var yaml = File.ReadAllText(yamlFilePath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)            
            .Build();

        return deserializer.Deserialize<DeclarativeEvalConfiguration>(yaml);
    }

    private async Task SetupTestHost(DeclarativeEvalConfiguration config)
    {
        var builder = TestHelpers.BuildTestApp(out _llmDeploymentName);
        builder.RegisterDefaultServices();
        builder.RegisterServicesForAgentFrameworkEval();

        // Configure database based on YAML config
        if (!string.IsNullOrEmpty(config.TestSuite.Configuration.Database))
        {
            builder.Services.AddLocalGremlin(config.TestSuite.Configuration.Database);
        }

        // Register plugins based on YAML config
        RegisterPluginDefinitions(builder.Services, config.TestSuite.Plugins);

        _host = builder.Build();

        _threadManager = _host.Services.GetRequiredService<ThreadManagementService>();
        _threadRepo = _host.Services.GetRequiredService<IThreadRepository>();
        _replayToolFactory = (ReplayToolFactory<AgentContext>)_host.Services.GetRequiredService<IToolFactory<AgentContext>>();

        var evalClient = _host.Services.GetRequiredService<IChatClient>();
        _chatConfiguration = new ChatConfiguration(evalClient);
        _agentStateAssessmentClient = _host.Services.GetRequiredKeyedService<IChatClient>("function-invocation-enabled");

        await _host.StartAsync();
    }

    private void RegisterPluginDefinitions(IServiceCollection services, List<string> pluginNames)
    {
        foreach (var pluginName in pluginNames)
        {
            if (pluginName == "GraphDBPluginDefinition")
            {
                services.AddSingleton<GraphDBPluginDefinition>();
                services.AddSingleton<IGraphDBPlugin, GraphDBPlugin>();
                continue;
            }

            Type pluginType = GetPluginDefinitionType(pluginName);

            try
            {
                services.AddSingleton(pluginType, Activator.CreateInstance(pluginType, args: [null]));
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to register plugin {pluginName}. Expected a constructor that takes an IPlugin that we can pass null to.", ex);
            }
        }
    }

    private Type GetPluginDefinitionType(string pluginName)
    {
        Assembly definitionAssembly = typeof(GraphDBPluginDefinition).Assembly;
        Type? pluginType = definitionAssembly.GetType($"Agent.Plugins.Definitions.{pluginName}");

        if (pluginType == null)
        {
            // not all definitions are in the definitions namespace, yay.
            pluginType = definitionAssembly.GetType($"Agent.Plugins.{pluginName}");
        }

        if (pluginType == null)
        {
            throw new Exception($"Could not find plugin definition type for {pluginName} in assembly {definitionAssembly.FullName}. Is it in the Agent.Plugins.Definitions or Agent.Plugins namespace?");
        }

        return pluginType;
    }

    private async Task LoadToolReplayLogs(DeclarativeEvalConfiguration config)
    {
        var toolReplay = config.TestSuite.Configuration.ToolReplay;
        if (toolReplay?.LogDirectory != null)
        {
            var logDirectory = Path.Combine("ToolReplayLogs", toolReplay.LogDirectory);
            if (Directory.Exists(logDirectory))
            {
                foreach (var file in Directory.GetFiles(logDirectory))
                {
                    _replayToolFactory.LoadLogFromString(await File.ReadAllTextAsync(file));
                }
            }
        }
    }

    private void SetupSkipReplayFunctions(DeclarativeEvalConfiguration config)
    {
        var toolReplay = config.TestSuite.Configuration.ToolReplay;
        var skipFunctions = toolReplay?.SkipReplayFunctions;

        if (skipFunctions != null)
        {
            foreach (var skipFunction in skipFunctions)
            {
                if (skipFunction.EndsWith(".*"))
                {
                    // Handle wildcard patterns like "GraphDBPluginDefinition.*"
                    var shortName = skipFunction.Replace(".*", "");

                    var type = GetPluginDefinitionType(shortName);

                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        _replayToolFactory.FunctionNamesSkippedForReplay.Add(method.Name);
                    }
                }
                else
                {
                    _replayToolFactory.FunctionNamesSkippedForReplay.Add(skipFunction);
                }
            }
        }
    }

    private void SetupFuzzyMatchFunctions(DeclarativeEvalConfiguration config)
    {
        var toolReplay = config.TestSuite.Configuration.ToolReplay;
        var fuzzyMatchFunctions = toolReplay?.FuzzyMatchFunctions;

        if (fuzzyMatchFunctions != null)
        {
            foreach (var fuzzyMatchFunction in fuzzyMatchFunctions)
            {
                if (fuzzyMatchFunction.EndsWith(".*"))
                {
                    // Handle wildcard patterns like "ChartPluginDefinition.*"
                    var typeName = fuzzyMatchFunction.Replace(".*", "");
                    var fullTypeName = $"Agent.Plugins.{typeName}, Agent.Plugins";
                    var type = Type.GetType(fullTypeName);

                    if (type != null)
                    {
                        foreach (var method in type.GetMethods())
                        {
                            _replayToolFactory.FunctionNamesAllowingFuzzyMatch.Add(method.Name);
                        }
                    }
                    else
                    {
                        throw new Exception($"Could not find type for wildcard fuzzy matching: {typeName}, searched for {fullTypeName}");
                    }
                }
                else
                {
                    _replayToolFactory.FunctionNamesAllowingFuzzyMatch.Add(fuzzyMatchFunction);
                }
            }
        }
    }

    private static TimeSpan ParseTimeout(string? timeoutString)
    {
        if (string.IsNullOrEmpty(timeoutString))
        {
            throw new ArgumentException("Timeout string cannot be null or empty.");
        }

        if (timeoutString.EndsWith("s"))
        {
            if (int.TryParse(timeoutString.Replace("s", ""), out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }
        else if (timeoutString.EndsWith("m"))
        {
            if (int.TryParse(timeoutString.Replace("m", ""), out var minutes))
            {
                return TimeSpan.FromMinutes(minutes);
            }
        }

        throw new ArgumentException($"Invalid timeout format: {timeoutString}. Expected format is '30s' or '5m'.");
    }
}
