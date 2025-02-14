using Agent.Core;
using Agent.Core.Models;
using Agent.Plugins;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Agent.Tests.Integration.Helpers;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OpenAI.Chat;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class GithubIssuePluginTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly Session Session;
        private readonly TestChatClient ToolCallingChatClient;

        public GithubIssuePluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddScoped<GitHubClient>();
            services.AddSingleton<IGithubIssuePlugin, GitHubIssuePlugin>();
            services.AddSingleton<GitHubIssuePluginDefinition>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            ServiceProvider s = services.BuildServiceProvider();

            GitHubIssuePluginDefinition gitHubIssuePlugin = s.GetRequiredService<GitHubIssuePluginDefinition>();
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(gitHubIssuePlugin.CreateGithubIssue),
                    AIFunctionFactory.Create(gitHubIssuePlugin.UpdateGithubIssue),
                    AIFunctionFactory.Create(gitHubIssuePlugin.FetchGithubIssues),
                    AIFunctionFactory.Create(gitHubIssuePlugin.CreateGithubIssueComment),
                    AIFunctionFactory.Create(gitHubIssuePlugin.UpdateGithubIssueComment),
                    AIFunctionFactory.Create(gitHubIssuePlugin.FetchGithubIssueComments),
                    AIFunctionFactory.Create(gitHubIssuePlugin.DeleteGithubIssueComment),
                ]
            };

            ToolCallingChatClient = new TestChatClient(
                chatClient
                    .AsBuilder()
                    .UseFunctionInvocation()
                    .Build(),
                chatOptions,
                _output
            );
        }

        [Fact]
        public async Task CreateIssue()
        {
            await ToolCallingChatClient.CompleteAsync($"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("issue created"));
        }

        [Fact]
        public async Task CreateIssueWithTagTitleAndBody()
        {
            await ToolCallingChatClient.CompleteAsync($"create a sample github issue with title 'new fake issue', body 'fake issue body', tags [E2ETests, Fake]:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("issue created"));
        }

        [Fact]
        public async Task UpdateIssueTags()
        {
            await ToolCallingChatClient.CompleteAsync($"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await ToolCallingChatClient.CompleteAsync($"add the [Update] tag");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("issue created and updated"));
        }

        [Fact]
        public async Task UpdateIssueTitle()
        {
            await ToolCallingChatClient.CompleteAsync($"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await ToolCallingChatClient.CompleteAsync($"change the title to 'changed title'");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("issue created and then title changed afterwards"));
        }


        [Fact]
        public async Task ListIssue()
        {
            await ToolCallingChatClient.CompleteAsync($"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await ToolCallingChatClient.CompleteAsync($"list the issues with the [E2ETests] tag");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("issue(s) listed"));
        }

        [Fact]
        public async Task CreateIssueComment()
        {
            await ToolCallingChatClient.CompleteAsync($"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await ToolCallingChatClient.CompleteAsync($"comment on the issue with 'this is a test comment'");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("issue comment created"));
        }

        [Fact]
        public async Task DeleteIssueComment()
        {
            await ToolCallingChatClient.CompleteAsync($"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await ToolCallingChatClient.CompleteAsync($"comment on the issue with 'this is a test comment'");
            await ToolCallingChatClient.CompleteAsync($"delete the comment");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("issue comment deleted"));
        }

        [Fact]
        public async Task UpdateIssueComment()
        {
            await ToolCallingChatClient.CompleteAsync($"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await ToolCallingChatClient.CompleteAsync($"comment on the issue with 'this is a test comment'");
            await ToolCallingChatClient.CompleteAsync($"modify comment body to be 'this is a updated comment'");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("issue comment updated"));
        }

        [Fact]
        public async Task ListIssueComments()
        {
            await ToolCallingChatClient.CompleteAsync($"create a sample github issue (content doesn't matter) here with the [E2ETests] tag:\r\n\r\nhttps://github.com/sanchitmehta/sample-app");
            await ToolCallingChatClient.CompleteAsync($"create 3 separate comments on the issue with 'this is a test comment'");
            await ToolCallingChatClient.CompleteAsync($"fetch all comments on that issue");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("3 issue comments returned"));
        }

        private async Task _Dispose()
        {
            await ToolCallingChatClient.CompleteAsync($"close all issues with the [E2ETests] tag: https://github.com/sanchitmehta/sample-app");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("no exceptions or errors occurred"));

            _output.WriteLine("\nAll chat messages:");
            foreach (var message in ToolCallingChatClient.ChatHistory)
            {
                if (message.Text != null)
                {
                    _output.WriteLine(message.Text);
                }
            }
        }

        public void Dispose()
        {
            _Dispose().GetAwaiter().GetResult();
        }
    }
}