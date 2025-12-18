using System.Net.Http.Headers;

namespace Session.Proxy.Services;

/// <summary>
/// Service for proxying unmatched requests to another backend service.
/// </summary>
public class PythonProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PythonProxyService> _logger;
    private readonly string _targetBaseUrl;

    public PythonProxyService(IHttpClientFactory httpClientFactory, ILogger<PythonProxyService> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _targetBaseUrl = configuration["PythonProxyService:TargetUrl"] ?? "http://localhost:6000";
    }

    /// <summary>
    /// Forwards an HTTP request to the target service and copies the response to the HttpContext.
    /// </summary>
    public async Task ForwardRequestAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var targetUrl = $"{_targetBaseUrl.TrimEnd('/')}{context.Request.Path}{context.Request.QueryString}";

        _logger.LogInformation("Forwarding {Method} request to: {TargetUrl}", context.Request.Method, targetUrl);

        var httpClient = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

        // Copy headers from the original request
        foreach (var header in context.Request.Headers)
        {
            // Skip headers that will be set automatically by HttpClient
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                // If the header can't be added to request headers, it might be a content header
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Copy request body if present
        if (context.Request.ContentLength > 0 || context.Request.Body.CanRead)
        {
            var streamContent = new StreamContent(context.Request.Body);

            // Copy content type header if present
            if (context.Request.ContentType != null)
            {
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }

            request.Content = streamContent;
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            _logger.LogInformation("Received response with status code: {StatusCode}", response.StatusCode);

            // Copy response to HttpContext
            context.Response.StatusCode = (int)response.StatusCode;

            // Copy response headers
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Copy content headers and body
            if (response.Content != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }

                // Stream the response body
                await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding request to {TargetUrl}", targetUrl);
            throw;
        }
    }
}
