// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Agent.Tests.End2End.Fixtures;
using Xunit;
using Xunit.Abstractions;
using Agent.Core.Configuration;

namespace E2ETests.Tests
{
    [Collection(nameof(CombinedTestCollection))]
    public class BasicScenarios : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly TestSettings? _testSettings;

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
            Assert.NotNull(_testSettings);
            var response = await Helper.SendMessage(_fixture, _output, $"start monitoring {Helper.GetWebAppName(_testSettings.SubscriptionId)}");
            await Task.Delay(TimeSpan.FromSeconds(15));
            Assert.True(await Helper.MatchesNaturalLanguagePrompt(_fixture, _output, "monitoring started"));
        }

        [Fact]
        public async Task ClearStateWorks()
        {
            await Helper.SendMessage(_fixture, _output, "how many resources are you tracking");
            await Task.Delay(TimeSpan.FromSeconds(30));

            Assert.True(await _fixture.MatchesNaturalLanguagePrompt(_output, "Cleared state"));
        }

        [Fact]
        public async Task NaturalLanguageTestWorksNegative()
        {
            string fakeErrorMessage = "Error: broke";
            bool res = await _fixture.MatchesNaturalLanguagePrompt(_output, fakeErrorMessage, "Successfully tracking new subscription");
            Assert.True(!res);
        }

        [Fact]
        public async Task NaturalLanguageTestWorks()
        {
            string fakeErrorMessage = "Subscription added";
            bool res = await _fixture.MatchesNaturalLanguagePrompt(_output, fakeErrorMessage, "Successfully tracking new subscription");
            Assert.True(res);
        }

        public void Dispose()
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Helper.DisposeAndRunGenericAssertions(_fixture, _output);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
    }
}
