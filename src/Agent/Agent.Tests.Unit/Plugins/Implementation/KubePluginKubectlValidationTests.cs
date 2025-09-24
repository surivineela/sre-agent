// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Graph.Crawler.Metrics;
using Agent.Graph.Services;
using Agent.Plugins;
using Agent.Prometheus.Services;
using Agent.Plugins.Interface;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class KubePluginKubectlValidationTests
    {
        private readonly KubePlugin _kubePlugin;
        private readonly Mock<ILogger<KubePlugin>> _mockLogger;

        public KubePluginKubectlValidationTests()
        {
            // Create all required mocks
            var mockChatClient = new Mock<IChatClient>();
            var mockPrometheusQueryService = new Mock<IPrometheusQueryService>();
            var mockAzureMetricsClient = new Mock<IAzureMetricsClient>();
            var mockKubernetesClientFactory = new Mock<IKubernetesClientFactory>();
            var mockArmClientFactory = new Mock<IArmClientFactory>();
            var mockGraphDatabaseClient = new Mock<IGraphDatabaseClient>();
            var mockThreadRepository = new Mock<IThreadRepository>();
            var mockAuthService = new Mock<IAuthenticationService>();
            var mockHostEnv = new Mock<IHostEnvironment>();
            var mockCrawlerTriggerService = new Mock<ICrawlerTriggerService>();
            var mockAgentCommunicationService = new Mock<IAgentOutboundCommunicationService>();
            var mockActionSettings = new Mock<ActionSettings>();
            var mockAgentRuntimeModifier = new Mock<IAgentRuntimeModifier<AgentContext>>();
            var mockJavaPlugin = new Mock<IKubeJavaPlugin>();
            var mockPrometheusEndpointService = new Mock<IPrometheusEndpointService>();
            _mockLogger = new Mock<ILogger<KubePlugin>>();

            // Create an actual instance with our mocked dependencies
            _kubePlugin = new KubePlugin(
                mockChatClient.Object,
                mockPrometheusQueryService.Object,
                mockAzureMetricsClient.Object,
                mockKubernetesClientFactory.Object,
                mockArmClientFactory.Object,
                mockGraphDatabaseClient.Object,
                mockThreadRepository.Object,
                mockAgentCommunicationService.Object,
                mockAuthService.Object,
                mockHostEnv.Object,
                _mockLogger.Object,
                mockCrawlerTriggerService.Object,
                mockActionSettings.Object,
                mockAgentRuntimeModifier.Object,
                mockJavaPlugin.Object,
                mockPrometheusEndpointService.Object
            );
        }

        #region Output Format Validation Tests

        [Fact]
        public void ValidateKubectlReadCommand_GetWithYamlOutput_ShouldPass()
        {
            // Arrange
            var command = "kubectl get deployment product-catalog -n default -o yaml";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithJsonOutput_ShouldPass()
        {
            // Arrange
            var command = "kubectl get pods -o json";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithNameOutput_ShouldPass()
        {
            // Arrange
            var command = "kubectl get pods -o name";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithWideOutput_ShouldPass()
        {
            // Arrange
            var command = "kubectl get pods -o wide";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithCustomColumnsOutput_ShouldPass()
        {
            // Arrange
            var command = "kubectl get pods -o custom-columns=NAME:.metadata.name,STATUS:.status.phase";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithUnsupportedOutput_ShouldFail()
        {
            // Arrange
            var command = "kubectl get pods -o table";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Unsupported output value 'table'", result);
            Assert.Contains("Allowed: name, wide, yaml, json, jsonpath, custom-columns", result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithoutOutputFormat_ShouldFail()
        {
            // Arrange
            var command = "kubectl get pods";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Command must include the '-o' or '--output' option", result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithMultipleOutputFormats_ShouldFail()
        {
            // Arrange
            var command = "kubectl get pods -o yaml --output json";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Command must contain only one output option", result);
        }

        #endregion

        #region Rollout Command Validation Tests

        [Fact]
        public void ValidateKubectlReadCommand_RolloutHistory_ShouldPass()
        {
            // Arrange
            var command = "kubectl rollout history deployment/product-catalog -n default";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_RolloutStatus_ShouldPass()
        {
            // Arrange
            var command = "kubectl rollout status deployment/my-app";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_RolloutRestart_ShouldFailInReadCommand()
        {
            // Arrange
            var command = "kubectl rollout restart deployment/my-app";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("'restart' is a write operation", result);
            Assert.Contains("Use RunKubectlWriteCommandAsync instead", result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_RolloutUndo_ShouldFailInReadCommand()
        {
            // Arrange
            var command = "kubectl rollout undo deployment/my-app";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("'undo' is a write operation", result);
            Assert.Contains("Use RunKubectlWriteCommandAsync instead", result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_RolloutPause_ShouldFailInReadCommand()
        {
            // Arrange
            var command = "kubectl rollout pause deployment/my-app";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("'pause' is a write operation", result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_RolloutResume_ShouldFailInReadCommand()
        {
            // Arrange
            var command = "kubectl rollout resume deployment/my-app";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("'resume' is a write operation", result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_RolloutWithoutAction_ShouldFail()
        {
            // Arrange
            var command = "kubectl rollout";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Rollout command missing action", result);
            Assert.Contains("Supported read actions: status, history", result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_RolloutWithInvalidAction_ShouldFail()
        {
            // Arrange - this is what happens when someone puts resource name where action should be
            var command = "kubectl rollout deployment/my-app";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("'deployment' is a write operation", result);
            Assert.Contains("Use RunKubectlWriteCommandAsync instead", result);
        }

        [Fact]
        public void ValidateKubectlWriteCommand_RolloutRestart_ShouldPass()
        {
            // Arrange
            var command = "kubectl rollout restart deployment/my-app";

            // Act
            var result = InvokeValidateKubectlWriteCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlWriteCommand_RolloutUndo_ShouldPass()
        {
            // Arrange
            var command = "kubectl rollout undo deployment/my-app";

            // Act
            var result = InvokeValidateKubectlWriteCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlWriteCommand_RolloutPause_ShouldPass()
        {
            // Arrange
            var command = "kubectl rollout pause deployment/my-app";

            // Act
            var result = InvokeValidateKubectlWriteCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlWriteCommand_RolloutResume_ShouldPass()
        {
            // Arrange
            var command = "kubectl rollout resume deployment/my-app";

            // Act
            var result = InvokeValidateKubectlWriteCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlWriteCommand_RolloutHistory_ShouldFailInWriteCommand()
        {
            // Arrange
            var command = "kubectl rollout history deployment/my-app";

            // Act
            var result = InvokeValidateKubectlWriteCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("'rollout history' is a read-only command", result);
            Assert.Contains("Use RunKubectlReadCommandAsync instead", result);
        }

        [Fact]
        public void ValidateKubectlWriteCommand_RolloutStatus_ShouldFailInWriteCommand()
        {
            // Arrange
            var command = "kubectl rollout status deployment/my-app";

            // Act
            var result = InvokeValidateKubectlWriteCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("'rollout status' is a read-only command", result);
            Assert.Contains("Use RunKubectlReadCommandAsync instead", result);
        }

        [Fact]
        public void ValidateKubectlWriteCommand_RolloutInvalidAction_ShouldFail()
        {
            // Arrange
            var command = "kubectl rollout invalidaction deployment/my-app";

            // Act
            var result = InvokeValidateKubectlWriteCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Unsupported write rollout action", result);
            Assert.Contains("Supported write actions: restart, undo, pause, resume", result);
        }

        #endregion

        #region Mixed Case and Edge Cases

        [Fact]
        public void ValidateKubectlReadCommand_RolloutHistoryMixedCase_ShouldPass()
        {
            // Arrange
            var command = "KUBECTL ROLLOUT HISTORY deployment/my-app";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetYamlMixedCase_ShouldPass()
        {
            // Arrange
            var command = "kubectl get pods -o YAML";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithJsonMixedCase_ShouldPass()
        {
            // Arrange
            var command = "kubectl get pods -o JSON";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithJsonPath_ShouldPass()
        {
            // Arrange
            var command = "kubectl get pods -o jsonpath='{.items[*].metadata.name}'";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_GetWithOutputFlag_ShouldPass()
        {
            // Arrange
            var command = "kubectl get pods --output yaml";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_ExplainCommand_ShouldPass()
        {
            // Arrange
            var command = "kubectl explain pods";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_VersionCommand_ShouldPass()
        {
            // Arrange
            var command = "kubectl version --client";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_ClusterInfoCommand_ShouldPass()
        {
            // Arrange
            var command = "kubectl cluster-info";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_ConfigViewCommand_ShouldPass()
        {
            // Arrange
            var command = "kubectl config view";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_ConfigGetContextsCommand_ShouldPass()
        {
            // Arrange
            var command = "kubectl config get-contexts";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_ConfigSetContextCommand_ShouldFail()
        {
            // Arrange
            var command = "kubectl config set-context my-context";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("'config' subcommand only supports read operations", result);
        }

        [Fact]
        public void ValidateKubectlReadCommand_IllegalPipeCommand_ShouldFail()
        {
            // Arrange
            var command = "kubectl get pods | grep running";

            // Act
            var result = InvokeValidateKubectlReadCommand(command);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Command contains potentially dangerous character(s): |", result);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Invoke the private ValidateKubectlReadCommand method using reflection
        /// </summary>
        private string? InvokeValidateKubectlReadCommand(string command)
        {
            var methodInfo = typeof(KubePlugin).GetMethod("ValidateKubectlReadCommand",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (methodInfo == null)
                throw new Exception("Failed to get ValidateKubectlReadCommand method");

            return methodInfo.Invoke(_kubePlugin, new object[] { command }) as string;
        }

        /// <summary>
        /// Invoke the private ValidateKubectlWriteCommand method using reflection
        /// </summary>
        private string? InvokeValidateKubectlWriteCommand(string command)
        {
            var methodInfo = typeof(KubePlugin).GetMethod("ValidateKubectlWriteCommand",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (methodInfo == null)
                throw new Exception("Failed to get ValidateKubectlWriteCommand method");

            return methodInfo.Invoke(_kubePlugin, new object[] { command }) as string;
        }

        #endregion
    }
}
