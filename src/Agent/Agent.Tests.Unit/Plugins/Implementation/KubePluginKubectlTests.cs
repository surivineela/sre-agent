// filepath: c:\Users\zhaoziqi\Documents\work\codes\AAPT-Antares-OperationalAgent\src\Agent\Agent.Tests.Unit\Plugins\Implementation\KubePluginKubectlTests.cs
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using Agent.Plugins;
using Microsoft.Extensions.AI;
using Agent.Prometheus.Services;
using Agent.Graph.Crawler.Metrics;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using System;
using System.Reflection;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class KubePluginKubectlTests
    {
        private readonly KubePlugin _kubePlugin;
        private readonly Mock<ILogger<KubePlugin>> _mockLogger;

        public KubePluginKubectlTests()
        {
            // Create all required mocks
            var mockChatClient = new Mock<IChatClient>();
            var mockPrometheusQueryService = new Mock<IPrometheusQueryService>();
            var mockAzureMetricsClient = new Mock<IAzureMetricsClient>();
            var mockKubernetesClientFactory = new Mock<IKubernetesClientFactory>();
            var mockArmClientFactory = new Mock<IArmClientFactory>();
            var mockGraphDatabaseClient = new Mock<IGraphDatabaseClient>();
            var mockThreadRepository = new Mock<IThreadRepository>();
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
                _mockLogger.Object
            );
        }

        [Fact]
        public void ParseKubectlSubcommand_NullCommand_ReturnsNull()
        {
            // Arrange
            string? command = null;

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Null(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ParseKubectlSubcommand_EmptyCommand_ReturnsNull()
        {
            // Arrange
            var command = string.Empty;

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Null(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ParseKubectlSubcommand_NonKubectlCommand_ReturnsNull()
        {
            // Arrange
            var command = "docker container ls";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Null(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ParseKubectlSubcommand_KubectlOnly_ReturnsNull()
        {
            // Arrange
            var command = "kubectl";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ParseKubectlSubcommand_ValidSubcommand_ReturnsSubcommand()
        {
            // Arrange
            var command = "kubectl get pods";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Equal("get", result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ParseKubectlSubcommand_WithOptions_ReturnsSubcommand()
        {
            // Arrange
            var command = "kubectl --namespace=default get pods -o json";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Equal("get", result);
        }

        [Fact]
        public void ParseKubectlSubcommand_AllOptions_ReturnsNull()
        {
            // Arrange
            var command = "kubectl --namespace=default -o json";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Null(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ParseKubectlSubcommand_ComplexCommand_ReturnsSubcommand()
        {
            // Arrange
            var command = "kubectl --context=my-cluster -n default get pods --selector=app=nginx -o wide";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Equal("get", result);
        }

        [Fact]
        public void ParseKubectlSubcommand_MixedCaseCommand_ReturnsLowercaseSubcommand()
        {
            // Arrange
            var command = "KuBeCtl GET pods";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Equal("get", result);
        }

        [Fact]
        public void ParseKubectlSubcommand_WithFlagValue_ReturnsCorrectSubcommand()
        {
            // Arrange
            var command = "kubectl -n test get pods";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Equal("get", result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ParseKubectlSubcommand_WithLongFlagValue_ReturnsCorrectSubcommand()
        {
            // Arrange
            var command = "kubectl --namespace test get pods";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Equal("get", result);
        }

        [Fact]
        public void ParseKubectlSubcommand_WithMultipleFlagValues_ReturnsCorrectSubcommand()
        {
            // Arrange
            var command = "kubectl -n test --context my-cluster describe pods";

            // Act
            var result = InvokeParseKubectlSubcommand(command);

            // Assert
            Assert.Equal("describe", result);
        }

        /// <summary>
        /// Invoke the private ParseKubectlSubcommand method using reflection
        /// </summary>
        private string? InvokeParseKubectlSubcommand(string? command)
        {
            var methodInfo = typeof(KubePlugin).GetMethod("ParseKubectlSubcommand",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (methodInfo == null)
                throw new Exception("Failed to get ParseKubectlSubcommand method");

            return methodInfo.Invoke(_kubePlugin, new object?[] { command }) as string;
        }
    }
}
