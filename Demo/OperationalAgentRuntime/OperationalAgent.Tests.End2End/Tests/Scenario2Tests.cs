using Microsoft.Extensions.Configuration;
using OperationalAgent.Tests.End2End.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace E2ETests.Tests
{
    [Collection(nameof(CombinedTestCollection))]
    public class Scenario2Tests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;

        public Scenario2Tests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = Helper.GetConfig();

            Helper.SendMessage(_fixture, _output, "clear state").GetAwaiter().GetResult();

        }

        [Fact]
        public async Task CanTellHowManyResources()
        {
            await Helper.SendMessage(_fixture, _output, $"track sub id {_config["SubscriptionId"]}");
            Assert.True(await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync("verified my access"));

            await Helper.SendMessage(_fixture, _output, $"How many apps are you managing?");
            await Task.Delay(TimeSpan.FromSeconds(60));
            Assert.True(_fixture.MatchesNaturalLanguagePrompt(_output, "includes a numbered list of the resources being managed (not a basic auth table)"));
        }

        [Fact]
        public async Task CanGiveBestPractices()
        {
            await Helper.SendMessage(_fixture, _output, $"track sub id {_config["SubscriptionId"]}");
            await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync(OperationalAgentRuntime.Consts.Verified);

            await Helper.SendMessage(_fixture, _output, $"Can you ensure we're applying best practices to my web apps");
            Assert.True(await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync("Click here to approve", 60));
            Assert.True(_fixture.MatchesNaturalLanguagePrompt(_output, "a table of apps where basic auth is allowed"));
            await Helper.SendDisableBasicAuthApprovalEvent(_fixture);

            Helper.LogAndClearWorkingOutput(_fixture, _output);

            Assert.True(await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync("App Name", 60));
            Assert.True(_fixture.MatchesNaturalLanguagePrompt(_output, "a table that shows basic auth is disabled"));
        }

        [Fact]
        public async Task CanGiveStatusUpdate()
        {
            await Helper.SendMessage(_fixture, _output, $"track sub id {_config["SubscriptionId"]}");
            Assert.True(await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync(OperationalAgentRuntime.Consts.Verified));
            Assert.True(await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync(_config["AppName"]));
        }

        public void Dispose()
        {
            Helper.DisposeAndRunGenericAssertions(_fixture, _output);
        }
    }
}