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
using Agent.Evals.Common.Evaluators;

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

            var file = Directory.GetFiles(filePath, "*.trx");
            if (file == null)
            {
                Console.WriteLine("No valid file path found.");
                return;
            }

            if (file.Length > 1)
            {
                throw new Exception("Multiple test result files found.");
            }

            testResultFile = file[0];
        }
        catch (Exception ex)
        {
            var warn = new WarningException(ex.Message);
            Console.Write(warn.ToString());
            return;
        }

        Console.WriteLine($"Try to load {testResultFile}");
        XmlDocument doc = new();
        doc.Load(testResultFile);
        if (doc == null)
        {
            var warn = new WarningException($"Fail to parse test result file. Invalid content for test result file {testResultFile}.");
            Console.Write(warn.ToString());
            return;
        }

        var buildId = Environment.GetEnvironmentVariable("Build_BuildId", EnvironmentVariableTarget.Process);
        var buildNumber = Environment.GetEnvironmentVariable("Build_BuildNumber", EnvironmentVariableTarget.Process);
        
        var testResults = new Dictionary<string, TestResult>();
        var testIdToTestInfoMap = new Dictionary<string, (string, string)>();

        XmlNode? testDefinitionsNode = null;
        XmlNode? resultsNode = null;
        foreach (XmlNode node in doc.ChildNodes)
        {
            if (node.Name == "TestRun")
            {
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

            if (string.IsNullOrEmpty(testId))
            {
                continue;
            }

            (var testName, var className) = testIdToTestInfoMap[testId];

            var result = new TestResult
            {
                TestId = testId,
                TestMethod = testName,
                ClassName = className,
                BuildId = buildId,
                BuildNumber = buildNumber,
                StartTime = testResult.Attributes?.GetNamedItem("startTime")?.Value,
                EndTime = testResult.Attributes?.GetNamedItem("endTime")?.Value,
            };

            foreach (XmlNode childNode in testResult.ChildNodes)
            {
                if (childNode.Name == "Output")
                {
                    var stdOut = childNode.ChildNodes[0].InnerText;
                    var jsonStartIndex = stdOut.IndexOf("{");
                    var jsonString = stdOut.Substring(jsonStartIndex);
                    var testResultObj = JsonSerializer.Deserialize<Dictionary<string, EvalsResult>>(jsonString);

                    if (testResultObj == null)
                    {
                        Console.WriteLine($"Fail to parse test result {stdOut}");
                        continue;
                    }

                    var coherence = testResultObj[SreAgentCoherenceEvaluator.SreAgentCoherenceMetricName];
                    var fluency = testResultObj[SreAgentFluencyEvaluator.SreAgentFluencyMetricName];
                    var equivalence = testResultObj[SreAgentEquivalenceEvaluator.SreAgentEquivalenceMetricName];
                    var groundedness = testResultObj[SreAgentGroundednessEvaluator.SreAgentGroundednessMetricName];
                    result.CoherenceRating = coherence.Value;
                    result.CoherenceReasoning = coherence.Reason;
                    result.FluencyRating = fluency.Value;
                    result.FluencyReasoning = fluency.Reason;
                    result.EquivalenceRating = equivalence.Value;
                    result.EquivalenceReasoning = equivalence.Reason;
                    result.GroundednessRating = groundedness.Value;
                    result.GroundednessReasoning = groundedness.Reason;
                }
            }

            testResults[testId] = result;
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
