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
            var testResultsLocation = Environment.GetEnvironmentVariable("SREA3P_TESTING_TEST_RESULTS_LOCATION");
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
        
        var testDefinitions = new Dictionary<string, TestResult>();
        var testIdToTestNameMap = new Dictionary<string, string>();

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

            var result = new TestResult
            {
                BuildId = buildId,
                BuildNumber = buildNumber
            };

            foreach (XmlNode unittest in definition.ChildNodes)
            {
                if (unittest.Name == "Owners")
                {
                    result.Owner = unittest.FirstChild?.Attributes?.GetNamedItem("name")?.Value;
                }

                if (unittest.Name == "TestMethod")
                {
                    var className = unittest.Attributes?.GetNamedItem("className")?.Value;
                    var testMethodName = unittest.Attributes?.GetNamedItem("name")?.Value;
                    result.ClassName = className;
                    result.TestMethod = testMethodName;
                    if (!string.IsNullOrEmpty(testMethodName))
                    {
                        testIdToTestNameMap[testId] = testMethodName;
                    }
                    else
                    {
                        Console.WriteLine($"Test method name is empty for test id {testId}.");
                        continue;
                    }

                    if (!testDefinitions.ContainsKey(testMethodName))
                    {
                        testDefinitions[testMethodName] = result;
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

            var testName = testIdToTestNameMap[testId];
            var result = testDefinitions[testName];

            result.StartTime = testResult.Attributes?.GetNamedItem("startTime")?.Value;
            result.EndTime = testResult.Attributes?.GetNamedItem("endTime")?.Value;
            result.Duration = testResult.Attributes?.GetNamedItem("duration")?.Value;
            var outCome = testResult.Attributes?.GetNamedItem("outcome")?.Value;
            result.TotalRuns++;

            if (outCome == "Failed")
            {
                foreach (XmlNode output in testResult.ChildNodes)
                {
                    foreach (XmlNode element in output.ChildNodes)
                    {
                        switch (element.Name)
                        {
                            // Output information is too large and cannot be sent to eventhub
                            //case "StdOut":
                            //    result.Output = element.InnerText;
                            //    break;
                            case "ErrorInfo":
                                result.ErrorInfo.Add(element.InnerText);
                                break;
                        }
                    }
                }

                result.FailedRuns++;
            }
            else
            {
                result.PassedRuns++;
            }

            testDefinitions[testName] = result;
        }

        // Send to EventHub
        Console.WriteLine("Start sending test results to event hub.");

        var clientCert = Environment.GetEnvironmentVariable("SRE_AGENT_TESTING_CLIENT_CERT");
        var clientId = Environment.GetEnvironmentVariable("SRE_AGENT_TESTING_CLIENT_ID");
        var tenantId = Environment.GetEnvironmentVariable("SRE_AGENT_TESTING_TENANT_ID");
        var azureCloud = Environment.GetEnvironmentVariable("SRE_AGENT_TESTING_ENVIRONMENT_AZURE_CLOUD");
        var azureAuthorityHost = Environment.GetEnvironmentVariable("SRE_AGENT_TESTING_ENVIRONMENT_AUTHORITY_HOST");

        X509Certificate2 clientCertificate = null;
        if (!string.IsNullOrEmpty(clientCert))
        {
            clientCertificate = new X509Certificate2(Convert.FromBase64String(clientCert), string.Empty, X509KeyStorageFlags.EphemeralKeySet);
        }

        var cred = string.IsNullOrEmpty(clientCert) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId) ?
            GetIntegrationTestsCredentials(null, null, null) :
            GetIntegrationTestsCredentials(clientId, clientCertificate, tenantId, string.Equals(azureCloud, "Public", StringComparison.OrdinalIgnoreCase), new Uri(azureAuthorityHost));

        var keyVaultUrl = Environment.GetEnvironmentVariable("SRE_AGENT_TESTING_KEY_VAULT");
        keyVaultUrl = string.IsNullOrEmpty(keyVaultUrl) ? "https://sre-agent-keyvault.vault.azure.net/" : keyVaultUrl;
        var kvClient = GetIntegrationTestsKVClient(keyVaultUrl, cred);
        var secretBundle = await kvClient.GetSecretAsync(EventHubConnectionStr);
        var connectionStr = secretBundle.Value.Value;

        var producerClient = new EventHubProducerClient(
            connectionStr,
            EventHubName);

        // Create a batch of events
        using EventDataBatch eventData = await producerClient.CreateBatchAsync();

        foreach (var testResult in testDefinitions)
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

    public static TokenCredential GetIntegrationTestsCredentials(string clientId, X509Certificate2 clientCertificate, string tenantId, bool useSNI = false, Uri authorityHost = null)
    {
        if (clientCertificate != null)
        {
            return new ClientCertificateCredential(
                tenantId,
                clientId,
                clientCertificate,
                new ClientCertificateCredentialOptions()
                {
                    AuthorityHost = authorityHost,
                    SendCertificateChain = useSNI,

                });
        }

        return new DefaultAzureCredential();
    }

    public static SecretClient GetIntegrationTestsKVClient(string vaultUrl, TokenCredential tokenCredential)
    {
        return new SecretClient(
            new Uri(vaultUrl),
            tokenCredential,
            new SecretClientOptions()
            {
                Diagnostics =
                {
#if DEBUG
                        IsLoggingContentEnabled = true,
                        LoggedHeaderNames = {"*"},
                        LoggedQueryParameters = {"*"},
#else
                        IsLoggingContentEnabled = false,
#endif
                        IsLoggingEnabled = true
                }
            });
    }
}
