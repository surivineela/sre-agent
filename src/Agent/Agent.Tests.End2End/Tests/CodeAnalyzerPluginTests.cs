// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Tests.End2End.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace E2ETests.Tests
{
    [Collection(nameof(CombinedTestCollection))]
    public class CodeAnalyzerPluginTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;

        public CodeAnalyzerPluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;

            Helper.SendMessage(_fixture, _output, "clear state").GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CreatePR()
        {
            await Helper.SendMessageAndWait(
                _fixture,
                _output,
                $@"my app uses managed identity. don't modify app settings; make a code change to fix managed identity. i know MI is disabled on the app but you must fix the source

you have a pat to the repo already

use the main branch

don't ask again

/subscriptions/c0ee2c19-bc01-4984-9f35-6b784acfda69/resourceGroups/other-rg/providers/Microsoft.Web/sites/oa-demo-web-mnilsen

https://github.com/sanchitmehta/sample-app",
                30
            );


            if (await Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "needs additional confirmation"))
            {
                await Helper.SendMessage(_fixture, _output, "don't modify app settings; make a code change to fix managed identity. you have access. just fix the code.");
            }

            await Task.Delay(TimeSpan.FromSeconds(60));

            Assert.True(await Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "work item created, "));
        }

        private async Task _Dispose()
        {
            await Helper.DisposeIssues(_fixture, _output);
        }

        public void Dispose()
        {
            _Dispose().GetAwaiter().GetResult();
            Helper.DisposeAndRunGenericAssertions(_fixture, _output);
        }
    }
}
