using Agent.Core.Services;
using Agent.Plugins.Definitions;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Plugins.Tests.Definitions
{
    public class RCAContainerAppIcMPluginDefinitionTests
    {
        private readonly IContainerAppIcMPlugin _mockPlugin;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly RCAContainerAppIcMPluginDefinition _pluginDefinition;

        public RCAContainerAppIcMPluginDefinitionTests()
        {
            _mockPlugin = new ContainerAppIcMPlugin(
                new Mock<IConfiguration>().Object,
                new Mock<IICMAPIClient>().Object,
                new Mock<IChatClient>().Object,
                new Mock<ITimePlugin>().Object,

                new Mock<ILogger<ContainerAppIcMPlugin>>().Object
            );
            _mockEnv = new Mock<IWebHostEnvironment>();

            _pluginDefinition = new RCAContainerAppIcMPluginDefinition(
                _mockPlugin,
                _mockEnv.Object);
        }

        [Fact]
        public void GetIssueInvestigationTimeRangeRCAContainerApp_WithAllValidDates_CallsPluginWithParsedDates()
        {
            // Arrange
            var firstOccurrence = "2025-04-01T04:00:00Z";
            var lastOccurrence = "2025-05-27T19:30:00Z";
            var reportedTime = "";

            var expectedResult = new InvestigationTimeRangeResult() { StartDate = new DateTime(2025, 4, 27, 18, 30, 0), EndDate = new DateTime(2025, 5, 27, 20, 30, 0) };

            // Act
            var result = _pluginDefinition.GetIssueInvestigationTimeRangeRCAContainerApp(
                firstOccurrence, lastOccurrence, reportedTime);

            // Assert
            Assert.Equivalent(expectedResult, result);
        }

        [Fact]
        public void GetIssueInvestigationTimeRangeRCAContainerApp_WithDifferentTimeZones_CallsPluginCorrectly()
        {
            // Arrange
            var firstOccurrence = "2024-09-12T10:00:00+05:00"; // UTC+5
            var lastOccurrence = "2024-09-12T20:00:00-03:00";  // UTC-3

            var expectedResult = new InvestigationTimeRangeResult() { StartDate = new DateTime(2024, 9, 12, 4, 0, 0), EndDate = new DateTime(2024, 9, 13, 0, 0, 0) }; // Converted to UTC

            // Act
            var result = _pluginDefinition.GetIssueInvestigationTimeRangeRCAContainerApp(
                firstOccurrence, lastOccurrence, null);

            // Assert
            Assert.Equivalent(expectedResult, result);
        }

        [Fact]
        public void GetIssueInvestigationTimeRangeRCAContainerApp_WithSameDateTime_CallsPluginCorrectly()
        {
            // Arrange
            var sameDateTime = "2024-11-25T15:30:00Z";
            var expectedResult = new InvestigationTimeRangeResult() { StartDate = new DateTime(2024, 11, 25, 14, 30, 0), EndDate = new DateTime(2024, 11, 25, 16, 30, 0) };

            // Act
            var result = _pluginDefinition.GetIssueInvestigationTimeRangeRCAContainerApp(
                sameDateTime, sameDateTime, sameDateTime);

            // Assert
            Assert.Equivalent(expectedResult, result);
        }
    }
}
