// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Web.Controllers.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Controllers.v1
{
    public class FeatureControllerTests
    {
        private readonly Mock<IOptions<ScheduledTaskSettings>> _mockScheduledTaskSettings;
        private readonly Mock<IOptions<AgentMemorySettings>> _mockAgentMemorySettings;
        private readonly Mock<ILogger<FeatureController>> _mockLogger;
        private readonly FeatureController _controller;

        public FeatureControllerTests()
        {
            _mockScheduledTaskSettings = new Mock<IOptions<ScheduledTaskSettings>>();
            _mockAgentMemorySettings = new Mock<IOptions<AgentMemorySettings>>();
            _mockLogger = new Mock<ILogger<FeatureController>>();

            _controller = new FeatureController(
                _mockScheduledTaskSettings.Object,
                _mockAgentMemorySettings.Object,
                _mockLogger.Object);
        }

        #region GetFeatureStatus Tests

        [Fact]
        public void GetFeatureStatus_AllFeaturesEnabled_ReturnsOkWithEnabledFeatures()
        {
            // Arrange
            var scheduledTaskSettings = new ScheduledTaskSettings { Enabled = true };
            var agentMemorySettings = new AgentMemorySettings { Enabled = true };

            _mockScheduledTaskSettings.Setup(x => x.Value).Returns(scheduledTaskSettings);
            _mockAgentMemorySettings.Setup(x => x.Value).Returns(agentMemorySettings);

            // Act
            var result = _controller.GetFeatureStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<FeatureStatusResponse>(okResult.Value);

            Assert.True(response.Features["scheduledTasks"]);
            Assert.True(response.Features["agentMemory"]);
            Assert.Equal(2, response.Features.Count);
        }

        [Fact]
        public void GetFeatureStatus_AllFeaturesDisabled_ReturnsOkWithDisabledFeatures()
        {
            // Arrange
            var scheduledTaskSettings = new ScheduledTaskSettings { Enabled = false };
            var agentMemorySettings = new AgentMemorySettings { Enabled = false };

            _mockScheduledTaskSettings.Setup(x => x.Value).Returns(scheduledTaskSettings);
            _mockAgentMemorySettings.Setup(x => x.Value).Returns(agentMemorySettings);

            // Act
            var result = _controller.GetFeatureStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<FeatureStatusResponse>(okResult.Value);

            Assert.False(response.Features["scheduledTasks"]);
            Assert.False(response.Features["agentMemory"]);
            Assert.Equal(2, response.Features.Count);
        }

        [Fact]
        public void GetFeatureStatus_MixedFeatureSettings_ReturnsOkWithMixedFeatures()
        {
            // Arrange
            var scheduledTaskSettings = new ScheduledTaskSettings { Enabled = true };
            var agentMemorySettings = new AgentMemorySettings { Enabled = false };

            _mockScheduledTaskSettings.Setup(x => x.Value).Returns(scheduledTaskSettings);
            _mockAgentMemorySettings.Setup(x => x.Value).Returns(agentMemorySettings);

            // Act
            var result = _controller.GetFeatureStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<FeatureStatusResponse>(okResult.Value);

            Assert.True(response.Features["scheduledTasks"]);
            Assert.False(response.Features["agentMemory"]);
            Assert.Equal(2, response.Features.Count);
        }

        [Fact]
        public void GetFeatureStatus_ExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            _mockScheduledTaskSettings.Setup(x => x.Value).Throws(new Exception("Configuration error"));

            // Act
            var result = _controller.GetFeatureStatus();

            // Assert
            var errorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, errorResult.StatusCode);
            Assert.Equal("Internal server error", errorResult.Value);
        }

        #endregion

        #region GetFeatureStatus by Name Tests

        [Theory]
        [InlineData("scheduledtasks", true)]
        [InlineData("scheduledTasks", true)]
        [InlineData("SCHEDULEDTASKS", true)]
        public void GetFeatureStatusByName_ScheduledTasksEnabled_ReturnsOkWithEnabled(string featureName, bool enabled)
        {
            // Arrange
            var scheduledTaskSettings = new ScheduledTaskSettings { Enabled = enabled };
            var agentMemorySettings = new AgentMemorySettings { Enabled = false };

            _mockScheduledTaskSettings.Setup(x => x.Value).Returns(scheduledTaskSettings);
            _mockAgentMemorySettings.Setup(x => x.Value).Returns(agentMemorySettings);

            // Act
            var result = _controller.GetFeatureStatus(featureName);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            var response = okResult.Value;

            var featureProperty = response.GetType().GetProperty("feature");
            var enabledProperty = response.GetType().GetProperty("enabled");

            Assert.NotNull(featureProperty);
            Assert.NotNull(enabledProperty);
            Assert.Equal(featureName, featureProperty.GetValue(response));
            Assert.Equal(enabled, enabledProperty.GetValue(response));
        }

        [Theory]
        [InlineData("agentmemory", true)]
        [InlineData("agentMemory", true)]
        [InlineData("AGENTMEMORY", true)]
        public void GetFeatureStatusByName_AgentMemoryEnabled_ReturnsOkWithEnabled(string featureName, bool enabled)
        {
            // Arrange
            var scheduledTaskSettings = new ScheduledTaskSettings { Enabled = false };
            var agentMemorySettings = new AgentMemorySettings { Enabled = enabled };

            _mockScheduledTaskSettings.Setup(x => x.Value).Returns(scheduledTaskSettings);
            _mockAgentMemorySettings.Setup(x => x.Value).Returns(agentMemorySettings);

            // Act
            var result = _controller.GetFeatureStatus(featureName);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            var response = okResult.Value;

            var featureProperty = response.GetType().GetProperty("feature");
            var enabledProperty = response.GetType().GetProperty("enabled");

            Assert.NotNull(featureProperty);
            Assert.NotNull(enabledProperty);
            Assert.Equal(featureName, featureProperty.GetValue(response));
            Assert.Equal(enabled, enabledProperty.GetValue(response));
        }

        [Fact]
        public void GetFeatureStatusByName_UnknownFeature_ReturnsNotFound()
        {
            // Arrange
            var scheduledTaskSettings = new ScheduledTaskSettings { Enabled = true };
            var agentMemorySettings = new AgentMemorySettings { Enabled = true };

            _mockScheduledTaskSettings.Setup(x => x.Value).Returns(scheduledTaskSettings);
            _mockAgentMemorySettings.Setup(x => x.Value).Returns(agentMemorySettings);

            // Act
            var result = _controller.GetFeatureStatus("unknownfeature");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Feature 'unknownfeature' not found", notFoundResult.Value);
        }

        [Fact]
        public void GetFeatureStatusByName_ExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            _mockScheduledTaskSettings.Setup(x => x.Value).Throws(new Exception("Configuration error"));

            // Act
            var result = _controller.GetFeatureStatus("scheduledtasks");

            // Assert
            var errorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, errorResult.StatusCode);
            Assert.Equal("Internal server error", errorResult.Value);
        }

        #endregion
    }
}