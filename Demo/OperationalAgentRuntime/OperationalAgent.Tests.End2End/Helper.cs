using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using OpenAI.Chat;
using OperationalAgent.Tests.End2End.Fixtures;
using OperationalAgentRuntime.Skills;

using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace E2ETests
{
    public static class Helper
    {
        private static string? _key;

        public static async Task<HttpResponseMessage> SendMessageAndWait(CombinedFixture _fixture, ITestOutputHelper _output, string message, int delayInSeconds = 5)
        {
            _output.WriteLine($"Sending message: {message}");

            StringContent content = new StringContent(message);
            HttpResponseMessage rsp = await _fixture.AzureFunctionsFixture.Client.PostAsync("api/Entrypoint", content);

            await Task.Delay(TimeSpan.FromSeconds(delayInSeconds));

            // Set approval key after first message sent
            if (_key == null)
            {
                //string responseBody = await rsp.Content.ReadAsStringAsync();
                //using JsonDocument doc = JsonDocument.Parse(responseBody);
                //string code = doc.RootElement.GetProperty("StatusQueryGetUri").GetString().Split("code=")[1];
                //_key = code;
            }

            return rsp;
        }

        public static async Task<HttpResponseMessage> SendMessage(CombinedFixture _fixture, ITestOutputHelper _output, string message)
        {
            _output.WriteLine($"Sending message: {message}");

            StringContent content = new StringContent(message);
            HttpResponseMessage rsp = await _fixture.AzureFunctionsFixture.Client.PostAsync("api/Entrypoint", content);

            // Set approval key after first message sent
            if (_key == null)
            {
                //string responseBody = await rsp.Content.ReadAsStringAsync();
                //using JsonDocument doc = JsonDocument.Parse(responseBody);
                //string code = doc.RootElement.GetProperty("StatusQueryGetUri").GetString().Split("code=")[1];
                //_key = code;
            }

            return rsp;
        }

        public static bool IsProcessRunning(Process process)
        {
            try
            {
                // Check the HasExited property to determine if the process is still running
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                // If the process has already been disposed, return false
                return false;
            }
        }

        public static void DisposeAndRunGenericAssertions(CombinedFixture _fixture, ITestOutputHelper _output)
        {
            List<string> output = _fixture.AzureFunctionsFixture.FunctionApp1Process.Output;
            var outputCopy = new List<string>(output);
            string outputString = string.Join(Environment.NewLine, outputCopy);

            _output.WriteLine($"\nAll function app logs:\n\n");
            _output.WriteLine(outputString);

            Assert.True(Helper.MatchesNaturalLanguagePrompt(_fixture, _output, "no exceptions or errors occurred"));
            Assert.True(Helper.IsProcessRunning(_fixture.AzureFunctionsFixture.FunctionApp1Process.FuncHostProcess));

            Helper.SendDisableBasicAuthApprovalEvent(_fixture).GetAwaiter().GetResult();
            _fixture.AzureFunctionsFixture.FunctionApp1Process.Output.Clear();
            _fixture.AzureFunctionsFixture.FunctionApp1Process.WorkingOutput.Clear();
        }

        public static async Task<bool> AnyTaskReturnsTrueAsync(List<Task<bool>> tasks)
        {
            var taskList = tasks.ToList(); // Create a mutable list

            while (taskList.Any())
            {
                // Wait for any task to complete
                Task<bool> finishedTask = await Task.WhenAny(taskList);

                // Remove the finished task from the list
                taskList.Remove(finishedTask);

                // If the result is true, return immediately
                if (await finishedTask)
                {
                    return true;
                }
            }

            // If no task returned true
            return false;
        }

        public static void WriteLine(this IMessageSink sink, string message)
        {
            sink.OnMessage(new DiagnosticMessage(message));
        }

        private static IConfiguration _config;
        public static IConfiguration GetConfig()
        {
            if (_config == null)
            {
                _config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // Default settings
                .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true) // Environment overrides
                .Build();

                string[] settingsToCheck = [
                    "SubscriptionId",
                    "SkipResourceCleanupAfterTestRun"
                ];

                foreach (string setting in settingsToCheck)
                {
                    if (_config[setting] == null)
                    {
                        throw new Exception($"{setting} is not set in appsettings.json or appsettings.development.json");
                    }
                }
            }

            return _config;
        }

        public static string GetWebAppName()
        {
            return $"{Consts.Prefix}-webapp-{GetConfig()["SubscriptionId"].Split("-")[0]}";
        }

        /// <summary>
        /// Calls <see cref="MatchesNaturalLanguagePrompt(CombinedFixture, ITestOutputHelper, string)"/>
        /// 
        /// If call returns True, clears working output to avoid accumulating tokens
        /// </summary>
        /// <param name="_fixture"></param>
        /// <param name="_output"></param>
        /// <param name="expected"></param>
        /// <returns></returns>
        public static bool MatchesNaturalLanguagePromptAndClear(this CombinedFixture _fixture, ITestOutputHelper _output, string expected)
        {
            if (MatchesNaturalLanguagePrompt(_fixture, _output, string.Join("\n", _fixture.AzureFunctionsFixture.FunctionApp1Process.WorkingOutput), expected))
            {
                _fixture.AzureFunctionsFixture.FunctionApp1Process.WorkingOutput.Clear();
                return true;
            }
            return false;
        }


        /// <summary>
        /// [Warning] If you call this directly, make sure you clear your output so you don't start sending too many tokens
        /// </summary>
        /// <param name="_fixture"></param>
        /// <param name="_output"></param>
        /// <param name="expected"></param>
        /// <returns></returns>
        public static bool MatchesNaturalLanguagePrompt(this CombinedFixture _fixture, ITestOutputHelper _output, string expected)
        {
            string actual = string.Join("\n", _fixture.AzureFunctionsFixture.FunctionApp1Process.WorkingOutput);
            return MatchesNaturalLanguagePrompt(_fixture, _output, actual, expected);
        }

        /// <summary>
        /// [Warning] If you call this directly, make sure you clear your output so you don't start sending too many tokens
        /// </summary>
        /// <param name="_fixture"></param>
        /// <param name="_output"></param>
        /// <param name="expected"></param>
        /// <returns></returns>
        public static bool MatchesNaturalLanguagePrompt(this CombinedFixture _fixture, ITestOutputHelper _output, string actual, string expected)
        {
            ChatCompletion completion = _fixture.ChatClientFixture.ChatClient.CompleteChat(
                [
                    new SystemChatMessage(
                        @"You are part of an end to end unit testing framework.
Your job is simply to respond with `true` or `false` depending on if the logs from the app match the expected text.
The text doesn't have to match exactly, but it needs to be close enough that a human would say it's an acceptible response for what we're trying to accomplish."
                    ),
                    new UserChatMessage($@"Expected: {expected}

Actual: {actual}
                    "),
                ]
            );

            bool succeeded = bool.TryParse(completion.Content[0].Text, out var result);

            if (!succeeded)
            {
                throw new Exception($"Natural language test failed to parse the result. Response was: {completion}");
            }

            _output.WriteLine($@"LLM ruled that the output {(result ? "DID" : "DID NOT")} match the following query: ""{expected}""");
            return result;
        }

        public static async Task SendDisableBasicAuthApprovalEvent(CombinedFixture _fixture)
        {
            ApprovalEventPayload payload = new()
            {
                ApprovalAction = true,
                DecisionMakerName = "E2ETests"
            };

            string approvalEndpoint = string.Format(
                Consts.ApprovalUrlFormatString,
                CheckAndDisableBasicAuth.CheckAndDisableBasicAuthAction,
                CheckAndDisableBasicAuth.CheckAndDisableBasicAuthKey,
                _key
            );

            var requestBody = JsonSerializer.Serialize(payload);
            var requestContent = new StringContent(requestBody, Encoding.UTF8, "application/json");

            HttpResponseMessage rsp = await _fixture.AzureFunctionsFixture.Client.PostAsync(approvalEndpoint, requestContent);
        }

        public static void LogAndClearWorkingOutput(CombinedFixture _fixture, ITestOutputHelper _output)
        {
            var outputCopy = new List<string>(_fixture.AzureFunctionsFixture.FunctionApp1Process.WorkingOutput);
            _output.WriteLine(string.Join(Environment.NewLine, outputCopy));
            _fixture.AzureFunctionsFixture.FunctionApp1Process.WorkingOutput.Clear();
        }

        public async static Task DisposeIssues(CombinedFixture _fixture, ITestOutputHelper _output)
        {
            await SendMessageAndWait(_fixture, _output, $"close all issues with the [E2ETests] tag: https://github.com/sanchitmehta/sample-app");
        }
    }
}
