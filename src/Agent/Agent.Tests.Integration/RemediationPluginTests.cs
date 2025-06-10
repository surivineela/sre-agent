// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class RemediationPluginTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly TestChatClient ToolCallingChatClient;
        private const string ValidResourceId = "/subscriptions/be8d491e-109c-4ee1-aaee-dc7615af0a42/resourcegroups/test-resources/providers/Microsoft.Web/sites/sreagent-testappservice-1";

        public RemediationPluginTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddSingleton<IRemediationPlugin, RemediationPlugin>();
            services.AddSingleton<RemediationPluginDefinition>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            ServiceProvider s = services.BuildServiceProvider();

            RemediationPluginDefinition remediationPlugin = s.GetRequiredService<RemediationPluginDefinition>();
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(remediationPlugin.ScaleAppServicePlanVertically),
                    AIFunctionFactory.Create(remediationPlugin.CollectMemoryDump),
                    AIFunctionFactory.Create(remediationPlugin.RestartWebApplication),
                    AIFunctionFactory.Create(remediationPlugin.SuggestNextSku),
                    AIFunctionFactory.Create(remediationPlugin.CalculateScalingCost),
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
        public async Task ScaleAppServicePlan_InvalidResourceId()
        {
            await ToolCallingChatClient.CompleteAsync($"Scale up the app service plan for this web app: invalid_resource_id");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("the resource id is not valid"));
        }

        [Fact]
        public async Task CollectMemoryDump_InvalidResourceId()
        {
            await ToolCallingChatClient.CompleteAsync($"Collect a memory dump for this web app: invalid_resource_id");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("the resource id is not valid"));
        }

        [Fact]
        public async Task RestartWebApp_ValidResourceId()
        {
            SetApprovalStatus("restart_webapp", ValidResourceId);
            await ToolCallingChatClient.CompleteAsync($"Restart this web app: {ValidResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("Restarted Web App"));
        }

        [Fact]
        public async Task RestartWebApp_InvalidResourceId()
        {
            await ToolCallingChatClient.CompleteAsync($"Restart this web app: invalid_resource_id");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("the resource id is not valid"));
        }

        [Fact]
        public async Task SuggestNextSku_ValidInput()
        {
            await ToolCallingChatClient.CompleteAsync($"What would be the next SKU if I want to scale up from P1v2 for this app: {ValidResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("SKU Suggestion"));
        }

        [Fact]
        public async Task CalculateScalingCost_ValidInput()
        {
            await ToolCallingChatClient.CompleteAsync($"Calculate the cost difference between Premium1v2 and Premium2v2 for this app: {ValidResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("Cost difference"));
        }

        private void SetApprovalStatus(string operation, string resourceId)
        {
            var operationId = Guid.NewGuid().ToString();
            GlobalStatic.ApprovalStatus[new ApprovalDescriptor(resourceId, operation)] =
                new ApprovalStatus(
                    OperationId: operationId,
                    StartTime: DateTime.UtcNow,
                    Status: ApprovalDecision.Approved,
                    ApprovedTime: DateTime.UtcNow,
                    DecisionMaker: "test-approver",
                    ProcessedTime: DateTime.UtcNow.AddHours(1),
                    description: "Approved for testing");
        }

        public void Dispose()
        {
            GlobalStatic.ApprovalStatus.Clear();

            _output.WriteLine("\nAll chat messages:");
            foreach (var message in ToolCallingChatClient.ChatHistory)
            {
                if (message.Text != null)
                {
                    _output.WriteLine(message.Text);
                }
            }
        }
    }
}
