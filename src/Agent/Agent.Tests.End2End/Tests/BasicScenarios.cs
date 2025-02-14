using Microsoft.Extensions.Configuration;
using Agent.Tests.End2End.Fixtures;
using Xunit;
using Xunit.Abstractions;
using Agent.Core.Configuration;
using Agent.Tests.Common;

namespace E2ETests.Tests
{
    [Collection(nameof(CombinedTestCollection))]
    public class BasicScenarios : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly TestSettings _testSettings;

        public BasicScenarios(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _testSettings = _fixture.ConfigFixture.Configuration.GetRequiredSection("TestSettings").Get<TestSettings>();

            Helper.SendMessage(_fixture, _output, "clear state").GetAwaiter().GetResult();
        }

        [Fact]
        public async Task HiDoesntCrash()
        {
            await Helper.SendMessage(_fixture, _output, $"Hi");
        }

        [Fact]
        public async Task CanTrackApp()
        {
            var response = await Helper.SendMessage(_fixture, _output, $"start monitoring {Helper.GetWebAppName(_testSettings.SubscriptionId)}");
            await Task.Delay(TimeSpan.FromSeconds(15));
            Assert.True(Helper.MatchesNaturalLanguagePrompt(_fixture, _output, "monitoring started"));
        }

        [Fact]
        public async Task ClearStateWorks()
        {
            await Helper.SendMessage(_fixture, _output, "how many resources are you tracking");
            await Task.Delay(TimeSpan.FromSeconds(30));

            Assert.True(_fixture.MatchesNaturalLanguagePrompt(_output, "Cleared state"));
        }

        [Fact]
        public async Task NaturalLanguageTestWorksNegative()
        {
            string fakeErrorMessage = "Error: broke";
            bool res = _fixture.MatchesNaturalLanguagePrompt(_output, fakeErrorMessage, "Successfully tracking new subscription");
            Assert.True(!res);
        }

        [Fact]
        public async Task NaturalLanguageTestWorks()
        {
            string fakeErrorMessage = "Subscription added";
            bool res = _fixture.MatchesNaturalLanguagePrompt(_output, fakeErrorMessage, "Successfully tracking new subscription");
            Assert.True(res);
        }

        public void Dispose()
        {
            Helper.DisposeAndRunGenericAssertions(_fixture, _output);
        }
    }
}