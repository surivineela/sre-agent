// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Agent.Tests.Unit.Plugins.AzCLI
{
    public class AzCliTests
    {
        private readonly ArmHelper _armHelper;
        private readonly Mock<ILogger<ArmHelper>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IArmClientFactory> _mockArmClientFactory;
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<IHostEnvironment> _mockHostEnvironment;
        private readonly Mock<IChatClient> _mockChatClient;
        private readonly Mock<ICrawlerTriggerService> _mockCrawlerTriggerService;
        private readonly Mock<ISessionPoolService> _mockSessionPoolService;

        public AzCliTests()
        {
            // Create all required mocks
            _mockLogger = new Mock<ILogger<ArmHelper>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockArmClientFactory = new Mock<IArmClientFactory>();
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockHostEnvironment = new Mock<IHostEnvironment>();
            _mockChatClient = new Mock<IChatClient>();
            _mockCrawlerTriggerService = new Mock<ICrawlerTriggerService>();
            _mockSessionPoolService = new Mock<ISessionPoolService>();
            var mockAzureSettings = new AzureSettings();

            // Create ArmHelper instance with mocked dependencies
            _armHelper = new ArmHelper(
                _mockLogger.Object,
                _mockHttpClientFactory.Object,
                _mockArmClientFactory.Object,
                _mockAuthService.Object,
                mockAzureSettings,
                _mockHostEnvironment.Object,
                _mockCrawlerTriggerService.Object,
                _mockSessionPoolService.Object,
                _mockChatClient.Object);
        }

        [Theory]
        [InlineData("az monitor log-analytics query -w /subscriptions/31ce06e2-bf5d-4bdf-9649-243ec548316f/resourcegroups/zhaoziqi-operations-agent-3p-rg/providers/microsoft.operationalinsights/workspaces/zhaoziqi-log-analytics --analytics-query \"Heartbeat | take 10\"", true)]
        [InlineData("az monitor log-analytics query -w xxx --analytics-query 'Heartbeat | take 10'", true)]
        [InlineData("az monitor log-analytics query --analytics-query \"Heartbeat | where Computer == 'server1' | take 10\"", true)]
        [InlineData("az monitor log-analytics query --analytics-query \"Heartbeat | where Computer == 'server1' && TimeGenerated > ago(1h) | project Computer, TimeGenerated | take 10\"", true)]
        [InlineData("az vm list --resource-group mygroup --output table", true)]
        [InlineData("az storage blob list ; rm -rf /", false)]
        [InlineData("az vm list && rm important-file", false)]
        [InlineData("az account show || cat /etc/passwd", false)]
        [InlineData("az group list | grep production", false)]
        [InlineData("az vm show --name test > output.txt", false)]
        [InlineData("az account list < input.txt", false)]
        [InlineData("az vm list `whoami`", false)]
        [InlineData("az account show $(id)", false)]
        [InlineData("az vm list\\nrm -rf /", false)]
        [InlineData("az account show\r\ncat /etc/passwd", false)]
        [InlineData("kubectl get pods", false)]
        [InlineData("not-az command", false)]
        [InlineData("", false)]
        [InlineData("az monitor log-analytics query --analytics-query Heartbeat | take 10", false)] // Unquoted value with pipe
        [InlineData("az monitor log-analytics query --analytics-query \"Heartbeat | take 10", false)] // Unclosed quote
        [InlineData("az monitor log-analytics query --analytics-query 'Heartbeat | take 10\"", false)] // Mismatched quotes
        [InlineData("az storage list --query \"[?name=='test'] | [0]\"", true)]
        [InlineData("az vm list-skus --location eastus --query \"[?name=='Standard_Ds_v6'].{Name:name,Capacity:capabilities[?name=='MaxResourceVolumeMB']|[0].value,Restrictions:restrictions}\" --subscription 31ce06e2-bf5d-4bdf-9649-243ec548316f", true)]
        [InlineData("az monitor log-analytics query --analytics-query \"Heartbeat | take 10\" && echo 'test'", false)] // Dangerous character outside whitelisted flag
        public void ValidateCommand_ShouldReturnExpectedResult(string command, bool shouldBeValid)
        {
            // Arrange & Act
            var result = InvokeValidateCommand(command);

            // Assert
            if (shouldBeValid)
            {
                result.ShouldBeNull($"Command '{command}' should be valid but validation returned: {result}");
            }
            else
            {
                result.ShouldNotBeNull($"Command '{command}' should be invalid but validation passed");
            }
        }

        [Theory]
        [InlineData("az monitor log-analytics query -w /subscriptions/test --analytics-query \"Heartbeat | take 10\"", true)]
        public void IsReadOnlyCommand_WithWhitelistedPatterns_ShouldReturnTrue(string command, bool expectedResult)
        {
            // Act
            var result = ArmHelper.IsReadOnlyCommand(command);

            // Assert
            result.ShouldBe(expectedResult, $"Command '{command}' should be classified as read-only: {expectedResult}");
        }

        [Theory]
        [InlineData("az monitor diagnostic-settings create --name mySettings --resource /subscriptions/test", true)]
        public void IsWriteCommand_WithWhitelistedPatterns_ShouldReturnExpected(string command, bool expectedResult)
        {
            // Act
            var result = ArmHelper.IsWriteCommand(command);

            // Assert
            result.ShouldBe(expectedResult, $"Command '{command}' should be classified as write command: {expectedResult}");
        }

        /// <summary>
        /// Invokes the private ValidateCommand method using reflection
        /// </summary>
        /// <param name="command">The command to validate</param>
        /// <returns>The validation result (null if valid, error message if invalid)</returns>
        private string? InvokeValidateCommand(string command)
        {
            var methodInfo = typeof(ArmHelper).GetMethod("ValidateCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            methodInfo.ShouldNotBeNull("ValidateCommand method should exist");

            var result = methodInfo.Invoke(_armHelper, new object[] { command });
            return result as string;
        }
    }
}
