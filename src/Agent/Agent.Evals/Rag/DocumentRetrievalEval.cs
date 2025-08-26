using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Data.AgentMemory;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Plugins.Mocks;
using Agent.Runtime.SubAgents;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.ScenarioTestHelpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Evals.Rag;

[TestClass]
[DoNotParallelize]
public partial class DocumentRetrievalEval
{
    public TestContext TestContext { get; set; }

    private IHost? _host;
    private static int _iterationCount = 1; // Default value

    // Static constructor to initialize _iterationCount
    static DocumentRetrievalEval()
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
            Console.WriteLine($"DocumentRetrievalEval Static Constructor: IterationCount not found or invalid. Using default value: {_iterationCount}.");
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
    public async Task DocumentRetrievalTest()
    {
        if (_host is null)
        {
            Console.WriteLine("Skip DocumentRetrievalTest because feature flag is off");
            return;
        }

        var agentMemoryClient = _host!.Services.GetRequiredService<IAgentMemoryClient>();

        // Read scenarios from JSON file
        var scenariosFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "ACATSG", "tsg-incident-scenarios.json");
        if (!File.Exists(scenariosFilePath))
        {
            Assert.Fail($"Scenarios file not found at: {scenariosFilePath}");
        }

        var scenariosJson = await File.ReadAllTextAsync(scenariosFilePath);
        var scenarios = JsonSerializer.Deserialize<List<TsgIncidentScenario>>(scenariosJson, JsonSerializerOptions.Web);

        if (scenarios == null || scenarios.Count == 0)
        {
            Assert.Fail("No scenarios found in the JSON file.");
        }

        Console.WriteLine($"Testing {scenarios.Count} document scenarios");

        var results = new List<DocumentRetrievalResult>();

        // Create all test cases first
        var testCases = new List<(TsgIncidentScenario scenario, string incident)>();
        foreach (var scenario in scenarios)
        {
            if (scenario.Incidents == null || scenario.Incidents.Count == 0)
            {
                Console.WriteLine($"Skipping scenario {scenario.FileName} - no incidents found");
                continue;
            }

            foreach (var incident in scenario.Incidents)
            {
                testCases.Add((scenario, incident));
            }
        }

        Console.WriteLine($"Running {testCases.Count} tests in parallel (max 10 concurrent)...");

        // Process all test cases in parallel with limited concurrency
        using var semaphore = new SemaphoreSlim(10, 10); // Limit to 10 concurrent operations
        var parallelResults = await Task.WhenAll(testCases.Select(async (testCase, index) =>
        {
            var (scenario, incident) = testCase;
            var testNumber = index + 1;

            await semaphore.WaitAsync();
            try
            {
                // Search for documents using the incident description
                var searchResults = await agentMemoryClient.SearchCustomerDocumentsAsync(
                    new SearchParams(
                        Query: incident,
                        K: 5,
                        EnableHybridSearch: true
                    )
                );

                bool foundCorrectDocument = false;
                bool foundInTop5 = false;
                string? retrievedFileName = null;

                if (searchResults.Count > 0)
                {
                    // Check if the top result matches the expected file name
                    var topResult = searchResults.First();
                    retrievedFileName = topResult.Title; // Assuming Title contains the file name

                    // Check if the retrieved file name matches the expected file name
                    // We'll be flexible with the matching - check if the core filename is present
                    var expectedFileNameCore = Path.GetFileNameWithoutExtension(scenario.FileName);
                    foundCorrectDocument = retrievedFileName?.Contains(expectedFileNameCore, StringComparison.OrdinalIgnoreCase) == true
                                         || retrievedFileName?.Contains(scenario.FileName, StringComparison.OrdinalIgnoreCase) == true;

                    // Check if any of the top 5 results contain the expected filename
                    foreach (var searchResult in searchResults.Take(5))
                    {
                        var resultTitle = searchResult.Title;
                        if (resultTitle?.Contains(expectedFileNameCore, StringComparison.OrdinalIgnoreCase) == true
                            || resultTitle?.Contains(scenario.FileName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            foundInTop5 = true;
                            break;
                        }
                    }
                }

                var result = new DocumentRetrievalResult
                {
                    ExpectedFileName = scenario.FileName,
                    IncidentDescription = incident,
                    RetrievedFileName = retrievedFileName,
                    IsCorrectMatch = foundCorrectDocument,
                    IsFoundInTop5 = foundInTop5,
                    NumberOfResults = searchResults.Count
                };

                var progressMessage = $"Test {testNumber}: Expected '{scenario.FileName}', Retrieved '{retrievedFileName}', Top1 Match: {foundCorrectDocument}, Top5 Match: {foundInTop5}";
                Console.WriteLine(progressMessage);
                // Note: TestContext is not thread-safe, so we'll collect these messages and write them later
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing incident for {scenario.FileName}: {ex.Message}");
                return new DocumentRetrievalResult
                {
                    ExpectedFileName = scenario.FileName,
                    IncidentDescription = incident,
                    RetrievedFileName = null,
                    IsCorrectMatch = false,
                    IsFoundInTop5 = false,
                    NumberOfResults = 0,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                semaphore.Release();
            }
        }));

        results.AddRange(parallelResults);

        // Write progress summary to TestContext
        var progressSummary = "\n=== Test Progress Summary ===\n";
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            progressSummary += $"Test {i + 1}: Expected '{result.ExpectedFileName}', Retrieved '{result.RetrievedFileName}', Top1 Match: {result.IsCorrectMatch}, Top5 Match: {result.IsFoundInTop5}\n";
        }
        TestContext.WriteLine(progressSummary);

        int totalTests = results.Count;
        int successfulMatches = results.Count(r => r.IsCorrectMatch);
        int successfulTop5Matches = results.Count(r => r.IsFoundInTop5);

        // Calculate and report results
        double accuracyPercentage = totalTests > 0 ? (double)successfulMatches / totalTests * 100 : 0;
        double top5AccuracyPercentage = totalTests > 0 ? (double)successfulTop5Matches / totalTests * 100 : 0;

        // Use TestContext to write messages that will be included in test results
        var resultsMessage = $"\n=== Document Retrieval Evaluation Results ===\n" +
                           $"Total tests: {totalTests}\n" +
                           $"Top 1 successful matches: {successfulMatches}\n" +
                           $"Top 1 accuracy: {accuracyPercentage:F2}%\n" +
                           $"Top 5 successful matches: {successfulTop5Matches}\n" +
                           $"Top 5 accuracy: {top5AccuracyPercentage:F2}%";

        TestContext.WriteLine(resultsMessage);

        // Output detailed results for analysis
        var detailedResults = "\n=== Detailed Results ===\n";
        foreach (var result in results.Where(r => !r.IsCorrectMatch))
        {
            detailedResults += $"MISS: Expected '{result.ExpectedFileName}', Got '{result.RetrievedFileName}' (Top1: {result.IsCorrectMatch}, Top5: {result.IsFoundInTop5})\n";
            detailedResults += $"  Query: {result.IncidentDescription.Substring(0, Math.Min(100, result.IncidentDescription.Length))}...\n";
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                detailedResults += $"  Error: {result.ErrorMessage}\n";
            }
            detailedResults += "\n";
        }

        TestContext.WriteLine(detailedResults);

        // Basic validation - we expect at least some successful matches
        Assert.IsTrue(totalTests > 0, "No tests were executed");
        Assert.IsTrue(successfulMatches > 0, "No successful matches found - all document retrievals failed");

        // We can adjust this threshold based on expectations
        var minimumAccuracyThreshold = 80.0; // 80% minimum accuracy
        TestContext.WriteLine($"Document retrieval Top 1 accuracy: {accuracyPercentage:F2}%, Top 5 accuracy: {top5AccuracyPercentage:F2}%");
        Assert.IsTrue(accuracyPercentage >= minimumAccuracyThreshold,
            $"Document retrieval Top 1 accuracy ({accuracyPercentage:F2}%) is below minimum threshold ({minimumAccuracyThreshold}%)");
    }

    // Data models for deserialization
    public class TsgIncidentScenario
    {
        public string FileName { get; set; } = string.Empty;
        public List<string> Incidents { get; set; } = new();
    }

    public class DocumentRetrievalResult
    {
        public string ExpectedFileName { get; set; } = string.Empty;
        public string IncidentDescription { get; set; } = string.Empty;
        public string? RetrievedFileName { get; set; }
        public bool IsCorrectMatch { get; set; }
        public bool IsFoundInTop5 { get; set; }
        public int NumberOfResults { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
