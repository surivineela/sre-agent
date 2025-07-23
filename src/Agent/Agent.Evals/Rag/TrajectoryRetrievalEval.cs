using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Data.AgentMemory;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Plugins.Mocks;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.KubernetesAgent;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.ScenarioTestHelpers;
using Agent.Evals.Evaluators;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Evals.Rag;

[TestClass]
[DoNotParallelize]
public partial class TrajectoryEval
{
    public TestContext TestContext { get; set; }

    private IHost? _host;
    private static int _iterationCount = 1; // Default value

    // Static constructor to initialize _iterationCount
    static TrajectoryEval()
    {
        // Retrieve the IterationCount from environment variables or a default value
        string? iterationCountEnv = Environment.GetEnvironmentVariable("IterationCount");
        if (int.TryParse(iterationCountEnv, out int parsedIterations))
        {
            Console.WriteLine($"Static Constructor: IterationCount is {parsedIterations}");
            _iterationCount = parsedIterations;
        }
        else
        {
            Console.WriteLine($"AKSAgentEvals Static Constructor: IterationCount not found or invalid. Using default value: {_iterationCount}.");
        }
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        // Create thread repository first
        var builder = TestHelpers.BuildTestApp(out var llmDeploymentName);
        if (!builder.IsAgentMemoryEnabled())
        {
            Console.WriteLine("AgentMemory is not enabled, skipping test initialization.");
            return;
        }

        builder.RegisterDefaultServices();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.ConfigureAgentMemory();

        _host = builder.Build();
        await _host.StartAsync();
    }


    [TestCleanup]
    public async Task TestCleanup()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }



    [TestMethod]
    public async Task TrajectoryRetrievalTest()
    {
        var agentMemoryClient = _host!.Services.GetRequiredService<IAgentMemoryClient>();
        var indexSerivce = _host.Services.GetRequiredService<ISearchIndexService>();
        var embeddingGenerator = _host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var chatClient = _host.Services.GetRequiredService<IChatClient>();
        var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<TrajectorySearchRelevanceEvaluator>();

        // rebuild the index
        await indexSerivce.DeleteIndexIfExistsAsync();
        await indexSerivce.CreateOrUpdateIndexAsync();

        var dataFolderPath = Path.Combine(AppContext.BaseDirectory, "Data", "Trajectory");
        var trajectoryFiles = Directory.GetFiles(
            dataFolderPath,
            "traj_*.txt",
            SearchOption.AllDirectories);

        if (trajectoryFiles.Length == 0)
        {
            Assert.Fail("No trajectory files found for indexing.");
        }

        foreach (var trajectoryFile in trajectoryFiles)
        {
            var trajectoryContent = await File.ReadAllTextAsync(trajectoryFile);
            var trajectory = JsonSerializer.Deserialize<ProcessedTrajectoryOutput_v3>(trajectoryContent, JsonSerializerOptions.Web);
            var embedding = await embeddingGenerator.GenerateVectorAsync(trajectory!.SymptomsObserved);
            var agentMemory = AgentMemory.FromTrajectory(Guid.NewGuid().ToString(), trajectory, [.. embedding.Span]);

            // Index the trajectory content
            await indexSerivce.IndexContentAsync(agentMemory);
        }

        var searchQuery = "quote-api pods in CrashLoopBackOff";
        var result = await agentMemoryClient.SearchTrajectoriesAsync(searchQuery);

        // Basic validation
        Assert.IsNotEmpty(result);

        // LLM as Judge Evaluation
        var trajectoryEvaluator = new TrajectorySearchRelevanceEvaluator(chatClient, logger);

        var evaluationResult = await trajectoryEvaluator.EvaluateTrajectorySearchAsync(
            searchQuery,
            result,
            CancellationToken.None
        );

        Console.WriteLine($"Trajectory Search Evaluation for query '{searchQuery}':");
        Console.WriteLine($"Number of results: {result.Count}");

        // Get the string metric from the evaluation result
        var stringMetric = evaluationResult.Get<StringMetric>(TrajectorySearchRelevanceEvaluator.TrajectoryRelevanceMetricName);
        Console.WriteLine($"Evaluation: {stringMetric.Value}");

        // Validate that we got an evaluation result
        Assert.IsNotNull(evaluationResult);
        Assert.IsNotNull(stringMetric);

        Assert.IsFalse(string.IsNullOrWhiteSpace(stringMetric.Value));

        // The evaluation should contain a score
        Assert.IsTrue(stringMetric.Value.Contains("Score:"),
            "Evaluation should contain a relevance score");
    }
}

