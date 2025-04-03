// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// Comment out until the tools required by these scenarios are migrated

//using Microsoft.Extensions.Configuration;
//using Agent.Tests.End2End.Fixtures;
//using Xunit;
//using Xunit.Abstractions;

//namespace Agent.Tests.End2End
//{
//    [Collection(nameof(CombinedWithWebAppTestCollection))]
//    public class Scenario1Tests : IDisposable
//    {
//        private readonly CombinedFixture _fixture;
//        private readonly ITestOutputHelper _output;
//        private readonly IConfiguration _config;

//        public Scenario1Tests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
//        {
//            _fixture = fixture;
//            _output = testOutputHelper;
//            _config = Helper.GetConfig();

//            Helper.SendMessage(_fixture, _output, "clear state").GetAwaiter().GetResult();

//        }

//        [Fact]
//        public async Task EmptyWhatAreYouDoing()
//        {
//            await Helper.SendMessage(_fixture, _output, $"what are you doing");
//            await Task.Delay(TimeSpan.FromSeconds(30));
//            Assert.True(_fixture.MatchesNaturalLanguagePrompt(_output, "not doing anything"));
//        }

//        [Fact]
//        public async Task ManagingWhatAreYouDoing()
//        {
//            await Helper.SendMessage(_fixture, _output, $"track sub id {_config["SubscriptionId"]}");
//            await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync(OperationalAgentRuntime.Consts.Verified);

//            await Helper.SendMessage(_fixture, _output, $"what are you doing");
//            await Task.Delay(TimeSpan.FromSeconds(15));
//            Assert.True(_fixture.MatchesNaturalLanguagePrompt(_output, "ingesting resources in subscription"));
//        }

//        [Fact]
//        public async Task CanGiveBestPractices()
//        {
//            await Helper.SendMessage(_fixture, _output, $"track sub id {_config["SubscriptionId"]}");
//            await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync(OperationalAgentRuntime.Consts.Verified);

//            await Helper.SendMessage(_fixture, _output, $"Can you ensure we're applying best practices to my web apps");
//            Assert.True(await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync("Click here to approve", 60));
//            Assert.True(_fixture.MatchesNaturalLanguagePrompt(_output, "a table of apps where basic auth is allowed"));
//            await Helper.SendDisableBasicAuthApprovalEvent(_fixture);

//            Helper.LogAndClearWorkingOutput(_fixture, _output);

//            Assert.True(await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync("App Name", 60));
//            Assert.True(_fixture.MatchesNaturalLanguagePrompt(_output, "a table that shows basic auth is disabled"));
//        }

//        [Fact]
//        public async Task CanGiveStatusUpdate()
//        {
//            await Helper.SendMessage(_fixture, _output, $"track sub id {_config["SubscriptionId"]}");
//            Assert.True(await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync(OperationalAgentRuntime.Consts.Verified));
//            Assert.True(await _fixture.AzureFunctionsFixture.FunctionApp1Process.WaitForOutputAsync(_config["AppName"]));
//        }

//        public void Dispose()
//        {
//            Helper.DisposeAndRunGenericAssertions(_fixture, _output);
//        }
//    }
//}
