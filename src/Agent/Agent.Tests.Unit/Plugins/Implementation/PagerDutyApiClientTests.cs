using System.Net;
using Agent.Core.Configuration;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Agent.Tests.Unit.Plugins.Implementation;
public class PagerDutyApiClientTests
{
    private readonly Mock<ILogger<PagerDutyService>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<IOptionsMonitor<IncidentManagementSettings>> _mockIncidentManagementSettings;
    private readonly IPagerDutyService _pagerDutyService;

    public PagerDutyApiClientTests()
    {
        _mockLogger = new Mock<ILogger<PagerDutyService>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockIncidentManagementSettings = new Mock<IOptionsMonitor<IncidentManagementSettings>>();

        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() =>
        {
            var client = new HttpClient(_mockHttpMessageHandler.Object);
            return client;
        });

        var mockCosmosClient = new Mock<CosmosClient>();
        var mockCosmosDbSettings = new Mock<CosmosDBSettings>();

        _pagerDutyService = new PagerDutyService(_mockLogger.Object, mockHttpClientFactory.Object, _mockIncidentManagementSettings.Object, mockCosmosClient.Object, mockCosmosDbSettings.Object);
    }

    [Fact]
    public async Task TestGetIncidentsAsync()
    {
        var stringRes = await ReadJsonFromFileAsync("GetIncidents.json");
        MockHttpResponse(stringRes);
        var result = await _pagerDutyService.GetIncidentsAsync(10, 0);
        Assert.True(result.Count() > 0);
        Assert.NotEqual(result.FirstOrDefault()?.IncidentId, string.Empty);
    }

    [Fact]
    public async Task TestGetIncidentAsync()
    {
        string stringRes;
        PagerDutyIncident result;

        stringRes = await ReadJsonFromFileAsync("GetIncident.json");
        MockHttpResponse(stringRes);
        result = await _pagerDutyService.GetPagerDutyIncidentAsync("TestId");
        Assert.NotNull(result);
        Assert.NotEqual(result?.IncidentId, string.Empty);

        stringRes = await ReadJsonFromFileAsync("GetIncident_Body_Detail_Object.json");
        MockHttpResponse(stringRes);
        result = await _pagerDutyService.GetPagerDutyIncidentAsync("TestId");
        Assert.NotNull(result);
        Assert.NotEqual(result?.IncidentId, string.Empty);
    }

    [Fact]
    public async Task TestGetIncidentAsync_NotFound()
    {
        var stringRes = await ReadJsonFromFileAsync("GetIncident_NotFound.json");
        MockHttpResponse(stringRes, HttpStatusCode.NotFound);
        await Assert.ThrowsAnyAsync<Exception>(() => _pagerDutyService.GetPagerDutyIncidentAsync("TestId"));
    }

    [Fact]
    public async Task TestGetLatestIncidentDetails()
    {
        var stringRes = await ReadJsonFromFileAsync("GetLatestIncidentDetails.json");
        MockHttpResponse(stringRes);
        var result = await _pagerDutyService.GetLatestIncidentDetails("TestId");
        Assert.NotNull(result);
        Assert.True(result.Notes.Count > 0);
    }

    [Fact]
    public async Task TestGetIncidents_Fatal_Deserialization()
    {
        var stringRes = await ReadJsonFromFileAsync("GetIncidents_Fatal_Deserialization.json");
        MockHttpResponse(stringRes);
        await Assert.ThrowsAnyAsync<Exception>(() => _pagerDutyService.GetIncidentsAsync(1, 0));
    }

    #region Utility Methods
    private void MockHttpResponse(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var httpResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
        };
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
    }

    private async Task<string> ReadJsonFromFileAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Incident", "PagerDuty", fileName);
        string res = await File.ReadAllTextAsync(path);
        return res;
    }
    #endregion
}
