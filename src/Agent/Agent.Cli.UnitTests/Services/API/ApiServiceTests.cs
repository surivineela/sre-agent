using Agent.Cli.Services;
using Moq;
using Agent.Cli.Models;
using System.Text.Json;
using Xunit.Abstractions;

namespace Agent.Cli.UnitTests.Services;

public partial class ApiServiceTests : IDisposable
{
    private readonly TestCliConfigurationService _configService;
    private readonly ApiService _apiService;
    private readonly string _testConfigPath;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly ITestOutputHelper _output;

    public ApiServiceTests(ITestOutputHelper output)
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();

        // Setup test configuration
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid():N}.json");
        var testConfig = new CliConfiguration
        {
            ResourceUrl = "https://test.example.com",
            AuthRequired = false
        };

        File.WriteAllText(_testConfigPath, JsonSerializer.Serialize(testConfig));

        // Create configuration service and API service with mocked HTTP
        _configService = new TestCliConfigurationService(_testConfigPath);

        // Create a properly mocked API service
        var httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("https://test.example.com/")
        };
        var tokenService = new Mock<ITokenService>().Object;
        _apiService = new ApiService(httpClient, _configService, tokenService);

        _output = output;
    }

    public void Dispose()
    {
        _apiService?.Dispose();

        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
        GC.SuppressFinalize(this);
    }
}
