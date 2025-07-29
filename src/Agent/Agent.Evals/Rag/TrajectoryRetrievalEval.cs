using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Data.AgentMemory;
using Agent.Evals.Evaluators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.WebApi;
using Prometheus.Protobuf;

namespace Agent.Evals.Rag;

[TestClass]
[DoNotParallelize]
public partial class TrajectoryEval
{
    public TestContext TestContext { get; set; }

    private static IHost? _host;
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

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        // Create thread repository first
        var builder = TestHelpers.BuildTestApp(out var llmDeploymentName);
        if (!builder.IsAgentMemoryEnabled())
        {
            Console.WriteLine("AgentMemory is not enabled, skipping class initialization.");
            return;
        }

        builder.RegisterDefaultServices();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.ConfigureAgentMemory();

        _host = builder.Build();
        await _host.StartAsync();

        // Setup the index once for the entire test class
        await SetupIndex();
        Console.WriteLine("Index initialized for test class.");
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        // No initialization needed per test since we use ClassInitialize
        if (_host == null)
        {
            Assert.Fail("Host was not initialized. Check if AgentMemory is enabled.");
        }
    }

    [TestMethod]
    public async Task TestBasicRetrieval()
    {
        var agentMemoryClient = _host!.Services.GetRequiredService<IAgentMemoryClient>();
        await TestBasicRetrieval(agentMemoryClient);
    }

    private async Task TestBasicRetrieval(IAgentMemoryClient agentMemoryClient)
    {
        Console.WriteLine("=== Basic Retrieval ===");
        var basicQueries = LoadBasicSearchQueries();

        foreach (var query in basicQueries)
        {
            var result = await agentMemoryClient.SearchTrajectoriesAsync(query, enableHybridSearch: true);
    
            // Assert that we get at least some results
            Assert.IsTrue(result.Count > 0, $"Basic query '{query}' should return results");
        }
    }

    [TestMethod]
    public async Task TestComprehensiveEvaluation()
    {
        var agentMemoryClient = _host!.Services.GetRequiredService<IAgentMemoryClient>();
        var chatClient = _host.Services.GetRequiredService<IChatClient>();
        var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<TrajectorySearchRelevanceEvaluator>();
        
        await TestComprehensiveEvaluation(agentMemoryClient, chatClient, logger);
    }

    private async Task TestComprehensiveEvaluation(IAgentMemoryClient agentMemoryClient, IChatClient chatClient, ILogger<TrajectorySearchRelevanceEvaluator> logger)
    {
        Console.WriteLine("\n=== Comprehensive Evaluation with LLM Judge ===");
        var evaluationQueries = LoadComprehensiveEvaluationQueries();

        var trajectoryEvaluator = new TrajectorySearchRelevanceEvaluator(chatClient, logger);

        foreach (var query in evaluationQueries)
        {
            var result = await agentMemoryClient.SearchTrajectoriesAsync(query, enableHybridSearch: true);

            var evaluationResult = await trajectoryEvaluator.EvaluateTrajectorySearchAsync(
                query,
                result,
                CancellationToken.None
            );

            // Test only the LLM-assessable metrics
            NumericMetric relevanceMetric = evaluationResult.Get<NumericMetric>(TrajectorySearchRelevanceEvaluator.TrajectoryRelevanceMetricName);
            NumericMetric diversityMetric = evaluationResult.Get<NumericMetric>(TrajectorySearchRelevanceEvaluator.DiversityMetricName);
            NumericMetric rankingQualityMetric = evaluationResult.Get<NumericMetric>(TrajectorySearchRelevanceEvaluator.RankingQualityMetricName);
            NumericMetric actionabilityMetric = evaluationResult.Get<NumericMetric>(TrajectorySearchRelevanceEvaluator.ActionabilityMetricName);

          
            // Assert minimum quality thresholds
            //Assert.IsTrue(relevanceMetric.Value >= 4, $"Overall Relevance should be at least 4 (Good), got {relevanceMetric.Value} for query '{query}'");
            //Assert.IsTrue(diversityMetric.Value >= 0.6, $"Diversity should be at least 0.6 (Good), got {diversityMetric.Value:F3} for query '{query}'");
            //Assert.IsTrue(rankingQualityMetric.Value >= 4, $"Ranking Quality should be at least 4 (Good), got {rankingQualityMetric.Value} for query '{query}'");
            //Assert.IsTrue(actionabilityMetric.Value >= 4, $"Actionability should be at least 4 (Good), got {actionabilityMetric.Value} for query '{query}'");

            // Assert that interpretation ratings are at least Good
            // Assert.IsTrue(relevanceMetric.Interpretation?.Rating >= EvaluationRating.Good, 
            //    $"Relevance interpretation should be at least Good, got {relevanceMetric.Interpretation?.Rating} for query '{query}'");
            // Assert.IsTrue(diversityMetric.Interpretation?.Rating >= EvaluationRating.Good, 
            //    $"Diversity interpretation should be at least Good, got {diversityMetric.Interpretation?.Rating} for query '{query}'");
            // Assert.IsTrue(rankingQualityMetric.Interpretation?.Rating >= EvaluationRating.Good, 
            //    $"Ranking Quality interpretation should be at least Good, got {rankingQualityMetric.Interpretation?.Rating} for query '{query}'");
            // Assert.IsTrue(actionabilityMetric.Interpretation?.Rating >= EvaluationRating.Good, 
            //    $"Actionability interpretation should be at least Good, got {actionabilityMetric.Interpretation?.Rating} for query '{query}'");
        }

        // Test empty results scenario
        Console.WriteLine("=== Testing Empty Results ===");
        var emptyQuery = "this query should return no results at all";
        var emptyResults = await agentMemoryClient.SearchTrajectoriesAsync(emptyQuery, enableHybridSearch: true);
        var emptyEvaluationResult = await trajectoryEvaluator.EvaluateTrajectorySearchAsync(
            emptyQuery,
            emptyResults,
            CancellationToken.None
        );

        var emptyRelevanceMetric = emptyEvaluationResult.Get<NumericMetric>(TrajectorySearchRelevanceEvaluator.TrajectoryRelevanceMetricName);
        var emptyDiversityMetric = emptyEvaluationResult.Get<NumericMetric>(TrajectorySearchRelevanceEvaluator.DiversityMetricName);

        //Assert.IsTrue(emptyRelevanceMetric.Value <= 2, $"Overall Relevance should be at least 2 (Bad), got {emptyRelevanceMetric.Value} for query '{emptyQuery}'");
        //Assert.IsTrue(emptyDiversityMetric.Value <= 0.8, $"Overall Relevance should be less than 0.8 (Very Good), got {emptyDiversityMetric.Value} for query '{emptyQuery}'");
    }

    [TestMethod]
    public async Task TestGroundTruthEvaluation()
    {
        var agentMemoryClient = _host!.Services.GetRequiredService<IAgentMemoryClient>();
        var chatClient = _host.Services.GetRequiredService<IChatClient>();
        var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<TrajectorySearchRelevanceEvaluator>();
        
        await TestGroundTruthEvaluation(agentMemoryClient, chatClient, logger);
    }

    private async Task TestGroundTruthEvaluation(IAgentMemoryClient agentMemoryClient, IChatClient chatClient, ILogger<TrajectorySearchRelevanceEvaluator> logger)
    {
        Console.WriteLine("\n=== Ground Truth Evaluation ===");
        
        var trajectoryEvaluator = new TrajectorySearchRelevanceEvaluator(chatClient, logger);

        // Load ground truth mappings from data file
        var groundTruthMappings = LoadGroundTruthMappings();

        foreach (var (query, expectedGroundTruthIds) in groundTruthMappings)
        {
            // Always want it to return 1 result. Can tweak this in the future if needed
            var searchResults = await agentMemoryClient.SearchTrajectoriesAsync(query, 1, enableHybridSearch: true);
            
            var evaluation = trajectoryEvaluator.EvaluateTrajectorySearchWithGroundTruthAsync(
                query,
                searchResults,
                expectedGroundTruthIds,
                CancellationToken.None
            );

            var precision = evaluation.Get<NumericMetric>(TrajectorySearchRelevanceEvaluator.PrecisionMetricName);
            var recall = evaluation.Get<NumericMetric>(TrajectorySearchRelevanceEvaluator.RecallMetricName);
            var f1Score = evaluation.Get<NumericMetric>(TrajectorySearchRelevanceEvaluator.F1ScoreMetricName);

            var actualPrecision = precision.Value;
            var actualRecall = recall.Value;
            var actualF1Score = f1Score.Value;

            Assert.IsTrue(actualPrecision >= 0.3, $"Precision should be ≥ 0.3 for query '{query}', got {actualPrecision:F3}");
            Assert.IsTrue(actualRecall >= 0.5, $"Recall should be ≥ 0.5 for query '{query}', got {actualRecall:F3}");
            Assert.IsTrue(actualF1Score >= 0.3, $"F1‑score should be ≥ 0.3 for query '{query}', got {actualF1Score:F3}");
        }
    }

    #region Data Models for JSON Deserialization

    private record BasicRetrievalQueriesData(string[] BasicRetrievalQueries);
    private record ComprehensiveEvaluationQueriesData(string[] ComprehensiveEvaluationQueries);
    private record GroundTruthMappingsData(Dictionary<string, string[]> GroundTruthMappings);

    #endregion

    #region Test Data Loading Methods

    /// <summary>
    /// Loads basic search queries for trajectory retrieval testing from JSON data file
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the data file is not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when the data file cannot be parsed</exception>
    private static string[] LoadBasicSearchQueries()
    {
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "TrajectorySearch", "basic-retrieval-queries.json");
        
        if (!File.Exists(dataFilePath))
        {
            throw new FileNotFoundException($"Basic retrieval queries data file not found: {dataFilePath}");
        }

        try
        {
            var jsonContent = File.ReadAllText(dataFilePath);
            var data = JsonSerializer.Deserialize<BasicRetrievalQueriesData>(jsonContent, JsonSerializerOptions.Web);
            
            if (data?.BasicRetrievalQueries == null || data.BasicRetrievalQueries.Length == 0)
            {
                throw new InvalidOperationException($"No basic retrieval queries found in data file: {dataFilePath}");
            }

            return data.BasicRetrievalQueries;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse basic retrieval queries from {dataFilePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads comprehensive evaluation queries that target specific trajectory symptoms and root causes from JSON data file
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the data file is not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when the data file cannot be parsed</exception>
    private static string[] LoadComprehensiveEvaluationQueries()
    {
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "TrajectorySearch", "comprehensive-evaluation-queries.json");
        
        if (!File.Exists(dataFilePath))
        {
            throw new FileNotFoundException($"Comprehensive evaluation queries data file not found: {dataFilePath}");
        }

        try
        {
            var jsonContent = File.ReadAllText(dataFilePath);
            var data = JsonSerializer.Deserialize<ComprehensiveEvaluationQueriesData>(jsonContent, JsonSerializerOptions.Web);
            
            if (data?.ComprehensiveEvaluationQueries == null || data.ComprehensiveEvaluationQueries.Length == 0)
            {
                throw new InvalidOperationException($"No comprehensive evaluation queries found in data file: {dataFilePath}");
            }

            return data.ComprehensiveEvaluationQueries;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse comprehensive evaluation queries from {dataFilePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads ground truth mappings for trajectory search evaluation with expected relevant trajectories from JSON data file
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the data file is not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when the data file cannot be parsed</exception>
    private static Dictionary<string, HashSet<string>> LoadGroundTruthMappings()
    {
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "TrajectorySearch", "ground-truth-mappings.json");
        
        if (!File.Exists(dataFilePath))
        {
            throw new FileNotFoundException($"Ground truth mappings data file not found: {dataFilePath}");
        }

        try
        {
            var jsonContent = File.ReadAllText(dataFilePath);
            var data = JsonSerializer.Deserialize<GroundTruthMappingsData>(jsonContent, JsonSerializerOptions.Web);
            
            if (data?.GroundTruthMappings == null || data.GroundTruthMappings.Count == 0)
            {
                throw new InvalidOperationException($"No ground truth mappings found in data file: {dataFilePath}");
            }

            var result = new Dictionary<string, HashSet<string>>();
            foreach (var (query, expectedTrajectoryIds) in data.GroundTruthMappings)
            {
                if (expectedTrajectoryIds == null || expectedTrajectoryIds.Length == 0)
                {
                    throw new InvalidOperationException($"No expected trajectory IDs found for query '{query}' in data file: {dataFilePath}");
                }
                result[query] = new HashSet<string>(expectedTrajectoryIds);
            }
            
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse ground truth mappings from {dataFilePath}: {ex.Message}", ex);
        }
    }

    #endregion

    private static async Task SetupIndex()
    {
        var indexSerivce = _host!.Services.GetRequiredService<ISearchIndexService>();
        var embeddingGenerator = _host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Console.WriteLine("Setting up search index...");
        
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

        Console.WriteLine($"Found {trajectoryFiles.Length} trajectory files to index.");

        foreach (var trajectoryFile in trajectoryFiles)
        {
            var trajectoryContent = await File.ReadAllTextAsync(trajectoryFile);
            var trajectory = JsonSerializer.Deserialize<ProcessedTrajectoryOutput_v3>(trajectoryContent, JsonSerializerOptions.Web);
            var embedding = await embeddingGenerator.GenerateVectorAsync(trajectory!.SymptomsObserved);
            var agentMemory = AgentMemory.FromTrajectory(Guid.NewGuid().ToString(), trajectory, [.. embedding.Span]);

            // Index the trajectory content
            await indexSerivce.IndexContentAsync(agentMemory);
        }

        Console.WriteLine("Index setup completed.");
    }
   
}
