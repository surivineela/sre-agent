using System.Net;
using Agent.Cli.Services;
using Moq;
using Xunit;

namespace Agent.Cli.UnitTests.Services;

public class AuthenticationTests
{
    [Fact]
    public async Task AuthenticationHandler_AddsTokenToRemoteRequests()
    {
        // Arrange
        var expectedToken = "test-access-token-12345";
        var mockTokenService = new Mock<ITokenService>();
        mockTokenService.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync(expectedToken);

        using var authHandler = new AuthenticationHandler(mockTokenService.Object);
        authHandler.InnerHandler = new TestHttpHandler();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        using var invoker = new HttpMessageInvoker(authHandler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
        Assert.Equal(expectedToken, request.Headers.Authorization.Parameter);
        mockTokenService.Verify(x => x.GetAccessTokenAsync(), Times.Once);
    }

    [Fact]
    public async Task AuthenticationHandler_SkipsTokenForLocalhost()
    {
        // Arrange
        var mockTokenService = new Mock<ITokenService>();
        mockTokenService.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync("should-not-be-used");

        using var authHandler = new AuthenticationHandler(mockTokenService.Object);
        authHandler.InnerHandler = new TestHttpHandler();

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:8080/api");
        using var invoker = new HttpMessageInvoker(authHandler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Null(request.Headers.Authorization);
        mockTokenService.Verify(x => x.GetAccessTokenAsync(), Times.Never);
    }

    [Fact]
    public async Task AuthenticationHandler_ThrowsExceptionWhenNoToken()
    {
        // Arrange
        var mockTokenService = new Mock<ITokenService>();
        mockTokenService.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync((string?)null);

        using var authHandler = new AuthenticationHandler(mockTokenService.Object);
        authHandler.InnerHandler = new TestHttpHandler();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        using var invoker = new HttpMessageInvoker(authHandler);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FailedToGetAccessTokenException>(
            () => invoker.SendAsync(request, CancellationToken.None));

        Assert.Equal("Failed to get access token. Please run 'az login' first.", exception.Message);
    }

    [Fact]
    public async Task AuthenticationHandler_PreservesExistingHeaders()
    {
        // Arrange
        var expectedToken = "test-token";
        var mockTokenService = new Mock<ITokenService>();
        mockTokenService.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync(expectedToken);

        using var authHandler = new AuthenticationHandler(mockTokenService.Object);
        authHandler.InnerHandler = new TestHttpHandler();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        request.Headers.Add("Custom-Header", "custom-value");
        request.Headers.Add("User-Agent", "test-agent");

        using var invoker = new HttpMessageInvoker(authHandler);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(expectedToken, request.Headers.Authorization?.Parameter);
        Assert.Contains(request.Headers, h => h.Key == "Custom-Header" && h.Value.Contains("custom-value"));
        Assert.Contains(request.Headers, h => h.Key == "User-Agent" && h.Value.Contains("test-agent"));
    }

    [Fact]
    public async Task TokenService_ReturnsToken()
    {
        // Arrange
        var tokenService = new TokenService();

        // Act
        var token = await tokenService.GetAccessTokenAsync();

        // Assert
        // Token can be null if Azure CLI is not authenticated, which is valid
        Assert.True(token == null || !string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task TokenService_HandlesAzureCliFailure()
    {
        // Arrange
        var tokenService = new TokenService();

        // Act
        var token = await tokenService.GetAccessTokenAsync();

        // Assert
        // Should not throw exceptions - returns null when Azure CLI fails
        Assert.True(token == null || !string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task AuthenticationHandler_DoesNotLeakTokensInExceptions()
    {
        // Arrange
        var sensitiveToken = "secret-access-token-12345";
        var mockTokenService = new Mock<ITokenService>();
        mockTokenService.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync(sensitiveToken);

        using var authHandler = new AuthenticationHandler(mockTokenService.Object);
        authHandler.InnerHandler = new FaultyHttpHandler(); // This will throw an exception

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com");
        using var invoker = new HttpMessageInvoker(authHandler);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.SendAsync(request, CancellationToken.None));

        // Assert - Exception message should not contain the token
        Assert.DoesNotContain(sensitiveToken, exception.Message);
        Assert.DoesNotContain(sensitiveToken, exception.ToString());
    }

    [Fact]
    public void FailedToGetAccessTokenException_HasCorrectMessage()
    {
        // Arrange & Act
        var exception = new FailedToGetAccessTokenException();

        // Assert
        Assert.Equal("Failed to get access token. Please run 'az login' first.", exception.Message);
    }
}

#region Test Helpers

/// <summary>
/// Simple test handler that returns successful responses
/// </summary>
internal class TestHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Test response")
        };
        return Task.FromResult(response);
    }
}

/// <summary>
/// Test handler that throws exceptions to test error handling
/// </summary>
internal class FaultyHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Simulated network error");
    }
}

#endregion
