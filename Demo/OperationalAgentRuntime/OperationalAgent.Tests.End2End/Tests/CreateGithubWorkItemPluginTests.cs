using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace E2ETests.Tests
{
    [Collection(nameof(AzureFunctionsTestsCollection))]
    public class GithubIssuePluginTests : IDisposable
    {
        private readonly TestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;

        public GithubIssuePluginTests(TestFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = Helper.GetConfig();

            Helper.SendMessage(_fixture, _output, "clear state").GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CreateIssue()
        {
            await Helper.SendMessageAndWait(_fixture, _output, $"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            Assert.True(Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "issue created"));
        }

        [Fact]
        public async Task CreateIssueWithTagTitleAndBody()
        {
            await Helper.SendMessageAndWait(_fixture, _output, $"create a sample github issue with title 'new fake issue', body 'fake issue body', tags [E2ETests, Fake]:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            Assert.True(Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "issue created"));
        }

        [Fact]
        public async Task UpdateIssueTags()
        {
            await Helper.SendMessageAndWait(_fixture, _output, $"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await Helper.SendMessageAndWait(_fixture, _output, $"add the [Update] tag");
            Assert.True(Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "issue created and updated"));
        }

        [Fact]
        public async Task UpdateIssueTitle()
        {
            await Helper.SendMessageAndWait(_fixture, _output, $"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await Helper.SendMessageAndWait(_fixture, _output, $"change the title to 'changed title'");
            Assert.True(Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "issue created and then title changed afterwards"));
        }


        [Fact]
        public async Task ListIssue()
        {
            await Helper.SendMessageAndWait(_fixture, _output, $"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await Helper.SendMessageAndWait(_fixture, _output, $"list the issues with the [E2ETests] tag");
            Assert.True(Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "issue(s) listed"));
        }

        [Fact]
        public async Task CreateIssueComment()
        {
            await Helper.SendMessageAndWait(_fixture, _output, $"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await Helper.SendMessageAndWait(_fixture, _output, $"comment on the issue with 'this is a test comment'");
            Assert.True(Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "issue comment created"));
        }

        [Fact]
        public async Task DeleteIssueComment()
        {
            await Helper.SendMessageAndWait(_fixture, _output, $"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await Helper.SendMessageAndWait(_fixture, _output, $"comment on the issue with 'this is a test comment'");
            await Helper.SendMessageAndWait(_fixture, _output, $"delete the comment");
            Assert.True(Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "issue comment deleted"));
        }

        [Fact]
        public async Task UpdateIssueComment()
        {
            await Helper.SendMessageAndWait(_fixture, _output, $"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await Helper.SendMessageAndWait(_fixture, _output, $"comment on the issue with 'this is a test comment'");
            await Helper.SendMessageAndWait(_fixture, _output, $"modify comment body to be 'this is a updated comment'");
            Assert.True(Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "issue comment updated"));
        }

        [Fact]
        public async Task ListIssueComments()
        {
            await Helper.SendMessageAndWait(_fixture, _output, $"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await Helper.SendMessageAndWait(_fixture, _output, $"create 3 separate comments on the issue with 'this is a test comment'");
            await Helper.SendMessageAndWait(_fixture, _output, $"fetch all comments on that issue");
            Assert.True(Helper.MatchesNaturalLanguagePromptAndClear(_fixture, _output, "3 issue comments returned"));
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