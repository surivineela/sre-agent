using Agent.Plugins.Implementation;
using Agent.Plugins.Services;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Configuration;
using Agent.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using System.IO;
using System.Threading.Tasks;

namespace Agent.Tests.Unit
{
    public class PostgreSQLPlaybookTests
    {
        private readonly Mock<ILogger<PostgreSQLPlugin>> _mockLogger;
        private readonly Mock<ILogger<PlaybookService>> _mockPlaybookLogger;
        private readonly IMemoryCache _memoryCache;
        private readonly PlaybookService _playbookService;
        private readonly PostgreSQLPlugin _postgreSQLPlugin;

        public PostgreSQLPlaybookTests()
        {
            _mockLogger = new Mock<ILogger<PostgreSQLPlugin>>();
            _mockPlaybookLogger = new Mock<ILogger<PlaybookService>>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            _playbookService = new PlaybookService(_memoryCache, _mockPlaybookLogger.Object);

            // Create mock ArmHelper with all required dependencies
            var mockArmLogger = new Mock<ILogger<ArmHelper>>();
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockArmClientFactory = new Mock<IArmClientFactory>();
            var mockAuthService = new Mock<IAuthenticationService>();
            var mockAzureSettings = new AzureSettings();
            var mockHostEnvironment = new Mock<IHostEnvironment>();
            var mockCrawlerTriggerService = new Mock<ICrawlerTriggerService>();
            var mockChatClient = new Mock<IChatClient>();

            var armHelper = new ArmHelper(
                mockArmLogger.Object,
                mockHttpClientFactory.Object,
                mockArmClientFactory.Object,
                mockAuthService.Object,
                mockAzureSettings,
                mockHostEnvironment.Object,
                mockCrawlerTriggerService.Object,
                mockChatClient.Object);

            // Create mock AzureMonitorMetricsHelper with all required constructor parameters
            var mockAzureMonitorMetricsHelper = new Mock<AzureMonitorMetricsHelper>(
                mockHttpClientFactory.Object, 
                mockArmClientFactory.Object, 
                mockAuthService.Object, 
                mockAzureSettings);

            _postgreSQLPlugin = new PostgreSQLPlugin(_mockLogger.Object, armHelper, _playbookService, mockArmClientFactory.Object, mockAzureMonitorMetricsHelper.Object);
        }

        [Fact]
        public async Task GetPlaybookContent_ReturnsValidContent_WhenPlaybookExists()
        {
            // Arrange
            var category = "PostgreSQL";
            var playbookName = "PostgreSQL_Performance_Investigation";

            // Act
            var result = await _playbookService.GetPlaybookContentAsync(category, playbookName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(playbookName, result.Name);
            Assert.Contains("PostgreSQL Performance Investigation", result.Content);
            Assert.Contains("Quick Diagnosis Steps", result.Content);
        }

        [Fact]
        public async Task GetPlaybookContent_ReturnsNotFound_WhenPlaybookDoesNotExist()
        {
            // Arrange
            var category = "PostgreSQL";
            var playbookName = "NonExistent_Playbook";

            // Act
            var result = await _playbookService.GetPlaybookContentAsync(category, playbookName);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("not found", result.Content);
        }

        [Fact]
        public async Task GetAvailablePlaybooks_ReturnsPlaybooks_ForPostgreSQLCategory()
        {
            // Arrange
            var category = "PostgreSQL";

            // Act
            var result = await _playbookService.GetAvailablePlaybooksAsync(category);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Count > 0);
            Assert.Contains(result, p => p.Name.Contains("Performance"));
        }

        [Fact]
        public async Task PostgreSQLPlugin_ListAvailablePlaybooks_ReturnsPlaybooks()
        {
            // Act
            var result = await _postgreSQLPlugin.ListAvailablePlaybooksAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Count > 0);
            Assert.Contains(result, p => p.Name.Contains("Performance"));
        }

        [Fact]
        public async Task PostgreSQLPlugin_GetPlaybook_ReturnsValidContent()
        {
            // Arrange
            var playbookName = "PostgreSQL_Performance_Investigation";

            // Act
            var result = await _postgreSQLPlugin.GetPlaybookAsync(playbookName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(playbookName, result.Name);
            Assert.Contains("PostgreSQL Performance Investigation", result.Content);
        }
    }
}
