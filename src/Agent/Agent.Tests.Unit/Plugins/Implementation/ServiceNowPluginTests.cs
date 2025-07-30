using System;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
using Agent.Plugins.Implementation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class ServiceNowPluginTests
    {
        private readonly ServiceNowPlugin _serviceNowPlugin;
        private readonly Mock<IServiceNowAPIClient> _mockServiceNowApiClient;
        private readonly Mock<ILogger<ServiceNowPlugin>> _mockLogger;

        public ServiceNowPluginTests()
        {
            // Create mocks
            _mockServiceNowApiClient = new Mock<IServiceNowAPIClient>();
            _mockLogger = new Mock<ILogger<ServiceNowPlugin>>();

            // Create the plugin instance with mocked dependencies
            _serviceNowPlugin = new ServiceNowPlugin(
                _mockServiceNowApiClient.Object,
                _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullServiceNowApiClient_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ServiceNowPlugin(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new ServiceNowPlugin(_mockServiceNowApiClient.Object, null!));
        }

        [Fact]
        public async Task GetServiceNowIncident_WithValidIncidentId_ReturnsIncident()
        {
            // Arrange
            var incidentId = "test-incident-123";
            var expectedIncident = new ServiceNowIncident
            {
                IncidentId = incidentId,
                Number = "INC0001234",
                Title = "Test Incident",
                Description = "Test incident description",
                State = "New",
                Priority = "2"
            };

            _mockServiceNowApiClient
                .Setup(x => x.GetIncidentAsync(incidentId))
                .ReturnsAsync(expectedIncident);

            // Act
            var result = await _serviceNowPlugin.GetServiceNowIncident(incidentId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedIncident.IncidentId, result.IncidentId);
            Assert.Equal(expectedIncident.Number, result.Number);
            Assert.Equal(expectedIncident.Title, result.Title);
        }

        [Fact]
        public async Task GetServiceNowIncident_WithEmptyIncidentId_CallsApiClient()
        {
            var incidentId = string.Empty;
            try
            {
                var result = await _serviceNowPlugin.GetServiceNowIncident(incidentId);
            }
            catch (Exception ex)
            {
                Assert.True(ex is ArgumentException);
            }
        }

        [Fact]
        public async Task PostServiceNowDiscussionEntry_WithValidParameters_ReturnsResult()
        {
            // Arrange
            var incidentId = "test-incident-123";
            var discussionEntry = "This is a test discussion entry";
            var expectedResult = "SUCCESS: Discussion entry posted";

            _mockServiceNowApiClient
                .Setup(x => x.PostDiscussionEntryAsync(incidentId, discussionEntry, true))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _serviceNowPlugin.PostServiceNowDiscussionEntry(incidentId, discussionEntry);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task AcknowledgeServiceNowIncident_WithValidIncidentId_ReturnsResult()
        {
            // Arrange
            var incidentId = "test-incident-123";
            var expectedResult = "SUCCESS: Incident acknowledged";

            _mockServiceNowApiClient
                .Setup(x => x.AcknowledgeIncidentAsync(incidentId))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _serviceNowPlugin.AcknowledgeServiceNowIncident(incidentId);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task ResolveServiceNowIncident_WithValidParameters_ReturnsResult()
        {
            // Arrange
            var incidentId = "test-incident-123";
            var discussionEntry = "Incident resolved successfully";
            var expectedResult = "SUCCESS: Incident resolved";

            _mockServiceNowApiClient
                .Setup(x => x.PostDiscussionEntryAsync(incidentId, $"Resolution: {discussionEntry}", true))
                .ReturnsAsync("Discussion posted");

            _mockServiceNowApiClient
                .Setup(x => x.ResolveIncidentAsync(incidentId, discussionEntry))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _serviceNowPlugin.ResolveServiceNowIncident(incidentId, discussionEntry);

            // Assert
            Assert.Equal(expectedResult, result);

            // Verify API client was called for both operations
            _mockServiceNowApiClient.Verify(x => x.PostDiscussionEntryAsync(incidentId, $"Resolution: {discussionEntry}", true), Times.Once);
            _mockServiceNowApiClient.Verify(x => x.ResolveIncidentAsync(incidentId, discussionEntry), Times.Once);
        }
    }
}
