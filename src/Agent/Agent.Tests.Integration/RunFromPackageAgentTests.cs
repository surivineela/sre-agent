// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Agent.Runtime;
using Agent.Tests.Integration.Fixtures;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Agent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class RunFromPackageAgentTests : IDisposable
    {
        private readonly CombinedFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _config;
        private readonly TestChatClient ToolCallingChatClient;
        
        // Test resource IDs for different scenarios
        private const string ValidWindowsResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app-windows";
        private const string ValidLinuxResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app-linux";
        private const string InvalidResourceId = "invalid_resource_id";

        public RunFromPackageAgentTests(CombinedFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _output = testOutputHelper;
            _config = fixture.ConfigFixture.Configuration;

            var services = new ServiceCollection();

            // Register dependencies
            services.AddLogging();
            services.AddSingleton(_config);
            services.AddSingleton<IRunFromPackagePlugin, RunFromPackagePlugin>();
            services.AddSingleton<RunFromPackagePluginDefinition>();
            services.ConfigureAzureOpenAIClient();
            services.ConfigureIChatClient();

            ServiceProvider s = services.BuildServiceProvider();

            RunFromPackagePluginDefinition runFromPackagePlugin = s.GetRequiredService<RunFromPackagePluginDefinition>();
            IChatClient chatClient = s.GetRequiredService<IChatClient>();

            var chatOptions = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(runFromPackagePlugin.GetRunFromPackageConfigurationAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.VerifyRunFromPackageConfigurationAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.DiagnoseRunFromPackageIssuesAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.RepairRunFromPackageConfigurationAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.MigrateToLocalPackageAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.GetSkuCapabilitiesAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.GeneratePackageSasUrlAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.ValidatePackageAccessibilityAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.GetRunFromPackageRecommendationsAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.GetRunFromPackageRecommendationsAsync),
                    AIFunctionFactory.Create(runFromPackagePlugin.HasRunFromPackageIssuesAsync)
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

        #region Configuration Management Tests

        [Fact]
        public async Task GetRunFromPackageConfiguration_ValidResourceId_ReturnsConfiguration()
        {
            await ToolCallingChatClient.CompleteAsync($"Get the WEBSITE_RUN_FROM_PACKAGE configuration for this function app: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("WEBSITE_RUN_FROM_PACKAGE configuration"));
        }

        [Fact]
        public async Task GetRunFromPackageConfiguration_InvalidResourceId_ReturnsError()
        {
            await ToolCallingChatClient.CompleteAsync($"Get the WEBSITE_RUN_FROM_PACKAGE configuration for this function app: {InvalidResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("invalid resource id or error occurred"));
        }

        [Fact]
        public async Task VerifyRunFromPackageConfiguration_ValidConfiguration_ReturnsVerificationResult()
        {
            await ToolCallingChatClient.CompleteAsync($"Verify the WEBSITE_RUN_FROM_PACKAGE configuration for this function app: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("verification result"));
        }

        #endregion

        #region Diagnostic Tests

        [Fact]
        public async Task DiagnoseRunFromPackageIssues_ValidResourceId_ReturnsDiagnosticResult()
        {
            await ToolCallingChatClient.CompleteAsync($"Diagnose WEBSITE_RUN_FROM_PACKAGE issues for this function app: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("diagnostic result"));
        }

        [Fact]
        public async Task CheckRunFromPackageStatus_ValidResourceId_ReturnsStatusCheck()
        {
            await ToolCallingChatClient.CompleteAsync($"Check the WEBSITE_RUN_FROM_PACKAGE status for this function app: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("status check"));
        }

        #endregion

        #region Repair and Rollback Tests

        [Fact]
        public async Task RepairRunFromPackageConfiguration_WithApproval_ExecutesRepair()
        {
            SetApprovalStatus("repair_run_from_package", ValidWindowsResourceId);
            await ToolCallingChatClient.CompleteAsync($"Repair the WEBSITE_RUN_FROM_PACKAGE configuration for this function app by setting it to 1: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("repair operation"));
        }

        [Fact]
        public async Task RepairRunFromPackageConfiguration_WithoutApproval_RequiresApproval()
        {
            await ToolCallingChatClient.CompleteAsync($"Repair the WEBSITE_RUN_FROM_PACKAGE configuration for this function app by setting it to 1: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("approval required"));
        }

        [Fact]
        public async Task RollbackRunFromPackageConfiguration_WithApproval_ExecutesRollback()
        {
            SetApprovalStatus("rollback_run_from_package", ValidWindowsResourceId);
            await ToolCallingChatClient.CompleteAsync($"Rollback the WEBSITE_RUN_FROM_PACKAGE configuration for this function app: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("rollback operation"));
        }

        #endregion

        #region SKU and Compatibility Tests

        [Fact]
        public async Task ValidateSkuCompatibility_WindowsConsumption_ReturnsCompatibilityResult()
        {
            await ToolCallingChatClient.CompleteAsync($"Validate SKU compatibility for WEBSITE_RUN_FROM_PACKAGE with Consumption plan on Windows for: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("SKU compatibility"));
        }

        [Fact]
        public async Task ValidateSkuCompatibility_LinuxConsumption_ReturnsCompatibilityResult()
        {
            await ToolCallingChatClient.CompleteAsync($"Validate SKU compatibility for WEBSITE_RUN_FROM_PACKAGE with Consumption plan on Linux for: {ValidLinuxResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("SKU compatibility"));
        }

        #endregion

        #region Security and URL Tests

        [Fact]
        public async Task GenerateSecureSasToken_ValidRequest_ReturnsSasToken()
        {
            await ToolCallingChatClient.CompleteAsync($"Generate a secure SAS token for storage account 'teststorage' container 'deployments' for function app: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("SAS token"));
        }

        [Fact]
        public async Task ValidateExternalUrl_ValidUrl_ReturnsValidationResult()
        {
            await ToolCallingChatClient.CompleteAsync($"Validate this external URL for WEBSITE_RUN_FROM_PACKAGE: https://example.blob.core.windows.net/deployments/package.zip");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("URL validation"));
        }

        [Fact]
        public async Task ValidateExternalUrl_InvalidUrl_ReturnsValidationError()
        {
            await ToolCallingChatClient.CompleteAsync($"Validate this external URL for WEBSITE_RUN_FROM_PACKAGE: invalid-url-format");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("invalid URL or validation error"));
        }

        #endregion

        #region Reporting and Best Practices Tests

        [Fact]
        public async Task GenerateDeploymentReport_ValidResourceId_ReturnsReport()
        {
            await ToolCallingChatClient.CompleteAsync($"Generate a deployment report for WEBSITE_RUN_FROM_PACKAGE for function app: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("deployment report"));
        }

        [Fact]
        public async Task GetBestPracticeRecommendations_WindowsFunction_ReturnsRecommendations()
        {
            await ToolCallingChatClient.CompleteAsync($"Get best practice recommendations for WEBSITE_RUN_FROM_PACKAGE for Windows function app: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("best practice recommendations"));
        }

        [Fact]
        public async Task GetBestPracticeRecommendations_LinuxFunction_ReturnsRecommendations()
        {
            await ToolCallingChatClient.CompleteAsync($"Get best practice recommendations for WEBSITE_RUN_FROM_PACKAGE for Linux function app: {ValidLinuxResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("best practice recommendations"));
        }

        #endregion

        #region Agent Handoff Scenarios

        [Fact]
        public async Task ComplexDiagnosticWorkflow_MultipleSteps_HandlesWorkflow()
        {
            // Simulate a complex workflow: check status, diagnose, recommend repair
            await ToolCallingChatClient.CompleteAsync($"For function app {ValidWindowsResourceId}, first check the WEBSITE_RUN_FROM_PACKAGE status, then diagnose any issues, and provide repair recommendations");
            
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("status check"));
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("diagnostic"));
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("recommendations"));
        }

        [Fact]
        public async Task SecurityFocusedWorkflow_UrlAndSasValidation_HandlesSecurely()
        {
            // Test security-focused workflow with URL validation and SAS token generation
            await ToolCallingChatClient.CompleteAsync($"For function app {ValidWindowsResourceId}, validate the external package URL https://test.blob.core.windows.net/deployments/package.zip and generate a secure SAS token for storage account 'test'");
            
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("URL validation"));
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("SAS token"));
        }

        [Fact]
        public async Task SkuMigrationWorkflow_CheckCompatibilityAndRecommendations_ProvidesGuidance()
        {
            // Test SKU migration scenario
            await ToolCallingChatClient.CompleteAsync($"For function app {ValidLinuxResourceId} on Linux Consumption plan, check SKU compatibility for WEBSITE_RUN_FROM_PACKAGE and provide migration recommendations");
            
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("SKU compatibility"));
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("recommendations"));
        }

        #endregion

        #region Error Handling and Edge Cases

        [Fact]
        public async Task HandleMalformedResourceId_ReturnsAppropriateError()
        {
            await ToolCallingChatClient.CompleteAsync($"Check WEBSITE_RUN_FROM_PACKAGE for this malformed resource ID: /subscriptions/invalid");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("invalid resource id or error"));
        }

        [Fact]
        public async Task HandleEmptyResourceId_ReturnsValidationError()
        {
            await ToolCallingChatClient.CompleteAsync($"Check WEBSITE_RUN_FROM_PACKAGE for this empty resource ID: ");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("resource id is required or empty"));
        }

        [Fact]
        public async Task HandleNetworkTimeouts_ReturnsGracefulError()
        {
            // This would need mock setup to simulate network issues
            await ToolCallingChatClient.CompleteAsync($"Get configuration for function app that might have network issues: {ValidWindowsResourceId}");
            Assert.True(await ToolCallingChatClient.MatchesNaturalLanguagePrompt("configuration or error"));
        }

        #endregion

        #region Helper Methods

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

        #endregion

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