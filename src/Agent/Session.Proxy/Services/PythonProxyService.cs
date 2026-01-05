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

    /// <summary>
    /// Hop-by-hop headers as defined in RFC 2616, RFC 7230, and common proxy implementations.
    /// These headers are meaningful only for a single transport-level connection and must not be forwarded.
    /// </summary>
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        // RFC 2616 Section 13.5.1 - End-to-end and Hop-by-hop Headers
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailers",
        "Transfer-Encoding",
        "Upgrade",

        // Additional headers commonly treated as hop-by-hop
        "Proxy-Connection",  // Non-standard but widely used
        "Public",            // RFC 2068 (obsolete)
        "Alt-Svc",           // Alternative services - connection specific
    };

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

            // Copy response headers (skip hop-by-hop headers that shouldn't be forwarded)
            foreach (var header in response.Headers)
            {
                if (!IsHopByHopHeader(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
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

    /// <summary>
    /// Determines whether a header is a hop-by-hop header that should not be forwarded by proxies.
    /// </summary>
    /// <param name="headerName">The name of the header to check.</param>
    /// <returns>True if the header is a hop-by-hop header; otherwise, false.</returns>
    private static bool IsHopByHopHeader(string headerName) => HopByHopHeaders.Contains(headerName);
}
