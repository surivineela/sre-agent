using Agent.Evals.Cmd.Helpers;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text;
using System.Xml;
using Azure.Messaging.EventHubs.Producer;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Messaging.EventHubs;
using Agent.Evals.Common;
using System.Collections;
using System.Text.RegularExpressions;

namespace Agent.Evals.Cmd;

public class Program
{
    private const string EventHubName = "sre-agent-test-results";

    private const string EventHubConnectionStr = "TestResultEventHubConnectionStr";

    public static async Task Main()
    {
        string testResultFile;
        try
        {
            var testResultsLocation = Environment.GetEnvironmentVariable("SRE_AGENT_TESTING_TEST_RESULTS_LOCATION");
            if (string.IsNullOrEmpty(testResultsLocation))
            {
                testResultsLocation = "src\\Agent\\Agent.Evals\\TestResults\\";
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), testResultsLocation);

            if (!OperatingSystem.IsWindows())
            {
                filePath = filePath.Replace('\\', '/');
            }

            Console.WriteLine(filePath);

            var files = Directory.GetFiles(filePath, "*.trx");
            if (files == null || files.Length == 0)
            {
                Console.WriteLine("No valid file path found.");
                return;
            }

            foreach(var f in files)
            {
                await ProcessFile(f);
            }
        }
        catch (Exception ex)
        {
            var warn = new WarningException(ex.Message);
            Console.Write(warn.ToString());
            return;
        }
    }

    public static async Task ProcessFile(string testResultFile)
    { 
        Console.WriteLine($"Try to load {testResultFile}");
        XmlDocument doc = new();
        doc.Load(testResultFile);
        if (doc == null)
        {
            var warn = new WarningException($"Fail to parse test result file. Invalid content for test result file {testResultFile}.");
            Console.Write(warn.ToString());
            return;
        }

        var buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID");
        var buildNumber = Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER");
        var buildBranch = Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH");

        // Iterate through all environment variables and log those starting with "Build"
        Console.WriteLine("Build-related environment variables:");
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            string key = entry.Key.ToString();
            if (key.StartsWith("Build", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  {key} = {entry.Value}");
            }
        }
        Console.WriteLine();

        var testResults = new Dictionary<string, TestResult>();
        var testIdToTestInfoMap = new Dictionary<string, (string, string)>();
        string testRunId = string.Empty;

        XmlNode? testDefinitionsNode = null;
        XmlNode? resultsNode = null;
        foreach (XmlNode node in doc.ChildNodes)
        {
            if (node.Name == "TestRun")
            {
                if (string.IsNullOrEmpty(testRunId))
                {
                    testRunId = node.Attributes?.GetNamedItem("id")?.Value ?? string.Empty;
                }

                foreach (XmlNode childnode in node.ChildNodes)
                {
                    if (childnode.Name == "TestDefinitions")
                    {
                        testDefinitionsNode = childnode;
                    }

                    if (childnode.Name == "Results")
                    {
                        resultsNode = childnode;
                    }
                }
            }
        }

        foreach (XmlNode definition in testDefinitionsNode.ChildNodes)
        {
            var testName = definition.Attributes?.GetNamedItem("name")?.Value;
            var testId = definition.Attributes?.GetNamedItem("id")?.Value;
            if (string.IsNullOrEmpty(testName) || string.IsNullOrEmpty(testId))
            {
                continue;
            }

            foreach (XmlNode unittest in definition.ChildNodes)
            {
                if (unittest.Name == "TestMethod")
                {
                    var className = unittest.Attributes?.GetNamedItem("className")?.Value;
                    var testMethodName = unittest.Attributes?.GetNamedItem("name")?.Value;

                    if (!string.IsNullOrEmpty(testMethodName))
                    {
                        testIdToTestInfoMap[testId] = (testMethodName, className);
                    }
                }
            }
        }

        foreach (XmlNode testResult in resultsNode.ChildNodes)
        {
            var testId = testResult.Attributes?.GetNamedItem("testId")?.Value;
            var outcome = testResult.Attributes?.GetNamedItem("outcome")?.Value;

            bool hasPassed = string.Equals(outcome, "Passed", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(testId))
            {
                continue;
            }

            (var testName, var className) = testIdToTestInfoMap[testId];

            foreach (XmlNode childNode in testResult.ChildNodes)
            {
                if (childNode.Name == "Output")
                {
                    var stdOut = childNode.ChildNodes[0].InnerText;

                    // Parse AdditionalInfo in kusto with parse-kv:
                    // | parse-kv AdditionalInfo as (TestRunId:string, Branch:string) with (pair_delimiter=",", kv_delimiter=":")
                    var runInfoBuilder = new StringBuilder();
                    runInfoBuilder.Append($"TestRunId:{testRunId},");
                    runInfoBuilder.Append($"Branch:{buildBranch},");

                    // If the test fails before any eval data was emitted, we still need a test result with HasPassed = false
                    var topLevelResult = new TestResult
                    {
                        TestId = $"{testId}__{Guid.NewGuid()}",
                        TestMethod = testName,
                        ClassName = className,
                        BuildId = buildId,
                        BuildNumber = buildNumber,
                        StartTime = testResult.Attributes?.GetNamedItem("startTime")?.Value,
                        EndTime = testResult.Attributes?.GetNamedItem("endTime")?.Value,
                        HasPassed = hasPassed,
                        AdditionalInfo = runInfoBuilder.ToString(),
                    };

                    var modelNamePattern = "\"LLMDeploymentName\":\"(.*?)\"";
                    var match = Regex.Match(stdOut, modelNamePattern);
                    topLevelResult.LLMDeploymentName = match.Success ? match.Groups[1].Value : "Unknown";
                    testResults[topLevelResult.TestId] = topLevelResult;

                    foreach (var line in stdOut.Split('\n'))
                    {
                        if (!line.Contains("{\"WordCount"))
                        {
                            continue;
                        }

                        var evalsGuid = Guid.NewGuid().ToString();
                        var jsonStartIndex = line.IndexOf("{");
                        var jsonString = line.Substring(jsonStartIndex);
                        var evaluationResults = JsonSerializer.Deserialize<EvaluationResults>(jsonString);

                        if (evaluationResults == null)
                        {
                            Console.WriteLine($"Fail to parse test result {stdOut}");
                            continue;
                        }

                        var result = new TestResult
                        {
                            TestId = $"{testId}__{evalsGuid}",
                            TestMethod = testName,
                            ClassName = className,
                            BuildId = buildId,
                            BuildNumber = buildNumber,
                            StartTime = testResult.Attributes?.GetNamedItem("startTime")?.Value,
                            EndTime = testResult.Attributes?.GetNamedItem("endTime")?.Value,
                        };

                        result.WordCountRating = evaluationResults.WordCount?.Value;
                        result.WordCountReasoning = evaluationResults.WordCount?.Reason;
                        result.CoherenceRating = evaluationResults.Coherence?.Value;
                        result.CoherenceReasoning = evaluationResults.Coherence?.Reason;
                        result.FluencyRating = evaluationResults.Fluency?.Value;
                        result.FluencyReasoning = evaluationResults.Fluency?.Reason;
                        result.EquivalenceRating = evaluationResults.Equivalence?.Value;
                        result.EquivalenceReasoning = evaluationResults.Equivalence?.Reason;
                        result.GroundednessRating = evaluationResults.Groundedness?.Value;
                        result.GroundednessReasoning = evaluationResults.Groundedness?.Reason;
                        result.HasPassed = hasPassed;
                        result.LLMDeploymentName = evaluationResults.LLMDeploymentName;

                        var additionalInfoBuilder = new StringBuilder(runInfoBuilder.ToString());
                        if(!string.IsNullOrEmpty(evaluationResults.UserInput))
                        {
                            additionalInfoBuilder.Append($"UserInput:{evaluationResults.UserInput.Replace(",", Uri.EscapeDataString(","))},");
                        }
                        if(!string.IsNullOrEmpty(evaluationResults.ModelResponse))
                        {
                            additionalInfoBuilder.Append($"ModelResponse:{evaluationResults.ModelResponse.Replace(",", Uri.EscapeDataString(","))},");
                        }
                        result.AdditionalInfo = additionalInfoBuilder.ToString();

                        testResults[result.TestId] = result;
                    }
                }
            }
        }

        // Send to EventHub
        Console.WriteLine("Start sending test results to event hub.");

        var eventHubConnectionString = Environment.GetEnvironmentVariable("SRE_AGENT_EVENT_HUB_CONNECTION_STRING");

        var producerClient = new EventHubProducerClient(
            eventHubConnectionString,
            EventHubName);

        // Create a batch of events
        using EventDataBatch eventData = await producerClient.CreateBatchAsync();

        foreach (var testResult in testResults)
        {
            var message = JsonSerializer.Serialize(testResult.Value);
            if (!eventData.TryAdd(new EventData(Encoding.UTF8.GetBytes(message))))
            {
                throw new Exception("Event is too large for the batch and cannot be sent.");
            }
        }

        // Use the producer client to send the batch of events to the event hub
        await producerClient.SendAsync(eventData);

        Console.WriteLine("Finished!");
    }
}
