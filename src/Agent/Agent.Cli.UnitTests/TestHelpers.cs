// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using Agent.Cli.Models;
using Agent.Cli.Services;
using Moq;
using Moq.Protected;

namespace Agent.Cli.UnitTests;

public class TestHelpers
{
    public static Mock<HttpMessageHandler> CreateMockHttpMessageHandler(HttpStatusCode statusCode, string jsonResponse)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });
        return mockHandler;
    }

    public static ApiService CreateApiServiceWithMockedHttp(Mock<HttpMessageHandler> mockHandler)
    {
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.test.com/")
        };

        // Setup test configuration
        var testConfigPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid():N}.json");
        var testConfig = new CliConfiguration
        {
            ResourceUrl = "https://test.example.com",
            AuthRequired = false
        };
        File.WriteAllText(testConfigPath, JsonSerializer.Serialize(testConfig));

        // Create configuration service and real API service with injected dependencies
        var configService = new TestCliConfigurationService(testConfigPath);

        var tokenService = new Mock<ITokenService>().Object;
        return new ApiService(httpClient, configService, tokenService);
    }
}
